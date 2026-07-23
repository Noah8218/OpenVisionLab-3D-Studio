# OpenVisionLab 3D Ordered Graph Validation

Date: 2026-07-23

## Outcome

The dockable bilingual `Validation Set` now replays the complete currently
supported typed inspection chain against each explicitly selected same-grid
C3D sample. This replaces the earlier raw Thickness/Warpage-only execution
boundary.

The operator contract remains:

1. open or teach one Inspection Recipe;
2. add an explicit ordered C3D sample list;
3. choose `Run all`;
4. review Pass/Fail/Error per sample;
5. select a sample and inspect its ordered step evidence.

Adding or selecting samples does not change the authored recipe or the current
Viewer input. Authoring Preview and Publish remain explicit.

## Execution design

`ToolRecipeOrderedGraphExecution` is the single per-sample orchestrator. It:

- verifies the source file, SHA-256, and taught grid dimensions;
- loads one verified raw C3D snapshot;
- creates an ephemeral recipe copy;
- rebinds raw C3D selections and refreshes raw PointSet captures by locator;
- executes typed steps strictly in authored INPUT -> OUTPUT order;
- stores each published typed artifact under the exact output entity ID;
- after Re-grid, rebinds artifact-owned selections to the new A3 owner, root
  source hash, grid, frame, unit, and artifact hash;
- records status, evidence, output entity ID, and output content SHA-256 for
  each step;
- stops on an upstream Error/NotRun/missing artifact;
- continues after a measurement tolerance Fail so later measurement evidence
  remains available;
- leaves the authored `ToolRecipeDocument` unchanged.

No algorithm was copied into Studio. Filter, feature, affine, re-grid, and
measurement arithmetic continue through the established typed adapters and
Library-Noah ownership boundary.

## Supported typed tools

- Median Filter
- Height Difference Edge
- 2-Point Line
- 3-Point Plane
- Datum Plane Raw-Height Deviation
- 3D Line Fit
- Line Intersection
- Landmark Correspondence
- XYZ Affine Solve
- Apply XYZ Affine
- Re-grid Height Map
- Thickness
- Warpage
- Plane Flatness
- Point Pair Dimensions
- Gap / Flush
- Volume
- Cross-section Dimensions

Tools without an executable adapter, including the current structural
`ROI / Crop` catalog entry, fail closed and are not reported as executed.

## Deterministic acceptance evidence

The fixed `Synthetic Affine Inspection Plate v1` recipe contains 27 ordered
steps:

`C3D -> Filter -> 8 Edge -> 8 Line Fit -> 4 Intersection -> Correspondence -> A1 -> A2 -> A3 -> Thickness -> Warpage`

Three explicit same-grid samples prove the result boundary:

- Pass: all 27 steps complete;
- Fail: a modified Thickness ROI fails tolerance and all 27 steps, including
  later Warpage, still produce evidence;
- Error: a missing first edge band stops at step 2 with zero accepted
  scanlines.

The original recipe path and source SHA-256 remain unchanged after all three
runs. The general executor's Filter, Correspondence, A1, A2, A3, Thickness,
and Warpage output hashes match the established direct-adapter outputs.

## Verification

Current Debug commands:

```powershell
dotnet build "OpenVisionLab.ThreeDStudio.slnx" -c Debug

dotnet run --no-build `
  --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj `
  -c Debug -- `
  --verify-validation-set `
  artifacts\current\20260723-ordered-graph-validation\validation-set-verification.txt

dotnet run --no-build `
  --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj `
  -c Debug -- `
  --verify-synthetic-affine-inspection-plate `
  --synthetic-affine-package 3D\SyntheticValidation\AffineInspectionPlateV1 `
  --report artifacts\current\20260723-ordered-graph-validation\synthetic-affine-verification.txt

dotnet run --no-build `
  --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj `
  -c Debug -- `
  --verify-tool-recipe-teaching `
  artifacts\current\20260723-ordered-graph-validation\recipe-teaching-regression.txt

dotnet run --no-build `
  --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj `
  -c Debug -- `
  --verify-workbench-docking `
  artifacts\current\20260723-ordered-graph-validation\docking-regression.txt
```

Results:

- solution build: `0 warnings / 0 errors`;
- ordered Validation Set: `16/16`;
- Synthetic Affine Inspection Plate: `18/18`;
- recipe teaching regression: `25/25`;
- docking regression: `26/26`;
- Korean and English actual-current-EXE screenshot quality: accepted on
  attempt 1.

## Evidence

- `artifacts/current/20260723-ordered-graph-validation/before-ko.png`
- `artifacts/current/20260723-ordered-graph-validation/after-ko.png`
- `artifacts/current/20260723-ordered-graph-validation/after-en.png`
- `artifacts/current/20260723-ordered-graph-validation/validation-set-verification.txt`
- `artifacts/current/20260723-ordered-graph-validation/synthetic-affine-verification.txt`
- `artifacts/current/20260723-ordered-graph-validation/recipe-teaching-regression.txt`
- `artifacts/current/20260723-ordered-graph-validation/docking-regression.txt`
- `artifacts/current/20260723-ordered-graph-validation/validation-set-fixture/`

## Claim boundary and next gate

This proves deterministic, sequential software replay for the currently
registered typed adapters. It does not prove arbitrary DAG execution,
parallel/batch production infrastructure, camera/PLC integration, physical
calibration, sensor fidelity, Gauge R&R, or metrology.

The next gate is real multi-piece validation using a trusted four-landmark
acquisition set with declared unit/frame/provenance. If that data is not
available, the next internal product priority is a durable general graph Run
Record/export that reuses this executor without duplicating orchestration.

## Closure record

Status: Complete

Scope: general fresh-sample ordered replay through the supported typed
Filter/feature/datum/full-XYZ affine/A3/measurement chain, integrated into
Validation Set.

Acceptance criteria: 27-step synthetic Pass -> passed; measurement Fail with
later evidence -> passed; upstream Error fail-close -> passed; output hash
parity -> passed; authored recipe immutability -> passed; bilingual actual EXE
evidence -> passed.

Verification: solution build `0/0`; Validation Set `16/16`; Synthetic Affine
Plate `18/18`; recipe teaching `25/25`; docking `26/26`; Korean/English
screenshot-quality attempt 1.

Evidence:
`docs/OPENVISIONLAB_3D_ORDERED_GRAPH_VALIDATION_20260723.md` and
`artifacts/current/20260723-ordered-graph-validation/`.

Boundary / next dependency: trusted real same-fixture acquisitions with
unit/frame/provenance are required before physical or metrology claims.
