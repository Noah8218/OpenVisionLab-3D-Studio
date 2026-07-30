# OpenVisionLab 3D Master Development Workflow and Backlog

Date: 2026-07-27

Status: Current execution source of truth for commercial-video-derived product
development after Inspection Workspace v3

## Purpose

This document converts the commercial-video review into an executable product
development system.

It answers five questions for every future chat:

1. What product are we building?
2. What has already been developed?
3. What is partial or missing?
4. In what dependency order should the remaining work be developed?
5. What evidence closes one item before another begins?

Read this document after:

- `AGENTS.md`;
- `docs/OPENVISIONLAB_3D_NEXT_CHAT_HANDOFF_PROMPT_20260728.md`;
- `docs/OPENVISIONLAB_3D_NEXT_SESSION_HANDOFF.md`;
- `docs/OPENVISIONLAB_3D_COMMERCIAL_VIDEO_DIRECTION_AND_PRIORITY_20260727.md`;
- `docs/OPENVISIONLAB_3D_INDUSTRIAL_UX_AUDIT_20260728.md`.

## Product identity

OpenVisionLab 3D Studio is:

> A local, file-first, deterministic 2.5D/3D rule-based inspection workbench
> for identified height fields, point clouds, and meshes.

The operator:

- loads a measured source and optional reference;
- verifies identity, frame, unit, and data quality;
- teaches typed regions and features;
- configures deterministic preparation, alignment, and inspection steps;
- explicitly Previews, Publishes, or Runs;
- reviews metrics, overlays, tolerance state, and failure evidence;
- replays the recipe across a Validation Set;
- saves a durable recipe and Run Record.

The Viewer is a synchronized teaching and evidence surface. It is not the
entire product.

## Product workflow target

```text
1. Input
   local source/reference identity
      |
2. Source trust
   valid/missing map, distribution, frame, unit, provenance
      |
3. Teaching workspace
   linked Height Image + 3D surface + profile
      |
4. Typed regions/features
   GridRectangle / PointSet / OrientedBox3D / derived region artifacts
      |
5. Preparation
   filter / mask / level / align / transform / re-grid
      |
6. Inspection
   thickness / flatness / presence / completeness / matching / dimensions
      |
7. Explicit execution
   Preview -> Publish where applicable -> Run
      |
8. Evidence
   per-region metrics + overlays + status + timing + failure reason
      |
9. Sample validation
   Good / Bad / Held-out evidence and replay
      |
10. Persistence
    recipe + source identity + Run Record + export
```

## Status legend

| Status | Meaning |
| --- | --- |
| `C` Complete | Present in current source with reusable focused evidence. Preserve it. |
| `P` Partial | A real adjacent capability exists, but the target workflow is not complete. |
| `N` New | No current typed product contract or complete workflow was found. |
| `E` External prerequisite | Completion needs owner operation, physical data, calibration, hardware, or another non-code prerequisite. |
| `O` Out of scope | Deliberately excluded from the current product phase. |

`C` does not mean certified metrology. It means complete for its documented
software scope.

Current inventory count:

| Classification | Count |
| --- | ---: |
| Complete `C` | 104 |
| Partial `P` | 17 |
| New `N` | 88 |
| External prerequisite `E` | 9 |
| Out of scope `O` | 16 |
| Total | 234 |

## Current maturity and first gate

- Inspection Workspace v3 is `7/8` bounded slices (`87.5%`) complete.
- Its historical remaining gate was the owner's unaided exact-source replay.
  The 2026-07-29 information-architecture change reopens `A-01`; after the
  new stages are implemented, that replay must be replaced by the
  Setup/Teach/Validate/Results owner path.
- The current local deterministic recipe/measurement foundation is
  operational.
- The coordinate-true full-size Height Image display foundation is complete.
  Shared 2D/3D native-coordinate hover, synchronized `GridRectangle` ROI
  teaching, and the visible invalid-cell overlay are complete. Typed
  preparation, completeness cell metrics, deterministic cell acceptance,
  aggregate results, linked colored overlays, failed-cell review, and
  repeated-Tab result mapping, Validation Set examples, and Completeness
  threshold assistance are complete. Surface matching remains incomplete.
- Physical calibration, traceability, uncertainty, GR&R, and production
  tolerance are unverified.

### Current execution checkpoint - Validation top dock tabs - 2026-07-30

The multi-pane AvalonDock work-surface strip now appears above Validate
content instead of at the bottom window edge. It uses the shared OpenVision
Command Bar, Divider, Selected Surface, Accent, Focus, and Disabled tokens.
Multi-item panes no longer duplicate the active title in a second dark header;
single-item panes retain their normal title.

All eight visible TabItems expose localized titles and stable ContentIds.
Actual UI Automation and pointer evidence finds the eight top tabs and selects
Output Compare. Compact keeps every tab on one row.

Preserve:

- `docs/OPENVISIONLAB_3D_VALIDATION_TOP_DOCK_TABS_20260730.md`;
- `artifacts/current/20260730-validation-top-tabs/`.

Release build passes `0/0`, Workbench docking passes `59/59`, Validation Set
passes `84/84`, and actual application-only captures pass Wide
`1920 x 1040` and Compact `1280 x 760`.

Inventory remains `104 C / 17 P / 88 N / 9 E / 16 O`; `A-01` remains
`Partial` only for a fresh human-owner unaided R0 on this updated binary set.
After owner R0 passes, begin `J-01/J-03/J-04 SurfaceModel`.

### Current execution checkpoint - novice hierarchy and accessibility - 2026-07-29

Failure Analysis now leads with failed sample, failed rule, reason, and next
action before the detailed sample, step, metric, and overlay evidence. Results
leads with the decision, executed-step summary, and a keyboard-focusable Fix
in Teach route before Run Record sidecars, paths, reports, export, and
Advanced.

The contextual sample-set action now has one stable owner in the stage
navigation surface. Current Release Wide and Compact actual-pointer timelines
find `ValidationSetRunAllButton` directly by AutomationId and localized name;
the historical coordinate fallback is absent.

Preserve:

- `docs/OPENVISIONLAB_3D_NOVICE_INFORMATION_HIERARCHY_AND_ACCESSIBILITY_20260729.md`;
- `artifacts/current/20260729-novice-hierarchy-accessibility/before/`;
- `artifacts/current/20260729-novice-hierarchy-accessibility/final/`.

Release build passes `0/0`, Workbench docking passes `58/58`, Validation Set
passes `84/84`, and final media passes Wide `1920 x 1040` and Compact
`1280 x 760`, 15 fps, 110 s. Both layouts preserve
`3 Pass / 2 Fail / 0 Error`, Advanced geometry, and final failure evidence.

Inventory remains `104 C / 17 P / 88 N / 9 E / 16 O`; `A-01` remains
`Partial` only for the human-owner unaided R0. Do not repeat the automated
route while current evidence remains valid. After owner R0 passes, begin
`J-01/J-03/J-04 SurfaceModel`.

### Earlier execution checkpoint - Advanced Viewer reactivation - 2026-07-29

The current Release now explicitly releases the main Viewer from the nested
Teach host and reactivates both the Advanced workspace dependency property
and its live AvalonDock presenter. A post-layout visible-frame request
restores the C3D surface, ROI, Viewer controls, and HUD.

The Wide and Compact actual-pointer replay now rejects off-screen or
zero-sized Automation matches and requires visible Advanced and final Failure
Analysis postconditions. Both layouts execute the five-sample set with
`3 Pass / 2 Fail / 0 Error`, render Advanced geometry, and return to preserved
failure evidence.

Preserve:

- `docs/OPENVISIONLAB_3D_ADVANCED_VIEWER_REACTIVATION_20260729.md`;
- `artifacts/current/20260729-advanced-viewer-reactivation/`.

Release build passes `0/0`; Workbench docking passes `55/55`; media
verification passes Wide `1920 x 1040` and Compact `1280 x 760`, 15 fps,
110 s. Inventory remains `104 C / 17 P / 88 N / 9 E / 16 O`; `A-01`
remains `Partial`.

The historical P1 hierarchy and accessibility slice is complete in the
current checkpoint above.

### Earlier execution checkpoint - direct novice full-route repeat - 2026-07-29

Fresh current Release application-only videos repeat the full Wide and
Compact novice route with actual pointer clicks:

```text
5-sample Run -> 3 Pass / 2 Fail / 0 Error
-> Failure Analysis -> Fix in Teach
-> Results -> Advanced -> Results -> Validate
```

The previous Teach correction remains valid: both layouts render the source,
ROI, selected `Completeness Grid` step, and failed-sample correction card.
The wider route is `Incomplete` because Advanced renders a dark empty
`3D 검사 보기` pane in both layouts. The contextual sample-set command also
cannot be found by its expected AutomationId or accessible name and requires
a layout-derived pointer fallback. Compact visibly restores final Failure
Analysis. Wide's final click occurred inside the recorded interval, but the
historical harness did not assert or retain a post-click visible state, so
Wide final preservation is unproven rather than failed.

Preserve:

- `docs/OPENVISIONLAB_3D_DIRECT_NOVICE_REPEAT_ANALYSIS_20260729.md`;
- `artifacts/current/20260729-direct-novice-r0-repeat/`.

Release build passes `0/0`; media verification passes Wide
`1920 x 1040` / 68 s and Compact `1280 x 760` / 68 s at 15 fps. The
authoritative inventory remains `104 C / 17 P / 88 N / 9 E / 16 O`;
`A-01` remains `Partial`. This historical blocker is superseded by the
Advanced Viewer reactivation checkpoint above.

### Earlier execution checkpoint - Teach failure correction closure - 2026-07-29

The current Release now completes the simulated-novice
`Validation -> Failure Analysis -> Fix in Teach` software route. Teach
reattaches and renders the identified `completeness-taught.C3D` source and
ROI after stage recomposition. A read-only correction card carries the failed
sample, rule, reason, and exact failed/passed-cell summary.

Compact uses a focused Selected Tool composition during failure correction,
so the operator does not need to find a small tab. Leaving Teach restores the
normal Recipe Chain/Selected Tool ownership. The route does not invoke
Preview, Publish, Run, or mutate recipe semantics.

Preserve:

- `docs/OPENVISIONLAB_3D_DIRECT_NOVICE_REPLAY_FINDINGS_20260729.md`;
- `docs/OPENVISIONLAB_3D_TEACH_FAILURE_CORRECTION_CONTEXT_20260729.md`;
- `artifacts/current/20260729-direct-novice-r0-replay/`;
- `artifacts/current/20260729-teach-failure-correction/`.

The authoritative inventory remains
`104 C / 17 P / 88 N / 9 E / 16 O`; `A-01` remains `Partial` only because
the human owner's unaided R0 is external. Release build passes `0/0`,
Workbench docking passes `54/54`, and current app-only actual-pointer videos
pass Wide `1920 x 1040` / 42 s and Compact `1280 x 760` / 44 s at 15 fps.
SurfaceModel remains gated until owner R0 passes.

