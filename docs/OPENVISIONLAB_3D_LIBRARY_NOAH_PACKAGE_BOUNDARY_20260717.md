# Library-Noah 3D Package Boundary

## Purpose

`Lib.ThreeD` is the reusable, UI-free height-map inspection package owned by
Library-Noah. OpenVisionLab 3D Studio consumes it through a fixed NuGet package
instead of an adjacent-checkout `ProjectReference`.

This keeps Studio CI, a deployed Viewer bundle, and a developer machine independent
from `C:\Git\Library-Noah` while preserving a reviewable source commit and package
hash.

## Current fixed input - 2026-08-01

| Item | Value |
| --- | --- |
| Package ID | `Lib.ThreeD` |
| Version | `2.8.6` |
| Source commit | `3ef2f52546a9187df465bf8973e26426c30f7634` |
| Target | `netstandard2.0` |
| Vendored path | `third_party/LibraryNoah/Lib.ThreeD.2.8.6.nupkg` |
| SHA-256 | `02E0D0B69F9D7CECBA958BF4BDC7F2999D0902539C33CD0F133C48C08C3A25B0` |

The current package includes the previously migrated inspection tools,
Surface Match pose-search/coverage, deterministic SurfaceModel and Prepared
Scene sampling, model/organized-scene edge extraction, and edge-domain
coverage. It now also includes center-excluded local-median outlier filtering
and deterministic height-field surface leveling, nominal/actual mesh
comparison, triangle-distance queries, and rigid-transform diagnostics.
It additionally owns raw height-grid summaries and distributions, rectangular
region statistics, Completeness Grid inspection, and declared/reference-axis
grid-point reconstruction through five public sealed Tools. It additionally
owns dual-surface thickness residual/statistical evaluation and height-summary
peak-deviation decisions through two public sealed Tools. It also owns declared
mesh-normal topology/alignment quality and four-point landmark rank/normalized-
volume validation through two public sealed Tools.
Studio consumes them through strict adapters and retains product
contracts, identities, unit/frame validation, acceptance, lifecycle,
evidence, and UI.

`NuGet.Config` adds only a relative `third_party/LibraryNoah` source plus
NuGet.org. No Studio project may point at a Library-Noah checkout.

## Historical fixed input - initial boundary

| Item | Value |
| --- | --- |
| Package ID | `Lib.ThreeD` |
| Version | `2.3.0` |
| Source commit | `630e37b9111f3223217c815e19c480546fde8ad7` |
| Target | `netstandard2.0` |
| Vendored path | `third_party/LibraryNoah/Lib.ThreeD.2.3.0.nupkg` |
| SHA-256 | `5143A6D270DB60751EDD825ABBC64A49B4612E149A60DF094F24D1ED3A7F21F8` |

The table above is retained as the initial package-boundary record and is not
the current package selection.

## Responsibility Split

- `Lib.ThreeD`: immutable scalar height-map contracts; thickness limits; warpage
  plane-fit residual metrics; source-neutral two-point line, line intersection,
  and exact-four full-XYZ affine solve; controlled error outcomes.
- `OpenVisionLab.ThreeD.Tools`: `LibraryNoahHeightMapInspection` translates Studio's
  declared source, grid ROI, unit, and frame into the package contract, then maps
  result status and metrics back to Studio `ToolResult`.
- `OpenVisionLab.ThreeD.Runner`: verifies the package assembly identity, the
  established inspection behaviors, and deterministic results from all seven
  height-map/height-rule Tools (`14/14` current bridge cases).
- View/ViewModel: the bounded Thickness and local raw-height Warpage task slices
  consume this bridge through typed recipes and explicit Preview/Publish commands.
  The Warpage source is user-designated and declares `raw-height` plus its display
  frame; it does not establish a calibrated unit, physical frame, datum, or
  source-to-grid metrology mapping.

## Guardrails

- A declared `Unit` and `FrameId` are mandatory at the Studio bridge boundary.
- `double.NaN` represents a missing scalar sample; infinity and invalid grid geometry
  are controlled errors.
