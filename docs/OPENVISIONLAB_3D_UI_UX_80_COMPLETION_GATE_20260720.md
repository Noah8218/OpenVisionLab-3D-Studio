# OpenVisionLab 3D UI/UX 80% Completion Gate

Updated: 2026-07-20
Status: Active product-delivery priority

## Owner decision

UI/UX is the current delivery priority. Do not start a new inspection
algorithm slice until this gate has at least `80/100` accepted points. Existing
algorithm behavior may be preserved and used for UI smoke evidence, but no new
tool, solver, measurement, or execution adapter is in scope during this gate.

The product remains an explainable, local 3D inspection recipe workbench. The
UI must make the current source, selected step, input/output route, required
action, result state, and evidence understandable before it adds more numeric
capability.

## Scorecard

| Area | Points | Acceptance evidence |
| --- | ---: | --- |
| Workbench information hierarchy | 20 | A first-time operator can identify source, selected step, input/output, state, and next action without opening a secondary pane. |
| Visual system and 1920 layout | 20 | Main Workbench, dock panes, windows, controls, selected/error/disabled states, and typography use one OpenVisionLab visual system at `1920 x 1080`; `1280 x 760` has no meaningful clipping. |
| Docking and window behavior | 15 | Every required view docks/floats reliably; Recipe Manager and each Tool Lab are single-instance, custom-title windows with a clear return path. |
| Recipe and tool workflow | 20 | Navigator tree is primary; typed `Input -> Output` is legible; Tool Labs separate input, parameters, output, evidence, Preview, and Publish without automatic execution. |
| Feedback and accessibility | 15 | Empty, blocked, warning, preview, stale, published, and saved states are distinguishable with text, color, icon, tooltip, and automation names. |
| Evidence and visual polish | 10 | Fresh current-build captures exist for main Workbench, Recipe Manager, each Tool Lab, and docked comparison panes; labels, overflow, contrast, and spacing are reviewed. |

`80%` is not a subjective visual estimate. It means accepted evidence totals at
least `80` points, with no failure in the explicit Preview/Publish, source
immutability, docking, or accessibility boundaries.

## Delivery order

1. **P0 Context and hierarchy** - consolidate selected-step context in the
   Workbench, simplify competing status surfaces, and make the next operator
   action visible.
2. **P1 Visual consistency** - remove remaining default-control appearance,
   normalize panel density, form labels, empty states, disabled states, and
   responsive behavior at the two reference sizes.
3. **P2 Recipe and tool navigation** - make tree-first input/output routing,
   step state, Tool Lab entry, and compare evidence easy to scan without
   creating a writable graph editor.
4. **P3 Tool-Lab and review polish** - give every existing Tool Lab the same
   input/parameters/output/evidence rhythm and make docked review panes useful
   at normal operator density.
5. **P4 Acceptance review** - capture current-build screens, review against
   this scorecard, record the accepted points, and ask the owner whether the
   UI gate is complete before restarting algorithm work.

## P0 first slice - selected-step work context

The first change is a display-only Workbench context bar above the Viewer. It
shows the selected step, routed `Input / Output`, typed-adapter state, and the
current parameter/action guidance in one place. When no step is selected, it
states the next navigation action.

It changes no recipe, source, Viewer state, Preview, Run, Publish, or tool
execution rule.

## Guardrails

- Preserve explicit Preview, Publish, and Run commands. UI guidance never runs
  a tool or edits a recipe.
- Preserve all dockable panes and custom title views.
- Use OpenVisionLab's own light/navy/teal system; do not copy commercial pane
  positions, color systems, assets, or wording.
- Keep labels, tooltips, and automation names when icons or color convey
  state.
- Capture a fresh before/after current-build screenshot for every visible UI
  slice and retain the actual verification reports.

## Current baseline

The pre-P0 current-build `1920 x 1080` capture is
`artifacts/ui/20260720-workbench-ui-context/before-workbench-1920.png`.
It is a baseline for hierarchy review, not an acceptance claim for the 80%
gate.

## P0 first-slice evidence - 2026-07-20

Status: Complete (P0 selected-step context slice only)

The Workbench now keeps the selected step's name, routed `Input / Output`,
parameter/action guidance, and typed-adapter readiness together immediately
above the Viewer. The empty state says what to do next. The Artifact Registry
also preserves an existing Height Difference Edge output identity while its
state changes between `Preview`, `Published`, and `Stale`; it no longer
displays a completed Preview as merely `Declared`.

| Acceptance criterion | Evidence |
| --- | --- |
| Empty Workbench gives one clear next action | `after-workbench-1920.png` shows `No recipe step selected` and the Toolbox navigation instruction. |
| A selected step presents route and status without changing recipe state | `after-workbench-selected-filter-1920.png` shows Filter `Input`, `Output`, guidance, and `Typed adapter ready`; no Preview or Publish was invoked. |
| Preview output identity is visible in the route tree | `navigator.txt` records `Edge Preview ... state=Preview` with a SHA-256 identity; changing a parameter records `state=Stale` with the same identity. |
| Existing interaction contracts remain intact | Current build passed docking `19/19`, teaching `16/16`, Edge workbench `11/11`, and Artifact Navigator `9/9`. |

