# LockIn — UI/UX redesign and motion specification

**Date:** 29 September 2026  
**Direction:** Quiet study room  
**Companion:** `lockin_redesign_prototype.html`  
**Basis:** `image(2).png`, `image(3).png`, and `lockin_full_scale_build_plan.md` v1.0.

This is a design handoff and interactive simulation, not an implemented Windows enforcement application. No repository or application source was supplied. The original build plan remains the authority for platform architecture, permissions, session lifecycle, enforcement, and recovery. This document develops its interface; it does not replace WPF with a website.

## 1. Design brief and assumptions

LockIn helps a student decide what to work on, temporarily put selected distractions aside, and regain normal access at the session deadline. Its everyday job is to make starting a prepared study session easy and to make its current restrictions understandable.

Assumptions used for this redesign:

- The two screenshots are the existing interface to improve, not unrelated inspiration.
- Primary use is on a Windows laptop. Narrow layouts represent small windows and extension surfaces, not a new mobile enforcement product.
- The user values a quiet, distinctive interface more than streaks, competitive scores, or elaborate dashboards.
- Existing capabilities follow the supplied plan. A screenshot alone cannot prove persistence, blocking, permissions, or recovery behavior.
- The existing forest-green and cream identity is worth developing. It has more relevance to focused study than a generic blue administration interface.
- Sample profiles, executable paths, browser connection states, and rule evidence in the prototype are illustrative. They are not detected from the user's PC.

### Main goals by supplied screen

| Screen | User goal | Primary action | Existing strengths |
| --- | --- | --- | --- |
| Focus, screenshot `image(2).png` | While idle: select and begin a session. In the captured active state: understand time remaining and current boundaries. | Idle: Start focus. Active: Open study resources; early ending is secondary. | Calm palette, readable large heading, short navigation, visible simulation disclosure, end-time information. |
| Profile, screenshot `image(3).png` | Configure reusable boundaries and understand the effect of changes. | Save profile; during a session, Save for next session. | Familiar field labels, clear profile name, explicit Guided-mode delay, save feedback. |

## 2. Prioritized critique

P0 means a potential task blocker or serious state misunderstanding. P1 affects everyday usability. P2 improves consistency or polish. Observed means visible in the supplied image. Hypothesis means runtime inspection is required.

| Location | Issue and evidence | User impact | Proposed change | Priority |
| --- | --- | --- | --- | --- |
| Profile screenshot, lower window | **Observed:** a large blank white area begins around the lower fifth of the capture and hides the form continuation. **Unknown:** whether this is application layout, capture clipping, or an overlay. | Configuration and Save may be obscured. Users cannot infer whether more controls exist. | Inspect the WPF root grid, clipping, overlays, and scroll extent. Use one expanding content area and a reachable editor footer; avoid a hardcoded content height. | P0, verify cause |
| Focus screenshot, active timer and setup form | **Observed:** saved-profile selection, a disabled Start focus, profile editing, and an active timer are present together. | Users have to infer which controls affect this session and which affect the next one. | Separate idle preparation from the active session surface. Keep active rules read-only; label edits as applying next time. | P1 |
| Active timer card | **Observed:** Request guided end, Cancel end request, Release/recover, and Reconnect are clustered with similar emphasis. | Troubleshooting and ending compete with the normal focus task. The inactive cancellation action adds noise. | Show Open study resources as the primary action. Show End early only in Guided mode; show Keep focusing only after an end request. Put contextual connection repair in a warning and recovery in a stable Help location. | P1 |
| Global simulation and connection information | **Observed:** simulation text is tiny above the application. The browser checkbox also exposes “alpha.” | Users may mistake a running timer for genuine blocking or overlook browser scope. | Retain an unmistakable prototype/simulation banner. Show actual per-profile coverage near the session summary. Move build labels to diagnostics in the installed app. | P1 |
| Restriction summary | **Observed:** “0 apps · 0 websites · 2 allowed hashtags” compresses distinct concepts. **Hypothesis:** “allowed hashtags” may not communicate whether matching permits or denies. | Users can misunderstand what will be restricted. Zero apps/sites is not necessarily an error if YouTube is configured. | Separate Apps, Websites, and YouTube summaries. Use “Block videos matching these hashtags” or “Approved videos only,” with a plain-language action preview. | P1 |
| Focus action label | **Observed:** “Request guided end” describes a mechanism rather than the user's intention. | Extra interpretation is needed before an important action. | Use “End early” with “Available after a 60-second wait.” During the wait show “Ending in …” and “Keep focusing.” | P1 |
| Profile editor | **Observed:** name, duration, and session mode occupy a long, undivided form; the scrollbar shows more content below. | Users may need to scan a long form to find specific boundaries. | Use Basics, Apps, Websites, YouTube, and Study links sections. Keep validation and save status consistent across them. | P1 |
| Typography | **Observed:** helper text and session metadata are much smaller than the headline. The captured image may also be scaled. | Critical instructions receive less visual weight than marketing copy. | Use 14–16 px body text, 12–13 px supporting text, and 13 px labels. Reserve the serif face for short headings and the timer. Verify physical readability at Windows scaling levels. | P1 |
| Forms and buttons | **Observed:** square native selects and softer rounded buttons/surfaces use different visual treatments. | The interface feels assembled from separate systems. | One field height, radius, border, padding, focus ring, and disabled treatment. Retain native control semantics. | P2 |
| Sidebar | **Observed:** the green navigation panel starts beneath the top status area and appears as a tall rounded island. | The global shell and page content have a less consistent alignment. | Use a continuous application rail, aligned content header, and one main content column. At narrow widths, reflow to labeled top navigation. | P2 |
| Bottom operational note | **Observed:** the user is told to keep the local host running for live expiry. | An implementation dependency appears in routine study instructions. It also signals that this build may not yet provide the planned independent service. | In a development build, retain an honest contextual warning. In the completed app, show service readiness; never claim independent expiry until integration tests pass. | P1 |
| Interaction and accessibility | Screenshots cannot establish tab order, focus visibility, assistive-technology output, hover behavior, persistence, or reduced-motion support. | These remain unverified risks rather than observed defects. | Implement semantic controls and state announcements, then test the actual WPF and extension surfaces with keyboard and assistive technology. | P1, hypothesis |

