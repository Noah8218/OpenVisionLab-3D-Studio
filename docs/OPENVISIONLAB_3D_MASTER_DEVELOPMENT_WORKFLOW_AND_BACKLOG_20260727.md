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
- `docs/OPENVISIONLAB_3D_NEXT_SESSION_HANDOFF.md`;
- `docs/OPENVISIONLAB_3D_COMMERCIAL_VIDEO_DIRECTION_AND_PRIORITY_20260727.md`.

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
| Complete `C` | 77 |
| Partial `P` | 17 |
| New `N` | 115 |
| External prerequisite `E` | 9 |
| Out of scope `O` | 16 |
| Total | 234 |

## Current maturity and first gate

- Inspection Workspace v3 is `7/8` bounded slices (`87.5%`) complete.
- The remaining v3 gate is the owner's unaided exact-source replay.
- The current local deterministic recipe/measurement foundation is
  operational.
- The coordinate-true full-size Height Image display foundation is complete.
  Shared 2D/3D native-coordinate hover, synchronized `GridRectangle` ROI
  teaching, and the visible invalid-cell overlay are complete. Typed
  preparation, completeness/presence, good/bad threshold teaching, and
  surface matching remain incomplete.
- Physical calibration, traceability, uncertainty, GR&R, and production
  tolerance are unverified.

### Current execution checkpoint - 2026-07-28

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

The next implementation item is `E-09` OrientedBox3D Viewer outline and
pointer handles. Height Image press-drag-release versus 3D two-point
instruction and a focused Compact ROI teaching surface remain P1 UX items.
The owner R0 replay and physical metrology remain external.

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

This is the persisted numeric contract, not the rendered or pointer-editable
box. `E-09` is next.

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
- the exact Synthetic Thickness Coupon v1 source shows `166,764` overlay pixels (`15.5%`) and
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
- the exact Synthetic Thickness Coupon v1 source proves
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
- the exact Synthetic Thickness Coupon v1 source changes from Auto Height SHA-256
  `D6B402B870622F25C73C10C6D312DF1BB8EC837BC3EFC7A9B5BA8FB8EF432C4A`
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
- the exact Synthetic Thickness Coupon v1 Thickness source has `1,075,200` cells, `166,764`
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
- the exact Synthetic Thickness Coupon v1 Thickness source produces `1280 x 840`,
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
- the exact Synthetic Thickness Coupon v1 Thickness source produces a `1280 x 840` report with
  `908,436` valid and `166,764` missing cells.

Preserve
`docs/OPENVISIONLAB_3D_SOURCE_QUALITY_REPORT_20260727.md` and
`artifacts/current/20260727-source-quality-report/`.

### G0 owner acceptance prerequisite

Prerequisite:

- the owner is available at the running current Release application;
- the exact Synthetic Thickness Coupon v1 C3D and documented 12-step workflow are used;
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
| A-01 | C | Catalog -> Recipe Chain -> Selected Tool -> dominant Viewer default composition | None | Current wide/compact Workspace v3 captures and docking verification |
| A-02 | C | One synchronized selected step/input/ROI/output/Viewer-slot identity | None | `InspectionWorkspaceSelectionSession` focused verification |
| A-03 | C | Explicit parameter Apply/Discard | None | PropertyGrid verification and recipe non-execution checks |
| A-04 | C | Explicit ROI Review/Apply/Cancel/Delete | None | ROI lifecycle and actual-pointer evidence |
| A-05 | C | Explicit Preview/Publish/Run separation | None | Workbench/Runner verification |
| A-06 | C | Save, Save As, recent recipe, last-recipe startup restoration | None | Recipe Manager and startup verification |
| A-07 | C | Selected output Show/Pin/Compare | None | Artifact Navigator and Output Compare verification |
| A-08 | C | Single, split, stacked, and pop-out Viewer layouts | None | Viewer Workspace verification |
| A-09 | P | Configure/Review/Run state language remains understandable across every tool | A-01 | Owner replay plus cross-tool state-text review |
| A-10 | P | Problems, Messages, Performance, and Validation open only when useful | A-01 | Current lower workspace behavior and compact capture |
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
| D-04 | N | Remove Outlier Pixels tool with explicit rule and mask evidence | B-09 | Known outlier fixture, before/after counts, Viewer/Runner parity |
| D-05 | N | Level Surface from one or more explicit reference ROIs | F-01, C-06 | Tilted synthetic plane becomes level with residual evidence |
| D-06 | N | Preserve leveling transform as typed output, not hidden image mutation | D-05 | Save/reopen and Runner transform parity |
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
| E-09 | N | Top/side/perspective move, resize, rotate, and height handles | E-07, C-06 | Actual pointer evidence |
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
| H-02 | N | Completeness tool with rows, columns, pitch, and cell-shape contract | C-06, E-01 | Deterministic grid generation |
| H-03 | N | Per-cell finite-coverage metric | H-02, B-07 | Known missing-cell fixture |
| H-04 | N | Per-cell height statistic relative to reference | H-02, D-05 | Known height fixture |
| H-05 | N | Per-cell presence threshold and Pass/Fail | H-03, H-04 | Workbench/Runner parity |
| H-06 | N | Failed-cell count and aggregate completeness result | H-05 | Aggregate equals child statuses |
| H-07 | N | Per-cell colored overlay and stable cell identity | H-02 | Height Image and 3D display |
| H-08 | N | Previous/next failed-cell navigation | H-07, K-08 | UI selection verification |
| H-09 | N | Use detected/oriented region artifact as completeness input | E-11, G-12 | Typed upstream route |
| H-10 | N | Map existing Tab 1..8 names to cell results without replacing ordinary Thickness steps | H-02 | Stable recipe and output identities |
| H-11 | N | Good/bad completeness examples in Validation Set | I-01, H-05 | At least one pass, one fail, one held-out replay |
| H-12 | N | Completeness assistant that proposes height/coverage thresholds | I-04 | Evidence table and explicit Apply |

