using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using LawyerPlatform.Application.Abstractions.Authentication;
using LawyerPlatform.Domain.Accounts;
using LawyerPlatform.Domain.Lawyers;
using LawyerPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace LawyerPlatform.IntegrationTests;

public sealed class PasswordResetEndpointsTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public async Task PasswordReset_EndToEnd_RevokesOldJwtAndDoesNotPersistPlaintextSecrets()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        const string email = "password.reset.client@example.test";
        const string oldPassword = "OldStrongPassword1!";
        const string newPassword = "NewStrongPassword2!";
        await RegisterClientAsync(client, "password.reset.client", email, "01234000001", oldPassword, cancellationToken);

        var login = await LoginAsync(client, email, oldPassword, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var oldToken = await ReadTokenAsync(login, cancellationToken);
        Assert.Equal("1", new JwtSecurityTokenHandler().ReadJwtToken(oldToken)
            .Claims.Single(claim => claim.Type == LawyerPlatformClaimTypes.CredentialVersion).Value);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", oldToken);
        Assert.Equal(
            HttpStatusCode.OK,
            (await client.GetAsync("/api/v1/client/profile", cancellationToken)).StatusCode);
        client.DefaultRequestHeaders.Authorization = null;

        var existingResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/forgot-password/request-otp",
            new { email },
            cancellationToken);
        var missingResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/forgot-password/request-otp",
            new { email = "missing.password.reset@example.test" },
            cancellationToken);

        Assert.Equal(HttpStatusCode.Accepted, existingResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, missingResponse.StatusCode);
        var existingBody = await existingResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var missingBody = await missingResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        Assert.Equal(
            existingBody.EnumerateObject().Select(property => property.Name).Order(),
            missingBody.EnumerateObject().Select(property => property.Name).Order());
        Assert.Equal(
            existingBody.GetProperty("expiresInSeconds").GetInt32(),
            missingBody.GetProperty("expiresInSeconds").GetInt32());
        Assert.Equal(
            existingBody.GetProperty("resendAfterSeconds").GetInt32(),
            missingBody.GetProperty("resendAfterSeconds").GetInt32());

        var requestId = existingBody.GetProperty("requestId").GetGuid();
        var missingRequestId = missingBody.GetProperty("requestId").GetGuid();
        var otp = GetOtpForRecipient(email);
        PasswordResetChallenge persistedChallenge;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<LawyerPlatformDbContext>();
            persistedChallenge = await dbContext.PasswordResetChallenges
                .AsNoTracking()
                .SingleAsync(challenge => challenge.Id == requestId, cancellationToken);
            Assert.False(await dbContext.PasswordResetChallenges
                .AsNoTracking()
                .AnyAsync(challenge => challenge.Id == missingRequestId, cancellationToken));
            Assert.DoesNotContain(otp, persistedChallenge.OtpHash, StringComparison.Ordinal);
            Assert.Null(persistedChallenge.ResetTokenHash);
            Assert.DoesNotContain(
                await dbContext.EmailOutboxMessages.AsNoTracking().ToListAsync(cancellationToken),
                message => message.HtmlBody.Contains(otp, StringComparison.Ordinal));
        }

        var verify = await client.PostAsJsonAsync(
            "/api/v1/auth/forgot-password/verify-otp",
            new { requestId, otp },
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);
        var verifyBody = await verify.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var resetToken = verifyBody.GetProperty("resetToken").GetString()!;
        Assert.Equal(600, verifyBody.GetProperty("expiresInSeconds").GetInt32());

        var otpReuse = await client.PostAsJsonAsync(
            "/api/v1/auth/forgot-password/verify-otp",
            new { requestId, otp },
            cancellationToken);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, otpReuse.StatusCode);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            persistedChallenge = await scope.ServiceProvider
                .GetRequiredService<LawyerPlatformDbContext>()
                .PasswordResetChallenges
                .AsNoTracking()
                .SingleAsync(challenge => challenge.Id == requestId, cancellationToken);
            Assert.True(persistedChallenge.IsVerified);
            Assert.DoesNotContain(resetToken, persistedChallenge.ResetTokenHash!, StringComparison.Ordinal);
        }

        var reset = await client.PostAsJsonAsync(
            "/api/v1/auth/forgot-password/reset",
            new
            {
                requestId,
                resetToken,
                newPassword,
                confirmPassword = newPassword
            },
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
        var resetBody = await reset.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        Assert.True(resetBody.GetProperty("passwordReset").GetBoolean());
        Assert.False(resetBody.TryGetProperty("accessToken", out _));

        var resetTokenReuse = await client.PostAsJsonAsync(
            "/api/v1/auth/forgot-password/reset",
            new
            {
                requestId,
                resetToken,
                newPassword = "AnotherStrongPassword3!",
                confirmPassword = "AnotherStrongPassword3!"
            },
            cancellationToken);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, resetTokenReuse.StatusCode);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<LawyerPlatformDbContext>();
            var account = await dbContext.UserAccounts
                .AsNoTracking()
                .SingleAsync(user => user.Email == email, cancellationToken);
            persistedChallenge = await dbContext.PasswordResetChallenges
                .AsNoTracking()
                .SingleAsync(challenge => challenge.Id == requestId, cancellationToken);
            Assert.Equal(2, account.CredentialVersion);
            Assert.False(account.IsFirstLogin);
            Assert.True(persistedChallenge.IsConsumed);
        }

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", oldToken);
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await client.GetAsync("/api/v1/client/profile", cancellationToken)).StatusCode);
        client.DefaultRequestHeaders.Authorization = null;

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await LoginAsync(client, email, oldPassword, cancellationToken)).StatusCode);
        var newLogin = await LoginAsync(client, email, newPassword, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);
        var newToken = await ReadTokenAsync(newLogin, cancellationToken);
        Assert.Equal("2", new JwtSecurityTokenHandler().ReadJwtToken(newToken)
            .Claims.Single(claim => claim.Type == LawyerPlatformClaimTypes.CredentialVersion).Value);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", newToken);
        Assert.Equal(
            HttpStatusCode.OK,
            (await client.GetAsync("/api/v1/client/profile", cancellationToken)).StatusCode);
    }

    [Fact]
    public async Task VerifyOtp_FiveWrongAttemptsInvalidateChallengeAndCorrectOtpThenFails()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        const string email = "password.attempts.client@example.test";
        await RegisterClientAsync(
            client,
            "password.attempts.client",
            email,
            "01234000002",
            "OldStrongPassword1!",
            cancellationToken);
        var requestId = await RequestOtpAsync(client, email, cancellationToken);
        var correctOtp = GetOtpForRecipient(email);
        var wrongOtp = correctOtp == "000000" ? "999999" : "000000";

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var response = await client.PostAsJsonAsync(
                "/api/v1/auth/forgot-password/verify-otp",
                new { requestId, otp = wrongOtp },
                cancellationToken);
            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
            Assert.Equal(
                "PasswordReset.OtpInvalidOrExpired",
                await ReadErrorCodeAsync(response, cancellationToken));
        }

        var correctAfterLock = await client.PostAsJsonAsync(
            "/api/v1/auth/forgot-password/verify-otp",
            new { requestId, otp = correctOtp },
            cancellationToken);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, correctAfterLock.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var challenge = await scope.ServiceProvider
            .GetRequiredService<LawyerPlatformDbContext>()
            .PasswordResetChallenges
            .AsNoTracking()
            .SingleAsync(item => item.Id == requestId, cancellationToken);
        Assert.Equal(5, challenge.FailedAttemptCount);
        Assert.True(challenge.IsInvalidated);
    }

    [Fact]
    public async Task ResendAfterCooldown_InvalidatesPreviousOtp()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        const string email = "password.resend.client@example.test";
        await RegisterClientAsync(
            client,
            "password.resend.client",
            email,
            "01234000003",
            "OldStrongPassword1!",
            cancellationToken);

        var firstRequestId = await RequestOtpAsync(client, email, cancellationToken);
        var firstOtp = GetOtpForRecipient(email);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<LawyerPlatformDbContext>();
            var challenge = await dbContext.PasswordResetChallenges
                .SingleAsync(item => item.Id == firstRequestId, cancellationToken);
            dbContext.Entry(challenge)
                .Property(nameof(PasswordResetChallenge.CreatedOnUtc))
                .CurrentValue = DateTime.UtcNow.AddMinutes(-2);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var secondRequestId = await RequestOtpAsync(client, email, cancellationToken);
        var secondOtp = GetOtpForRecipient(email);
        Assert.NotEqual(firstRequestId, secondRequestId);

        var oldOtpResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/forgot-password/verify-otp",
            new { requestId = firstRequestId, otp = firstOtp },
            cancellationToken);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, oldOtpResponse.StatusCode);

        var newOtpResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/forgot-password/verify-otp",
            new { requestId = secondRequestId, otp = secondOtp },
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, newOtpResponse.StatusCode);
    }

    private HttpClient CreateClient()
        => factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

    private string GetOtpForRecipient(string email, int occurrenceFromEnd = 0)
    {
        var messages = factory.Services.GetRequiredService<TestPasswordResetEmailSender>()
            .Messages
            .Where(message => message.To.Contains(email, StringComparer.OrdinalIgnoreCase))
            .ToArray();
        var message = messages[^(occurrenceFromEnd + 1)];
        var match = Regex.Match(message.HtmlBody!, "(?<![0-9])[0-9]{6}(?![0-9])");
        Assert.True(match.Success);
        return match.Value;
    }

    private static async Task RegisterClientAsync(
        HttpClient client,
        string userName,
        string email,
        string phoneNumber,
        string password,
        CancellationToken cancellationToken)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/clients/register",
            new
            {
                fullName = "Password Reset Client",
                userName,
                email,
                phoneNumber,
                password
            },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private static async Task<Guid> RequestOtpAsync(
        HttpClient client,
        string email,
        CancellationToken cancellationToken)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/forgot-password/request-otp",
            new { email },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return body.GetProperty("requestId").GetGuid();
    }

    private static Task<HttpResponseMessage> LoginAsync(
        HttpClient client,
        string userNameOrEmail,
        string password,
        CancellationToken cancellationToken)
        => client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { userNameOrEmail, password },
            cancellationToken);

    private static async Task<string> ReadTokenAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return body.GetProperty("accessToken").GetString()!;
    }

    private static async Task<string> ReadErrorCodeAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return body.GetProperty("errors")[0].GetProperty("code").GetString()!;
    }
}

public sealed class PasswordResetAccountStatusTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public async Task SuspendedLawyer_CanResetWithoutChangingAccountOrApprovalStatus()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        const string email = "password.reset.suspended.lawyer@example.test";
        const string oldPassword = "OldStrongPassword1!";
        const string newPassword = "NewStrongPassword2!";
        var registration = await client.PostAsJsonAsync(
            "/api/v1/auth/lawyers/register",
            new
            {
                fullName = "Suspended Reset Lawyer",
                userName = "password.reset.suspended.lawyer",
                email,
                phoneNumber = "01234000004",
                password = oldPassword
            },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<LawyerPlatformDbContext>();
            var account = await dbContext.UserAccounts
                .SingleAsync(item => item.Email == email, cancellationToken);
            Assert.True(account.Suspend(DateTime.UtcNow).IsSuccess);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await CompleteResetAsync(client, email, newPassword, cancellationToken);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<LawyerPlatformDbContext>();
            var account = await dbContext.UserAccounts
                .AsNoTracking()
                .SingleAsync(item => item.Email == email, cancellationToken);
            var profile = await dbContext.LawyerProfiles
                .AsNoTracking()
                .SingleAsync(item => item.UserAccountId == account.Id, cancellationToken);
            Assert.Equal(AccountStatus.Suspended, account.Status);
            Assert.Equal(2, account.CredentialVersion);
            Assert.Equal(LawyerApprovalStatus.Draft, profile.ApprovalStatus);
        }

        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await LoginAsync(client, email, newPassword, cancellationToken)).StatusCode);
    }

    [Fact]
    public async Task InactiveClient_CanResetWithoutReactivatingAccount()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        const string email = "password.reset.inactive.client@example.test";
        const string oldPassword = "OldStrongPassword1!";
        const string newPassword = "NewStrongPassword2!";
        var registration = await client.PostAsJsonAsync(
            "/api/v1/auth/clients/register",
            new
            {
                fullName = "Inactive Reset Client",
                userName = "password.reset.inactive.client",
                email,
                phoneNumber = "01234000005",
                password = oldPassword
            },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<LawyerPlatformDbContext>();
            var account = await dbContext.UserAccounts
                .SingleAsync(item => item.Email == email, cancellationToken);
            Assert.True(account.Deactivate(DateTime.UtcNow).IsSuccess);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await CompleteResetAsync(client, email, newPassword, cancellationToken);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var account = await scope.ServiceProvider
                .GetRequiredService<LawyerPlatformDbContext>()
                .UserAccounts
                .AsNoTracking()
                .SingleAsync(item => item.Email == email, cancellationToken);
            Assert.Equal(AccountStatus.Inactive, account.Status);
            Assert.Equal(2, account.CredentialVersion);
        }

        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await LoginAsync(client, email, newPassword, cancellationToken)).StatusCode);
    }

    private async Task CompleteResetAsync(
        HttpClient client,
        string email,
        string newPassword,
        CancellationToken cancellationToken)
    {
        var request = await client.PostAsJsonAsync(
            "/api/v1/auth/forgot-password/request-otp",
            new { email },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Accepted, request.StatusCode);
        var requestBody = await request.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var requestId = requestBody.GetProperty("requestId").GetGuid();
        var emailMessage = factory.Services.GetRequiredService<TestPasswordResetEmailSender>()
            .Messages
            .Last(message => message.To.Contains(email, StringComparer.OrdinalIgnoreCase));
        var otp = Regex.Match(emailMessage.HtmlBody!, "(?<![0-9])[0-9]{6}(?![0-9])").Value;
        var verify = await client.PostAsJsonAsync(
            "/api/v1/auth/forgot-password/verify-otp",
            new { requestId, otp },
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);
        var verifyBody = await verify.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var resetToken = verifyBody.GetProperty("resetToken").GetString();
        var reset = await client.PostAsJsonAsync(
            "/api/v1/auth/forgot-password/reset",
            new { requestId, resetToken, newPassword, confirmPassword = newPassword },
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
    }

    private HttpClient CreateClient()
        => factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

    private static Task<HttpResponseMessage> LoginAsync(
        HttpClient client,
        string userNameOrEmail,
        string password,
        CancellationToken cancellationToken)
        => client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { userNameOrEmail, password },
            cancellationToken);
}