- A package `Fail` remains a measurement result. Invalid input, ROI, or insufficient
  data becomes a Studio `Error` and is not presented as a tolerance failure.
- This bridge does not convert a C3D display height into physical thickness or
  calibrated Warpage. The local Viewer overlay represents an explicit raw-height
  best-fit residual result only; it is not a calibrated scalar-map or GD&T claim.
- Do not publish a new package from an uncommitted Library-Noah working tree.

## Update Checklist

1. Commit the Library-Noah source changes.
2. Build, run `Lib.Inspection.Smoke`, and pack `Lib.ThreeD` from that commit.
3. Verify the package nuspec ID, version, target, license entries, and source commit.
4. Copy the package into `third_party/LibraryNoah` and update its SHA-256 sidecar and
   this document together.
5. Update `LibraryNoahHeightMapInspection.PackageVersion` and
   `PackageSourceCommit` only with the matching package.
6. Run the Studio package verifier, bridge verifier, restore, build, and NuGet health
   gate before requesting a push.

## Verification

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\verify-library-noah-package.ps1 `
  -ReportPath artifacts\library_noah_package_boundary.txt

dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj `
  -c Debug --no-build -- --verify-library-noah-3d `
  --report artifacts\library_noah_3d_bridge.txt
```

The fixed local baseline on 2026-07-17 passed package integrity and `7/7` bridge
cases. A same-commit revalidation also confirmed that the vendored package and NuGet
restore-cache package have SHA-256
`C4D119D12EB607874882BB34E65EC264A9F78CF188C785A61FF79CEFF1D895E5`, and the
clean `C:\Git\Library-Noah` source at commit
`b113ee8099ffcfe9f75f34928b0e214b542b75fb` passed `Lib.Inspection.Smoke` `14/14`.
That source build initially emitted four `Lib.Common` compiler warnings (`CS0168`
twice and `CS0219` twice). A later local, warning-only cleanup makes the current
Library-Noah working tree dirty and passes its Release build with `0` warnings and
`0` errors plus the same Smoke `14/14`; it was not repacked, so it is not contained
in the immutable vendored `Lib.ThreeD` `2.1.0` package. This is package and
algorithm-boundary evidence only, not physical calibration, metrology, Gauge R and
R, or a completed Viewer inspection workflow.

Studio commit `c45ce78` passed Windows Actions run `29569056102` on 2026-07-17. The job's vendored-package and Studio bridge steps succeeded alongside the full Viewer/Runner suite; uploaded artifact metadata is ID `8402387241`, `3,727,932` bytes, and digest `sha256:24080e4ef536a56a5c56a5178822ecfb885c4ae71d96c145e339ded4e0045787`. GitHub's public archive endpoint requires authentication, so this local environment did not independently download or inspect that archive. Library-Noah warning-cleanup commit `c2b5860` separately passed Build run `29569055985`.

## Historical 2.3.0 checkpoint — 2026-07-21

Library-Noah commit `630e37b9111f3223217c815e19c480546fde8ad7` is the exact
source of the current vendored package. It adds pure `LineIntersectionTool` to
the preceding `TwoPointLineTool` and `FullXyzAffineSolveTool`. Studio's A1 and
Line Intersection rules adapt those algorithms and retain only C3D/recipe
identity, Studio artifact hashing, and lifecycle evidence. Package integrity,
Studio bridge, A1 Golden, Line Intersection Golden, and full Studio regression
evidence passed from the current 2.3.0 package: Library-Noah build `0/0`,
Smoke `20/20`, Studio build `0/0`, package integrity pass, Studio bridge
`7/7`, A1 Golden `4/4`, Line Intersection Golden `9/9`, Line Intersection
Workbench `23/23`, teaching `18/18`, Recipe Manager/WPG `18/18`, docking
`25/25`, and Artifact Navigator `24/24`. Reports are under
`artifacts/verification/20260721-noah-migration/`. This does not prove a real
fixture, affine application, calibration, or metrology.

## Historical 2.8.0 checkpoint - 2026-08-01

