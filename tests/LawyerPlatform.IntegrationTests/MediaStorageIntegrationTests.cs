using System.Net;
using LawyerPlatform.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;

namespace LawyerPlatform.IntegrationTests;

public sealed class MediaStorageIntegrationTests
{
    private const string LawyerId = "945e8e5016984202b634590e5477d495";
    private const string ProfileFileName = "existing-profile.png";

    [Fact]
    public async Task ExistingStyleProfileFile_RemainsStaticAcrossApplicationRestart_WhileDocumentsStayPrivate()
    {
        var testRoot = Path.Combine(
            Path.GetTempPath(),
            "LawyerPlatformRestartTests",
            Guid.NewGuid().ToString("N"));
        var profileBytes = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");
        var documentBytes = System.Text.Encoding.ASCII.GetBytes("%PDF-1.4\n%%EOF");
        var profileStorageKey = $"lawyers/{LawyerId}/profile/{ProfileFileName}";
        var documentStorageKey = $"lawyers/{LawyerId}/documents/private.pdf";

        await using (var firstFactory = new CustomWebApplicationFactory(testRoot))
        {
            var profilePath = CombineMediaPath(firstFactory.MediaRootPath, profileStorageKey);
            var documentPath = CombineMediaPath(firstFactory.MediaRootPath, documentStorageKey);
            Directory.CreateDirectory(Path.GetDirectoryName(profilePath)!);
            Directory.CreateDirectory(Path.GetDirectoryName(documentPath)!);
            await File.WriteAllBytesAsync(profilePath, profileBytes, TestContext.Current.CancellationToken);
            await File.WriteAllBytesAsync(documentPath, documentBytes, TestContext.Current.CancellationToken);

            using var firstClient = firstFactory.CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost")
            });
            Assert.Equal(
                Path.GetFullPath(firstFactory.MediaRootPath),
                firstFactory.Services.GetLawyerPlatformMediaStorageRoot());
            await AssertProfileAndPrivacyAsync(
                firstClient,
                profileStorageKey,
                documentStorageKey,
                profileBytes,
                TestContext.Current.CancellationToken);
        }

        await using (var restartedFactory = new CustomWebApplicationFactory(testRoot))
        {
            using var restartedClient = restartedFactory.CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost")
            });
            await AssertProfileAndPrivacyAsync(
                restartedClient,
                profileStorageKey,
                documentStorageKey,
                profileBytes,
                TestContext.Current.CancellationToken);
        }
    }

    private static async Task AssertProfileAndPrivacyAsync(
        HttpClient client,
        string profileStorageKey,
        string documentStorageKey,
        byte[] profileBytes,
        CancellationToken cancellationToken)
    {
        using var profileResponse = await client.GetAsync($"/uploads/{profileStorageKey}", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, profileResponse.StatusCode);
        Assert.Equal("image/png", profileResponse.Content.Headers.ContentType?.MediaType);
        Assert.Equal(profileBytes, await profileResponse.Content.ReadAsByteArrayAsync(cancellationToken));

        using var documentResponse = await client.GetAsync($"/uploads/{documentStorageKey}", cancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, documentResponse.StatusCode);
    }

    private static string CombineMediaPath(string mediaRoot, string storageKey)
        => Path.Combine(mediaRoot, storageKey.Replace('/', Path.DirectorySeparatorChar));
}
