using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace LawyerPlatform.IntegrationTests;

public sealed class LawyerConsultationSettingsEndpointsTests
{
    [Fact]
    public async Task LawyerCanCreateReadReplaceAndConcurrentlyUpdateOwnSettings()
    {
        await using var factory = await CreateFactoryAsync();
        using var client = CreateClient(factory);
        var lawyerToken = await RegisterAndLoginLawyerAsync(client, "settings.owner", "01071111111");

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await client.GetAsync(
                "/api/v1/lawyer/consultation-settings",
                TestContext.Current.CancellationToken)).StatusCode);

        SetToken(client, lawyerToken);
        var unconfigured = await GetJsonAsync(client, "/api/v1/lawyer/consultation-settings");
        Assert.Equal(JsonValueKind.Null, unconfigured.GetProperty("consultationPrice").ValueKind);
        Assert.Empty(unconfigured.GetProperty("availability").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, unconfigured.GetProperty("rowVersion").ValueKind);

        var createdResponse = await PutSettingsAsync(client, 500m, null,
            new AvailabilityJson("Tuesday", "10:00:00", "17:00:00"),
            new AvailabilityJson("Sunday", "10:00:00", "17:00:00"),
            new AvailabilityJson("Monday", "10:00:00", "17:00:00"));
        Assert.Equal(HttpStatusCode.OK, createdResponse.StatusCode);
        var created = await ReadJsonAsync(createdResponse);
        var firstRowVersion = created.GetProperty("rowVersion").GetString()!;
        Assert.Equal(500m, created.GetProperty("consultationPrice").GetDecimal());
        Assert.Equal(
            ["Sunday", "Monday", "Tuesday"],
            created.GetProperty("availability").EnumerateArray()
                .Select(item => item.GetProperty("dayOfWeek").GetString()!).ToArray());

        var duplicate = await PutSettingsAsync(client, 500m, firstRowVersion,
            new AvailabilityJson("Sunday", "10:00:00", "17:00:00"),
            new AvailabilityJson("sunday", "12:00:00", "16:00:00"));
        await AssertProblemAsync(duplicate, HttpStatusCode.UnprocessableEntity, "Lawyer.DuplicateAvailabilityDay");

        var invalidRange = await PutSettingsAsync(client, 500m, firstRowVersion,
            new AvailabilityJson("Sunday", "17:00:00", "10:00:00"));
        await AssertProblemAsync(invalidRange, HttpStatusCode.UnprocessableEntity, "Lawyer.AvailabilityInvalid");

        var updatedResponse = await PutSettingsAsync(client, 500m, firstRowVersion,
            new AvailabilityJson("Sunday", "09:00:00", "18:00:00"),
            new AvailabilityJson("Monday", "10:00:00", "17:00:00"));
        Assert.True(
            updatedResponse.StatusCode == HttpStatusCode.OK,
            await updatedResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var updated = await ReadJsonAsync(updatedResponse);
        Assert.NotEqual(firstRowVersion, updated.GetProperty("rowVersion").GetString());
        Assert.Equal(["Sunday", "Monday"], updated.GetProperty("availability").EnumerateArray()
            .Select(item => item.GetProperty("dayOfWeek").GetString()!).ToArray());

        var stale = await PutSettingsAsync(client, 900m, firstRowVersion,
            new AvailabilityJson("Sunday", "09:00:00", "18:00:00"));
        await AssertProblemAsync(stale, HttpStatusCode.Conflict, "Lawyer.ConsultationSettingsConcurrencyConflict");

        SetToken(client, await RegisterAndLoginClientAsync(client, "settings.wrong.role", "01072222222"));
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await client.GetAsync(
                "/api/v1/lawyer/consultation-settings",
                TestContext.Current.CancellationToken)).StatusCode);
    }

    private static Task<HttpResponseMessage> PutSettingsAsync(
        HttpClient client,
        decimal price,
        string? rowVersion,
        params AvailabilityJson[] availability)
        => client.PutAsJsonAsync("/api/v1/lawyer/consultation-settings", new
        {
            consultationPrice = price,
            availability = availability.Select(item => new
            {
                dayOfWeek = item.DayOfWeek,
                startTime = item.StartTime,
                endTime = item.EndTime
            }),
            rowVersion
        }, TestContext.Current.CancellationToken);

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

    private static async Task<string> RegisterAndLoginLawyerAsync(
        HttpClient client,
        string userName,
        string phoneNumber)
    {
        client.DefaultRequestHeaders.Authorization = null;
        const string password = "LawyerPassword1";
        var registration = await client.PostAsJsonAsync("/api/v1/auth/lawyers/register", new
        {
            fullName = "Settings Lawyer",
            userName,
            email = $"{userName}@example.test",
            phoneNumber,
            password
        }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
        return await LoginAsync(client, userName, password);
    }

    private static async Task<string> RegisterAndLoginClientAsync(
        HttpClient client,
        string userName,
        string phoneNumber)
    {
        client.DefaultRequestHeaders.Authorization = null;
        const string password = "ClientPassword1";
        var registration = await client.PostAsJsonAsync("/api/v1/auth/clients/register", new
        {
            fullName = "Settings Client",
            userName,
            email = $"{userName}@example.test",
            phoneNumber,
            password
        }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
        return await LoginAsync(client, userName, password);
    }

    private static async Task<string> LoginAsync(HttpClient client, string userName, string password)
    {
        client.DefaultRequestHeaders.Authorization = null;
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            userNameOrEmail = userName,
            password
        }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        return (await ReadJsonAsync(login)).GetProperty("accessToken").GetString()!;
    }

    private static void SetToken(HttpClient client, string? token)
        => client.DefaultRequestHeaders.Authorization = token is null
            ? null
            : new AuthenticationHeaderValue("Bearer", token);

    private static async Task<JsonElement> GetJsonAsync(HttpClient client, string path)
    {
        var response = await client.GetAsync(path, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadJsonAsync(response);
    }

    private static Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
        => response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

    private static async Task AssertProblemAsync(
        HttpResponseMessage response,
        HttpStatusCode status,
        string code)
    {
        Assert.Equal(status, response.StatusCode);
        var problem = await ReadJsonAsync(response);
        Assert.Contains(problem.GetProperty("errors").EnumerateArray(),
            error => error.GetProperty("code").GetString() == code);
    }

    private sealed record AvailabilityJson(string DayOfWeek, string StartTime, string EndTime);
}
