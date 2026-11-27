# Frontend audit and direction — handoff 16

## Product and architecture

LockIn helps a student or independent worker configure a focus session and return to study resources. WPF owns Focus, Profiles, History, Connections and Help; MV3 HTML/CSS/TypeScript owns pairing/status, blocked sites and the Focus Page. Host state remains authoritative. App enforcement is simulated; browser alpha is explicitly selected and requires permission. Preserve exact allowed hashtags, review/countdown and evidence-based recovery.

Existing components: QuietStudy.xaml templates for buttons, inputs, tabs, surfaces and focus rings; shared popup.css for browser surfaces. Typography uses Segoe UI and Georgia. Spacing mostly follows 4/8/12/16/24 but duplicated inline styles diverge. Desktop adapts its rail at 760px and Focus composition at 900px, with native minimum 620×540. Browser popup has a 380px width; destination pages are fluid. There is no mobile native app or web dashboard.

| Location | Problem | User impact | Proposed improvement | Priority |
| --- | --- | --- | --- | --- |
| Shell/Focus | Oversized slogans, repeated help, poetic page titles | Task hierarchy diluted | Compact branding, task titles, retain timer emphasis | High |
| Profiles | YouTube exceptions/action/destination compete with primary tags; blank errors consume space | Setup looks longer than necessary | Group optional controls, concise visible matching semantics, collapsible help | High |
| History | Tall rounded record cards and always-open multirow filters | Poor scan density | Journal rows, concise dates, collapsed filters, meaningful empty/no-results states | High |
| Connections | Repeated instructions; internal terms | More reading than action | Short setup sequence and secondary coverage details | Medium |
| Help | Three visually competing cards | Recovery hard to distinguish | Flat sections, clear recovery heading and retained critical instructions | High |
| Popup | Setup and idle timer repeat; actions lack consistent busy states; raw error dominates | Weak feedback and uncertain next step | State-led session panel, compact pairing form, actionable error region and details | High |
| Focus/blocked page | Link nodes rebuilt every refresh; refresh rejects uncaught; only one paragraph of status | Focus can disappear and failures stay unclear | Stable resource list, visible errors and loading feedback, separate deadline/status | High |
| Shared styles | Repeated radii/colors; default tiny expander targets; incomplete focus styles in browser | Inconsistency and keyboard/touch friction | Tokens, 44px targets, shared sections, focus/disabled/pressed states | Medium |

## Direction before implementation

- **Visual:** study journal, forest ink/warm paper, serif accents at modest scale. No extra decorative imagery or icon dependency.
- **Information:** task labels first; optional filters, exceptions and help behind labeled disclosure. Critical scope, permission and recovery consequences remain visible where decisions occur.
- **Layout:** flat form sections and history rows; reserved tinted session panel; compact brand/navigation.
- **Interaction:** explicit busy/error feedback, stable DOM during polling, keyboard-visible focus, finite fades honoring reduced motion. No animation drives enforcement.
- **Responsive:** native desktop from 620px minimum; compact toolbar/editor at narrow sizes. Web popup at 320/375px; destination pages at 320/375/768/1024/1280/1440px. Desktop UI is not misrepresented as a mobile app.

## Verification

Build and existing functional tests; extend UI checks for disclosures and browser layout/focus/error behavior. Inspect actual screenshots. Keep owner processes and OS/browser policy untouched. Record results and remaining gaps in handoff 16.
