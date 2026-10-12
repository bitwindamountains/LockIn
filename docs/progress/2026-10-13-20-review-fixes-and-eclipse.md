# Review 20/21 fixes and Eclipse redesign — handoff 20

**Date:** 2026-10-13  
**Milestone:** M3/M4 alpha reliability (review findings R20-1 to R21-5) and a complete frontend redesign.  
**Release:** Developer alpha. Windows app enforcement, release packaging and remaining platform acceptance are still incomplete.

## Accomplished

### Review 20 fixes

- **R20-1, trailing-dot hosts:** `example.com.` and `www.youtube.com.` no longer bypass website rules or the YouTube filter. Compiled DNR rules and `blockedHost` accept a trailing dot, and during a session other trailing-dot pages reload at their canonical host. Unit tests and a Chromium regression failed before the fix.
- **R20-2, damaged history records:** a stored session that fails current validation no longer breaks `ListSessions` or idle-browser polling. History lists it as "Record could not be verified" with whatever fields remain readable; nothing is deleted. Polling finds the browser's latest readable session with one `json_extract` query. Live-session reads stay strict.
- **R20-3, recovery of a live browser session:** `RecoverSession` on an Active browser-alpha session now requests the 60-second Guided end (owner decision). It stays immediate for simulation and for sessions already activating, releasing or pending cleanup. The desktop prompt states the delay.
- **R20-4, rule churn:** an unchanged policy no longer rewrites DNR rules or re-sweeps every tab on each 5-second sync.
- **R20-5, storage writes:** `current()` writes the extension cache at most once a second. Clock-rewind detection keeps its 2-second tolerance by also comparing against the latest time the worker saw.
- **R20-6, pipe creation:** the host logs and retries a pipe-construction failure instead of exiting.

### Review 21 follow-ups

- **R21-1:** unreadable history records show "Date unknown" instead of Jan 1, 0001 (`SessionHistoryItem.TimesKnown`).
- **R21-2:** the recover prompt promises the 60-second delay only when no early end is already pending. The desktop harness now reads both dialog texts.
- **R21-3:** a single unreadable-snapshot filter in `LocalStore`.
- **R21-4 (owner-approved deletion):** removed the unused `LocalStore.ListSessions()` and the `RecoverSimulation` alias.
- **R21-5, host stalls:** `ServiceClient` and `PipeTransport` now use `ConfigureAwait(false)`. From WPF, request I/O previously resumed on the UI thread while holding the host's only pipe instance, so a busy desktop could stall every client, the browser bridge included, for up to 4 seconds. A stalled-context test hung before the fix. Memory pressure on the development machine had also made intermittent harness timeouts much likelier.

### Eclipse redesign

The owner rejected the earlier green, lamplit-desk direction as generic and chose **Eclipse** from three options. Across the desktop app, the popup, the study page and the blocked page:

- A solar eclipse holds the clock and reflects state. It is partial when idle, total during a session (the corona blooms and a diamond-ring bead glints once), and slipping with a red corona when attention is needed. Text remains the authority; the scene is decorative.
- A night canvas with violet, ember and blue aurora washes, plus stars and grain on the web. Frosted-glass cards and pill buttons, with one ember accent (gold to coral).
- Geist and Geist Mono are bundled under the SIL OFL, with no remote fonts or images. The WPF controls (inputs, combo boxes, check boxes, lists, scrollbars, progress bar, navigation) have dark styles.
- Reduced motion (system or in-app) shows the resting eclipse for the current state, with no loops.

## Verification

| Check | Result |
| --- | --- |
| Release build | Passed; 0 warnings/errors |
| .NET tests | 53 passed (3 consecutive runs after the R21-5 fix) |
| Extension typecheck/build | Passed |
| Extension unit/fault tests | 15 passed |
| Browser suite | 13 passed, including trailing-dot, unchanged-policy sync, throttled status writes and reduced-motion eclipse resting state |
| Desktop design | Passed after the redesign, including compact Start visibility at 640×600 |
| Desktop full session/history | Passed after the redesign |
| Desktop browser recovery | Passed after the redesign, including the Guided delay and both recover prompts |
| Desktop editor (`-EditorOnly`) | Not run in this handoff |

The contrast of the Eclipse palette was measured; the lowest pair is tertiary text on glass at 5.34:1 (see `DESIGN_SYSTEM.md`). This is not a complete accessibility certification.

## Not verified

Installed Brave pairing and enforcement, and live YouTube, after these changes. Windows high-contrast themes, display scaling above 100%, and screen readers.

## Next

Owner review of the Eclipse design in the running app and the installed extension. Then the installed-Brave walkthrough and the remaining production gates (R18-1, R18-4, R18-5). The deferred `json_extract` index needs an owner decision because it is a schema change.
