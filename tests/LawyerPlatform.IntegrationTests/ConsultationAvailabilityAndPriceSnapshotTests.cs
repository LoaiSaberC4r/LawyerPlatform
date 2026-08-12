using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LawyerPlatform.Application.Abstractions.Authentication;
using LawyerPlatform.Domain.Accounts;
using LawyerPlatform.Domain.Consultations;
using LawyerPlatform.Domain.Lawyers;
using LawyerPlatform.Infrastructure.Persistence;
using LawyerPlatform.Infrastructure.Seeding;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LawyerPlatform.IntegrationTests;

public sealed class ConsultationAvailabilityAndPriceSnapshotTests
{
    [Fact]
    public async Task GuestAndClientUseBusinessLocalAvailabilityAndImmutablePriceSnapshots()
    {
        await using var factory = await CreateFactoryAsync();
        using var client = CreateClient(factory);
        var lawyer = await RegisterApproveAndConfigureLawyerAsync(
            factory,
            client,
            "availability.lawyer",
            "01073333333",
            configureSettings: true);
        var sundayAtTen = NextBusinessLocalUtc(DayOfWeek.Sunday, new TimeOnly(10, 0));
        var sundayAtFourteen = NextBusinessLocalUtc(DayOfWeek.Sunday, new TimeOnly(14, 0));
        var sundayAtSeventeen = NextBusinessLocalUtc(DayOfWeek.Sunday, new TimeOnly(17, 0));

        await AssertCreatedAsync(await PostGuestAsync(client, lawyer.ProfileId, sundayAtTen, "01081111111"));
        var oldGuestResponse = await PostGuestAsync(client, lawyer.ProfileId, sundayAtFourteen, "01082222222");
        var oldGuest = await AssertCreatedAsync(oldGuestResponse);
        await AssertCreatedAsync(await PostGuestAsync(client, lawyer.ProfileId, sundayAtSeventeen, "01083333333"));
        await AssertCreatedAsync(await PostGuestAsync(client, lawyer.ProfileId, sundayAtFourteen, "01084444444"));

        await AssertProblemAsync(
            await PostGuestAsync(client, lawyer.ProfileId, NextBusinessLocalUtc(DayOfWeek.Sunday, new TimeOnly(9, 59)), "01085555555"),
            "ConsultationRequest.OutsideLawyerWorkingHours");
        await AssertProblemAsync(
            await PostGuestAsync(client, lawyer.ProfileId, NextBusinessLocalUtc(DayOfWeek.Sunday, new TimeOnly(17, 1)), "01086666666"),
            "ConsultationRequest.OutsideLawyerWorkingHours");
        await AssertProblemAsync(
            await PostGuestAsync(client, lawyer.ProfileId, NextBusinessLocalUtc(DayOfWeek.Wednesday, new TimeOnly(14, 0)), "01087777777"),
            "ConsultationRequest.LawyerNotAvailableOnSelectedDay");

        var clientToken = await RegisterAndLoginClientAsync(client);
        SetToken(client, clientToken);
        var oldClient = await AssertCreatedAsync(
            await PostClientAsync(client, lawyer.ProfileId, sundayAtFourteen));
        await AssertProblemAsync(
            await PostClientAsync(client, lawyer.ProfileId, NextBusinessLocalUtc(DayOfWeek.Wednesday, new TimeOnly(14, 0))),
            "ConsultationRequest.LawyerNotAvailableOnSelectedDay");
        await AssertProblemAsync(
            await PostClientAsync(client, lawyer.ProfileId, NextBusinessLocalUtc(DayOfWeek.Sunday, new TimeOnly(17, 1))),
            "ConsultationRequest.OutsideLawyerWorkingHours");

        SetToken(client, lawyer.Token);
        var settings = await GetJsonAsync(client, "/api/v1/lawyer/consultation-settings");
        var priceUpdate = await client.PutAsJsonAsync("/api/v1/lawyer/consultation-settings", new
        {
            consultationPrice = 700m,
            availability = new[]
            {
                new { dayOfWeek = "Sunday", startTime = "10:00:00", endTime = "17:00:00" }
            },
            rowVersion = settings.GetProperty("rowVersion").GetString()
        }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, priceUpdate.StatusCode);

        SetToken(client, null);
        var newGuest = await AssertCreatedAsync(
            await PostGuestAsync(client, lawyer.ProfileId, sundayAtFourteen, "01088888888"));
        SetToken(client, clientToken);
        var newClient = await AssertCreatedAsync(
            await PostClientAsync(client, lawyer.ProfileId, sundayAtFourteen));

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<LawyerPlatformDbContext>();
            var prices = await context.ConsultationRequests.AsNoTracking()
                .Where(request => new[] { oldGuest.Id, oldClient.Id, newGuest.Id, newClient.Id }.Contains(request.Id))
                .ToDictionaryAsync(request => request.Id, request => request.ConsultationPrice,
                    TestContext.Current.CancellationToken);
            Assert.Equal(500m, prices[oldGuest.Id]);
            Assert.Equal(500m, prices[oldClient.Id]);
            Assert.Equal(700m, prices[newGuest.Id]);
            Assert.Equal(700m, prices[newClient.Id]);
        }

