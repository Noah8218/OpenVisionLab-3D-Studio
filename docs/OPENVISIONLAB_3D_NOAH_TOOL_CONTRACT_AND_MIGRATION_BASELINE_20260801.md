# OpenVisionLab 3D Library-Noah Tool Contract and Migration Baseline

Date: 2026-08-01

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

After the validation-statistics migration, the machine-readable decreasing
migration baseline records `0` Studio migration-debt files and `30` reviewed
Studio-boundary files:

- `docs/OPENVISIONLAB_3D_NOAH_TOOL_MIGRATION_BASELINE_20260801.json`.

Nominal/actual mesh comparison, triangle-distance lookup, and registration
transform diagnostics join SurfaceModel/PreparedScene preparation,
model/scene edge extraction, edge coverage, local-median outlier filtering,
and Level Surface as reviewed Studio adapters over `Lib.ThreeD 2.8.8`.
Height-grid summary/distribution, Source Quality distribution, Completeness
Grid, ROI statistics, and reference-grid reconstruction are also reviewed
boundaries over public Noah Tools. Dual Surface Thickness and Height Deviation
are reviewed strict adapters over their public Noah Tools. Declared mesh-normal
quality and Landmark Correspondence are also strict adapters over their public
Noah Tools. Thickness and Aligned Point repeatability are strict adapters over
`RepeatabilityStatisticsTool`. Labeled-evidence descriptive statistics, C3D
ROI aggregation, and threshold-candidate analysis are strict adapters over
`LabeledEvidenceStatisticsTool`, `HeightMapRegionStatisticsTool`, and
`ThresholdCandidateAnalysisTool`.

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
6. all seven Surface Match execution adapters continue to call the Noah
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
8. `J-12 Multiple-match result collection`, with all new matching arithmetic
   implemented in committed Noah first.

Human-owner Wide/Compact R0 remains an external usability acceptance task and
does not replace, or get replaced by, this numerical-ownership gate.

## Completion record

Status: Complete

Scope: Noah Tool shape, Studio boundary, zero-debt numerical baseline, and
no-new-debt structure gate, including the completed validation-statistics
migration.

Acceptance criteria: Tool contract is explicit; all audited Studio numerical
owners are migrated or classified as strict reviewed boundaries; new or
expanded Studio numerical ownership fails the structure guard; the final
validation-statistics adapters preserve observable reports exactly.

Verification: `scripts/verify-code-structure.ps1` passes `29/29`, including
the contract, schema-1 inventory, zero-debt, and no-new-ownership checks. The
vendored Library-Noah package boundary passes for `Lib.ThreeD 2.8.8`, source
commit `0fe04bc967fa89918b3c6d937566cce56de69682`, package SHA-256
`D62B050710C4CCA0309B3FA49CDCDBB239C675944E29C085E50CD198D4D15405`,
and `netstandard2.0`. Validation Set passes `84/84`, and its normalized
before/after full report has `0` differences.

Evidence: this document, the schema-1 JSON baseline,
`docs/OPENVISIONLAB_3D_VALIDATION_STATISTICS_NOAH_MIGRATION_20260801.md`, and
the current-task reports under
`D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260801-noah-validation-statistics-migration\`.

Boundary / next dependency: The inventoried Studio numerical debt is closed,
but this is not a physical-calibration, metrology, production-readiness, or
human-usability claim. The next implementation slice is `J-12 Multiple-match
result collection`; all new matching arithmetic must be added to committed
Noah first.
