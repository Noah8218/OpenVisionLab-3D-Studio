# OpenVisionLab 3D Identity, Direction, and GoPxL Completeness Review

Date: 2026-07-22  
Status: Complete for the current local software assessment and bounded ordered-execution priority

## Product identity

OpenVisionLab 3D Studio is an explainable, local, sensor-neutral, rule-based
3D inspection recipe workbench for height maps, point clouds, and meshes. It
is not a passive model viewer, a Thickness/Warpage application, a self-tuning
AI inspector, or a sensor/PLC/cloud production platform.

The canonical workflow is:

```text
measured/nominal data
  -> unit, frame, reference, and ROI
  -> ordered typed tools
  -> explicit Preview
  -> metrics, tolerance, and overlays
  -> explicit Publish
  -> recipe save/reopen
  -> headless Runner replay
  -> durable run record and report
```

Thickness and Warpage are ordinary Measure steps. Library-Noah owns approved
source-neutral numerical algorithms; Studio owns strict recipe adapters,
identity/provenance, UI state, explicit lifecycle, Viewer evidence, and Runner
orchestration.

## Commercial reference and boundary

The primary UX reference remains the useful part of GoPxL rather than its
visual identity or full system scope:

- searchable/filterable tool catalog;
- a readable typed tool chain;
- selected-tool `Inputs -> Parameters -> Outputs` configuration;
- graphical ROI teaching in the data viewer;
- visible missing inputs, Problems, enabled outputs, and pinned outputs;
- repeatable execution of interconnected tools.

Official sources checked:

- GoPxL Tools: https://am.lmi3d.com/manuals/gopxl/gopxl-1.2/LMILaserLineProfiler/Content/Inspect_toolRelated/Inspect_Tools.htm
- GoPxL Tool Chaining: https://am.lmi3d.com/manuals/gopxl/gopxl-1.4/en-US/LMI2DSmartCamera/Content/ToolsDiagram/Working_with_Tool_Chains_in_the_Tools_Diagram.htm
- ZEISS INSPECT: https://www.zeiss.com/metrology/us/software/zeiss-inspect.html
- PolyWorks Inspector: https://www.polyworks.com/en-us/products/polyworks-inspector

OpenVisionLab should emulate typed readability, repeatable parametric plans,
explicit evidence, and deterministic replay. Camera/sensor lifecycle, PLC,
robot, HMI, cloud, account, and plant-wide data management remain intentional
non-goals for the current product phase.

## Direction audit

The implementation follows the approved direction in the following material
ways:

- one canonical `Inspection Recipe` Workbench and `ToolRecipeDocument`;
- 15 catalog entries grouped as Prepare, Feature & Datum, Transform, Measure,
  and Review;
- Recipe Navigator, read-first Flow Map, typed Artifact Registry, Problems,
  compatible-next-tool suggestions, and output comparison/display management;
- shared WPG PropertyGrid editing with explicit Apply/Discard;
- explicit Preview/Publish and immutable source/result separation;
- dedicated Tool Labs and a dockable bilingual WPF workspace;
- strict Tools adapters and headless Runner checks.

The important remaining gaps are functional rather than cosmetic. The
deterministic `Synthetic Affine Inspection Plate v1` now closes a `16/16`
software golden for C3D -> Filter -> four edge/line/intersection anchors ->
A1/A2/A3 -> ordered Thickness/Warpage. It does not close the real-data or
physical trust gates; see
`OPENVISIONLAB_3D_SYNTHETIC_AFFINE_INSPECTION_PLATE_V1_20260722.md`.

Remaining gaps:

- arbitrary whole-recipe execution is not implemented;
- real four-landmark acquisition input and trusted physical A3 grid
  provenance are absent; synthetic A1/A2/A3 evidence now passes;
- several validated legacy measurement slices are not yet generic recipe
  adapters;
- multi-step run records, batch review, trends, and operator studies are not
  complete;
