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
using LawyerPlatform.Api.Middleware;
using LawyerPlatform.Application;
using LawyerPlatform.Application.Abstractions.Authentication;
using LawyerPlatform.Domain.Accounts;
using LawyerPlatform.Infrastructure;
using LawyerPlatform.Infrastructure.Options;
using LawyerPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;

EnvironmentFileLoader.LoadIfDevelopment(args);
JwtSecurityTokenHandler.DefaultMapInboundClaims = false;

var builder = WebApplication.CreateBuilder(args);

builder.AddBuildingBlockSerilog("LawyerPlatform.Api");

builder.Services
    .AddControllers()
    .AddApplicationPart(typeof(BuildingBlock.Api.ProblemDetailsMappingMvc).Assembly);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
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
builder.Services.AddBuildingBlockCurrentUser();
builder.Services.AddBuildingBlockTokenReader(builder.Configuration);

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
    });

builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<Microsoft.Extensions.Options.IOptions<JwtOptions>>((options, jwtOptionsAccessor) =>
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
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key)),
            ClockSkew = TimeSpan.FromMinutes(1),
            NameClaimType = LawyerPlatformClaimTypes.PreferredUserName,
            RoleClaimType = LawyerPlatformClaimTypes.Role
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
        var isConsultationRequest = context.HttpContext.Request.Path.StartsWithSegments(
            "/api/v1/public/consultation-requests",
            StringComparison.OrdinalIgnoreCase);
        var error = isConsultationRequest
            ? Error.RateLimit(
                "ConsultationRequest.RateLimitExceeded",
                "Too many consultation requests.",
                TimeSpan.FromMinutes(1))
            : Error.RateLimit(
                "Auth.RateLimitExceeded",
                "Too many authentication attempts.",
                TimeSpan.FromMinutes(1));
        await new[] { error }.ToProblem(context.HttpContext).ExecuteAsync(context.HttpContext);
    };
});

builder.Services.AddHealthChecks()
    .AddDbContextCheck<LawyerPlatformDbContext>();

builder.Services.AddLawyerPlatformCors(builder.Configuration);
builder.Services.AddLawyerPlatformApplication();
builder.Services.AddLawyerPlatformInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseBuildingBlockSerilog();
app.UseHttpsRedirection();
app.UseBuildingBlockLocalization();
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

app.Run();

public partial class Program
{
}
