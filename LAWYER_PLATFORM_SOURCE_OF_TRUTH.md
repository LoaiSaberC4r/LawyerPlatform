# Lawyer Platform Source of Truth

## Approved Decision — Lawyer Onboarding Lifecycle

- Lawyer registration creates an active `UserAccount` with the `Lawyer` role and a `Draft` `LawyerProfile` only.
- Professional profile completion occurs after the Lawyer signs in.
- Primary office, legal specializations, and private documents are managed as separate onboarding sections owned by the `LawyerProfile` aggregate.
- Submission for approval is allowed only from `Draft` and `ChangesRequested`.
- SuperAdmin controls approval, rejection, change requests, suspension, and reactivation.
- Permitted edits to an approved profile do not automatically remove approval or trigger automatic reapproval.
- Exact required document types, allowed extensions and MIME types, and file-size limits remain deployment configuration decisions under `LawyerDocuments`.

## Approved Decision — Office Coordinates and Email Branding

- `LawyerOffice` supports optional `Latitude` and `Longitude`; both values must be supplied together or both omitted.
- Coordinates are visible only to the owning Lawyer and SuperAdmin, and are never exposed through public, Client, or Guest API responses.
- Coordinates are not required for profile completion, submission, approval, or public eligibility.
- When a Consultation Request is approved, the requester email may include a Google Maps URL generated from the active primary office coordinates.
- The Google Maps URL is generated at notification preparation time and is not persisted; missing coordinates omit the map section.
- Client and Guest content does not display raw coordinates.
- The human-facing email brand is `Avokatoo`; technical project identifiers remain unchanged.

---

## Approved Later-Phase Decision — Lawyer Consultation Settings

- A Lawyer owns one independently concurrent Consultation Settings aggregate for the `Online` and `Onsite` consultation types.
- Online has the current positive consultation price and its own zero to seven weekly availability periods; Onsite has an independent zero to seven weekly availability periods and no platform price.
- Availability contains at most one working period per `ConsultationType` and `DayOfWeek`; the same day may be configured once for each type, an omitted day is unavailable for that type, and each period requires `StartTime < EndTime`.
- `PreferredAppointmentOnUtc` remains optional for Guest and Client Consultation Requests.
- Guest and Client creation requires a `ConsultationType`; when an appointment is supplied, the backend converts it from UTC into the configured business timezone and requires its local day and time to match availability for that selected type. Start and end boundaries are inclusive.
- Online requests snapshot the current Online consultation price at creation; Onsite requests always have a `null` consultation price.
- When Consultation Settings do not exist, an Onsite contact request without an appointment remains valid; Online requests and all appointment requests are rejected because their required settings are unavailable.
- Later Lawyer price changes never modify old Consultation Requests. Request details always use the historical snapshot, while public Lawyer APIs use the current price.
- Consultation Settings are operational settings with independent optimistic concurrency and do not modify Lawyer approval or public-eligibility state.
- Slots, slot generation, appointment duration, reserved-time detection, and double-booking prevention are not included in this phase. Multiple requests may use the same preferred time.

---

## Approved Post-Baseline Changes — September 2026

1. Consultation request references use `AV-XXXXXX`, where the six-character uppercase suffix is generated cryptographically from `23456789ABCDEFGHJKLMNPQRSTUVWXYZ`. The database unique index remains authoritative, and confirmed reference collisions are retried at most five times before returning `ConsultationRequest.ReferenceNumberConflict`.
2. Guest and Client consultation confirmation emails include the persisted tracking phone, the consultation reference, and the configuration-driven credential-free `FrontendUrls:ConsultationTrackingUrl`. The existing public POST tracking endpoint accepts reference and phone in its body and verifies Guest requests against `GuestPhoneNumber` and Client requests against the persisted account phone while returning the same generic failure for every invalid pair.
3. The supported Lawyer availability uniqueness object is `UX_LawyerAvailabilities_SettingsId_Type_DayOfWeek`; violations map to `Lawyer.DuplicateAvailabilityDay`.
4. Runtime IDs for Governorates, Cities, Areas, and Legal Specializations come from independent SQL Server sequences. Seed maxima were verified as 27, 27008, 270080001, and 18 respectively, so sequence starts are 10000, 100000, 1000000000, and 10000. Sequence gaps are valid and IDs are not display ordering.
5. Admin, Lawyer, and Client dashboards use no-tracking grouped aggregate reads behind an Application abstraction. Response contracts, ownership rules, missing-profile behavior, and zero-state counts are unchanged.
6. Reference-data duplicate checks use scoped, self-excluding targeted specifications for Arabic or English name candidates. Existing unique indexes remain the final concurrency guarantee and preserve stable duplicate-name errors.
7. `DatabaseInitialization:ApplyMigrationsOnStartup` and `DatabaseInitialization:ApplySeedingOnStartup` are independent and both default to `false`. Enabled migration or seeding failures remain fail-fast; when both are enabled migration completes before seeding. Production startup keeps both switches disabled and schema changes are applied once as a controlled deployment operation.
