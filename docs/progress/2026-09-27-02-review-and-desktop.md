# Progress report 2 — review and desktop continuation

## Reviewed and corrected

The initial build's records contained mutable rule lists, countdown enforcement existed only in the UI, clients could leave the sole pipe endpoint blocked indefinitely, and invalid profile data could cause null/URI exceptions. Saving through the original editor also discarded study resources and video approvals. These were concrete correctness gaps before M3.

- Profile validation now returns field-specific errors, rejects malformed collections/enums/rules/URLs, detects exact and subdomain destination/study-link conflicts, and freezes normalized snapshots into read-only collections.
- Elapsed checkpoints combine UTC and an in-process monotonic counter. Guided end retains its full delay after a backward clock adjustment. Invalid state transitions cannot skip release.
- The host now checks countdown duration, candidate identity, expected profile revision, and durable activation retries even after completion. Failures retain visible cleanup state with retry backoff and explicit simulation recovery.
- The local pipe is scoped by user SID; both ends bound I/O lifetime, and the client checks response identity. A second host on the same endpoint exits. Engine logic was separated from process startup.
- Schema 2 enforces one live session, creates a backup before an idle v1 upgrade, rejects unsupported versions, and validates stored snapshot hashes and timing. Corrupt data remains intact for diagnosis.

## Continued implementation

- Reorganized WPF into Focus, Profile, and History tabs.
- Added executable browsing/existence checks, study links, exact/subdomain website selection, approved video IDs/URLs, Guided/Committed simulation mode, all three tab actions and Home/Focus/custom destinations.
- Added field-specific validation, unsaved-edit protection, profile duplication, exact-hashtag preview with approval and unknown-metadata decisions, and session outcome history.
- Start reviews the current draft and saves it before preparation. Closing during countdown cancels the candidate; closing an active desktop retains the host-owned session.
- Added `scripts/start-simulation.ps1` and isolated Windows UI verification scripts.

## Evidence

- `dotnet build LockIn.slnx --no-restore`: 0 warnings, 0 errors.
- `dotnet test LockIn.slnx --no-restore`: **35 passed, 0 failed, 0 skipped**, including a real Windows host process restart and stalled-client check.
- `scripts/test-desktop.ps1`: passed profile save, countdown cancellation, close/reopen with identical session/deadline, automatic one-minute completion, and completion history. Evidence directory: `%TEMP%\lockin-ui-e050118dad584766ac972cdd3870a094`.
- `scripts/test-desktop.ps1 -EditorOnly`: passed saving study resources, approved videos, tab action, and custom destination, then reopening and editing without losing those values. Evidence directory: `%TEMP%\lockin-ui-1a79e7b264aa447498dcc8b0ddf0c6ec`.
- Windows 10 Pro 19045 x64, .NET SDK 10.0.401. No browser enforcement or Windows 11 claims follow from these results.

## Remaining scope

The simulation workflow is verified. M3 browser pairing, native messaging, website enforcement, and independent extension expiry are next. Real app closure, live YouTube behavior, installed-service security, full persistent-artifact recovery, accessibility/scaling review, and packaging remain pending. The extension itself is unchanged in this handoff and remains a scaffold.

No services, native hosts, browser rules, OS policies, or application restrictions were installed. All test-owned UI/host processes were stopped. Temporary synthetic databases/logs remain at the evidence paths; existing user simulation data was not used by the automated walkthroughs.