public sealed class PasswordResetRateLimitTests
{
    [Fact]
    public async Task SixthOtpRequestWithinWindow_ReturnsPasswordResetProblemDetails()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        var cancellationToken = TestContext.Current.CancellationToken;

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var accepted = await client.PostAsJsonAsync(
                "/api/v1/auth/forgot-password/request-otp",
                new { email = $"missing-{attempt}@example.test" },
                cancellationToken);
            Assert.Equal(HttpStatusCode.Accepted, accepted.StatusCode);
        }

        var limited = await client.PostAsJsonAsync(
            "/api/v1/auth/forgot-password/request-otp",
            new { email = "missing-limited@example.test" },
            cancellationToken);
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.True(limited.Headers.RetryAfter is not null);
        var problem = await limited.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        Assert.Equal(
            "PasswordReset.RateLimitExceeded",
            problem.GetProperty("errors")[0].GetProperty("code").GetString());
    }
}

public sealed class PasswordResetCooldownTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public async Task ResendBeforeCooldown_IsSafelyThrottledWithoutSendingOrReplacingOtp()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        const string email = "password.cooldown.client@example.test";
        var registration = await client.PostAsJsonAsync(
            "/api/v1/auth/clients/register",
            new
            {
                fullName = "Password Cooldown Client",
                userName = "password.cooldown.client",
                email,
                phoneNumber = "01234000008",
                password = "StrongPassword1!"
            },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);

        var first = await client.PostAsJsonAsync(
            "/api/v1/auth/forgot-password/request-otp",
            new { email },
            cancellationToken);
        var second = await client.PostAsJsonAsync(
            "/api/v1/auth/forgot-password/request-otp",
            new { email },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, second.StatusCode);
        var firstBody = await first.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var secondBody = await second.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        Assert.NotEqual(
            firstBody.GetProperty("requestId").GetGuid(),
            secondBody.GetProperty("requestId").GetGuid());

        var sentMessages = factory.Services
            .GetRequiredService<TestPasswordResetEmailSender>()
            .Messages
            .Where(message => message.To.Contains(email, StringComparer.OrdinalIgnoreCase))
            .ToArray();
        Assert.Single(sentMessages);

        await using var scope = factory.Services.CreateAsyncScope();
        var persisted = await scope.ServiceProvider
            .GetRequiredService<LawyerPlatformDbContext>()
            .PasswordResetChallenges
            .AsNoTracking()
            .Where(challenge =>
                challenge.InvalidatedOnUtc == null &&
                challenge.ConsumedOnUtc == null)
            .ToListAsync(cancellationToken);
        Assert.Contains(persisted, challenge => challenge.Id == firstBody.GetProperty("requestId").GetGuid());
        Assert.DoesNotContain(persisted, challenge => challenge.Id == secondBody.GetProperty("requestId").GetGuid());
    }
}