### Earlier execution checkpoint - IA-4b automated owner path - 2026-07-29

Current Release Wide and Compact application-only videos now execute the
controlled five-sample Completeness set and expose
`3 Pass / 2 Fail / 0 Error`. The selected failure opens its owning
`step.validation.completeness` in Teach. Results shows the supplied one-step
Fail Run Record, Advanced opens, and returning through Results to Validation
preserves the recipe, source, selected step, saved/dirty state, Validation
summary, and Run Record without starting hidden Preview or Run.

The initial replay discovered that the visible `Fix in Teach` button lost its
Shell command owner after dock recomposition. The hosted Validation view now
receives an explicit `RunRecordContext` binding from
`ToolRecipeWorkbenchView`. Release build passes `0/0`, the combined
Window-hosted integration/state-preservation verifier passes `52/52`, and
the accepted videos are `1920 x 1040` and `1280 x 760`, 15 fps, 72 seconds.

Preserve:

- `docs/OPENVISIONLAB_3D_IA4B_OWNER_PATH_REPLAY_20260729.md`;
- `artifacts/current/20260729-ia4b-owner-path-replay/`.

The automated IA-4b software gate is complete. `A-01` remains `Partial` and
the authoritative inventory remains `104 C / 17 P / 88 N / 9 E / 16 O`
until the human owner completes the documented unaided Wide/Compact R0
checklist. Do not repeat automated implementation work while that external
evidence is unavailable. `J-01/J-03/J-04 SurfaceModel` begins only after
owner R0 passes.

### Earlier execution checkpoint - IA-4a live stage-host repair - 2026-07-29

Every dynamically recomposed stage view now owns an explicit stable Shell or
Workbench context. The actual Release Wide and Compact replay restores Teach
Selected Tool content, five named Validate sections with five Pending
`2 Good / 2 Bad / 1 Held-out` rows, three named Results sections with the
supplied one-step Fail Run Record, and a visible Advanced transition.
Validate's local action is now `샘플 세트 실행` / `Run sample set`, distinct
from global recipe Run All.

The Workbench verification now hosts the view in a real off-screen WPF
Window and fails on stage-host owner loss, empty localized/accessibility
navigation, incorrect Validation Set row count, unavailable sample-set
command, or disconnected Advanced command. Release build passes `0/0` and
the focused integration check passes `48/48`. `A-10` returns to `Complete`;
`A-01` remains `Partial` until IA-4b and human-owner R0. The authoritative
inventory is `104 C / 17 P / 88 N / 9 E / 16 O`.

Preserve:

- `docs/OPENVISIONLAB_3D_STAGE_HOST_INTEGRATION_REPAIR_20260729.md`;
- `artifacts/current/20260729-stage-host-integration-repair/`;
- the historical before evidence under
  `artifacts/current/20260729-novice-stage-navigation-video-review/`.

At this earlier checkpoint, IA-4b still had to execute the sample set, open a
failure in Teach, and prove
Results -> Advanced -> Results state preservation, and complete the owner's
unaided Wide/Compact R0. SurfaceModel remains gated behind that acceptance.

### Superseded execution checkpoint - IA-4 novice actual-Release replay blocker - 2026-07-29

The application-only Wide and Compact video replay reaches all five
top-level stages, but the live dock recomposition loses the context required
by Teach Selected Tool, Validate, and Results. Validate renders five
unlabeled radio circles instead of the saved `2 Good / 2 Bad / 1 Held-out`
sample set and leaves Run All disabled. Results renders three unlabeled radio
circles instead of the supplied one-step Fail Run Record, and its enabled
Advanced gear produces no visible transition. The same controls expose empty
accessible names.

The prior IA-2/IA-3 structural checks and generated View captures did not
assert live MainWindow child context, non-empty localized labels, loaded row
counts, command readiness, or visible Advanced navigation. `A-01` stays
`Partial`; `A-10` returns from `Complete` to `Partial`. The authoritative
inventory is therefore `103 C / 18 P / 88 N / 9 E / 16 O`.

Preserve:

- `docs/OPENVISIONLAB_3D_NOVICE_STAGE_NAVIGATION_VIDEO_REVIEW_20260729.md`;
- `artifacts/current/20260729-novice-stage-navigation-video-review/`;
- `scripts/run-novice-stage-navigation-video-review.ps1`.

Immediate: repair stable stage-host ownership and add actual MainWindow
integration assertions, then repeat the Wide/Compact simulated-novice replay
and the owner's unaided R0. Do not begin SurfaceModel until this gate passes
or the owner explicitly reprioritizes.

### Superseded execution checkpoint - IA-3 dedicated Results workspace - 2026-07-29

IA-3 structure exists in current source. Results is one full-height
read-only workspace with local Run Record, Output Compare, and Reports/export
sections. It no longer combines the Viewer with a compressed lower record and
no longer exposes Save or teaching/validation mutation commands. Existing
expert docks remain available only through the explicit Advanced/Tool Labs
route.

Stage/local/Advanced navigation preserves recipe identity, selected-step
identity, step count, dirty state, current Viewer output summary, and Run
Snapshot summary. Current Release evidence passes build `0/0`,
docking/stage/non-mutation `47/47`, Run Record `10/10`, Artifact Navigator
`31/31`, Shell options `24/24`, structure `17/17`, and current
Wide/Compact/section capture quality.

The newer IA-4 actual-Release checkpoint above invalidates the live
integration closure claim. Preserve the prior implementation evidence:

- `docs/OPENVISIONLAB_3D_DEDICATED_RESULTS_WORKSPACE_20260729.md`;
- `artifacts/current/20260729-results-workspace-extraction/`.

`IA-4a` live stage-host integration repair is next. SurfaceModel
`J-01/J-03/J-04` remains the next functional train only after IA-4 and owner
R0.

### Historical execution checkpoint - IA-2 dedicated Validate workspace - 2026-07-29

`IA-2 / A-10` is complete in current Release source. Validate is now the only
full-height task surface and no longer combines a dominant Viewer with a
compressed lower Validation Set. Five local drill-down sections own Samples,
Run Results, Failure Analysis, Threshold Review, and Held-out evidence.
Failure-to-Teach navigation selects the existing owning step without changing
or executing the recipe. Results retains its Viewer plus Run Record
composition.

The implementation reuses the existing deterministic Validation Set,
candidate/error table, correction, and Held-out replay contracts. It does not
rewrite Runner logic or change Held-out exclusion. Current Release evidence
passes build `0/0`, docking/stage `44/44`, Validation Set `84/84`, Inspection
Workspace `63/63`, teaching `28/28`, Artifact Navigator `31/31`, Shell options
`24/24`, structure `17/17`, and current Wide/Compact capture quality.

`A-10` moves from `Partial` to `Complete`. `A-01` remains `Partial` until
Results/Advanced extraction and owner replay close. Inventory is now
`104 C / 17 P / 88 N / 9 E / 16 O`. Preserve:

- `docs/OPENVISIONLAB_3D_DEDICATED_VALIDATE_WORKSPACE_20260729.md`;
- `artifacts/current/20260729-validate-workspace-extraction/`.

The newer IA-3 checkpoint above supersedes this historical next priority.

### Historical execution checkpoint - IA-1 Setup/Teach separation - 2026-07-29

The owner rejected the current all-in-one default Workspace. Tool composition,
selected-step teaching, Viewer interaction, Validation Set/threshold evidence,
and Run Record review are valid capabilities, but they must not permanently
compete on one screen.

The approved design defines real top stages:

```text
Setup -> Teach -> Validate -> Results
```

`IA-1` is now complete in current Release source. Setup owns Tool Library and
the full Recipe Chain without Viewer or lower evidence. Teach owns the compact
step rail, dominant Viewer, and Selected Tool without Tool Library or lower
evidence. Wide and Compact compositions are distinct. Navigation preserves
recipe/source/selection state, never executes, and is guarded by active ROI,
PropertyGrid, Preview, and Validation work.

`Calibration` remains independent and Advanced diagnostics remain opt-in.
At this IA-1 checkpoint, `A-01` remained `Partial` because dedicated
Validate/Results extraction and the new owner replay were still open.
Inventory was
`103 C / 18 P / 88 N / 9 E / 16 O`. Preserve:

- `docs/OPENVISIONLAB_3D_WORKSPACE_INFORMATION_ARCHITECTURE_REDESIGN_20260729.md`;
- `docs/OPENVISIONLAB_3D_SETUP_TEACH_WORKSPACE_SEPARATION_20260729.md`;
- `artifacts/current/20260729-workspace-information-architecture/`.

The historical next item was `IA-2 / A-10`; the newer checkpoint above closes
it.

### Current execution checkpoint - 2026-07-29

`H-11/H-12/I-14 Completeness Validation Set and threshold assistance` is
complete. One controlled recipe replays two Good, two Bad, and one Held-out
sample with real `Pass/Fail/Pass` evidence. The threshold analyzer derives
one policy-equivalent worst-cell observation per sample for minimum finite
coverage, minimum reference-relative mean, and maximum reference-relative
mean. Shared report contract `2.1` carries the exact `r###.c###` cell
locator into every candidate decision. Held-out remains excluded from
candidate boundaries, ranking, counts, and decisions. Three fail-closed
mappings target only the existing Completeness policy parameters.
Review/Cancel are non-mutating; candidate Apply changes the PropertyGrid
draft only; an explicit development-only replay gates the separate Held-out
replay. Current Release evidence passes build `0/0`, Validation Set `82/82`,
Completeness golden `23/23`, Inspection Workspace `63/63`,
Recipe Manager/PropertyGrid `37/37`, docking `33/33`, Shell options `24/24`,
structure `17/17`, Runner schema `1.1`/threshold contract `2.1` with
`57` candidates, `4` development samples, `1` Held-out excluded,
`0` warnings, and `8` mappings, plus current Wide/Compact capture quality.
Preserve
`docs/OPENVISIONLAB_3D_COMPLETENESS_VALIDATION_AND_THRESHOLD_ASSISTANCE_20260729.md`
and
`artifacts/current/20260729-completeness-threshold-assistance/`.
At that closure `J-01/J-03/J-04 SurfaceModel preparation foundation` was
next. The newer information-architecture checkpoint above supersedes the
immediate priority and pauses SurfaceModel behind IA-1.

`H-08/H-10 completeness failure navigation and repeated-Tab result mapping`
is complete. Workbench now owns a view-only selected-cell review projection
over the existing H-07 stable cell IDs. Previous/Next traverses failed cells
in deterministic row-major order with wrap; all-pass output disables both
actions. Height Image and 3D emphasize the same selected cell without
changing cell policy. Ordinary Thickness steps named `Tab 1..8 Thickness`
map by ordinal to cell-result presentation while retaining their step and
output identities. Navigation does not dirty, save, Preview, Publish, Run, or
replay Validation Set. Current Release evidence passes build `0/0`, height
measurement Workbench `54/54`, Completeness golden `23/23`, Inspection
Workspace `63/63`, recipe teaching `28/28`, Artifact Navigator `31/31`,
docking `33/33`, Shell options `24/24`, Viewer display `103/103`, structure
`17/17`, and current Wide/Compact capture quality. Preserve
`docs/OPENVISIONLAB_3D_COMPLETENESS_FAILURE_NAVIGATION_AND_TAB_MAPPING_20260729.md`
and `artifacts/current/20260729-completeness-failure-navigation/`.
Its historical next `H-11/H-12` slice is complete in the newer checkpoint
above.
`H-09` remains blocked by the missing typed detected-region route
`E-11/G-12`.

