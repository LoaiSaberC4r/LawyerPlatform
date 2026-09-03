using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Threading.RateLimiting;
using Asp.Versioning;
using BuildingBlock.Api;
using BuildingBlock.Api.Bootstrap;
using BuildingBlock.Api.Logging;
using BuildingBlock.Api.OpenApi;
using BuildingBlock.Api.ProblemDetails;
using BuildingBlock.Api.Security;
using BuildingBlock.Domain.Results;
using BuildingBlock.Infrastructure.Bootstrap;
using LawyerPlatform.Api.Authorization;
using LawyerPlatform.Api.Configuration;
using LawyerPlatform.Api.Errors;
using LawyerPlatform.Api.Middleware;
using LawyerPlatform.Api.OpenApi;
using LawyerPlatform.Application;
using LawyerPlatform.Application.Abstractions.Authentication;
using LawyerPlatform.Domain.Accounts;
using LawyerPlatform.Domain.Resources;
using LawyerPlatform.Infrastructure;
using LawyerPlatform.Infrastructure.Media;
using LawyerPlatform.Infrastructure.Options;
using LawyerPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

EnvironmentFileLoader.LoadIfDevelopment(args);
JwtSecurityTokenHandler.DefaultMapInboundClaims = false;

var builder = WebApplication.CreateBuilder(args);

builder.AddBuildingBlockSerilog("LawyerPlatform.Api");

builder.Services
    .AddControllers()
    .AddApplicationPart(typeof(BuildingBlock.Api.ProblemDetailsMappingMvc).Assembly);

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    options.OperationFilter<ParameterDescriptionOperationFilter>();
    options.SchemaFilter<ConsultationTypeSchemaFilter>();
});

builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = false;
    options.ReportApiVersions = true;

    options.ApiVersionReader =
        new UrlSegmentApiVersionReader();
})
.AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

builder.Services.AddBuildingBlockSwagger(options =>
{
    options.ApiTitle = "LawyerPlatform API";
});

builder.Services.AddBuildingBlockLocalization(builder.Configuration);
builder.Services.AddBuildingBlockProblemDetails();
builder.Services.Replace(
    ServiceDescriptor.Singleton<IProblemDetailsMapper, LawyerPlatformProblemDetailsMapper>());
builder.Services.AddBuildingBlockCurrentUser();
builder.Services.AddBuildingBlockTokenReader(builder.Configuration);

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
    });

builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<Microsoft.Extensions.Options.IOptions<JwtOptions>>(
        (options, jwtOptionsAccessor) =>
        {
            var jwtOptions = jwtOptionsAccessor.Value;

            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,

                ValidIssuer = jwtOptions.Issuer,
                ValidAudience = jwtOptions.Audience,

                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(jwtOptions.Key)),

                ClockSkew = TimeSpan.FromMinutes(1),

                NameClaimType = LawyerPlatformClaimTypes.PreferredUserName,
                RoleClaimType = LawyerPlatformClaimTypes.Role
            };

            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = async context =>
                {
                    var subjectValue = context.Principal?
                        .FindFirst(LawyerPlatformClaimTypes.Subject)?.Value;
                    var credentialVersionValue = context.Principal?
                        .FindFirst(LawyerPlatformClaimTypes.CredentialVersion)?.Value;
                    if (!Guid.TryParse(subjectValue, out var userAccountId) ||
                        userAccountId == Guid.Empty ||
                        !int.TryParse(
                            credentialVersionValue,
                            NumberStyles.None,
                            CultureInfo.InvariantCulture,
                            out var credentialVersion) ||
                        credentialVersion <= 0)
                    {
                        context.Fail("Invalid access token.");
                        return;
                    }

                    var stateReader = context.HttpContext.RequestServices
                        .GetRequiredService<IUserAuthenticationStateReader>();
                    var state = await stateReader.ReadAsync(
                        userAccountId,
                        context.HttpContext.RequestAborted);
                    if (state is null ||
                        state.Status != AccountStatus.Active ||
                        state.CredentialVersion != credentialVersion)
                    {
                        context.Fail("Invalid access token.");
                    }
                }
            };
        });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("BuildingBlockDiagnostics", policy =>
        policy.RequireAuthenticatedUser());

    options.AddPolicy("SuperAdminOnly", policy =>
        policy.RequireRole(AccountRole.SuperAdmin.ToString()));

    options.AddPolicy("LawyerOnly", policy =>
        policy.RequireRole(AccountRole.Lawyer.ToString()));

    options.AddPolicy("ClientOnly", policy =>
        policy.RequireRole(AccountRole.Client.ToString()));
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddFixedWindowLimiter("auth", limiter =>
    {
        limiter.PermitLimit = 20;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
        limiter.AutoReplenishment = true;
        limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });

    options.AddPolicy("password-recovery-request", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(15),
                QueueLimit = 0,
                AutoReplenishment = true,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
            }));

    options.AddPolicy("password-recovery-verify", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromMinutes(15),
                QueueLimit = 0,
                AutoReplenishment = true,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
            }));

    options.AddPolicy("password-recovery-reset", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(15),
                QueueLimit = 0,
                AutoReplenishment = true,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
            }));

    options.AddFixedWindowLimiter("consultation-guest", limiter =>
    {
        limiter.PermitLimit = 20;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
        limiter.AutoReplenishment = true;
        limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });

    options.AddFixedWindowLimiter("consultation-track", limiter =>
    {
        limiter.PermitLimit = 20;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
        limiter.AutoReplenishment = true;
        limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });

    options.OnRejected = async (context, cancellationToken) =>
    {
        cancellationToken.ThrowIfCancellationRequested();

        var isConsultationRequest =
            context.HttpContext.Request.Path.StartsWithSegments(
                "/api/v1/public/consultation-requests",
                StringComparison.OrdinalIgnoreCase);

        var isPasswordRecoveryRequest =
            context.HttpContext.Request.Path.StartsWithSegments(
                "/api/v1/auth/forgot-password",
                StringComparison.OrdinalIgnoreCase);

        var isContactInquiry =
            context.HttpContext.Request.Path.StartsWithSegments(
                "/api/v1/public/contact-us",
                StringComparison.OrdinalIgnoreCase);

        var retryAfter = context.Lease.TryGetMetadata(
            MetadataName.RetryAfter,
            out TimeSpan retryAfterMetadata)
            ? retryAfterMetadata
            : TimeSpan.FromMinutes(15);

        var error = isPasswordRecoveryRequest
            ? PasswordResetErrors.RateLimitExceeded(retryAfter)
            : isContactInquiry
                ? Error.RateLimit(
                    "ContactInquiry.RateLimitExceeded",
                    ErrorMessage.ContactInquiryRateLimitExceeded,
                    TimeSpan.FromMinutes(1))
                : isConsultationRequest
                ? Error.RateLimit(
                    "ConsultationRequest.RateLimitExceeded",
                    ErrorMessage.ConsultationRateLimitExceeded,
                    TimeSpan.FromMinutes(1))
                : Error.RateLimit(
                    "Auth.RateLimitExceeded",
                    ErrorMessage.AuthenticationRateLimitExceeded,
                    TimeSpan.FromMinutes(1));

        await new[] { error }
            .ToProblem(context.HttpContext)
            .ExecuteAsync(context.HttpContext);
    };
});

