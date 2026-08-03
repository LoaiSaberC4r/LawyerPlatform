namespace LawyerPlatform.Api.Authorization;

[AttributeUsage(AttributeTargets.Method)]
public sealed class AllowPasswordChangeRequiredAttribute : Attribute;