Library-Noah commit `7d1ad8721ca7aed9efa2a17beaa36409d7dbd718` is the exact
source of the current vendored package. The committed source passes Release
build `0/0` and full Smoke `69/69`. Package metadata records the same commit;
the packed, vendored, and restored-cache artifacts share SHA-256
`7378C02ABDED9C02F1448CDF80577B00A7AD99E78BC2B722E341DD7513CE754C`.

Studio passes package/bridge `7/7`, Surface Match `34/34`, acceptance `14/14`,
edge `21/21`, edge diagnostic/review `20/20`, SurfaceModel `22/22`, fixed
performance `18/18`, Workbench/Runner parity `23/23`, structure `18/18`, and
NuGet health `12 projects / 0 vulnerable / 0 deprecated`. The known-pose
result is byte-identical before and after migration. Preserve
`docs/OPENVISIONLAB_3D_SURFACE_MATCH_NOAH_MIGRATION_20260801.md` and
`artifacts/current/20260801-surface-match-noah-migration/`.

## Current 2.8.6 checkpoint - 2026-08-01

Library-Noah commit `3ef2f52546a9187df465bf8973e26426c30f7634` is the
exact source of the current vendored package. It adds public sealed
`DeclaredMeshNormalQualityTool` and
`LandmarkCorrespondenceValidationTool` while retaining every prior 2.8.5 API.

The package SHA-256 is
`02E0D0B69F9D7CECBA958BF4BDC7F2999D0902539C33CD0F133C48C08C3A25B0`.
Package integrity and nuspec provenance pass; the Studio direct package bridge
passes `16/16`. Baseline/current normal-quality and Landmark Correspondence
reports have exact normalized parity. Preserve
`docs/OPENVISIONLAB_3D_DECLARED_NORMAL_QUALITY_AND_LANDMARK_CORRESPONDENCE_NOAH_MIGRATION_20260801.md`
and
`artifacts/current/20260801-noah-normal-quality-landmark-migration/`.

## Earlier 2.8.5 checkpoint - 2026-08-01

Library-Noah commit `ec8f1b3db57bea0065cd82735acb08111f88f3c0` is the exact
source of the current vendored package. It adds public sealed
`DualSurfaceThicknessInspectionTool` and `HeightDeviationInspectionTool`
while retaining every prior 2.8.4 API.

The committed source passes Release `0/0` and full Smoke `92/92`. Package
metadata records the same commit, and the packed/vendored artifact has
SHA-256
`3BE4E7F83CC4A9E3542C6FCA9C38C5F13D2BFEE703F78035CB9082DC0B5EBCDB`.
Studio Release passes `0/0`; package integrity passes; bridge `14/14`, generic
height-measurement Workbench `54/54`, actual Height Deviation parity,
Validation Set `84/84`, and structure `26/26` pass. The decreasing ledger is
`6` migration-debt and `24` reviewed-boundary files. Preserve
`docs/OPENVISIONLAB_3D_DUAL_SURFACE_THICKNESS_AND_HEIGHT_DEVIATION_NOAH_MIGRATION_20260801.md`
and
`artifacts/current/20260801-noah-dual-thickness-height-deviation-migration/`.

## Historical 2.8.4 checkpoint - 2026-08-01

Library-Noah commit `a64c31b1024f154e402d258ade4b70470ad50fb2` is the exact
source of the current vendored package. It adds public sealed
`HeightGridSummaryTool`, `HeightDistributionStatisticsTool`,
`HeightMapRegionStatisticsTool`, `CompletenessGridInspectionTool`, and
`ReferenceGridPointReconstructionTool` while retaining every prior 2.8.3 API.

The committed source passes Release `0/0` and full Smoke `86/86`. Package
metadata records the same commit, and the packed/vendored artifact has
SHA-256
`0F4FB2A1115C0247E03BA85D335BE40241FD02A6F5694FE6E36B872CB3A846F5`.
Studio Release passes `0/0`; package integrity passes; the expanded bridge
passes `12/12`; map fidelity `10/10`; Source Quality `13/13`; Completeness
Grid `23/23`; Height distribution `25/25`; generic height-measurement
Workbench `54/54`; and structure `25/25` with `8` migration-debt and `22`
reviewed-boundary files. Preserve
`docs/OPENVISIONLAB_3D_HEIGHT_MAP_INSPECTION_PREPARATION_NOAH_MIGRATION_20260801.md`
and
`artifacts/current/20260801-noah-height-map-inspection-preparation-migration/`.

