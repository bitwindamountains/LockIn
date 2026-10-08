# Handoff 4 — Browser coverage and owner-reported recovery

Date: 2026-09-29. Owner instruction: continue writing; skip testing until implementation is done.

## Implemented in source

- Extended browser polling with applied session/hash and a bounded set of issue codes. Extension inspects granted origins, owned dynamic rule shapes and YouTube script registration before reporting installed policy.
- Added fresh/offline, awaiting policy, reported active, releasing and degraded host coverage states. Freshness uses UTC plus a monotonic process counter; enrollment credentials still authenticate reports.
- Added Focus/Browser coverage text for missing heartbeats, permissions, locally released policy and installation/cleanup failure. Labels distinguish reported installation from live-page compatibility.
- Coalesced scheduled synchronization so a slow native connection does not accumulate periodic poll requests. Added handling for startup and tab-update reconciliation failures.
- Added desktop-only owner-reported removal for lost browser pairing. It requires CleanupPending, the original deadline to have passed, a checked removal statement and explicit confirmation. The service rechecks state/deadline/session identity.
- Recorded final recovery and revoked the old enrollment in one SQLite transaction. BrowserReleased stays false: no browser acknowledgment is fabricated. History and completion text explicitly show owner-reported evidence.
- Normal release acknowledgments now store their own cleanup evidence. Existing serialized profile hashes remain unchanged.
- Corrected the browser setup-guide path and host logging, which previously described all sessions as simulation even when browser alpha was enabled.
- Updated the host-process test's expected startup text for the eventual verification run; did not execute it.

## Files involved

Core browser/session/history contracts; LocalStore atomic recovery; BrowserRegistry/SimulationEngine; service startup text; desktop XAML/code; extension background worker; README, status, decisions, backlog and browser recovery guide.

## Verification status

**No builds, tests, typechecks, browser automation, application launches or runtime walkthroughs were run after the owner's instruction.** Work was edited and reviewed from source. Compiled binaries and screenshots remain from handoff 3 and do not represent these latest additions.

When verification resumes, check contract serialization/backward compatibility, recovery deadline/state guards, atomic rollback on storage failure, revoked-credential rejection, distinction between manual and browser-confirmed history, heartbeat expiry/clock changes, permission removal, rule/script mismatch, coalesced polling and all existing regression suites.

No service, native registration, extension in a normal browser, browser/OS policy, or real restriction was installed or changed in this continuation.

## Remaining project work

This is still a developer alpha. Installed native messaging, live YouTube layouts/SPA/media, worker/browser restart, cross-component recovery, Windows service/app enforcement, packaging and platform/accessibility gates remain unfinished. See [STATUS](../STATUS.md) and [BACKLOG](../BACKLOG.md).
