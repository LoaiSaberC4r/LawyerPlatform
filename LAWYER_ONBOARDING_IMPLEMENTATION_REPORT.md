# Lawyer Onboarding Implementation Report

## Implemented behavior

- Preserved anonymous lawyer registration at `POST /api/v1/auth/lawyers/register`; it creates one active Lawyer account and one Draft profile atomically without issuing a JWT or creating onboarding children.
- Added the LawyerProfile-owned office, specialization, document, and append-only approval-history entities.
- Added calculated profile completion, configured required-document checks, current-user-owned Lawyer operations, SuperAdmin review, and public eligibility filtering.
- Added the complete Draft → PendingApproval → Approved/Rejected/ChangesRequested lifecycle, ChangesRequested resubmission, and Approved → Suspended → Approved lifecycle.
- Added Base64 RowVersion validation and EF original-value handling, with concurrency conflicts mapped to `Lawyer.ConcurrencyConflict` (HTTP 409).
- Added safe local storage writes and reads, signature-aware upload validation through the existing BuildingBlock media abstraction, upload compensation, path traversal protection, private streaming, and soft document deletion.
- Added no-tracking, paginated Admin/public projections with deterministic ordering and no exposed password hashes, storage keys, paths, private file URLs, file bytes, or public registration numbers.

## Endpoint inventory

Registration:

- `POST /api/v1/auth/lawyers/register` — anonymous.

Lawyer (`LawyerOnly`):

- `GET /api/v1/lawyer/profile`
- `PUT /api/v1/lawyer/profile`
- `PUT /api/v1/lawyer/profile/image`
- `GET /api/v1/lawyer/profile/image`
- `PUT /api/v1/lawyer/office`
- `PUT /api/v1/lawyer/specializations`
- `GET /api/v1/lawyer/documents`
- `POST /api/v1/lawyer/documents`
- `GET /api/v1/lawyer/documents/{documentId}/content`
- `DELETE /api/v1/lawyer/documents/{documentId}`
- `GET /api/v1/lawyer/approval-status`
- `POST /api/v1/lawyer/submit-for-approval`

SuperAdmin (`SuperAdminOnly`):

- `GET /api/v1/admin/lawyers`
- `GET /api/v1/admin/lawyers/{lawyerId}`
- `GET /api/v1/admin/lawyers/{lawyerId}/profile-image`
- `GET /api/v1/admin/lawyers/{lawyerId}/documents/{documentId}/content`
- `POST /api/v1/admin/lawyers/{lawyerId}/approve`
- `POST /api/v1/admin/lawyers/{lawyerId}/reject`
- `POST /api/v1/admin/lawyers/{lawyerId}/request-changes`
- `POST /api/v1/admin/lawyers/{lawyerId}/suspend`
- `POST /api/v1/admin/lawyers/{lawyerId}/reactivate`

Public (anonymous):

- `GET /api/v1/public/lawyers`
- `GET /api/v1/public/lawyers/{lawyerId}`
- `GET /api/v1/public/lawyers/{lawyerId}/profile-image`

## Swagger request examples

Register:

```json
{
  "fullName": "Ahmed Mohamed Ali",
  "userName": "ahmed.lawyer",
  "email": "ahmed@example.com",
  "phoneNumber": "01012345678",
  "password": "StrongPassword1"
}
```

Update professional profile:

```json
{
  "fullName": "Ahmed Mohamed Ali",
  "professionalTitle": "Attorney at Law",
  "biography": "Commercial and civil litigation experience.",
  "yearsOfExperience": 12,
  "professionalRegistrationNumber": "LAW-123456",
  "rowVersion": "AAAAAAAAB9E="
}
```

Upsert primary office:

```json
{
  "governorateId": 1,
  "cityId": 10,
  "areaId": 100,
  "detailedAddress": "Court Street, Building 5",
  "publicPhoneNumber": "01012345678",
  "rowVersion": null
}
```

Replace specializations:

