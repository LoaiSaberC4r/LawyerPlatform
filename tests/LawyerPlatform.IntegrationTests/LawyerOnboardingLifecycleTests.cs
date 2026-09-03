using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using LawyerPlatform.Domain.ReferenceData;
using LawyerPlatform.Infrastructure.Persistence;
using LawyerPlatform.Infrastructure.Seeding;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using LawyerPlatform.Application.Notifications.Email;
using LawyerPlatform.Domain.Accounts;
using LawyerPlatform.Domain.Lawyers;

namespace LawyerPlatform.IntegrationTests;

public sealed class LawyerOnboardingLifecycleTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private static readonly int[] CivilLawSpecialization = [1];
    private static readonly AreaSeed OfficeArea = EgyptLocationSeedCatalog.Areas[0];
    private static readonly CitySeed OfficeCity = EgyptLocationSeedCatalog.Cities.Single(
        seed => seed.Id == OfficeArea.CityId);

    [Fact]
    public async Task LawyerApprovalLifecycle_EnforcesCompletionPrivacyAndPublicVisibility()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        var cancellationToken = TestContext.Current.CancellationToken;
        await factory.SeedDatabaseAsync(cancellationToken);
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
        Assert.False(initialProfile.GetProperty("hasProfileImage").GetBoolean());
        Assert.Equal(JsonValueKind.Null, initialProfile.GetProperty("profileImagePath").ValueKind);
        AssertObsoleteProfileImageFieldsAreAbsent(initialProfile);
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
        Assert.False(updatedProfile.GetProperty("hasProfileImage").GetBoolean());
        Assert.Equal(JsonValueKind.Null, updatedProfile.GetProperty("profileImagePath").ValueKind);
        AssertObsoleteProfileImageFieldsAreAbsent(updatedProfile);
        var profileRowVersion = updatedProfile.GetProperty("rowVersion").GetString()!;

        var (profileImageUpload, profileImageBytes) = await UploadProfileImageAsync(
            client,
            profileRowVersion,
            cancellationToken);
        Assert.True(profileImageUpload.GetProperty("hasProfileImage").GetBoolean());
        var profileImagePath = profileImageUpload.GetProperty("profileImagePath").GetString()!;
        Assert.StartsWith($"/uploads/lawyers/{lawyerId:N}/profile/", profileImagePath, StringComparison.Ordinal);
        Assert.EndsWith(".png", profileImagePath, StringComparison.Ordinal);
        AssertObsoleteProfileImageFieldsAreAbsent(profileImageUpload);
        profileRowVersion = profileImageUpload.GetProperty("rowVersion").GetString()!;

        client.DefaultRequestHeaders.Authorization = null;
        using (var staticImage = await client.GetAsync(profileImagePath, cancellationToken))
        {
            Assert.Equal(HttpStatusCode.OK, staticImage.StatusCode);
            Assert.Equal("image/png", staticImage.Content.Headers.ContentType?.MediaType);
            Assert.Equal(profileImageBytes, await staticImage.Content.ReadAsByteArrayAsync(cancellationToken));
        }

        var missingImagePath = profileImagePath[..(profileImagePath.LastIndexOf('/') + 1)] + "missing.png";
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await client.GetAsync(missingImagePath, cancellationToken)).StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", lawyerToken);

        using (var legacyOwnProfileImage = await client.GetAsync(
                   "/api/v1/lawyer/profile/image",
                   cancellationToken))
        {
            Assert.Equal(HttpStatusCode.OK, legacyOwnProfileImage.StatusCode);
            Assert.Equal(profileImageBytes, await legacyOwnProfileImage.Content.ReadAsByteArrayAsync(cancellationToken));
        }

        var ownProfileWithImage = await GetJsonAsync(client, "/api/v1/lawyer/profile", cancellationToken);
        Assert.True(ownProfileWithImage.GetProperty("hasProfileImage").GetBoolean());
        Assert.Equal(profileImagePath, ownProfileWithImage.GetProperty("profileImagePath").GetString());
        AssertObsoleteProfileImageFieldsAreAbsent(ownProfileWithImage);

        var office = await client.PutAsJsonAsync("/api/v1/lawyer/office", new
        {
            governorateId = OfficeCity.GovernorateId,
            cityId = OfficeCity.Id,
            areaId = OfficeArea.Id,
            detailedAddress = "Court Street, Building 5",
            publicPhoneNumber = "01012345678",
            latitude = 30.044420m,
            longitude = 31.235712m,
            rowVersion = (string?)null
        }, cancellationToken);
        Assert.True(office.StatusCode == HttpStatusCode.OK, await office.Content.ReadAsStringAsync(cancellationToken));
        var officeBody = await office.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        Assert.Equal(30.044420m, officeBody.GetProperty("latitude").GetDecimal());
        Assert.Equal(31.235712m, officeBody.GetProperty("longitude").GetDecimal());

        foreach (var invalidCoordinates in new[]
                 {
                     new { Latitude = (decimal?)90.000001m, Longitude = (decimal?)31.235712m },
                     new { Latitude = (decimal?)30.044420m, Longitude = (decimal?)180.000001m },
                     new { Latitude = (decimal?)30.044420m, Longitude = (decimal?)null }
                 })
        {
            var invalidOffice = await client.PutAsJsonAsync("/api/v1/lawyer/office", new
            {
                governorateId = OfficeCity.GovernorateId,
                cityId = OfficeCity.Id,
                areaId = OfficeArea.Id,
                detailedAddress = "Invalid coordinate update",
                publicPhoneNumber = "01012345678",
                latitude = invalidCoordinates.Latitude,
                longitude = invalidCoordinates.Longitude,
                rowVersion = officeBody.GetProperty("rowVersion").GetString()
            }, cancellationToken);
            Assert.Equal(HttpStatusCode.UnprocessableEntity, invalidOffice.StatusCode);
        }

        var ownProfileWithCoordinates = await GetJsonAsync(client, "/api/v1/lawyer/profile", cancellationToken);
        Assert.True(ownProfileWithCoordinates.GetProperty("hasProfileImage").GetBoolean());
        Assert.Equal(profileImagePath, ownProfileWithCoordinates.GetProperty("profileImagePath").GetString());
        Assert.Equal(
            30.044420m,
            ownProfileWithCoordinates.GetProperty("primaryOffice").GetProperty("latitude").GetDecimal());
        Assert.Equal(
            31.235712m,
            ownProfileWithCoordinates.GetProperty("primaryOffice").GetProperty("longitude").GetDecimal());

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

        string privateDocumentStorageKey;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<LawyerPlatformDbContext>();
            var membershipDocumentId = membershipDocument.GetProperty("id").GetGuid();
            privateDocumentStorageKey = await dbContext.LawyerDocuments
                .Where(document => document.Id == membershipDocumentId)
                .Select(document => document.StorageKey)
                .SingleAsync(cancellationToken);
        }

        client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await client.GetAsync($"/uploads/{privateDocumentStorageKey}", cancellationToken)).StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", lawyerToken);

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
        Assert.True(clarificationBody.GetProperty("hasProfileImage").GetBoolean());
        Assert.Equal(profileImagePath, clarificationBody.GetProperty("profileImagePath").GetString());
        AssertObsoleteProfileImageFieldsAreAbsent(clarificationBody);

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
        var adminProfessionalProfile = adminDetails.GetProperty("professionalProfile");
        Assert.True(adminProfessionalProfile.GetProperty("hasProfileImage").GetBoolean());
        Assert.Equal(profileImagePath, adminProfessionalProfile.GetProperty("profileImagePath").GetString());
        AssertObsoleteProfileImageFieldsAreAbsent(adminProfessionalProfile);
        Assert.DoesNotContain("storageKey", adminJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("passwordHash", adminJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("fileBytes", adminJson, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(30.044420m, adminDetails.GetProperty("primaryOffice").GetProperty("latitude").GetDecimal());
        Assert.Equal(31.235712m, adminDetails.GetProperty("primaryOffice").GetProperty("longitude").GetDecimal());
        Assert.True(adminDetails.GetProperty("statusHistory").GetArrayLength() >= 4);
        Assert.All(adminDetails.GetProperty("documents").EnumerateArray(), document =>
        {
            Assert.StartsWith(
                $"/api/v1/admin/lawyers/{lawyerId}/documents/",
                document.GetProperty("contentUrl").GetString(),
                StringComparison.Ordinal);
            Assert.Contains("/content", document.GetProperty("contentUrl").GetString(), StringComparison.Ordinal);
        });
        Assert.Equal(
            HttpStatusCode.OK,
            (await client.GetAsync($"/api/v1/admin/lawyers/{lawyerId}/profile-image", cancellationToken)).StatusCode);

        var adminList = await GetJsonAsync(
            client,
            "/api/v1/admin/lawyers?pageNumber=1&pageSize=100",
            cancellationToken);
        var adminListItem = adminList.GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("id").GetGuid() == lawyerId);
        Assert.True(adminListItem.GetProperty("hasProfileImage").GetBoolean());
        Assert.Equal(profileImagePath, adminListItem.GetProperty("profileImagePath").GetString());
        AssertObsoleteProfileImageFieldsAreAbsent(adminListItem);

        client.DefaultRequestHeaders.Authorization = null;
        Assert.True(await PublicSearchContainsAsync(client, lawyerId, cancellationToken));
        var publicDetails = await GetJsonAsync(client, $"/api/v1/public/lawyers/{lawyerId}", cancellationToken);
        var publicJson = publicDetails.GetRawText();
        Assert.True(publicDetails.GetProperty("hasProfileImage").GetBoolean());
        Assert.Equal(profileImagePath, publicDetails.GetProperty("profileImagePath").GetString());
        AssertObsoleteProfileImageFieldsAreAbsent(publicDetails);
        Assert.DoesNotContain("documents", publicJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("storageKey", publicJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("professionalRegistrationNumber", publicJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("userAccountId", publicJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("latitude", publicJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("longitude", publicJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("lawyerOfficeMapUrl", publicJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("googleMapsUrl", publicJson, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(
            HttpStatusCode.OK,
            (await client.GetAsync($"/api/v1/public/lawyers/{lawyerId}/profile-image", cancellationToken)).StatusCode);
        var publicList = await GetJsonAsync(
            client,
            "/api/v1/public/lawyers?pageNumber=1&pageSize=100",
            cancellationToken);
        var publicListItem = publicList.GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("id").GetGuid() == lawyerId);
        Assert.True(publicListItem.GetProperty("hasProfileImage").GetBoolean());
        Assert.Equal(profileImagePath, publicListItem.GetProperty("profileImagePath").GetString());
        AssertObsoleteProfileImageFieldsAreAbsent(publicListItem);
        var publicListJson = publicList.GetRawText();
        Assert.DoesNotContain("latitude", publicListJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("longitude", publicListJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("lawyerOfficeMapUrl", publicListJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("googleMapsUrl", publicListJson, StringComparison.OrdinalIgnoreCase);

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
        var reactivateBody = await reactivate.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var invalidReactivate = await client.PostAsJsonAsync($"/api/v1/admin/lawyers/{lawyerId}/reactivate", new
        {
            rowVersion = reactivateBody.GetProperty("rowVersion").GetString()
        }, cancellationToken);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, invalidReactivate.StatusCode);

        client.DefaultRequestHeaders.Authorization = null;
        Assert.True(await PublicSearchContainsAsync(client, lawyerId, cancellationToken));

        var pendingRejectedLawyer = await SeedPendingLawyerAsync(cancellationToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var reject = await client.PostAsJsonAsync(
            $"/api/v1/admin/lawyers/{pendingRejectedLawyer.LawyerId}/reject",
            new
            {
                reason = "Application requirements were not met.",
                rowVersion = pendingRejectedLawyer.RowVersion
            },
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, reject.StatusCode);
        var rejectBody = await reject.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var invalidReject = await client.PostAsJsonAsync(
            $"/api/v1/admin/lawyers/{pendingRejectedLawyer.LawyerId}/reject",
            new
            {
                reason = "Duplicate invalid rejection.",
                rowVersion = rejectBody.GetProperty("rowVersion").GetString()
            },
            cancellationToken);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, invalidReject.StatusCode);

        await using var notificationScope = factory.Services.CreateAsyncScope();
        var notificationContext = notificationScope.ServiceProvider.GetRequiredService<LawyerPlatformDbContext>();
        var lifecycleNotifications = await notificationContext.EmailOutboxMessages
            .AsNoTracking()
            .Where(message => message.AggregateId == lawyerId)
            .ToListAsync(cancellationToken);
        var rejectedNotifications = await notificationContext.EmailOutboxMessages
            .AsNoTracking()
            .Where(message => message.AggregateId == pendingRejectedLawyer.LawyerId)
            .ToListAsync(cancellationToken);

        Assert.Equal(7, lifecycleNotifications.Count);
        Assert.Contains(lifecycleNotifications, message =>
            message.NotificationType == EmailNotificationType.LawyerRegistrationWelcome &&
            message.RecipientEmail == "ahmed.lifecycle@example.test");
        Assert.Equal(2, lifecycleNotifications.Count(message =>
            message.NotificationType == EmailNotificationType.LawyerSubmittedForApproval &&
            message.RecipientEmail == "admin@lawyerplatform.test"));
        Assert.Contains(lifecycleNotifications, message =>
            message.NotificationType == EmailNotificationType.LawyerChangesRequested &&
            message.RecipientEmail == "ahmed.lifecycle@example.test");
        Assert.Contains(lifecycleNotifications, message =>
            message.NotificationType == EmailNotificationType.LawyerApproved);
        Assert.Contains(lifecycleNotifications, message =>
            message.NotificationType == EmailNotificationType.LawyerSuspended);
        Assert.Contains(lifecycleNotifications, message =>
            message.NotificationType == EmailNotificationType.LawyerReactivated);
        var rejectedNotification = Assert.Single(rejectedNotifications);
        Assert.Equal(EmailNotificationType.LawyerRejected, rejectedNotification.NotificationType);
        Assert.Equal("rejected.notification@example.test", rejectedNotification.RecipientEmail);
        Assert.All(lifecycleNotifications.Concat(rejectedNotifications), message =>
        {
            Assert.DoesNotContain("Clarified commercial", message.HtmlBody, StringComparison.Ordinal);
            Assert.DoesNotContain("storageKey", message.HtmlBody, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("password", message.HtmlBody, StringComparison.OrdinalIgnoreCase);
        });
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
        if (await dbContext.LegalSpecializations.FindAsync([1], cancellationToken) is not null)
        {
            return;
        }

        dbContext.LegalSpecializations.Add(LegalSpecialization.Create(1, "قانون مدني", "Civil Law", 1).Value);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<(Guid LawyerId, string RowVersion)> SeedPendingLawyerAsync(
        CancellationToken cancellationToken)
    {
        var nowUtc = new DateTime(2026, 8, 16, 10, 0, 0, DateTimeKind.Utc);
        var account = UserAccount.CreateLawyer(
            "rejected.notification",
            "REJECTED.NOTIFICATION",
            "rejected.notification@example.test",
            "REJECTED.NOTIFICATION@EXAMPLE.TEST",
            "01077777777",
            "hash",
            nowUtc).Value;
        var profile = LawyerProfile.Create(account, "Rejected Notification Lawyer").Value;
        profile.UpdateProfessionalProfile(
            "Rejected Notification Lawyer",
            "Attorney",
            "Private biography",
            5,
            "REG-REJECT-NOTIFICATION");
        profile.UpsertPrimaryOffice(
            OfficeCity.GovernorateId,
            OfficeCity.Id,
            OfficeArea.Id,
            "Complete office address",
            null);
        profile.ReplaceSpecializations(CivilLawSpecialization);
        profile.AddDocument(
            "IdentityVerification",
            "documents/reject-id.pdf",
            "identity.pdf",
            "application/pdf",
            100,
            nowUtc);
        profile.AddDocument(
            "ProfessionalMembership",
            "documents/reject-membership.pdf",
            "membership.pdf",
            "application/pdf",
            100,
            nowUtc);
        Assert.True(profile.SubmitForApproval(account.Id, true, true, nowUtc.AddMinutes(1)).IsSuccess);

        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<LawyerPlatformDbContext>();
        context.LawyerProfiles.Add(profile);
        await context.SaveChangesAsync(cancellationToken);
        return (profile.Id, Convert.ToBase64String(profile.RowVersion));
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

    private static async Task<(JsonElement Body, byte[] Bytes)> UploadProfileImageAsync(
        HttpClient client,
        string rowVersion,
        CancellationToken cancellationToken)
    {
        var bytes = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(rowVersion), "rowVersion");
        var image = new ByteArrayContent(bytes);
        image.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(image, "image", "profile.png");

        var response = await client.PutAsync("/api/v1/lawyer/profile/image", content, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken), bytes);
    }

    private static void AssertObsoleteProfileImageFieldsAreAbsent(JsonElement response)
    {
        Assert.False(response.TryGetProperty("profileImageUrl", out _));
        Assert.False(response.TryGetProperty("profileImageContentUrl", out _));
        Assert.False(response.TryGetProperty("profileImageStorageKey", out _));
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
