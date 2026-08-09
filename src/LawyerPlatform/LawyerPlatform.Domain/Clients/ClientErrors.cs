using BuildingBlock.Domain.Results;

namespace LawyerPlatform.Domain.Clients;

public static class ClientErrors
{
    public static readonly Error NotFound = Error.NotFound("Client.NotFound", "The client profile was not found.");
    public static readonly Error InvalidRowVersion = Error.Validation("Client.InvalidRowVersion", "RowVersion must be a valid Base64 value.");
    public static readonly Error ConcurrencyConflict = Error.Conflict("Client.ConcurrencyConflict", "The client profile was changed by another request.");
}
