# OpenVisionLab 3D Historical Manual Functional Reference

Updated: 2026-07-19

Status: Reference-only functional review. This document does not authorize copying the historical product UI, visual assets, source code, terminology, parameter defaults, or equipment behavior.

## Source and Review Boundary

- User-provided source: `C:\Users\user\Desktop\TLB_3D_Manual_KR_20240325.pptx`
- Source SHA-256: `6A97FC625E0B2E51A19F7EFDB77DC4CF1A43D81757DFC1388C2A0144C21D1A07`
- Reviewed scope: all `87` slides, rendered and inspected locally.
- Confidentiality boundary: the source deck was not uploaded, searched on the internet, copied into this repository, or reused as a visual/template asset.
- Allowed use: identify user workflows, generic interaction patterns, and inspection concepts that can be re-specified in OpenVisionLab-owned contracts and UI.
- Disallowed use: reproduce screenshots, black/orange chrome, icon shapes, pane arrangement, labels such as `OLTT`, exact parameter names/defaults, proprietary correction behavior, or equipment-control sequences.

## Product Fit

The historical program is a production-oriented PCB thickness/warpage system. OpenVisionLab 3D Studio is narrower: an explainable, local, sensor-neutral rule-based 3D inspection recipe workbench. The reusable part is therefore the inspection authoring and evidence workflow, not the machine platform.

The strongest reusable workflow is:

```text
source/derived 3D data
-> grouped teaching selections
-> typed tool parameters
-> explicit Preview
-> visible overlays and metrics
-> ordered step result review
-> explicit Publish / recipe save / Runner replay
```

## Functional Adoption Matrix

| Historical manual evidence | Functional lesson | OpenVisionLab decision | Differentiated implementation |
| --- | --- | --- | --- |
| Slides 22-24: movable/resizable/savable layouts | Users need task layouts and an expert layout | Keep | AvalonDock task workspaces plus opt-in Advanced layout; do not reproduce the original multi-column arrangement or menu chrome |
| Slides 31-35: 3D/2D Viewer, model tree, PropertyGrid, result, grouped ROI operations | Teaching needs one connected source-selection-parameter-result loop | Keep | Project Explorer + typed recipe rows + Properties + Pipeline/Validation; explicit commands instead of implicit Enter-key execution |
| Slides 36-43: module ROI, four line finds, two intersections, two reference points, alignment | Confirms the user's feature-first alignment workflow | Keep as product direction; executor still requires approval | Filter -> height-difference edge -> line fit -> intersection -> correspondence -> XYZ affine -> derived map, expressed with OpenVisionLab-owned contracts and new golden fixtures |
| Slides 46-53: thickness and warpage ROI teaching | Measurement tools consume derived data and taught regions | Keep concept, not the narrow product boundary | Thickness, warpage, flatness, gap/flush, volume, cross-section, and future tools remain independent typed steps; no single-purpose PCB UI |
| Slide 55: quick per-item/full inspection check | Users need a fast teaching check | Keep | Explicit `Preview` for one selected step and explicit `Run` for the recipe; never execute from visibility, layer, or selection changes |
| Slides 57-65: repeatability charts and correction modes | Chart/grid linking is valuable; physical correction is high-risk | Partly keep | Existing typed Calibration Center and shared selection remain; correction modes require separate evidence and are not inferred from this manual |
| Slides 67-71: create/copy/open/delete model | Recipes need discoverable lifecycle actions | Keep later | Recipe Manager with stable identity, validation, duplicate/open/delete confirmation; do not copy the dialog or model directory semantics |
| Slides 73-76: view presets, profile, palette, contour, original/enhanced/aligned display | Viewer must explain orientation, height, cross-section, and processing state | Keep | Camera-aware XYZ triad, draggable Profile, full-source height histogram/scale, compact `View` menu, and entity/layer comparison; future presets/contours use new controls and styling |
| Slides 77-80: linked 2D view, cursor XY/height, measure, drawing state | 2D height-map context accelerates precise teaching | Keep selectively | Docked Height Map/Profile views with linked selection and raw-height cursor readout; preserve source/derived layers and explicit Preview boundaries |
| Slide 82: ordered result tree plus failure details | Operators need step-level evidence, not only a large OK/NG mark | Keep | Pipeline/Validation and Review surfaces should expose step status, metrics, overlays, entity IDs, and failure reason |

