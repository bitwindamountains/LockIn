# LockIn frontend design system

## Identity

A lamplit study desk at night. Warm paper and forest ink carry reading and forms. The session itself is a night-desk scene: one desk lamp, an open notebook, a pencil and a mug. The lamp is the session's character. It rests switched off and lifted when idle, leans in and lights the notebook during a session, and shows an amber-red bulb when cleanup needs attention. The blocked page tells one short story: the distracting site, drawn as a small browser-window letter, is tucked into an envelope and sealed "later". Objects echo state that text already states; they never replace it. Use flat sections for forms/help and rows for history; no stat cards, stock photography or remote images.

## Foundations

| Role | Value / rule |
| --- | --- |
| Body | Segoe UI/system sans, 14px native / 15px web |
| Page title | Georgia, 28px native/popup; 30–36px destination pages |
| Section | 16–20px, semibold |
| Supporting text | 12–13px; never shrink important copy to make it fit |
| Spacing | 4px base: 4, 8, 12, 16, 24, 32 |
| Paper / ink | Web #F7F4EA, native #F7F7F0 / #1F3A2B web, #20392C native |
| Night scene | #1B3125 to #12221A gradient; text #F4EFE0, supporting #B9C7B2 |
| Lamp accent | #F2B84B amber, #F7D58A glow (active timer and lamp light only); alarm bulb #E07A5F |
| Secondary / primary action | #586657 / #254B36 |
| Session tint / divider | #E5EDCE / #D4D9CD |
| Error / keyboard focus | #A02B20 / #175ACA |
| Radius | 6px web controls, 8px native controls/panels |
| Targets | Buttons/inputs/disclosures at least 44px tall; checkbox label is the target |
| Motion | Navigation/disclosure 180ms; page sections rise 10px with a 70ms stagger; lamp tilt 0.9-1.1s spring and a one-time switch-on flicker; mug steam loops only during an active session; letter story 1.3s once. Clock digits never animate. |

Scene markup lives in `extension/src/scene.ts` (inline SVG, decorative, `aria-hidden`) and in the session ticket of `MainWindow.xaml` (`LampScene`). Native primitives live in `Resources/QuietStudy.xaml`: SurfaceCard, SectionPanel, SupportingText, Error, PrimaryButton, SecondaryButton, FocusRing and the Expander template. Browser surfaces share tokenized `popup.css`. Browser coverage wording is centralized in `shared/presentation.ts`.

## Composition and behavior

- **Focus:** profile/duration and session panel; compact view groups fields horizontally. Keep Start visible at the tested 640×600 configuration. Review precedes countdown.
- **Review:** separate scope and consequences from the exact-entry disclosure; render the validated snapshot, not live editor fields. New drafts have a visible name. Contextual custom URL fields retain their value when hidden, and validation focuses the appropriate visible selector or input after layout.
- **Profiles:** stable section tabs and persistent save footer. Exact hashtag entry stays visible; exceptions/redirects are disclosed together. Validation opens the appropriate disclosure before focusing a field.
- **History:** search and refresh together, secondary filters in a disclosure whose header indicates active filters. Scan-friendly rows keep cleanup evidence visible. Latest 50 records only; elapsed time is not verified study time.
- **Connections:** one-time setup and connected state remain distinct. Site-access consent is explicit.
- **Help:** flat sections for connection/release, exceptional owner-confirmed removal and motion. Never hide release consequences solely in a tooltip.
- **Browser:** status first, setup/access when needed; secondary appearance/coverage details collapsed. Destination pages show study links as a stable list and adapt from two columns to one below 768px.

## State and accessibility rules

Pending session review and asynchronous profile operations disable conflicting edits/actions. Keep navigation and review Back/countdown Cancel available; explain how to return to editing. Save/stage/prepare use the operation's captured profile context. A running session still permits editing the next profile after activation completes.

Show busy labels for asynchronous actions and disable repeated submissions. Preserve focused resource nodes during polling; move focus to a visible heading when a completed setup/access section disappears. Empty, unavailable and recovery states have explicit text, not color alone. Do not declare cleanup complete merely because no policy is cached.

Respect Windows reduced motion and web `prefers-reduced-motion`; local preferences can reduce motion further. With motion reduced, every object shows its resting state for the current session state (lamp on or off, envelope sealed) and nothing loops. Resting styles are the final state; keyframes animate only from it. Host authority, permissions, immutable session rules and release logic never depend on animation.

Measured static contrast pairs, night scene at its lighter end (#1B3125): text 12.08:1, amber timer 9.81:1, supporting 7.85:1. On #12221A: alert 9.69:1, amber focus ring 9.24:1. Web paper: body 11.24:1, secondary 5.53:1, link-card arrow hover 5.54:1. Earlier pairs: body/paper 11.60:1; secondary/paper 5.65:1; secondary/session tint 5.02:1; primary button 9.82:1; navigation 9.31:1; error/paper 6.82:1; input border/white 3.34:1. These checks do not constitute a complete accessibility certification.

Desktop minimum is 620×540; there is no native mobile app. Verify web surfaces at 320, 375, 768, 1024, 1280 and 1440px. Physical scaling, high contrast and screen-reader acceptance require further testing.
