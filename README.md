# OpenVisionLab 3D Studio

A Windows workbench for teaching and replaying rule-based 3D inspections.

[![CI](https://github.com/Noah8218/OpenVisionLab-3D-Studio/actions/workflows/ci.yml/badge.svg)](https://github.com/Noah8218/OpenVisionLab-3D-Studio/actions/workflows/ci.yml)

Use height maps, meshes, and point clouds to review input quality, teach regions,
run tools explicitly, and review inspection results and records.

## Version

Current version: `v0.6.0-dev`

This project is maintained using explicit version numbers. This is a development
source snapshot. It is not a production qualification or a binary release.
Viewer Host API `1.1` remains unchanged.

### Recent version history

#### `v0.6.0-dev` (2026-10-03)

- Updated the Graphite interface across tool setup, recipe nodes, viewer controls,
  results, recipe management, and tool labs.
- Improved numeric-draft validation and small-panel access to coordinates and
  height-profile controls.
- Included current pinned integration dependencies and refreshed public
  source-build and usage documentation.

#### `v0.5.7-dev` (2026-09-16)

- Improved C3D input admission, crop/axis/grid identity, Height Measurement,
  and Run Record evidence.
- Added coordinate/thickness, source stability, memory admission, and
  independent Viewer consumer checks.

#### `v0.5.6-dev` (2026-09-14)

- Consolidated Shell, Workbench, and Viewer command, state, event, and lifetime
  ownership while preserving inspection and save/reopen workflows.
- Connected HeightMap/projection consumption to V2 exchange and result records.

#### `v0.5.5-dev` (2026-09-10)

- Moved Viewer display and teaching coordination to the Workbench coordination
  module while preserving Viewer and recipe contracts.

#### `v0.5.4-dev` (2026-09-09)

- Added Nominal/Actual comparison display snapshots for readiness, progress,
  evidence, and deviation summaries.
- Updated Viewer Host API to `1.1` while retaining existing construction paths.

#### `v0.5.3-dev` (2026-09-05)

- Bounded the LAS/LAZ sample cache to three recently used entries.
- Prevented stale asynchronous scene and telemetry updates.

#### `v0.5.2-dev` (2026-09-05)

- Restored Verification sources needed for clean public builds.
- Preserved recipe, Run Record, and Viewer Host API contracts.

#### `v0.5.1-dev` (2026-09-05)

- Consolidated Shell workflow, dialog, persistence, startup, and smoke lifetimes.
- Separated Viewer loading, rendering resources, caches, transforms, and colors.

#### `v0.5.0-dev` (2026-09-04)

- Improved asynchronous cancellation, dispatcher, and resource lifetime ownership.
- Added the dedicated Verification project and independent Viewer consumer path.

#### `v0.4.9-dev` (2026-09-01)

- Added shared WPF-neutral source-load coordination for C3D and viewer-only input.
- Suppressed obsolete Workbench state after newer operations.

## Inspection workflow

1. Open a recipe and its input, or start a recipe in Recipe Center.
2. Review source quality, declared units, coordinates, and required inputs.
3. Select a tool, connect compatible inputs, and teach its regions.
4. Apply edited settings and choose **Run selected tool** for the current step.
5. Choose **Run all** to execute the complete recipe.
6. Review the results, save the recipe, and reopen it when needed.

Opening a recipe, connecting nodes, changing a camera, or restoring settings does
not run an inspection. Tool Lab Preview/Apply remains an explicit workflow.

The viewer provides 3D and height-image views, coordinate readouts, ROI teaching,
and two-point height profiles. Geometry-only imports remain viewer data until a
supported inspection input is configured.

Use the included [Thickness Coupon](3D/Samples/ThicknessCouponV1/README.md) for a
reproducible starting input. Its `raw-height` unit is deliberately non-physical.
The product does not infer calibration or certify metrology accuracy.

## Start here

Use [source setup and project navigation](docs/OPENVISIONLAB_3D_SYSTEM_REQUIREMENTS_AND_SETUP.md)
for the canonical build and startup route. Open `OpenVisionLab.ThreeDStudio.slnx`
(or the equivalent `.sln`) and select `OpenVisionLab.ThreeD.Shell` as the startup
project for the workbench.

```powershell
dotnet restore OpenVisionLab.ThreeDStudio.slnx
dotnet build OpenVisionLab.ThreeDStudio.slnx -c Release --no-restore
dotnet run --project src/OpenVisionLab.ThreeD.Shell/OpenVisionLab.ThreeD.Shell.csproj -c Release --no-build
```

Windows, Git, PowerShell, and the SDK selected by [global.json](global.json)
are required. Running the viewer also requires a suitable OpenGL driver.
Pinned product dependencies are included in `third_party` and selected through
[NuGet.Config](NuGet.Config); a separate SDK or integration checkout is not
required for this source build.

## Documentation

- [First inspection](docs/OPENVISIONLAB_3D_USER_TUTORIAL.md)
- [Public sample data](docs/OPENVISIONLAB_3D_SAMPLE_DATA.md)
- [SDK ownership boundary](docs/OPENVISIONLAB_3D_VISION_SDK_PACKAGE_BOUNDARY_20260805.md)
- [Viewer Host display contract](docs/OPENVISIONLAB_3D_VIEWER_HOST_DISPLAY_BOUNDARY_20260909.md)
- [Documentation index](docs/README.md)

## Current validation boundary

The source includes automated contract checks and focused WPF rendering checks.
The updated interface still needs actual mouse/keyboard, popup, OpenGL, native
100%/125% scaling, and full save/reopen/repeated-inspection verification.
A build or a rendered bitmap does not establish those operational results.
The current integration alpha.4 dependencies also have a separate source
qualification and cross-application round-trip boundary.

## License

[Apache License 2.0](LICENSE). See [NOTICE](NOTICE) and the vendored dependency
notices for third-party attribution.
