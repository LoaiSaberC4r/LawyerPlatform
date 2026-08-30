using System.Diagnostics;
using System.Globalization;
using System.Net;
using BuildingBlock.Api.ProblemDetails;
using BuildingBlock.Domain.Results;
using LawyerPlatform.Domain.Resources;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

namespace LawyerPlatform.Api.Errors;

internal sealed class LawyerPlatformProblemDetailsMapper(IWebHostEnvironment environment)
    : IProblemDetailsMapper
{
    private const int MaxDetailLength = 1000;
    private const string CorrelationItemKey = "__CorrelationId";

    public Microsoft.AspNetCore.Mvc.ProblemDetails Map(
        HttpContext httpContext,
        IReadOnlyCollection<Error> errors)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(errors);

        var localizedErrors = errors.Count == 0
            ? [Error.Unknown(ErrorCodes.Common.Unknown, ErrorMessage.UnknownErrorOccurred)]
            : errors.Select(LocalizeSharedError).ToArray();
        var primary = SelectPrimaryError(localizedErrors);
        var (status, typeUri, title) = Map(primary);

        var problem = new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Type = typeUri,
            Title = title,
            Status = (int)status,
            Detail = GetSafeDetail(primary),
            Instance = httpContext.Request.Path
        };

        AddCommonExtensions(problem, httpContext);
        problem.Extensions["errors"] = localizedErrors.Select(ToErrorPayload).ToArray();

        if (status == HttpStatusCode.TooManyRequests && primary.RetryAfter is { } retryAfter)
        {
            httpContext.Response.Headers.RetryAfter =
                ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
        }

        return problem;
    }

    public Microsoft.AspNetCore.Mvc.ProblemDetails MapException(
        HttpContext httpContext,
        Exception exception)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);

        var problem = new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Type = "about:blank#unknown",
            Title = ErrorMessage.UnexpectedErrorTitle,
            Status = StatusCodes.Status500InternalServerError,
            Detail = environment.IsDevelopment()
                ? Truncate(exception.ToString())
                : ErrorMessage.UnexpectedErrorOccurred,
            Instance = httpContext.Request.Path
        };

        AddCommonExtensions(problem, httpContext);
        problem.Extensions["errors"] = new[]
        {
            ToErrorPayload(Error.Unknown(
                ErrorCodes.Common.Unknown,
                ErrorMessage.UnexpectedErrorOccurred,
                source: "Exception"))
        };

        return problem;
    }

    private static Error LocalizeSharedError(Error error)
    {
        var message = error.Code switch
        {
            ErrorCodes.Common.Unauthorized => ErrorMessage.AuthenticationRequired,
            ErrorCodes.Common.Forbidden => ErrorMessage.AccessForbidden,
            ErrorCodes.Common.NotFound => ErrorMessage.RequestedResourceNotFound,
            ErrorCodes.Common.MethodNotAllowed => ErrorMessage.HttpMethodNotAllowed,
            ErrorCodes.Common.UnsupportedMediaType => ErrorMessage.UnsupportedContentType,
            ErrorCodes.Common.RateLimited => ErrorMessage.TooManyRequests,
            ErrorCodes.Common.Unknown => ErrorMessage.UnknownErrorOccurred,
            ErrorCodes.Validation.InvalidJson => ErrorMessage.InvalidJson,
            ErrorCodes.Validation.ModelBinding or ErrorCodes.Validation.Invalid => ErrorMessage.InvalidInput,
            _ => error.Message
        };

        return error with { Message = message };
    }

    private static Error SelectPrimaryError(IReadOnlyCollection<Error> errors)
        => errors.OrderBy(error => GetPrecedence(error.Type)).First();

    private static int GetPrecedence(ErrorType type) => type switch
    {
        ErrorType.Unknown => 0,
        ErrorType.Infrastructure => 1,
        ErrorType.Unauthorized => 2,
        ErrorType.Security => 3,
        ErrorType.RateLimit => 4,
        ErrorType.Conflict => 5,
        ErrorType.NotFound => 6,
        ErrorType.Validation => 7,
        ErrorType.Domain => 8,
        _ => 9
    };

    private static (HttpStatusCode Status, string TypeUri, string Title) Map(Error error)
    {
        if (error.Code == ErrorCodes.Common.MethodNotAllowed)
        {
            return (HttpStatusCode.MethodNotAllowed, "about:blank#method-not-allowed", ErrorMessage.MethodNotAllowedTitle);
        }

        if (error.Code == ErrorCodes.Common.UnsupportedMediaType)
        {
            return (HttpStatusCode.UnsupportedMediaType, "about:blank#unsupported-media-type", ErrorMessage.UnsupportedMediaTypeTitle);
        }

        return error.Type switch
        {
            ErrorType.Validation => (HttpStatusCode.UnprocessableEntity, "about:blank#validation", ErrorMessage.ValidationErrorTitle),
            ErrorType.Domain => (HttpStatusCode.UnprocessableEntity, "about:blank#domain", ErrorMessage.DomainErrorTitle),
            ErrorType.NotFound => (HttpStatusCode.NotFound, "about:blank#not-found", ErrorMessage.ResourceNotFoundTitle),
            ErrorType.Conflict => (HttpStatusCode.Conflict, "about:blank#conflict", ErrorMessage.ConflictTitle),
            ErrorType.Unauthorized => (HttpStatusCode.Unauthorized, "about:blank#unauthorized", ErrorMessage.UnauthorizedTitle),
            ErrorType.Security => (HttpStatusCode.Forbidden, "about:blank#security", ErrorMessage.SecurityErrorTitle),
            ErrorType.RateLimit => (HttpStatusCode.TooManyRequests, "about:blank#rate-limit", ErrorMessage.TooManyRequestsTitle),
            ErrorType.Infrastructure => (HttpStatusCode.ServiceUnavailable, "about:blank#infrastructure", ErrorMessage.InfrastructureErrorTitle),
            _ => (HttpStatusCode.InternalServerError, "about:blank#unknown", ErrorMessage.UnexpectedErrorTitle)
        };
    }

    private static void AddCommonExtensions(
        Microsoft.AspNetCore.Mvc.ProblemDetails problem,
        HttpContext httpContext)
    {
        problem.Extensions["traceId"] =
            Activity.Current?.TraceId.ToString() ?? httpContext.TraceIdentifier;

        if (httpContext.Items.TryGetValue(CorrelationItemKey, out var correlationId) &&
            !string.IsNullOrWhiteSpace(correlationId?.ToString()))
        {
            problem.Extensions["correlationId"] = correlationId.ToString();
        }
    }

    private LocalizedProblemDetailsErrorPayload ToErrorPayload(Error error)
    {
        var expose = ShouldExpose(error);
        return new LocalizedProblemDetailsErrorPayload(
            error.Code,
            expose ? Truncate(error.Message) : ErrorMessage.RequestCouldNotBeCompleted,
            error.Type.ToString(),
            expose && !string.IsNullOrWhiteSpace(error.Details) ? Truncate(error.Details) : null,
            expose ? error.Source : null,
            error.RetryAfter?.TotalSeconds);
    }

    private string GetSafeDetail(Error error)
        => ShouldExpose(error)
            ? Truncate(error.Message)
            : ErrorMessage.RequestCouldNotBeCompleted;

    private bool ShouldExpose(Error error)
        => error.Type is not (ErrorType.Infrastructure or ErrorType.Unknown) ||
           environment.IsDevelopment();

    private static string Truncate(string value)
        => value.Length <= MaxDetailLength ? value : value[..MaxDetailLength];
}

internal sealed record LocalizedProblemDetailsErrorPayload(
    string Code,
    string Message,
    string Type,
    string? Details,
    string? Source,
    double? RetryAfter);
