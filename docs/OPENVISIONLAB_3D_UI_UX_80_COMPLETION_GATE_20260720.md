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

## P2 bilingual navigation foundation - 2026-07-20

Status: Complete (Korean/English authoring-surface slice only)

The 3D Shell now takes a direct reference to the existing
`OpenVisionLab.Localization` service used by the 2D product. The Header has a
Korean/English selector. The service persists an operator-selected language
through its existing `CONFIG/language.txt` behavior; the current-build capture
argument `--ui-language ko|en` deliberately does not persist a smoke choice.

The translated authoring surface is limited to the product subtitle, workspace
commands, Tool Labs menu, dock titles, Recipe Navigator, source card, empty
Step Parameters guidance, selected-step labels, and Recipe Pipeline headings
and commands. Tool IDs, user-authored recipe content, typed contracts, and
currently English-only deep parameter values remain unchanged so routes and
recipe compatibility do not change. The dock verifier now validates stable
pane IDs and content rather than locale-specific caption text.

| Acceptance criterion | Evidence |
| --- | --- |
| Korean and English are visibly distinct at the two reference sizes | Current-build captures show the selector plus translated header, tree, Step Parameters, dock titles, and Pipeline controls at `1920 x 1080` and `1280 x 760`. |
| Existing 2D localization pattern is reused instead of a new language subsystem | `OpenVisionLab.ThreeD.Shell` directly references `OpenVisionLab.Localization`; `ThreeDLocalization` refreshes view bindings from its existing `LanguageChanged` event. |
| Locale does not weaken docking or teaching contracts | Current build passed Workbench docking `20/20` and Tool Recipe Teaching `16/16`. The structural docking contract is intentionally locale-neutral. |
| Capture quality and responsive review passed | All four current-build captures passed the Shell screenshot-quality report on first attempt. |

Fresh current-build captures:

- Before English 1920: `artifacts/ui/20260720-workbench-p2-localization/before-english-1920.png`
- Before English 1280: `artifacts/ui/20260720-workbench-p2-localization/before-english-1280.png`
- After Korean 1920: `artifacts/ui/20260720-workbench-p2-localization/after-korean-1920.png`
- After Korean 1280: `artifacts/ui/20260720-workbench-p2-localization/after-korean-1280.png`
- After English 1920: `artifacts/ui/20260720-workbench-p2-localization/after-english-1920.png`
- After English 1280: `artifacts/ui/20260720-workbench-p2-localization/after-english-1280.png`

Verification reports are under
`artifacts/verification/20260720-workbench-p2-localization/`.

Boundary: this is the bilingual foundation for P2, not complete translation of
every tool parameter, result string, evidence pane, or dialog. The P2
tree-first route/Tool Lab entry completion is recorded immediately below; P3
and P4 remain required before algorithm work resumes.

## P2 selected-route and Tool Lab entry slice - 2026-07-20

Status: Complete (selected-step route and existing Tool Lab entry only)

The left Toolbox now presents one compact `Selected inspection route` card for
the current pipeline step. It keeps the current step name/state and the exact
authored `Input -> Output` entity route together before the larger Recipe
Navigator tree. When the selected tool already has an existing focused Tool
Lab (Filter, Height Difference Edge, Line Intersection, or Landmark
Correspondence), the card exposes one direct open command. It only opens the
existing single-instance comparison window; it does not add/edit a step,
Preview, Publish, Run, or alter source data.

The Header Tool Labs menu retains its prior fallback behavior. In contrast, a
direct selected-route command preserves the exact selected step when multiple
steps use the same tool, so its parameter and evidence context does not jump
to an earlier duplicate.

| Acceptance criterion | Evidence |
| --- | --- |
| Selected route is readable before expanding the tree | Current Korean Filter captures show `Step 01: Filter`, Ready state, source input, and filtered-height output together above Recipe Navigator. |
| Existing focused Tool Lab is reachable from its selected route | Tool Recipe Teaching verification checks the selected Filter input/output, available command, requested tool ID, and preserved selected step. The P2 UI Automation report finds `OpenSelectedToolLabButton`, invokes it, and finds `Filter Tool Lab | OpenVisionLab 3D Studio`; the current Filter Tool Lab smoke also captures its actual single-instance comparison window. |
| Lifecycle and docking contracts remain unchanged | Current build passed Tool Recipe Teaching `18/18` and Workbench docking `20/20`; the direct command only routes an open-window request. |
| Two reference sizes remain usable | Fresh current-build Korean `1920 x 1080` and `1280 x 760` captures passed Shell screenshot quality on first attempt. |