- physical scale, calibration, uncertainty, and metrology trust are unverified.

## Completeness assessment

These values use different denominators and must not be merged into one
marketing percentage.

| Denominator | Current assessment | Basis and boundary |
| --- | ---: | --- |
| Owner UI/UX gate | `85/100` accepted | Current local Workbench evidence; 150% DPI, keyboard-only, assistive, and first-time-operator reviews remain open. |
| Narrow OpenVisionLab software MVP | `65-70%` directional estimate | Viewer/UI foundations and typed slices are strong; generic execution and real end-to-end alignment remain incomplete. |
| GoPxL Tools/Tool Chaining core workflow | about `60%` directional estimate | Catalog, typed I/P/O, Problems, WPG, Viewer evidence, and bounded replay exist; writable connections, breadth, and arbitrary execution do not. |
| Full GoPxL commercial platform | about `35-40%` directional estimate | Sensor/job/runtime/industrial deployment breadth is absent and mostly intentionally out of scope. |
| Physical/metrology trust | `Unverified` | No trusted physical mapping, uncertainty, real repeated acquisition, or licensed metrology comparison supports a percentage. |

The correct product description remains **early inspection workbench MVP**.
The accepted UI score is not overall product completeness.

## Completed immediate priority: Generic Ordered Recipe Executor v1

The smallest useful execution expansion is complete for this closed set:

```text
explicit current Published A2 TransformedPointCloud
  -> authored A3 Re-grid Height Map
  -> Published TransformedHeightField
  -> one or more authored Thickness/Warpage steps in recipe order
  -> ordered measurement outputs and aggregate status/evidence
```

The same Tools owner now supports the legacy one-measurement sequence and the
new ordered multi-measurement path. The ordered path:

- derives execution order from `ToolRecipeDocument.Steps`;
- executes every downstream Thickness/Warpage step after A3;
- continues later measurements after an earlier tolerance `Fail`;
- aggregates status, elapsed time, metrics, and overlays;
- records recipe index, step ID, tool ID, output, result, and content hash;
- rejects no-measurement, unsupported downstream tools, invalid ordering,
  stale artifact identity, and duplicate outputs instead of guessing.

Current verification passes `13/13`, including direct-adapter hash parity for
both Thickness and Warpage. The previous single-measurement API remains
compatible.

## Remaining priorities

1. Acquire four distinct real CornerAnchor source/reference pairs plus trusted
   frame, unit, provenance, revision, and ReferenceGridProfile evidence, then
   replay the passed synthetic A1 -> A2 -> A3 lifecycle on real data.
2. Migrate the already validated Plane Flatness, Point Pair, Gap/Flush,
   Volume, and Cross-section slices into the generic ToolRecipe lifecycle.
3. Add a multi-step Run Record that persists per-step input/output IDs,
   hashes, status, metrics, elapsed time, and blocked/failure provenance.
4. Add writable typed connections only after execution semantics are stable;
   do not build a decorative general node editor first.
5. Run the deferred 150% DPI, keyboard-only, assistive, and first-time-operator
   UI protocols.

## Completion record

```text
Status: Complete
Scope: current identity/commercial assessment plus explicit A2 -> A3 -> multiple Thickness/Warpage ordered execution
Acceptance criteria: documented denominators -> pass; authored order -> pass; direct-adapter hash parity -> pass; fail-closed route checks -> pass; legacy single sequence -> pass
Verification: full solution build 0 warnings/0 errors; ArtifactOwnedRoiRunnerVerification 13/13; A3 4/4; Thickness 5/5; Warpage 5/5; generic Workbench 9/9; teaching 18/18
Evidence: artifacts/current/20260722-generic-ordered-recipe-runner and durable contract documents in docs/
Boundary / next dependency: no arbitrary graph, real A1/A2 package, physical calibration, metrology, sensor, PLC, robot, or cloud claim
```
