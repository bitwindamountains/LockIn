# Review fixes and regression checks — handoff 19

**Date:** 2026-10-01  
**Milestone:** M3/M4 alpha reliability; fixes for R18-2, R18-3 and R18-6.  
**Release:** Developer alpha. Windows app enforcement, release packaging and remaining platform acceptance are still incomplete.

## Accomplished

### YouTube evaluation recovery (R18-2)

The content script now records a metadata signature only after a valid decision. Rejected messages, error replies and malformed replies remain retryable with unchanged metadata. Retries wait 1, 2, 4 and then at most 8 seconds between attempts; they continue while the session is valid. A gated video shows an automatic-retry message with connection/recovery guidance. A successful allow decision removes the pause gate and resumes previously playing media once.

Deadline checks precede retry/busy checks. Release still clears the gate without starting media. Late replies/errors after release or navigation cannot restore the badge, resume the old video or delay evaluation of the new video. Exact entered hashtag and approved-ID semantics are unchanged.

Added eight tests that execute the actual bundled content script in a synthetic DOM/runtime/clock fixture. Before the fix, four initial cases failed; after implementation all cases pass. These are fault-injection tests, not live YouTube certification.

### Stable review and save context (R18-3)

Pending review, countdown and asynchronous profile operations now disable conflicting editor actions, profile/browser selection, staging and disconnection. Handler guards enforce the same boundary. Save operations use the captured baseline revision and return the saved profile; staging and preparation use that returned ID/revision instead of reading mutable editor state after an await. The browser Start path reuses its saved snapshot instead of saving the old draft a second time.

The editor explains how to return from review. Back and countdown cancellation restore editing; editing for the next session remains available once activation has completed. WPF checks verify disabled New/Duplicate/fields, successful Begin with the reviewed draft, restored controls after cancellation, exact approved-only review, and blocked competing browser staging/selection. No host authority or session deadlines were moved into UI code.

### Reliable desktop geometry and captures (R18-6)

The harness normalizes only its own test window before resize/capture, waits for a normal usable window, checks move/resize availability and rejects minimized/failed captures. A regression deliberately minimizes the test window before the first screenshot; restoration and the entire design workflow then pass. The failed handoff 18 run remains documented rather than being overwritten as a success.

## Files changed

- `extension/src/content.ts`: retry state, success-only deduplication, expiry and late-result guards, feedback.
- `extension/tests/content.test.ts`: eight fault/lifecycle regressions using the real bundled script.
- `src/LockIn.Desktop/MainWindow.xaml.cs`: operation guards and captured save context.
- `MainWindow.xaml`, `MainWindow.Review.cs`, `MainWindow.Presentation.cs`: control IDs, enabled states and review feedback.
- `scripts/test-desktop.ps1`: test-owned window restoration, capture validation and review interaction regressions.
- Status, decisions, backlog, readiness, design-system and README documentation: current outcomes and next gates.

## Verification

| Check | Result |
| --- | --- |
| Space guard | Passed; approximately 23.6 GB free at build start |
| Release build | Passed; 0 warnings/errors |
| .NET tests | 49 passed |
| Extension typecheck/build | Passed, including the final late-error guard |
| Extension unit/fault tests | 13 passed: 5 existing policy plus 8 content-script cases |
| Browser suite | 11 passed: 9 enforcement/recovery/motion and 2 presentation cases |
| Expanded desktop design | Passed, including deliberate minimization, review guards and compact layout/validation |
| Desktop editor | Passed |
| Desktop full session/history | Passed |
| Desktop browser recovery | Passed: coverage/permission loss, original deadline, owner-reported recovery and enrollment revocation |

The browser suite ran after the main retry fix; the final stale-error guard was subsequently checked by typecheck/build and the expanded content-script suite. No installed Brave or live YouTube result is inferred.

Commands: documented process-local PowerShell space guard; local SDK `build LockIn.slnx -c Release --no-restore` and `test LockIn.slnx -c Release --no-build --no-restore`; from `extension`, `npm.cmd run typecheck`, `npm.cmd run build`, `npm.cmd test`, `npm.cmd run test:browser`; serialized `scripts/test-desktop.ps1 -Configuration Release` with `-DesignOnly`, `-EditorOnly`, no case switch, and `-BrowserRecovery`.

Evidence under `%TEMP%`:

- Design: `lockin-ui-367e6e3744394cff96fbbfb49d91a702`
- Editor: `lockin-ui-cd7ca4206aaa491c95eadc04b4ff43ab`
- Full session/history: `lockin-ui-ee0e054c68ed4b00bace66a2126fa3a7`
- Browser recovery: `lockin-ui-65b5250fd9974a2e93a502823a0a10a6`

Browser artifacts remain in ignored `extension/test-results`. Valid first-use and compact-review screenshots were visually inspected and copied to `docs/images/ui19-first-use.png` and `docs/images/ui19-compact-review.png`. The compact review keeps Begin and Back reachable. Minor review pluralization polish remains outside these defect fixes.

## Research and boundaries

The implementation handles rejected messaging promises as well as this project's explicit error-response envelope; see [Chrome message passing](https://developer.chrome.com/docs/extensions/develop/concepts/messaging). Test-window normalization uses [WindowPattern.SetWindowVisualState](https://learn.microsoft.com/en-us/dotnet/api/system.windows.automation.windowpattern.setwindowvisualstate?view=windowsdesktop-10.0).

No service, browser/OS policy or native registration was installed/changed. No owner apps were closed. Real browser rules existed only in disposable Chromium test profiles; desktop browser-recovery messages are simulated. Test-owned processes are closed by the harness. Existing owner processes and registration remain untouched.

## Next step

Complete a dedicated installed-Brave permissions/activation/expiry/recovery walkthrough through the native bridge, followed by live YouTube supported-layout and remaining failure acceptance. R18-1 (Windows enforcement/packaging), R18-4 (installed/physical acceptance) and R18-5 (retention/data controls) remain open. Physical accessibility/DPI, Windows 11 and sleep/reboot checks remain required. The immediate code fixes needed no owner action.
