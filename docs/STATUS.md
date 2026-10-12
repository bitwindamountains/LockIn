# LockIn progress report

**Updated:** 2026-10-13, review 20  
**Stage:** Review defects fixed and regression checks passed.  
**Release:** Developer alpha. **Not production ready.**

## Accomplished

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
| .NET suite | 49 passed |
| Extension typecheck/build | Passed |
| Policy/content-script tests | 15 passed: 7 policy plus 8 fault/lifecycle cases |
| Chromium enforcement/recovery/motion and presentation | 12 passed (website case includes a trailing-dot host; new unchanged-policy sync case) |
| Expanded desktop design | Passed: deliberate window restoration, review action guards, browser staging guard, compact layouts and validation focus |
| Desktop editor | Passed |
| Desktop full session/history | Passed |
| Desktop browser recovery | Passed using a simulated browser actor |

Four newly added fault cases failed before the retry fix. Final content-script tests cover error/rejected/malformed responses, backoff, deadline cleanup and late results after release/navigation. Browser fixtures are separate from installed Brave and live YouTube acceptance. The final small stale-error guard was verified by typecheck/build and the expanded content-script suite after the browser run.

## Owner handoff

To load these changes, reopen LockIn and reload the unpacked extension at `brave://extensions` between sessions. No owner input was needed for the fixes. Existing owner processes and native registration were left in place; tests closed only their own processes.

## Remaining production gates and next step

Next: complete a dedicated installed-Brave permission/activation/expiry/recovery walkthrough through the native bridge, then live YouTube and remaining failure acceptance.

Open review gates: **R18-1** real Windows service/app enforcement and packaging; **R18-4** installed/physical platform acceptance; **R18-5** retention/deletion and sanitized diagnostics controls. Windows 11, physical sleep/reboot, screen-reader, high-contrast and DPI/scaling checks remain required. A dedicated Windows test environment will be needed for installation/disruption work.

No service, native registration or browser/OS policy was changed. Real browser restrictions were confined to disposable Chromium test profiles. Desktop browser messages are simulated; no new restrictions were installed in the owner's browser profile.
