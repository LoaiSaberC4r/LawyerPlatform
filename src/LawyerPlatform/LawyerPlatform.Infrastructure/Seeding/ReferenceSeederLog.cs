using Microsoft.Extensions.Logging;

namespace LawyerPlatform.Infrastructure.Seeding;

internal static partial class ReferenceSeederLog
{
    [LoggerMessage(EventId = 4110, Level = LogLevel.Warning, Message = "Approved seed data is not available for {CatalogName}; the catalog remains empty.")]
    public static partial void NoApprovedData(ILogger logger, string catalogName);
}
