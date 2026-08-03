using BuildingBlock.Api;
using LawyerPlatform.Api.Authorization;
using LawyerPlatform.Application.Abstractions.Authentication;
using LawyerPlatform.Domain.Accounts;

namespace LawyerPlatform.Api.Middleware;

internal sealed class PasswordChangeRequiredMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var changeRequired = context.User.Identity?.IsAuthenticated == true &&
            string.Equals(
                context.User.FindFirst(LawyerPlatformClaimTypes.PasswordChangeRequired)?.Value,
                "true",
                StringComparison.OrdinalIgnoreCase);

        if (changeRequired && context.GetEndpoint()?.Metadata.GetMetadata<AllowPasswordChangeRequiredAttribute>() is null)
        {
            await new[] { AccountErrors.PasswordChangeRequired }.ToProblem(context).ExecuteAsync(context);
            return;
        }

        await next(context);
    }
}
