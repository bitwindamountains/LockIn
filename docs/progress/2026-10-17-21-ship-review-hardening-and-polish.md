# Ship review, extension hardening and polish — handoff 21

**Date:** 2026-10-17  
**Milestone:** M3/M4 alpha reliability: review follow-ups, a deployment-readiness review, browser-bridge hardening and an extension polish pass.  
**Release:** Developer alpha. **Not production ready.** The production gates R18-1, R18-4 and R18-5 are unchanged.

## Accomplished

### Review of the R20/R21 and Eclipse commits

- **Popup eclipse on a lost host (`147b83e`):** the popup's polling failure updated the status text but left the scene state, so a lost connection during a session still showed a total eclipse. It now switches to attention, as the study page already did. A duplicate `SessionTicket.Padding` assignment was removed.

### Desktop performance

- **Eclipse breathing capped at 15 fps (`ff36397`):** the corona's endless 4% scale animation redrew a 396 px radial-gradient ellipse at 60 fps for the whole session. Measured over 60 s, Release, 1140×740, session active and window visible, two runs each:

| Configuration | CPU | GPU |
| --- | --- | --- |
| 60 fps (before) | 3.66% / 4.14% | 6.58% / 11.30% |
| 15 fps (after) | 1.72% / 1.74% | 2.75% / 2.81% |
| Motion reduced (control) | about 0.4% | about 0.2–0.3% |

A minimized window already cost nothing extra, because WPF stops rendering it; the earlier review suggestion to pause the animation when minimized was withdrawn. The compositor (`dwm`) column could not be read, so compositor cost is not counted.

### Deployment-readiness review

Three parallel reviews (code, security, tests) over the branch. Verdict: **no-go for production, go as a developer alpha.** No new Critical defects, no secrets in the tree or history, `npm audit` and `dotnet list package --vulnerable` clean. Production stays blocked by the tracked gates: the host is a user console process, app enforcement is a no-op provider, there is no installer, and retention/diagnostics controls are missing.

### Browser-bridge hardening (`aac5a4b`)

- **Framed pages:** only `blocked.html` is web accessible now (the website-rule redirect needs it); `focus.html`, `page.js` and `popup.css` are not. Messages from an extension page inside a website's frame are refused ("Sender is not supported."), and a framed blocked page never polls. Before, a hostile site could embed many copies, each polling the single-connection host every 5 s.
- **Idle traffic:** `release()` returns early when nothing is applied, so idle syncs no longer query and message every YouTube tab every 5 s. Each released session is acknowledged to the host once per worker instead of on every poll.
- **Pairing limits tested:** malformed codes do not count, five attempts at most, two-minute expiry, single use, ten browsers at most.
- **Flaky browser tests:** the offline-expiry deadline is 10 s (was 5 s) and the YouTube status wait 15 s; both had failed while the .NET suite ran in parallel.

Each new test failed with its fix removed.

### Extension polish (`2c6d90c`, `fea1eeb`)

- The popup moon says "Status unavailable" when the host is unreachable, instead of keeping "Checking connection…" or a stale remaining time.
- Error alerts are a tinted glass box instead of a 2px coloured left bar; on the study pages the alert is centred and capped at 60ch.
- The study-link arrow is a drawn stroke icon instead of a text glyph. Selection, caret and scrollbars are themed. The popup footer drops one of three stacked dividers.
- **Idle wording (owner-approved):** with no session running, the status line reads "No session is running in this browser." (was "No active browser policy remains." plus popup setup instructions). Headings and footers carry an idle variant in `data-idle`: the blocked page reads "Your session has ended." with "Revisit the site from the address bar. Tabs do not reopen automatically."; the study page footer reads "Study links appear here during a session. Edit them in LockIn." Session wording is unchanged, and nothing claims cleanup is complete.

The Impeccable design detector flagged Geist, the ember glows, corona rays and star halos; these are pinned by the Eclipse design system and were kept. Its contrast and padding findings were false positives against the renders.

## Verification

| Check | Result |
| --- | --- |
| Release build | Passed; 0 warnings/errors |
| .NET tests | 54 passed (53 plus the pairing-limits test) |
| Extension typecheck/build | Passed |
| Extension unit/fault tests | 15 passed |
| Browser suite | 15 passed (13 plus framed-page and idle-release cases) |
| Extension captures | 14 renders: 6 popup states; study and blocked pages at 375, 768 and 1280 px. No overflow or console errors |
| Desktop harness modes | Not run in this handoff |

## Not verified

Installed Brave and live YouTube after these changes, including whether the trimmed web-accessible resources still behave as expected there. The 15 fps breathing was not reviewed by eye in the running app. Desktop harness modes were not run.

## Open findings from the readiness review

- **Needs owner approval (schema change):** browser polling filters sessions with an unindexed `json_extract` (`LocalStore.cs:180`), a full scan per poll that grows with history. Proposed fix: an indexed `browser_installation_id` column.
- **Before a real enforcement provider:** a release inside `HandleAsync` can outlast the host's 4 s request budget (`Program.cs:424`).
- **Low / informational:** a silent same-user client can hold the host's only pipe instance; another Windows account can take the pipe name first (denial of service only); `LOCKIN_NATIVE_CONFIG` is honoured in release builds; engine exception messages reach the extension verbatim; the manifest has no `key`, so the unpacked extension ID depends on its path; `blocked.html` still lets sites detect LockIn (`use_dynamic_url` untested).
- **Test gaps:** readiness lost during countdown, mismatched or missing pipe replies, concurrent countdowns, malformed frame headers, native-host field validation, a damaged newest row in browser lookup, and popup coverage-issue states.

## Owner handoff

Reload the unpacked extension at `brave://extensions` and reopen LockIn between sessions to load these changes. All commits are pushed to `origin/design/lamplight-desk`; `master` is unchanged.

## Next

Owner decision on the polling index. Then the installed-Brave walkthrough and the remaining production gates (R18-1, R18-4, R18-5).
