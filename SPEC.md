# Spec: Review 20 fixes

## Objective

Close the remaining findings from the 2026-10-13 full review (review 20) of the LockIn developer alpha. Finding 1 (trailing-dot host bypass) is already fixed in `e6f657a`. This spec covers the rest. It does not add features or advance the production gates R18-1, R18-4 or R18-5.

User: the owner, studying with LockIn in simulation or browser-alpha scope.

| Id | Finding | Required outcome |
| --- | --- | --- |
| R20-2 | Stored sessions are re-validated against current rules. One failing row makes `ListSessions` throw, which breaks History and `PollBrowser` for idle browsers. | Live-session reads stay strict. History shows a failing old row as damaged instead of failing. Browser polling finds its own latest session without validating unrelated rows. |
| R20-3 | `RecoverSession` releases an Active browser-alpha session instantly, which bypasses the 60 s Guided end. | For an Active browser session, Recover requests the Guided end. Recover stays immediate for simulation sessions and for any session already `Activating`, `Releasing` or `CleanupPending`. |
| R20-4 | The extension rewrites its DNR rules and re-injects YouTube tabs on every 5 s sync. | Rules are rewritten only when they differ in shape. The tab sweep runs only when a session's policy is first applied in this worker. |
| R20-5 | `current()` writes `chrome.storage.local` on every call. | It writes only when `lastUtc` advances by at least 1 s. Clock-rewind detection keeps its 2 s tolerance. |
| R20-6 | The host's `NamedPipeServerStream` constructor is outside the `try`, so a constructor exception ends the host. | A constructor failure is logged and retried after a delay. Reconciliation keeps running. |

## Decisions (owner, 2026-10-13)

- **R20-3:** "Release / recover" during an Active browser session uses the 60 s Guided delay. The plan's Committed-mode recovery tradeoff for simulation is unchanged.
- **R20-2:** Old records that fail validation are shown in History as damaged and are never deleted.

## Tech Stack

No changes. .NET SDK 10.0.401 (`global.json`), WPF desktop, Microsoft.Data.Sqlite, xUnit. The extension uses TypeScript 5.9.3, esbuild 0.28.2, `node:test` and Playwright 1.63.0. No new dependencies.

## Commands

From the repository root, in PowerShell:

```powershell
$sdk = "$env:LOCALAPPDATA\LockInDev\dotnet\dotnet.exe"
& $sdk build LockIn.slnx
& $sdk test LockIn.slnx --no-restore
```

From `extension/`:

```powershell
npm run typecheck
npm run build
npm test
npm run test:browser
```

Desktop UI check, for R20-2 and R20-3 only (opens its own test window; never closes owner processes):

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/test-desktop.ps1 -Configuration Release -BrowserRecovery
```

## Project Structure

```
src/LockIn.Core/            Records, protocol, ProfileRules, SessionLogic (pure logic)
src/LockIn.Infrastructure/  LocalStore (SQLite)
src/LockIn.Service/         Host: Program (pipe loop), SimulationEngine, BrowserRegistry
src/LockIn.Desktop/         WPF UI (MainWindow partials)
src/LockIn.NativeHost/      Browser native-messaging bridge
tests/LockIn.Core.Tests/    xUnit: engine, store, native bridge, regressions
extension/src/              background, content, shared/policy
extension/tests/            node:test unit tests; tests/browser Playwright
scripts/                    Launch, registration, desktop test harness
docs/                       STATUS.md (update per milestone), DECISIONS.md
```

## Code Style

Match the surrounding code: dense C# records and expression-bodied members, guard clauses that throw `ArgumentException` or `InvalidOperationException` with plain-English messages, and parameterized SQL only.

```csharp
public Session? GetSession(string id)
{
    using var db = Open();
    using var command = db.CreateCommand();
    command.CommandText = "SELECT json FROM sessions WHERE id=$id";
    command.Parameters.AddWithValue("$id", id);
    var json = command.ExecuteScalar() as string;
    return json is null ? null : ReadSession(json);
}
```

Comment only the reason the code can't show. Label simulation versus browser scope accurately in UI text.

## Testing Strategy

- **R20-2, R20-3, R20-6:** xUnit in `tests/LockIn.Core.Tests/`, through `SimulationEngine.HandleAsync` or `LocalStore` with a temp database, like the existing tests. Each regression test must fail before its fix.
- **R20-2 History label** and the **R20-3 dialog/flow:** covered by the existing `test-desktop.ps1 -BrowserRecovery` run, updated as needed.
- **R20-4, R20-5:** a Playwright case in `enforcement.spec.ts` or a `node:test` case that observes rule writes or storage writes across repeated syncs.
- The full .NET, extension unit and browser suites pass after every task.

## Boundaries

- **Always:** write a failing test first, run the documented checks, update `docs/STATUS.md` per task, make one commit per task on a branch.
- **Ask first:** any schema change or migration, any change to the native command allowlist, deleting the `RecoverSimulation` alias, adding a dependency.
- **Never:** install a service, change browser or OS policy, close owner apps or processes, delete database rows, or edit the native registration.

## Success Criteria

1. **R20-2**
   - With a stored old session row that fails validation, `ListSessions` succeeds and includes that row marked damaged.
   - `PollBrowser` for an idle browser succeeds and returns that browser's latest valid session for its release acknowledgment.
   - `GetCurrentSession` and `GetSession` still throw on an invalid snapshot.
   - History shows the damaged row with a visible "record could not be verified" label.
2. **R20-3**
   - `RecoverSession` on an Active browser session sets `EarlyEndRequestedAtUtc` and leaves the session Active. After 60 s the session releases with reason `guidedEnd`.
   - On an Active simulation session, `RecoverSession` releases immediately with reason `explicitRecovery`, as now.
   - On a browser session in `Activating`, `Releasing` or `CleanupPending`, `RecoverSession` releases immediately.
   - The desktop confirmation text says the browser session ends in 60 seconds.
   - `RecoverSession` stays excluded from the native host allowlist.
3. **R20-4:** repeated syncs with an unchanged policy call `updateDynamicRules` zero times after the first apply. The YouTube tab sweep runs once per session per worker lifetime.
4. **R20-5:** `chrome.storage.local.set` is not called by `current()` when called repeatedly within 1 s.
5. **R20-6:** a pipe-server construction failure is logged, the loop continues after a delay, and the host still serves requests once construction succeeds.
6. All suites pass. `docs/STATUS.md` and `docs/DECISIONS.md` (for R20-3) are updated.

## Open Questions

1. The `RecoverSimulation` command alias is used only by one test. Remove it with that test update, or keep it?
2. R20-6 can't easily be unit-tested at the `Program.cs` top level. Is it acceptable to move the accept loop into a small testable method on the service side? If not, R20-6 ships with a manual verification note only.
