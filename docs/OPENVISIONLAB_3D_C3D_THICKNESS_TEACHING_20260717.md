# C3D Thickness Teaching v1

Updated: 2026-07-17

## Purpose

This is the first feature-first vertical inspection slice. It turns the local C3D Thickness height grid into a teachable inspection step rather than a repeatability input.

```text
Load C3D height grid
  -> enter Thickness ROI Teach mode
  -> click one grid location or edit the grid ROI
  -> set lower/upper scalar limits and minimum valid samples
  -> explicit Preview
  -> inspect ROI overlay and metrics in Viewer and Shell
  -> explicit Publish
  -> save/reopen typed recipe
  -> replay the same typed recipe in Runner
```

## Scope

- `C3DThicknessRecipe` owns a stable step, source, ROI reference, grid ROI, acceptance, unit, frame, and minimum-sample contract.
- `C3DThicknessRule` adapts `LibraryNoahHeightMapInspection.EvaluateThickness`; the Tool result owns nine metrics and one ROI overlay.
- The View only forwards C3D mouse picks and renders the taught ROI. `MainWindowViewModel` owns ROI, command, preview, result, and publish state.
- The result is visible in the standalone Viewer HUD and editor, plus Shell Data & Layers, Tool / Inspector, Evidence Workbench, linked state, entity layers, and saved Runner JSON/HTML/CSV.

## Current Evidence

The current local Thickness sample uses the taught grid ROI `(row=900, column=570, rows=160, columns=160)`.

| Metric | Current local result |
| --- | --- |
| Status | Pass |
| Mean | `1649.236 raw-height` |
| Minimum | `-32.936 raw-height` |
| Maximum | `2035.136 raw-height` |
| Range | `2068.072 raw-height` |
| Valid samples | `24,201` |
| Limits | `[-100.000, 2500.000] raw-height` |

Current-source evidence is under `artifacts/thickness_teaching_20260717`:

- `c3d_thickness_golden_after_message_fix.txt`: Tool contract golden `5/5`.
- `viewer_thickness_after.png`: standalone Viewer Preview, Publish, and saved recipe capture.
- `shell_thickness_after.png`: hosted Shell result capture.
- `runner_viewer_compare_thickness.txt`: saved Viewer recipe replayed by Runner with the Viewer contract comparison accepted.

## Reproduce

```powershell
dotnet build OpenVisionLab.ThreeDStudio.slnx -c Debug --no-restore
dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --verify-c3d-thickness --report artifacts\c3d_thickness\golden.txt
dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --recipe recipes\c3d-thickness.recipe.json --report artifacts\c3d_thickness\runner.txt
```

## Claim Boundary

The sample's declared scalar unit is `raw-height`; it is not a calibrated physical thickness unit. This slice does not infer sensor scale, part datum, physical thickness, repeatability, uncertainty, Gauge R&R, or metrology certification.

## Next Functional Priority

The bounded local raw-height `c3d-warpage` slice is now implemented under the
user-designated `3D/Warpage` input path. It reuses the same typed C3D grid-ROI
teaching interaction and the existing Library-Noah plane-fit tool, but its
result is explicitly a local display-frame/raw-height result, not a physical
warpage, datum, GD&T, or independently acquired part claim. See
`docs/OPENVISIONLAB_3D_WARPAGE_INPUT_PREFLIGHT_20260717.md`.

Do not add a durable multi-run result list merely because Thickness and Warpage
now have two task names. The next higher-trust priority is distinct source and
acquisition provenance with declared scalar meaning, unit, frame, reference,
and acceptance ownership. A physical claim still requires separate calibration
evidence.
