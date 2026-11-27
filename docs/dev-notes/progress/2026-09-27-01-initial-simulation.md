# LockIn progress report

**Updated:** 2026-09-27  
**Current milestone:** M2 simulation slice implemented; M2 exit gate remains open.  
**Overall release status:** Not ready for real focus use.

## What was accomplished this handoff

### M0 — repository and platform readiness

- Inspected the sole original file, `lockin_full_scale_build_plan (1).md`; no source repository or `AGENTS.md` existed.
- Initialized local Git tracking; no commit or remote was created.
- Created the .NET 10 solution, WPF desktop project, current-user simulation host, shared core, SQLite infrastructure, tests, extension workspace, synthetic YouTube fixture, backlog, decisions, and setup guide.
- Detected Windows 10 Pro 19045 x64, Chrome 154.0.8037.58, Edge 154.0.4258.37, Node 24.16.0, and no preinstalled `dotnet` on PATH. Installed .NET SDK 10.0.401 into `%LOCALAPPDATA%\LockInDev\dotnet` for development only.
- No service, native host, extension, Windows policy, or real restriction was installed or enabled.

### M1 — domain, persistence, and simulation

- Added typed profile, rule, and session models with validation, revision conflicts, policy hash, immutable session snapshot, fixed UTC deadline, UTC checkpoint, guided early-end delay, committed-mode rejection, and clock-anomaly release.
- Added SQLite schema version 1, profile/session persistence, current-session lookup, and history readback. Session state is written before simulated activation; interrupted `Activating` state releases on restart. Release retries through `CleanupPending` if the provider fails.
- Added a no-op simulation provider. Its apply/release methods change no app, browser, or Windows setting.
- Added a current-user named-pipe protocol with bounded length-prefixed frames, versioned request/response envelopes, and a narrow command list. This is development simulation transport, not the final browser or installed-service trust boundary.

### M2 — everyday desktop simulation workflow

- Added a Windows dashboard with saved profile selection, duration, app-path/site/YouTube-term editing, three YouTube tab-action choices, simulated preflight summary, cancelable 10-second countdown, live status, exact local finish time, and Guided end control.
- A saved 1-minute session can be started through the UI. The separate host owns the deadline; closing/reopening the UI reads the same session. Restarting the host reconciles an elapsed deadline.
- Added a Manifest V3 popup scaffold that explicitly says browser restrictions are not connected.

## Verification actually run

| Check | Result |
| --- | --- |
| `dotnet restore LockIn.slnx` | Passed after adding the updated SQLite bundle dependency. |
| `dotnet build LockIn.slnx --no-restore` | Passed, 0 warnings and 0 errors. |
| `dotnet test LockIn.slnx --no-build --no-restore` | Passed, 8 tests. Tests cover deadline/snapshot, clock anomaly, Guided delay/cancel, Committed rejection, revision conflict/persistence, oversized pipe frame, duplicate activation/restart expiry, and interrupted activation release. |
| `npm ci` in `extension` | Passed, 0 reported vulnerabilities. |
| `npm run typecheck` and `npm run build` | Passed. |
| Simulation host and WPF startup smoke checks | Both processes stayed alive for 3 seconds on Windows 10 with `DOTNET_ROOT` pointed at the local SDK; the test then closed only those LockIn processes. This does not verify UI interaction or accessibility. |

## Simulated, real, and pending

- **Real now:** Local profile/session data storage, separate process authority while its console host runs, pipe framing, WPF shell startup, extension build output.
- **Simulated now:** Restriction provider, app/site/YouTube rule effect, activation acknowledgment, cleanup side effects.
- **Pending real verification:** End-to-end UI click-through, Windows 11 runtime, installed Windows service, browser pairing/native messaging, Chrome website blocking and expiry, YouTube matching/tab actions, selected-app closure, sleep/reboot recovery, corruption recovery, accessibility, installer/uninstaller.

## Known limitations and unresolved acceptance IDs

- The current machine is Windows 10, while the plan targets Windows 11 first. Windows 11 acceptance remains pending.
- The simulation host is a console app and must be kept running for live deadline processing. If stopped, it releases overdue sessions when restarted. It is not yet a Windows service.
- The M1 monotonic clock source, full cross-component journal, and corrupt-store recovery are incomplete. The current UTC checkpoint handles bounded backward changes by releasing; sleep/reboot behavior with OS effects is unverified.
- Profile editing is functional but still basic; app discovery, rule preview matching, history UI, field-level validation messages, and complete keyboard/screen-reader review remain M2 refinements.
- SES-01/02/08/09/10/11/14/15 have automated domain or service-simulation evidence, but SES-01/02 still need a full UI click-through. SES-03 through SES-07, SES-12/13/16 and all real APP/WEB/YT/INS IDs remain unresolved. UX-01/02 are implemented in the simulation UI but not yet manually verified; UX-03/04 remain unresolved.
- No installed restriction artifacts remain, because none were created. A local development SDK and, if the simulation host is run, `%LOCALAPPDATA%\LockIn\simulation.db` may remain.

## Exact next implementation step

Finish the M2 gate with a manual 1-minute UI walkthrough, field-level validation, study-resource and destination editing, rule preview, and a basic history view. Then begin M3's native messaging and Chrome website-rule vertical slice in a dedicated test profile. Before enabling a real dynamic rule, add browser enrollment/authentication, policy acknowledgment, independent extension expiry/reconciliation, and bounded cleanup for already-open tabs. Keep app closure disabled until M5 recovery is implemented.
