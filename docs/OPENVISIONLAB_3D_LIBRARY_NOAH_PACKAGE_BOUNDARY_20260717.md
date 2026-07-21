# Library-Noah 3D Package Boundary

## Purpose

`Lib.ThreeD` is the reusable, UI-free height-map inspection package owned by
Library-Noah. OpenVisionLab 3D Studio consumes it through a fixed NuGet package
instead of an adjacent-checkout `ProjectReference`.

This keeps Studio CI, a deployed Viewer bundle, and a developer machine independent
from `C:\Git\Library-Noah` while preserving a reviewable source commit and package
hash.

## Fixed Input

| Item | Value |
| --- | --- |
| Package ID | `Lib.ThreeD` |
| Version | `2.3.0` |
| Source commit | `630e37b9111f3223217c815e19c480546fde8ad7` |
| Target | `netstandard2.0` |
| Vendored path | `third_party/LibraryNoah/Lib.ThreeD.2.3.0.nupkg` |
| SHA-256 | `5143A6D270DB60751EDD825ABBC64A49B4612E149A60DF094F24D1ED3A7F21F8` |

`NuGet.Config` adds only a relative `third_party/LibraryNoah` source plus NuGet.org.
No Studio project may point at the Library-Noah checkout.

## Responsibility Split

- `Lib.ThreeD`: immutable scalar height-map contracts; thickness limits; warpage
  plane-fit residual metrics; source-neutral two-point line, line intersection,
  and exact-four full-XYZ affine solve; controlled error outcomes.
- `OpenVisionLab.ThreeD.Tools`: `LibraryNoahHeightMapInspection` translates Studio's
  declared source, grid ROI, unit, and frame into the package contract, then maps
  result status and metrics back to Studio `ToolResult`.
- `OpenVisionLab.ThreeD.Runner`: verifies the package assembly identity plus pass,
  tolerance failure, invalid ROI, missing metadata, analytic plane, and insufficient
  sample behaviors.
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

## Current 2.3.0 checkpoint — 2026-07-21

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
