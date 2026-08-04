# OpenVisionLab 3D SurfaceModel Preparation Foundation

Date: 2026-07-31

Status: Complete

Backlog scope: `J-01`, `J-03`, `J-04`

Superseding extension (2026-08-03): `F-13` adds schema-`1.1` explicit
none/discrete-model-axis symmetry declarations while preserving exact bytes
and canonical hashes for undeclared schema-`1.0` artifacts. See
`docs/OPENVISIONLAB_3D_SURFACE_MODEL_SYMMETRY_DECLARATION_20260803.md`.

## Outcome

OpenVisionLab 3D Studio now has a WPF-neutral, identified, content-addressed
`SurfaceModel` artifact; deterministic triangle-centroid preparation with
explicit sampling parameters; atomic save/load; and fail-closed
point/triangle/normal/sample validation.

This is a reusable software foundation, not a matching UI or a complete
surface matcher. It does not copy GoPxL layout, theme, terminology, assets, or
code. It addresses the OpenVisionLab requirement for explicit source
identity, deterministic preparation, and replayable evidence.

## Included scope

### J-01: identified artifact

`SurfaceModelArtifact` schema `1.0` records:

- artifact ID and display name;
- source entity ID, source SHA-256, and source format;
- unit, frame ID, and `source-cartesian-xyz` coordinate convention;
- all source points, oriented triangles, and declared point normals;
- exact preparation parameters;
- ordered sampled points with source-triangle locators;
- one canonical SHA-256 over every persisted semantic field.

`SurfaceModelArtifactStore`:

- validates before save and after load;
- writes UTF-8 JSON through a write-through temporary file and atomic replace;
- rejects malformed JSON, unsupported schema, invalid geometry, and content
  identity mismatch;
- preserves the last valid artifact when a replacement is rejected.

### J-03: deterministic preparation

`SurfaceModelPreparation` accepts an `ImportedMesh` and an explicit request.
Version 1 uses
`deterministic-triangle-centroid-even-index-v1`.

Parameters are:

- maximum sample count;
- minimum triangle area;
- unit-normal tolerance;
- minimum normal/triangle-winding alignment cosine.

The preparation path:

1. requires the existing `B-16` dense declared-normal evidence to be valid;
2. preserves all source points, triangle order, and declared normals;
3. selects unique triangles in a deterministic even-index schedule;
4. stores one centroid and normalized interpolated normal per selected
   triangle;
5. calculates the artifact SHA-256 from the complete prepared content.

Changing only the maximum sample count changes both the sample collection and
the model hash. Repeating the same input and parameters produces the same
hash. The source mesh is not mutated.

### J-04: validity checks

`SurfaceModelArtifactValidator` fails closed for:

- missing identity, unsupported schema, or non-canonical source/content hash;
- empty or non-finite points;
- out-of-range or repeated triangle indices;
- degenerate or below-minimum-area triangles;
- missing, partial, non-finite, zero, non-unit, or reversed normals;
- samples with wrong order, duplicate/wrong triangle locator, non-finite
  position, non-unit normal, wrong centroid, or inconsistent normal;
- a sample count that differs from the deterministic parameter contract.

The validator returns a typed `SurfaceModelValidityReport`; it never repairs
the artifact.

## Ownership

| Responsibility | Owner |
| --- | --- |
| Artifact, canonical hash, sampling schedule, validity report | `OpenVisionLab.ThreeD.Core` |
| Atomic JSON persistence and imported-mesh normal evidence | `OpenVisionLab.ThreeD.Data` |
| Pure imported-mesh-to-model preparation | `OpenVisionLab.ThreeD.Tools` |
| Known-valid/invalid fixtures and headless closure evidence | `OpenVisionLab.ThreeD.Runner` |

No WPF, Shell, Viewer, recipe state, ROI state, Preview, Publish, Run, or
Validation action is owned by this slice.

## Verification

Commands:

```powershell
dotnet build "OpenVisionLab.ThreeDStudio.sln" -c Release -p:Platform="Any CPU"

dotnet run --no-build -c Release `
  --project src/OpenVisionLab.ThreeD.Runner/OpenVisionLab.ThreeD.Runner.csproj -- `
  --verify-surface-model-foundation `
  --report artifacts/current/20260731-surface-model-foundation/surface-model-foundation-verification.txt

