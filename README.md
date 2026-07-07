# OpenVisionLab 3D Studio

[![CI](https://github.com/Noah8218/OpenVisionLab-3D-Studio/actions/workflows/ci.yml/badge.svg)](https://github.com/Noah8218/OpenVisionLab-3D-Studio/actions/workflows/ci.yml)

OpenVisionLab 3D Studio is an early-stage Windows desktop project for rule-based 3D inspection. The immediate goal is not a full production platform; it is a reliable 3D viewer and inspection workbench for loading local 3D data, measuring geometry, showing result overlays, and replaying repeatable validation recipes.

This repository is under active development and is not production-ready yet.

## 1 Minute Summary

- Product direction: local 3D vision inspection workbench.
- Current focus: SharpGL/WPF viewer foundation before deeper 3D algorithms.
- Current viewer scope: camera control, C3D height-grid rendering, picking, entity visibility, measurement HUD, two-point and ROI step-height measurement, overlays, recipe load/save, and screenshot smoke evidence.
- Current rule scope: first C3D height-deviation recipe and a headless runner path.
- Out of early scope: camera control, PLC, robot, cloud, deployment management, production database, and full CAD editing.

## Requirements

- Windows
- .NET SDK 8.0.x
- Git

The project owner plans to evaluate .NET 10 later, but the current checked build targets .NET 8.

## Install And Run

```powershell
git clone https://github.com/Noah8218/OpenVisionLab-3D-Studio.git
cd OpenVisionLab-3D-Studio
dotnet restore OpenVisionLab.ThreeDStudio.slnx
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug
```

To run the docked workbench shell:

```powershell
dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug
```

## Sample Data

Local sample data is expected under `3D\`:

| Path | Role |
| --- | --- |
| `3D\Thickness\Ori_20240116_094414.C3D` | Current C3D height-grid sample used by viewer and runner smoke. |
| `3D\Thickness\Ori_20240116_094414.png` | 2D reference image for the Thickness sample. |
| `3D\Warpage\Ori_20240116_094430.C3D` | Current Warpage candidate sample. |
| `3D\Warpage\Ori_20240116_094430.png` | 2D reference image for the Warpage sample. |

The current C3D layout is inferred as `int32 width`, `int32 height`, then `float32` height/depth samples. Treat that as an implementation observation until an official C3D specification is confirmed.

More details: `docs\OPENVISIONLAB_3D_SAMPLE_DATA.md`.

## Build Command

```powershell
dotnet build OpenVisionLab.ThreeDStudio.slnx -c Debug
```

## Smoke Commands

Viewer screenshot and contract smoke:

```powershell
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_two_point_after.png --smoke-c3d thickness --smoke-measure two-point --smoke-contracts artifacts\viewer_two_point_after.txt
```

ROI step-height smoke:

```powershell
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_roi_step_after.png --smoke-c3d thickness --smoke-measure roi-step --smoke-contracts artifacts\viewer_roi_step_after.txt
```

Shell-hosted viewer smoke:

```powershell
dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --shell-smoke-screenshot artifacts\shell_viewer_internal_hud_after.png --smoke-measure two-point
```

Headless recipe runner smoke:

```powershell
dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --recipe recipes\c3d-height-deviation.recipe.json --report artifacts\ci\runner_c3d_height_rule.txt --expect-status Fail
```

The expected `Fail` status is intentional for the current sample recipe because the sample exceeds the configured height-deviation tolerance.

## CI

GitHub Actions workflow: `.github\workflows\ci.yml`.

CI currently runs on `windows-latest` and performs:

1. `dotnet restore OpenVisionLab.ThreeDStudio.slnx`
2. `dotnet build OpenVisionLab.ThreeDStudio.slnx -c Debug --no-restore`
3. Headless C3D recipe runner smoke
4. CI artifact upload from `artifacts\ci\`

## Release Notes

No packaged binary release is published yet.

Current development snapshot:

- SharpGL/WPF viewer foundation.
- Standalone viewer host and docked shell host.
- C3D height-grid sample rendering and picking.
- Viewer-internal coordinate, measurement, and performance HUD.
- Two-point distance and height-delta measurement smoke.
- ROI step-height comparison smoke.
- First C3D height-deviation recipe and headless runner.
- Windows GitHub Actions CI build and runner smoke.

## Roadmap

1. Finish the viewer completion gate: reliable display, camera, picking, selection, overlays, color modes, screenshots, and MVVM state separation.
2. Define transform/alignment state before CAD, multi-sensor, or broader algorithm work.
3. Harden 3D entity, layer, metric, overlay, and result contracts.
4. Add more rule-based validation tools with UI preview and headless runner coverage.
5. Expand file formats and heavier geometry libraries only after the core inspection loop is verified.

## Known Limitations

- The project is not production-ready.
- Current C3D parsing is inferred from local samples, not an official format contract.
- The current Thickness and Warpage sample files may be byte-identical; do not assume they represent different measurements until new evidence is available.
- Algorithm coverage is intentionally narrow; the viewer is still being completed first.
- No camera, PLC, robot, cloud, deployment, account, or production database integration exists.
- No packaged installer or binary release exists yet.
- .NET 10 migration is planned as a separate compatibility task, not mixed into current feature work.

## Documentation

- `AGENTS.md`: repository working rules and verification commands.
- `docs\CODEBASE_STRUCTURE.md`: project layout.
- `docs\OPENVISIONLAB_3D_PLATFORM_DIRECTION.md`: product direction and roadmap.
- `docs\OPENVISIONLAB_3D_SAMPLE_DATA.md`: sample inventory and C3D observations.
- `docs\OPENVISIONLAB_3D_NEXT_SESSION_HANDOFF.md`: current engineering handoff.
