# Validation Report

## Completed static validation

The generated repository passed the following checks:

- All JSON files parse successfully.
- All MSBuild XML files parse successfully.
- All package references are centrally versioned.
- All project references resolve to existing projects.
- Both solution files reference existing projects.
- All 211 non-generated BuildingBlock C# source sections were extracted from the supplied source dump.
- No `bin`, `obj`, or `.vs` artifacts are included.
- No active `net8.0` or `net9.0` target remains.
- Production source contains no QControl-specific names or hidden assumptions.
- The reference Application layer has no EF Core or ASP.NET Core dependency.
- The reference Domain layer has no Application, Infrastructure, MediatR, EF Core, or ASP.NET Core dependency.
- The template was dry-run renamed from `LawyerPlatform` to `InventoryManagement`; filenames, namespaces, solution paths, and project references remained consistent.

## Runtime validation status

`dotnet restore`, `dotnet build`, and `dotnet test` could not be executed in the artifact-generation environment because the .NET SDK is not installed and outbound SDK installation is unavailable there.

Run this command on a machine with any installed stable .NET 10 SDK:

```powershell
.\scripts\verify.ps1
```

This performs restore, Release build, the non-Docker test suite, template installation, generation of a renamed smoke-test solution, and a build of that generated solution.

To include the optional real SQL Server/Testcontainers suite:

```powershell
.\scripts\verify.ps1 -IncludeSqlServerIntegration
```

## Corrective revision

This archive includes corrections for the first published template revision: flexible .NET 10 SDK selection, OpenAPI 3.x media contracts, transaction-marker namespace placement, source-generated logging, and SQLite native package pinning.
