# Implementation Plan: Review 20 fixes

Source: `SPEC.md`. Tasks are tracked in `tasks/todo.md`. Work happens on branch `fix/trailing-dot-host`, with one commit per task.

## Architecture Decisions

- **R20-3 lives in `SimulationEngine.Recover()`.** For an Active browser session it delegates to `SessionLogic.RequestGuidedEnd`; every other case keeps the immediate `explicitRecovery` release. No new command, and the native allowlist is unchanged.
- **R20-2 splits strict and tolerant reads in `LocalStore`.** `GetCurrentSession` and `GetSession` keep `ReadSession` (strict). `ListHistory` catches per-row validation failures and emits a `SessionHistoryItem` with `Damaged = true`, using whatever the JSON still yields: id, name and times, with fallbacks. A new `GetLatestBrowserSession(installationId)` uses `json_extract` plus `LIMIT 1` with a strict read, so `PollBrowser` no longer scans 50 rows. No schema change: `json_extract` is a built-in SQLite function, and no index or migration is needed.
- **R20-6 needs no refactor (answers spec open question 2).** Wrap pipe construction in the existing `try` with a delayed retry. `HostProcessTests` can prove it end to end: the test holds the pipe name first, the host starts, construction fails, the test releases the name, and the host then serves.
- **`RecoverSimulation` alias stays (spec open question 1).** Deleting it needs explicit approval, so it's out of scope.

## Task List

### Phase 1: Host behaviour (highest risk first)
- [x] Task 1: R20-3 engine: Recover on an Active browser session requests the Guided end
- [x] Task 2: R20-3 desktop: confirmation text, test harness, DECISIONS
### Checkpoint A: .NET suite plus `test-desktop.ps1 -BrowserRecovery` pass
- [x] Task 3: R20-2 tolerant history and direct browser-session lookup
- [x] Task 4: R20-6 pipe construction failure is retried
### Checkpoint B: .NET suite and desktop browser-recovery run pass
### Phase 2: Extension
- [ ] Task 5: R20-4 no rule rewrite or tab sweep on unchanged policy
- [ ] Task 6: R20-5 `current()` storage write throttle
### Checkpoint C: all suites pass; STATUS.md final

## Risks and Mitigations

| Risk | Impact | Mitigation |
| --- | --- | --- |
| The desktop harness now waits 60 s for the guided release | Med (slower test) | Its 75 s wait for the deadline already exists. Reuse it rather than adding a new wait. |
| A damaged row has unreadable JSON | Med | Fall back to id `"unknown"` and the row's `rowid` and state columns; never throw. |
| Chrome-returned DNR rules differ in shape from the compiled rules | Low | Reuse the existing `ruleShape` normalizer, which `coverage()` already relies on. |
| The R20-6 test is timing-sensitive | Low | Poll with `ServiceClient` retries up to 15 s. |

## Open Questions

None blocking. Spec Q1 is resolved as "keep the alias" and Q2 as "no refactor needed"; see above.