Fresh current-build captures:

- Before Filter 1920: `artifacts/ui/20260720-workbench-p2-route-entry/before-filter-1920.png`
- Before Filter 1280: `artifacts/ui/20260720-workbench-p2-route-entry/before-filter-1280.png`
- After Filter 1920: `artifacts/ui/20260720-workbench-p2-route-entry/after-filter-1920.png`
- After Filter 1280: `artifacts/ui/20260720-workbench-p2-route-entry/after-filter-1280.png`
- Filter Tool Lab 1280: `artifacts/ui/20260720-workbench-p2-route-entry/filter-tool-lab-1280.png`

Verification reports are under
`artifacts/verification/20260720-workbench-p2-route-entry/`, including
`selected-tool-lab-ui-automation.txt`.

Boundary: this completes the selected-route and direct-entry slice. It does
not make a writable graph editor, add a Tool Lab for every tool, or implement
new inspection algorithms. The next UI delivery priority is P3: consistent
input/parameters/output/evidence review inside the existing Tool Lab windows.

## P3 Tool-Lab and review polish - 2026-07-20

Status: Complete (existing Tool Lab presentation only)

Filter, Height Difference Edge, Line Intersection, and Landmark
Correspondence now share one three-stage reading order: the upper comparison
shows the routed input and output/evidence, the shared lower review surface
shows staged parameters, and execution evidence stays explicit beside the
current output. The shared `ToolLabReviewView` supplies the lower
`Parameters & execution evidence` header, current state badge, and the
explicit draft/Preview/Publish boundary. It reuses the existing
`ToolInspectorView`, so it does not create a second parameter editor or a new
execution path. The docked review panes already received the same compact
heading/status-card density in the completed P1 slice; P3 preserves that
normal-density review surface and verifies docking again rather than creating
another parallel evidence view.

The command row is now consistent across the four windows: localized
`Preview`, `Show input`, and `Publish` labels retain their existing commands,
tooltips, and automation names. Filter now exposes its execution/hash summary
in its output header; Line Intersection exposes its current execution summary;
Landmark Correspondence exposes current execution, structural evidence, and
output-hash state alongside its reference-coordinate evidence. This is a
presentation improvement only: no Tool Lab opens, previews, publishes, or
edits a recipe automatically.

| Acceptance criterion | Evidence |
| --- | --- |
| Every existing Tool Lab has the same input / parameter / output-evidence sequence | Current Korean Filter, Edge, Line Intersection, and Landmark Correspondence captures show the common lower review header/state plus their native input and output/evidence surfaces. |
| Result evidence is visible before scrolling through parameter details | Filter, Line Intersection, and Landmark Correspondence now place their current execution/result/hash information in the upper output/evidence header; Edge retains its existing equivalent summary. |
| Korean and English labels remain intentional and readable | The shared command and review labels are Korean in the current Korean captures and English in the current English Filter capture; technical contract names remain stable identifiers. |
| Existing lifecycle and dock contracts remain intact | Current build passed Tool Recipe Teaching `18/18` and Workbench docking `20/20`; four Tool Lab smoke captures passed screenshot quality on their first attempt. |

Fresh current-build captures:

- Before Filter, Edge, Line Intersection, Landmark Correspondence: `artifacts/ui/20260720-workbench-p3-tool-lab-rhythm/before-*.png`
- After Korean Filter: `artifacts/ui/20260720-workbench-p3-tool-lab-rhythm/after-filter.png`
- After Korean Edge: `artifacts/ui/20260720-workbench-p3-tool-lab-rhythm/after-edge.png`
- After Korean Line Intersection: `artifacts/ui/20260720-workbench-p3-tool-lab-rhythm/after-intersection.png`
- After Korean Landmark Correspondence: `artifacts/ui/20260720-workbench-p3-tool-lab-rhythm/after-correspondence.png`
- After English Filter: `artifacts/ui/20260720-workbench-p3-tool-lab-rhythm/after-filter-en.png`

