# LockIn frontend design system

## Identity

Eclipse: a night sky where the session is a solar eclipse. The moon blocks the sun the way a session blocks distractions. Idle shows a partial eclipse (a crescent of sun); an active session slides the moon to totality, the corona blooms and a "diamond ring" glints once; attention needs (cleanup pending, status unavailable) let the moon slip and turn the corona red. The clock sits on the moon. Surfaces are frosted glass over a violet, ember and blue aurora with stars and film grain. One accent only: an ember gradient (gold to coral) for primary actions, focus and the corona. Objects echo state that text already states; they never replace it. No stock photography, remote images, gradient text or meta-labels.

## Foundations

| Role | Value / rule |
| --- | --- |
| Type | Geist (UI, headings) and Geist Mono (clock, scope labels, URLs), bundled under the SIL OFL; never fetched remotely |
| Display | Web page titles clamp(40px, 6.4vw, 92px), tracking -0.05em, two lines at most; native page titles 32px (26px compact) |
| Body / supporting | 15px web, 14px native / 12-13px; never shrink important copy to make it fit |
| Canvas | #07080D with aurora washes: violet #7B6CFF, ember #FF7A45, blue #40AAFF at low alpha; stars and grain on the web |
| Text | #F2EFEA primary, #AAA6B6 secondary, #8E8A9C tertiary |
| Glass | Cards 6-8% white over the canvas, 1px edge at 9-16% white, radius 20-22px (cards), 12px (fields), pill buttons |
| Accent | Ember gradient #FFC86B to #FF7A45; button text #170B04; focus ring #FFC86B |
| Status | Warning #FFB27A, error #FF8A80, active clock #FFE9C4 |
| Spacing | 4px base: 4, 8, 12, 16, 24, 32; destination pages use large section breaks (72-150px) |
| Targets | Buttons/inputs/disclosures at least 44px tall; checkbox label is the target |
| Motion | Sections rise 14px with blur and a 70-80ms stagger; the moon slides 1.9s (ease-in-out); diamond-ring glint once; corona breathes 7s and rays turn slowly only while active; sky drift and star twinkle are ambient. Clock digits never animate. |

Scene markup lives in `extension/src/scene.ts` (inline SVG, decorative, `aria-hidden`) and in the session ticket of `MainWindow.xaml` (`EclipseScene`). Native styles live in `Resources/QuietStudy.xaml` (resource keys kept for compatibility). Browser surfaces share `popup.css`. Browser coverage wording is centralized in `shared/presentation.ts`.

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

Respect Windows reduced motion and web `prefers-reduced-motion`; local preferences can reduce motion further. With motion reduced, the eclipse shows its resting position for the current state (partial, total or slipping) and nothing loops. Resting styles are the final state; keyframes animate only from it. Host authority, permissions, immutable session rules and release logic never depend on animation.

Measured static contrast pairs: primary text 17.45:1 on the canvas and 14.74:1 on the lightest aurora; secondary 7.11:1 or better; tertiary 5.34:1 on glass; warning 9.58:1; error 7.83:1 on glass; focus ring 11.69:1 on glass; button text 7.48:1 on the coral end of the ember gradient. These checks do not constitute a complete accessibility certification.

Desktop minimum is 620×540; there is no native mobile app. Verify web surfaces at 320, 375, 768, 1024, 1280 and 1440px. Physical scaling, high contrast and screen-reader acceptance require further testing.