`H-05/H-06/H-07 completeness result and overlays` is complete. The optional
typed policy adds inclusive finite-coverage and reference-relative mean
raw-height limits while preserving seven-parameter H-02 recipes as
evidence-only `Warning`. Tools produces deterministic cell Pass/Fail, fails
closed when a cell has no finite mean, counts passed/failed cells, and sets
aggregate Pass only when every cell passes. Core owns stable coordinate-true
overlay descriptors; Height Image and 3D render the same green/red cells
without owning decision policy. The mixed `8 x 8` fixture produces `2` Pass,
`2` Fail, aggregate `Fail`, `4` overlays, and output SHA
`1B051233FFCCC65FD72A4CB50299C629C8BCE7929E7AC4CA3CA3F33653DBF8CE`;
an independent all-valid fixture produces aggregate Pass. Current Release
evidence passes build `0/0`, golden `23/23`, height measurement Workbench
`50/50`, Inspection Workspace `63/63`, Recipe Manager/PropertyGrid `37/37`,
Artifact Navigator `31/31`, Shell options `24/24`, structure `17/17`,
production Runner parity, and current Wide/Compact capture quality. Preserve
`docs/OPENVISIONLAB_3D_COMPLETENESS_RESULTS_AND_OVERLAYS_20260729.md` and
`artifacts/current/20260729-completeness-results-overlays/`. Its historical
next slice is complete in the newer H-08/H-10 checkpoint above.

`H-02/H-03/H-04 completeness grid metrics` is complete. Core owns the typed
rows/columns/native X-column and Z-row pitch/cell-size/GridRectangle profile,
stable row-major cell identity, exact finite coverage, and explicit
reference-relative mean raw-height output. Tools generates deterministic
non-overlapping cell geometry inside one Inspection Grid ROI and fails closed
when the extent does not fit. Workbench preserves ordered Reference and
Inspection Grid ROI roles, typed PropertyGrid editing, and explicit
Preview/Publish. Ordered graph and production Runner emit the same typed
output SHA-256. The controlled `8 x 8` fixture produces four cells with
coverage `1, 0.75, 0.5, 0` and relative means `2, 4, -2, missing`. Current
Release evidence passes build `0/0`, golden `14/14`, height measurement
Workbench `50/50`, Inspection Workspace `63/63`, Recipe Manager/PropertyGrid
`37/37`, Shell options `24/24`, structure `17/17`, production Runner parity,
and current Wide/Compact capture quality. Preserve
`docs/OPENVISIONLAB_3D_COMPLETENESS_GRID_METRICS_20260729.md` and
`artifacts/current/20260729-completeness-grid-metrics/`. This slice applies
no acceptance policy or aggregate decision. That historical next slice is
complete in the newer H-05/H-06/H-07 checkpoint above.

`I-12/I-13/I-15 threshold-assistant evidence hardening` is complete. The
shared candidate report contract `2.0` now owns deterministic missing-Good,
missing-Bad, insufficient-Good, insufficient-Bad, imbalanced-class, and
inseparable-distribution warnings with exact step/metric ownership,
Good/Bad counts, and development-sample SHA identities. Held-out remains
excluded. Warnings are limited to explicitly supported assistant metrics so
unmapped ROI statistics do not create misleading parameter warnings. The
published fail-closed coverage matrix contains Thickness Mean
Minimum/Maximum/Range and Warpage PeakToValley/Rms Maximum only. Role edits,
warning-state changes, Review, candidate draft Apply, manual PropertyGrid
edits/Apply, development replay, and Held-out replay retain their explicit
execution boundaries. Current Release evidence passes build `0/0`,
Validation Set `72/72`, Inspection Workspace `63/63`,
Recipe Manager/PropertyGrid `37/37`, Shell options `24/24`, structure
`17/17`, Runner report schema `1.1`/threshold contract `2.0` with the same
five mappings, and fresh Wide/Compact capture quality.
Preserve
`docs/OPENVISIONLAB_3D_THRESHOLD_ASSISTANT_HARDENING_20260729.md` and
`artifacts/current/20260729-threshold-assistant-hardening/`.

The earlier 11-video analysis is a durable product-direction input, not a
one-time audit. Future items must trace to the source-by-source lessons in
`docs/OPENVISIONLAB_3D_COMMERCIAL_VIDEO_DIRECTION_AND_PRIORITY_20260727.md`
and the operator-focused findings in
`docs/OPENVISIONLAB_3D_INDUSTRIAL_UX_AUDIT_20260728.md`: GoPxL responsibility
separation, SICK evidence-based threshold/completeness, HALCON explicit model
and scene preparation plus pose/score diagnostics, MERLIC Height Image cell
inspection, and Zivid/Photoneo source-quality trust. This direction does not
authorize camera, reconstruction, factory-integration, cloud, or implicit
execution scope.

`L-11 threshold-correction evidence in Run Record` is complete. Ordered graph
Run Record schema `1.5` now embeds one read-only snapshot of the existing
recipe-side correction sidecar. It preserves exact candidate, step, tool,
metric, before, suggested, manually committed, before/corrected development,
and Held-out identities and values. Missing evidence is `Unavailable`;
identity differences are `Mismatch`; changed committed parameters are
`Stale`; malformed or internally inconsistent evidence is `Invalid`.
Projection never recalculates a threshold, applies a parameter, executes
inspection, or replays development/Held-out samples. JSON, HTML, and the
Workbench Run Record tab share the same typed contract. Current Release
evidence passes build `0/0`, Run Record `10/10`, Validation Set `72/72`,
Inspection Workspace `63/63`, Recipe Manager/PropertyGrid `37/37`, structure
`17/17`, production Runner JSON/HTML parity, and fresh Wide/Compact capture
quality. Preserve
`docs/OPENVISIONLAB_3D_THRESHOLD_CORRECTION_RUN_RECORD_20260729.md` and
`artifacts/current/20260729-threshold-correction-run-record/`.

The next implementation item is
`J-01/J-03/J-04 SurfaceModel preparation foundation`.

`I-09/I-11 manual parameter correction and durable failure -> correction ->
Held-out evidence` is complete. A controlled committed Thickness draft
`0..20` produces one genuine expected-role mismatch: Bad-high SHA
`6E00A03C6A901DFC39EBE41E7E14E3EC1FE8A3F4FBFBFECE9C1E8A5E6DCE9AD9`,
Mean `20`, passes incorrectly. The deterministic Range candidate remains
`threshold.0ad7b16eaa3d4362`, suggested `2..4`. The operator changes the typed
draft to `1.5..4.5`, commits through ordinary PropertyGrid Apply, then invokes
an explicit development-only replay. That replay preserves the same four
sample SHA identities and changes mismatch `1 -> 0`; it does not run Held-out.
Only then does the separate explicit Held-out command unlock. Held-out Mean
`3`, SHA
`D9384A7B5A032D28E952E8742619EA224F2763FC5B5B3C431DC895544AA93C3B`
passes. The portable evidence extension stores before, suggested, manual,
corrected development, and Held-out records. Workbench and Runner schema
`2.0` agree exactly. Current Release evidence passes build `0/0`, Validation
Set `66/66`, Inspection Workspace `63/63`, Recipe Manager/PropertyGrid
`37/37`, Shell options `24/24`, code structure `17/17`, Runner parity, and
fresh Wide/Compact capture quality. Preserve
`docs/OPENVISIONLAB_3D_THRESHOLD_MANUAL_CORRECTION_AND_FAILURE_RECORD_20260728.md`
and `artifacts/current/20260728-threshold-manual-correction/`.

`I-08/I-10 explicit threshold Review/Cancel/draft Apply and Held-out replay`
is complete. Exact candidate mappings fail closed against existing typed
Thickness/Warpage parameters. Review is non-mutating; Cancel preserves the
recipe, PropertyGrid, and execution state; candidate Apply changes only the
typed PropertyGrid draft. Ordinary PropertyGrid Apply remains separate.
Explicit Held-out replay projects the proposal onto an immutable recipe copy,
executes only Held-out samples, and saves a portable correction-evidence
sidecar. Workbench and Runner agree on candidate
`threshold.0ad7b16eaa3d4362`, `MinimumThickness 0->2`,
`MaximumThickness 10->4`, four development samples, one Held-out sample, and
the exact Held-out SHA. Current Release evidence passes build `0/0`,
Validation Set `58/58`, Inspection Workspace `63/63`, recipe teaching
`28/28`, Recipe Manager/PropertyGrid `37/37`, Artifact Navigator/Output
Compare `31/31`, Shell smoke options `24/24`, code structure `17/17`, Runner
parity, and current Wide/Compact capture quality. Preserve
`docs/OPENVISIONLAB_3D_THRESHOLD_REVIEW_APPLY_AND_HELD_OUT_REPLAY_20260728.md`
and `artifacts/current/20260728-threshold-review-heldout/`.

The historical next slice named here is now closed by the newer I-09/I-11,
I-12/I-13/I-15, L-11, and H-02/H-03/H-04 checkpoints above.
`H-05/H-06/H-07` is complete in the newer checkpoint above.

`I-06/I-07 threshold candidates and exact error table` is complete.
Explicit-run Good/Bad observations now produce one deterministic Minimum,
Maximum, and Range candidate per eligible step/region metric. Ranking
minimizes total errors, then false accepts, false rejects, and finally uses a
stable tightness rule. Every candidate owns exact sample decisions and
reproducible confusion counts. Held-out observations are recorded as excluded
and never enter boundaries, ranking, counts, or decisions. Workbench exposes
read-only candidate/error tables without editing or executing; Runner emits
the same contract. Current Release evidence passes build `0/0`, Validation
Set `45/45`, Inspection Workspace `63/63`, Shell smoke options `24/24`,
recipe teaching `28/28`, Artifact Navigator/Output Compare `31/31`, code
structure `17/17`, Runner parity with zero Held-out decisions, and current
default/expanded/compact screenshot quality. Preserve
`docs/OPENVISIONLAB_3D_THRESHOLD_CANDIDATES_AND_ERROR_TABLE_20260728.md` and
`artifacts/current/20260728-threshold-candidates/`.

The durable next-chat startup request, current working-tree boundary, full
commercial-video-derived priority train summary, and next
`J-01/J-03/J-04` acceptance boundary are maintained in
`docs/OPENVISIONLAB_3D_NEXT_CHAT_HANDOFF_PROMPT_20260728.md`.

Height Image press-drag-release versus 3D two-point instruction and a focused
Compact ROI teaching surface remain P1 UX items. The owner R0 replay and
physical metrology remain external.