Verification reports are under
`artifacts/verification/20260720-workbench-p3-tool-lab-rhythm/`.

Boundary: this completes P3 for the four existing Tool Labs. It does not add
a Tool Lab for every future tool, a writable graph editor, linked cameras, a
new inspection algorithm, physical calibration, or metrology evidence. P4 is
the owner scorecard review and `80/100` acceptance decision.

## P4 provisional acceptance review - 2026-07-20

Status: Owner review reopened — the `89/100` score below is historical and is not an acceptance candidate until the GoPxL chain-readability gaps are re-reviewed.

This review uses the current Debug build after the Tool Lab context correction.
Each single-instance Tool Lab now remembers its exact recipe step and restores
that step when the window becomes active. This prevents a Filter, Edge, or
Line Intersection Tool Lab from editing or showing the last Tool Lab's
parameters after several comparison windows are open. The smoke capture also
refreshes the window's own context before taking its screenshot.

The points below are an evidence-based provisional score, not an owner
acceptance. It exceeds the `80/100` threshold, but no new algorithm work is
authorized until the owner explicitly accepts the scorecard.

| Area | Available | Provisional points | Current evidence and remaining deduction |
| --- | ---: | ---: | --- |
| Workbench information hierarchy | 20 | 18 | Korean and English `1920 x 1080` Workbench captures show recipe/source, selected route, typed state, next explicit action, Viewer, and Pipeline together. Two points remain for a first-time-operator observation rather than a smoke capture. |
| Visual system and 1920 layout | 20 | 17 | Current `1920 x 1080` Korean/English and `1280 x 760` Korean captures preserve the navy/light/teal system without meaningful overlap. Three points remain because deep typed-parameter/evidence strings are intentionally still mixed technical English and no separate high-DPI review was run. |
| Docking and window behavior | 15 | 15 | Workbench docking verification passes `20/20`; current Recipe Manager and all four Tool Labs use their custom title views and each focused window remains single-instance. |
| Recipe and tool workflow | 20 | 18 | Tree-first source/route/output presentation, active-lab step restoration, Tool Lab input/parameters/output-evidence sequence, and explicit Preview/Publish are visible. Tool Recipe Teaching passes `18/18`. Two points remain for a later owner usability pass across a real multi-step taught recipe. |
| Feedback and accessibility | 15 | 12 | Current Ready, pending/upstream-blocked, read-only, adapter, warning, and disabled states carry visible text, color, icons, tooltips, and automation names. Three points remain because no manual assistive-technology or keyboard-only operator session was performed. |
| Evidence and visual polish | 10 | 9 | Fresh current-build captures cover Workbench, Recipe Manager, four Tool Labs, and three docked comparison panes; all screenshot-quality reports accept on attempt 1. One point remains for manual real-hardware/high-DPI visual review. |
| **Total** | **100** | **89** | **Owner decision required** |

Fresh current-build review artifacts:

- Workbench: `artifacts/ui/20260720-workbench-p4-acceptance/workbench-filter-1920-ko.png`, `workbench-filter-1920-en.png`, and `workbench-filter-1280-ko.png`.
- Recipe Manager: `artifacts/ui/20260720-workbench-p4-acceptance/recipe-manager-ko.png`.
- Tool Labs before the context correction: `artifacts/ui/20260720-workbench-p4-acceptance/filter-tool-lab-ko.png`, `height-difference-edge-tool-lab-ko.png`, `line-intersection-tool-lab-ko.png`, and `landmark-correspondence-tool-lab-ko.png`.
- Tool Labs after the correction: `artifacts/ui/20260720-workbench-p4-acceptance/after-refresh-filter-tool-lab-ko.png`, `after-refresh-height-difference-edge-tool-lab-ko.png`, `after-refresh-line-intersection-tool-lab-ko.png`, and `after-refresh-landmark-correspondence-tool-lab-ko.png`.
- Docked comparison panes: `artifacts/ui/20260720-workbench-p4-acceptance/workbench-profile-1280-ko.png`, `workbench-intersection-1280-ko.png`, and `workbench-correspondence-1280-ko.png`.
- Screenshot-quality reports and command-verification reports: `artifacts/verification/20260720-workbench-p4-acceptance/`.

