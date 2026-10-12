# Review 20 tasks

## Task 1: R20-3 engine: Recover on an Active browser session requests the Guided end
- Acceptance:
  - [x] `RecoverSession` on an Active browser session sets `EarlyEndRequestedAtUtc`; the state stays Active; after 60 s the reason is `guidedEnd`
  - [x] Simulation Active, plus browser Activating, Releasing and CleanupPending, still release immediately (`explicitRecovery`)
  - [x] `NativeLifecycleTests` and `BrowserAlphaTests` are updated to the new contract
- Verify: `dotnet test LockIn.slnx`
- Files: `src/LockIn.Service/SimulationEngine.cs`, `tests/LockIn.Core.Tests/ReviewRegressionTests.cs`, `BrowserAlphaTests.cs`, `NativeLifecycleTests.cs`
- Depends: none · Size: S

## Task 2: R20-3 desktop: confirmation text, test harness, DECISIONS
- Acceptance:
  - [x] The Recover dialog for an Active browser session says it ends in 60 seconds
  - [x] `test-desktop.ps1 -BrowserRecovery` passes with the guided release
  - [x] The decision is recorded in `DECISIONS.md`
- Verify: `scripts/test-desktop.ps1 -Configuration Release -BrowserRecovery`
- Files: `src/LockIn.Desktop/MainWindow.xaml.cs`, `scripts/test-desktop.ps1`, `docs/DECISIONS.md`, `docs/STATUS.md`
- Depends: 1 · Size: S

## Task 3: R20-2 tolerant history and direct browser-session lookup
- Acceptance:
  - [x] With an invalid old row, `ListSessions` succeeds and marks that row `Damaged`
  - [x] Idle `PollBrowser` returns this browser's latest session without reading other rows
  - [x] `GetCurrentSession` and `GetSession` still throw on an invalid snapshot
  - [x] History shows "record could not be verified" for damaged rows
- Verify: `dotnet test`; desktop History check in the browser-recovery run
- Files: `src/LockIn.Core/Protocol.cs`, `src/LockIn.Infrastructure/LocalStore.cs`, `src/LockIn.Service/SimulationEngine.cs`, `src/LockIn.Desktop/MainWindow.History.cs`, `tests/LockIn.Core.Tests/ReviewRegressionTests.cs`
- Depends: none · Size: M

## Task 4: R20-6 pipe construction failure is retried
- Acceptance:
  - [x] A host started while another server holds its pipe name logs the error, survives, and serves once the name is free
- Verify: `dotnet test --filter HostProcessTests`
- Files: `src/LockIn.Service/Program.cs`, `tests/LockIn.Core.Tests/HostProcessTests.cs`
- Depends: none · Size: S

## Task 5: R20-4 no rule rewrite or tab sweep on unchanged policy
- Acceptance:
  - [x] Repeated syncs with the same policy make 0 `updateDynamicRules` calls after the first apply
  - [x] The tab sweep runs once per session per worker
- Verify: `npm test`, `npm run test:browser`
- Files: `extension/src/background.ts`, `extension/tests/browser/enforcement.spec.ts`
- Depends: none · Size: S

## Task 6: R20-5 `current()` storage write throttle
- Acceptance:
  - [x] Calls to `current()` within 1 s cause no `storage.local.set`
  - [x] Rewind detection still works (existing cache test)
- Verify: `npm test`, `npm run test:browser`
- Files: `extension/src/background.ts`, `extension/tests/browser/enforcement.spec.ts`, `docs/STATUS.md`
- Depends: 5 · Size: S

## Task 7: R21-1 history rows with unknown times show "Date unknown"
- Acceptance:
  - [x] `SessionHistoryItem.TimesKnown` is false for an unreadable row and true for readable and damaged-but-parseable rows
  - [x] History shows "Date unknown" and "Original deadline: unknown" when `TimesKnown` is false
- Verify: `dotnet test`; Release build
- Files: `src/LockIn.Core/Protocol.cs`, `src/LockIn.Desktop/MainWindow.History.cs`, `tests/LockIn.Core.Tests/BrowserAlphaTests.cs`
- Depends: none · Size: S

## Task 8: R21-2 recover prompt only promises 60 s when no early end is pending
- Acceptance:
  - [x] Pressing Recover with a Guided end already pending shows the neutral prompt
  - [x] The desktop harness reads the dialog text in both cases
- Verify: `scripts/test-desktop.ps1 -Configuration Release -BrowserRecovery`
- Files: `src/LockIn.Desktop/MainWindow.xaml.cs`, `scripts/test-desktop.ps1`
- Depends: none · Size: S

## Task 9: R21-3 single unreadable-snapshot filter in LocalStore
- Acceptance:
  - [ ] One helper is used by `ListHistory` and `GetLatestBrowserSession`; existing tests stay green
- Verify: `dotnet test`
- Files: `src/LockIn.Infrastructure/LocalStore.cs`
- Depends: 7 · Size: XS

## Task 10: R21-4 remove `ListSessions()` and the `RecoverSimulation` alias
- Acceptance:
  - [ ] No production or test references remain; `RecoverSimulation` returns "Unknown command."
  - [ ] Tests use `GetSession`, `ListHistory` and `RecoverSession` instead
- Verify: `dotnet test`
- Files: `src/LockIn.Infrastructure/LocalStore.cs`, `src/LockIn.Service/SimulationEngine.cs`, `tests/LockIn.Core.Tests/SimulationEngineTests.cs`, `tests/LockIn.Core.Tests/ReviewRegressionTests.cs`
- Depends: 9 · Size: S