## 3. Visual direction

### Two suitable options

| Direction | Visual language | Strength | Trade-off |
| --- | --- | --- | --- |
| **Quiet study room — selected** | Warm paper, forest green, restrained pale green, editorial serif headings, familiar sans-serif controls, generous spacing. | Evolves the existing identity and makes an attention tool feel calm and personal. | Requires discipline: decorative language must never obscure restriction status. |
| Precision workspace | Graphite, cool white, cobalt accents, all-sans typography, compact controls, technical clarity. | Strong for a dense power-user utility and diagnostics. | More generic and more visually associated with administration than study. |

Select Quiet study room because this is an everyday ritual, not a monitoring console. The design should help the user commit to one task and then get out of the way.

### Recognizable details

1. **The session ticket:** a pale-green area beside setup contains the selected duration, estimated finish, commitment mode, and Start focus. It presents a clear agreement before the session begins. During focus, this becomes a dark, quiet timer surface with the same visual hierarchy.
2. **The three-line mark:** three simple vertical strokes identify LockIn without a padlock or a punitive visual metaphor. The mark stays still during focus. No pulsing logo, streak flame, mascot, confetti, or animated background is needed.

### Composition decisions

- Use a continuous 190 px desktop rail; 218 px on wide displays and 160 px on intermediate desktop widths.
- Give the main content 24–48 px horizontal breathing room, reduced to 18 px on narrow screens.
- Keep one dominant surface per task. Restriction summaries are ruled text groups, not another row of metric cards.
- Display profile selection and duration before secondary customization.
- Keep essential status outside tooltips and hover states.
- Use mostly opaque surfaces. No glass blur, backdrop filters, remote hero images, or decorative gradients.
- Restrict shadows to dialogs. Flat surfaces suit the product and avoid unnecessary visual depth.
- Acknowledge outcomes without equating elapsed session time with confirmed learning or attention.

### Current references and applied principles

Official guidance was reviewed on 29 September 2026. These references inform principles, not copied visual layouts.

