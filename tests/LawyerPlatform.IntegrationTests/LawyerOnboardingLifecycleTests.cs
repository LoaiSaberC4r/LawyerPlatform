using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using LawyerPlatform.Domain.ReferenceData;
using LawyerPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace LawyerPlatform.IntegrationTests;

public sealed class LawyerOnboardingLifecycleTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private static readonly int[] CivilLawSpecialization = [1];

    [Fact]
    public async Task LawyerApprovalLifecycle_EnforcesCompletionPrivacyAndPublicVisibility()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        var cancellationToken = TestContext.Current.CancellationToken;
        await SeedReferenceDataAsync(cancellationToken);

        var registration = await client.PostAsJsonAsync("/api/v1/auth/lawyers/register", new
        {
            fullName = "Ahmed Mohamed Ali",
            userName = "ahmed.lifecycle",
            email = "ahmed.lifecycle@example.test",
            phoneNumber = "01012345678",
            password = "LawyerPassword1"
        }, cancellationToken);
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
        var registrationBody = await registration.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var lawyerId = registrationBody.GetProperty("lawyerProfileId").GetGuid();
        Assert.Equal("Draft", registrationBody.GetProperty("approvalStatus").GetString());

        var lawyerToken = await LoginAndReadTokenAsync(client, "ahmed.lifecycle", "LawyerPassword1", cancellationToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", lawyerToken);

        var initialProfile = await GetJsonAsync(client, "/api/v1/lawyer/profile", cancellationToken);
        Assert.Equal("Draft", initialProfile.GetProperty("approvalStatus").GetString());
        Assert.False(initialProfile.GetProperty("completion").GetProperty("profileIsComplete").GetBoolean());
        var initialRowVersion = initialProfile.GetProperty("rowVersion").GetString()!;

        client.DefaultRequestHeaders.Authorization = null;
        Assert.False(await PublicSearchContainsAsync(client, lawyerId, cancellationToken));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", lawyerToken);

        var profileUpdate = await client.PutAsJsonAsync("/api/v1/lawyer/profile", new
        {
            fullName = "Ahmed Mohamed Ali",
            professionalTitle = "Attorney at Law",
            biography = "Commercial and civil litigation experience.",
            yearsOfExperience = 12,
            professionalRegistrationNumber = "LAW-123456",
            rowVersion = initialRowVersion
        }, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, profileUpdate.StatusCode);
        var updatedProfile = await profileUpdate.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var profileRowVersion = updatedProfile.GetProperty("rowVersion").GetString()!;

        var office = await client.PutAsJsonAsync("/api/v1/lawyer/office", new
        {
            governorateId = 1,
            cityId = 10,
            areaId = 100,
            detailedAddress = "Court Street, Building 5",
            publicPhoneNumber = "01012345678",
            rowVersion = (string?)null
        }, cancellationToken);
        Assert.True(office.StatusCode == HttpStatusCode.OK, await office.Content.ReadAsStringAsync(cancellationToken));

        var staleUpdate = await client.PutAsJsonAsync("/api/v1/lawyer/profile", new
        {
            fullName = "Ahmed Mohamed Ali",
            professionalTitle = "Attorney at Law",
            biography = "Stale edit",
            yearsOfExperience = 12,
            professionalRegistrationNumber = "LAW-123456",
            rowVersion = initialRowVersion
        }, cancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, staleUpdate.StatusCode);
        await AssertProblemCodeAsync(staleUpdate, "Lawyer.ConcurrencyConflict", cancellationToken);

        profileRowVersion = (await GetJsonAsync(client, "/api/v1/lawyer/profile", cancellationToken))
            .GetProperty("rowVersion").GetString()!;
        var specializations = await client.PutAsJsonAsync("/api/v1/lawyer/specializations", new
        {
            specializationIds = CivilLawSpecialization,
            rowVersion = profileRowVersion
        }, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, specializations.StatusCode);

        var identityDocument = await UploadPdfAsync(client, "IdentityVerification", "identity.pdf", cancellationToken);
        Assert.False(identityDocument.TryGetProperty("storageKey", out _));
        var membershipDocument = await UploadPdfAsync(client, "ProfessionalMembership", "membership.pdf", cancellationToken);
        Assert.False(membershipDocument.TryGetProperty("storageKey", out _));

        var deletedDocumentId = identityDocument.GetProperty("id").GetGuid();
        using (var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/lawyer/documents/{deletedDocumentId}"))
        {
            deleteRequest.Headers.TryAddWithoutValidation("If-Match", identityDocument.GetProperty("rowVersion").GetString());
            var delete = await client.SendAsync(deleteRequest, cancellationToken);
            Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        }

        var documentsAfterDelete = await GetJsonAsync(client, "/api/v1/lawyer/documents", cancellationToken);
        Assert.DoesNotContain(documentsAfterDelete.EnumerateArray(), item => item.GetProperty("id").GetGuid() == deletedDocumentId);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync($"/api/v1/lawyer/documents/{deletedDocumentId}/content", cancellationToken)).StatusCode);
        identityDocument = await UploadPdfAsync(client, "IdentityVerification", "identity-replacement.pdf", cancellationToken);

        var completeProfile = await GetJsonAsync(client, "/api/v1/lawyer/profile", cancellationToken);
        Assert.True(completeProfile.GetProperty("completion").GetProperty("profileIsComplete").GetBoolean());
        profileRowVersion = completeProfile.GetProperty("rowVersion").GetString()!;

        using var submitRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/lawyer/submit-for-approval");
        submitRequest.Headers.TryAddWithoutValidation("If-Match", profileRowVersion);
        var submit = await client.SendAsync(submitRequest, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, submit.StatusCode);
        var submitBody = await submit.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        Assert.Equal("PendingApproval", submitBody.GetProperty("approvalStatus").GetString());
        var pendingRowVersion = submitBody.GetProperty("rowVersion").GetString()!;

        client.DefaultRequestHeaders.Authorization = null;
        Assert.False(await PublicSearchContainsAsync(client, lawyerId, cancellationToken));

        var adminToken = await LoginAndChangeAdminPasswordAsync(client, cancellationToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var requestChanges = await client.PostAsJsonAsync($"/api/v1/admin/lawyers/{lawyerId}/request-changes", new
        {
            explanation = "Please clarify the biography.",
            rowVersion = pendingRowVersion
        }, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, requestChanges.StatusCode);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", lawyerToken);
        var changedProfile = await GetJsonAsync(client, "/api/v1/lawyer/profile", cancellationToken);
        var changesRowVersion = changedProfile.GetProperty("rowVersion").GetString()!;
        var clarification = await client.PutAsJsonAsync("/api/v1/lawyer/profile", new
        {
            fullName = "Ahmed Mohamed Ali",
            professionalTitle = "Attorney at Law",
            biography = "Clarified commercial and civil litigation experience.",
            yearsOfExperience = 12,
            professionalRegistrationNumber = "LAW-123456",
            rowVersion = changesRowVersion
        }, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, clarification.StatusCode);
        var clarificationBody = await clarification.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);

        using var resubmitRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/lawyer/submit-for-approval");
        resubmitRequest.Headers.TryAddWithoutValidation("If-Match", clarificationBody.GetProperty("rowVersion").GetString());
        var resubmit = await client.SendAsync(resubmitRequest, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, resubmit.StatusCode);
        var resubmitBody = await resubmit.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var approve = await client.PostAsJsonAsync($"/api/v1/admin/lawyers/{lawyerId}/approve", new
        {
            rowVersion = resubmitBody.GetProperty("rowVersion").GetString()
        }, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, approve.StatusCode);
        var approveBody = await approve.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        Assert.Equal("Approved", approveBody.GetProperty("approvalStatus").GetString());

        var adminDetails = await GetJsonAsync(client, $"/api/v1/admin/lawyers/{lawyerId}", cancellationToken);
        var adminJson = adminDetails.GetRawText();
        Assert.DoesNotContain("storageKey", adminJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("passwordHash", adminJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("fileBytes", adminJson, StringComparison.OrdinalIgnoreCase);
        Assert.True(adminDetails.GetProperty("statusHistory").GetArrayLength() >= 4);

        client.DefaultRequestHeaders.Authorization = null;
        Assert.True(await PublicSearchContainsAsync(client, lawyerId, cancellationToken));
        var publicDetails = await GetJsonAsync(client, $"/api/v1/public/lawyers/{lawyerId}", cancellationToken);
        var publicJson = publicDetails.GetRawText();
        Assert.DoesNotContain("documents", publicJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("storageKey", publicJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("professionalRegistrationNumber", publicJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("userAccountId", publicJson, StringComparison.OrdinalIgnoreCase);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", lawyerToken);
        using (var protectedDeleteRequest = new HttpRequestMessage(
            HttpMethod.Delete,
            $"/api/v1/lawyer/documents/{identityDocument.GetProperty("id").GetGuid()}"))
        {
            protectedDeleteRequest.Headers.TryAddWithoutValidation("If-Match", identityDocument.GetProperty("rowVersion").GetString());
            var protectedDelete = await client.SendAsync(protectedDeleteRequest, cancellationToken);
            Assert.Equal(HttpStatusCode.UnprocessableEntity, protectedDelete.StatusCode);
            await AssertProblemCodeAsync(protectedDelete, "Lawyer.RequiredDocumentCannotBeRemoved", cancellationToken);
        }

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var suspend = await client.PostAsJsonAsync($"/api/v1/admin/lawyers/{lawyerId}/suspend", new
        {
            reason = "Temporary compliance hold.",
            rowVersion = approveBody.GetProperty("rowVersion").GetString()
        }, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, suspend.StatusCode);
        var suspendBody = await suspend.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);

        client.DefaultRequestHeaders.Authorization = null;
        Assert.False(await PublicSearchContainsAsync(client, lawyerId, cancellationToken));
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync($"/api/v1/public/lawyers/{lawyerId}", cancellationToken)).StatusCode);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var reactivate = await client.PostAsJsonAsync($"/api/v1/admin/lawyers/{lawyerId}/reactivate", new
        {
            rowVersion = suspendBody.GetProperty("rowVersion").GetString()
        }, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, reactivate.StatusCode);

        client.DefaultRequestHeaders.Authorization = null;
        Assert.True(await PublicSearchContainsAsync(client, lawyerId, cancellationToken));
    }

    [Fact]
    public async Task Authorization_BlocksAnonymousClientAndCrossLawyerAccess()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        var cancellationToken = TestContext.Current.CancellationToken;

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.GetAsync("/api/v1/lawyer/profile", cancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.GetAsync($"/api/v1/admin/lawyers/{Guid.NewGuid()}", cancellationToken)).StatusCode);

        await client.PostAsJsonAsync("/api/v1/auth/clients/register", new
        {
            fullName = "Client Authorization",
            userName = "client.authorization",
            email = "client.authorization@example.test",
            phoneNumber = "01099999991",
            password = "ClientPassword1"
        }, cancellationToken);
        var clientToken = await LoginAndReadTokenAsync(client, "client.authorization", "ClientPassword1", cancellationToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", clientToken);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.GetAsync("/api/v1/lawyer/profile", cancellationToken)).StatusCode);

        client.DefaultRequestHeaders.Authorization = null;
        await client.PostAsJsonAsync("/api/v1/auth/lawyers/register", new
        {
            fullName = "Lawyer Authorization",
            userName = "lawyer.authorization",
            email = "lawyer.authorization@example.test",
            phoneNumber = "01099999992",
            password = "LawyerPassword1"
        }, cancellationToken);
        var lawyerToken = await LoginAndReadTokenAsync(client, "lawyer.authorization", "LawyerPassword1", cancellationToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", lawyerToken);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.GetAsync("/api/v1/admin/lawyers", cancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync($"/api/v1/lawyer/documents/{Guid.NewGuid()}/content", cancellationToken)).StatusCode);
    }

    private async Task SeedReferenceDataAsync(CancellationToken cancellationToken)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<LawyerPlatformDbContext>();
        if (await dbContext.Governorates.FindAsync([1], cancellationToken) is not null)
        {
            return;
        }

        dbContext.Governorates.Add(Governorate.Create(1, "القاهرة", "Cairo", 1).Value);
        dbContext.Cities.Add(City.Create(10, 1, "مدينة نصر", "Nasr City", 1).Value);
        dbContext.Areas.Add(Area.Create(100, 10, "المنطقة الأولى", "First District", 1).Value);
        dbContext.LegalSpecializations.Add(LegalSpecialization.Create(1, "قانون مدني", "Civil Law", 1).Value);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task<JsonElement> UploadPdfAsync(
        HttpClient client,
        string documentType,
        string fileName,
        CancellationToken cancellationToken)
    {
        var bytes = Encoding.ASCII.GetBytes("%PDF-1.4\n1 0 obj\n<<>>\nendobj\n%%EOF");
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(documentType), "documentType");
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(file, "file", fileName);
        var response = await client.PostAsync("/api/v1/lawyer/documents", content, cancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
    }

    private static async Task<string> LoginAndReadTokenAsync(
        HttpClient client,
        string identifier,
        string password,
        CancellationToken cancellationToken)
    {
        client.DefaultRequestHeaders.Authorization = null;
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            userNameOrEmail = identifier,
            password
        }, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken))
            .GetProperty("accessToken").GetString()!;
    }

    private static async Task<string> LoginAndChangeAdminPasswordAsync(
        HttpClient client,
        CancellationToken cancellationToken)
    {
        var restrictedToken = await LoginAndReadTokenAsync(client, "superadmin", "InitialPassword1", cancellationToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", restrictedToken);
        var change = await client.PostAsJsonAsync("/api/v1/auth/change-password", new
        {
            currentPassword = "InitialPassword1",
            newPassword = "ChangedAdminPassword2"
        }, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, change.StatusCode);
        return (await change.Content.ReadFromJsonAsync<JsonElement>(cancellationToken))
            .GetProperty("accessToken").GetString()!;
    }

    private static async Task<JsonElement> GetJsonAsync(
        HttpClient client,
        string path,
        CancellationToken cancellationToken)
    {
        var response = await client.GetAsync(path, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
    }

    private static async Task<bool> PublicSearchContainsAsync(
        HttpClient client,
        Guid lawyerId,
        CancellationToken cancellationToken)
    {
        var response = await client.GetAsync("/api/v1/public/lawyers?pageNumber=1&pageSize=100", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return body.GetProperty("items").EnumerateArray().Any(item => item.GetProperty("id").GetGuid() == lawyerId);
    }

    private static async Task AssertProblemCodeAsync(
        HttpResponseMessage response,
        string expectedCode,
        CancellationToken cancellationToken)
    {
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        Assert.Contains(problem.GetProperty("errors").EnumerateArray(),
            error => error.GetProperty("code").GetString() == expectedCode);
    }
}
