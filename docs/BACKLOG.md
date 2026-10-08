# Implementation backlog

The milestone IDs and acceptance IDs come from `lockin_full_scale_build_plan (1).md`.

Current progress (handoff 19): R18-2/R18-3 fixed with regression checks; the expanded desktop design check now passes with test-owned window restoration (R18-6). Release build, 49 .NET tests, 13 policy/content-script tests, 11 browser tests and all four desktop walkthroughs pass. Installed Brave pairing passed in handoff 14; permission/activation/release and live YouTube acceptance remain open. M3/M4 are not fully accepted. See [fix report](progress/2026-10-01-19-review-fixes.md) and [status](STATUS.md).

## Next work, in order

1. Dedicated Brave-profile installed native bridge walkthrough: enrollment, permissions, activation, expiry and Guided/recovery release across the actual stdio/pipe connection.
2. Remaining worker termination/restart, browser offline at expiry and permission-removal fault matrix. Owner-removal guards, atomic rollback/revocation and distinct history evidence now pass; injected browser storage failure no longer permits stale reactivation after retry.
3. Live YouTube current-video metadata/SPA tests; delayed/stale signals, approved IDs, missing metadata, all tab actions, multi-window races, no duplicate tabs; document unsupported surfaces.
4. Coverage reporting transitions and browser rule-mismatch detection now pass component checks. Add actual content-layout coverage signals without browsing-history collection, and verify them through the installed bridge.
5. Windows 11, physical suspend/reboot, keyboard/screen-reader/scaling and installer checks.
6. M5 privileged service and selected-app identity/release design, then M6-M8 gates. Do not enable real app closure through the simulation provider.
7. Implement retention/deletion and sanitized diagnostics controls alongside packaging/release work. Keep active cleanup ownership records intact.

| Milestone | Deliverable | Acceptance IDs |
| --- | --- | --- |
| M0 | Solution, contracts, tooling, documented commands | Platform readiness gate |
| M1 | Validated profiles, immutable sessions, durable state, simulation, recovery | SES-01, 02, 08, 09, 10, 11, 14, 15 |
| M2 | Desktop profile and session workflow | UX-01, UX-02 |
| M3 | Native bridge, service endpoint, website rules | WEB-01 to WEB-05 |
| M4 | YouTube matching and all three tab actions | YT-01 to YT-24 |
| M5 | Windows service and selected-app closure | APP-01 to APP-11, SES-03, 04 |
| M6 | Cross-component recovery and failure handling | SES-01 to SES-16 |
| M7 | Install, repair, uninstall, owner alpha | INS-01 to INS-04, UX-01 to UX-04 |
| M8 | Version 1 release evidence | Section 31 |
