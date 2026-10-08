# Handoff 12 — Diagnose Brave bridge disconnect

Owner reported the generic Windows-bridge-unavailable popup. Verified the Brave registry value and exact extension origin, confirmed the desktop and local host are running, and directly invoked the published bridge. Its framed probe reached the host and received the expected invalid-credential rejection. This does not prove Brave can discover/launch it.

Found that the extension discarded `chrome.runtime.lastError` on native disconnection. It now displays the browser's specific native-host error using the popup's existing text-only output. Rebuilt extension; typecheck/build and all 5 policy tests passed. No native registration or restriction behavior changed.

Owner action: reload LockIn at brave://extensions, reopen its popup, and retry pairing with a fresh code. Send the specific new error if pairing still fails. Root cause of Brave's disconnect remains unconfirmed pending that diagnostic. Do not infer successful installed integration from the direct executable probe.
