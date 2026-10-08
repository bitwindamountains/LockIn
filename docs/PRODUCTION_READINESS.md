# Production readiness gates

Updated 2026-10-01, handoff 19. **Not production ready.** The [full review](progress/2026-10-01-18-full-review.md) found two defects and an incomplete design run; [handoff 19](progress/2026-10-01-19-review-fixes.md) fixes the defects and completes the expanded design checks. This checklist tracks remaining implementation and evidence; it does not substitute for the original plan's acceptance cases.

## Current environment and owner setup

Storage blocker cleared: approximately 23.6 GB free at the start of the handoff 19 build. The owner supplied the Brave extension ID and native registration is installed. Real installed-Brave pairing passed in isolated profiles in handoff 14. Installed permission/activation/release and live YouTube acceptance remain open. Handoffs 15–19 improve frontend behavior/reliability; they do not establish those enforcement gates.

Launch/test helpers now refuse work below 1 GB free. This is a minimum guard, not a promise that 1 GB is sufficient for a release build. Direct dotnet/npm commands do not all run the guard; run `scripts/check-development-space.ps1` first using the README's process-local execution-policy invocation.

## Work remaining, in order

| Gate | Required implementation/evidence | Owner involvement |
| --- | --- | --- |
| Reproduced defects and design verification | R18-2/R18-3 fixed with regressions; R18-6 complete design check passes after normalizing test-owned windows | No owner action needed for these fixes |
| Reliable development environment | Storage headroom restored; retain space guards and isolated test data | No current storage action needed |
| Native integration | Bridge process lifecycle and installed Brave pairing verified; complete installed permissions/activation/release | Dedicated test browser/profile for installed walkthrough |
| Browser recovery | Worker/browser restart, offline expiry, permission loss, stale messages and release acknowledgment matrix | Reboot/sleep tests on test machine later |
| Live YouTube | Current-video/SPA/delayed metadata, approved IDs, all tab actions, media neutralization and supported-layout evidence | Live browser access; no personal account required for public fixtures |
| Desktop application enforcement | Privileged service, authenticated owner-scoped IPC, executable identity checks, protected-app exclusions, explicit preflight, release/recovery; test-owned process fixtures | Dedicated Windows test machine or VM for installation and disruption tests |
| Packaging and recovery | Install/update/repair/uninstall, recovery tooling, owned-artifact cleanup and permissions | Administrator access in that test environment when concrete package is ready |
| Local data controls | Configurable retention/deletion that preserves active cleanup evidence, and sanitized diagnostics export; a 50-row query limit is not retention | No owner input needed for implementation |
| Release validation | Accessibility/scaling, Windows support matrix, privacy/retention, performance, clean-machine install and owner demo | Interactive accessibility/display and physical sleep/reboot checks |

Ordinary builds/tests must not install a service, close the owner's applications or change browser/OS policy. Any system-changing demonstration will have a concrete setup, cleanup procedure and evidence checklist before it is run. Advanced Windows launch prevention is a separate optional gate and is not implied by basic app closure.

## Evidence discipline

- Current host is a user console process; it is not a Windows service.
- UI and simulation tests do not demonstrate real app enforcement.
- Synthetic Chromium tests do not establish live YouTube or installed native integration.
- Preserve exact entered allow-tag behavior and honest cleanup evidence.
- Mark acceptance only after testing the shipped path, including failure and release.
- Refresh `docs/STATUS.md` and add a progress report at every handoff.