`I-04/I-05 labeled sample evidence` is complete. Each Validation Set sample
has one durable `Good`, `Bad`, or `HeldOut` role in a portable recipe-side
manifest. Explicit Run produces per-step metric distributions plus routed
`GridRectangle` mean raw-height and valid-cell-ratio distributions. Held-out
observations remain visible with `IncludedInDevelopment=false`. Role edits
never execute inspection or dirty the recipe graph; normal save/close state
still protects the sidecar change. Workbench save/reopen restores Pending
roles without stale evidence, and production Runner emits the same contract.
Preserve
`docs/OPENVISIONLAB_3D_LABELED_SAMPLE_EVIDENCE_20260728.md` and
`artifacts/current/20260728-labeled-sample-evidence/`.

`D-05/D-06 Level Surface` is complete. One or more explicit reference
`GridRectangle` ROIs define a least-squares raw-height plane; overlapping
finite cells count once. The derived C3D preserves the source grid and missing
mask while applying
`Y' = Y - fittedPlane(X,Z) + referenceMean`. The typed leveling transform
records source identity, every reference region, residual evidence,
coefficients, the equivalent matrix, provenance, and SHA-256. The authored
RMS gate fails closed. Workbench typed Apply, explicit Preview/Publish,
multi-ROI addition, save/reopen, Viewer/Output Compare, and Runner parity pass
on the known tilted fixture. Preserve
`docs/OPENVISIONLAB_3D_LEVEL_SURFACE_20260728.md` and
`artifacts/current/20260728-level-surface/`.

`D-04 Remove Outlier Pixels` is complete. The typed
`LocalMedianAbsoluteDeviation` preparation rule excludes the center sample,
uses a strict-greater-than threshold, supports odd `3/5/7` windows and an
explicit minimum-neighbor gate, preserves source missing cells, uses available
neighbors at boundaries, and sets detected outliers missing. Data owns one
immutable coordinate-true outlier mask; Tools, Workbench, Viewer, Output
Compare, and Runner share its identity and the derived C3D. The known
`12 x 10` fixture removes exactly `3` cells and changes valid/missing from
`119/1` to `116/4`, while the source hash remains unchanged. Preserve
`docs/OPENVISIONLAB_3D_REMOVE_OUTLIER_PIXELS_20260728.md` and
`artifacts/current/20260728-remove-outlier-pixels/`.

`E-09 OrientedBox3D Viewer outline and pointer handles` is complete. The
persisted schema `1.4` volume now renders as a translucent oriented cuboid
with a rotation ring and fixed-screen-size center, X/Y/Z resize, height, and
local-Y rotation handles. When a projected axis collapses in Top or side
views, its screen-space fallback remains visible and draggable. Viewer
gestures and numeric fields edit one synchronized transient Review candidate;
the global Review bar is the sole visible Apply/Cancel owner, Enter/Esc remain
available, and Apply preserves the selection identity without running
inspection. Real Windows pointer evidence passes Perspective move/X/Y/Z
resize/rotate, Top height resize, and side collapsed-axis resize while recipe,
execution, and gesture camera state remain unchanged. Preserve
`docs/OPENVISIONLAB_3D_ORIENTED_BOX_VIEWER_HANDLES_20260728.md` and
`artifacts/current/20260728-oriented-box-viewer-handles/`.

The E-09 checkpoint preceded the newer D-04 closure above.

The two P0 findings from the current Release operator-video review are closed.
Schema `1.5` stores first/second ROI identities on the owning inspection step,
so deleting Reference cannot promote the surviving Measurement selection.
The shared capture now ends before the role advances, so fresh Height Image
Reference Apply immediately enables Measurement Draw.

External pointer/keyboard replay completes Reference and Measurement
`Missing -> Drawing -> Review -> Applied`, Preview readiness, Ctrl+S, and
save/reopen at Wide and Compact widths. The workflow does not invoke Preview
or Run implicitly. Preserve
`docs/OPENVISIONLAB_3D_DUAL_ROI_ROLE_PRESERVATION_20260728.md`,
`artifacts/current/20260728-dual-roi-role-preservation/`, and the updated
`docs/assets/openvisionlab-3d-roi-workflow.gif`.

That dual-ROI closure was the prerequisite immediately before E-09; the newer
checkpoint above supersedes its former next-item statement.

`E-07/E-08 OrientedBox3D contract and numeric editing` is complete:

- schema `1.4` adds a persisted `oriented-box-3d` selection with center,
  right-handed orthonormal axes, and positive half-extents;
- existing schema `1.3` artifact-owned recipes remain valid and executable;
- the Selected Tool Regions surface owns a numeric MVVM editor with explicit
  New, Apply, Cancel, and guarded Delete;
- Apply preserves identity, changes only the recipe, and never invokes
  Preview, Publish, or Run;
- exact save/reopen, invalid-axis/extent/payload rejection, and old affine
  adapters pass;
- focused evidence passes Release build `0/0`, selection `25/25`, Inspection
  Workspace `60/60`, teaching `28/28`, height measurement `45/45`, Artifact
  Navigator `31/31`, docking `33/33`, Recipe Manager/WPG `37/37`,
  artifact-owned Runner `18/18`, synthetic affine `18/18`, schema `1.3`
  affine `4/4`, schema `1.3` correspondence `5/5`, shell options `21/21`,
  structure `17/17`, and Wide/Compact screenshot quality.

Preserve
`docs/OPENVISIONLAB_3D_ORIENTED_BOX_CONTRACT_AND_NUMERIC_EDITOR_20260728.md`
and `artifacts/current/20260728-oriented-box-contract/`.

That checkpoint was the persisted numeric contract before rendering.
The newer E-09 checkpoint above closes the Viewer outline and pointer handles.

The current Wide and Compact synchronized ROI captures were reviewed against
the v3 and GoPxL-derived interaction contracts. One concrete v3 acceptance
gap was corrected:

- the global Review ribbon is now the only visible primary ROI Apply/Cancel
  owner;
- duplicate Selected Tool and Height Image Apply/Cancel controls and the
  Viewer instruction toast were removed;
- the Viewer no longer repeats the selected-step title, route IDs, output ID,
  or typed-adapter status already owned by the global bar and Selected Tool;
- local ROI capture hides the unrelated Thickness repeat card;
- inline Height Image editing temporarily changes the split to
  `35% 3D / 65% Height Image` and restores the existing ratio afterward;
- compact exact-source evidence improves from `4.2%` to `7.9%`.

Preserve
`docs/OPENVISIONLAB_3D_WORKSPACE_V3_UX_MID_REVIEW_AND_ACCEPTANCE_CORRECTION_20260728.md`
and
`artifacts/current/20260728-workspace-v3-ux-acceptance-correction/`.
Workspace v3 remains `7/8` because R0 is still an external owner replay.

The owner explicitly requested continued development while R0 remains
available only as a later unaided acceptance gate. `C-09/C-10 synchronized
Height Image / 3D ROI editing` is complete:

- Reference cyan and Measurement orange overlays use the same selection ID
  and native-grid rectangle in both views;
- `HeightImageRoiWorkspaceViewModel` owns WPF-neutral 2D projection and
  gestures while the existing Workbench owns lifecycle and recipe mutation;
- Height Image supports draw, move, corner resize, role selection, Review,
  Apply, Cancel, and Delete;
- actual Windows pointer evidence proves the Height Image and 3D transient
  candidates remain equal;
- Review preserves dirty state, steps, selections, routing, applied geometry,
  current output, and camera;
- Apply preserves selection ID and passes save/reopen;
- focused evidence passes build `0/0`, Workspace `50/50`, smoke options
  `21/21`, wide/compact pointer smoke, display `103/103`, Height Image
  `21/21`, docking `33/33`, height measurement `45/45`, recipe teaching
  `28/28`, and structure `17/17`.

Preserve
`docs/OPENVISIONLAB_3D_SYNCHRONIZED_HEIGHT_IMAGE_ROI_EDITING_20260728.md`
and `artifacts/current/20260728-height-image-roi-editing/`.

`C-11 visible invalid/missing-cell overlay` is complete:

- Height Image shows the shared native invalid-cell map in magenta by default
  and exposes a direct view-only toggle;
- the legend reports the exact missing count and percentage;
- valid palette pixels remain unchanged and hiding/re-enabling the overlay is
  deterministic;
- the exact Thickness Coupon v1 source shows `166,764` overlay pixels (`15.5%`) and
  retains mask SHA-256
  `44EDC44DEE6D0193DCCF22130487DC3CF80CCE2F68BDAA854A1D16FAA4BDC358`;
- focused evidence passes build `0/0`, Height Image `25/25`, exact-source
  probe, Inspection Workspace `53/53`, invalid map `15/15`,
  SourceQualityReport `13/13`, docking `33/33`, recipe teaching `28/28`,
  Artifact Navigator `31/31`, height measurement `45/45`, shell options
  `21/21`, structure `17/17`, and Wide/Compact screenshot quality.

Preserve
`docs/OPENVISIONLAB_3D_VISIBLE_INVALID_CELL_OVERLAY_20260728.md` and
`artifacts/current/20260728-invalid-cell-overlay/`.

`C-08 Shared Height Image / 3D cursor` is complete:

- one WPF-neutral presentation session owns source identity, cursor origin,
  native row/column, raw height, valid/missing state, and revision;
- Height Image hover renders the same valid point as a yellow/cyan marker in
  the main 3D Viewer;
- 3D hover renders the same picked C3D point as a Height Image crosshair;
- source mismatch, missing cells, and stale leave events fail closed;
- recipe, execution, output, and camera state remain unchanged;
- the exact Thickness Coupon v1 source proves
  `column 593 / row 800 / H 633.4000244140625 raw-height` in both directions;
- focused evidence passes Inspection Workspace `42/42`, smoke options
  `20/20`, and wide/compact actual-window bidirectional smoke.

Preserve
`docs/OPENVISIONLAB_3D_SHARED_HEIGHT_CURSOR_20260728.md` and
`artifacts/current/20260728-shared-height-hover/`.

`C-07 Height Image palette and display range` is complete:

- Height, Grayscale, and Thermal palettes are first-class Height Image state;
- Auto range uses the finite full-source minimum and maximum;
- numeric Min/Max is fail-closed and requires explicit Apply;
- active range text and a matching color legend remain visible at wide and
  `1280 x 760` widths;
- palette/range changes regenerate only immutable display pixels and preserve
  native coordinates, raw heights, invalid cells, recipe, and execution;
- the exact Thickness Coupon v1 source changes from Auto Height SHA-256
  `6A6C12F7A729ABF49830F07CBB868FCCCB94C987584856128662109BA377B087`
  to Thermal `0..1200 raw-height` SHA-256
  `49FE0B0009CDE14BEE44C40C99F7EC0A6571BBC3DCDF8EDA168943E418F531BF`;
- focused evidence passes Height Image `21/21`, Inspection Workspace `36/36`,
  and wide/compact exact-source non-execution smoke.

