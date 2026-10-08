# UX refactor plan — 2026-10-01

## Objective and constraints

Make starting a saved session understandable without remembering setup instructions. Preserve host authority, exact allow-tag semantics, explicit permission consent, review/countdown, immutable active rules and accessible recovery. Retain WPF and the quiet forest/paper design; these changes do not claim production readiness.

## Research and application

| Evidence | Decision for LockIn |
| --- | --- |
| [Microsoft navigation basics](https://learn.microsoft.com/en-us/windows/apps/design/basics/navigation-basics): simplicity, consistency and shallow structure | Retain stable labeled destinations; bring readiness into Focus rather than requiring page hopping. |
| [NN/g progressive disclosure](https://www.nngroup.com/articles/progressive-disclosure/): reveal secondary complexity when needed | Collapse detailed boundaries/maintenance; keep duration and the current primary action directly available. |
| [NN/g recognition and recall](https://www.nngroup.com/articles/recognition-and-recall/): visible cues reduce memory requirements | Show browser readiness and the next step inline; do not require remembered Send/Refresh steps. |
| [Fluent motion](https://fluent2.microsoft.design/motion): brief fades for top-level changes, reduced motion | Replace whole-page translation with a short fade; keep timer digits still. |
| [W3C focus visibility](https://www.w3.org/WAI/WCAG22/Understanding/focus-not-obscured-minimum.html) | Keep save/recovery reachable, label every control, inspect keyboard focus and narrow layouts. Web guidance informs desktop practice; no WCAG certification is claimed. |

## Audit: observed problems

1. Focus repeats five duration buttons plus Custom, three rule shortcuts, edit/help actions and a full boundary block. The main task competes with secondary actions.
2. Browser readiness is mainly on Connections; an idle timer can look ready while pairing or permissions are missing.
3. Connection setup remains visible even after success; management and the happy path lack clear separation.
4. Presentation decisions are embedded in a large update method and mix independent states.
5. Similar cards repeat inline border/padding values. Blank feedback space and long instructions reduce usable room.

## Journeys and acceptance criteria

- **Prepared session:** select saved profile, optionally adjust duration in place, Start → Review → Begin countdown. No compulsory trip to Profiles or Connections, no manual Send/Refresh.
- **Browser not ready:** Focus states exactly what is missing and offers a contextual setup action. It never claims protection from enrollment alone.
- **Active session:** timer/resources dominate; editable setup disappears; details are expandable; release/recovery remain available.
- **Compact window:** labels wrap; duration and save remain usable; no horizontal page overflow. Recovery remains in stable Help navigation.
- **Motion:** finite <=240 ms; no top-level sliding or timer-digit animation; reduced motion suppresses animation.

## Implementation sequence

1. **Structure and state:** derive readiness in one presentation helper from existing host/browser state. No new authoritative cache or enforcement logic.
2. **Focus layout:** one editable duration selector, contextual readiness card, expandable boundaries, one edit entry; retain review.
3. **Connection hierarchy and tokens:** shared surface/secondary-action styles; setup vs connected presentation; management kept in an expander.
4. **Verification:** compile, desktop design/editor/session/recovery checks as affected, unit regression; inspect screenshots at wide/compact sizes. Record actual evidence and remaining gaps. Tests verify outcomes, not exact styling implementation.

## Rollout boundaries

No framework migration, new privileged service, automatic permission granting, removal of preflight or broad visual rewrite of unrelated enforcement pages. No repeated speculative registry changes. Installed-Brave pairing evidence from handoff 14 is the connection baseline.

## Evidence log

Implementation and results will be recorded in the handoff 15 report. This plan is written before the refactor; layout corrections during verification must address a concrete failed acceptance criterion.

Completed the planned desktop pass: [handoff 15 report](progress/2026-10-01-15-systematic-ux-refactor.md). Compact screenshot inspection exposed excessive vertical stacking; the layout now places profile/duration side by side and verifies Start is not clipped at 640×600. Broader scaling/accessibility acceptance remains open.
