# LockIn

LockIn is a local Windows study-focus app in development. The current build provides a tested simulation workflow and an **M3/M4 browser alpha** with selected website redirects and exact YouTube allow-tag filtering. Desktop app closure is unavailable. This is not production ready. Browser setup is explicit; ordinary builds do not install restrictions.

Browser coverage and owner-reported recovery have been built and tested. Installed Brave pairing passed in isolated profiles in handoff 14. Handoffs 16–17 redesign the main frontend screens and improve first use, optional settings and session review, with verified session/editor/recovery journeys. Installed browser permission/activation/release and live YouTube acceptance remain pending. Handoff 19 fixes the two review defects with regression tests and completes the expanded desktop design check. See the [progress report](docs/progress/2026-10-01-19-review-fixes.md).

The specification is [`lockin_full_scale_build_plan (1).md`](lockin_full_scale_build_plan%20(1).md). Progress and pending acceptance cases are in [`docs/STATUS.md`](docs/STATUS.md).

## Requirements

- Windows and .NET SDK 10.0.401 or a compatible 10.0 patch SDK.
- Node.js 24 and npm for the extension build.

This checkout was built on Windows 10 Pro 22H2 (19045), which is outside the plan's initial Windows 11 target. The .NET SDK was installed at `%LOCALAPPDATA%\LockInDev\dotnet` for this development environment; on a normal .NET installation, use `dotnet` in the commands below.

The frontend follows the [screen audit](docs/UI_AUDIT_16.md) and [design system](docs/DESIGN_SYSTEM.md), building on the [research plan](docs/UX_REFACTOR_PLAN.md). Focus now has an editable duration field, inline browser readiness and expandable rule details. See [current status](docs/STATUS.md). To build/check a separate copy while Debug is open, use `dotnet build LockIn.slnx -c Release`, then add `-Configuration Release` to the desktop test or launch helper. `-SkipBuild` opens an already built copy.

If you launch the generated `.exe` files directly on this development machine, set `$env:DOTNET_ROOT = "$env:LOCALAPPDATA\LockInDev\dotnet"` in that terminal first. The `& $sdk run` commands below use the local SDK directly.

## Build and test

In PowerShell from the repository root:

```powershell
$sdk = "$env:LOCALAPPDATA\LockInDev\dotnet\dotnet.exe"
& $sdk restore LockIn.slnx
& $sdk build LockIn.slnx
& $sdk test LockIn.slnx --no-restore
```

If `dotnet` is already on PATH, replace `& $sdk` with `dotnet`.

For the extension:

```powershell
Set-Location extension
& 'C:\Program Files\nodejs\npm.cmd' ci
& 'C:\Program Files\nodejs\npm.cmd' run typecheck
& 'C:\Program Files\nodejs\npm.cmd' run build
& 'C:\Program Files\nodejs\npm.cmd' test
& 'C:\Program Files\nodejs\npx.cmd' playwright install chromium
& 'C:\Program Files\nodejs\npm.cmd' run test:browser
```

## Run the one-minute simulation