Preserve
`docs/OPENVISIONLAB_3D_HEIGHT_IMAGE_DISPLAY_RANGE_20260728.md` and
`artifacts/current/20260728-height-image-display-range/`.

`B-08 Unified Source
Quality workspace` is complete:

- the normal Selected Tool surface presents the current report whenever a
  source is loaded and no inspection step is selected;
- the source card exposes explicit read-only navigation back to quality;
- grid, coverage, height statistics/distribution, invalid-map identity,
  frame/unit/provenance, and actual channel availability are visible;
- exact-source wide/compact smoke proves recipe and execution state remain
  unchanged;
- focused workspace verification passes `18/18` and current wide/compact
  captures pass on attempt 1.

Preserve
`docs/OPENVISIONLAB_3D_SOURCE_QUALITY_WORKSPACE_20260728.md` and
`artifacts/current/20260728-source-quality-workspace/`.

`B-09 Coordinate-true
invalid-cell map` is complete:

- Data owns one immutable row-major LSB-first packed map and stable identity;
- Source Quality and Height Image consume the same map owner;
- synthetic invalid-map verification passes `15/15`, Source Quality
  regression `13/13`, and Height Image regression `14/14`;
- the exact Thickness Coupon v1 Thickness source has `1,075,200` cells, `166,764`
  missing cells, `134,400` packed bytes, and identical Source Quality / Height
  Image mask SHA-256
  `44EDC44DEE6D0193DCCF22130487DC3CF80CCE2F68BDAA854A1D16FAA4BDC358`.

Preserve
`docs/OPENVISIONLAB_3D_INVALID_CELL_MAP_PARITY_20260728.md` and
`artifacts/current/20260728-invalid-cell-map-parity/`.

`C-06 Full-size
coordinate-true Height Image Viewer` is complete:

- Data owns an immutable native-grid frame with
  `pixel X=column / pixel Y=row / no flip / one cell per pixel`;
- the Workbench auxiliary slot defaults to Height Image and still accepts
  existing real source/Filter C3D candidates;
- Fit, 1:1, wheel zoom, middle-drag pan, and exact row/column/raw-height hover
  are view-only;
- the exact Thickness Coupon v1 Thickness source produces `1280 x 840`,
  `1,075,200` pixels, `908,436` valid, and `166,764` missing cells;
- focused mapping passes `11/11`, Workspace non-execution `30/30`, Artifact
  Navigator `31/31`, docking `33/33`, Source Quality `12/12`, and structure
  `17/17`.

Preserve
`docs/OPENVISIONLAB_3D_FULL_HEIGHT_IMAGE_VIEWER_20260727.md` and
`artifacts/current/20260727-full-height-image-viewer/`.

`B-07 SourceQualityReport` is also complete:

- schema `1.0` is WPF-neutral and owned by Core;
- Data calculates exact counts, raw-height statistics/distribution, source
  identity, frame/unit/provenance, and invalid-cell mask identity;
- unsupported C3D intensity/color/depth/normal/confidence/SNR channels are
  explicit and never fabricated;
- Runner verification passes `12/12`;
- the exact Thickness Coupon v1 Thickness source produces a `1280 x 840` report with
  `908,436` valid and `166,764` missing cells.

Preserve
`docs/OPENVISIONLAB_3D_SOURCE_QUALITY_REPORT_20260727.md` and
`artifacts/current/20260727-source-quality-report/`.

### G0 owner acceptance prerequisite

Prerequisite:

- the owner is available at the running current Release application;
- the exact Thickness Coupon v1 C3D and documented 12-step workflow are used;
- no assistant guidance is supplied during the replay.

Pass:

- New -> source -> Thickness -> Reference ROI -> Measurement ROI ->
  parameters -> Preview -> repeat `4 x 2` -> per-Tab review -> Run -> Save ->
  reopen is completed unaided.

Fail:

- the operator cannot discover the next action;
- a visible state does not match the recipe state;
- an ROI cannot be created, corrected, deleted, or applied;
- Save/reopen or Run does not preserve the expected recipe.

Do not recommend or spend model tokens on repeated implementation verification
while this external prerequisite is unavailable.

## Release-train sequence

| Train | Outcome | Entry gate | Exit gate | Recommended model | Reasoning effort |
| --- | --- | --- | --- | --- | --- |
| R0 | Workspace v3 owner acceptance | Current Release evidence passes | Owner completes unaided replay | External owner prerequisite | No model until available |
| R1 | Source Trust and Linked Teaching | R0 accepted | SourceQualityReport and linked Height Image pass current-source UI/Runner gates | `gpt-5.6-sol` | high |
| R2 | Typed 3D Regions and Preparation | R1 accepted | `OrientedBox3D`, invalid/outlier handling, and Level Surface pass round-trip/execution gates | `gpt-5.6-sol` | high |
| R3 | Evidence-Assisted Presence Inspection | R2 accepted | Good/Bad/Held-out threshold teaching and Completeness pass Workbench/Runner replay | `gpt-5.6-sol` | high |
| R4 | Surface Matching Foundation | R1 source trust accepted; R3 need not block prototype | One identified model/scene fixture returns reproducible pose, scores, and overlay | `gpt-5.6-sol` | high |
| R5 | Matching Optimization and Diagnostics | R4 accepted | normals/keypoints/constraints/edge scores/multiple-match review pass | `gpt-5.6-sol` | high |
| R6 | Physical Measurement Credibility | trusted units/calibration artifacts available | documented uncertainty/repeatability gate passes | External data prerequisite first | No model until available |

## Development backlog

### A. Product workflow, navigation, and lifecycle

Recommended model: `gpt-5.6-sol`

Reasoning effort: medium for localized changes, high for cross-workspace state
changes

| ID | Status | Development item | Dependency | Closure evidence |
| --- | --- | --- | --- | --- |
| A-01 | P | Real Setup -> Teach -> Validate -> Results structure, live hosted ownership, automated failure-to-Teach, actionable Teach correction context, Wide/Compact Viewer/ROI recovery, and Results/Advanced return preservation pass; human-owner R0 remains | None | `OPENVISIONLAB_3D_TEACH_FAILURE_CORRECTION_CONTEXT_20260729.md`; current app-only Wide/Compact replay; owner R0 |
| A-02 | C | One synchronized selected step/input/ROI/output/Viewer-slot identity | None | `InspectionWorkspaceSelectionSession` focused verification |
| A-03 | C | Explicit parameter Apply/Discard | None | PropertyGrid verification and recipe non-execution checks |
| A-04 | C | Explicit ROI Review/Apply/Cancel/Delete | None | ROI lifecycle and actual-pointer evidence |
| A-05 | C | Explicit Preview/Publish/Run separation | None | Workbench/Runner verification |
| A-06 | C | Save, Save As, recent recipe, last-recipe startup restoration | None | Recipe Manager and startup verification |
| A-07 | C | Selected output Show/Pin/Compare | None | Artifact Navigator and Output Compare verification |
| A-08 | C | Single, split, stacked, and pop-out Viewer layouts | None | Viewer Workspace verification |
| A-09 | P | Configure/Review/Run state language remains understandable across every tool | A-01 | Owner replay plus cross-tool state-text review |
| A-10 | C | Validate and Results local drill-down retains live content, localized/accessibility navigation, failure-to-Teach routing, and an explicit visible Advanced route after stage recomposition | A-01 | `OPENVISIONLAB_3D_IA4B_OWNER_PATH_REPLAY_20260729.md`; actual Release Wide/Compact video; Window-hosted `52/52` |
| A-11 | N | Consistent per-tool empty, incomplete, stale, ready, running, pass, fail, and error presentation matrix | A-09 | One shared state contract and focused UI verification |
| A-12 | N | Global current-source quality state beside recipe/input state | B-08 | Current-source command-bar capture |
| A-13 | N | Task-specific assistant host using `analyze -> propose -> review -> explicit apply` | H-03 or D-04 | One assistant with Cancel/non-mutation and Apply evidence |
| A-14 | N | In-product first-use checklist limited to current inspection task | R1 | Owner can dismiss/reopen; no permanent journey strip |
| A-15 | P | Keyboard command coverage for common recipe, execution, and ROI actions | None | Existing shortcut verifier plus new Height Image/assistant actions |

### B. Source identity, quality, and provenance

Recommended model: `gpt-5.6-sol`

Reasoning effort: high

| ID | Status | Development item | Dependency | Closure evidence |
| --- | --- | --- | --- | --- |
| B-01 | C | C3D path, byte length, SHA-256, grid width/height identity | None | Current source binding verifier |
| B-02 | C | Local C3D, GLB, STL, LAS, and LAZ loading | None | Existing loader/sample matrix |
| B-03 | C | Asynchronous/cancellable C3D load with previous-source retention on failure | None | Release source-load verification |
| B-04 | C | Valid/missing counts and height distribution | None | Height distribution verification |
| B-05 | C | Frame and declared-unit fields in recipe/result contracts | None | Recipe round-trip and Runner evidence |
| B-06 | P | Bounds, coverage, collision, and missing evidence are distributed across outputs | None | Existing C3D/re-grid summaries |
| B-07 | C | WPF-neutral `SourceQualityReport` contract | B-01, B-04 | Release build `0/0`, headless `12/12`, exact owner-source JSON |
| B-08 | C | Unified Source Quality workspace/panel | B-07 | Release build `0/0`, workspace `18/18`, exact-source wide/compact non-execution smoke and capture quality |
| B-09 | C | Coordinate-true invalid-cell map and mask identity | B-07, C-06 | Release build `0/0`, map `15/15`, Source Quality `13/13`, Height Image `14/14`, exact-source pixel/cell/SHA parity |
| B-10 | N | Grid monotonicity, duplicate locator, non-finite coordinate, and topology diagnostics | B-07 | Deterministic malformed fixtures |
| B-11 | N | Available-channel catalog: height, intensity, color, depth, normal, confidence/SNR | B-07 | Unsupported channels visibly unavailable, never fabricated |
| B-12 | N | Acquisition/source provenance text and limitation notes | B-07 | Saved/reopened provenance without execution |
| B-13 | N | Source quality gate consumed by compatible-tool suggestions | B-07 | Invalid source disables only unsupported tools with exact reason |
| B-14 | N | Before/after quality delta for each preparation output | D-01 | Derived artifact report with valid/missing/outlier changes |
| B-15 | P | Normal inspection for imported mesh pick exists only at one selected surface point | None | Current mesh pick normal overlay |
| B-16 | N | Dense normal availability/consistency report when source supports normals | B-11 | Known-normal synthetic fixture |
| B-17 | N | Source limitation flags for reflective, transparent, textureless, clipped, or low-coverage acquisition | B-12 | Operator-authored or imported flags persist in recipe/session evidence |

### C. Linked Height Image, 3D Viewer, and diagnostic views

Recommended model: `gpt-5.6-sol`

Reasoning effort: high

