# OpenVisionLab 3D Studio

English | [한국어](README.ko.md)

Teach inspection steps on 3D data, save a recipe, and apply it to compatible files.

OpenVisionLab 3D Studio is a Windows desktop application for setting up and
repeating rule-based inspections on local 3D data. Connect tool inputs, define inspection regions and set
limits, then review the measurements and results for each file.

[![CI](https://github.com/Noah8218/OpenVisionLab-3D-Studio/actions/workflows/ci.yml/badge.svg)](https://github.com/Noah8218/OpenVisionLab-3D-Studio/actions/workflows/ci.yml)
![Windows](https://img.shields.io/badge/platform-Windows%2010%2F11%20x64-0078D4) ![.NET](https://img.shields.io/badge/.NET-10-512BD4) [![License](https://img.shields.io/badge/license-Apache--2.0-green)](LICENSE)

![Measure warpage, compensate the height map, inspect thickness, save and reopen](docs/assets/openvisionlab-3d-linked-inspection.gif)

## Try it in 3 steps

1. Clone `https://github.com/Noah8218/OpenVisionLab-3D-Studio.git` and enter the repository directory.
2. [Build and run the Shell](#build-from-source).
3. In **Recipe Center**, open the [Thickness Coupon Recipe](3D/Samples/ThicknessCouponV1/inspection-recipe.ov3d-recipe.json).
   Keep its paired input file in the same folder. See the [sample guide](3D/Samples/ThicknessCouponV1/README.md) for the input and declared
   `raw-height` units.

If you already have a self-contained Windows package, extract it and run
`OpenVisionLab.ThreeD.Shell.exe`. Its .NET runtime is included. Follow the
[Windows package quick start](docs/OPENVISIONLAB_3D_WINDOWS_PACKAGE_QUICK_START.md).

## What it does

- Connect compatible tool inputs and define ROIs in a Recipe.
- Prepare a HeightMap and measure Warpage or datum-referenced Thickness.
- Inspect compatible files as a batch and review per-file results and saved Run Records.
- View 3D data, height images and sections, with aligned appearance textures on C3D height maps.

## From input to inspection results

Open a recipe and its 3D input, adjust the teaching settings, run the inspection,
and open the results for a file.

![Warpage before compensation on a textured synthetic height map](docs/assets/openvisionlab-3d-warpage-before.png)

1. Open a saved recipe, or create one and load a 3D file.
2. Add tools, connect compatible inputs, and define the required ROIs.
3. Apply the settings and run the selected tool to check its result.
4. Save the recipe, then add compatible files to the inspection list.
5. Run the inspection and review each file's status, values and limits.
6. Open a saved record to review the conditions and results from that run.

Loading a file, selecting a result, editing parameters, restoring saved setup
or changing display settings does not start an inspection or sample validation.

![Nine thickness results on the compensated map after saving and reopening](docs/assets/openvisionlab-3d-thickness-after-reopen.png)

## Included Thickness Coupon tutorial data

The repository and the self-contained package include a ready-to-run Thickness
Coupon with eight independently inspectable pads.

![Thickness Coupon with paired reference and measurement ROIs](3D/Samples/ThicknessCouponV1/source-height-preview.png)

| Item | Included value |
| --- | --- |
| Height grid | 1280 × 840 |
| Layout | Eight pads in a 4 × 2 arrangement |
| Recipe | Eight Thickness steps with 16 editable ROIs |
| ROI contract | Reference and Measurement ROI remain on the same visible pad |
| Declared unit | `raw-height` |

Open this recipe from **Recipe Center → Open existing recipe**:

```text
3D\Samples\ThicknessCouponV1\inspection-recipe.ov3d-recipe.json
```

The recipe keeps its C3D input beside the recipe file, so the complete example
works after cloning or moving the whole package folder.

## Build the tool sequence

Combine preparation and measurement tools in the recipe. Set the inputs,
regions and parameters for each step. Run the complete recipe to pass each
created output to its connected downstream tools.

![Run linked preparation and measurement steps](docs/assets/openvisionlab-3d-linked-inspection.gif)

| Task | Tools | What to review |
| --- | --- | --- |
| Prepare a height map | Median filter, noise removal, leveling, nine-point surface compensation, ROI crop | Input and processed height maps |
| Measure surface warpage | Warpage | Peak-to-valley and RMS deviation within the ROI |
| Measure height separation | Thickness | Signed height difference from the reference plane to the measurement region |

Tool availability depends on the input type and required regions. Height-map
measurement tools require a compatible height-map input. Meshes and point
clouds can be viewed separately.

<details>
<summary>Other inspection tools and input limits</summary>

## Supported inspection work

| Area | Current workflow |
| --- | --- |
| Input review | C3D recipe-source loading plus Viewer-only GLB, STL, LAS, and LAZ import, with progress/cancel, visible limitations, source-quality evidence, and optional source-frame sensor-to-scene direction |
| Preparation | Median filtering, outlier removal, surface leveling, and explicit ROI/Crop into a separate immutable HeightField; compatible later tools can teach against the created crop |
| Height inspection | Thickness, Warpage, Plane Flatness, Height Deviation, Gap/Flush, Volume, and grid/region statistics |
| Geometry inspection | Point-pair and cross-section measurements, lines, planes, edges, landmarks, nominal/actual mesh comparison, and retained surface-match review |
| Teaching | Linked 3D and full-resolution Height Image editing for GridRectangle, GridCircle, and ordered GridPolygon selections with explicit Review, Apply, Cancel, and Delete actions; GridPolygon stores the authoring outline; the separate Grid Polygon Domain step explicitly rasterizes a compatible selection into a grid-domain output |
| Validation | Good, Bad, and Held-out sample roles; run results; failure analysis; threshold review; and held-out replay |
| Evidence | Viewer overlays, metrics, reports, output comparison, retained match review, headless Runner records, and a privacy-safe support ZIP |
| Recipe lifecycle | Save and restore ordered steps, inputs, parameters, ROI roles, outputs, and validation setup |

Measurements use the unit declared by the source. A `raw-height` result is not
automatically a calibrated physical measurement. Apply and verify the correct
calibration and acceptance limits before using physical tolerances.

Use the import button in the 3D Viewer toolbar for supported local 3D data.
`C3D` becomes the recipe input; `GLB`, `STL`, `LAS`, and `LAZ` are displayed in
the Viewer only and leave the recipe input unchanged. The application does not
advertise `.gltf`, OBJ, PCD, XYZ, TIFF, or RAW as general import formats.

Surface-match experiments remain review-only; they are not general inspection
tools.

</details>

## Source quality and support evidence

Source Quality provides an acquisition/source contract editor. Record whether
acquisition evidence is available, the evidence and known limitations, and,
when supplied, the source-frame `Sensor → scene` XYZ direction. Choose
**Apply source contract** to apply it. The normalized direction can classify
existing edge normals for display; it does not change matching or infer a
camera pose, calibration, or viewpoint from geometry.

When support evidence must be shared, open **Results → Run Record** and choose
**Export privacy-safe support bundle**. The ZIP includes a manifest, sanitized
recipe, the newest 200 session-log entries at most, source identity, recorded
Source Quality, and the current result. It omits raw 3D source or mesh bytes,
absolute paths, the full application log, and user or machine identity by
default. Review the ZIP before sending it. The separate full Run Record export
is not the privacy-safe sharing path.

## Warpage measurement

Fit a reference plane to valid points in the selected ROI and review the
surface deviations as peak-to-valley (PV) and RMS values. Nine-point surface
compensation creates a separate corrected height map for subsequent inspection.
The synthetic example below measures 538.13 µm before compensation and 0 µm
after compensation.

![Measure initial warpage, apply nine-point compensation and inspect the corrected map](docs/assets/openvisionlab-3d-linked-inspection.gif)

## Thickness measurement

Define a reference ROI and a measurement ROI. The Thickness tool reports the
signed H-axis height difference between the fitted reference plane and the
measurement region. This is datum-referenced height separation; it does not
measure a paired upper and lower surface. Connect the compensated height map
to the Thickness steps and review their values and OK/NG labels in the 3D
Viewer. The illustrated nine-region example has seven OK and two NG results.


## 3D viewing and textures

Use 3D, height-image and section views to inspect the input and tool results.
The viewer provides orbit, pan, fit, coordinate readouts and ROI overlays.
Displayed numbers use up to two decimal places; calculations and saved values
keep their original precision.

For a C3D height map, open **Color and height display**, then <strong>Choose image…</strong>
to select an image aligned to the full height map. **Show texture** switches
between the image and height colors; **Remove** clears the texture. The saved
selection is restored for the same recipe and source after the image file
is validated. Texture display does not change height values or inspection inputs.

<details>
<summary>Compact layout</summary>

![Compensated-map measurements in the Compact layout](docs/assets/openvisionlab-3d-linked-compact.png)

</details>

The demonstrations use synthetic C3D data and photographic appearance textures. Image
brightness is not a height measurement, and the example is not a calibrated
camera capture. The bundled [Thickness Coupon](3D/Samples/ThicknessCouponV1/README.md)
provides a ready-to-run recipe with declared `raw-height` units.

## Build from source

Use Windows 10 build 19041 or later, or Windows 11, on x64, with Git, PowerShell
5.1 or later, and the SDK selected by [global.json](global.json) (`10.0.300`, `latestFeature`).
The viewer requires an OpenGL-compatible GPU and driver. Open
`OpenVisionLab.ThreeDStudio.slnx` (or the equivalent `.sln`) and select
`OpenVisionLab.ThreeD.Shell` as the startup project.

```powershell
dotnet restore OpenVisionLab.ThreeDStudio.slnx
dotnet build OpenVisionLab.ThreeDStudio.slnx -c Release --no-restore
dotnet run --project src/OpenVisionLab.ThreeD.Shell/OpenVisionLab.ThreeD.Shell.csproj -c Release --no-build
```

Pinned dependencies are included in `third_party`; [NuGet.Config](NuGet.Config)
configures package sources. A separate SDK checkout is not required.
See [source setup and project navigation](docs/OPENVISIONLAB_3D_SYSTEM_REQUIREMENTS_AND_SETUP.md) for details.

Python 3.13 is required for the full independent verification suite, not for
a normal source build or application launch. See the
[development and verification guide](docs/OPENVISIONLAB_3D_SYSTEM_REQUIREMENTS_AND_SETUP.md#local-paths-and-tests)
for test commands.

<details>
<summary>Check or install source-build prerequisites</summary>

Check the machine without changing it:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\setup-development-environment.ps1 -CheckOnly -Scope Build
```

Install missing build utilities by their fixed `winget` package IDs:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\setup-development-environment.ps1 -InstallMissing -Scope Build
```

Use a short local clone path to avoid Windows and NuGet path-length failures.
The setup guide also covers a short NuGet cache and post-reinstall recovery.

</details>

The main application is `src/OpenVisionLab.ThreeD.Shell`.
`src/OpenVisionLab.ThreeDStudio` is a Viewer-only host. Follow the
[code navigation route](docs/OPENVISIONLAB_3D_SYSTEM_REQUIREMENTS_AND_SETUP.md#start-here)
when choosing a startup project.

## Create the self-contained Windows package

From a source clone:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\publish-windows-app.ps1
```

The default output is
`artifacts\release\openvisionlab-3d-studio-win-x64`. It is a folder-based,
self-contained `win-x64` package with the application, samples, recipes,
operator documentation, license notices, and a SHA-256 manifest.

The package quick-start source is available at
[Windows package quick start](docs/OPENVISIONLAB_3D_WINDOWS_PACKAGE_QUICK_START.md).

## Keyboard shortcuts

| Shortcut | Action |
| --- | --- |
| `Ctrl+N` | New recipe |
| `Ctrl+O` | Open recipe |
| `Ctrl+Shift+O` | Open C3D height input |
| `Ctrl+S` | Save recipe |
| `Ctrl+Shift+S` | Save recipe as |
| `F5` | Run the selected tool |
| `Ctrl+F5` | Run the complete recipe |
| `Enter` | Apply the current ROI candidate |
| `Esc` | Cancel the current ROI candidate |
| `Delete` | Delete the selected supported ROI or recipe item |

## Documentation

**Use the app:** [First inspection](docs/OPENVISIONLAB_3D_USER_TUTORIAL.md) · [Sample data](docs/OPENVISIONLAB_3D_SAMPLE_DATA.md)

**Develop:** [SDK package boundary](docs/OPENVISIONLAB_3D_VISION_SDK_PACKAGE_BOUNDARY_20260805.md) · [Executable and Viewer Host projects](docs/OPENVISIONLAB_3D_SYSTEM_REQUIREMENTS_AND_SETUP.md#start-here) · [Documentation index](docs/README.md)

- [Development and verification guide](docs/OPENVISIONLAB_3D_SYSTEM_REQUIREMENTS_AND_SETUP.md#local-paths-and-tests)
- [Sample data and attribution](3D/PublicSamples/README.md)

- [Contributor rules and versioning](AGENTS.md)

Current version: `v0.8.1-dev` · [Changelog](CHANGELOG.md)

## Synthetic inspection examples

These captures use synthetic height maps. Textures show appearance; measurements
come from the height data. They do not establish sensor accuracy or LAS picking.

![Tool selection and inspection results on synthetic input](docs/assets/openvisionlab-3d-tools.gif)

![Warpage inspection on a synthetic height map](docs/assets/openvisionlab-3d-warpage.gif)

![Thickness inspection with nine result labels](docs/assets/openvisionlab-3d-thickness.gif)

![Textured synthetic inspection in the Wide layout](docs/assets/openvisionlab-3d-texture-wide.png)

![Textured synthetic inspection in the Compact layout](docs/assets/openvisionlab-3d-texture-compact.png)

## Status and limitations

- The latest hosted Workbench checks fail in docking/render cleanup, threshold-candidate review and one initial property-editor caption. Cause classification is still in progress; this snapshot is not fully CI-qualified.

- Final native confirmation of Dock-menu return and four distinct picks in large-origin LAS is still pending. Completed computation and media checks do not establish those two behaviors.

- This version is a development source snapshot, not a production qualification. Tags, releases and deployment have separate approval steps.
- Measurement units follow the input data; the bundled example declares `raw-height` units and is not a calibrated camera capture.
- Tool selection can still pause the interface. Some native dialog recovery, floating-panel and held-button states remain unverified.

## Part of OpenVisionLab

| Project | What it does |
| --- | --- |
| [2D Workbench](https://github.com/Noah8218/OpenVisionLab) | Rule-based 2D inspection Recipes |
| [3D Studio](https://github.com/Noah8218/OpenVisionLab-3D-Studio) | 3D teaching, Thickness and Warpage |
| [Labeling Studio](https://github.com/Noah8218/OpenVisionLab-Labeling-Studio) | Labeling, training and model comparison |
| [Machine Studio](https://github.com/Noah8218/OpenVisionLab-Machine-Studio) | Machine simulation before hardware |
| [Vision SDK](https://github.com/Noah8218/OpenVisionLab-Vision-SDK) | Shared C# inspection library |

## License

OpenVisionLab 3D Studio is licensed under the
[Apache License 2.0](LICENSE). Commercial use, modification, and redistribution
are permitted under its terms. Distributions must retain the `LICENSE`,
[NOTICE](NOTICE), copyright, and required attribution notices. Third-party components
remain subject to their respective licenses.

```text
This project includes software developed by Noah Choi.
Copyright (c) 2026 Noah Choi.
```