- [Fluent 2 layout](https://fluent2.microsoft.design/layout): proximity, alignment, and reflow guide grouped session controls and the smaller-window layout.
- [Fluent 2 motion](https://fluent2.microsoft.design/motion): motion explains changes and relationships; transitions stay quick and localized. The focus surface avoids unrelated movement.
- [WCAG 2.2 target size](https://www.w3.org/WAI/WCAG22/Understanding/target-size-minimum.html): AA provides a 24 × 24 CSS pixel minimum with exceptions. LockIn uses a more comfortable 44 px target for primary controls and touch-oriented interactions.
- [WCAG 2.2 focus not obscured](https://www.w3.org/WAI/WCAG22/Understanding/focus-not-obscured-minimum.html): focus must remain visible; sticky surfaces must not cover the focused control.

The exact palette, typography pairing, and session ticket are design judgments. Their effect on trust and task completion requires user testing.

## 4. Main user journey

### First-run setup — specification

Use a short setup checklist, with optional scope clearly marked:

1. Explain the product in one sentence and distinguish simulation from real restrictions.
2. Check the Windows component and guide browser pairing. Show the actual browser/profile label and missing permissions.
3. Let the user choose apps, websites, or YouTube filtering. A narrower profile is valid.
4. Add useful resources and review the redirect destination.
5. Offer the one-minute demonstration and verify automatic release.
6. Save the profile. Offer Committed mode only after the real demonstration and recovery checks required by the engineering plan.

Setup must not silently install software, select all preset distractions, or claim other browser profiles are covered. This setup is specified, not implemented by the browser prototype.

### Everyday flow — demonstrated

| Step | User sees and does | Product response |
| --- | --- | --- |
| Enter | Focus page with a saved profile and a 25-minute default. | Show its selected boundaries and connection status. |
| Prepare | Select a profile, optionally name one task, choose a duration. | Update the session ticket and finish estimate. Custom duration accepts whole minutes from 1 to 480. |
| Review | Select Start focus. | Summarize app closure, websites, YouTube action, browser scope, mode, and expected finish. |
| Save work | If a selected app is already running, acknowledge the closure consequence. | Do not continue without confirmation. In production, use detected running apps and the service preflight result. |
| Begin | Select Begin countdown. | Show a cancelable 10-second countdown. No restrictions apply yet. |
| Focus | See the task, remaining time, exact finish time, and fixed revision. | Put Open study resources in the primary position. Profile edits affect the next session only. |
| End early, if Guided | Select End early. | Wait 60 seconds; show Keep focusing to cancel. Do not expose a direct ordinary end action in Committed mode. |
| Release | Deadline or authorized early end is reached. | Show Releasing, then completion only when cleanup is confirmed. Simulated cleanup failure leads to a recoverable pending state. |
| Finish | See outcome and elapsed session minutes. | Offer history or another session. Do not restart automatically or reopen closed distractions. |

Prototype-only Preview controls let reviewers advance simulated time and select connection/cleanup scenarios. They are visually separate from product controls and must not ship in an enforcement build.

## 5. Screen-by-screen redesign

### 5.1 Focus — idle

**Primary action:** Start focus.

Order: title → capability warning, if any → profile/goal/duration → session ticket → Apps/Websites/YouTube summaries → study resources.

- Heading: “Make room for one thing.”
- Profile selector uses saved names, not internal IDs.
- Goal is optional and limited to 120 characters in the prototype.
- Presets: 25, 45, 50, 60 minutes; Custom reveals a number input.
- Ticket shows duration, estimated finish including the start countdown, and the mode's consequence.
- Start focus leads to preflight. It does not immediately close anything.
- Missing capabilities explain why starting is unavailable and link to Connections. There is no silent desktop-only downgrade.
- Restriction groups open their relevant profile section.
- Study links open only after explicit user selection.

### 5.2 Preflight

**Primary action:** Begin countdown. **Secondary:** Back.

Use a focused page instead of nesting another layer of complex configuration in a modal. Include the profile, intention, duration, finish estimate, app-enforcement method, browser profile, exact redirect behavior, and commitment consequence.

The app label must say “Closes selected apps after launch” for the basic provider. Do not label it “Prevents launch.” Highlight already-running selected apps and the possibility of losing unsaved work. Committed mode requires a separate acknowledgment.

A readiness check in production uses the actual service, providers, permissions, revision, and cleanup readiness. A green display is not evidence of successful activation.

### 5.3 Countdown and activation

**Primary available action:** Cancel countdown.

Show one large number and the chosen task. The number is a timer, not a live region that announces every second. Announce the start and cancellation once. After zero, show an activation state until required providers acknowledge the exact policy. Canceling before activation applies nothing.

The prototype enters a simulated active state after rechecking sample connections. It does not implement the production prepare/commit protocol.

### 5.4 Active session

**Primary action:** Open study resources.

- One dominant timer, with the fixed goal and exact deadline.
- Display remaining time as time remaining, not “productive time.”
- A thin linear progress track provides secondary context, not a second competing dial.
- Show selected app/site counts and the frozen revision.
- Collapse the detailed boundary list; it remains accessible without hovering.
- In Guided mode, show End early with its 60-second explanation.
- In Committed mode, omit ordinary early ending. Keep Help & recovery discoverable.
- A coverage warning does not reset the timer or pretend all other providers have stopped.
- Do not let navigation to Profiles change the active snapshot.
- No looping motion, confetti, bouncing numbers, or continual performance counters.

### 5.5 Ending, completion, and cleanup pending

**Normal completion action:** Set up another session. **Cleanup-pending action:** Retry cleanup.

Releasing uses a concise busy status and prevents another session from starting. Completion summarizes duration and outcome without an invented score. Keep the product's tone neutral for early endings and recovery.

If cleanup is unconfirmed, say which component needs attention. Do not display “All restrictions removed.” Preserve the release marker, retry through the real service, and provide recovery instructions. The prototype's retry and recovery branches simulate success explicitly.

### 5.6 Profiles and Basics

**List primary action:** New profile. **Editor primary action:** Save profile.

Each profile presents name, purpose, duration, mode, and brief restriction counts. Available actions are Use profile, Edit, and Duplicate. In-session selection means “Use next time.”

Editor sections are labeled buttons, not icon-only tabs: Basics, Apps, Websites, YouTube, Study links. Native buttons remain in a familiar tab order. Unsaved navigation asks whether to keep editing or discard.

Basics contains name, default duration, and mode. Validation routes back to the affected section and focuses its field. A profile with no effective restrictions can be saved only after a timer-only acknowledgment. Production archive/restore behavior is specified below; it is not simulated in the list.

### 5.7 Apps

**Primary action:** Save profile after selecting identities.

Search the app list, choose checkboxes, and show executable identity beneath each app. Distinguish Steam from each selected game. In production, add Running, Installed, and Browse .exe sources; verify identity and show Missing/Changed/Needs review states. A process basename is not sufficient identity.

The prototype searches three sample apps. It does not scan the filesystem, open an executable picker, close a process, or assert that the sample paths exist.

### 5.8 Websites

**Local action:** Add website. **Page action:** Save profile.

Use a domain input with an includes-subdomains checkbox. After adding, show normalized host and scope. Reject malformed input and duplicates in place. Removal has Undo.

Production advanced options expand per rule: exact path or path prefix, allow exception, and a URL test field showing the normalized match. Domain boundaries must distinguish `facebook.com` from `facebook.com.example.org`. Preserve path case rules and authentication dependencies from the engineering plan.

The prototype supports domain rules only and states this limitation. Redirect conflicts are validated before saving or starting.

### 5.9 YouTube

**Local primary action:** Test this rule. **Page action:** Save profile.

Group settings in this order:

1. What is allowed: distraction filtering or exact approved videos only.
2. Matching evidence: source and explicit terms; distinguish exact hashtags from title words/phrases.
3. Unknown metadata policy: reduced coverage versus redirect after timeout. Approved-only never treats unknown identity as approval.
4. Exact video approvals and explicit blocks, inside a disclosure section.
5. Return destination and tab action, with a sentence describing the outcome.
6. Optional surface controls: Home recommendations, Shorts, related videos, comments, end screens, autoplay.
7. A rule tester using entered evidence and an explained decision.

Preserve all three tab behaviors: use this tab; switch to an existing destination tab; open one replacement tab. Always explain source neutralization. The approved lesson destination needs an actual approved ID. Custom destinations require HTTPS and cannot conflict with blocking rules.

The prototype demonstrates hashtags/title matching, exact video deny/allow precedence, missing evidence, return destinations, tab choices, and surface settings. It does not fetch video metadata, manipulate tabs, or represent creator-tag availability. Broader channel approvals remain an advanced capability behind the original adapter gate.

### 5.10 Study links

**Local action:** Add study link. **Page action:** Save profile.

Require a label and valid HTTPS address. Reject credential-bearing or executable URLs. Removing a link supports Undo. Adding a study link alone does not grant a YouTube exception.

Production may offer “Open once when this session starts,” off by default unless chosen. Do not repeatedly reopen a resource after the user closes it. The prototype opens resources only through explicit links.

### 5.11 History

**Empty-state action:** Set up a session.

Start empty instead of inventing productivity statistics. After a simulated session, show name, duration, finish time, and outcome. Details reveal the revision and identify the simulated data source. No time-saved claim, attention score, or pressure streak.

In production, use local date grouping and an outcome filter when the history has enough rows to need them. Aggregate attempts must come from deduplicated events. Zero attempts is not a failed session. Detailed browsing events remain off by default.

### 5.12 Connections

**Primary action:** Check connection; production uses the appropriate contextual Connect/Repair action.

Separate desktop timer/recovery from Chrome's named browser profile. Show other profiles/private windows as outside coverage unless individually verified. Explain stale cache and missing permissions without equating disconnect with tampering.

The prototype has a finite simulated Checking state and then restores sample readiness. Production must replace it with actual results and a real failure path. Keep protocol/build identifiers in an expanded diagnostics section.

### 5.13 Settings and recovery

Settings contains system/light/dark appearance, motion preference, and local history controls. Reduced system motion always takes priority. Clearing prototype history offers confirmation and Undo. The installed app additionally needs the original privacy retention, notifications, startup, and sanitized export settings.

Recovery is accessible in a stable footer and through contextual errors. Its dialog states whether a session will end and what it will release. Require a deliberate confirmation, but do not make it difficult to use. Record Recovered rather than framing it as failure.

### 5.14 Browser surfaces — specification

| Surface | Composition | Primary action / behavior |
| --- | --- | --- |
| Extension popup | Compact profile, remaining time, exact finish, coverage line, study links. | Open dashboard; no duplicated full editor. |
| Blocked website | Quiet reason, session goal, finish time, useful resources. | Open a study resource. After verified expiry, offer Retry access. |
| Focus Page | Goal and selected links; optional small timer. | Open the chosen resource. Avoid another endless feed. |
| YouTube redirect notice | “Returned to Home: matched #games.” | Automatic action; no mandatory dismissal. Deduplicate announcements. |
| Missing metadata | Name only the sources actually unavailable and state the chosen policy. | Link to coverage details; never mark unknown evidence as clean. |

These surfaces inherit tokens and wording. They are specified, not separately rendered in this prototype.

## 6. Interaction states

| State | Visual treatment | Behavior and accessibility |
| --- | --- | --- |
| Default | Opaque surface, clear label, stable dimensions. | Native semantic control and logical source order. |
| Hover | Small background change; no newly appearing essential controls. | Same action remains available to keyboard and touch users. |
| Keyboard focus | 3 px blue outline with 3 px separation. | Never remove focus merely because a control is hovered. Keep it visible in dialogs and scrolling content. |
| Pressed | At most 1 px downward translation. | Immediate feedback; no delay before command handling. Reduced motion removes transition effects. |
| Selected | Filled or tinted background plus border/weight and programmatic state. | Use aria-current for page navigation and aria-pressed for section/duration buttons. |
| Disabled | Reduced emphasis; retain readable explanation nearby. | Native disabled state. Do not make the only error explanation a tooltip on a disabled button. |
| Loading | State text such as Checking or Releasing with aria-busy/status. | Do not fabricate completion. Prevent duplicate activation. |
| Empty | Short explanation and one practical next action. | History starts empty; missing study links invite adding one. |
| Success | Text acknowledgment and a quiet, finite entrance. | Polite status announcement. Profile-save messages clarify next-session scope. |
| Validation | Field-linked message and aria-invalid; focus affected field. | Preserve draft values; explain a fix. Duration, domain, HTTPS, ID, and destination conflicts have specific messages. |
| Runtime error | Plain-language cause, affected coverage, and repair action. | Critical alert once, not on every timer tick. Preserve the original deadline and useful editing. |
| Undo | Persistent inline action until dismissed or superseded. | Returns removed draft items or cleared preview history. Do not undo service-owned active restrictions through this mechanism. |

## 7. Motion design

Motion is functional and finite. It should make state changes understandable, then disappear. Never make restrictions, cleanup, or accessibility depend on an animation completing.

### Motion tokens

| Token | Value | Use |
| --- | --- | --- |
| Fast | 120 ms | Button background/color and press transitions. |
| Count change | 140 ms | Local countdown-number feedback. |
| Feedback | 180 ms | Save/undo notice entrance. |
| State change | 240 ms | Page/section and dialog entrance. |
| Progress | 250 ms, linear | Small changes to the secondary progress track. |
| Easing | `cubic-bezier(.2,.8,.2,1)` | Quick settle without bounce or overshoot. |
| Page displacement | 6 px downward-to-rest | Shallow entrance, not a large lateral slide. |
| Feedback displacement | -4 px to rest | Local notice arrival. |
| Countdown scale | 0.97 → 1 | Gentle emphasis on the changed number. |
| Reduced motion | Zero transforms/animations/transitions | Immediate state update; all information and controls retained. |

### Choreography and purpose

| Trigger | Motion | Purpose | Limit |
| --- | --- | --- | --- |
| Navigate to a different screen | Main content settles 6 px over 240 ms with opacity. | Show a change of context while preserving the stable shell. | No animation when merely editing a field or rerendering the same view. |
| Open a profile section | Same shallow entrance. | Connect the selected section to its new contents. | No elaborate staggering of form controls. |
| Select a duration/profile | Control color/selected state changes. | Immediate response to input. | Do not animate or count up the selected duration. |
| Start countdown | Enter the countdown surface, then locally emphasize changed digits. | Make the short cancelable transition clear. | No flashing, shake, sound, or full-screen pulse. |
| Countdown reaches active | Enter the stable focus surface once. | Confirm that the state changed. | Production waits for activation acknowledgment first. |
| Active timer ticks | Text changes in tabular numerals. | Convey remaining time without inviting attention. | No rolling digits or repeated scale. |
| Progress changes | Thin bar updates linearly. | Secondary temporal context. | Do not use animated gradients or a glowing trail. |
| Profile saved / item removed | Notice enters once over 180 ms. | Confirm cause and effect and expose Undo when relevant. | The message does not auto-dismiss before it can be read. |
| Dialog opens | 6 px entrance over 240 ms. | Preserve a sense of continuity. | Native modal behavior takes effect immediately; closing is immediate. |
| Validation fails | Static error message. | Explain the fix calmly. | No shake or punitive motion. |
| Completion | Completion surface settles once. | Acknowledge the transition without interruptive celebration. | No confetti, audio, or autoplay. |
| Connection/cleanup failure | Static persistent warning. | Keep the problem visible and actionable. | No pulsing warning or flashing red border. |

### Reduced motion and implementation

- Honor `prefers-reduced-motion: reduce` and the in-product Reduce motion selection.
- A system preference change cancels current Web Animations. CSS transitions also stop.
- System reduced motion cannot be overridden by the app's normal-motion preference.
- No libraries or remote assets are required. Use CSS transitions and short native Web Animations for opacity/transform.
- The main content animates only when its view key changes. It does not animate on timer updates or every form edit.
- Native timers and service events control state. Never use animationend as authority for activation or release.
- Animate opacity and transforms, not layout-heavy heights or repeated shadows. The tiny progress width change is bounded to its own track.
- For WPF, translate these recipes to short opacity/TranslateTransform/ScaleTransform storyboards. Keep business state in view models and stop storyboards when reduced motion is enabled. Verify against the actual WPF accessibility/system-preference implementation.
- This motion layer is implemented in the browser prototype, but its rendered smoothness could not be inspected in this environment. Treat timings as a starting point for user testing.

## 8. Design tokens

### Color

| Role | Light | Dark | Usage |
| --- | --- | --- | --- |
| Canvas | `#F7F7F0` | `#111B17` | Application background. |
| Surface | `#FFFFFF` | `#1C2A22` | Forms, panels, dialogs. |
| Subtle surface | `#EEEFE6` | `#27362B` | Quiet grouped controls. |
| Primary text | `#20392C` | `#F0F1E8` | Headings and body text. |
| Secondary text | `#586657` | `#B4C2B6` | Supporting text with readable contrast. |
| Primary action | `#254B36` | `#DBE8AE` | One main action per task. |
| On primary | `#FFFFFF` | `#183426` | Primary button content. |
| Selected/ticket surface | `#E5EDCE` | `#354B2D` | Session preview and selected context. |
| Decorative separator | `#D4D9CD` | `#4C6050` | Nonessential dividing rules. |
| Control boundary | `#82917B` | `#819686` | Inputs and controls that need an identifiable outline. |
| Focus | `#175ACA` | `#A5C3FF` | 3 px outer focus indicator. |
| Warning surface/text | `#FFF1D6` / `#785017` | `#44341C` / `#FFDC91` | Degraded coverage and preflight caution. |
| Error surface/text | `#FFF0ED` / `#A02B20` | `#442824` / `#FFC5B9` | Validation and unresolved cleanup. |
| Identity rail | `#203A2D` | `#203A2D` | Stable brand anchor in both themes. |
| Rail selected surface/text | `#E1EBBD` / `#203A2D` | Same | Recognizable current destination. |

Do not substitute decorative separator colors for input boundaries. Status always includes a text label or symbol, not only a color.

### Type, spacing, and shape

| Category | Values | Rules |
| --- | --- | --- |
| Body family | Segoe UI, Arial, sans-serif | Native-feeling Windows controls; no remote font dependency. |
| Editorial family | Georgia, Times New Roman, serif | Short page headings and duration; not dense forms. |
| Page heading | 32–46 px desktop, 36 px narrow | Regular weight, ~1.12 line height. Wrap naturally. |
| Section heading | 20 px / 1.3 / 600 | One coherent hierarchy. |
| Body | 15 px / 1.5 | Use 14–16 px for functional text. |
| Labels | 13 px / 600 | Persistent, visible labels. |
| Supporting text | 12–13 px / 400 | Never hide critical consequences in tiny captions. |
| Timer | 55–94 px depending on space | Tabular numerals; reserve stable width. |
| Spacing | 4, 8, 12, 16, 20, 24, 32, 48 px | Use proximity to group related elements. |
| Control target | 44 px minimum for main controls | 48 px check rows on narrow layouts; secondary compact desktop controls may be 32–42 px with adequate spacing. |
| Input/button radius | 8 px | Familiar controls; avoid turning every element into a pill. |
| Panel radius | 12 px | Settings and editor panels. |
| Session/dialog radius | 16 px | Reserved for main task surfaces. |
| Border | 1 px; selected duration 2 px | Geometry remains stable when state changes. |
| Dialog shadow | `0 16px 60px #10231F33` | No general card-shadow system. |

## 9. Responsive behavior and accessibility

### Window sizes

| Width | Shell | Content and action behavior |
| --- | --- | --- |
| ≥1250 px | 218 px rail, 48 px content padding | Two-part session card; reading area capped around 1000 px. |
| 901–1249 px | 190 px rail, 36 px padding | Session setup and ticket remain adjacent. |
| 741–900 px | 160 px rail, 24 px padding | Reduce gaps before shrinking text; keep labels visible. |
| 521–740 px | Top brand and labeled navigation | No icon-only rail. Session areas can remain side by side while they fit. |
| ≤520 px | Wrapping top navigation, 18 px padding | Setup stacks above the ticket. Restriction summaries and resource links stack. Form rows and actions wrap. |
| 320 px / equivalent zoomed layout | Single column | No horizontal page scroll intended; dialogs fit inside viewport; long paths and names wrap. Must be visually verified. |

The narrow layout is an adaptation of the UI, not a claim of iOS/Android enforcement support. Production WPF uses the same hierarchy in device-independent units and must be checked at 100%, 150%, and 200% Windows scaling.

### Accessibility requirements

- Target WCAG 2.2 AA for browser surfaces; validate the corresponding native application behavior as well.
- Use semantic main, navigation, headings, forms, labels, buttons, and dialogs.
- Keep real buttons for interactive summaries. Do not use click handlers on decorative divs.
- Preserve source order and avoid positive tabindex.
- Support Tab/Shift+Tab, Enter/Space, native select keyboard behavior, and Escape to dismiss non-destructive dialogs.
- Restore or retain focus when sections rerender. Do not move focus every second when the timer changes.
- Announce state transitions and concise results politely. Do not announce every timer tick.
- Focus an invalid field and connect its message with aria-describedby; keep an error summary for cross-section conflicts.
- A failure message must include its recovery path. Simulated ready status must not be mistaken for actual installation.
- Avoid fixed bottom bars that can cover the last field at high zoom. The prototype uses normal document flow.
- Color changes always accompany labels, selected states, or symbols.
- Support long profile names, localized copy, URLs, and executable paths with wrapping rather than clipping essential values.
- No motion is needed to understand the next action.
- Full screen-reader, forced-colors, zoom, actual target geometry, and WPF UI Automation tests remain required before release.

### Calculated contrast samples

These ratios were calculated from the exact specified sRGB values. They do not constitute a complete rendered accessibility audit.

| Pair | Ratio |
| --- | ---: |
| Light body / canvas | 11.60:1 |
| Light secondary / canvas | 5.65:1 |
| Light secondary / session ticket | 5.02:1 |
| Light primary button text / fill | 9.82:1 |
| Dark body / canvas | 15.47:1 |
| Dark secondary / subtle surface | 6.87:1 |
| Dark primary button text / fill | 10.38:1 |
| Light warning text / warning surface | 6.36:1 |
| Light error text / error surface | 6.62:1 |
| Light control outline / white surface | 3.34:1 |
| Dark control outline / dark surface | 4.73:1 |

## 10. Engineering handoff

### Preserve the stack and authority model

The original plan specifies a .NET/WPF/MVVM desktop UI, independent Windows service, native bridge, SQLite, and a TypeScript Manifest V3 extension. Keep that architecture. The single-file browser prototype is a design reference, not a production migration plan.

| UI concern | Production binding |
| --- | --- |
| Profile list/editor | ListProfiles/GetProfile and SaveProfileDraft; service validates future revisions. |
| Session preparation | PrepareSession readiness result, conflicts, detected running apps, and short-lived candidate. |
| Countdown and activation | CancelPreparation/CommitSession; exact lifecycle state and required acknowledgments. |
| Active timer | Service-owned deadline and immutable snapshot. UI derives display only. |
| Guided early end | RequestGuidedEnd/CancelGuidedEnd, not local UI unlock. |
| Connections | Actual capabilities, paired browser identity, permissions, and policy acknowledgment. |
| Recovery | Authenticated ReleaseOwnedArtifacts and verified cleanup outcome. |
| YouTube preview | Shared deterministic fixtures and matcher semantics across C# and TypeScript. |
| History/privacy | Service-owned records, configured retention, no default detailed browsing log. |

Suggested component boundaries: ApplicationShell, FocusSetup, SessionTicket, RestrictionSummary, SessionReview, CountdownView, ActiveSessionView, ReleaseStatus, ProfileEditor, AppIdentityList, WebsiteRuleList, YouTubeRuleEditor, RulePreview, ConnectionStatus, RecoveryDialog, and SessionHistory.

Use resource dictionaries/styles in WPF and equivalent CSS variables in the extension. Never mix privileged operations into animation completion handlers or UI code-behind. A reconnect result updates coverage, not the stored deadline.

### Complete capabilities that are specified but not simulated

- Real first-run installation, elevation, pairing, permissions, and the one-minute verification.
- Executable discovery, identity changes, and file browsing.
- Website path rules and narrow allow exceptions.
- Creator-tag/description metadata adapters, channel-ID features when gated, and live YouTube extraction.
- Actual tab actions, source neutralization, deduplication, and loop fallback.
- Service-owned persistence across window closure, sleep, reboot, and crash.
- Profile archive/restore with confirmation; no deletion of active snapshots.
- Production history retention, diagnostic export, startup, and notification settings.
- Extension popup, blocked page, Focus Page, and context notices.
- Activation compensation, independent expiry, uninstall, and verified real cleanup.

Do not show these as available merely because this specification describes them. Keep unavailable controls hidden or accurately labeled by capability, following the original release gates.

### Performance

The prototype has no runtime network calls, analytics, external fonts, image assets, or animation dependencies. Its timer refreshes a small number of DOM nodes; it does not rerender the page every tick. Loading it does not inspect browser tabs or processes.

Production should render on state changes, debounce searches, and animate only cheap properties. Keep the original performance targets and measure them on named Windows/browser hardware. Small prototype file size is not evidence that process enforcement or metadata extraction meets those targets.

## 11. Prototype walkthrough

1. Open `lockin_redesign_prototype.html` in a modern browser, or use the inline preview.
2. Set a goal and duration. Start focus, review the app-closure acknowledgment, and Begin countdown.
3. Wait ten seconds or use Preview controls → Advance 60 seconds. Only simulated time changes.
4. Try End early → Keep focusing. Alternatively, Jump to finish demonstrates release and completion.
5. Edit a profile during an active session, save it, and return to Focus. The current snapshot remains fixed.
6. In YouTube, test `#games`, then `#gaming` with only the `games` rule, exact approval/block IDs, and missing metadata.
7. Use Preview controls to try a browser disconnect or cleanup failure. Follow the recovery path.
8. Open Settings & appearance to change theme or reduce motion.

All profile/history changes are in-memory and reset on reload. Resource links may open actual public websites only when clicked. No real desktop application, website, or video is restricted.

## 12. Verification and remaining work

The final verification record is in `lockin_redesign_verification.md`. It distinguishes static checks, isolated logic checks, attempted rendering, and tests still pending.

The provided screenshots were visually reviewed. New browser rendering was attempted with local Playwright and the browser preview. The local browser binary was unavailable; the attempted official download did not produce a usable archive. The remote preview blocked the localhost URL. Consequently, new desktop/mobile screenshots, actual keyboard traversal, and observed animation smoothness were **not verified**. No full WCAG conformance claim is made.

Before production acceptance, run the original plan's UX-01–UX-04 and relevant SES/WEB/YT cases against the real application. Also verify 320, 390, 768, 1024, and 1440 px browser layouts; 200%/400% zoom; light/dark/forced-colors; screen-reader naming; modal focus; and reduced-motion behavior.

### Usability validation

Test with the owner and several intended student users. Ask them to start a saved session, explain what will be restricted, change a YouTube destination, recover from a disconnected browser, and distinguish next-session edits from active rules. Record completion, confusion, and errors. Compare against the supplied interface without assuming the redesign already improves measured performance.

### Most important before-and-after changes

| Before | After | Expected benefit, to validate |
| --- | --- | --- |
| Setup and active controls compete. | Separate preparation and active states. | Faster understanding of what can change now. |
| Recovery/reconnect resemble ordinary actions. | Contextual repair and stable Help & recovery. | Fewer accidental or confused exits. |
| Restrictions are compressed into one small sentence. | Dedicated summaries and an explicit preflight. | Clearer expectations before app closure or redirection. |
| Long profile form with apparent clipping. | Sectioned editor and natural scrolling. | Easier configuration and reachable save action. |
| Tiny technical status text. | Readable capability and simulation disclosure. | More accurate trust in the product's state. |
| No motion behavior can be inferred from screenshots. | Finite purposeful transitions, a still focus screen, and reduced motion. | Clearer state changes without adding distraction. |