public sealed class PasswordResetFailureTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public async Task WeakAndCurrentPasswordsFailWithoutConsumingChallengeThenDifferentPasswordSucceeds()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        const string email = "password.failure.client@example.test";
        const string currentPassword = "CurrentStrongPassword1!";
        await RegisterAsync(client, "password.failure.client", email, "01234000009", currentPassword, cancellationToken);
        var (requestId, resetToken) = await CreateResetTokenAsync(client, email, cancellationToken);

        var weak = await ResetAsync(client, requestId, resetToken, "weak", cancellationToken);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, weak.StatusCode);
        Assert.Equal("Account.NewPasswordInvalid", await ReadErrorCodeAsync(weak, cancellationToken));

        var same = await ResetAsync(client, requestId, resetToken, currentPassword, cancellationToken);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, same.StatusCode);
        Assert.Equal(
            "PasswordReset.PasswordMustBeDifferent",
            await ReadErrorCodeAsync(same, cancellationToken));

        var successful = await ResetAsync(
            client,
            requestId,
            resetToken,
            "DifferentStrongPassword2!",
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, successful.StatusCode);
    }

    [Fact]
    public async Task InvalidAndExpiredResetTokensReturnSameSafeError()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        const string email = "password.token.failure@example.test";
        await RegisterAsync(
            client,
            "password.token.failure",
            email,
            "01234000010",
            "CurrentStrongPassword1!",
            cancellationToken);
        var (requestId, resetToken) = await CreateResetTokenAsync(client, email, cancellationToken);

        var invalid = await ResetAsync(
            client,
            requestId,
            "invalid-reset-token",
            "DifferentStrongPassword2!",
            cancellationToken);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, invalid.StatusCode);
        Assert.Equal(
            "PasswordReset.ResetTokenInvalidOrExpired",
            await ReadErrorCodeAsync(invalid, cancellationToken));

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<LawyerPlatformDbContext>();
            var challenge = await dbContext.PasswordResetChallenges
                .SingleAsync(item => item.Id == requestId, cancellationToken);
            dbContext.Entry(challenge)
                .Property(nameof(PasswordResetChallenge.ResetTokenExpiresOnUtc))
                .CurrentValue = DateTime.UtcNow.AddMinutes(-1);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var expired = await ResetAsync(
            client,
            requestId,
            resetToken,
            "DifferentStrongPassword2!",
            cancellationToken);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, expired.StatusCode);
        Assert.Equal(
            "PasswordReset.ResetTokenInvalidOrExpired",
            await ReadErrorCodeAsync(expired, cancellationToken));
    }

    [Fact]
    public async Task UnknownRequestIdsFailWithSafeStableErrors()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = CreateClient();

        var verify = await client.PostAsJsonAsync(
            "/api/v1/auth/forgot-password/verify-otp",
            new { requestId = Guid.NewGuid(), otp = "123456" },
            cancellationToken);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, verify.StatusCode);
        Assert.Equal(
            "PasswordReset.OtpInvalidOrExpired",
            await ReadErrorCodeAsync(verify, cancellationToken));

        var reset = await ResetAsync(
            client,
            Guid.NewGuid(),
            "unknown-reset-token",
            "DifferentStrongPassword2!",
            cancellationToken);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, reset.StatusCode);
        Assert.Equal(
            "PasswordReset.ResetTokenInvalidOrExpired",
            await ReadErrorCodeAsync(reset, cancellationToken));
    }

    private HttpClient CreateClient()
        => factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

    private static async Task RegisterAsync(
        HttpClient client,
        string userName,
        string email,
        string phoneNumber,
        string password,
        CancellationToken cancellationToken)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/clients/register",
            new { fullName = "Password Failure Client", userName, email, phoneNumber, password },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private async Task<(Guid RequestId, string ResetToken)> CreateResetTokenAsync(
        HttpClient client,
        string email,
        CancellationToken cancellationToken)
    {
        var request = await client.PostAsJsonAsync(
            "/api/v1/auth/forgot-password/request-otp",
            new { email },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Accepted, request.StatusCode);
        var requestBody = await request.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var requestId = requestBody.GetProperty("requestId").GetGuid();
        var message = factory.Services.GetRequiredService<TestPasswordResetEmailSender>()
            .Messages
            .Last(item => item.To.Contains(email, StringComparer.OrdinalIgnoreCase));
        var otp = Regex.Match(message.HtmlBody!, "(?<![0-9])[0-9]{6}(?![0-9])").Value;
        var verify = await client.PostAsJsonAsync(
            "/api/v1/auth/forgot-password/verify-otp",
            new { requestId, otp },
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);
        var verifyBody = await verify.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return (requestId, verifyBody.GetProperty("resetToken").GetString()!);
    }

    private static Task<HttpResponseMessage> ResetAsync(
        HttpClient client,
        Guid requestId,
        string resetToken,
        string newPassword,
        CancellationToken cancellationToken)
        => client.PostAsJsonAsync(
            "/api/v1/auth/forgot-password/reset",
            new { requestId, resetToken, newPassword, confirmPassword = newPassword },
            cancellationToken);

    private static async Task<string> ReadErrorCodeAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return body.GetProperty("errors")[0].GetProperty("code").GetString()!;
    }
}

public sealed class CredentialVersionTokenValidationTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private const string Issuer = "LawyerPlatform.Api.Tests";
    private const string Audience = "LawyerPlatform.TestClients";
    private const string SigningKey = "kkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkk";

    [Theory]
    [InlineData(null)]
    [InlineData("malformed")]
    [InlineData("0")]
    [InlineData("2")]
    [InlineData("999")]
    public async Task ProtectedRequest_RejectsMissingMalformedOrMismatchedCredentialVersion(
        string? credentialVersion)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        const string email = "credential.version.client@example.test";
        await EnsureClientExistsAsync(client, email, cancellationToken);

        Guid accountId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            accountId = await scope.ServiceProvider
                .GetRequiredService<LawyerPlatformDbContext>()
                .UserAccounts
                .AsNoTracking()
                .Where(account => account.Email == email)
                .Select(account => account.Id)
                .SingleAsync(cancellationToken);
        }

        var token = CreateToken(accountId, credentialVersion, SigningKey, DateTime.UtcNow.AddMinutes(5));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/v1/client/profile", cancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedRequest_RejectsExpiredAndBadSignatureTokens()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        const string email = "credential.crypto.client@example.test";
        await EnsureClientExistsAsync(client, email, cancellationToken, "01234000007");

        Guid accountId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            accountId = await scope.ServiceProvider
                .GetRequiredService<LawyerPlatformDbContext>()
                .UserAccounts
                .AsNoTracking()
                .Where(account => account.Email == email)
                .Select(account => account.Id)
                .SingleAsync(cancellationToken);
        }

        var expired = CreateToken(accountId, "1", SigningKey, DateTime.UtcNow.AddMinutes(-2));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", expired);
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await client.GetAsync("/api/v1/client/profile", cancellationToken)).StatusCode);

        var badSignature = CreateToken(accountId, "1", new string('x', 64), DateTime.UtcNow.AddMinutes(5));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", badSignature);
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await client.GetAsync("/api/v1/client/profile", cancellationToken)).StatusCode);
    }

    private static async Task EnsureClientExistsAsync(
        HttpClient client,
        string email,
        CancellationToken cancellationToken,
        string phoneNumber = "01234000006")
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/clients/register",
            new
            {
                fullName = "Credential Version Client",
                userName = email[..email.IndexOf('@')],
                email,
                phoneNumber,
                password = "StrongPassword1!"
            },
            cancellationToken);
        Assert.True(response.StatusCode is HttpStatusCode.Created or HttpStatusCode.Conflict);
    }

    private static string CreateToken(
        Guid accountId,
        string? credentialVersion,
        string signingKey,
        DateTime expiresOnUtc)
    {
        var claims = new List<Claim>
        {
            new(LawyerPlatformClaimTypes.Subject, accountId.ToString()),
            new(LawyerPlatformClaimTypes.Email, "credential.version.client@example.test"),
            new(LawyerPlatformClaimTypes.PreferredUserName, "credential.version.client"),
            new(LawyerPlatformClaimTypes.Role, AccountRole.Client.ToString()),
            new(LawyerPlatformClaimTypes.PasswordChangeRequired, "false"),
            new(LawyerPlatformClaimTypes.PasswordChangeReason, "None")
        };
        if (credentialVersion is not null)
        {
            claims.Add(new Claim(LawyerPlatformClaimTypes.CredentialVersion, credentialVersion));
        }

        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            notBefore: DateTime.UtcNow.AddMinutes(-5),
            expires: expiresOnUtc,
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
