using Asp.Versioning;
using BuildingBlock.Api.Bootstrap;
using BuildingBlock.Api.Logging;
using BuildingBlock.Api.OpenApi;
using BuildingBlock.Api.ProblemDetails;
using BuildingBlock.Api.Security;
using BuildingBlock.Infrastructure.Bootstrap;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using LawyerPlatform.Application;
using LawyerPlatform.Infrastructure;
using LawyerPlatform.Infrastructure.Persistence;
using System.Text;

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

var jwtKey = builder.Configuration["Authentication:Jwt:SigningKey"]
    ?? throw new InvalidOperationException("Authentication:Jwt:SigningKey is not configured.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Authentication:Jwt:Issuer"],
            ValidAudience = builder.Configuration["Authentication:Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("BuildingBlockDiagnostics", policy =>
        policy.RequireAuthenticatedUser());
});

builder.Services.AddHealthChecks()
    .AddDbContextCheck<LawyerPlatformDbContext>();

builder.Services.AddLawyerPlatformApplication();
builder.Services.AddLawyerPlatformInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseBuildingBlockSerilog();
app.UseHttpsRedirection();
app.UseBuildingBlockLocalization();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
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
}

app.MapControllers();
app.MapHealthChecks("/health");
app.MapBuildingBlockLoggingDiagnostics();

app.Run();

public partial class Program
{
}