## Historical 2.8.3 checkpoint - 2026-08-01

Library-Noah commit `4420c40d3179edc7703cfef6e0ea53ac898f8f3f` is the exact
source of the current vendored package. It adds public sealed
`TriangleMeshDistanceTool`, `NominalActualMeshComparisonTool`, and
`RigidTransformDiagnosticsTool` while retaining every prior 2.8.2 API.

The committed source passes Release `0/0` and full Smoke `81/81`. Package
metadata records the same commit, and the packed/vendored artifact has
SHA-256
`63F70F92354257E6E2975753BC17A76118478CB6AB0C77EB487C09F5A50F0C39`.
Studio Release passes `0/0`; package integrity and bridge `7/7` pass; focused
mesh deviation, nominal/actual, and registration acceptance checks pass
`23/23`, `29/29`, and `20/20`; all three focused before/after reports are
exact; and structure passes `24/24` with `12` migration-debt and `16`
reviewed-boundary files. Preserve
`docs/OPENVISIONLAB_3D_NOMINAL_COMPARISON_AND_TRANSFORM_DIAGNOSTICS_NOAH_MIGRATION_20260801.md`
and
`artifacts/current/20260801-noah-nominal-registration-migration/`.

## Historical 2.8.2 checkpoint - 2026-08-01

Library-Noah commit `3a2cbf8e7195d6f251dcafe6a9343b795d53fe79` is the exact
source of the current vendored package. It adds public sealed
`DeterministicLocalMedianOutlierFilterTool` and `LevelSurfaceTool` while
retaining every prior 2.8.1 API.

The committed source passes Release `0/0` and full Smoke `78/78`. Package
metadata records the same commit, and the packed/vendored artifact has
SHA-256
`EF397381CDD3344E3BAB7A7F29FF6124451DA6A1FCB1BC007B0BFDB284A0BFD7`.
Studio package integrity and bridge pass, both focused Runner goldens pass
`9/9`, the `28` comparable pre/post report lines are identical, and structure
passes `23/23` with `15` migration-debt and `13` reviewed-boundary files.
Preserve
`docs/OPENVISIONLAB_3D_OUTLIER_FILTER_AND_LEVELING_NOAH_MIGRATION_20260801.md`
and
`artifacts/current/20260801-noah-outlier-leveling-migration/`.

## Historical 2.8.1 checkpoint - 2026-08-01

Library-Noah commit `46cfa0946bb4c23190b0dab75415ce2c637b4c41` is the exact
source of the current vendored package. It adds public deterministic Tools for
SurfaceModel and Prepared Scene sampling, mesh boundary/crease extraction,
organized-scene height-step extraction, and edge-domain coverage. The latter
reuses the shared unique-nearest coverage kernel and accepts an empty scene
edge set as valid zero-coverage evidence.

The committed source passes Release build `0/0` and full Smoke `75/75`.
Package metadata records the same commit, and the packed/vendored artifact has
SHA-256
`3C908BB6671D2F89C7BC9DDEC601CD10A33A0905D78A8A24A276DA9BAAFF4445`.

Studio passes Release `0/0`, package/bridge `7/7`, SurfaceModel `22/22`,
matching `34/34`, acceptance `14/14`, performance `18/18`, edge `21/21`,
edge review `20/20`, Workbench parity `14/14`, `12/12`, and `13/13`, and
structure `22/22`. All 24 pre/post JSON artifact files are byte-identical.
Preserve
`docs/OPENVISIONLAB_3D_SURFACE_PREPARATION_EDGE_NOAH_MIGRATION_20260801.md`
and
`artifacts/current/20260801-noah-surface-preparation-edge-migration/`.