```json
{
  "specializationIds": [1, 2, 5],
  "rowVersion": "AAAAAAAAB9E="
}
```

Profile image uses `multipart/form-data` fields `image` and `rowVersion`. Document upload uses `multipart/form-data` fields `documentType` and `file`. Submit and document deletion use `If-Match: {base64-row-version}`.

Admin decisions:

```json
{ "rowVersion": "AAAAAAAAB9F=" }
```

```json
{ "reason": "Required reason", "rowVersion": "AAAAAAAAB9F=" }
```

```json
{ "explanation": "Please clarify the biography.", "rowVersion": "AAAAAAAAB9F=" }
```

Swagger generation is integration-tested for every endpoint listed above, including both multipart operations.

## Migration review

Migration: `20260803163031_AddLawyerOnboardingAndApprovalLifecycle`.

- Adds `SubmittedOnUtc` to `LawyerProfiles`.
- Creates `LawyerOffices`, `LawyerSpecializations`, `LawyerDocuments`, and `LawyerApprovalStatusHistory`.
- Creates the filtered unique primary-office index, composite specialization key, reverse specialization index, document lookup index, approval-history index, approval queue index, and preserves the registration-number unique index.
- Configures SQL Server RowVersion columns for profiles, offices, and documents.
- All new foreign keys use `Restrict`; there are no cascade deletes.
- No existing table is dropped. The old approval-status-only index is replaced by the required approval-status/submission-time index.
- No physical storage path or storage default is introduced into SQL.
- The model snapshot is updated and `dotnet ef migrations has-pending-model-changes` reports no drift.
- The migration was applied successfully to a Docker-backed SQL Server 2022 test database.

## Test and build results

- Release build: passed, 0 warnings, 0 errors.
- LawyerPlatform unit tests: 38 passed, 0 failed.
- LawyerPlatform architecture test: 1 passed, 0 failed (also included in the unit count).
- LawyerPlatform SQLite/API integration tests: 8 passed, 0 failed.
- LawyerPlatform Docker/SQL Server integration tests: 2 passed, 0 failed.
- No LawyerPlatform test was skipped.
- A solution-wide BuildingBlock test run was also attempted: 312 passed and 6 pre-existing BuildingBlock baseline/foundation tests failed because five public-API baselines are resolved from nonexistent root-level paths and `global.json` uses `latestFeature` while that test expects `latestPatch`. These failures are outside this task and BuildingBlock was intentionally not modified.

Key verification commands:

```powershell
dotnet build LawyerPlatform.sln -c Release --no-restore
dotnet test tests/LawyerPlatform.UnitTests/LawyerPlatform.UnitTests.csproj -c Release --no-restore
dotnet test tests/LawyerPlatform.UnitTests/LawyerPlatform.UnitTests.csproj -c Release --no-build --filter "FullyQualifiedName~ArchitectureTests"
dotnet test tests/LawyerPlatform.IntegrationTests/LawyerPlatform.IntegrationTests.csproj -c Release --no-restore --filter "Category!=SqlServerIntegration"
dotnet test tests/LawyerPlatform.IntegrationTests/LawyerPlatform.IntegrationTests.csproj -c Release --no-restore --filter "Category=SqlServerIntegration"
dotnet ef migrations add AddLawyerOnboardingAndApprovalLifecycle --project src/LawyerPlatform/LawyerPlatform.Infrastructure --startup-project src/LawyerPlatform/LawyerPlatform.Api
dotnet ef migrations has-pending-model-changes --project src/LawyerPlatform/LawyerPlatform.Infrastructure --startup-project src/LawyerPlatform/LawyerPlatform.Api --no-build
git diff --check
```

Additional read-only inspection commands included `rg`, `Get-Content`, `git status`, `git diff --stat`, `Get-FileHash`, and `docker info`.

## Assumptions and unresolved configuration

