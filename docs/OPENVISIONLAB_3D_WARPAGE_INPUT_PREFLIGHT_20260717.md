# OpenVisionLab 3D C3D Warpage Input Preflight and Local Slice Record

Updated: 2026-07-17

Status: local raw-height Warpage input accepted by explicit user folder
designation; physical/metrology claims remain blocked.

## Decision

The user explicitly designated
`C:\Git\OpenVisionLab-3D-Studio\3D\Warpage` as the input for the current
Warpage workflow. That authorizes one local `c3d-warpage` recipe that evaluates
declared scalar `raw-height` values as residuals from a best-fit inspection-ROI
plane.

The designation does **not** establish a distinct acquisition, sensor
provenance, physical unit, datum, calibration, or GD&T Warpage claim. The
Warpage file is byte-identical to the local Thickness file, so it is accepted
only as a named local recipe input/alias for this experimental workflow.

## Verified Candidate Evidence

The following direct file checks were run from the current Studio workspace on
2026-07-17.

| Comparison | SHA-256 | Direct byte comparison |
| --- | --- | --- |
| `3D/Thickness/Ori_20240116_094414.C3D` | `79C02761F9B711C0F8980D4376B9FCE25E00D425E6CA85DA4D4349ECF5F0299C` | Baseline |
| `3D/Warpage/Ori_20240116_094430.C3D` | `79C02761F9B711C0F8980D4376B9FCE25E00D425E6CA85DA4D4349ECF5F0299C` | `fc.exe /b` exit 0; no differences |
| `3D/Thickness/Ori_20240116_094414.png` | `97C8CAE2D39746398BEDE57FC66FD552AC95910287FA48C9B13968E4175A31A8` | Baseline |
| `3D/Warpage/Ori_20240116_094430.png` | `97C8CAE2D39746398BEDE57FC66FD552AC95910287FA48C9B13968E4175A31A8` | `fc.exe /b` exit 0; no differences |

The C3D file contains an Int32 width, an Int32 height, and `1301 x 1967`
single-precision values. Its expected and actual length are both `10,236,276`
bytes. It has no embedded sensor, unit, calibration, reference-plane, or
acquisition identity block. Both paths entered Git together in commit
`d728e45f6c9`; repository history adds no acquisition provenance.

## Accepted Local Input Contract

| Contract item | Accepted local value |
| --- | --- |
| Source path | `3D/Warpage/Ori_20240116_094430.C3D` |
| Source entity ID | `source.c3d-warpage` |
| Declared scalar meaning | `raw-height` scalar samples only |
| Unit | `raw-height` |
| Frame | `frame.c3d-grid-index` |
| Reference mode | `BestFitInspectionRoi` |
| Inspection ROI | row `900`, column `570`, rows `160`, columns `160` |
| Minimum valid samples | `3` |
| Acceptance | maximum P2V `10000 raw-height`; RMS shown but not limited |
| Claim level | local, uncalibrated best-fit residual check |

No new runtime schema was invented for this designation. The typed recipe
records the values above and rejects missing IDs, invalid ROI geometry, fewer
than three valid samples, non-finite values, and non-positive limits.

## Implemented Local Vertical Slice

- `C3DWarpageRecipe` and `C3DWarpageRule` keep the Studio recipe identity,
  best-fit ROI contract, fixed metric order, result overlay, and explicit claim
  boundary while reusing `LibraryNoahHeightMapInspection.EvaluateWarpage`.
- The Shell task selector switches between Thickness and Warpage. Warpage Teach
  exposes one best-fit ROI, one P2V limit, `Teach ROI`, and explicit `Preview
  Warpage`; Inspect exposes Preview and Publish; Review reads the published
  result only.
- The Viewer keeps camera and selection state in `MainWindowViewModel`, clears
  unrelated transient overlays when it loads a Warpage task recipe, and renders
  the current result overlay only after explicit Preview.
- `Runner` loads `recipes/c3d-warpage.recipe.json`, emits JSON/HTML/CSV Run
  Record output, and has a dedicated analytic/error golden command.

Current local result for the accepted ROI is `Pass`: `24,201` valid samples,
P2V `2018.5327096550388 raw-height`, RMS `261.37727205702936 raw-height`,
minimum residual `-1559.0635684734232`, and maximum residual
`459.46914118161567`. The result is not physical Warpage.

## Current Evidence and Reproduction

Current-source evidence is under `artifacts/c3d_warpage_20260717`:

- `final_golden.txt`: `C3D Warpage` golden `5/5`.
- `after_teach_warpage_final.png` and `after_review_warpage_final.png`: current
  `1280 x 760` Shell Teach and published-Review captures. Both passed the
  screenshot-quality gate on the first attempt.
- `final_ui_saved_warpage.recipe.json`, `final_ui_saved_runner.txt`, and
  `final_ui_saved_run.{json,html,csv}`: explicit Shell Preview -> Save -> Publish
  -> Review followed by a Runner replay of the UI-saved recipe, all `Pass`.

```powershell
dotnet build OpenVisionLab.ThreeDStudio.slnx -c Debug -p:Platform='Any CPU' --no-restore
dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --verify-c3d-warpage --report artifacts\c3d_warpage_20260717\final_golden.txt
dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --shell-workspace Inspect --shell-task Warpage --smoke-publish-result --smoke-save-recipe artifacts\c3d_warpage_20260717\final_ui_saved_warpage.recipe.json --shell-smoke-screenshot artifacts\c3d_warpage_20260717\after_review_warpage_final.png
dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --recipe artifacts\c3d_warpage_20260717\final_ui_saved_warpage.recipe.json --report artifacts\c3d_warpage_20260717\final_ui_saved_runner.txt --expect-status Pass --run-record artifacts\c3d_warpage_20260717\final_ui_saved_run.json
```

## Still Required for a Physical Claim

A physical Warpage, calibration, repeatability, Gauge R&R, or metrology claim
still requires all of the following:

1. A distinct acquired source or sensor/writer provenance for the exact input.
2. A calibrated physical unit and frame, including source-to-grid mapping.
3. A justified datum/reference definition, normal/direction, and acceptance
   policy.
4. Separate evidence for uncertainty, repeated aligned acquisitions, and any
   claimed measurement capability.

Use this checklist for a future physical-input supplement; do not infer missing
fields from the duplicate filename or the current local result.