| ID | Status | Development item | Dependency | Closure evidence |
| --- | --- | --- | --- | --- |
| C-01 | C | Surface is the default C3D geometry | None | Viewer display verification |
| C-02 | C | Perspective and true X/Z Top orthographic projection | None | Camera math and display verification |
| C-03 | C | Orbit, left/empty-space behavior, middle/right pan, wheel zoom, Fit all, Fit ROI | None | Actual pointer verification |
| C-04 | C | Height palette and full-source distribution legend | None | Height distribution verification |
| C-05 | P | Small read-only linked Height Map preview | None | Current Shell linked-view contract |
| C-06 | C | Full-size coordinate-true Height Image Viewer | B-07 | Native-grid `11/11`, exact owner-source probe, current Release inline/pop-out evidence |
| C-07 | C | Height Image pan, zoom, fit, palette, and numeric range controls | C-06 | Release build `0/0`, Height Image `21/21`, Workspace `36/36`, exact-source wide/compact manual-range smoke |
| C-08 | C | Shared hover row/column/raw-height between Height Image and 3D | C-06 | Release build `0/0`, Workspace `42/42`, smoke options `20/20`, exact-source wide/compact bidirectional smoke |
| C-09 | C | Shared selected ROI and role colors between Height Image and 3D | C-06, E-01 | Same selection ID and geometry in both views; Workspace `50/50`; current Release wide/compact evidence |
| C-10 | C | Height Image ROI draw/move/resize/delete/review/apply/cancel | C-09 | Actual Windows pointer Review and Apply/save/reopen; recipe non-execution before Apply |
| C-11 | C | Invalid/missing mask overlay in Height Image | B-09, C-06 | Pixel count matches SourceQualityReport |
| C-12 | P | Height range palette selection exists in the 3D display | C-04 | Current height distribution |
| C-13 | N | Manual/auto display range in both linked views without recipe mutation | C-06 | View-only state contract |
| C-14 | C | Height profile and endpoint interaction | None | Profile UI/pointer verification |
| C-15 | N | Linked crosshair/profile line between Height Image, Profile, and 3D | C-06, C-14 | One coordinate identity across three views |
| C-16 | P | Intensity/color/depth display varies by file type and available source data | B-11 | Current GLB/LAS color plus C3D height evidence |
| C-17 | N | First-class diagnostic map selector driven by available channels | B-11 | Channel-specific view with unavailable reasons |
| C-18 | N | Normal map/normal-vector diagnostic mode when source supports normals | B-16 | Known-normal fixture and no fabricated C3D normals |
| C-19 | C | Viewer split/stack/pop-out with independent cameras | None | Viewer Workspace verification |
| C-20 | P | Per-Viewer source/output pinning exists for renderable artifacts | None | Current Output Compare/Viewer Workspace |
| C-21 | N | Per-Viewer diagnostic channel, palette, overlay, and linked-camera options | C-17 | Two real Viewers with independent and linked states |
| C-22 | C | Screenshot capture and evidence artifact | None | Existing Viewer/Shell smoke captures |

### D. Height-field and point-cloud preparation

Recommended model: `gpt-5.6-sol`

Reasoning effort: high

| ID | Status | Development item | Dependency | Closure evidence |
| --- | --- | --- | --- | --- |
| D-01 | C | Median Filter creates a separate `FilteredHeightField` | None | Filter adapter and Runner verification |
| D-02 | C | Missing mask preserved and available-neighbor boundary policy | None | Filter contract verification |
| D-03 | P | ROI/Crop is cataloged, while full preparation-output workflow is incomplete | E-01 | Typed output identity and Runner execution |
| D-04 | C | Remove Outlier Pixels tool with explicit rule and mask evidence | B-09 | Known outlier fixture, before/after counts, Viewer/Runner parity |
| D-05 | C | Level Surface from one or more explicit reference ROIs | F-01, C-06 | Tilted fixture levels with residual and fail-closed gate evidence |
| D-06 | C | Preserve leveling transform as typed output, not hidden image mutation | D-05 | Save/reopen, Workbench/Viewer, and Runner transform parity |
| D-07 | N | Reduce Domain/Mask tool | E-11, D-03 | Outside cells remain missing in derived output |
| D-08 | N | Height-threshold background removal | B-07 | Known foreground/background fixture |
| D-09 | N | Saved-background identity and subtraction | B-01 | Background SHA, aligned grid, delta output, mismatch rejection |
| D-10 | N | Distance-based point-cloud background filter | B-07 | Synthetic separated cloud fixture |
| D-11 | N | Region-growing component preparation | G-11 | Known connected-region fixture |
| D-12 | P | Display render-density sampling exists but does not change inspection data | None | Current Viewer density contract |
| D-13 | N | Typed point-cloud voxel/grid downsample with source/result separation | B-07 | Count reduction, bounds tolerance, deterministic hash |
| D-14 | N | Normal calculation/validation preparation when algorithm and licensing are approved | B-16 | Known analytic surface fixture |
| D-15 | C | Full-XYZ affine apply and explicit re-grid | None | A1/A2/A3 verification |
| D-16 | C | Re-grid reports coverage, missing cells, and collisions | None | Re-grid verification |
| D-17 | N | Preparation chain quality comparison view | B-14, C-19 | Source and prepared output shown with numeric quality delta |
| D-18 | N | Preparation presets as editable drafts, never automatic execution | A-13 | Analyze/propose/review/apply contract |

### E. Typed selections and region artifacts

Recommended model: `gpt-5.6-sol`

Reasoning effort: high

| ID | Status | Development item | Dependency | Closure evidence |
| --- | --- | --- | --- | --- |
| E-01 | C | `GridRectangle` X=column/Z=row footprint | None | Schema `1.3` recipe verification |
| E-02 | C | PointSet(2), PointSet(3), and landmark correspondence selections | None | Existing feature/datum verification |
| E-03 | C | Reference and Measurement role ownership | None | Dual-ROI Thickness/Flatness verification |
| E-04 | C | GridRectangle Review state and same-ID replacement | None | ROI lifecycle verification |
| E-05 | C | Numeric row/column/count editing | None | Teaching verification |
| E-06 | C | `4 x 2` repeat-grid display review and ordinary-step Apply | None | Repeat authoring `20/20` |
| E-07 | C | New `OrientedBox3D` selection kind | B-05 | Core/Data schema and validator verification |
| E-08 | C | `OrientedBox3D` center, axes, and half-extents numeric editor | E-07 | Round-trip and invalid-axis rejection |
| E-09 | C | Top/side/perspective move, resize, rotate, and height handles | E-07, C-06 | `docs/OPENVISIONLAB_3D_ORIENTED_BOX_VIEWER_HANDLES_20260728.md`; actual Windows pointer Perspective/Top/side evidence |
| E-10 | N | Distinguish view-only GridRectangle overlay Y from persisted volume extent | E-07 | UI wording and contract verification |
| E-11 | N | Region artifact output that downstream tools can consume | E-07 or E-01 | Typed route and Artifact Registry evidence |
| E-12 | N | Region-source relationship and transform propagation | E-11, F-05 | Same physical region after typed alignment |
| E-13 | N | Per-tool declaration of supported selection kinds and roles | E-07 | Compatible-tool matrix and fail-closed validator |
| E-14 | N | GridCircle selection for circular 2D height-field regions | C-06, E-13 | Draw/numeric/save/Runner evidence |
| E-15 | N | GridPolygon selection for irregular masks | C-06, E-13 | Vertex edit/save/mask output evidence |
| E-16 | N | Convert selected connected region into editable region artifact | G-11, E-11 | Detection output -> editable derived region without source mutation |

### F. Feature, datum, alignment, and coordinate frames

Recommended model: `gpt-5.6-sol`

Reasoning effort: high

| ID | Status | Development item | Dependency | Closure evidence |
| --- | --- | --- | --- | --- |
| F-01 | C | 3-Point Plane and plane-fit primitives | None | Existing feature and measurement verification |
| F-02 | C | Height Difference Edge | None | Edge adapter/diagnostics |
| F-03 | C | 2-Point Line and deterministic 3D Line Fit | None | Existing feature verification |
| F-04 | C | Line Intersection/CornerAnchor | None | Existing intersection verification |
| F-05 | C | Landmark Correspondence -> XYZ Affine Solve -> Apply -> Re-grid | None | Current A1/A2/A3 chain |
| F-06 | P | Manual/deterministic alignment exists; general object alignment does not | None | Current affine and fixed nominal/actual evidence |
| F-07 | N | Level-frame artifact derived from explicit surface ROIs | D-05 | Transform identity and residual evidence |
| F-08 | N | 2D height-image border/feature alignment for moving parts | C-06, F-02 | Known translated/rotated height-image fixture |
| F-09 | N | Rigid point-pair/manual alignment distinct from full affine | E-02 | Known rigid transform and Runner parity |
| F-10 | N | Constrained best-fit alignment policy | F-09 | Synthetic known-transform fixture and failure gates |
| F-11 | N | Alignment confidence/residual/coverage evidence | F-07 or F-10 | Explicit metric and acceptance state |
| F-12 | N | Named coordinate-frame hierarchy and visible transform chain | F-07 | Source/reference/result frame display |
| F-13 | N | Symmetry declaration for later matching | J-01 | Saved model contract and validation |
| F-14 | N | Allowed pose/rotation/search range contract | J-01 | Invalid range rejection and saved parameters |
| F-15 | E | Physical calibration frame and traceable unit validation | Trusted calibration artifact | Independent physical evidence |

### G. Deterministic measurement and inspection tools

Recommended model: `gpt-5.6-sol`

Reasoning effort: medium for one established-rule adapter, high for new
geometry or numerical policy

| ID | Status | Development item | Dependency | Closure evidence |
| --- | --- | --- | --- | --- |
| G-01 | C | Dual-surface Thickness | E-01 | Reference fit, signed separation, limits, overlays |
| G-02 | C | Warpage | E-01 | P2V/RMS/valid-sample evidence |
| G-03 | C | Plane Flatness | F-01 | Reference/measurement residual evidence |
| G-04 | C | Point Pair Dimensions | E-02 | distance/width/height/angle evidence |
| G-05 | C | Gap/Flush | E-01 | signed gap and flush evidence |
| G-06 | C | Volume | F-01, E-01 | reference-plane integrated volume evidence |
| G-07 | C | Cross-section Dimensions | E-01 | width/height-range evidence |
| G-08 | C | Datum Plane Raw-Height Deviation | F-01 | P2V/RMS overlays |
| G-09 | C | Min/max/tolerance parameters and Pass/Fail/Error results | None | PropertyGrid and Runner evidence |
| G-10 | C | Per-step metrics and overlays | None | Artifact Registry, Validation Set, Run Record |
| G-11 | N | Connected Region / Blob Finder for height-field masks | D-04, C-06 | Known connected-component fixture |
| G-12 | N | Region count, area, center, orientation, and bounding artifact outputs | G-11 | Per-region metrics and selected overlay |
| G-13 | N | Presence Check using explicit height/coverage features | G-11 or E-07 | Good/present and missing fixtures |
| G-14 | N | Fill Height per region against a reference surface | D-05, E-01 | Known fill-level synthetic fixture |
| G-15 | N | Aggregate `all regions accepted` result preserving per-region evidence | G-13 | Aggregate and child status parity |
| G-16 | P | Width/height/area outputs exist across several tools but not one detected-region dimension tool | G-12 | One region-dimension adapter |
| G-17 | N | Selected output enable/disable policy stored in recipe when execution semantics require it | G-10 | Disabled output remains declared and non-fabricated |
| G-18 | N | Tool-specific help example and expected overlay for every new tool | A-11 | Localized help and screenshot gate |

