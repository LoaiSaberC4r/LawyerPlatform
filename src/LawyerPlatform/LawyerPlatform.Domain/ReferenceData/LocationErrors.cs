using BuildingBlock.Domain.Results;
using LawyerPlatform.Domain.Resources;

namespace LawyerPlatform.Domain.ReferenceData;

public static class GovernorateErrors
{
    public static Error Invalid => Error.Validation("Governorate.Invalid", ErrorMessage.GovernorateInvalid);
    public static Error NotFound => Error.NotFound("Governorate.NotFound", ErrorMessage.GovernorateNotFound);
    public static Error DuplicateNameAr => Error.Conflict("Governorate.DuplicateNameAr", ErrorMessage.GovernorateDuplicateNameAr);
    public static Error DuplicateNameEn => Error.Conflict("Governorate.DuplicateNameEn", ErrorMessage.GovernorateDuplicateNameEn);
    public static Error AlreadyActive => Error.Domain("Governorate.AlreadyActive", ErrorMessage.GovernorateAlreadyActive);
    public static Error AlreadyInactive => Error.Domain("Governorate.AlreadyInactive", ErrorMessage.GovernorateAlreadyInactive);
    public static Error InvalidRowVersion => Error.Validation("Governorate.InvalidRowVersion", ErrorMessage.InvalidRowVersion);
    public static Error ConcurrencyConflict => Error.Conflict("Governorate.ConcurrencyConflict", ErrorMessage.GovernorateConcurrencyConflict);
}

public static class CityErrors
{
    public static Error Invalid => Error.Validation("City.Invalid", ErrorMessage.CityInvalid);
    public static Error NotFound => Error.NotFound("City.NotFound", ErrorMessage.CityNotFound);
    public static Error DuplicateNameAr => Error.Conflict("City.DuplicateNameAr", ErrorMessage.CityDuplicateNameAr);
    public static Error DuplicateNameEn => Error.Conflict("City.DuplicateNameEn", ErrorMessage.CityDuplicateNameEn);
    public static Error InvalidGovernorate => Error.Validation("City.InvalidGovernorate", ErrorMessage.InvalidGovernorate);
    public static Error AlreadyActive => Error.Domain("City.AlreadyActive", ErrorMessage.CityAlreadyActive);
    public static Error AlreadyInactive => Error.Domain("City.AlreadyInactive", ErrorMessage.CityAlreadyInactive);
    public static Error InvalidRowVersion => Error.Validation("City.InvalidRowVersion", ErrorMessage.InvalidRowVersion);
    public static Error ConcurrencyConflict => Error.Conflict("City.ConcurrencyConflict", ErrorMessage.CityConcurrencyConflict);
}

public static class AreaErrors
{
    public static Error Invalid => Error.Validation("Area.Invalid", ErrorMessage.AreaInvalid);
    public static Error NotFound => Error.NotFound("Area.NotFound", ErrorMessage.AreaNotFound);
    public static Error DuplicateNameAr => Error.Conflict("Area.DuplicateNameAr", ErrorMessage.AreaDuplicateNameAr);
    public static Error DuplicateNameEn => Error.Conflict("Area.DuplicateNameEn", ErrorMessage.AreaDuplicateNameEn);
    public static Error InvalidCity => Error.Validation("Area.InvalidCity", ErrorMessage.InvalidCity);
    public static Error AlreadyActive => Error.Domain("Area.AlreadyActive", ErrorMessage.AreaAlreadyActive);
    public static Error AlreadyInactive => Error.Domain("Area.AlreadyInactive", ErrorMessage.AreaAlreadyInactive);
    public static Error InvalidRowVersion => Error.Validation("Area.InvalidRowVersion", ErrorMessage.InvalidRowVersion);
    public static Error ConcurrencyConflict => Error.Conflict("Area.ConcurrencyConflict", ErrorMessage.AreaConcurrencyConflict);
}
