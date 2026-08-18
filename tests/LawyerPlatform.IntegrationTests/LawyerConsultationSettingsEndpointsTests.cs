using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace LawyerPlatform.IntegrationTests;

public sealed class LawyerConsultationSettingsEndpointsTests
{
    [Fact]
    public async Task LawyerCanPerformMultipleSequentialConsultationSettingsUpdatesUsingLatestRowVersion()
    {
        await using var factory = await CreateFactoryAsync();
        using var client = CreateClient(factory);
        SetToken(client, await RegisterAndLoginLawyerAsync(
            client,
            "settings.sequential",
            "01073333333"));

        var createResponse = await PutSettingsAsync(client, 500m, null,
            new AvailabilityJson("Sunday", "10:00:00", "17:00:00"));
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        var firstRowVersion = (await ReadJsonAsync(createResponse)).GetProperty("rowVersion").GetString()!;
        Assert.False(string.IsNullOrWhiteSpace(firstRowVersion));

        var rowVersions = new List<string> { firstRowVersion };
        var updates = new[]
        {
            new SettingsUpdate(500m,
            [
                new AvailabilityJson("Sunday", "10:00:00", "17:00:00"),
                new AvailabilityJson("Wednesday", "10:00:00", "20:00:00")
            ]),
            new SettingsUpdate(600m,
            [
                new AvailabilityJson("Sunday", "10:00:00", "17:00:00"),
                new AvailabilityJson("Wednesday", "10:00:00", "20:00:00")
            ]),
            new SettingsUpdate(600m,
            [
                new AvailabilityJson("Sunday", "09:00:00", "18:00:00"),
                new AvailabilityJson("Wednesday", "10:00:00", "20:00:00"),
                new AvailabilityJson("Thursday", "10:00:00", "19:00:00")
            ]),
            new SettingsUpdate(600m,
            [
                new AvailabilityJson("Sunday", "09:00:00", "18:00:00"),
                new AvailabilityJson("Wednesday", "11:00:00", "20:00:00"),
                new AvailabilityJson("Friday", "10:00:00", "16:00:00")
            ]),
            new SettingsUpdate(650m, [])
        };

        foreach (var update in updates)
        {
            var response = await PutSettingsAsync(
                client,
                update.Price,
                rowVersions[^1],
                update.Availability);
            Assert.True(
                response.StatusCode == HttpStatusCode.OK,
                await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            var rowVersion = (await ReadJsonAsync(response)).GetProperty("rowVersion").GetString()!;
            Assert.False(string.IsNullOrWhiteSpace(rowVersion));
            Assert.NotEqual(rowVersions[^1], rowVersion);
            rowVersions.Add(rowVersion);
        }

        var staleResponse = await PutSettingsAsync(client, 700m, rowVersions[^3]);
        await AssertProblemAsync(
            staleResponse,
            HttpStatusCode.Conflict,
            "Lawyer.ConsultationSettingsConcurrencyConflict");
    }

    [Fact]
    public async Task LawyerCanPutExistingConsultationSettingsUsingLatestRowVersionWithoutFalseConcurrencyConflict()
    {
        await using var factory = await CreateFactoryAsync();
        using var client = CreateClient(factory);
        SetToken(client, await RegisterAndLoginLawyerAsync(
            client,
            "settings.noop",
            "01074444444"));

        var availability = new AvailabilityJson("Sunday", "10:00:00", "17:00:00");
        var createResponse = await PutSettingsAsync(client, 500m, null, availability);
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        var firstRowVersion = (await ReadJsonAsync(createResponse)).GetProperty("rowVersion").GetString()!;

        var noOpResponse = await PutSettingsAsync(client, 500m, firstRowVersion, availability);
        Assert.True(
            noOpResponse.StatusCode == HttpStatusCode.OK,
            await noOpResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var secondRowVersion = (await ReadJsonAsync(noOpResponse)).GetProperty("rowVersion").GetString()!;

        Assert.False(string.IsNullOrWhiteSpace(secondRowVersion));
        Assert.NotEqual(firstRowVersion, secondRowVersion);
    }

    [Fact]
    public async Task AvailabilityOnlyUpdateAdvancesSettingsConcurrencyToken()
    {
        await using var factory = await CreateFactoryAsync();
        using var client = CreateClient(factory);
        SetToken(client, await RegisterAndLoginLawyerAsync(
            client,
            "settings.availability",
            "01075555555"));

        var createResponse = await PutSettingsAsync(client, 500m, null,
            new AvailabilityJson("Sunday", "10:00:00", "17:00:00"));
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        var firstRowVersion = (await ReadJsonAsync(createResponse)).GetProperty("rowVersion").GetString()!;

        var updateResponse = await PutSettingsAsync(client, 500m, firstRowVersion,
            new AvailabilityJson("Sunday", "09:00:00", "18:00:00"),
            new AvailabilityJson("Wednesday", "10:00:00", "20:00:00"));
        Assert.True(
            updateResponse.StatusCode == HttpStatusCode.OK,
            await updateResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var updated = await ReadJsonAsync(updateResponse);
        var secondRowVersion = updated.GetProperty("rowVersion").GetString()!;

        Assert.NotEqual(firstRowVersion, secondRowVersion);
        Assert.Equal(
            ["Sunday", "Wednesday"],
            updated.GetProperty("online").GetProperty("availability").EnumerateArray()
                .Select(item => item.GetProperty("dayOfWeek").GetString()!).ToArray());

        var nextResponse = await PutSettingsAsync(client, 550m, secondRowVersion,
            new AvailabilityJson("Sunday", "09:00:00", "18:00:00"),
            new AvailabilityJson("Wednesday", "10:00:00", "20:00:00"));
        Assert.Equal(HttpStatusCode.OK, nextResponse.StatusCode);
    }

    [Theory]
    [InlineData("price")]
    [InlineData("times")]
    [InlineData("add")]
    [InlineData("remove")]
    [InlineData("replace")]
    [InlineData("empty")]
    public async Task LawyerCanUpdateExistingConsultationSettingsForEachSupportedReplacementShape(
        string updateShape)
    {
        await using var factory = await CreateFactoryAsync();
        using var client = CreateClient(factory);
        SetToken(client, await RegisterAndLoginLawyerAsync(
            client,
            $"settings.shape.{updateShape}",
            "01076666666"));

        var initial = updateShape switch
        {
            "remove" or "replace" => new[]
            {
                new AvailabilityJson("Sunday", "10:00:00", "17:00:00"),
                new AvailabilityJson("Wednesday", "10:00:00", "20:00:00"),
                new AvailabilityJson("Thursday", "10:00:00", "19:00:00")
            },
            "empty" =>
            [
                new AvailabilityJson("Sunday", "10:00:00", "17:00:00"),
                new AvailabilityJson("Wednesday", "10:00:00", "20:00:00")
            ],
            _ => [new AvailabilityJson("Sunday", "10:00:00", "17:00:00")]
        };
        var expectedPrice = updateShape == "price" ? 600m : 500m;
        var replacement = updateShape switch
        {
            "times" =>
            [new AvailabilityJson("Sunday", "09:00:00", "18:00:00")],
            "add" =>
            [
                new AvailabilityJson("Sunday", "10:00:00", "17:00:00"),
                new AvailabilityJson("Wednesday", "10:00:00", "20:00:00")
            ],
            "remove" =>
            [
                new AvailabilityJson("Sunday", "10:00:00", "17:00:00"),
                new AvailabilityJson("Wednesday", "10:00:00", "20:00:00")
            ],
            "replace" =>
            [
                new AvailabilityJson("Sunday", "09:00:00", "18:00:00"),
                new AvailabilityJson("Wednesday", "11:00:00", "20:00:00"),
                new AvailabilityJson("Friday", "10:00:00", "16:00:00")
            ],
            "empty" => [],
            _ => initial
        };

        var createResponse = await PutSettingsAsync(client, 500m, null, initial);
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        var firstRowVersion = (await ReadJsonAsync(createResponse)).GetProperty("rowVersion").GetString()!;

        var updateResponse = await PutSettingsAsync(client, expectedPrice, firstRowVersion, replacement);
        Assert.True(
            updateResponse.StatusCode == HttpStatusCode.OK,
            await updateResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var updated = await ReadJsonAsync(updateResponse);

        Assert.Equal(expectedPrice, updated.GetProperty("online").GetProperty("price").GetDecimal());
        Assert.Equal(
            replacement.Select(item => item.DayOfWeek).OrderBy(DayIndex).ToArray(),
            updated.GetProperty("online").GetProperty("availability").EnumerateArray()
                .Select(item => item.GetProperty("dayOfWeek").GetString()!).ToArray());
        Assert.NotEqual(firstRowVersion, updated.GetProperty("rowVersion").GetString());
    }

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
        Assert.Equal(JsonValueKind.Null, unconfigured.GetProperty("online").GetProperty("price").ValueKind);
        Assert.Empty(unconfigured.GetProperty("online").GetProperty("availability").EnumerateArray());
        Assert.Empty(unconfigured.GetProperty("onsite").GetProperty("availability").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, unconfigured.GetProperty("rowVersion").ValueKind);

        var createdResponse = await PutSettingsAsync(client, 500m, null,
            new AvailabilityJson("Tuesday", "10:00:00", "17:00:00"),
            new AvailabilityJson("Sunday", "10:00:00", "17:00:00"),
            new AvailabilityJson("Monday", "10:00:00", "17:00:00"));
        Assert.Equal(HttpStatusCode.OK, createdResponse.StatusCode);
        var created = await ReadJsonAsync(createdResponse);
        var firstRowVersion = created.GetProperty("rowVersion").GetString()!;
        Assert.Equal(500m, created.GetProperty("online").GetProperty("price").GetDecimal());
        Assert.Equal(
            ["Sunday", "Monday", "Tuesday"],
            created.GetProperty("online").GetProperty("availability").EnumerateArray()
                .Select(item => item.GetProperty("dayOfWeek").GetString()!).ToArray());

        var duplicate = await PutSettingsAsync(client, 500m, firstRowVersion,
            new AvailabilityJson("Sunday", "10:00:00", "17:00:00"),
            new AvailabilityJson("sunday", "12:00:00", "16:00:00"));
        await AssertProblemAsync(duplicate, HttpStatusCode.UnprocessableEntity, "Lawyer.DuplicateAvailabilityDay");

        var invalidRowVersion = await PutSettingsAsync(client, 500m, "not-base64",
            new AvailabilityJson("Sunday", "10:00:00", "17:00:00"));
        await AssertProblemAsync(
            invalidRowVersion,
            HttpStatusCode.UnprocessableEntity,
            "Lawyer.ConsultationSettingsInvalidRowVersion");

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
        Assert.Equal(["Sunday", "Monday"], updated.GetProperty("online").GetProperty("availability").EnumerateArray()
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

    [Fact]
    public async Task LawyerCanRoundTripAndReplaceIndependentOnlineAndOnsiteAvailability()
    {
        await using var factory = await CreateFactoryAsync();
        using var client = CreateClient(factory);
        SetToken(client, await RegisterAndLoginLawyerAsync(
            client,
            "settings.types",
            "01077777777"));

        var createdResponse = await PutGroupedSettingsAsync(
            client,
            500m,
            null,
            [new AvailabilityJson("Sunday", "10:00:00", "17:00:00")],
            [new AvailabilityJson("Sunday", "09:00:00", "14:00:00")]);
        Assert.Equal(HttpStatusCode.OK, createdResponse.StatusCode);
        var created = await ReadJsonAsync(createdResponse);
        Assert.Equal(500m, created.GetProperty("online").GetProperty("price").GetDecimal());
        Assert.Equal("10:00:00", created.GetProperty("online").GetProperty("availability")[0]
            .GetProperty("startTime").GetString());
        Assert.Equal("09:00:00", created.GetProperty("onsite").GetProperty("availability")[0]
            .GetProperty("startTime").GetString());

        var updatedResponse = await PutGroupedSettingsAsync(
            client,
            700m,
            created.GetProperty("rowVersion").GetString(),
            [
                new AvailabilityJson("Sunday", "08:00:00", "18:00:00"),
                new AvailabilityJson("Friday", "10:00:00", "16:00:00")
            ],
            [
                new AvailabilityJson("Sunday", "08:30:00", "15:00:00"),
                new AvailabilityJson("Thursday", "09:00:00", "14:00:00")
            ]);
        Assert.Equal(HttpStatusCode.OK, updatedResponse.StatusCode);
        var updated = await ReadJsonAsync(updatedResponse);
        Assert.NotEqual(
            created.GetProperty("rowVersion").GetString(),
            updated.GetProperty("rowVersion").GetString());
        Assert.Equal(
            ["Sunday", "Friday"],
            updated.GetProperty("online").GetProperty("availability").EnumerateArray()
                .Select(item => item.GetProperty("dayOfWeek").GetString()!).ToArray());
        Assert.Equal(
            ["Sunday", "Thursday"],
            updated.GetProperty("onsite").GetProperty("availability").EnumerateArray()
                .Select(item => item.GetProperty("dayOfWeek").GetString()!).ToArray());
    }

    private static Task<HttpResponseMessage> PutSettingsAsync(
        HttpClient client,
        decimal price,
        string? rowVersion,
        params AvailabilityJson[] availability)
        => PutGroupedSettingsAsync(client, price, rowVersion, availability, []);

    private static Task<HttpResponseMessage> PutGroupedSettingsAsync(
        HttpClient client,
        decimal price,
        string? rowVersion,
        AvailabilityJson[] onlineAvailability,
        AvailabilityJson[] onsiteAvailability)
        => client.PutAsJsonAsync("/api/v1/lawyer/consultation-settings", new
        {
            online = new
            {
                price,
                availability = onlineAvailability.Select(item => new
                {
                    dayOfWeek = item.DayOfWeek,
                    startTime = item.StartTime,
                    endTime = item.EndTime
                })
            },
            onsite = new
            {
                availability = onsiteAvailability.Select(item => new
                {
                    dayOfWeek = item.DayOfWeek,
                    startTime = item.StartTime,
                    endTime = item.EndTime
                })
            },
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

    private static int DayIndex(string day)
        => (int)Enum.Parse<DayOfWeek>(day);

    private sealed record AvailabilityJson(string DayOfWeek, string StartTime, string EndTime);

    private sealed record SettingsUpdate(decimal Price, AvailabilityJson[] Availability);
}