### H. Completeness, repeated cells, and presence workflow

Recommended model: `gpt-5.6-sol`

Reasoning effort: high

| ID | Status | Development item | Dependency | Closure evidence |
| --- | --- | --- | --- | --- |
| H-01 | C | `4 x 2` repeated Tab ROI authoring | E-06 | Existing repeat authoring evidence |
| H-02 | C | Completeness tool with rows, columns, pitch, and cell-shape contract | C-06, E-01 | Deterministic grid generation |
| H-03 | C | Per-cell finite-coverage metric | H-02, B-07 | Known missing-cell fixture |
| H-04 | C | Per-cell height statistic relative to reference | H-02, D-05 | Known height fixture |
| H-05 | C | Per-cell presence threshold and Pass/Fail | H-03, H-04 | Workbench/Runner parity |
| H-06 | C | Failed-cell count and aggregate completeness result | H-05 | Aggregate equals child statuses |
| H-07 | C | Per-cell colored overlay and stable cell identity | H-02 | Height Image and 3D display |
| H-08 | C | Previous/next failed-cell navigation | H-07, K-08 | UI selection verification |
| H-09 | N | Use detected/oriented region artifact as completeness input | E-11, G-12 | Typed upstream route |
| H-10 | C | Map existing Tab 1..8 names to cell results without replacing ordinary Thickness steps | H-02 | Stable recipe and output identities |
| H-11 | C | Good/bad completeness examples in Validation Set | I-01, H-05 | Two Good Pass, two Bad Fail, one separate Held-out Pass |
| H-12 | C | Completeness assistant that proposes height/coverage thresholds | I-04 | Exact sample/cell error table, draft Apply, development gate, Held-out replay |

### I. Sample evidence, threshold teaching, and correction

Recommended model: `gpt-5.6-sol`

Reasoning effort: high

| ID | Status | Development item | Dependency | Closure evidence |
| --- | --- | --- | --- | --- |
| I-01 | C | Validation Set stages same-grid C3D samples without running on add | None | Current validation verification |
| I-02 | C | Explicit Run across samples with progress/cancel | None | Current Validation Set UI |
| I-03 | C | Pass/Fail/Error filters, issue navigation, per-step metrics/overlays | None | Current failure-analysis verification |
| I-04 | C | Assign `Good`, `Bad`, and `Held-out` sample roles | I-01 | Role persistence without source mutation |
| I-05 | C | Per-step and per-region metric distribution over labeled samples | I-04 | Reproducible statistics |
| I-06 | C | Candidate threshold generation for one or two scalar limits | I-05 | Deterministic candidate set |
| I-07 | C | Confusion/error table with exact supporting sample IDs | I-06 | Counts reproduce from raw sample results |
| I-08 | C | Explicit threshold suggestion Review/Cancel/Apply | I-06, A-13 | Cancel non-mutation; Apply updates draft only |
| I-09 | C | Manual parameter correction after suggestion | I-08 | Ordinary PropertyGrid Apply commits values distinct from suggestion |
| I-10 | C | Held-out replay gate after applied correction | I-04, I-08 | Held-out data excluded from suggestion and then replayed |
| I-11 | C | Failure -> correction -> held-out evidence record | I-10 | Durable exact before/suggested/manual/after/Held-out record |
| I-12 | C | Sample balance, overlap, and insufficient-evidence warnings | I-05 | Release build `0/0`, Validation Set `72/72`, Runner contract `2.0`, controlled missing/imbalanced/overlap sets |
| I-13 | C | Threshold assistant for Thickness/Warpage first | I-08 | Explicit five-entry mapping matrix, Thickness end-to-end correction, Warpage typed proposal verification |
| I-14 | C | Threshold assistant for Presence/Completeness second | H-05, I-08 | Contract `2.1`; three exact Completeness mappings with worst-cell evidence |
| I-15 | C | Never auto-run or auto-apply after sample role/threshold edits | I-08 | Pending/evidence state plus Review/draft/manual/PropertyGrid command verification |

### J. Surface-model matching foundation

Recommended model: `gpt-5.6-sol`

Reasoning effort: high

| ID | Status | Development item | Dependency | Closure evidence |
| --- | --- | --- | --- | --- |
| J-01 | N | Identified `SurfaceModel` artifact contract | B-05 | Save/load and content identity |
| J-02 | P | Mesh import and fixed nominal/actual comparison exist, but are not a matching model | None | Current mesh/nominal evidence |
| J-03 | N | Model preparation step with sampling parameters | J-01 | Deterministic sampled-model hash |
| J-04 | N | Model point/triangle/normal validity checks | J-03, B-16 | Known-valid and invalid model fixtures |
| J-05 | N | Remove internal/redundant/unobservable model surfaces | J-03 | Controlled model-preparation comparison |
| J-06 | N | Scene preparation contract tied to SourceQualityReport | B-07 | Explicit prepared-scene identity |
| J-07 | N | Model key-point artifact and debug overlay | J-03 | Stable key-point count/identity |
| J-08 | N | Pose-search executor returning rigid pose | J-03, J-06 | Known-pose synthetic fixture |
| J-09 | N | Explicit surface-coverage score semantics | J-08 | Occluded fixture with documented expected range |
| J-10 | N | Transformed-model scene overlay | J-08, C-19 | Workbench and screenshot evidence |
| J-11 | N | Match Pass/Fail limits distinct from raw score display | J-09 | PropertyGrid/Runner evidence |
| J-12 | N | Multiple-match result collection with stable identities | J-08 | Known two-object fixture |
| J-13 | N | Symmetry-aware pose equivalence | F-13, J-08 | Symmetric fixture |
| J-14 | N | Bounded translation/rotation/search domain | F-14, J-08 | Runtime and false-positive comparison |
| J-15 | N | Matcher runtime and rejection reason evidence | J-08 | Per-stage timing and fail-closed reason |
| J-16 | N | Workbench/Runner pose, score, overlay, and hash parity | J-08 | Focused execution verification |

### K. Edge-supported matching and advanced diagnostics

Recommended model: `gpt-5.6-sol`

Reasoning effort: high

| ID | Status | Development item | Dependency | Closure evidence |
| --- | --- | --- | --- | --- |
| K-01 | C | Height Difference Edge, Line Fit, and Line diagnostics exist as inspection features | None | Current feature evidence |
| K-02 | N | Model 3D edge extraction for a SurfaceModel | J-03 | Stable model-edge artifact |
| K-03 | N | Scene 3D edge extraction for matching | J-06 | Stable scene-edge artifact |
| K-04 | N | Acquisition viewpoint/direction metadata for edge orientation | B-12 | Explicit available/unavailable state |
| K-05 | N | Normal/edge-direction diagnostic overlay | B-16, K-02 | Known outward-normal fixture |
| K-06 | N | Separate surface and 3D-edge match scores | J-08, K-02, K-03 | False background match fixture |
| K-07 | N | Independent thresholds for score components | K-06 | PropertyGrid and Runner evidence |
| K-08 | N | False-positive review with original scene, samples, model, pose, and scores | K-06 | One retained rejected/accepted comparison |
| K-09 | N | Multiple-match issue navigation | J-12 | Previous/next match selection |
| K-10 | N | Matching parameter experiment comparison without changing current published result | J-15 | Preview candidates and explicit Publish |
| K-11 | N | Matching performance budget over fixed fixtures | J-15 | Release timing matrix |
| K-12 | O | Calibrated 2D intensity or extra-camera fusion in current phase | Separate scope approval | Not scheduled |

### L. Results, validation, reporting, and diagnostics

Recommended model: `gpt-5.6-sol` for multi-file result-state work

Reasoning effort: medium

Use `gpt-5.6-terra` with low effort for narrow documentation/export-path
verification only.

| ID | Status | Development item | Dependency | Closure evidence |
| --- | --- | --- | --- | --- |
| L-01 | C | Selected output identity, freshness, value, unit, and status | None | Selected Tool output verification |
| L-02 | C | Show/Pin/Compare without fabricating a surface | None | Artifact Navigator verification |
| L-03 | C | Per-step metrics, overlays, status, message, and output SHA | None | Ordered Runner and Validation Set |
| L-04 | C | JSON/HTML/CSV Run Record | None | General Run Record verification |
| L-05 | C | Recent Run Record open and collision-safe bundle export | None | Run Record history verification |
| L-06 | C | Viewer/Runner comparison evidence | None | Current parity gates |
| L-07 | C | Local structured session logging | None | Logging verification |
| L-08 | P | Performance timing exists in reports/diagnostics but is not uniform for every future stage | None | Current performance evidence |
| L-09 | N | Standard per-step stage timing contract for preparation and matching | D-04 or J-08 | Timing fields in UI and Run Record |
| L-10 | N | Source Quality report included in Run Record | B-07 | Same source-quality identity in UI/Runner |
| L-11 | C | Threshold-correction evidence included in Run Record | I-11 | Schema `1.5`, exact before/suggested/manual/development/Held-out JSON/HTML and Workbench parity, `10/10` fail-closed projection checks |
| L-12 | N | Completeness per-cell result export | H-06 | HTML/CSV child rows |
| L-13 | N | Surface-match pose/score component export | J-16 | JSON/HTML/CSV parity |
| L-14 | N | One support/diagnostic bundle for recipe, log excerpt, source identity, quality report, and current result | B-07 | Bundle manifest and missing-sensitive-data policy |
| L-15 | P | Validation is local ordered sample replay, not production batch/history | None | Current boundary retained |
| L-16 | O | Plant database, long-term trend/SPC service, and retention policy | Product-scope decision | Not scheduled |

### M. Reliability, architecture, and verification

Recommended model: `gpt-5.6-sol`

Reasoning effort: medium for established focused tests, high for numerical,
renderer, or cross-module state changes

