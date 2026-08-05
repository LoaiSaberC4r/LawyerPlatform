using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace LawyerPlatform.IntegrationTests;

public sealed class CorsEndpointsTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private const string AllowedOrigin = "http://localhost:4200";
    private const string DisallowedOrigin = "https://attacker.example";

    [Fact]
    public async Task AllowedSimpleRequest_ReturnsConfiguredOrigin()
    {
        using var client = CreateHttpsClient(factory);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("Origin", AllowedOrigin);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(AllowedOrigin, Assert.Single(GetHeaderValues(response, "Access-Control-Allow-Origin")));
    }

    [Fact]
    public async Task DisallowedSimpleRequest_DoesNotReturnAllowOriginHeader()
    {
        using var client = CreateHttpsClient(factory);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("Origin", DisallowedOrigin);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task AllowedPreflight_ReturnsMethodAndRequestedHeadersWithoutCredentials()
    {
        using var client = CreateHttpsClient(factory);
        using var request = CreatePreflightRequest(
            AllowedOrigin,
            HttpMethod.Get,
            "authorization,content-type,accept-language,x-correlation-id");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(AllowedOrigin, Assert.Single(GetHeaderValues(response, "Access-Control-Allow-Origin")));
        Assert.Contains("GET", GetCommaSeparatedHeaderValues(response, "Access-Control-Allow-Methods"), StringComparer.OrdinalIgnoreCase);

        var allowedHeaders = GetCommaSeparatedHeaderValues(response, "Access-Control-Allow-Headers");
        Assert.Contains("authorization", allowedHeaders, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("content-type", allowedHeaders, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("accept-language", allowedHeaders, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("x-correlation-id", allowedHeaders, StringComparer.OrdinalIgnoreCase);
        Assert.False(response.Headers.Contains("Access-Control-Allow-Credentials"));
    }

    [Fact]
    public async Task DisallowedPreflight_DoesNotReturnAllowOriginHeader()
    {
        using var client = CreateHttpsClient(factory);
        using var request = CreatePreflightRequest(
            DisallowedOrigin,
            HttpMethod.Get,
            "authorization,content-type");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task AuthorizationAndCorrelationHeaders_AreAllowedByPreflight()
    {
        using var client = CreateHttpsClient(factory);
        using var request = CreatePreflightRequest(
            AllowedOrigin,
            HttpMethod.Get,
            "authorization,x-correlation-id");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        var allowedHeaders = GetCommaSeparatedHeaderValues(response, "Access-Control-Allow-Headers");
        Assert.Contains("authorization", allowedHeaders, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("x-correlation-id", allowedHeaders, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task IfMatchHeader_IsAllowedByPreflightForPut()
    {
        using var client = CreateHttpsClient(factory);
        using var request = CreatePreflightRequest(
            AllowedOrigin,
            HttpMethod.Put,
            "if-match,authorization,content-type",
            "/api/v1/lawyer/profile");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var allowedHeaders = GetCommaSeparatedHeaderValues(response, "Access-Control-Allow-Headers");
        Assert.Contains("if-match", allowedHeaders, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RequestWithoutOrigin_ExecutesNormallyWithoutCorsHeaders()
    {
        using var client = CreateHttpsClient(factory);

        using var response = await client.GetAsync("/health", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.False(response.Headers.Contains("Access-Control-Allow-Credentials"));
    }

    [Fact]
    public async Task ExplicitOriginWithCredentialsEnabled_ReturnsCredentialsHeader()
    {
        using var credentialsFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Cors:AllowAnyOrigin"] = "false",
                    ["Cors:AllowCredentials"] = "true"
                });
            });
        });
        using var client = CreateHttpsClient(credentialsFactory);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("Origin", AllowedOrigin);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(AllowedOrigin, Assert.Single(GetHeaderValues(response, "Access-Control-Allow-Origin")));
        Assert.Equal("true", Assert.Single(GetHeaderValues(response, "Access-Control-Allow-Credentials")));
    }

    private static HttpClient CreateHttpsClient(WebApplicationFactory<Program> applicationFactory)
        => applicationFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

    private static HttpRequestMessage CreatePreflightRequest(
        string origin,
        HttpMethod requestedMethod,
        string requestedHeaders,
        string path = "/api/v1/public/governorates")
    {
        var request = new HttpRequestMessage(HttpMethod.Options, path);
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", requestedMethod.Method);
        request.Headers.Add("Access-Control-Request-Headers", requestedHeaders);
        return request;
    }

    private static IEnumerable<string> GetHeaderValues(HttpResponseMessage response, string headerName)
    {
        Assert.True(response.Headers.TryGetValues(headerName, out var values));
        return values;
    }

    private static string[] GetCommaSeparatedHeaderValues(HttpResponseMessage response, string headerName)
        => GetHeaderValues(response, headerName)
            .SelectMany(value => value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ToArray();
}
