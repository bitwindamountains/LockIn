# LockIn redesign — verification record

**Date:** 29 September 2026  
**Artifact:** `lockin_redesign_prototype.html`  
**Scope:** UI prototype and design handoff. No Windows enforcement or extension integration.

## Outcome

The supplied screenshots were inspected. The prototype has passed JavaScript syntax and isolated logic checks. Generated markup was checked for duplicate IDs and missing form/button names. Selected color pairs were calculated. New rendered screens and browser-driven interaction checks remain unverified because the available rendering routes were blocked.

This is not a claim of WCAG conformance, platform integration, or release readiness.

## Isolated logic checks

The harness evaluates the actual prototype script with lightweight DOM stubs. It drives the script's event handlers and state transitions; it does not reproduce browser layout, native event propagation, accessibility APIs, animations, or platform enforcement.

| Check | Result |
| --- | --- |
| Whole-minute duration bounds and HTTPS safety | PASS |
| Saved profile validation rejects empty names and destination conflicts | PASS |
| Countdown cancellation applies no simulated session | PASS |
| Lost required connection cancels activation | PASS |
| Profile save during a session preserves its immutable snapshot | PASS |
| Guided early ending waits 60 seconds and then releases | PASS |
| Guided end request can be canceled | PASS |
| Committed mode rejects ordinary early end but supports recovery | PASS |
| Cleanup pending is not falsely recorded as complete; retry completes once | PASS |
| Active browser disconnect changes coverage without changing deadline | PASS |
| Hashtags match exactly, not aliases or substrings | PASS |
| Exact approval overrides broad terms; exact block overrides approval | PASS |
| Unknown metadata has a distinct outcome and never approves an unapproved video | PASS |
| Title matching uses whole normalized words and ordered phrases | PASS |
| Website addition normalizes a domain and rejects unrelated URL paths | PASS |
| Draft removal supports undo without mutating an active profile | PASS |
| User content is escaped as text in generated markup | PASS |
| Page templates generate all reviewable states without JS errors | PASS |

## Static checks

| Check | Result | Limitation |
| --- | --- | --- |
| JavaScript syntax (`node --check`) | Pass | Does not execute browser APIs. |
| Generated screen templates | 15 templates inspected | HTML generation only. |
| Duplicate IDs within each generated screen | None found | Dynamic browser state still needs checking. |
| Input/select/textarea accessible names | Explicit or enclosing labels present | Not a screen-reader audit. |
| Visible button names | Text or aria-label present | Does not verify speech output or focus order. |
| CSS delimiter balance | Pass | Not a full CSS parser or layout validation. |
| Runtime fetch/XHR/WebSocket calls | None | Clicking a study link may open its actual website. |
| Inline fragment size | Under 100 KB | No external runtime dependencies. |
| Selected color contrasts | Body/button pairs exceed 4.5:1; selected control boundaries exceed 3:1 | Does not cover every composited, disabled, hover, or focus state. |
| Reduced-motion source handling | System preference and app preference implemented | Runtime media-query changes not observed in a browser. |

## Rendering attempts and blockers

1. Local Playwright launch failed because the expected Chromium executable was unavailable.
2. An installation from the official Playwright download source was attempted; the returned archive was unusable. No alternate browser binary was run.
3. The browser preview attempted to open the locally served prototype and returned `net::ERR_BLOCKED_BY_CLIENT`.

No new prototype screenshots were produced. Desktop/mobile layout, clipping, focus geometry, and animation smoothness are not reported as passed.

## Checks still required

| Check | Status |
| --- | --- |
| 1440/1024/768/390/320 px rendered layouts | Not run |
| 200% and 400% browser zoom / reflow | Not run |
| Keyboard-only start → countdown → active → completion | Not run in a real browser |
| Dialog focus trap, Escape, and focus restoration | Implemented using native dialog; not browser-verified |
| Screen-reader labels and announcements | Not run |
| Light, dark, forced-colors, and all focus-state contrasts | Partial token calculation only |
| Reduced motion and motion interruption | Source reviewed; not runtime-verified |
| Real service persistence, process closure, permissions, tab redirection, expiry, recovery | Not implemented by this prototype |
| User testing against the original interface | Not run |

## Reviewable motion behavior

- 120 ms control transitions.
- 140 ms local countdown changes.
- 180 ms feedback entrance.
- 240 ms screen/section/dialog entrance with only 6 px travel.
- A still active countdown display and restrained progress track.
- No loops, sound, parallax, confetti, animated gradients, or remote animation dependencies.
- Reduced motion removes animation while preserving the state change.

## Artifact identity

Prototype size: 77,137 bytes.  
SHA-256: `70146c36dab1b3be60f9fd8bf0a48df3ccd9fe8e0df988bf6844683882a018d4`
