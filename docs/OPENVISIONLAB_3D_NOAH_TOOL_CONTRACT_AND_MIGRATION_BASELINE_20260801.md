# OpenVisionLab 3D Library-Noah Tool Contract and Migration Baseline

Date: 2026-08-01

Updated: 2026-08-04 for `Lib.ThreeD 2.8.13` height-map input contracts

Status: Active architecture contract

## Decision

Every reusable numerical, geometric, filtering, feature-extraction, matching,
measurement, inspection, or statistical algorithm belongs to
`Library-Noah` and is consumed from the committed, vendored `Lib.ThreeD`
package. Studio must not contain a second implementation.

This decision applies to existing Studio calculation debt as well as new work.
Existing code is not grandfathered as a permanent exception. Migration is
phased so observable product behavior can be preserved and verified one Tool
family at a time.

## Meaning of Tool form

A Noah algorithm is Tool-shaped when it has all of the following:

- a public, sealed `XxxTool` entry type;
- source-neutral typed input and, when needed, `XxxOptions`;
- a typed `XxxResult` that distinguishes success, controlled invalid input,
  and unavailable measurement;
- one explicit `Execute(...)` entry point, with `CancellationToken` when the
  work can be material;
- no mutable cross-run algorithm state;
- deterministic ordering and tie-breaking where the result is replayed;
- no dependency on Studio Core/Data, WPF, Viewer, recipe JSON, paths, Shell,
  Preview, Publish, Run, or Validation state.

The existing `IThreeDInspectionTool` is retained for its current
`HeightMap3D -> ThreeDInspectionResult` compatibility contract. Matching,
mesh comparison, multi-input geometry, and feature extraction must not be
forced into that narrow interface. They still use the public sealed
`XxxTool` plus typed `Execute` contract. A generic common interface may be
introduced only after compatibility review; the type shape and ownership
boundary do not depend on that interface.

Illustrative shape:

```csharp
public sealed class ExampleInput
{
    // Source-neutral immutable values only.
}

public sealed class ExampleOptions
{
    // Explicit numerical limits and deterministic search controls only.
}

public sealed class ExampleResult
{
    // Controlled state, typed outputs, and algorithm evidence only.
}

public sealed class ExampleTool
{
    public ExampleResult Execute(
        ExampleInput input,
        ExampleOptions options,
        CancellationToken cancellationToken = default)
    {
        // Library-Noah owns the calculation.
    }
}
```

## Studio boundary

Studio may own:

- product contracts and source/unit/frame/artifact identity validation;
- recipe parameter parsing, ROI binding, graph routing, and persistence;
- strict conversion to Noah input and from Noah result;
- canonical Studio artifact hashing and retained evidence linkage;
- authored acceptance policy that only compares Noah-owned metrics with
  explicit limits;
- explicit Preview, Publish, Run, and Validation orchestration;
- Viewer camera, color, layout, overlay composition, and presentation-only
  sampling;
- localized status, error, and next-action presentation.

If any of those surfaces starts calculating a fit, distance, transform,
correspondence, neighborhood result, distribution, measurement, candidate
ranking, or statistical estimate, that calculation must become a Noah Tool.
Studio may iterate to map or compose already calculated evidence; it may not
use product-layer iteration as a hidden numerical kernel.

## Current audit result

The inventoried Studio numerical migration is complete. The current vendored
Noah API owns Thickness, Warpage, datum deviation, affine solve/apply,
two-point line,
three-point plane, line fit/intersection, median filtering, height-difference
edge extraction, reference-grid regridding, plane fitting, five additional
measurement families, and Surface Match pose search/coverage.

After the model key-point addition, the machine-readable decreasing migration
baseline records `0` Studio migration-debt files and `33` reviewed
Studio-boundary files:

- `docs/OPENVISIONLAB_3D_NOAH_TOOL_MIGRATION_BASELINE_20260801.json`.

