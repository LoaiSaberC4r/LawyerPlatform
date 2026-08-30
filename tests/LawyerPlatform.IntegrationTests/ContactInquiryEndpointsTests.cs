using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BuildingBlock.Application.Abstraction.Encryption;
using LawyerPlatform.Application.Notifications.Email;
using LawyerPlatform.Domain.Accounts;
using LawyerPlatform.Domain.ContactInquiries;
using LawyerPlatform.Domain.Resources;
using LawyerPlatform.Infrastructure.Email;
using LawyerPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LawyerPlatform.IntegrationTests;

public sealed class ContactInquiryEndpointsTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public async Task PublicCreatePersistsInquiryAndTwoOutboxRowsWhileAdminReadsAreRoleProtectedAndProjected()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = CreateClient();

        using (var anonymousAdmin = await client.GetAsync(
                   "/api/v1/admin/contact-inquiries",
                   cancellationToken))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, anonymousAdmin.StatusCode);
        }

        await PhaseOneTestHelpers.RegisterClientAsync(
            client,
            "contact.denied.client",
            "contact.denied.client@example.test",
            "01070000001",
            "Contact Client",
            cancellationToken);
        await PhaseOneTestHelpers.RegisterLawyerAsync(
            client,
            "contact.denied.lawyer",
            "contact.denied.lawyer@example.test",
            "01170000001",
            "Contact Lawyer",
            cancellationToken);
        var clientToken = await PhaseOneTestHelpers.LoginAndReadTokenAsync(
            client,
            "contact.denied.client",
            PhaseOneTestHelpers.ClientPassword,
            cancellationToken);
        var lawyerToken = await PhaseOneTestHelpers.LoginAndReadTokenAsync(
            client,
            "contact.denied.lawyer",
            PhaseOneTestHelpers.LawyerPassword,
            cancellationToken);

        PhaseOneTestHelpers.UseToken(client, clientToken);
        using (var clientDenied = await client.GetAsync(
                   "/api/v1/admin/contact-inquiries",
                   cancellationToken))
        {
            Assert.Equal(HttpStatusCode.Forbidden, clientDenied.StatusCode);
        }

        PhaseOneTestHelpers.UseToken(client, lawyerToken);
        using (var lawyerDenied = await client.GetAsync(
                   "/api/v1/admin/contact-inquiries",
                   cancellationToken))
        {
            Assert.Equal(HttpStatusCode.Forbidden, lawyerDenied.StatusCode);
        }

        var sender = factory.Services.GetRequiredService<TestPasswordResetEmailSender>();
        sender.Clear();
        sender.FailSending = true;
        client.DefaultRequestHeaders.Authorization = null;

        var createdIds = new List<Guid>();
        for (var index = 0; index < 3; index++)
        {
            var fullName = index == 0
                ? "<script>alert(1)</script>"
                : $"Contact Person {index}";
            var message = index == 0
                ? "first line\n<script>alert(2)</script>"
                : $"Message {index}";
            using var response = await client.PostAsJsonAsync(
                "/api/v1/public/contact-us",
                new
                {
                    fullName,
                    phoneNumber = $"0101234567{index}",
                    email = $"contact.{index}@example.test",
                    inquiryType = index == 2 ? "Frontend Defined Category" : "Technical Support",
                    message
                },
                cancellationToken);

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
            Assert.True(body.TryGetProperty("id", out var idProperty));
            Assert.True(body.TryGetProperty("createdOnUtc", out _));
            Assert.False(body.TryGetProperty("message", out _));
            Assert.False(body.TryGetProperty("email", out _));
            Assert.False(body.TryGetProperty("phoneNumber", out _));
            createdIds.Add(idProperty.GetGuid());
        }

        Assert.Empty(sender.Messages);

        List<Guid> expectedOrder;
        await using (var verificationScope = factory.Services.CreateAsyncScope())
        {
            var dbContext = verificationScope.ServiceProvider
                .GetRequiredService<LawyerPlatformDbContext>();
            var inquiries = await dbContext.ContactInquiries
                .AsNoTracking()
                .Where(inquiry => createdIds.Contains(inquiry.Id))
                .OrderByDescending(inquiry => inquiry.CreatedOnUtc)
                .ThenByDescending(inquiry => inquiry.Id)
                .ToListAsync(cancellationToken);
            var notifications = await dbContext.EmailOutboxMessages
                .AsNoTracking()
                .Where(message => createdIds.Contains(message.AggregateId))
                .OrderBy(message => message.CreatedOnUtc)
                .ThenBy(message => message.Id)
                .ToListAsync(cancellationToken);

            Assert.Equal(3, inquiries.Count);
            Assert.Equal(6, notifications.Count);
            Assert.All(createdIds, id =>
            {
                var perInquiry = notifications
                    .Where(notification => notification.AggregateId == id)
                    .ToArray();
                Assert.Equal(2, perInquiry.Length);
                Assert.Contains(perInquiry, notification =>
                    notification.NotificationType == EmailNotificationType.ContactInquirySupportNotification &&
                    notification.RecipientEmail == "Support@avokatoo.com" &&
                    notification.IdempotencyKey == $"ContactInquirySupport:{id:N}");
                Assert.Contains(perInquiry, notification =>
                    notification.NotificationType == EmailNotificationType.ContactInquiryConfirmation &&
                    notification.RecipientEmail == $"contact.{createdIds.IndexOf(id)}@example.test" &&
                    notification.IdempotencyKey == $"ContactInquiryConfirmation:{id:N}");
                Assert.Equal(2, perInquiry.Select(notification => notification.IdempotencyKey).Distinct().Count());
                Assert.All(perInquiry, notification =>
                    Assert.Equal(EmailOutboxStatus.Pending, notification.Status));
            });

            var maliciousInquiry = inquiries.Single(inquiry => inquiry.Email == "contact.0@example.test");
            Assert.Equal("<script>alert(1)</script>", maliciousInquiry.FullName);
            Assert.Equal("first line\n<script>alert(2)</script>", maliciousInquiry.Message);
            var maliciousBodies = notifications
                .Where(notification => notification.AggregateId == maliciousInquiry.Id)
                .Select(notification => notification.HtmlBody)
                .ToArray();
            Assert.All(maliciousBodies, body =>
            {
                Assert.DoesNotContain("<script>", body, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("&lt;script&gt;", body, StringComparison.Ordinal);
            });

            expectedOrder = inquiries.Select(inquiry => inquiry.Id).ToList();
        }

        await CreateSuperAdminAsync(cancellationToken);
        var adminToken = await PhaseOneTestHelpers.LoginAndChangeAdminPasswordAsync(
            client,
            cancellationToken);
        PhaseOneTestHelpers.UseToken(client, adminToken);

        using var listResponse = await client.GetAsync(
            "/api/v1/admin/contact-inquiries?pageNumber=1&pageSize=2",
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var listBody = await listResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        Assert.Equal(1, listBody.GetProperty("pageNumber").GetInt32());
        Assert.Equal(2, listBody.GetProperty("pageSize").GetInt32());
        Assert.Equal(3, listBody.GetProperty("totalItems").GetInt64());
        var items = listBody.GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal(2, items.Length);
        Assert.Equal(expectedOrder.Take(2), items.Select(item => item.GetProperty("id").GetGuid()));
        Assert.All(items, item => Assert.False(item.TryGetProperty("message", out _)));

        using var detailsResponse = await client.GetAsync(
            $"/api/v1/admin/contact-inquiries/{createdIds[0]}",
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);
        var details = await detailsResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        Assert.Equal(createdIds[0], details.GetProperty("id").GetGuid());
        Assert.Equal("first line\n<script>alert(2)</script>", details.GetProperty("message").GetString());

        using var missingRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/v1/admin/contact-inquiries/{Guid.NewGuid()}");
        missingRequest.Headers.AcceptLanguage.ParseAdd("en");
        using var missing = await client.SendAsync(missingRequest, cancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        await AssertProblemAsync(
            missing,
            "ContactInquiry.NotFound",
            ErrorMessage.ContactInquiryNotFound,
            cancellationToken);
    }

    [Fact]
    public async Task AcceptLanguageLocalizesValidationAndAuthorizationWhileCodesStayStableAndQueryLangIsIgnored()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = CreateClient();

        using var english = await SendInvalidCreateAsync(client, "en", cancellationToken);
        using var arabic = await SendInvalidCreateAsync(client, "ar", cancellationToken);
        using var queryStringIgnored = await SendInvalidCreateAsync(
            client,
            "ar-EG",
            cancellationToken,
            "?lang=en");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, english.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, arabic.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, queryStringIgnored.StatusCode);
        var englishProblem = await english.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var arabicProblem = await arabic.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var ignoredProblem = await queryStringIgnored.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var englishError = englishProblem.GetProperty("errors")[0];
        var arabicError = arabicProblem.GetProperty("errors")[0];
        var ignoredError = ignoredProblem.GetProperty("errors")[0];

        Assert.Equal("Validation.ContactInquiry.FullNameRequired", englishError.GetProperty("code").GetString());
        Assert.Equal(englishError.GetProperty("code").GetString(), arabicError.GetProperty("code").GetString());
        Assert.Equal("Full name is required.", englishError.GetProperty("message").GetString());
        Assert.Equal("الاسم الكامل مطلوب.", arabicError.GetProperty("message").GetString());
        Assert.Equal("الاسم الكامل مطلوب.", ignoredError.GetProperty("message").GetString());
        Assert.Equal("Full name is required.", englishProblem.GetProperty("detail").GetString());
        Assert.Equal("الاسم الكامل مطلوب.", arabicProblem.GetProperty("detail").GetString());
        Assert.Equal("Validation Error", englishProblem.GetProperty("title").GetString());
        Assert.Equal("خطأ في التحقق", arabicProblem.GetProperty("title").GetString());
        Assert.Equal("en", Assert.Single(english.Content.Headers.ContentLanguage));
        Assert.Equal("ar", Assert.Single(arabic.Content.Headers.ContentLanguage));
        Assert.Equal("ar", Assert.Single(queryStringIgnored.Content.Headers.ContentLanguage));

        using var unauthorizedRequest = new HttpRequestMessage(
            HttpMethod.Get,
            "/api/v1/admin/contact-inquiries");
        unauthorizedRequest.Headers.AcceptLanguage.ParseAdd("ar");
        using var unauthorized = await client.SendAsync(unauthorizedRequest, cancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
        await AssertProblemAsync(
            unauthorized,
            "Common.Unauthorized",
            "المصادقة مطلوبة.",
            cancellationToken);

        await PhaseOneTestHelpers.RegisterClientAsync(
            client,
            "localized.forbidden.client",
            "localized.forbidden.client@example.test",
            "01270000001",
            "Localized Client",
            cancellationToken);
        var clientToken = await PhaseOneTestHelpers.LoginAndReadTokenAsync(
            client,
            "localized.forbidden.client",
            PhaseOneTestHelpers.ClientPassword,
            cancellationToken);
        using var forbiddenRequest = new HttpRequestMessage(
            HttpMethod.Get,
            "/api/v1/admin/contact-inquiries");
        forbiddenRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", clientToken);
        forbiddenRequest.Headers.AcceptLanguage.ParseAdd("ar");
        using var forbidden = await client.SendAsync(forbiddenRequest, cancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        await AssertProblemAsync(
            forbidden,
            "Common.Forbidden",
            "الوصول غير مسموح به.",
            cancellationToken);
    }

    private HttpClient CreateClient()
        => factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

    private async Task CreateSuperAdminAsync(CancellationToken cancellationToken)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<LawyerPlatformDbContext>();
        if (await dbContext.UserAccounts.AnyAsync(
                account => account.Role == AccountRole.SuperAdmin,
                cancellationToken))
        {
            return;
        }

        var passwordService = scope.ServiceProvider.GetRequiredService<IPasswordService>();
        var passwordHash = await passwordService.HashAsync(
            "InitialPassword1",
            cancellationToken);
        var account = UserAccount.CreateSuperAdmin(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "superadmin",
            "SUPERADMIN",
            "admin@lawyerplatform.test",
            "ADMIN@LAWYERPLATFORM.TEST",
            "01000000000",
            passwordHash,
            DateTime.UtcNow).Value;
        dbContext.UserAccounts.Add(account);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static Task<HttpResponseMessage> SendInvalidCreateAsync(
        HttpClient client,
        string language,
        CancellationToken cancellationToken,
        string queryString = "")
    {
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/v1/public/contact-us{queryString}")
        {
            Content = JsonContent.Create(new
            {
                fullName = "   ",
                phoneNumber = "01012345678",
                email = "valid@example.test",
                inquiryType = "Technical Support",
                message = "Please contact me."
            })
        };
        request.Headers.AcceptLanguage.ParseAdd(language);
        return client.SendAsync(request, cancellationToken);
    }

    private static async Task AssertProblemAsync(
        HttpResponseMessage response,
        string expectedCode,
        string expectedMessage,
        CancellationToken cancellationToken)
    {
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var error = problem.GetProperty("errors")[0];
        Assert.Equal(expectedCode, error.GetProperty("code").GetString());
        Assert.Equal(expectedMessage, error.GetProperty("message").GetString());
        Assert.Equal(expectedMessage, problem.GetProperty("detail").GetString());
    }
}

public sealed class ContactInquiryRateLimitingTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public async Task PublicCreateUsesConfiguredPublicWriteRateLimit()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        var statuses = new List<HttpStatusCode>();

        for (var index = 0; index < 21; index++)
        {
            using var response = await client.PostAsJsonAsync(
                "/api/v1/public/contact-us",
                new
                {
                    fullName = $"Rate Limited {index}",
                    phoneNumber = "01512345678",
                    email = $"rate.{index}@example.test",
                    inquiryType = "Other / Suggestion",
                    message = "Rate limit verification."
                },
                TestContext.Current.CancellationToken);
            statuses.Add(response.StatusCode);
        }

        Assert.Equal(20, statuses.Count(status => status == HttpStatusCode.Created));
        Assert.Equal(HttpStatusCode.TooManyRequests, statuses[^1]);
    }
}
