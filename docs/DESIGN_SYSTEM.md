# LockIn frontend design system

## Identity

A quiet study journal: warm paper, forest ink, modest serif page titles, a prominent session clock and structured lists. Reserve tinted surfaces for the session itself. Use flat sections for forms/help and rows for history; no decorative stat cards or imagery.

## Foundations

| Role | Value / rule |
| --- | --- |
| Body | Segoe UI/system sans, 14px native / 15px web |
| Page title | Georgia, 28px native/popup; 30–36px destination pages |
| Section | 16–20px, semibold |
| Supporting text | 12–13px; never shrink important copy to make it fit |
| Spacing | 4px base: 4, 8, 12, 16, 24, 32 |
| Paper / ink | #F7F7F0 / #20392C |
| Secondary / primary action | #586657 / #254B36 |
| Session tint / divider | #E5EDCE / #D4D9CD |
| Error / keyboard focus | #A02B20 / #175ACA |
| Radius | 6px web controls, 8px native controls/panels |
| Targets | Buttons/inputs/disclosures at least 44px tall; checkbox label is the target |
| Motion | 180ms opacity for navigation/disclosure; no animated clock digits |

Native primitives live in `Resources/QuietStudy.xaml`: SurfaceCard, SectionPanel, SupportingText, Error, PrimaryButton, SecondaryButton, FocusRing and the Expander template. Browser surfaces share tokenized `popup.css`. Browser coverage wording is centralized in `shared/presentation.ts`.

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

Respect Windows reduced motion and web `prefers-reduced-motion`; local preferences can reduce motion further. Host authority, permissions, immutable session rules and release logic never depend on animation.

Measured static contrast pairs: body/paper 11.60:1; secondary/paper 5.65:1; secondary/session tint 5.02:1; primary button 9.82:1; navigation 9.31:1; error/paper 6.82:1; input border/white 3.34:1. These checks do not constitute a complete accessibility certification.

Desktop minimum is 620×540; there is no native mobile app. Verify web surfaces at 320, 375, 768, 1024, 1280 and 1440px. Physical scaling, high contrast and screen-reader acceptance require further testing.
