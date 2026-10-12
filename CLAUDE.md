# LockIn — notes for Claude

@AGENTS.md

## Orientation

- Spec: `lockin_full_scale_build_plan (1).md`. Current state: `docs/STATUS.md`. Decisions: `docs/DECISIONS.md`. Latest handoff: `docs/progress/2026-10-13-20-review-fixes-and-eclipse.md`.
- Active work items and acceptance criteria: `SPEC.md` and `tasks/plan.md` / `tasks/todo.md`.
- Visual identity is **Eclipse** (owner choice, 2026-10-13); see `docs/DESIGN_SYSTEM.md`. Do not reintroduce the forest-green/paper or lamplit-desk styling, gradient text or meta-labels.

## Commands (PowerShell, repository root)

```powershell
$sdk = "$env:LOCALAPPDATA\LockInDev\dotnet\dotnet.exe"
& $sdk build LockIn.slnx -c Release
& $sdk test LockIn.slnx          # about 75 s; the native-bridge test waits for a real 60 s release
# extension/: npm run typecheck; npm run build; npm test; npm run test:browser
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/test-desktop.ps1 -Configuration Release [-DesignOnly | -EditorOnly | -BrowserRecovery]
```

Run the desktop harness modes one at a time. They open their own test window and host; never close the owner's LockIn processes. Intermittent harness pipe timeouts were traced to memory pressure and to the R21-5 UI-thread stall (now fixed). If they recur, check free memory before changing code.

## Conventions that bite

- The host is the session authority. Pipe client code must not resume on the caller's context (`ConfigureAwait(false)` in `PipeTransport`).
- Live-session reads in `LocalStore` are strict; history tolerates damaged rows. Never delete session rows.
- The extension pages may only load bundled scripts, fonts and images (CSP `script-src 'self'`); no remote fonts or images. Decorative SVG lives in `extension/src/scene.ts`, and its state comes from `body[data-state]`.
- Motion is presentation only. Resting CSS styles are the final state, and keyframes animate only from them, so reduced motion still shows the correct state. In WPF, gate animations on `MotionEnabled`.
- Asking first is required for: schema changes or migrations, the native command allowlist, new dependencies, and deleting code or data.