builder.Services
    .AddHealthChecks()
    .AddDbContextCheck<LawyerPlatformDbContext>();

builder.Services.AddLawyerPlatformCors(builder.Configuration);
builder.Services.AddLawyerPlatformApplication();
builder.Services.AddLawyerPlatformInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseBuildingBlockSerilog();

app.UseHttpsRedirection();

app.UseBuildingBlockLocalization();

var profileImagesOptions = app.Services
    .GetRequiredService<IOptions<ProfileImagesOptions>>()
    .Value;
var mediaStorageRoot = app.Services.GetLawyerPlatformMediaStorageRoot();
Directory.CreateDirectory(mediaStorageRoot);
var profileImagesFileProvider = new PhysicalFileProvider(mediaStorageRoot);
app.Lifetime.ApplicationStopped.Register(profileImagesFileProvider.Dispose);

app.Map(profileImagesOptions.PublicPathBase, profileImageFiles =>
{
    profileImageFiles.Use(async (context, next) =>
    {
        if (!ProfileImageStaticPathPolicy.IsAllowed(context.Request.Path.Value))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        await next(context);
    });
    profileImageFiles.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = profileImagesFileProvider
    });
    profileImageFiles.Run(context =>
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return Task.CompletedTask;
    });
});

// Serve Angular index.html automatically for "/".
app.UseDefaultFiles();

// Serve Angular JavaScript, CSS, images and assets from wwwroot.
app.UseStaticFiles();

app.UseRouting();

app.UseCors(CorsPolicyNames.Default);

app.UseRateLimiter();

app.UseAuthentication();

app.UseMiddleware<PasswordChangeRequiredMiddleware>();

app.UseAuthorization();

app.UseSwagger(options =>
{
    options.RouteTemplate =
        "swagger/{documentName}/swagger.json";
});

app.UseSwaggerUI(options =>
{
    options.RoutePrefix = "swagger";

    options.SwaggerEndpoint(
        "./v1/swagger.json",
        "LawyerPlatform API v1");
});

app.MapControllers();

app.MapHealthChecks("/health")
    .WithMetadata(new AllowPasswordChangeRequiredAttribute());

app.MapBuildingBlockLoggingDiagnostics();

// Angular SPA fallback.
// API, Swagger, Health and internal routes must never fall back to index.html.
app.MapFallback(async context =>
{
    var path = context.Request.Path;

    var isBackendPath =
        path.StartsWithSegments(
            "/api",
            StringComparison.OrdinalIgnoreCase) ||
        path.StartsWithSegments(
            "/swagger",
            StringComparison.OrdinalIgnoreCase) ||
        path.StartsWithSegments(
            "/health",
            StringComparison.OrdinalIgnoreCase) ||
        path.StartsWithSegments(
            "/internal",
            StringComparison.OrdinalIgnoreCase);

    if (isBackendPath)
    {
        context.Response.StatusCode =
            StatusCodes.Status404NotFound;

        return;
    }

    var fallbackWebRootPath =
        app.Environment.WebRootPath ??
        Path.Combine(
            app.Environment.ContentRootPath,
            "wwwroot");

    var indexFilePath =
        Path.Combine(
            fallbackWebRootPath,
            "index.html");

    if (!File.Exists(indexFilePath))
    {
        context.Response.StatusCode =
            StatusCodes.Status404NotFound;

        return;
    }

    context.Response.ContentType =
        "text/html; charset=utf-8";

    await context.Response.SendFileAsync(
        indexFilePath,
        context.RequestAborted);
})
.WithMetadata(new AllowPasswordChangeRequiredAttribute());

app.Run();

public partial class Program
{
}
