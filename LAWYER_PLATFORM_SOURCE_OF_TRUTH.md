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

- A Lawyer owns independent Consultation Settings containing the current positive Consultation Price and zero to seven weekly availability periods.
- Availability contains at most one working period per available `DayOfWeek`; an omitted day is unavailable and each period requires `StartTime < EndTime`.
- `PreferredAppointmentOnUtc` remains optional for Guest and Client Consultation Requests.
- When `PreferredAppointmentOnUtc` is supplied, the backend converts it from UTC into the configured business timezone and requires its local day and time to match the Lawyer's weekly availability. Start and end boundaries are inclusive.
- When Consultation Settings exist, their current Consultation Price is snapshotted into each new `ConsultationRequest`, including contact requests without an appointment.
- When Consultation Settings do not exist, a contact request without an appointment remains valid and its historical Consultation Price is `null`; an appointment request is rejected.
- Later Lawyer price changes never modify old Consultation Requests. Request details always use the historical snapshot, while public Lawyer APIs use the current price.
- Consultation Settings are operational settings with independent optimistic concurrency and do not modify Lawyer approval or public-eligibility state.
- Slots, slot generation, appointment duration, reserved-time detection, and double-booking prevention are not included in this phase. Multiple requests may use the same preferred time.
