using LawyerPlatform.Domain.Lawyers;

namespace LawyerPlatform.Application.Features.Lawyers.Common;

internal static class RowVersionCodec
{
    public static string Encode(byte[] rowVersion) => Convert.ToBase64String(rowVersion);

    public static bool TryDecode(string? value, out byte[] rowVersion)
    {
        rowVersion = [];
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim().Trim('"');
        try
        {
            rowVersion = Convert.FromBase64String(normalized);
            return rowVersion.Length > 0;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public static BuildingBlock.Domain.Results.Result<byte[]> Decode(string? value)
        => TryDecode(value, out var rowVersion)
            ? BuildingBlock.Domain.Results.Result<byte[]>.Ok(rowVersion)
            : BuildingBlock.Domain.Results.Result<byte[]>.Fail(LawyerErrors.InvalidRowVersion);
}
