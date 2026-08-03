# V3 Infrastructure Build Fix

This revision addresses a build failure in `LawyerPlatform.Infrastructure` when nullable warnings are treated as errors.

## Changes

- Replaced `Assembly.FullName` passed directly to `MigrationsAssembly(...)` with a non-null validated assembly name.
- Added null guards for the public dependency-injection extension method.
- Added a null guard for design-time `CreateDbContext` arguments to avoid analyzer noise under strict warning settings.
- Retains all V2 fixes for SDK roll-forward, OpenAPI, transaction abstractions, source-generated logging, cache naming, JWT claim constants, and SQLite pinning.

## Why the metadata errors appeared

`LawyerPlatform.Infrastructure.dll` was not generated because the Infrastructure project failed first. API and test projects then reported missing reference assemblies as cascading errors.
