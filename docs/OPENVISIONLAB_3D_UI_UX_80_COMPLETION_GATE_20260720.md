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
