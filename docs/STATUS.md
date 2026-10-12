# LockIn progress report

**Updated:** 2026-10-13, review 20  
**Stage:** Review 20 and 21 findings (R20-1 to R21-5) fixed; regression checks passed.  
**Release:** Developer alpha. **Not production ready.**

## Accomplished

- **Eclipse redesign (supersedes the lamplit desk):** the desktop app, popup, study page and blocked page share a night-sky identity. A solar eclipse holds the clock: partial when idle, total during a session (corona bloom and a diamond-ring glint), slipping with a red corona when attention is needed. Surfaces are frosted glass over an aurora. Primary actions use an ember gradient. Geist and Geist Mono are bundled. All extension suites pass, as do the desktop design, full-session and browser-recovery harness modes.
- **R21-5 fixed:** pipe client I/O no longer resumes on the caller's thread. From the desktop, a request used to continue on the WPF UI thread while holding the host's only pipe instance, so a busy UI could stall every client, browser bridge included, for up to 4 s. A test with a stalled synchronization context failed before the fix.
- **R21-3/R21-4 done:** one shared unreadable-snapshot filter in `LocalStore`. The unused `ListSessions()` and the `RecoverSimulation` alias were removed with owner approval; tests use `ListHistory` and `RecoverSession`. The native-bridge process test now tolerates a transient unavailable host while it waits for release, as the extension does.
- **R21-2 fixed:** the recover prompt promises the 60-second delay only when no early end is already pending. The desktop browser-recovery run now reads both prompts. The harness retries a busy pipe connection and reports which wait timed out.
- **R21-1 fixed:** a damaged history record whose data can't be read shows "Date unknown" instead of Jan 1, 0001.
- **R20-5 fixed:** session status checks write the extension's stored cache at most once a second. Clock-rewind detection keeps its 2-second tolerance by also comparing against the latest time the worker saw.
- **R20-4 fixed:** an unchanged policy no longer rewrites the extension's site rules or re-checks every open tab on each 5-second sync. Rules are compared by shape, as coverage already did, and the tab sweep runs once per session per worker.
- **R20-6 fixed:** the host no longer exits when it cannot create its pipe endpoint; it logs, keeps reconciling and retries every second. A process test holds the pipe name first and confirms the host serves once it is free.
- **R20-2 fixed:** History lists old session records that fail current validation as "Record could not be verified" instead of failing; nothing is deleted. Browser polling looks up its own latest readable session directly instead of validating the last 50 rows. Live-session reads stay strict.
- **R20-3 fixed:** Recover on an Active browser session now requests the 60-second Guided end, and the desktop prompt says so. It stays immediate for simulation and for sessions already releasing or pending cleanup. The engine test and desktop browser-recovery run cover it.
- **R20-1 fixed:** a trailing-dot hostname (`example.com.`, `www.youtube.com.`) no longer bypasses website rules or the YouTube filter. Website rules and `blockedHost` accept the trailing dot; during a session, other trailing-dot pages are reloaded at their canonical host so YouTube content rules apply. Unit and Chromium regressions failed before the fix.
- **R18-2 fixed:** failed YouTube evaluations retry with unchanged metadata and visible feedback; delay grows from 1 to at most 8 seconds. Successful decisions remain deduplicated. Late replies/errors cannot undo release or affect a newly navigated video.
- **R18-3 fixed:** pending review and asynchronous profile operations lock conflicting edits/actions. Save, staging and preparation use a captured profile/revision. Back and countdown cancellation restore editing.
- **R18-6 verified:** the desktop harness restores only its test window and rejects invalid/minimized captures. Deliberate minimization and the entire expanded design check pass.

See the [progress report and evidence](progress/2026-10-01-19-review-fixes.md). The [full review](progress/2026-10-01-18-full-review.md) remains the historical record of the original findings.

## Latest verification

| Check | Result |
| --- | --- |
| Release build | Passed, zero warnings/errors |
| .NET suite | 53 passed |
| Extension typecheck/build | Passed |
| Policy/content-script tests | 15 passed: 7 policy plus 8 fault/lifecycle cases |
| Chromium enforcement/recovery/motion and presentation | 13 passed (website case includes a trailing-dot host; new unchanged-policy sync and status-throttle cases) |
| Expanded desktop design | Passed: deliberate window restoration, review action guards, browser staging guard, compact layouts and validation focus |
| Desktop editor | Passed |
| Desktop full session/history | Passed (2026-10-13) |
| Desktop browser recovery | Passed 3 consecutive runs, including the Guided delay and both recover prompts |

Four newly added fault cases failed before the retry fix. Final content-script tests cover error/rejected/malformed responses, backoff, deadline cleanup and late results after release/navigation. Browser fixtures are separate from installed Brave and live YouTube acceptance. The final small stale-error guard was verified by typecheck/build and the expanded content-script suite after the browser run.

Resolved 2026-10-13: the intermittent desktop-harness host timeouts had two causes. Memory pressure on the development machine made the earlier failures much likelier; with about 2.5 GB free, 3 of 3 runs passed with no stalls. The other cause was the client-side UI-thread stall fixed in R21-5. After the fix, 3 of 3 browser-recovery runs and the full session/history run passed, with no `Incomplete pipe request` entries in the host log.

## Owner handoff

To load these changes, reopen LockIn and reload the unpacked extension at `brave://extensions` between sessions. No owner input was needed for the fixes. Existing owner processes and native registration were left in place; tests closed only their own processes.

## Remaining production gates and next step

Next: complete a dedicated installed-Brave permission/activation/expiry/recovery walkthrough through the native bridge, then live YouTube and remaining failure acceptance.

Open review gates: **R18-1** real Windows service/app enforcement and packaging; **R18-4** installed/physical platform acceptance; **R18-5** retention/deletion and sanitized diagnostics controls. Windows 11, physical sleep/reboot, screen-reader, high-contrast and DPI/scaling checks remain required. A dedicated Windows test environment will be needed for installation/disruption work.

No service, native registration or browser/OS policy was changed. Real browser restrictions were confined to disposable Chromium test profiles. Desktop browser messages are simulated; no new restrictions were installed in the owner's browser profile.
