# Lawyer Platform Source of Truth

## Approved Decision — Lawyer Onboarding Lifecycle

- Lawyer registration creates an active `UserAccount` with the `Lawyer` role and a `Draft` `LawyerProfile` only.
- Professional profile completion occurs after the Lawyer signs in.
- Primary office, legal specializations, and private documents are managed as separate onboarding sections owned by the `LawyerProfile` aggregate.
- Submission for approval is allowed only from `Draft` and `ChangesRequested`.
- SuperAdmin controls approval, rejection, change requests, suspension, and reactivation.
- Permitted edits to an approved profile do not automatically remove approval or trigger automatic reapproval.
- Exact required document types, allowed extensions and MIME types, and file-size limits remain deployment configuration decisions under `LawyerDocuments`.
