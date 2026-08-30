using System.Globalization;
using System.Reflection;
using System.Xml.Linq;
using LawyerPlatform.Domain.Lawyers;
using LawyerPlatform.Domain.Resources;

namespace LawyerPlatform.UnitTests.Localization;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class CultureSensitiveGroup
{
    public const string Name = "CultureSensitive";
}

[Collection(CultureSensitiveGroup.Name)]
public sealed class ErrorMessageTests
{
    [Fact]
    public void EnglishAndArabicLookupsUseCurrentUiCulture()
    {
        using (new CultureScope("en"))
        {
            Assert.Equal(
                "Invalid username/email or password.",
                ErrorMessage.InvalidLogin);
        }

        using (new CultureScope("ar"))
        {
            Assert.Equal(
                "اسم المستخدم/البريد الإلكتروني أو كلمة المرور غير صحيحة.",
                ErrorMessage.InvalidLogin);
        }
    }

    [Fact]
    public void EnglishAndArabicResourcesContainExactlyTheSameKeys()
    {
        var resourceDirectory = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "LawyerPlatform",
            "LawyerPlatform.Domain",
            "Resources");
        var englishKeys = ReadKeys(Path.Combine(resourceDirectory, "ErrorMessage.resx"));
        var arabicKeys = ReadKeys(Path.Combine(resourceDirectory, "ErrorMessage.ar.resx"));

        Assert.NotEmpty(englishKeys);
        Assert.Equal(englishKeys, arabicKeys);
    }

    [Fact]
    public void DomainErrorCodeIsStableAndMessageDoesNotFreezeAcrossCultures()
    {
        string arabicMessage;
        string englishMessage;
        string arabicCode;
        string englishCode;

        using (new CultureScope("ar"))
        {
            var error = LawyerErrors.NotFound;
            arabicMessage = error.Message;
            arabicCode = error.Code;
        }

        using (new CultureScope("en"))
        {
            var error = LawyerErrors.NotFound;
            englishMessage = error.Message;
            englishCode = error.Code;
        }

        Assert.Equal("Lawyer.NotFound", arabicCode);
        Assert.Equal(arabicCode, englishCode);
        Assert.Equal("المحامي غير موجود.", arabicMessage);
        Assert.Equal("Lawyer was not found.", englishMessage);
        Assert.NotEqual(arabicMessage, englishMessage);
        Assert.Null(typeof(LawyerErrors).GetField(
            nameof(LawyerErrors.NotFound),
            BindingFlags.Public | BindingFlags.Static));
        Assert.NotNull(typeof(LawyerErrors).GetProperty(
            nameof(LawyerErrors.NotFound),
            BindingFlags.Public | BindingFlags.Static));
    }

    private static SortedSet<string> ReadKeys(string path)
        => new(
            XDocument.Load(path)
            .Root!
            .Elements("data")
            .Select(element => element.Attribute("name")!.Value),
            StringComparer.Ordinal);

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "LawyerPlatform.sln")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Unable to locate LawyerPlatform.sln.");
    }

    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo _originalCulture = CultureInfo.CurrentCulture;
        private readonly CultureInfo _originalUiCulture = CultureInfo.CurrentUICulture;

        public CultureScope(string cultureName)
        {
            var culture = CultureInfo.GetCultureInfo(cultureName);
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
        }

        public void Dispose()
        {
            CultureInfo.CurrentCulture = _originalCulture;
            CultureInfo.CurrentUICulture = _originalUiCulture;
        }
    }
}