- The two supplied task attachments were byte-for-byte identical.
- `LAWYER_PLATFORM_SOURCE_OF_TRUTH.md` was not present, so it was created with only the requested approved-decision section.
- Required production document types and exact limits remain unresolved. Production `RequiredDocumentTypes` is intentionally empty; submission returns `Lawyer.DocumentRequirementsNotConfigured` until deployment configuration is approved.
- Automated tests use only the explicitly allowed sample document types `IdentityVerification` and `ProfessionalMembership`.
- The existing BuildingBlock media validation is reused for safe names, extension/MIME allowlists, detected signatures, bounded sizes, and optional malware scanning.
- Approved lawyers may upload replacement/additional documents and may remove a document only when doing so preserves all required document types. Rejected and suspended profiles remain read-only.
- Reactivation preserves the original approval actor and approval timestamp while clearing suspension fields and appending a new history entry.

## Dependency and BuildingBlock confirmation

- No BuildingBlock file was modified.
- No NuGet package or project reference was added or changed.
- Work remained on branch `codex/identity-auth-foundation`.

## Complete changed-file inventory

Top level:

- `LAWYER_ONBOARDING_IMPLEMENTATION_REPORT.md` (created)
- `LAWYER_PLATFORM_SOURCE_OF_TRUTH.md` (created)

API:

- `src/LawyerPlatform/LawyerPlatform.Api/appsettings.json` (modified)
- `src/LawyerPlatform/LawyerPlatform.Api/Contracts/Lawyers/AdminLawyerRequests.cs` (created)
- `src/LawyerPlatform/LawyerPlatform.Api/Contracts/Lawyers/LawyerRequests.cs` (created)
- `src/LawyerPlatform/LawyerPlatform.Api/Controllers/AdminLawyersController.cs` (created)
- `src/LawyerPlatform/LawyerPlatform.Api/Controllers/LawyerController.cs` (created)
- `src/LawyerPlatform/LawyerPlatform.Api/Controllers/PublicLawyersController.cs` (created)

Application abstractions and registration:

- `src/LawyerPlatform/LawyerPlatform.Application/DependencyInjection.cs` (modified)
- `src/LawyerPlatform/LawyerPlatform.Application/Abstractions/Lawyers/IConcurrencyTokenManager.cs` (created)
- `src/LawyerPlatform/LawyerPlatform.Application/Abstractions/Lawyers/ILawyerAggregatePersistence.cs` (created)
- `src/LawyerPlatform/LawyerPlatform.Application/Abstractions/Lawyers/ILawyerDocumentPolicy.cs` (created)
- `src/LawyerPlatform/LawyerPlatform.Application/Abstractions/Lawyers/IStoredFileReader.cs` (created)

Application Lawyer features:

- `src/LawyerPlatform/LawyerPlatform.Application/Features/Lawyers/Common/LawyerAggregateCompletionService.cs` (created)
- `src/LawyerPlatform/LawyerPlatform.Application/Features/Lawyers/Common/LawyerAggregateSpecifications.cs` (created)
- `src/LawyerPlatform/LawyerPlatform.Application/Features/Lawyers/Common/LawyerApplicationErrors.cs` (created)
- `src/LawyerPlatform/LawyerPlatform.Application/Features/Lawyers/Common/LawyerProfileCompletionCalculator.cs` (created)
- `src/LawyerPlatform/LawyerPlatform.Application/Features/Lawyers/Common/LawyerResponses.cs` (created)
- `src/LawyerPlatform/LawyerPlatform.Application/Features/Lawyers/Common/RowVersionCodec.cs` (created)
- `src/LawyerPlatform/LawyerPlatform.Application/Features/Lawyers/DeleteDocument/DeleteDocumentCommand.cs` (created)
- `src/LawyerPlatform/LawyerPlatform.Application/Features/Lawyers/GetApprovalStatus/GetApprovalStatusQuery.cs` (created)
- `src/LawyerPlatform/LawyerPlatform.Application/Features/Lawyers/GetOwnDocumentContent/GetOwnDocumentContentQuery.cs` (created)
- `src/LawyerPlatform/LawyerPlatform.Application/Features/Lawyers/GetOwnDocuments/GetOwnDocumentsQuery.cs` (created)
- `src/LawyerPlatform/LawyerPlatform.Application/Features/Lawyers/GetOwnProfile/GetOwnProfileQuery.cs` (created)
- `src/LawyerPlatform/LawyerPlatform.Application/Features/Lawyers/GetOwnProfileImage/GetOwnProfileImageQuery.cs` (created)
- `src/LawyerPlatform/LawyerPlatform.Application/Features/Lawyers/ReplaceSpecializations/ReplaceSpecializationsCommand.cs` (created)
- `src/LawyerPlatform/LawyerPlatform.Application/Features/Lawyers/SubmitForApproval/SubmitForApprovalCommand.cs` (created)
- `src/LawyerPlatform/LawyerPlatform.Application/Features/Lawyers/UpdateOwnProfile/UpdateOwnProfileCommand.cs` (created)
- `src/LawyerPlatform/LawyerPlatform.Application/Features/Lawyers/UpdateProfileImage/UpdateProfileImageCommand.cs` (created)
- `src/LawyerPlatform/LawyerPlatform.Application/Features/Lawyers/UploadDocument/UploadDocumentCommand.cs` (created)
- `src/LawyerPlatform/LawyerPlatform.Application/Features/Lawyers/UpsertPrimaryOffice/UpsertPrimaryOfficeCommand.cs` (created)