### I. Sample evidence, threshold teaching, and correction

Recommended model: `gpt-5.6-sol`

Reasoning effort: high

| ID | Status | Development item | Dependency | Closure evidence |
| --- | --- | --- | --- | --- |
| I-01 | C | Validation Set stages same-grid C3D samples without running on add | None | Current validation verification |
| I-02 | C | Explicit Run across samples with progress/cancel | None | Current Validation Set UI |
| I-03 | C | Pass/Fail/Error filters, issue navigation, per-step metrics/overlays | None | Current failure-analysis verification |
| I-04 | N | Assign `Good`, `Bad`, and `Held-out` sample roles | I-01 | Role persistence without source mutation |
| I-05 | N | Per-step and per-region metric distribution over labeled samples | I-04 | Reproducible statistics |
| I-06 | N | Candidate threshold generation for one or two scalar limits | I-05 | Deterministic candidate set |
| I-07 | N | Confusion/error table with exact supporting sample IDs | I-06 | Counts reproduce from raw sample results |
| I-08 | N | Explicit threshold suggestion Review/Cancel/Apply | I-06, A-13 | Cancel non-mutation; Apply updates draft only |
| I-09 | N | Manual parameter correction after suggestion | I-08 | Ordinary PropertyGrid contract retained |
| I-10 | N | Held-out replay gate after applied correction | I-04, I-08 | Held-out data excluded from suggestion and then replayed |
| I-11 | N | Failure -> correction -> held-out evidence record | I-10 | Durable correction record with before/after parameters |
| I-12 | N | Sample balance, overlap, and insufficient-evidence warnings | I-05 | Controlled degenerate sample sets |
| I-13 | N | Threshold assistant for Thickness/Warpage first | I-08 | One current tool closes end-to-end |
| I-14 | N | Threshold assistant for Presence/Completeness second | H-05, I-08 | Per-cell and aggregate evidence |
| I-15 | N | Never auto-run or auto-apply after sample role/threshold edits | I-08 | Command and execution-state verification |

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
| L-11 | N | Threshold-correction evidence included in Run Record | I-11 | Before/after/held-out record |
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
| M-08 | C | Exact Synthetic Thickness Coupon v1 Tab Thickness self-test | None | Generated model/Runner `8/8` |
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

1. `E-09 OrientedBox3D 3D handles`
2. `D-04 Remove Outlier Pixels`
3. `D-05/D-06 Level Surface`
4. `I-04/I-05 labeled sample evidence`
5. `I-06/I-07 threshold suggestions and error table`
6. `I-08/I-10 explicit Apply and held-out replay`
7. `H-02/H-03/H-04 completeness grid metrics`
8. `H-05/H-06/H-07 completeness result and overlays`
9. `J-01/J-03/J-04 SurfaceModel preparation foundation`
10. `J-06/J-08/J-09 scene matching, pose, and score`
11. `J-10/J-16 overlay and Workbench/Runner parity`
12. `K-02/K-03/K-06 edge-supported score components`
13. `K-08/K-11 false-positive review and performance gate`

Recommended model for queue items 1-13: `gpt-5.6-sol`.

Reasoning effort: high, except narrow UI/localization follow-ups after a
contract passes may use medium.

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