| ID | Status | Development item | Dependency | Closure evidence |
| --- | --- | --- | --- | --- |
| M-01 | C | 12-project solution parity and executable structure guard | None | `verify-code-structure.ps1` |
| M-02 | C | Core/Data/Tools/Runner remain runtime-neutral | None | Structure guard |
| M-03 | C | MVVM selected-tool, selection, Viewer workspace, and output-compare owners | None | Focused non-WPF verification |
| M-04 | C | WPF dialogs, AvalonDock, PropertyGrid flush, OpenGL, and pointer behavior remain View adapters | None | Code rules and structure evidence |
| M-05 | C | Current C3D GPU VBO/IBO and staged LOD performance baseline | None | Release matrix and pointer verification |
| M-06 | C | Current-source before/after screenshot discipline | None | Existing artifact checkpoints |
| M-07 | C | Deterministic synthetic whole-chain fixture | None | Synthetic Affine Plate verification |
| M-08 | C | Exact Thickness Coupon v1 Tab Thickness self-test | None | Generated model/Runner `8/8` |
| M-09 | N | SourceQualityReport malformed/edge-case fixture suite | B-07 | Finite/missing/topology cases |
| M-10 | C | Height Image coordinate and pointer verification suite | C-06 | Native-grid/hover checks, actual Windows pointer Review, 2D/3D edit parity, Apply/save/reopen, and Wide/Compact current-source evidence pass |
| M-11 | N | Cross-view selection atomicity suite | C-09 | No duplicate selection or execution |
| M-12 | N | `OrientedBox3D` schema/geometry/pointer/Runner suite | E-07 | Round-trip and degenerate-axis cases |
| M-13 | N | Preparation-tool before/after hash and source-immutability suite | D-04 | One suite per typed preparation tool |
| M-14 | N | Good/Bad/Held-out split and no-leakage suite | I-04 | Held-out excluded from suggestions |
| M-15 | N | Completeness known-cell golden suite | H-02 | Expected per-cell result matrix |
| M-16 | N | Surface-matching known-pose and false-positive suite | J-08 | Pose/score/rejection goldens |
| M-17 | N | Release performance matrix for full-size Height Image and matching | C-06 or J-08 | Fixed viewport/source repeated runs |
| M-18 | N | Accessibility names/tooltips for new icon-only or ambiguous controls | Each UI item | Automation-name verification |
| M-19 | N | Localization coverage for all new user-visible states | Each UI item | Korean/English current-source captures |
| M-20 | E | Owner unaided acceptance for every major workflow train | Current Release application | Owner replay record |

### N. Physical measurement credibility

Prerequisite first:

- trusted calibrated source data;
- declared unit and traceable scale;
- reference artifact or calibration procedure;
- repeat acquisition samples;
- production tolerance owner decision.

Do not recommend model spending until the required physical evidence exists.

| ID | Status | Development item | Dependency | Closure evidence |
| --- | --- | --- | --- | --- |
| N-01 | P | Software calibration capability and repeatability views exist | None | Current calibration center evidence |
| N-02 | E | Verify physical datum definition for Thickness | Owner/metrology input | Approved datum document |
| N-03 | E | Verify C3D raw-height to physical-unit mapping | Calibration data | Independent scale check |
| N-04 | E | Establish traceability chain | Calibration artifact | Traceability record |
| N-05 | E | Measurement uncertainty budget | N-02 to N-04 | Reviewed uncertainty document |
| N-06 | E | Gauge R&R / repeatability and reproducibility | Repeated operator/hardware data | GR&R result |
| N-07 | E | Production tolerance and guard-band decision | Process owner | Approved acceptance limits |
| N-08 | E | Certified claim wording and report boundary | N-05 to N-07 | Owner/legal/metrology review |

## Deliberately deferred commercial-platform features

These were visible in the supplied videos but are not current backlog items.
They require an explicit product-scope change.

| ID | Status | Deferred capability | Reason |
| --- | --- | --- | --- |
| O-01 | O | Camera discovery and connection | Current product is file-first |
| O-02 | O | Exposure, projector, acquisition-frame, and filter control | Sensor-specific acquisition scope |
| O-03 | O | Trigger Scan and Free Run | Would introduce live hardware/runtime state |
| O-04 | O | Assisted capture settings and SDK export | Acquisition product scope |
| O-05 | O | Multi-sensor grouping and alignment | Hardware system scope |
| O-06 | O | Stereo camera calibration and disparity reconstruction | Separate reconstruction product |
| O-07 | O | Pairwise/fusion reconstruction engine | Separate algorithm/runtime scope |
| O-08 | O | Encoder, conveyor, and motion synchronization | Production integration scope |
| O-09 | O | Ethernet/IP, Profinet, Modbus, ASCII, and PLC outputs | Industrial control scope |
| O-10 | O | Robot pose/gripping integration | Robot application scope |
| O-11 | O | HMI deployment | Runtime/platform scope |
| O-12 | O | Cloud, accounts, plant health, and fleet management | Platform scope |
| O-13 | O | Production database, retention, and SPC service | Platform/data-governance scope |
| O-14 | O | Arbitrary AI anomaly training | Conflicts with current deterministic rule target |

## How one development item is executed

Every `P` or `N` item must follow this loop.

### Step 1. Rebuild current status

- run `git status --short` and `git log --oneline -5`;
- read `AGENTS.md`, the current handoff, this master backlog, and the owning
  feature document;
- confirm that no newer evidence has completed, blocked, or reordered the
  item.

### Step 2. Select one bounded item

- select the first incomplete item whose dependencies pass;
- name included behavior;
- name excluded behavior;
- name external prerequisites;
- do not combine unrelated backlog IDs merely because they touch one screen.

### Step 3. Define evidence before code

Record:

- exact source/sample identity;
- typed input;
- typed output;
- parameter/selection ownership;
- expected success result;
- at least one expected failure/rejection;
- Workbench and Runner evidence requirement;
- UI before/after requirement when visible behavior changes;
- claim boundary.

### Step 4. Establish a real responsibility owner

Prefer:

- Core contract for durable identity and result types;
- Data adapter for file/serialization/source binding;
- Tools service/rule for deterministic preparation or inspection;
- Runner adapter for headless execution;
- non-WPF ViewModel/session for presentation state;
- WPF/OpenGL View only for dialogs, hosting, rendering, and pointer input.

Do not use a new partial file as the architectural boundary.

### Step 5. Preserve explicit lifecycle

Unless a newer approved product contract says otherwise:

- selection, visibility, view mode, palette, and layout never run inspection;
- parameter/ROI Apply changes the recipe or draft only;
- Preview calculates temporary selected-step evidence;
- Publish promotes an eligible current Preview;
- Run executes the recipe;
- Validation Set runs only through its explicit command;
- Save never silently runs inspection.

### Step 6. Implement in dependency order

For a new typed feature:

```text
Core contract
  -> validation and serialization
  -> deterministic Tools rule/service
  -> Runner execution
  -> ViewModel/session state
  -> View/render/pointer adapter
  -> commands and localization
```

### Step 7. Verify at meaningful checkpoints

Minimum:

- focused contract/math test;
- invalid/failure case;
- recipe save/reopen if persisted;
- Workbench/Runner parity if executable;
- source immutability;
- no hidden Preview/Run;
- current Release build for UI work;
- fresh wide and compact captures for visible changes;
- code-structure guard for ownership changes.

### Step 8. Owner replay when workflow changes

- use the current built EXE;
- provide the task goal, not click-by-click guidance;
- record where the operator hesitates or fails;
- reopen the item only when a stated acceptance criterion fails.

### Step 9. Durable closure

Update:

- owning feature document;
- this backlog item status;
- `AGENTS.md` current product target;
- next-session handoff;
- current artifact folder.

Use exactly one state:

```text
Status: Complete | Blocked | Incomplete
Scope:
Acceptance criteria:
Verification:
Evidence:
Boundary / next dependency:
```

## Definition of Done for a product slice

A slice is `Complete` only when:

- its selected backlog IDs are explicit;
- every dependency is satisfied;
- typed inputs/outputs and ownership are recorded;
- success and failure cases pass;
- recipe/source/result identity remains stable;
- explicit execution boundaries pass;
- focused verification passes;
- Workbench and Runner agree where execution exists;
- current-source UI evidence exists when visible behavior changed;
- no unresolved core TODO remains;
- the durable handoff names the next eligible item;
- the result does not overclaim physical calibration or commercial-platform
  scope.

## Current executable queue

R0 remains an external owner acceptance gate. `B-07`, `C-06`, `B-09`,
`B-08`, `C-07`, `C-08`, `C-09`, `C-10`, `C-11`, `E-07`, and `E-08` were
completed ahead of that gate by explicit owner direction. The remaining order
below follows typed dependencies; linked teaching can now consume the report,
Height Image, coordinate-true invalid-cell overlay, explicit Height Image
display range, shared native-grid cursor and ROI, and the persisted numeric
volume contract.

Execute only one queue item at a time.

1. Human-owner Wide/Compact R0 replay | Prerequisite: owner operates the
   current Release unaided |
   Recommended model: none until the owner evidence exists | Reasoning
   effort: none
2. `J-01/J-03/J-04 SurfaceModel preparation foundation` | Prerequisite:
   owner R0 passes | Recommended model:
   `gpt-5.6-sol` | Reasoning effort: `high`
3. `J-06/J-08/J-09 scene matching, pose, and score` | Recommended model:
   `gpt-5.6-sol` | Reasoning effort: `high`
4. `J-10/J-16 overlay and Workbench/Runner parity` | Recommended model:
   `gpt-5.6-sol` | Reasoning effort: `high`
5. `K-02/K-03/K-06 edge-supported score components` | Recommended model:
   `gpt-5.6-sol` | Reasoning effort: `high`
6. `K-08/K-11 false-positive review and performance gate` | Recommended model:
   `gpt-5.6-sol` | Reasoning effort: `high`

## Documentation decision

This master backlog supersedes short feature lists in older commercial-review
documents when selecting future work. Older documents remain evidence for
their completed historical slices.

Do not mark a backlog item `C` based only on:

- a catalog label;
- a mock-up;
- a screenshot without behavior verification;
- XML/JSON validity alone;
- one successful execution without semantic result evidence;
- an adjacent feature with a similar name.

## Completion record

Status: Complete

Scope: Commercial-video direction expanded into a complete product workflow,
release-train order, detailed status inventory, dependency graph, evidence
gates, execution loop, Definition of Done, explicit deferred scope, and a
20-item first executable queue. At creation, the inventory contained 234
unique items: 65 Complete, 17 Partial, 127 New, 9 External prerequisite, and
16 Out of scope. Current execution status is maintained in the inventory
table above.

Acceptance criteria:

- full workflow from source identity through persistence -> pass;
- developed/partial/new/external/out-of-scope classification -> pass;
- commercial-video-derived functional backlog -> pass;
- dependencies and closure evidence per item -> pass;
- release-train and per-item development workflow -> pass;
- future-chat selection and durable closure rules -> pass.

Verification: Cross-checked against the current tool catalog, recipe selection
kinds, Viewer/Height Map state, Validation Set, current commercial-video
analysis, Inspection Workspace v3 handoff, and current completion documents.
Markdown whitespace and referenced handoff links are checked in the owning
documentation task.

Evidence:

- this document;
- `docs/OPENVISIONLAB_3D_COMMERCIAL_VIDEO_DIRECTION_AND_PRIORITY_20260727.md`;
- `artifacts/current/20260727-commercial-video-direction/`;
- current `AGENTS.md` and next-session handoff.

Boundary / next dependency: This planning task is complete. R0 remains an
external owner replay prerequisite. Physical metrology and deferred
commercial-platform scope remain unverified or out of scope.