Current verification:

- `dotnet build OpenVisionLab.ThreeDStudio.slnx -c Debug -p:Platform="Any CPU" --no-restore -v:q`: `0` warnings, `0` errors.
- Tool Recipe Teaching: `18/18` passed.
- Workbench docking: `20/20` passed.
- Recipe Manager/WPG: `17/17` passed.
- Eleven current-shell/window screenshot-quality reports: accepted on attempt `1`.

Owner decision required: accept or revise the provisional `89/100` score. If
accepted, update the product-target checkpoint to record the UI gate as
complete and only then select the next algorithm slice. If revised below `80`,
keep algorithm work paused and use the deducted rows as the next UI scope.

## GoPxL chain-readability reassessment and G1 Flow Map - 2026-07-20

Status: Complete for G1; the global UI/UX gate remains **not accepted**.

The owner correctly identified that the earlier `89/100` table evaluated
tree-first navigation, Tool Labs, docking, and styling, but did not give
enough weight to the readability of a multi-tool data chain. It therefore
cannot be used as a UI-gate acceptance score. The functional reference was
checked again against the official [LMI GoPxL Tools](https://am.lmi3d.com/manuals/gopxl/gopxl-1.2/LMILaserLineProfiler/Content/Inspect_toolRelated/Inspect_Tools.htm)
and [Working with Tool Chains](https://am.lmi3d.com/manuals/gopxl/gopxl-1.2/LMILaserLineProfiler/Content/ToolsDiagram/Working_with_Tool_Chains_in_the_Tools_Diagram.htm)
guidance: GoPxL makes the current tool's Inputs, Parameters, Outputs, typed
ports, and displayable results easy to inspect together. OpenVisionLab adopts
that operating principle, not GoPxL's visual design.

| Area | Current OpenVisionLab evidence | Decision |
| --- | --- | --- |
| Tool catalog and route selection | Toolbox, selected-route card, and Recipe Navigator already expose typed tools and authored IDs. | Keep tree-first; later add compatible-next-tool scanning, never silent auto-routing. |
| Data-chain readability | The Pipeline table lists rows but did not show each row as a connected input-to-output route. | G1 added a read-only Flow Map synchronized with the same selected step. |
| Tool configuration | Typed WPG parameters and Tool Labs exist. | G3 complete: selected Step Parameters begins with a read-only Inputs / Parameters / Output summary; retain one WPG editor. |
| Displayed outputs and comparison | A shared Output Compare dock pins real source/Filter Preview C3D artifacts into independent A/B/C viewers. | G2 complete; owner must now score the combined route/configuration workflow. |
| Graph editing and industrial scope | No generic typed-port graph executor, undo model, or hardware-control boundary exists. | Deliberately deferred; no free drag-to-connect, camera, PLC, HMI, or production control. |

G1 scope: the existing docked Pipeline / Validation evidence pane now provides
a bilingual `Flow Map` tab. Each authored row renders the existing `Input
contract + entity IDs -> Tool -> Output contract + entity ID + state` route.
Both the tree/table and the Flow Map bind to `SelectedPipelineStep`, so
selection is shared. The new `--workbench-bottom-pane flow-map` smoke option
opens that tab and retains the existing dock behavior. It only displays the
recipe draft: it cannot edit a connection, create a step, change a parameter,
or run Preview/Publish.

Acceptance criteria and current evidence:

| Acceptance criterion | Evidence |
| --- | --- |
| Existing typed routes are visible as input-to-tool-to-output cards | Current `1920 x 1080` Korean capture shows Filter and Height Difference Edge cards with existing contract/entity IDs and state. |
| Flow Map is read-first and selection-synchronized | It reuses `PipelineSteps` / `SelectedPipelineStep`; docking verification explicitly activates the Flow Map without a model mutation. |
| The two reference heights remain legible | Final Korean `1920 x 1080` and `1280 x 760` captures show the selected Flow Map. The compact `1280` dock displays the first complete route and retains vertical scrolling for the remaining chain. |
| Korean and English labels are intentional | Final Korean and English `1920 x 1080` captures show translated Flow Map labels while technical contracts/IDs remain stable. |
| Existing teaching and docking behavior remains intact | Build is `0` warnings / `0` errors; Tool Recipe Teaching `18/18`; Workbench docking `21/21`. |

Evidence:

- Before: `artifacts/ui/20260720-gopxl-flow-map-g1/before-workbench-current-1920-ko.png`.
- After Korean: `artifacts/ui/20260720-gopxl-flow-map-g1/after-flow-map-1920-ko-final.png` and `after-flow-map-1280-ko-final.png`.
- After English: `artifacts/ui/20260720-gopxl-flow-map-g1/after-flow-map-1920-en-final.png`.
- Verification: `artifacts/ui/20260720-gopxl-flow-map-g1/workbench-docking.report.txt` and `tool-recipe-teaching.report.txt`.

Boundary: G1 is a route map, not a free-form topology editor. It intentionally
does not draw editable branch wires or fabricate result data.

## G2 Displayed Outputs / Compare - 2026-07-20

Status: Complete for G2; the global UI/UX gate remains **not accepted**.

The new `Output Compare` dock is a floatable, hideable lower Workbench pane
with explicit A/B/C session pins. Its candidate list is derived from the
existing Artifact Registry but intentionally renders only artifacts that have
real C3D data: the verified source and the current non-stale Filter Preview.
The `—` item clears a slot. An Edge, line, intersection, or declared-only
artifact is not shown as a fabricated C3D surface.

The card labels show display name, contract, state, and stable entity ID. Each
occupied card hosts its own compact viewer; the card strip has a fixed
three-card width with horizontal access at `1280 x 760`, and the entire pane
can be floated for a larger comparison. A registry rebuild after Preview keeps
the explicit source pin rather than allowing WPF list refresh to clear it.
Selecting, clearing, docking, or floating a comparison slot never changes the
recipe, reroutes an input, or executes Preview/Publish.

| Acceptance criterion | Evidence |
| --- | --- |
| Only real, current displayable C3D artifacts can be pinned | Artifact Navigator `11/11` proves source plus Filter Preview candidates are present, while downstream `EdgePointSet` is excluded. |
| Explicit A/B pin survives Filter Preview candidate rebuild | The same `11/11` check confirms the source pin and Filter Preview pin remain resolved after the registry is rebuilt. |
| Compare is a normal dockable Workspace view | Workbench docking is `22/22`, including Output Compare activation; the pane allows Float and Hide but cannot close. |
| Korean/English and compact layout are readable | Current-build Korean `1920 x 1080` / `1280 x 760` and English `1920 x 1080` screenshot-quality reports all accepted on attempt one. |
| Existing recipe behavior remains unchanged | Current build has `0` warnings / `0` errors; the compare view reads session state only. |

Evidence:

- Before: `artifacts/ui/20260720-output-compare-g2/before-workbench-1920-ko.png`.
- After Korean: `artifacts/ui/20260720-output-compare-g2/after-output-compare-1920-ko.png` and `after-output-compare-1280-ko.png`.
- After English: `artifacts/ui/20260720-output-compare-g2/after-output-compare-1920-en.png`.
- Verification: `artifacts/verification/20260720-output-compare-g2-artifact-navigator.txt` and `20260720-output-compare-g2-docking.txt`.

Boundary / next dependency: G2 is a bounded real-artifact compare surface,
not a generic result router or linked-camera subsystem. G3 completes the
selected-tool summary below without adding a second parameter editor or
starting an algorithm.

## G3 Selected-tool Inputs / Parameters / Outputs - 2026-07-20

Status: Complete for G3; the global UI/UX gate remains **not accepted**.

The selected Step Parameters pane now starts with a compact, read-only
`Inputs -> Parameters -> Output` summary. It places the existing input
contract/entity ID, typed adapter state, and output contract/entity ID in one
scan path immediately above the existing WPG. Existing input/output text-box
editors remain their current controls, and the WPG remains the single staged
parameter editor with the existing Apply/Discard boundary.

| Acceptance criterion | Evidence |
| --- | --- |
| Selected tool configuration is readable without expanding Input or Output sections | The summary card shows the authored Filter input and output IDs, contracts, and typed adapter status together. |
| Parameter editing has one source of truth | The summary is read-only; only the existing `RecipeStepPropertyGridHost` edits a typed draft. |
| Korean/English labels and compact width remain readable | Current-build Korean `1920 x 1080` / `1280 x 760` and English `1920 x 1080` captures show the localized summary labels while technical IDs remain stable. |
| Existing recipe and dock behavior remains unchanged | Build is `0` warnings / `0` errors; Tool Recipe Teaching `18/18`; Workbench docking `22/22`; Artifact Navigator `11/11`. |

Evidence:

- Before: `artifacts/ui/20260720-input-parameter-output-g3/before-tool-inspector-1920-ko.png`.
- After Korean: `artifacts/ui/20260720-input-parameter-output-g3/after-tool-inspector-1920-ko.png` and `after-tool-inspector-1280-ko.png`.
- After English: `artifacts/ui/20260720-input-parameter-output-g3/after-tool-inspector-1920-en.png`.
- Verification: `artifacts/ui/20260720-input-parameter-output-g3/tool-recipe-teaching.report.txt`, `workbench-docking.report.txt`, and `artifact-navigator.report.txt`.

Boundary / next decision: G3 changes information hierarchy and localized text
only. It does not add a tool, algorithm, graph editor, generic executor,
camera/PLC/HMI, affine execution, calibration, or metrology claim.

## G4 Displayed Outputs / Overlay Manager - 2026-07-20

Status: Complete for G4; the global UI/UX gate remains **not accepted**.

The new `Displayed Outputs / Overlay Manager` is a floatable/hideable lower
Workbench dock that sits between the existing recipe navigation and independent
Output Compare viewers. It derives every row from the read-only typed Artifact
Registry. Only an actual verified C3D source or a current non-stale Filter C3D
can use `Show in 3D View` or `Pin to Compare`; the Viewer reports a successful
display before the row is marked as current. Pinning fills the first empty A,
B, or C slot and remains session-only.

Feature artifacts with current identity are explicitly marked evidence-only
and expose only `Focus Step`. Declared or stale artifacts are visibly
unavailable. This prevents an `EdgePointSet`, fitted line, or corner from
looking like a complete transformed C3D surface. The manager contains no new
tool execution, parameter editor, route editor, persistence path, or generic
overlay renderer.

| Acceptance criterion | Evidence |
| --- | --- |
| Only real C3D artifacts can enter the main Viewer or comparison pins | Artifact Navigator `14/14` covers source display request, Filter pinning, and excludes current `EdgePointSet` from both paths. |
| Feature output is honest about its display boundary | The same verification checks evidence-only Edge output with `Focus Step`; stale output becomes unavailable without losing identity. |
| Manager actions preserve authored workflow boundaries | The display/pin checks confirm no Filter/Edge execution; Tool Recipe Teaching remains `18/18`. |
| Output manager is a normal dockable view | Workbench docking is `24/24`; `displayed-outputs` activates, its dock contract permits Float/Hide, and it cannot close. |
| Bilingual operator layouts remain usable | Current screenshots pass Korean `1920 x 1080` / `1280 x 720` and English `1920 x 1080` quality checks on first attempt. |

Evidence:

- Before: `artifacts/ui/20260720-displayed-outputs-g4/before-displayed-outputs-1920-ko.png`.
- After Korean: `artifacts/ui/20260720-displayed-outputs-g4/after-displayed-outputs-1920-ko.png` and `after-displayed-outputs-1280-ko.png`.
- After English: `artifacts/ui/20260720-displayed-outputs-g4/after-displayed-outputs-1920-en.png`.
- Verification: `artifacts/ui/20260720-displayed-outputs-g4/tool-recipe-teaching.report.txt`, `workbench-docking.report.txt`, and `artifact-navigator.report.txt`.

Boundary / next priority: G4 completes a bounded display-management surface,
not a generic graph, automatic routing system, arbitrary-overlay renderer, or
algorithm executor. G5 is port-level Flow Map diagnostics and a compact
Problems surface; G6 is compatible Tool Catalog scanning. The `80/100` UI/UX
gate is still not owner accepted.
