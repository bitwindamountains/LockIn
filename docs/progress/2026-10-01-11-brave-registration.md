# Handoff 11 — Brave native bridge registered

The owner supplied extension ID `ncojaneafdlbailgpgdekkbdgphlhajc` for the previously requested installed Brave setup.

## Completed

- Published the self-contained Windows x64 native bridge and registered its manifest under the current user's Brave NativeMessagingHosts key using the existing guarded helper.
- Verified the registry value, executable existence and exact single allowed extension origin.
- Invoked the published executable through real framed stdio with a deliberately invalid browser credential. The running host rejected the credential, demonstrating the published executable reaches the local host. This is not a successful pairing or a Brave lookup test.
- Built the solution with zero warnings/errors and opened the desktop/local console host for pairing. Host PID at launch: 21044; desktop PID at verification: 6420. These are interactive setup processes intentionally left running.

Files: `%LOCALAPPDATA%\LockInBrowserAlpha\Brave`. Registration: `HKCU\Software\BraveSoftware\Brave-Browser\NativeMessagingHosts\com.lockin.bridge`.

No focus session was started by the agent. No Windows service or browser policy was installed. Registration was an explicit setup step following the owner's extension ID, not a build/test side effect.

## Next owner step

In LockIn, open Connections and generate a pairing code. Open LockIn's extension popup in the separate Brave profile, enter that code and pair within two minutes. Report the resulting status or exact error. Keep the local host running. Actual Brave lookup, successful pairing, permission grants and lifecycle/release remain unverified.

After sessions are released, the matching removal command is `powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/unregister-native-host.ps1 -Browser Brave`; remove the extension from the test profile separately. Do not remove components during an active session.