        SetToken(client, null);
        var tracking = await client.PostAsJsonAsync("/api/v1/public/consultation-requests/track", new
        {
            referenceNumber = oldGuest.ReferenceNumber,
            phoneNumber = "01082222222"
        }, TestContext.Current.CancellationToken);
        Assert.Equal(500m, (await ReadJsonAsync(tracking)).GetProperty("consultationPrice").GetDecimal());

        SetToken(client, clientToken);
        Assert.Equal(500m, (await GetJsonAsync(client, $"/api/v1/client/consultation-requests/{oldClient.Id}"))
            .GetProperty("consultationPrice").GetDecimal());
        SetToken(client, lawyer.Token);
        Assert.Equal(500m, (await GetJsonAsync(client, $"/api/v1/lawyer/consultation-requests/{oldGuest.Id}"))
            .GetProperty("consultationPrice").GetDecimal());
        SetToken(client, await LoginAndChangeAdminPasswordAsync(client));
        Assert.Equal(500m, (await GetJsonAsync(client, $"/api/v1/admin/consultation-requests/{oldGuest.Id}"))
            .GetProperty("consultationPrice").GetDecimal());

        SetToken(client, null);
        var publicList = await GetJsonAsync(client, "/api/v1/public/lawyers?pageNumber=1&pageSize=100");
        var listItem = publicList.GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("id").GetGuid() == lawyer.ProfileId);
        Assert.Equal(700m, listItem.GetProperty("consultationPrice").GetDecimal());
        Assert.False(listItem.TryGetProperty("availability", out _));
        var publicDetails = await GetJsonAsync(client, $"/api/v1/public/lawyers/{lawyer.ProfileId}");
        Assert.Equal(700m, publicDetails.GetProperty("consultationPrice").GetDecimal());
        Assert.Equal("Sunday", publicDetails.GetProperty("availability")[0].GetProperty("dayOfWeek").GetString());
        Assert.False(publicDetails.TryGetProperty("rowVersion", out _));
    }

    [Fact]
    public async Task OptionalAppointmentRemainsCompatibleForUnconfiguredLawyer()
    {
        await using var factory = await CreateFactoryAsync();
        using var client = CreateClient(factory);
        var lawyer = await RegisterApproveAndConfigureLawyerAsync(
            factory,
            client,
            "unconfigured.lawyer",
            "01074444444",
            configureSettings: false);

        SetToken(client, null);
        var contact = await AssertCreatedAsync(
            await PostGuestAsync(client, lawyer.ProfileId, null, "01089999999"));
        await AssertProblemAsync(
            await PostGuestAsync(
                client,
                lawyer.ProfileId,
                NextBusinessLocalUtc(DayOfWeek.Sunday, new TimeOnly(14, 0)),
                "01080000000"),
            "ConsultationRequest.LawyerAvailabilityNotConfigured");

        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<LawyerPlatformDbContext>();
        Assert.Null((await context.ConsultationRequests.AsNoTracking().SingleAsync(
            request => request.Id == contact.Id,
            TestContext.Current.CancellationToken)).ConsultationPrice);

        var publicDetails = await GetJsonAsync(client, $"/api/v1/public/lawyers/{lawyer.ProfileId}");
        Assert.Equal(JsonValueKind.Null, publicDetails.GetProperty("consultationPrice").ValueKind);
        Assert.Empty(publicDetails.GetProperty("availability").EnumerateArray());
    }

    private static async Task<ApprovedLawyer> RegisterApproveAndConfigureLawyerAsync(
        CustomWebApplicationFactory factory,
        HttpClient client,
        string userName,
        string phoneNumber,
        bool configureSettings)
    {
        SetToken(client, null);
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<LawyerPlatformDbContext>();
        var nowUtc = DateTime.UtcNow;
        var normalizedUserName = userName.ToUpperInvariant();
        var account = UserAccount.CreateLawyer(
            userName,
            normalizedUserName,
            $"{userName}@example.test",
            $"{normalizedUserName}@EXAMPLE.TEST",
            phoneNumber,
            "integration-test-hash",
            nowUtc).Value;
        var profile = LawyerProfile.Create(account, "Availability Lawyer").Value;
        var profileId = profile.Id;
        Assert.True(profile.UpdateProfessionalProfile(
            "Availability Lawyer",
            "Attorney",
            "Availability and snapshot integration test profile.",
            10,
            $"REG-{Guid.NewGuid():N}").IsSuccess);
        var area = EgyptLocationSeedCatalog.Areas[0];
        var city = EgyptLocationSeedCatalog.Cities.Single(item => item.Id == area.CityId);
        Assert.True(profile.UpsertPrimaryOffice(
            city.GovernorateId,
            city.Id,
            area.Id,
            "Complete public office address",
            phoneNumber).IsSuccess);
        Assert.True(profile.ReplaceSpecializations([1]).IsSuccess);
        Assert.True(profile.AddDocument(
            "IdentityVerification", $"docs/{userName}-id.pdf", "id.pdf", "application/pdf", 100, nowUtc).IsSuccess);
        Assert.True(profile.AddDocument(
            "ProfessionalMembership", $"docs/{userName}-member.pdf", "member.pdf", "application/pdf", 100, nowUtc).IsSuccess);
        Assert.True(profile.SubmitForApproval(profile.UserAccountId, true, true, nowUtc.AddMinutes(1)).IsSuccess);
        Assert.True(profile.Approve(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            true,
            nowUtc.AddMinutes(2)).IsSuccess);

        if (configureSettings)
        {
            context.LawyerConsultationSettings.Add(LawyerConsultationSettings.Create(
                profileId,
                500m,
                [new LawyerAvailabilityPeriod(DayOfWeek.Sunday, new TimeOnly(10, 0), new TimeOnly(17, 0))],
                nowUtc).Value);
        }

        context.LawyerProfiles.Add(profile);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var token = scope.ServiceProvider.GetRequiredService<IJwtProvider>().GenerateToken(
            account,
            new PasswordLifecycleState(
                nowUtc.AddDays(90),
                PasswordExpired: false,
                PasswordChangeRequired: false,
                PasswordChangeReason.None)).AccessToken;
        return new ApprovedLawyer(profileId, token);
    }

    private static Task<HttpResponseMessage> PostGuestAsync(
        HttpClient client,
        Guid lawyerId,
        DateTime? appointmentOnUtc,
        string phoneNumber)
    {
        SetToken(client, null);
        return client.PostAsJsonAsync("/api/v1/public/consultation-requests", new
        {
            lawyerId,
            legalSpecializationId = 1,
            fullName = "Availability Guest",
            phoneNumber,
            email = "availability.guest@example.test",
            description = "Availability validation request",
            preferredAppointmentOnUtc = appointmentOnUtc
        }, TestContext.Current.CancellationToken);
    }

    private static Task<HttpResponseMessage> PostClientAsync(
        HttpClient client,
        Guid lawyerId,
        DateTime appointmentOnUtc)
        => client.PostAsJsonAsync("/api/v1/client/consultation-requests", new
        {
            lawyerId,
            legalSpecializationId = 1,
            description = "Client availability validation request",
            preferredAppointmentOnUtc = appointmentOnUtc
        }, TestContext.Current.CancellationToken);

    private static async Task<CreatedRequest> AssertCreatedAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await ReadJsonAsync(response);
        return new CreatedRequest(
            body.GetProperty("id").GetGuid(),
            body.GetProperty("referenceNumber").GetString()!);
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, string code)
    {
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var problem = await ReadJsonAsync(response);
        Assert.Contains(problem.GetProperty("errors").EnumerateArray(),
            error => error.GetProperty("code").GetString() == code);
    }

    private static DateTime NextBusinessLocalUtc(DayOfWeek dayOfWeek, TimeOnly time)
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo");
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone);
        var date = DateOnly.FromDateTime(localNow).AddDays(1);
        while (date.DayOfWeek != dayOfWeek)
        {
            date = date.AddDays(1);
        }

        var local = DateTime.SpecifyKind(date.ToDateTime(time), DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(local, timeZone);
    }

    private static async Task<string> RegisterAndLoginClientAsync(HttpClient client)
    {
        SetToken(client, null);
        const string userName = "availability.client";
        const string password = "ClientPassword1";
        var registration = await client.PostAsJsonAsync("/api/v1/auth/clients/register", new
        {
            fullName = "Availability Client",
            userName,
            email = "availability.client@example.test",
            phoneNumber = "01075555555",
            password
        }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
        return await LoginAsync(client, userName, password);
    }

    private static async Task<string> LoginAndChangeAdminPasswordAsync(HttpClient client)
    {
        var token = await LoginAsync(client, "superadmin", "InitialPassword1");
        SetToken(client, token);
        var change = await client.PostAsJsonAsync("/api/v1/auth/change-password", new
        {
            currentPassword = "InitialPassword1",
            newPassword = "ChangedAdminPassword2"
        }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, change.StatusCode);
        return (await ReadJsonAsync(change)).GetProperty("accessToken").GetString()!;
    }

    private static async Task<string> LoginAsync(HttpClient client, string userName, string password)
    {
        SetToken(client, null);
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            userNameOrEmail = userName,
            password
        }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        return (await ReadJsonAsync(login)).GetProperty("accessToken").GetString()!;
    }

    private static async Task<JsonElement> GetJsonAsync(HttpClient client, string path)
    {
        var response = await client.GetAsync(path, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadJsonAsync(response);
    }

    private static Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
        => response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

    private static void SetToken(HttpClient client, string? token)
        => client.DefaultRequestHeaders.Authorization = token is null
            ? null
            : new AuthenticationHeaderValue("Bearer", token);

    private static async Task<CustomWebApplicationFactory> CreateFactoryAsync()
    {
        var factory = new CustomWebApplicationFactory();
        await factory.SeedDatabaseAsync(TestContext.Current.CancellationToken);
        return factory;
    }

    private static HttpClient CreateClient(CustomWebApplicationFactory factory)
        => factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

    private sealed record ApprovedLawyer(Guid ProfileId, string Token);
    private sealed record CreatedRequest(Guid Id, string ReferenceNumber);
}
