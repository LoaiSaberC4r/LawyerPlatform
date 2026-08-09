using BuildingBlock.Domain.Results;

namespace LawyerPlatform.Domain.ReferenceData;

public static class GovernorateErrors
{
    public static readonly Error Invalid = Error.Validation("Governorate.Invalid", "Governorate data is invalid.");
    public static readonly Error NotFound = Error.NotFound("Governorate.NotFound", "The governorate was not found.");
    public static readonly Error DuplicateNameAr = Error.Conflict("Governorate.DuplicateNameAr", "The Arabic governorate name already exists.");
    public static readonly Error DuplicateNameEn = Error.Conflict("Governorate.DuplicateNameEn", "The English governorate name already exists.");
    public static readonly Error AlreadyActive = Error.Domain("Governorate.AlreadyActive", "The governorate is already active.");
    public static readonly Error AlreadyInactive = Error.Domain("Governorate.AlreadyInactive", "The governorate is already inactive.");
    public static readonly Error InvalidRowVersion = Error.Validation("Governorate.InvalidRowVersion", "RowVersion must be a valid Base64 value.");
    public static readonly Error ConcurrencyConflict = Error.Conflict("Governorate.ConcurrencyConflict", "The governorate was changed by another request.");
}

public static class CityErrors
{
    public static readonly Error Invalid = Error.Validation("City.Invalid", "City data is invalid.");
    public static readonly Error NotFound = Error.NotFound("City.NotFound", "The city was not found.");
    public static readonly Error DuplicateNameAr = Error.Conflict("City.DuplicateNameAr", "The Arabic city name already exists in this governorate.");
    public static readonly Error DuplicateNameEn = Error.Conflict("City.DuplicateNameEn", "The English city name already exists in this governorate.");
    public static readonly Error InvalidGovernorate = Error.Validation("City.InvalidGovernorate", "The selected governorate does not exist.");
    public static readonly Error AlreadyActive = Error.Domain("City.AlreadyActive", "The city is already active.");
    public static readonly Error AlreadyInactive = Error.Domain("City.AlreadyInactive", "The city is already inactive.");
    public static readonly Error InvalidRowVersion = Error.Validation("City.InvalidRowVersion", "RowVersion must be a valid Base64 value.");
    public static readonly Error ConcurrencyConflict = Error.Conflict("City.ConcurrencyConflict", "The city was changed by another request.");
}

public static class AreaErrors
{
    public static readonly Error Invalid = Error.Validation("Area.Invalid", "Area data is invalid.");
    public static readonly Error NotFound = Error.NotFound("Area.NotFound", "The area was not found.");
    public static readonly Error DuplicateNameAr = Error.Conflict("Area.DuplicateNameAr", "The Arabic area name already exists in this city.");
    public static readonly Error DuplicateNameEn = Error.Conflict("Area.DuplicateNameEn", "The English area name already exists in this city.");
    public static readonly Error InvalidCity = Error.Validation("Area.InvalidCity", "The selected city does not exist.");
    public static readonly Error AlreadyActive = Error.Domain("Area.AlreadyActive", "The area is already active.");
    public static readonly Error AlreadyInactive = Error.Domain("Area.AlreadyInactive", "The area is already inactive.");
    public static readonly Error InvalidRowVersion = Error.Validation("Area.InvalidRowVersion", "RowVersion must be a valid Base64 value.");
    public static readonly Error ConcurrencyConflict = Error.Conflict("Area.ConcurrencyConflict", "The area was changed by another request.");
}
