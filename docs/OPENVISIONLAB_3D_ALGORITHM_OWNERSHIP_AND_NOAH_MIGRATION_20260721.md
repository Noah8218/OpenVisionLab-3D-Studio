# OpenVisionLab 3D Algorithm Ownership and Library-Noah Migration

> Status: Superseded historical evidence. The active ownership contract is
> `OPENVISIONLAB_3D_VISION_SDK_TOOL_CONTRACT_AND_MIGRATION_BASELINE_20260805.md`;
> this file preserves the Library-Noah migration state recorded at the time.

Updated: 2026-08-03

Status: **Owner-approved Tool-only numerical ownership contract; the audited
Studio migration baseline is zero and future work remains evidence-gated.**

> 2026-08-03 current package update: `Lib.ThreeD 2.8.12` at
> `7ed50ea37b3d7cb711c2afe698d209f9073e9217` additionally owns deterministic
> farthest-point model key-point extraction from an already prepared sample
> domain, including stable seed and source-order tie-breaking. It retains the
> 2.8.11 deterministic
> exact-coordinate duplicate removal and canonical explicit internal/
> unobservable source-triangle exclusion. It retains the 2.8.10 direct and
> declared model-axis cyclic rigid-pose equivalence and the 2.8.9 bounded
> multiple Surface Match search and disjoint collection APIs, labeled-evidence
> descriptive statistics, threshold-
> candidate construction/ranking, repeatability mean, extrema, sample standard deviation, six-sigma
> spread, range, and explicit negative-variance round-off policy; declared
> mesh-normal quality and four-point landmark independence validation, plus
> retains dual-surface thickness, height-deviation, height-map, filtering,
> leveling, Surface
> Match, mesh-comparison, and transform-diagnostic Tools.
> The vendored package SHA-256 is
> `7E5DAF887851CB16C45279CD957260C2546AD0EDBB92B9F4903E23E529BADFE3`.
> Older package sections below remain migration history.

## Binding ownership rule

OpenVisionLab 3D Studio is a typed inspection-tool workbench. It owns recipe
identity, source binding, operator teaching, WPG, Tool Labs, explicit
Preview/Publish state, Viewer overlays, Runner reports, and replay evidence.

`Library-Noah` (`Lib.ThreeD`) owns reusable, pure numerical 3D algorithms.
It must not reference Studio `Core`, `Data`, WPF, Viewer, recipe JSON, or
Shell state. Studio `Tools` converts its own immutable contracts to/from Noah
inputs and results; it must not carry a second numerical implementation.

```text
Studio recipe / source / current Published artifact
  -> Studio typed adapter and identity validation
  -> Lib.ThreeD pure input
  -> Lib.ThreeD algorithm
  -> Studio immutable artifact, hash, metrics, overlay, lifecycle evidence
```

This preserves the Tool-first product workflow while making calculations
reusable by other OpenVisionLab products.

## Current audited state

