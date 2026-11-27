# Handoff 13 — Correct Brave native-host lookup

The owner reported the specific error: native messaging host not found. Previous Brave-only registry mapping was insufficient for the installed Brave 154.1.96.59.

Read-only inspection of its chrome.dll found the UTF-16 `SOFTWARE\Google\Chrome\NativeMessagingHosts` lookup string and no equivalent BraveSoftware path. This agrees with [Chromium's Windows native launcher](https://raw.githubusercontent.com/chromium/chromium/main/chrome/browser/extensions/api/messaging/launch_context_win.cc), which uses the Chrome-compatible lookup (and Chromium fallback).

Added the current-user Google/Chrome host registration pointing to the existing Brave manifest, after verifying no conflicting registration existed and that the manifest permits only the supplied extension origin. No binaries were replaced. Updated future Brave setup to use that compatible registry path. Updated removal to check ownership of both compatible and legacy Brave keys before deleting either; published files remain separate per browser. Chrome and Brave registrations of the same host name now intentionally conflict rather than silently overwriting each other.

Registry read-back and PowerShell parser checks passed. The original Brave-only key remains as a legacy alias, covered by the updated removal helper. Actual Brave pairing must still be retried; do not claim success from registry verification alone. No focus session or restriction was started.

Next: close/reopen the extension popup, generate a fresh code in Connections and retry Pair. If it fails, capture the new specific error.