Nominal/actual mesh comparison, triangle-distance lookup, and registration
transform diagnostics join SurfaceModel/PreparedScene preparation,
model/scene edge extraction, edge coverage, local-median outlier filtering,
and Level Surface as reviewed Studio adapters over `Lib.ThreeD 2.8.13`.
Height-grid summary/distribution, Source Quality distribution, Completeness
Grid, ROI statistics, and reference-grid reconstruction are also reviewed
boundaries over public Noah Tools. Dual Surface Thickness and Height Deviation
are reviewed strict adapters over their public Noah Tools. Declared mesh-normal
quality and Landmark Correspondence are also strict adapters over their public
Noah Tools. Thickness and Aligned Point repeatability are strict adapters over
`RepeatabilityStatisticsTool`. Labeled-evidence descriptive statistics, C3D
ROI aggregation, and threshold-candidate analysis are strict adapters over
`LabeledEvidenceStatisticsTool`, `HeightMapRegionStatisticsTool`, and
`ThresholdCandidateAnalysisTool`. Multiple-match result collection is a strict
adapter over `DeterministicMultipleSurfaceMatchTool`; Noah owns repeated pose
search, unique-nearest scoring, disjoint scene-sample claiming, result order,
and bounded termination.
Symmetry-aware pose equivalence is a strict adapter over
`RigidPoseSymmetryEquivalenceTool`; Noah owns `reference rotation * declared
symmetry operation`, translation/rotation residuals, inclusive decisions, and
stable operation tie-breaking. Studio owns SurfaceModel, pose, unit, source-
frame, target-frame, limit, and typed evidence validation.
Model key-point extraction is a strict adapter over
`DeterministicModelKeyPointExtractionTool`; Noah owns deterministic
farthest-point selection, while Studio owns retained-domain identity,
persistence, validation, and display-only overlay composition.

`Landmark Correspondence` has no numerical exception. Studio keeps its recipe
and artifact identity; rank and normalized-volume calculation are owned by
`LandmarkCorrespondenceValidationTool`.

## Automated guard

`scripts/verify-code-structure.ps1` now treats the JSON file as a decreasing
migration baseline, not an approved exception list.

The guard verifies:

1. the contract and schema-1 baseline exist and agree;
2. every listed source file and target Noah Tool name is explicit;
3. every heuristically detected Studio numerical owner is classified as
   migration debt or a reviewed Studio boundary;
4. no listed file increases its numerical-signal count;
5. no new unclassified numerical owner appears in the scanned Tools and
   relevant Data directories;
6. all eight Surface Match execution adapters continue to call the Noah
   public Tools without restoring their former arithmetic;
7. nominal/actual comparison, triangle-distance, and rigid-transform
   diagnostics continue to call their Noah public Tools without restoring
   their former arithmetic;
8. height-map summary/distribution, Completeness Grid, region statistics, and
   reference-grid reconstruction continue to call their Noah public Tools
   without restoring their former arithmetic;
9. Dual Surface Thickness and Height Deviation continue to call their Noah
   public Tools without restoring residual/statistical arithmetic;
10. declared mesh-normal quality and Landmark Correspondence continue to call
    their Noah public Tools without restoring geometry arithmetic;
11. repeatability rules continue to call `RepeatabilityStatisticsTool` without
    restoring scalar accumulation arithmetic;
12. validation-statistics analyzers continue to call their Noah public Tools
    without restoring aggregation, candidate-generation, classification,
    ranking, or tie-break arithmetic.
13. model key-point extraction continues to call its Noah public Tool without
    restoring point-selection arithmetic in Studio.

A passing guard proves that new debt was not introduced relative to the
zero-debt baseline. Future algorithm work must remain in committed Noah; the
debt count must never be increased merely to make the guard pass.

## Required migration workflow

For every debt item or new algorithm:

1. inspect the currently vendored Noah public API;
2. define source-neutral Tool input/options/result and controlled failures;
3. implement and verify the Tool in a clean Library-Noah worktree;
4. commit the exact Noah source;
5. pack from that commit and record package version, commit, and SHA-256;
6. vendor the package without a cross-repository `ProjectReference`;
7. replace Studio arithmetic with strict adaptation and evidence composition;
8. prove observable parity or record an intentional contract change;
9. remove or narrow the corresponding baseline entry;
10. run Library-Noah, package, Studio, Runner/Workbench parity, and structure
    checks proportionate to the migrated family.

