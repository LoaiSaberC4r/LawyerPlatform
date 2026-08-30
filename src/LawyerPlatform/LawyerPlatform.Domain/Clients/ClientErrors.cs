using BuildingBlock.Domain.Results;
using LawyerPlatform.Domain.Resources;

namespace LawyerPlatform.Domain.Clients;

public static class ClientErrors
{
    public static Error NotFound => Error.NotFound("Client.NotFound", ErrorMessage.ClientNotFound);
    public static Error InvalidRowVersion => Error.Validation("Client.InvalidRowVersion", ErrorMessage.InvalidRowVersion);
    public static Error ConcurrencyConflict => Error.Conflict("Client.ConcurrencyConflict", ErrorMessage.ClientConcurrencyConflict);
}
