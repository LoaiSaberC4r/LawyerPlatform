using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace LawyerPlatform.Infrastructure.Persistence;

public sealed class LawyerPlatformDbContextFactory
    : IDesignTimeDbContextFactory<LawyerPlatformDbContext>
{
    private const string EnvironmentFileName = ".env";
    private const string UserSecretsId = "LawyerPlatform.Api";

    public LawyerPlatformDbContext CreateDbContext(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var environment =
            Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? "Development";

        var apiProjectPath = ResolveApiProjectPath();
        var environmentFilePath = ResolveEnvironmentFilePath(
            apiProjectPath);

        var configurationBuilder = new ConfigurationBuilder()
            .SetBasePath(apiProjectPath)
            .AddJsonFile(
                "appsettings.json",
                optional: false,
                reloadOnChange: false)
            .AddJsonFile(
                $"appsettings.{environment}.json",
                optional: true,
                reloadOnChange: false);

        if (string.Equals(environment, "Development", StringComparison.OrdinalIgnoreCase))
        {
            configurationBuilder.AddJsonFile(
                ResolveUserSecretsFilePath(),
                optional: true,
                reloadOnChange: false);
        }

        if (environmentFilePath is not null)
        {
            var environmentValues =
                ReadEnvironmentFile(environmentFilePath);

            configurationBuilder.AddInMemoryCollection(
                environmentValues);
        }

        // Real operating-system or container environment variables
        // override appsettings files and the local .env file.
        configurationBuilder.AddEnvironmentVariables();

        var commandLineValues = ReadCommandLineConfiguration(args);
        if (commandLineValues.Count > 0)
        {
            configurationBuilder.AddInMemoryCollection(commandLineValues);
        }

        var configuration = configurationBuilder.Build();

        var connectionString =
            configuration.GetConnectionString("Database")
            ?? throw new InvalidOperationException(
                "Connection string 'Database' was not found. " +
                "Configure 'ConnectionStrings:Database' using API User Secrets, " +
                "an environment variable, or a command-line argument.");

        var migrationsAssemblyName =
            typeof(LawyerPlatformDbContext)
                .Assembly
                .GetName()
                .Name
            ?? throw new InvalidOperationException(
                "Unable to resolve the migrations assembly name.");

        var optionsBuilder =
            new DbContextOptionsBuilder<LawyerPlatformDbContext>();

        optionsBuilder.UseSqlServer(
            connectionString,
            sqlServerOptions =>
                sqlServerOptions.MigrationsAssembly(
                    migrationsAssemblyName));

        return new LawyerPlatformDbContext(
            optionsBuilder.Options);
    }

    private static Dictionary<string, string?>
        ReadEnvironmentFile(string filePath)
    {
        var values = new Dictionary<string, string?>(
            StringComparer.OrdinalIgnoreCase);

        var lines = File.ReadAllLines(filePath);

        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index].Trim();

            if (string.IsNullOrWhiteSpace(line) ||
                line.StartsWith('#'))
            {
                continue;
            }

            if (line.StartsWith(
                "export ",
                StringComparison.OrdinalIgnoreCase))
            {
                line = line["export ".Length..].Trim();
            }

            var separatorIndex = line.IndexOf('=');

            if (separatorIndex <= 0)
            {
                throw new InvalidOperationException(
                    $"Invalid .env entry at line {index + 1}.");
            }

            var key = line[..separatorIndex].Trim();

            if (string.IsNullOrWhiteSpace(key))
            {
                throw new InvalidOperationException(
                    $"Invalid .env key at line {index + 1}.");
            }

            var value = line[(separatorIndex + 1)..].Trim();

            value = RemoveSurroundingQuotes(value);

            values[key] = value;
        }

        return values;
    }

    private static Dictionary<string, string?> ReadCommandLineConfiguration(string[] args)
    {
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < args.Length; index++)
        {
            var argument = args[index];
            if (!argument.StartsWith("--", StringComparison.Ordinal))
            {
                continue;
            }

            var keyAndValue = argument[2..];
            var separatorIndex = keyAndValue.IndexOf('=');
            if (separatorIndex > 0)
            {
                values[keyAndValue[..separatorIndex]] = keyAndValue[(separatorIndex + 1)..];
                continue;
            }

            if (keyAndValue.Length > 0 &&
                index + 1 < args.Length &&
                !args[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                values[keyAndValue] = args[++index];
            }
        }

        return values;
    }

    private static string ResolveUserSecretsFilePath()
    {
        if (OperatingSystem.IsWindows())
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Microsoft",
                "UserSecrets",
                UserSecretsId,
                "secrets.json");
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".microsoft",
            "usersecrets",
            UserSecretsId,
            "secrets.json");
    }

    private static string RemoveSurroundingQuotes(
        string value)
    {
        if (value.Length < 2)
        {
            return value;
        }

        var startsAndEndsWithDoubleQuotes =
            value.StartsWith('"') &&
            value.EndsWith('"');

        var startsAndEndsWithSingleQuotes =
            value.StartsWith('\'') &&
            value.EndsWith('\'');

        return startsAndEndsWithDoubleQuotes ||
               startsAndEndsWithSingleQuotes
            ? value[1..^1]
            : value;
    }

    private static string? ResolveEnvironmentFilePath(
        string apiProjectPath)
    {
        var currentDirectory = new DirectoryInfo(
            Directory.GetCurrentDirectory());

        var environmentFilePath =
            FindFileInCurrentOrParentDirectories(
                currentDirectory,
                EnvironmentFileName);

        if (environmentFilePath is not null)
        {
            return environmentFilePath;
        }

        var apiDirectory = new DirectoryInfo(apiProjectPath);

        return FindFileInCurrentOrParentDirectories(
            apiDirectory,
            EnvironmentFileName);
    }

    private static string?
        FindFileInCurrentOrParentDirectories(
            DirectoryInfo? directory,
            string fileName)
    {
        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                fileName);

            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }

    private static string ResolveApiProjectPath()
    {
        var directory = new DirectoryInfo(
            Directory.GetCurrentDirectory());

        while (directory is not null)
        {
            var directCandidate = Path.Combine(
                directory.FullName,
                "LawyerPlatform.Api");

            if (File.Exists(
                Path.Combine(
                    directCandidate,
                    "appsettings.json")))
            {
                return directCandidate;
            }

            var solutionCandidate = Path.Combine(
                directory.FullName,
                "src",
                "LawyerPlatform",
                "LawyerPlatform.Api");

            if (File.Exists(
                Path.Combine(
                    solutionCandidate,
                    "appsettings.json")))
            {
                return solutionCandidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "Unable to locate " +
            "LawyerPlatform.Api/appsettings.json.");
    }
}