| Algorithm family | Current numerical owner | Required direction |
| --- | --- | --- |
| Thickness / Warpage | `Lib.ThreeD` through the Studio bridge | Retain; Studio remains an adapter. |
| Full XYZ Affine Solve | `Lib.ThreeD` | Studio is a typed adapter; preserve Studio artifact/hash/UI. |
| 2-Point Line | `Lib.ThreeD` pure construction | Complete: Studio is the strict raw-C3D/PointSet(2) adapter and owns lifecycle/evidence only. |
| 3-Point Plane | `Lib.ThreeD` pure construction | Complete: Studio is the strict raw-C3D/PointSet(3) datum-plane adapter and owns lifecycle/evidence only. |
| Filter | `Lib.ThreeD` pure finite/NaN median filter | Complete: Studio retains the C3D-zero/derived-finite-zero boundary and typed lifecycle/evidence only. |
| Raw height summary / distribution / Source Quality distribution | `Lib.ThreeD 2.8.4` deterministic source-neutral Tools | Complete: Studio retains C3D decoding, byte identity, report composition, load timing, and Viewer-only point projection. |
| Completeness Grid | `Lib.ThreeD 2.8.4` deterministic source-neutral Tool | Complete: Noah owns cell placement, finite coverage, reference-relative means, and typed cell/aggregate decisions; Studio retains recipe identity, canonical output hash, metrics, overlays, and lifecycle. |
| Height-map ROI statistics / reference-grid reconstruction | `Lib.ThreeD 2.8.4` deterministic source-neutral Tools | Complete: Studio retains recipe routing and output evidence; Noah owns aggregation and declared/reference-axis coordinate reconstruction. |
| Dual Surface Thickness | `Lib.ThreeD 2.8.5` `DualSurfaceThicknessInspectionTool` | Complete: Noah owns plane-relative residuals, statistics, and typed limits; Studio retains identity, lifecycle, metrics, and overlays. |
| Height Deviation | `Lib.ThreeD 2.8.5` `HeightDeviationInspectionTool` | Complete: Noah owns low/high/peak calculation and typed decision; Studio retains source text, unit, lifecycle, metrics, and overlays. |
| Declared mesh-normal quality | `Lib.ThreeD 2.8.6` `DeclaredMeshNormalQualityTool` | Complete: Noah owns normal length, topology, degenerate-triangle, and corner-alignment evidence; Studio retains source/format identity, admission evidence, and report composition. |
| Local-median outlier removal | `Lib.ThreeD 2.8.4` deterministic source-neutral Tool | Complete: Studio retains C3D identity, authored parameters, mask/artifact composition, lifecycle, metrics, and overlays. |
| Level Surface | `Lib.ThreeD 2.8.4` deterministic source-neutral Tool | Complete: Noah owns unique reference-cell selection, plane/residual statistics, detrending, and output-plane evidence; Studio retains source/ROI identity, authored RMS acceptance, transform/artifact composition, lifecycle, metrics, and overlays. |
| Height Difference Edge | `Lib.ThreeD` pure adjacent-pair scan/selection | Complete: Studio is the strict C3D lineage/artifact adapter and owns lifecycle/evidence only. |
| 3D Line Fit | `Lib.ThreeD` pure deterministic consensus/TLS | Complete: Studio is the strict C3D lineage/artifact adapter and owns lifecycle/evidence only. |
| Line Intersection | `Lib.ThreeD` | Studio is a typed C3D lineage/artifact adapter; Noah owns closest-approach geometry. |
| Plane Flatness / Point Pair / Gap-Flush / Volume / Cross-section Dimensions | `Lib.ThreeD` pure inspection tools | Complete for deterministic software evidence: Studio owns A3 identity, ROI/WPG/UI, metrics, overlays, hashes, and replay. |
| Surface Match pose search / unique-nearest coverage | `Lib.ThreeD 2.8.4` deterministic source-neutral tools | Complete: Studio retains identity/unit/frame validation, strict adapters, canonical artifacts, acceptance, lifecycle, evidence, and UI. |
| SurfaceModel / Prepared Scene preparation and surface edges | `Lib.ThreeD 2.8.4` deterministic source-neutral tools | Complete: Studio retains admission, identities, canonical artifacts, separate diagnostic evidence, and UI. |
| Nominal/actual mesh comparison | `Lib.ThreeD 2.8.4` deterministic source-neutral tools | Complete: Noah owns triangle-distance, sign recovery, streaming statistics, counts, and display sampling; Studio retains source/unit/frame identity, loaders, artifacts, progress adaptation, and UI. |
| Registration transform diagnostics | `Lib.ThreeD 2.8.4` deterministic source-neutral Tool | Complete: Noah owns homogeneous-row, orthogonality, determinant, translation, and rotation diagnostics; Studio retains authored limits, ordered acceptance, evidence, lifecycle, and UI. |
| Landmark Correspondence | `Lib.ThreeD 2.8.6` `LandmarkCorrespondenceValidationTool` | Complete: Noah owns augmented rank and normalized tetrahedral volume; Studio retains identity, lineage, recipe, artifact, hashing, and lifecycle ownership. |
| Thickness / aligned-point repeatability statistics | `Lib.ThreeD 2.8.7` `RepeatabilityStatisticsTool` | Complete: Noah owns scalar accumulation and descriptive statistics; Studio retains study/source/correspondence identity, unit/frame/alignment, acceptance, metrics, and evidence. |
| Labeled-evidence descriptive and C3D ROI statistics | `Lib.ThreeD 2.8.8` `LabeledEvidenceStatisticsTool` and `HeightMapRegionStatisticsTool` | Complete: Noah owns aggregation; Studio retains recipe/Tool/parameter/source/sample/role identity, grouping, warnings, reports, and evidence locators. |
| Threshold candidate analysis | `Lib.ThreeD 2.8.8` `ThresholdCandidateAnalysisTool` | Complete: Noah owns candidate construction, classification, error counts, ranking, and tie-breaking; Studio retains eligibility/routing, HeldOut exclusion, canonical candidate identity, warnings, reports, and lifecycle. |
| Multiple Surface Match result collection | `Lib.ThreeD 2.8.9` `DeterministicMultipleSurfaceMatchTool` | Complete: Noah owns repeated pose search, unique-nearest coverage, disjoint scene claims, deterministic ordering, and bounded termination; Studio retains identities, authored acceptance, immutable collection persistence, lifecycle, evidence, and presentation-only selection. |
| Symmetry-aware rigid-pose equivalence | `Lib.ThreeD 2.8.10` `RigidPoseSymmetryEquivalenceTool` | Complete: Noah owns direct/cyclic model-axis equivalence, residuals, inclusive decisions, and tie-breaking; Studio retains SurfaceModel/pose/unit/frame/limit validation and typed evidence. |
| Model surface selection | `Lib.ThreeD 2.8.11` `DeterministicModelSurfaceSelectionTool` | Complete: Noah owns exact-coordinate duplicate selection and canonical explicit exclusions; Studio retains source identity, authored roles, original locators, schema/persistence, and active-domain routing. |
| Model key-point extraction | `Lib.ThreeD 2.8.12` `DeterministicModelKeyPointExtractionTool` | Complete: Noah owns deterministic farthest-point selection over the retained model samples; Studio retains stable source-sample/source-triangle identity, atomic persistence, model-context validation, and display-only overlay composition. |

