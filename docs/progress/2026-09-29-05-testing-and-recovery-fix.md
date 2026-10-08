# Handoff 5 — Testing resumed and recovery fixed

Date: 2026-09-29. Request: run testing and adapt the implementation to the results.

## Results and changes

The previously unbuilt coverage/owner-recovery changes compiled without warnings or errors. Existing .NET tests passed. Added tests then verified owner-removal state/deadline/confirmation guards, revocation across restart, transaction rollback on an injected SQLite delete failure, coverage transitions and cleanup evidence. The native process test also rejects `ConfirmBrowserRemoval` through the browser command allowlist.

A new Chromium fault-injection test exposed a production bug: expiry removed the cache before persisting the ended-session marker. A failed storage write left no local record preventing the host's old policy from being installed again. The regression observed **one rule when zero was expected** on the next synchronization.

Fixed `extension/src/background.ts` to record the ended decision before discarding ownership, retain cache through cleanup, retry already-ended cached sessions, await video release messaging and discard cache last. If the marker write fails, rule/script removal is still attempted, ownership remains, and incomplete release is not acknowledged. The regression now confirms both immediate rule removal and no stale reactivation after retry.

Two initial Chromium checks exceeded the five-second startup-readiness wait during a concurrent build. Kept the same rule-installation assertion and increased its bounded wait to 15 seconds. Final runs passed.

The initial full WPF session reached correct host completion/history but its final UI lookup found the Profile tab selected while another walkthrough was running. Made the final Focus navigation explicit and added a local mutex to prevent overlapping desktop walkthroughs. The sequential rerun passed. No production session logic was changed for that harness failure.

## Final verification

| Check | Actual result |
| --- | --- |
| Local SDK `dotnet build LockIn.slnx` | Passed, 0 warnings/errors |
| `dotnet test LockIn.slnx --no-restore` | 48 passed, 0 failed/skipped |
| Extension `npm.cmd run typecheck` and `npm.cmd run build` | Passed after final fix |
| `npm.cmd test` | 5 passed |
| `npm.cmd run test:browser` | 7 passed after final fix, 30.1 seconds |
| `scripts/test-desktop.ps1 -EditorOnly` | Passed save/reopen/edit |
| `scripts/test-desktop.ps1` | Passed sequential rerun, including real-time one-minute expiry |
| `scripts/test-desktop.ps1 -BrowserRecovery` | Passed coverage/permission-loss display and owner-reported recovery |

Browser cases cover actual DNR installation/redirect/expiry, exact allow tags, three tab actions, stale metadata, missing-rule reporting, locally expired-policy rejection and storage failure. Five cases have no native connection; two use simulated native responses in the temporary test copy. These checks do not verify registry installation or real browser-to-host messaging.

The recovery UI walkthrough simulates a browser actor via the host pipe. It checks policy-reported and permission-missing messages, verifies recovery is disabled before the original deadline, performs the checkbox/dialog confirmation, and checks revoked enrollment plus owner-reported history/completion. It installs no real browser restriction.

## Evidence

- Editor: `%TEMP%\lockin-ui-5df0f5f5cb0c4d04b0eda53cae70a81f`.
- Final full session: `%TEMP%\lockin-ui-92bccab9cd9d497c82d9e1109a117528`.
- Owner recovery: `%TEMP%\lockin-ui-66ca68a07aee4aee925606eca817f74e`.
- Generated browser captures: `extension/test-results`; disposable profiles: `%TEMP%\lockin-chromium-*`.
- [Owner-recovery capture](../images/owner-recovery-2026-09-29.png) shows explicit unverified-cleanup wording. Window capture clips part of the lower rendered area; this is not a full scaling/accessibility review.

## Scope and remaining work

No Windows service, native registration, extension in the normal browser or OS/browser policy was installed. Test-owned UI/host/Chromium processes were stopped; final process inspection found no matching test instances. Temporary data remains for diagnosis.

Still a developer alpha. Installed native transport, real permission dialogs, live YouTube/SPA/media, remaining restart/offline recovery cases, Windows 11/sleep/reboot, app/service enforcement, accessibility and packaging remain open. See [STATUS](../STATUS.md) and [BACKLOG](../BACKLOG.md).