## Viewer Priorities Informed by the Manual

### Already established

- Wireframe is the C3D default.
- Right-button drag pans; short right-click opens the Viewer-local menu.
- Viewer commands are available from both the compact top `View` menu and the context menu.
- A camera-aware XYZ triad identifies the right-handed Y-up display frame.
- Profile places and drags two source-bound C3D endpoints and updates a docked chart without running inspection.
- The right-side full-source raw-height scale and 32-bin distribution use the active Height/Grayscale/Thermal palette and remain display-only.
- Viewer, Shell, and zero-`ProjectReference` BinaryHost share the same Viewer control.

### Recommended additions

1. Canonical camera presets: Top, Front, Right, Left, Back, and Isometric. Put them in the existing compact `View` menu and optional shortcut layer, not a copied perspective combo.
2. Display-only contour overlay: derived from the active scalar display and clearly labeled as visualization, not an inspection result.
3. Source/derived/aligned layer comparison: use the existing entity/layer model rather than three copied toolbar toggle buttons.
4. Linked Height Map cursor: show row, column, viewer XYZ, and raw-height while synchronizing one selection marker with the 3D Viewer.
5. Step-result drilldown: selecting a pipeline result should focus its overlays and metrics without changing the recipe input layer or running Preview.

The first implementation priority remains one owner-approved typed execution adapter. Viewer additions should be small, display-only slices that do not delay or silently substitute for the numerical inspection contract.

## Explicitly Deferred or Out of Scope

- Inspector library selection, sensor IDs, Gocator jobs, MMI/WCF, database, equipment IP, disk cleanup, production image retention, and machine paths.
- Login/account surfaces unless a future deployment requirement explicitly introduces them.
- Target alarm that stops equipment and retry sequences that alter line control or takt time.
- Physical offsets, Average/Profile/Slope correction, calibration claims, or micrometre units without source metadata, traceable fixtures, and independent validation.
- Exact PCB-specific nine-point warpage behavior as the product architecture; it may become one typed recipe tool only after its numerical contract is separately approved.

## Non-Copy UI Rules

1. Use OpenVisionLab's light application shell, navy dock panes, teal workflow accents, and existing icon library; do not reuse the historical black/orange identity.
2. Keep the bounded Workbench layout and opt-in Advanced docking; do not mirror the historical pane coordinates or toolbar strip.
3. Use `Teach -> Preview -> Run -> Publish`, not historical product terminology or Enter-key shortcuts that execute implicitly.
4. Use typed entities, step IDs, inputs, outputs, metrics, and overlays; do not copy PropertyGrid field names or defaults from screenshots.
5. Represent Original/Filtered/Aligned states as source and derived layers with provenance, not as visually matching toggle buttons.
6. Create new icons, labels, screenshots, and sample fixtures. Never import assets or proprietary samples from the source deck.
7. Preserve explicit display-only labels for palettes, profiles, contours, and linked cursors so visualization cannot be mistaken for a published inspection result.

## Pre-Implementation Checklist

- [ ] Describe the requested function without historical product names or UI coordinates.
- [ ] Confirm there is no existing OpenVisionLab equivalent.
- [ ] Define typed input/output entities, selection semantics, parameters, and failure cases.
- [ ] Define explicit Preview/Run/Publish behavior and prove display changes do not execute inspection.
- [ ] Use OpenVisionLab-owned icons, wording, layout, data, and golden fixtures.
- [ ] Capture current-build before/after UI evidence and contract output.
- [ ] Keep physical calibration, uncertainty, and metrology claims blocked until independent evidence exists.

## Current Evidence

The height-distribution Viewer slice that was active when this manual was reviewed passes locally with:

- solution build: `0` warnings / `0` errors;
- focused height distribution: `20/20`;
- display settings: `82` checks;
- docking: `15/15`;
- C3D Profile: `10/10`, Profile ViewModel: `8`;
- Viewer pointer/context menu and Profile pointer: pass;
- BinaryHost: manifest `14/14`, outputs `12/12`, Host API `3/3`.

This evidence proves the current software/display contract only. It does not prove sensor scale, physical thickness, warpage accuracy, calibration, uncertainty, or metrology.