dotnet run --no-build -c Release `
  --project src/OpenVisionLab.ThreeD.Shell/OpenVisionLab.ThreeD.Shell.csproj -- `
  --verify-source-channel-normal-quality `
  artifacts/current/20260731-surface-model-foundation/source-channel-normal-quality-report.txt

dotnet run --no-build -c Release `
  --project src/OpenVisionLab.ThreeD.Shell/OpenVisionLab.ThreeD.Shell.csproj -- `
  --verify-source-quality-workspace `
  artifacts/current/20260731-surface-model-foundation/source-quality-workspace-report.txt

powershell -NoProfile -ExecutionPolicy Bypass `
  -File scripts/verify-code-structure.ps1 `
  -ReportPath artifacts/current/20260731-surface-model-foundation/code-structure-report.txt
```

Results:

| Check | Result |
| --- | --- |
| Release solution build | Pass, `0` warnings / `0` errors |
| SurfaceModel foundation | Pass, `22/22` |
| Existing source channel + dense normal | Pass, `26/26` |
| Existing Source Quality workspace | Pass, `18/18` |
| Code structure | Pass, `17/17` |
| Human R0 launcher `-ValidateOnly` | Pass, Wide and Compact; no application launched |

The known-valid square artifact has `4` finite points, `2` index-valid
non-degenerate triangles, `4` finite/non-zero/unit normals, `6/6` aligned
triangle corners, `1/1` valid sample, and content SHA-256
`084EF0B6919673CB43817CA6ED50526BF20761B2D7FB0C609D8E35D28BB1A82B`.

This slice does not change UI, UX, layout, navigation, or visible text.
Wide/Compact screenshot evidence is therefore not applicable.

## Evidence

- `artifacts/current/20260731-surface-model-foundation/surface-model-foundation-verification.txt`
- `artifacts/current/20260731-surface-model-foundation/known-valid.surface-model.json`
- `artifacts/current/20260731-surface-model-foundation/source-channel-normal-quality-report.txt`
- `artifacts/current/20260731-surface-model-foundation/source-quality-workspace-report.txt`
- `artifacts/current/20260731-surface-model-foundation/code-structure-report.txt`

## Boundaries

This historical completion did not:

- remove internal, redundant, or unobservable surfaces (`J-05`; superseded by
  the completed bounded contract in
  `OPENVISIONLAB_3D_MODEL_SURFACE_SELECTION_20260803.md`);
- prepare a measured scene (`J-06`);
- create model key points (`J-07`);
- search for a rigid pose (`J-08`);
- define a surface-coverage score (`J-09`);
- render a transformed model or prove Workbench/Runner matching parity
  (`J-10/J-16`);
- prove physical calibration, normal accuracy, or metrology.

Human-owner Wide/Compact R0 remains the external acceptance prerequisite for
`A-01`; automated evidence does not replace it.

## Completion record

Status: Complete

Scope: `J-01` identified `SurfaceModel` artifact and atomic persistence;
`J-03` deterministic parameterized preparation and sampled-model hash;
`J-04` point/triangle/normal/sample validity.

Acceptance criteria:

- save/load and stable content identity -> Pass;
- repeated preparation has the same hash -> Pass;
- changed sampling parameter changes samples and hash -> Pass;
- known-valid fixture passes -> Pass;
- non-finite point, bad/degenerate triangle, missing/non-unit/reversed normal,
  bad sample locator, corrupt JSON, unsupported schema, and tampered hash fail
  closed -> Pass;
- source remains unchanged -> Pass;
- existing source-quality and project structure gates remain valid -> Pass.

Verification: Release build `0/0`; focused `22/22`; source/normal `26/26`;
Source Quality `18/18`; structure `17/17`; R0 Wide/Compact `-ValidateOnly`
Pass.

Evidence:
`artifacts/current/20260731-surface-model-foundation/`, this document, the
master backlog, and the refreshed fixed-hash R0 launcher.

Boundary / next dependency: `J-06/J-08/J-09` is the next dependency-ready
software slice. It must define identified measured-scene preparation, a
bounded rigid-pose result, and explicit surface-coverage score semantics
before any transformed overlay or acceptance limit is claimed.