Do not package an uncommitted Noah working tree. Do not temporarily implement
new arithmetic in Studio for later migration.

## Migration order

1. Filtering and preparation: local-median outlier removal and leveling - complete.
2. Mesh comparison and transform diagnostics - complete.
3. Height-map summary/distribution, Completeness, region statistics, and
   reference-grid reconstruction - complete.
4. Dual-surface thickness and Height Deviation - complete.
5. Declared-normal quality and Landmark Correspondence validation - complete.
6. Repeatability statistics - complete.
7. Labeled-evidence statistics and threshold candidates - complete.
8. `J-12 Multiple-match result collection` - complete in committed Noah
   `4e301f481cac886f78425197314cd540b653473a` and vendored
   `Lib.ThreeD 2.8.9`.
9. `J-13 Symmetry-aware pose equivalence` - complete in committed Noah
   `f225fd2709de1dd1d0ecfe19b37315cb1f019ee4` and vendored
   `Lib.ThreeD 2.8.10`.
10. `J-05 Model surface selection` - complete in committed Noah
    `55ea7a61bd1281294e91aa5366d2bafb509d3667` and vendored
    `Lib.ThreeD 2.8.11`.
11. `J-07 Model key-point artifact and debug overlay` - complete in committed
    Noah `7ed50ea37b3d7cb711c2afe698d209f9073e9217` and vendored
    `Lib.ThreeD 2.8.12`.

Human-owner Wide/Compact R0 remains an external usability acceptance task and
does not replace, or get replaced by, this numerical-ownership gate.

## Completion record

Status: Complete

Scope: Noah Tool shape, Studio boundary, zero-debt numerical baseline, and
no-new-debt structure gate, including the completed validation-statistics,
multiple Surface Match, symmetry-aware pose-equivalence, and model surface-
selection and model key-point migrations.

Acceptance criteria: Tool contract is explicit; all audited Studio numerical
owners are migrated or classified as strict reviewed boundaries; new or
expanded Studio numerical ownership fails the structure guard; multiple Surface
Match arithmetic remains in Noah and the known two-object collection is stable,
ordered, disjoint, and tamper-checked; symmetry-equivalence arithmetic remains
in Noah and direct/cyclic typed evidence is controlled and deterministic;
exact duplicate and explicit source-triangle surface selection remains in Noah
and its active domain is preserved by Studio; model key-point selection remains
in Noah while Studio preserves stable sample/triangle identity, persistence,
and display-only overlay evidence.

Verification: `scripts/verify-code-structure.ps1` passes `29/29`, including
the contract, schema-1 inventory, zero-debt, and no-new-ownership checks. The
vendored Library-Noah package boundary passes for `Lib.ThreeD 2.8.13`, source
commit `21f2e3084843ef8a499e6fe02c4326a19813aa2c`, package SHA-256
`852B5A959A3DD76AF69A7C295CEAC77E13F72BBB969A79FC48D88A83B9D8229D`,
and `netstandard2.0`. The Noah bridge passes `25/25`, J-07 passes `15/15`,
legacy byte parity passes `5/5`, multiple-match Runner passes `14/14`,
Workbench passes `10/10`, the existing matching foundation passes `34/34`,
and Validation Set passes `84/84`.

Evidence: this document, the schema-1 JSON baseline,
`docs/OPENVISIONLAB_3D_MODEL_KEY_POINT_ARTIFACT_AND_DEBUG_OVERLAY_20260803.md`,
and
the current-task reports under
`D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260803-j07-model-key-points\`.

Boundary / next dependency: The inventoried Studio numerical debt is closed,
but this is not a physical-calibration, metrology, production-readiness, or
human-usability claim. J-07 key points are display/debug evidence and are not
matching inputs. The next dependency-ready slice is `B-12 Acquisition/source
provenance text and limitation notes`; `K-04` remains blocked on B-12.
