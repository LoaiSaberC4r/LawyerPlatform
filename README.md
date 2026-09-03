# BuildingBlock + LawyerPlatform (.NET 10)

This repository is both:

1. A source-based reusable `BuildingBlock` framework.
2. A working Clean Architecture reference application showing how a consumer implements and configures the framework.
3. An installable `dotnet new` solution template.

## Solution structure

```text
src/
├── BuildingBlock/
│   ├── BuildingBlock.Domain
│   ├── BuildingBlock.Application
│   ├── BuildingBlock.Infrastructure
│   ├── BuildingBlock.Infrastructure.EntityFrameworkCore.SqlServer
│   ├── BuildingBlock.Api
│   └── BuildingBlock.Tests
└── LawyerPlatform/
    ├── LawyerPlatform.Domain
    ├── LawyerPlatform.Application
    ├── LawyerPlatform.Infrastructure
    └── LawyerPlatform.Api

tests/
├── LawyerPlatform.UnitTests
└── LawyerPlatform.IntegrationTests
```

The sample `CatalogItem` feature demonstrates:

- `Result` / `Result<T>`
- CQRS with MediatR
- FluentValidation pipeline behavior
- Generic read/write repositories
- Unit of Work
- Specification Pattern with projection and paging
- Opt-in caching and tag invalidation
- Soft delete, restore, and permanent delete
- Auditing
- Domain events without a Domain dependency on MediatR
- EF Core global query filters
- Shared ProblemDetails mapping
- Structured Serilog pipeline
- Localization and current-user abstractions

## Prerequisites

- .NET 10 SDK 10.0.100 or any later .NET 10 feature band or a later patch in the 10.0.3xx feature band
- Visual Studio 2026 18.8+ on Windows, or the `dotnet` CLI
- SQL Server LocalDB on Windows, or update the `Database` connection string

## Build and run

```powershell
dotnet tool restore
dotnet restore LawyerPlatform.sln
dotnet build LawyerPlatform.sln -c Release
dotnet test LawyerPlatform.sln -c Release --filter "Category!=SqlServerIntegration"
dotnet ef database update `
  --project src/LawyerPlatform/LawyerPlatform.Infrastructure `
  --startup-project src/LawyerPlatform/LawyerPlatform.Api
dotnet run --project src/LawyerPlatform/LawyerPlatform.Api
```

Swagger opens at `https://localhost:7080/swagger` in Development.

To use the included SQL Server container, copy `.env.example` to `.env`, replace the sample password, start `docker compose up -d`, and update the API connection string for the container.

The sample catalog CRUD endpoints are anonymous only to make the template immediately testable. The permanent-delete and internal diagnostics endpoints require an authenticated user. Replace the sample authorization rules before production use.

## Install as a template

From this repository root:

```powershell
dotnet new install .
```

Create a new solution:

```powershell
dotnet new buildingblock-sln `
  --name InventoryManagement `
  --output .\InventoryManagement
```

This changes only the consumer name `LawyerPlatform` to `InventoryManagement`. The reusable `BuildingBlock.*` project names remain unchanged.

## Verify the template

```powershell
.\scripts\verify.ps1
```

The verification script restores, builds, runs the non-Docker test suite, installs the template, creates a smoke-test solution, and builds the generated result. Run `./scripts/verify.ps1 -IncludeSqlServerIntegration` to include the SQL Server Testcontainers suite; Docker or an explicitly allowed SQL Server test connection is required for that optional suite.

## Production checklist

Before deployment:

- Build and publish the Release artifact, verify production configuration and secrets, and take and verify a SQL Server backup.
- Apply pending EF Core migrations once as a controlled deployment step; run approved seeding separately only when needed.
- Start every API instance with `DatabaseInitialization__ApplyMigrationsOnStartup=false` and `DatabaseInitialization__ApplySeedingOnStartup=false` so multiple instances never race to mutate the schema.
- Verify `/health`, database connectivity, the Outbox worker, a controlled smoke test, and startup logs after deployment.

- Move the JWT signing key to a secret store.
- Replace or integrate the authentication authority.
- Require authorization on business endpoints.
- Configure CORS explicitly.
- Configure persistent distributed caching when scaling horizontally.
- Add an Outbox before domain events trigger irreversible external effects.
- Review provider-specific migrations and connection resiliency.
- Keep diagnostics disabled in production unless explicitly secured and enabled.

## SDK selection

The repository requests `.NET SDK 10.0.100` and uses `rollForward: latestFeature`. Any installed stable `.NET 10` SDK feature band can be selected, while automatic roll-forward to `.NET 11` is not allowed.

Check the selected SDK before restoring:

```powershell
dotnet --list-sdks
dotnet --version
```