Fresh current-build captures:

- Before: `artifacts/ui/20260720-workbench-ui-context/before-workbench-1920.png`
- After, empty state: `artifacts/ui/20260720-workbench-ui-context/after-workbench-1920.png`
- After, selected Filter step: `artifacts/ui/20260720-workbench-ui-context/after-workbench-selected-filter-1920.png`

Verification reports are under
`artifacts/verification/20260720-workbench-ui-context/`.

Boundary: this completes one P0 slice, not the global `80/100` UI/UX gate.
No new inspection algorithm was added.

## P1 first-slice evidence - 2026-07-20

Status: Complete (Workbench header density only)

The primary header keeps Recipe Manager visible, groups the four existing
Tool Lab windows under one icon-and-text `Tool Labs` command, and preserves
Advanced layout as a separate explicit choice. This removes the compact-width
competition between secondary Tool Lab shortcuts and the active Workbench
context. The grouped menu retains text, icons, tooltips, and automation names
for every Tool Lab; it does not create, edit, Preview, Publish, or Run a
recipe merely by opening the menu.

| Acceptance criterion | Evidence |
| --- | --- |
| Header commands do not overlap at the two review sizes | Fresh `before` and `after` captures at `1280 x 760` and `1920 x 1080` show a separate Recipe Manager, Tool Labs, and Advanced layout command. |
| Existing Tool Lab navigation remains usable | Current UI Automation invoked `ToolLabsMenu`, selected `OpenFilterToolLabButton`, and found `Filter Tool Lab | OpenVisionLab 3D Studio`. |
| No algorithm or recipe lifecycle behavior changed | The change is header composition and a ContextMenu opener only; Preview/Publish/Run bindings are unchanged. |

Fresh current-build captures:

- Before 1280: `artifacts/ui/20260720-workbench-p1-1280/before-workbench-selected-filter-1280.png`
- After 1280: `artifacts/ui/20260720-workbench-p1-1280/after-workbench-selected-filter-1280.png`
- Before 1920: `artifacts/ui/20260720-workbench-p1-1280/before-workbench-selected-filter-1920.png`
- After 1920: `artifacts/ui/20260720-workbench-p1-1280/after-workbench-selected-filter-1920.png`

Boundary: this verifies the Workbench header density at the two reference
sizes. It does not accept the whole visual-system or `80/100` UI/UX gate.

## P1 state-surface slice evidence - 2026-07-20

Status: Complete (Recipe Pipeline state presentation only)

The Pipeline Review keeps the existing workflow state values and command
rules, but presents every current state with one visible text label, color,
familiar icon, tooltip, and automation name. Neutral `Taught / pending` stays
gray with a clock. `Ready` and `Preview ready` use the teal/eye-or-check
treatment, `Published` uses green/check, and blocked/correction/stale states
use amber warning or refresh treatment. `Error` uses the existing red/error
treatment. No Preview, Publish, Run, recipe save, parameter, or tool adapter
binding changed.

| Acceptance criterion | Evidence |
| --- | --- |
| Operator can distinguish current Pipeline states without reading a muted text column | Current-build Preview and Published captures show `Preview ready`, `Published`, and `Taught / pending` as compact text-and-icon badges. The XAML state map also explicitly covers `Waiting for upstream`, `Preview stale`, `Taught / needs correction`, `Taught incomplete`, and `Error`. |
| Status remains accessible when color is unavailable | Each badge preserves the bound state text, provides a `Pipeline step state` tooltip, and exposes the same name through WPF Automation. |
| The two reference widths preserve the review table | The current `1280 x 760` Preview capture shows the status badges without horizontal overlap or truncation of the visible state labels. |
| Existing Docking, teaching, recipe-manager, and output-state behavior remains intact | Current build passed docking `19/19`, Tool Recipe Teaching `16/16`, Recipe Manager/WPG `17/17`, and Artifact Navigator `9/9`, including Preview/Published/Stale output-state transitions. |

Fresh current-build captures:

- Before, 1920: `artifacts/ui/20260720-workbench-p1-state-badges/before-workbench-filter-1920.png`
- After, Preview 1920: `artifacts/ui/20260720-workbench-p1-state-badges/after-workbench-filter-preview-1920.png`
- After, Published 1920: `artifacts/ui/20260720-workbench-p1-state-badges/after-workbench-filter-published-1920.png`
- After, Preview 1280: `artifacts/ui/20260720-workbench-p1-state-badges/after-workbench-filter-preview-1280.png`

Verification reports are under
`artifacts/verification/20260720-workbench-p1-state-badges/`.

Boundary: this completes the Pipeline state surface only. It does not accept
the remaining P1 visual-system work or the global `80/100` UI/UX gate, and it
does not add an inspection algorithm.