For a single-command launch from the repository root:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/start-simulation.ps1
```

This builds the solution, starts the simulation host in the background, and opens the desktop. The PowerShell policy flag applies only to that process; it does not change the machine's policy. The launcher prints the host PID and log directory. If you want to stop the host, end that specific process after the session finishes. Restarting the launcher reconnects to an existing host.

Alternatively, use two terminals. In the first, from the repository root:

```powershell
$sdk = "$env:LOCALAPPDATA\LockInDev\dotnet\dotnet.exe"
& $sdk run --project src/LockIn.Service
```

This is a current-user console host, **not** an installed Windows service. In the second terminal:

```powershell
$sdk = "$env:LOCALAPPDATA\LockInDev\dotnet\dotnet.exe"
& $sdk run --project src/LockIn.Desktop
```

Use the **Profiles** tab to name a profile, choose `1` minute, browse for apps, add website hosts and HTTPS study links, and configure exact YouTube hashtags, approved videos, tab action, and destination. Website entries include subdomains by default; prefix a host with `=` for an exact-host rule. The rule preview accepts synthetic current-video hashtags and supports an explicit unavailable-metadata state.

Select **Start focus** in the **Focus** tab. It reviews and saves the current draft, then starts a 10-second cancelable countdown that the host also enforces. The exact deadline lives in `%LOCALAPPDATA%\LockIn\simulation.db`. Closing and reopening the desktop retains the session; restarting the host reconciles the original deadline. A Guided early-end request releases after 60 seconds. **Release / recover** is available under **Help & motion**. **History** shows the latest 50 host records as rows with timing and cleanup details. Search by profile, filter by scope or local date, and see elapsed totals for the matching records. These are session times, not verified study time. Use **Ctrl+S** to save a profile; the editor footer shows unsaved changes.

Leave **Use my paired browser** unchecked for simulation: app, site, and YouTube rules have no enforcement effect in that scope. For real browser alpha setup and recovery, read [Browser alpha](docs/BROWSER_ALPHA.md). New profiles allow only videos with a matching entered visible hashtag; exact approved videos override this requirement. Legacy saved profiles retain their clearly labeled block mode until changed.

## Recovery and data

The simulation creates no OS or browser restriction artifacts. If the host is stopped, simply restart it; it reconciles any expired session before accepting commands. The SQLite file is local to the current Windows user. There is no account, cloud service, or telemetry in this slice.

Schema version 3 adds browser enrollment and retains the one-live-session constraint. Upgrading an idle version 1 or 2 database first creates `simulation.db.v1.backup` or `simulation.db.v2.backup`; live sessions must finish using the prior build before upgrade. Unknown versions and invalid snapshots are rejected. A corrupt database is preserved rather than silently replaced. To continue a **simulation only** while retaining a damaged file for diagnosis, stop its host and use a new data file explicitly:

```powershell
& $sdk run --project src/LockIn.Service -- --data-path "$env:LOCALAPPDATA\LockIn\fresh-simulation.db"
```

This fresh-file workflow applies to simulation only. Browser sessions own extension artifacts and must follow [browser release/recovery](docs/BROWSER_ALPHA.md#release-and-recovery).

## Windows verification

The regular test suite includes an isolated Windows host process test. It creates temporary data, checks that incomplete pipe requests time out, restarts only its own host, and verifies the original deadline. Portable tests can be selected with `--filter 'Category!=WindowsIntegration'`.

After building, run the WPF walkthrough on Windows:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/test-desktop.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/test-desktop.ps1 -EditorOnly
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/test-desktop.ps1 -BrowserRecovery
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/test-desktop.ps1 -DesignOnly
```

The full walkthrough takes about 80 seconds. It creates a temporary profile, cancels a countdown, runs a real-time one-minute simulation, closes/reopens its desktop, and checks completion. The editor check verifies study-link, video-approval, tab-action, and destination persistence. Both use unique local pipes and temporary databases, close only their own test processes, and print the evidence directory. They do not install a service or browser extension.

`-DesignOnly` checks compact navigation, reachable save, validation routing, reduced motion and backing out of inline review. Desktop motion follows Windows settings; Help & motion provides a window-local override. The browser popup persists its own reduce-motion preference.

Run desktop walkthroughs sequentially; a local mutex prevents overlapping runs. `-BrowserRecovery` simulates browser messages through the host pipe, verifies coverage/permission-loss UI, waits for the original deadline and exercises owner-reported removal. It installs no browser restriction. Browser tests separately use disposable Chromium profiles; two cases simulate native responses to exercise reporting and storage-failure recovery.

## Next milestone

Next, complete installed browser permission/activation/release, live YouTube and the remaining recovery matrix. Windows service/app enforcement, Windows 11, accessibility and packaging remain pending. See [status](docs/STATUS.md) and the [latest report](docs/progress/2026-10-01-19-review-fixes.md).

## Development disk space

Keep at least 5 GB free (10 GB preferred) for builds and disposable browser profiles. Run `powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/check-development-space.ps1` before direct build commands. Desktop launch/test and native-registration helpers, plus Chromium fixtures, refuse work below 1 GB free. These guards delete nothing and do not change runtime release/recovery. See [production readiness](docs/PRODUCTION_READINESS.md).

## Simplified local setup

On this development PC, open the **LockIn** desktop shortcut. It starts the existing build without rebuilding. In Connections choose **Connect browser**, then paste the copied code into the Brave extension; pairing is automatic. Approve site access only if requested. Start focus prepares the current profile automatically before review/countdown. After extension code updates, reload the unpacked extension once at brave://extensions.
