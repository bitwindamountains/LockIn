# Browser alpha setup and recovery

This is a developer alpha. Real website redirects and supported YouTube video filtering require explicitly enabling the browser path. Desktop application closure is unavailable. The default desktop path remains simulation.

Coverage and owner-recovery changes were built and tested in handoff 5. Host rollback/guard checks, desktop owner-recovery UI and isolated browser reporting checks pass. This still does not verify an installed native bridge or live YouTube.

## YouTube access

New profiles **only allow videos with at least one exact entered visible hashtag**. For example, `education` permits `#education`, including case/compatibility normalization, but not `#educationTips`. No match or unverified metadata redirects. Exact approved video IDs override the tag requirement. Existing profiles retain their legacy block mode until you change **YouTube access** and save; running snapshots never change.

Only current-video title/description hashtag anchors are read. Private creator tags, inferred subjects, comments, and recommendations are not tag sources. The supported content-script origin is `https://www.youtube.com/*`. Embedded players, mobile domains, other profiles, and incognito are outside alpha coverage. Shorts/unsupported watch layouts cannot verify hashtags and redirect in allow mode. No claim of zero-frame/zero-audio playback is made.

## Explicit setup

These steps change the selected development browser's extension setup. They are **not** run by builds or tests. Finish any active session before upgrading or removing components.

1. Build the .NET solution and extension using the README commands.
2. Create a separate Brave development profile (the owner uses Brave). Open `brave://extensions`, enable developer mode, and load `extension/dist` as unpacked. Copy its 32-character extension ID.
3. From the repository root, register the current-user native bridge explicitly:

   ```powershell
   powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/register-native-host.ps1 -ExtensionId YOUR_EXTENSION_ID -Browser Brave
   ```

   The helper publishes a self-contained Windows x64 bridge to `%LOCALAPPDATA%\LockInBrowserAlpha\Brave` and creates only its named HKCU native messaging registration. It installs no Windows service or browser policy. Existing registrations/configuration are not overwritten. `-Browser Chrome` and `-Browser Edge` are also available. Brave uses the compatible `HKCU\Software\Google\Chrome\NativeMessagingHosts\com.lockin.bridge` location on the verified installation; its files remain in the Brave folder. Chrome and Brave cannot register different manifests under that same host name simultaneously with this helper; browser-specific installed integration is still unverified.

4. Start the desktop/local host with `scripts/start-simulation.ps1` as documented in the README. Despite the compatibility name, this host serves both scopes. Keep it running.
5. In **Connections**, choose **Connect browser**. This saves/prepares the profile and copies a two-minute setup code. Paste it into the extension: pairing happens automatically. Codes remain single-use with five attempts.
6. Use a **Guided** profile with no desktop apps. If the popup requests site access, approve the listed origins once. Already-granted access is not requested again.
7. Choose **Start focus**. The app prepares current settings and checks readiness automatically before showing the restriction review and cancelable countdown. Connect browser selects browser scope; confirm that scope in the review. No separate Send profile or Refresh step is required.

The host stores an Activating session and marks it Active only after the extension installs policy and acknowledges its exact hash. If acknowledgment is absent for 15 seconds or the host restarts during activation, the host compensates with release. Browser alpha permits Guided mode only.

## Release and recovery

- Normal expiry, Guided end, and **Release / recover** all request cleanup. The extension removes owned dynamic rules, unregisters its content script, clears its derived cache, and releases any video gate. It does not reopen closed tabs or autoplay paused media.
- The extension also checks its cached deadline without the host. Alarms/polling require the browser to run; startup checks expire stale rules. Browser scheduling can delay cleanup. This is not an OS-wide restriction or a tamper-proof timer.
- The host displays **CleanupPending** until the enrolled browser acknowledges release. Reopen that browser and host to complete acknowledgment. Lost enrollment/extension storage cannot produce a verified acknowledgment; use the explicit owner-reported removal flow below instead of editing the database.
- To regain immediate access when the extension is malfunctioning, disable it in the dedicated development browser. This removes its enforcement while disabled. Re-enable with the host running to reconcile. If its pairing is lost, remove the extension from that profile, wait until the original deadline, then open **Help & motion → A browser went missing?**. Check the removal confirmation and choose **Record extension removal**. The host records **Owner reported removal**, revokes that pairing and finishes the expired pending session atomically. This is an owner attestation, not automatic cleanup proof. Re-pair before using that browser again. Host guards/rollback and the desktop flow have been tested with a simulated browser actor.
- Do not delete the database or replace it with a blank file as a browser recovery step. Preserve it for diagnosis.
- After confirmed release, remove the extension from the development browser, then run:

   ```powershell
   powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/unregister-native-host.ps1 -Browser Brave
   ```

   This removes only the matching current-user registration and configuration files. Published executable files remain. Use the same `-Browser` option as registration.

## Verification boundaries

The desktop shows host-derived browser health: heartbeat missing, policy report pending, policy reported installed, release pending, or degraded. The report checks installed dynamic rules, script registration and granted origins, but cannot prove that every live YouTube layout works. Host transitions, desktop permission-loss messages and browser rule-mismatch reporting have been tested.

Automated tests exercise real Chromium dynamic rules, content scripts, redirects, expiry and all three tab actions in disposable profiles against synthetic page fixtures. They seed a derived policy cache in a temporary extension copy, with **no successful native connection**. Separate .NET tests exercise host pairing/authentication/lifecycle and native process framing/origin/command rejection. These are component tests, not proof of an installed desktop-to-Chrome deployment.

Two additional Chromium cases simulate native responses in the temporary test copy to check installed-policy reporting and recovery from an injected storage-write failure. Release now retains ownership until cleanup finishes, records the ended session before discarding cache, and attempts rule removal even if that write fails. A failed storage write cannot silently erase the expiry decision and reinstall the old policy on the next successful retry.

Still required: installed bridge end-to-end walkthrough, actual Chrome permission dialogs, live YouTube layout/SPA/media behavior, worker termination/restart fault matrix, multi-window races, Windows 11/sleep/reboot, accessibility/scaling, installer repair/uninstall, and privileged Windows service/app enforcement. Do not ship as production ready.

Registration/framing follows [Chrome native messaging documentation](https://developer.chrome.com/docs/extensions/develop/concepts/native-messaging). Isolated extension verification follows [Playwright's Chromium extension guidance](https://playwright.dev/docs/chrome-extensions), checked 2026-09-29.