## P1 Toolbox and Step Parameters feedback slice - 2026-07-20

Status: Complete (source readiness and parameter-adapter presentation only)

The theme now owns the shared Pipeline workflow-state badge instead of keeping
it private to the review table. Toolbox uses the existing source readiness
contract to present a green verified-source or amber blocked-source card.
Step Parameters uses the same state badge for the selected step and presents
the existing typed-adapter status as a teal ready or amber preserved/read-only
card. The empty inspector now directs the operator to Toolbox's `Add
inspection step` route, rather than incorrectly implying that Recipe Manager
is the creation surface.

| Acceptance criterion | Evidence |
| --- | --- |
| Empty workbench has a visible source and next-action state | The current-build empty capture shows the verified recipe-source card, the explicit `No recipe step selected` context, and the Toolbox route in Step Parameters. |
| Unsupported authored step exposes its non-execution boundary | The XYZ Affine capture shows `Taught / pending` plus the amber `Partially supported - parameters are preserved read-only` adapter card. |
| State treatment is consistent across review and inspector panes | The shared theme owns the same badge colors, icons, text, tooltips, and automation names used by Recipe Pipeline and Step Parameters. |
| Reference sizes and existing behavior remain intact | Current `1920 x 1080` empty/unsupported captures and `1280 x 760` unsupported capture are accepted. Current build passed docking `19/19`, Tool Recipe Teaching `16/16`, Recipe Manager/WPG `17/17`, and Artifact Navigator `9/9`. |

Fresh current-build captures:

- Before, empty 1920: `artifacts/ui/20260720-workbench-p1-pane-feedback/before-empty-1920.png`
- Before, unsupported 1920: `artifacts/ui/20260720-workbench-p1-pane-feedback/before-unsupported-affine-1920.png`
- After, empty 1920: `artifacts/ui/20260720-workbench-p1-pane-feedback/after-empty-1920.png`
- After, unsupported 1920: `artifacts/ui/20260720-workbench-p1-pane-feedback/after-unsupported-affine-1920.png`
- After, unsupported 1280: `artifacts/ui/20260720-workbench-p1-pane-feedback/after-unsupported-affine-1280.png`

Verification reports are under
`artifacts/verification/20260720-workbench-p1-pane-feedback/`.

Boundary: this completes one P1 feedback slice. It does not score the global
UI/UX gate or add algorithm execution, editable graph behavior, physical
calibration, or metrology claims.

## P1 dock-panel consistency slice - 2026-07-20

Status: Complete (docked-panel controls, labels, density, and read-only state)

The lower Workbench panes now use one compact title/caption/status-card rhythm.
Pipeline, Session Log, Height Profile, Fit Diagnostics, Intersection Evidence,
and Landmark Correspondence Evidence retain their existing content and actions,
but distinguish view-only evidence from actionable teaching controls. Read-only
text fields now use the disabled surface rather than appearing editable. The
Session Log can be selected through the same dock API as Profile and the
evidence panes; the command-line smoke selector is capture-only and has no
normal interactive behavior.

| Acceptance criterion | Evidence |
| --- | --- |
| Lower dock panels have a consistent readable structure | Current captures show a common compact heading, explanation, status card, and bordered content region for Session Log, Height Profile, Intersection Evidence, and Correspondence Evidence. |
| View-only data does not look like a write surface | Read-only evidence cards label the boundary in text, icon, tooltip, and Automation name. The XYZ Affine parameter fields use the disabled/read-only surface in the `1920 x 1080` capture. |
| Two reference sizes remain usable | Current Pipeline captures at `1920 x 1080` and `1280 x 760`, plus five focused `1280 x 760` pane captures, show no meaningful clipping or overlap. |
| Dock behavior remains intact | Current build passed Workbench docking `20/20`, including Float/Dock preservation and explicit selection of Session Log, Profile, Fit Diagnostics, Intersection Evidence, and Correspondence Evidence. Tool Recipe Teaching passed `16/16`. |

Fresh current-build captures:

- Before 1920: `artifacts/ui/20260720-workbench-p1-dock-density/before-1920.png`
- Before 1280: `artifacts/ui/20260720-workbench-p1-dock-density/before-1280.png`
- After Pipeline 1920: `artifacts/ui/20260720-workbench-p1-dock-density/after-pipeline-1920.png`
- After Pipeline 1280: `artifacts/ui/20260720-workbench-p1-dock-density/after-pipeline-1280.png`
- After Session, Profile, Intersection, and Correspondence 1280: `artifacts/ui/20260720-workbench-p1-dock-density/after-*-1280.png`

Verification reports are under
`artifacts/verification/20260720-workbench-p1-dock-density/`.

Boundary: this completes the requested dock-panel P1 slice. The global
`80/100` UI/UX gate remains unscored; P2 tree-first recipe/tool navigation,
P3 Tool Lab review, and P4 owner acceptance are still required before new
algorithm work resumes.