No migration is a claim of physical calibration, metrology, or a real
four-anchor fixture result.

## Superseding Tool contract and full migration baseline

The owner clarified on 2026-08-01 that inspection algorithms, filters, and
their supporting reusable calculations must use the Library-Noah Tool form.
The target is a public sealed `XxxTool` with source-neutral typed
input/options, a typed controlled result, and one explicit `Execute(...)`
entry point. The narrow `IThreeDInspectionTool` interface remains compatible
for regular height-map inspection, but is not forced onto matching, mesh, or
multi-input Tools.

The current Studio audit is recorded in:

- `docs/OPENVISIONLAB_3D_NOAH_TOOL_CONTRACT_AND_MIGRATION_BASELINE_20260801.md`;
- `docs/OPENVISIONLAB_3D_NOAH_TOOL_MIGRATION_BASELINE_20260801.json`.

The decreasing migration baseline contains `0` migration-debt files and `33`
reviewed Studio boundaries. It is not a permanent exception list. The
structure verifier rejects new unclassified numerical owners and
numerical-signal growth above the recorded boundary ceilings.

Current verification passes structure `29/29`; the vendored `Lib.ThreeD 2.8.12`
package boundary also passes with source commit and SHA-256 agreement. Preserve
`docs/OPENVISIONLAB_3D_MODEL_KEY_POINT_ARTIFACT_AND_DEBUG_OVERLAY_20260803.md`
and
the physical current-task evidence under
`D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260803-j07-model-key-points\`.

## Current model key-point extraction: Lib.ThreeD 2.8.12

`DeterministicModelKeyPointExtractionTool` owns deterministic farthest-point
selection from the J-05 retained SurfaceModel sample domain. Studio's
`ModelKeyPointExtractor` validates model identity and maps the Noah result to
stable source-sample/source-triangle evidence. The persisted artifact and
WPF-neutral position/normal overlay do not execute or alter matching.

Noah passes Release `0/0` and Smoke `122/122`; Studio passes Release Rebuild
`0/0`, package/bridge `21/21`, focused J-07 `15/15`, legacy byte parity `5/5`,
and structure `29/29`. See
`docs/OPENVISIONLAB_3D_MODEL_KEY_POINT_ARTIFACT_AND_DEBUG_OVERLAY_20260803.md`.

## Previous model surface selection: Lib.ThreeD 2.8.11

`DeterministicModelSurfaceSelectionTool` owns exact-coordinate triangle
identity, deterministic first-retained duplicate ownership, canonical explicit
internal/unobservable exclusions, and controlled invalid-input results.
Studio preserves the complete source topology and maps one retained domain to
sampling, matching, model edges, and overlays. It does not infer enclosure,
near-duplicate tolerance, or viewpoint visibility.

Noah passes Release `0/0` and Smoke `118/118`; Studio passes Release Rebuild
`0/0`, package/bridge `21/21`, focused J-05 `15/15`, legacy byte parity `5/5`,
and structure `29/29`. See
`docs/OPENVISIONLAB_3D_MODEL_SURFACE_SELECTION_20260803.md`.

## Previous symmetry-aware pose equivalence: Lib.ThreeD 2.8.10

`RigidPoseSymmetryEquivalenceTool` owns direct and declared model-axis cyclic
rigid-pose equivalence. Studio's `SurfaceMatchPoseEquivalenceEvaluator`
validates the model, both poses, unit, source and target frames, and authored
limits, then maps the Noah result to WPF-neutral typed evidence. It does not
execute or modify single or multiple matching.

Noah passes Release `0/0` and Smoke `113/113`; Studio passes Release `0/0`,
package/bridge `20/20`, focused J-13 `15/15`, legacy byte parity `5/5`, and
structure `29/29`. See
`docs/OPENVISIONLAB_3D_SYMMETRY_AWARE_POSE_EQUIVALENCE_20260803.md`.

## Previous multiple Surface Match collection: Lib.ThreeD 2.8.9

`DeterministicMultipleSurfaceMatchTool` owns the bounded repeated search,
per-result unique-nearest scoring, greedy disjoint scene-sample claiming,
stable ordering, and stop policy. Studio's
`MultipleSurfaceMatchEvaluationExecutor` validates product identity, maps the
Noah result into separately authored assessments, composes a schema-1
collection with stable content-derived IDs, and supplies persistence and UI.

Noah passes Release `0/0` and Smoke `108/108`; Studio passes Release `0/0`,
package/bridge `19/19`, multiple-match Runner `14/14`, Workbench `6/6`,
existing matching `34/34`, and structure `29/29`. See
`docs/OPENVISIONLAB_3D_MULTIPLE_SURFACE_MATCH_RESULT_COLLECTION_20260803.md`.

## Earlier validation-statistics migration: Lib.ThreeD 2.8.8

`LabeledEvidenceStatisticsTool` owns role-grouped descriptive statistics.
`ThresholdCandidateAnalysisTool` owns deterministic candidate construction,
classification, error counting, ranking, and tie-breaking. The Studio adapter
uses `HeightMapRegionStatisticsTool` for rectangular C3D ROI mean and coverage.
Studio retains all product identities, grouping, routing, HeldOut policy,
canonical candidate IDs, reporting, lifecycle, and UI.

Noah passes `0/0` and `106/106`; Studio passes `0/0`, bridge `19/19`,
Validation Set `84/84`, normalized report difference `0`, and structure
`29/29`. See
`docs/OPENVISIONLAB_3D_VALIDATION_STATISTICS_NOAH_MIGRATION_20260801.md`.

## Current declared-normal quality and Landmark Correspondence migration: Lib.ThreeD 2.8.6

`DeclaredMeshNormalQualityTool` owns finite/non-zero/unit-length evidence,
triangle index and degeneracy checks, and per-corner normal alignment.
`LandmarkCorrespondenceValidationTool` owns exactly-four augmented rank and
span-normalized tetrahedral volume. Studio retains source and landmark
identity, lineage, report/artifact policy, canonical hashing, lifecycle,
metrics, overlays, and Viewer presentation.

Normalized baseline/current reports are exact for normal quality and Landmark
Correspondence (`2/2`). Noah passes `0/0` and `98/98`; Studio passes `0/0`,
bridge `16/16`, focused `26/26` and `5/5`, loading matrix `128/128`, and
structure `27/27`. See
`docs/OPENVISIONLAB_3D_DECLARED_NORMAL_QUALITY_AND_LANDMARK_CORRESPONDENCE_NOAH_MIGRATION_20260801.md`.

## Current Dual Surface Thickness and Height Deviation migration: Lib.ThreeD 2.8.5

`DualSurfaceThicknessInspectionTool` owns reference-plane residuals, mean/min/
max/range/RMS statistics, limit counts, and typed decisions.
`HeightDeviationInspectionTool` owns low/high/peak absolute deviation and typed
decisions. Studio retains product identity, recipe lifecycle, elapsed time,
ToolResult metrics, overlays, and Viewer presentation.

Normalized current-Release parity is exact for the generic height-measurement
Workbench and actual Height Deviation recipe (`2/2`). Noah passes `0/0` and
`92/92`; Studio passes `0/0`, bridge `14/14`, Workbench `54/54`, Validation
Set `84/84`, focused regressions, and structure `26/26`. See
`docs/OPENVISIONLAB_3D_DUAL_SURFACE_THICKNESS_AND_HEIGHT_DEVIATION_NOAH_MIGRATION_20260801.md`.

## Current height-map inspection and preparation migration: Lib.ThreeD 2.8.4

Five public Tools now own raw height-grid summary/distribution calculation,
rectangular finite-value statistics, Completeness Grid placement and typed
decisions, and declared/reference-axis grid-point reconstruction. Studio
retains C3D decoding, source/unit/frame/recipe identity, canonical hashes,
metric/overlay composition, explicit lifecycle, and presentation-only Viewer
projection.

The five normalized pre/post reports are exact (`5/5`). Focused and expanded
verification passes package bridge `12/12`, map fidelity `10/10`, Source
Quality `13/13`, Completeness Grid `23/23`, Height distribution `25/25`,
generic height-measurement Workbench `54/54`, Height Image `25/25`,
artifact-owned ROI `18/18`, Validation Set `84/84`, and structure `25/25`.
See
`docs/OPENVISIONLAB_3D_HEIGHT_MAP_INSPECTION_PREPARATION_NOAH_MIGRATION_20260801.md`.

## Earlier nominal comparison and transform diagnostics migration: Lib.ThreeD 2.8.3

`TriangleMeshDistanceTool`, `NominalActualMeshComparisonTool`, and
`RigidTransformDiagnosticsTool` now own the deterministic mesh-distance,
signed-distance recovery, streaming comparison statistics, and rigid-matrix
diagnostic calculations. Studio retains strict source/unit/frame/identity
validation, file loading, canonical evidence, authored acceptance, lifecycle,
and presentation.

Focused Studio reports remain exact before and after migration: mesh deviation
`25/25` lines, nominal/actual comparison `31/31` lines, and registration
acceptance `23/23` lines, each with zero differences. See
`docs/OPENVISIONLAB_3D_NOMINAL_COMPARISON_AND_TRANSFORM_DIAGNOSTICS_NOAH_MIGRATION_20260801.md`.

## Earlier filtering and leveling migration: Lib.ThreeD 2.8.2

`DeterministicLocalMedianOutlierFilterTool` now owns available-neighbor
selection, center exclusion, deterministic median calculation, strict
absolute-deviation comparison, and output/mask indices.

`LevelSurfaceTool` owns unique finite reference-cell collection, the existing
least-squares height-plane fit, raw-height residual RMS/P2V and reference
mean, full-grid detrending with missing-mask preservation, and output-plane
evidence. Studio still validates exact C3D and GridRectangle identity, maps
region evidence, interprets the authored RMS limit, and creates the immutable
OpenVisionLab transform, derived C3D, metrics, and overlays.

The two focused Studio goldens pass `9/9` each. Excluding the intentionally
different evidence-directory path line, all `28` comparable pre/post report
lines are identical, including derived C3D, mask, and transform SHA-256.
See
`docs/OPENVISIONLAB_3D_OUTLIER_FILTER_AND_LEVELING_NOAH_MIGRATION_20260801.md`.

## Earlier Surface Match migration: Lib.ThreeD 2.8.1

Five additional public Tools now own deterministic SurfaceModel and Prepared
Scene sampling, mesh boundary/crease extraction, organized height-step edge
extraction, and edge-domain coverage. The then-seven Studio Surface Match
compatibility entry points contain validation, mapping, artifact, and evidence
composition only. Exact pre/post persisted parity passes `24/24`. See
`docs/OPENVISIONLAB_3D_SURFACE_PREPARATION_EDGE_NOAH_MIGRATION_20260801.md`.

The earlier pose/coverage foundation remains:

`DeterministicRigidSurfacePoseSearchTool` and
`DeterministicSurfaceCoverageTool` accept only finite, ordered, source-neutral
XYZ samples plus explicit pure pose/search parameters. Noah owns candidate
generation and order, centroid translation, bounds and budget rejection,
unique-nearest correspondence, coverage/RMSE, and best-candidate ranking.

Studio's `RigidSurfacePoseSearch` and `SurfaceCoverageScorer` retain their
public compatibility names, but now validate Studio artifacts and delegate to
Noah through `LibraryNoahSurfaceMatching`. A structure guard rejects the old
rotation, centroid, distance, and claimed-scene helpers in those adapters.
The controlled known-pose Studio result remains byte-identical at SHA-256
`4D214BA3684162407332A69D95155C7FF7D780CC7C8B277795DB028619408B5F`.
See `docs/OPENVISIONLAB_3D_SURFACE_MATCH_NOAH_MIGRATION_20260801.md`.

## Historical package migration: Lib.ThreeD 2.7.4

The active package retains the source-neutral affine, common-line, plane,
point-cloud-apply, and reference-grid tools, deterministic full-XYZ
consensus/TLS line fitting, deterministic height-difference edge selection,
and deterministic median filtering:

1. `FullXyzAffineSolveTool` — exact four-pair source-to-reference solve using
   scaled partial-pivot arithmetic, determinant/condition evidence, and
   residuals.
2. `TwoPointLineTool` — ordered full-XYZ segment construction from two finite
   points with controlled invalid-input results.

3. `LineIntersectionTool` evaluates full-XYZ closest approach, acute angle,
   and finite-segment support for two normalized source-neutral lines.

4. `ThreePointPlaneTool` evaluates an ordered full-XYZ support triangle,
   oriented unit normal, and plane offset from three finite non-collinear
   points. It has no C3D, recipe, WPF, or measurement dependency.

5. `DeterministicLineFitTool` evaluates ordered finite XYZ points using the
   fixed SHA-256 pair schedule, consensus priority, orthogonal TLS,
   source-scanline direction, inlier support gates, and diagnostics. It has no
   C3D, recipe, WPF, source identity, or measurement dependency.

6. `DeterministicHeightDifferenceEdgeTool` evaluates a source-neutral
   row-major scalar grid, explicit rectangular selection, axis, polarity, and
   minimum delta. It owns finite-pair filtering, adjacent-pair deltas,
   strongest-per-scanline selection, exact-tie ordering, and diagnostics. It
   has no C3D, recipe, WPF, source identity, or measurement dependency.

7. `DeterministicMedianFilterTool` evaluates source-neutral row-major scalar
   grids where finite values are valid and non-finite values are missing. It
   owns bounded `3/5/7` median-window arithmetic, finite-neighbor selection,
   available-neighbor borders, missing-mask preservation, and changed-cell
   count. It has no C3D, recipe, WPF, source identity, or measurement
   dependency.

Studio continues to own C3D locator resolution, source SHA/frame validation,
recipe parameter parsing, canonical Studio output hashes, and the WPF lifecycle.
The A1 and Line Intersection Studio rules call Noah rather than retaining
matrix/pivot or closest-approach/angle/support numerical implementations. The
completed 2-Point Line Tool calls the Noah construction tool and does not
duplicate subtraction, normalization, or zero-length checks.

The Studio package reference is pinned to the locally vendored `Lib.ThreeD`
2.7.4 artifact from Library-Noah commit
`5d06460c14b1edf390241b28511ce4997f70dc28`; its SHA-256 is
`BB44D30F8D3AB9C1CF528482CFA2A5A804D9222FFBAE258C765CEF2696EB2573`.
Development uses the packaged output, not a cross-repository `ProjectReference`,
so the same package boundary is tested locally and in CI.

## Migration acceptance gates

- Library-Noah build and deterministic smoke prove the pure result and error
  paths.
- Studio A1 Golden continues to pass without a local inverse/matrix-solve
  implementation.
- Studio Line Intersection Golden continues to pass without a local
  closest-approach/angle/support implementation.
- Search proves Studio calls `Lib.ThreeD.FeatureExtraction` for migrated math
  and no old private numerical helper remains.
- Vendored package ID/version/hash match the Library-Noah package output.
- Existing Studio Tool/Runner checks continue to pass.
- The 2-Point Line Studio adapter proves strict raw-C3D/PointSet(2) binding,
  ordered replay identity, explicit lifecycle, source-change clearing, Tool
  Lab, and Runner behavior without copying Noah geometry math.
- The 3-Point Plane Studio adapter proves strict raw-C3D/PointSet(3) binding,
  ordered-normal replay identity, explicit lifecycle, source-change clearing,
  Tool Lab support-triangle/normal evidence, and Runner behavior without
  copying Noah cross-product, normalization, or plane-equation math.
- The 3D Line Fit Studio adapter proves strict Published EdgePointSet binding,
  unchanged canonical artifact hash, explicit lifecycle, and Runner behavior
  while the Noah tool owns pair scheduling, TLS, residual classification, and
  support diagnostics.
- The Height Difference Edge Studio adapter proves strict raw-height derived
  C3D source/selection binding, unchanged canonical artifact hash, explicit
  lifecycle, and Runner behavior while the Noah tool owns all pair scanning,
  missing-pair handling, candidate ordering, and numerical diagnostics.
- The Filter Studio adapter proves strict raw-height C3D source binding,
  unchanged finite-zero derived-output rejection, canonical artifact hash,
  explicit lifecycle, and Runner behavior while the Noah tool owns all median
  windows, neighbor selection, missing-mask preservation, and changed-count
  arithmetic.
- The schema-1 Noah Tool migration baseline parses, every entry resolves to a
  current source file and target Tool, and no unclassified or expanded Studio
  numerical owner is detected.

## Explicit boundaries

- A1's real four-anchor fixture Preview/Publish/Runner replay remains
  unverified because no real source/reference package exists.
- A2 affine application and A3 re-grid have deterministic synthetic evidence
  only; real aligned fixture validation, Thickness/Warpage after alignment,
  calibration, and metrology are not included in this migration.
- Studio does not create a generic graph executor, a plugin factory, or a
  second algorithm API. Each typed adapter remains explicit.
- 2-Point Line is construction evidence only. It does not find a physical
  edge, establish a calibrated length, or authorize affine application,
  re-grid, Thickness, Warpage, calibration, or metrology.