Application Admin features:

- `src/LawyerPlatform/LawyerPlatform.Application/Features/AdminLawyers/ApproveLawyer/ApproveLawyerCommand.cs` (created)
- `src/LawyerPlatform/LawyerPlatform.Application/Features/AdminLawyers/Common/AdminLawyerDecisionService.cs` (created)
- `src/LawyerPlatform/LawyerPlatform.Application/Features/AdminLawyers/GetLawyerDetails/GetLawyerDetailsQuery.cs` (created)
- `src/LawyerPlatform/LawyerPlatform.Application/Features/AdminLawyers/GetLawyerDocumentContent/GetLawyerDocumentContentQuery.cs` (created)
- `src/LawyerPlatform/LawyerPlatform.Application/Features/AdminLawyers/GetLawyerProfileImage/GetLawyerProfileImageQuery.cs` (created)
- `src/LawyerPlatform/LawyerPlatform.Application/Features/AdminLawyers/GetLawyers/GetLawyersQuery.cs` (created)
- `src/LawyerPlatform/LawyerPlatform.Application/Features/AdminLawyers/ReactivateLawyer/ReactivateLawyerCommand.cs` (created)
- `src/LawyerPlatform/LawyerPlatform.Application/Features/AdminLawyers/RejectLawyer/RejectLawyerCommand.cs` (created)
- `src/LawyerPlatform/LawyerPlatform.Application/Features/AdminLawyers/RequestLawyerChanges/RequestLawyerChangesCommand.cs` (created)
- `src/LawyerPlatform/LawyerPlatform.Application/Features/AdminLawyers/SuspendLawyer/SuspendLawyerCommand.cs` (created)

Application public features:

- `src/LawyerPlatform/LawyerPlatform.Application/Features/PublicLawyers/Common/PublicLawyerSpecifications.cs` (created)
- `src/LawyerPlatform/LawyerPlatform.Application/Features/PublicLawyers/GetLawyerDetails/GetPublicLawyerDetailsQuery.cs` (created)
- `src/LawyerPlatform/LawyerPlatform.Application/Features/PublicLawyers/GetLawyerProfileImage/GetPublicLawyerProfileImageQuery.cs` (created)
- `src/LawyerPlatform/LawyerPlatform.Application/Features/PublicLawyers/SearchLawyers/SearchLawyersQuery.cs` (created)

Domain:

- `src/LawyerPlatform/LawyerPlatform.Domain/Lawyers/LawyerProfile.cs` (modified)
- `src/LawyerPlatform/LawyerPlatform.Domain/Lawyers/LawyerApprovalStatusHistory.cs` (created)
- `src/LawyerPlatform/LawyerPlatform.Domain/Lawyers/LawyerDocument.cs` (created)
- `src/LawyerPlatform/LawyerPlatform.Domain/Lawyers/LawyerErrors.cs` (created)
- `src/LawyerPlatform/LawyerPlatform.Domain/Lawyers/LawyerOffice.cs` (created)
- `src/LawyerPlatform/LawyerPlatform.Domain/Lawyers/LawyerSpecialization.cs` (created)

Infrastructure:

- `src/LawyerPlatform/LawyerPlatform.Infrastructure/DependencyInjection.cs` (modified)
- `src/LawyerPlatform/LawyerPlatform.Infrastructure/Lawyers/ConcurrencyTokenManager.cs` (created)
- `src/LawyerPlatform/LawyerPlatform.Infrastructure/Lawyers/LawyerAggregatePersistence.cs` (created)
- `src/LawyerPlatform/LawyerPlatform.Infrastructure/Lawyers/LawyerDocumentPolicy.cs` (created)
- `src/LawyerPlatform/LawyerPlatform.Infrastructure/Lawyers/StoredFileReader.cs` (created)
- `src/LawyerPlatform/LawyerPlatform.Infrastructure/Options/LawyerDocumentOptions.cs` (created)
- `src/LawyerPlatform/LawyerPlatform.Infrastructure/Persistence/Configurations/LawyerApprovalStatusHistoryConfiguration.cs` (created)
- `src/LawyerPlatform/LawyerPlatform.Infrastructure/Persistence/Configurations/LawyerDocumentConfiguration.cs` (created)
- `src/LawyerPlatform/LawyerPlatform.Infrastructure/Persistence/Configurations/LawyerOfficeConfiguration.cs` (created)
- `src/LawyerPlatform/LawyerPlatform.Infrastructure/Persistence/Configurations/LawyerProfileConfiguration.cs` (modified)
- `src/LawyerPlatform/LawyerPlatform.Infrastructure/Persistence/Configurations/LawyerSpecializationConfiguration.cs` (created)
- `src/LawyerPlatform/LawyerPlatform.Infrastructure/Persistence/LawyerPlatformDbContext.cs` (modified)
- `src/LawyerPlatform/LawyerPlatform.Infrastructure/Persistence/LawyerPlatformUniqueConstraintExceptionMapper.cs` (modified)
- `src/LawyerPlatform/LawyerPlatform.Infrastructure/Persistence/Migrations/20260803163031_AddLawyerOnboardingAndApprovalLifecycle.cs` (created)
- `src/LawyerPlatform/LawyerPlatform.Infrastructure/Persistence/Migrations/20260803163031_AddLawyerOnboardingAndApprovalLifecycle.Designer.cs` (created)
- `src/LawyerPlatform/LawyerPlatform.Infrastructure/Persistence/Migrations/LawyerPlatformDbContextModelSnapshot.cs` (modified)

Tests:

- `tests/LawyerPlatform.IntegrationTests/AuthenticationEndpointsTests.cs` (modified)
- `tests/LawyerPlatform.IntegrationTests/CustomWebApplicationFactory.cs` (modified)
- `tests/LawyerPlatform.IntegrationTests/LawyerOnboardingLifecycleTests.cs` (created)
- `tests/LawyerPlatform.IntegrationTests/LawyerPersistenceModelTests.cs` (created)
- `tests/LawyerPlatform.IntegrationTests/SqlServerIdentityModelTests.cs` (modified)
- `tests/LawyerPlatform.IntegrationTests/SqlServerIdentityPersistenceTests.cs` (modified)
- `tests/LawyerPlatform.UnitTests/Lawyers/LawyerProfileLifecycleTests.cs` (created)
- `tests/LawyerPlatform.UnitTests/Lawyers/LawyerValidatorTests.cs` (created)
- `tests/LawyerPlatform.UnitTests/Lawyers/UploadDocumentHandlerTests.cs` (created)
