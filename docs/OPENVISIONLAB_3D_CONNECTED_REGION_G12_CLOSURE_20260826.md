# OpenVisionLab 3D G-12 Connected Region Output and Overlay Closure

Date: 2026-08-26

Status: Complete for the bounded software slice

## Scope

G-12 connects the already evaluated G-11 `C3DConnectedRegionOutput` to a
user-facing Workbench output review. The Workbench now exposes total region and
foreground counts, grid-index area, source identity, and per-region count,
area, center, orientation, bounds, and exact row/column cells. A stable region
selection is routed to the existing 3D Viewer and Height Image presentation
paths.

The presentation path consumes an existing typed evaluation. It does not run
G-11 again, infer a mask from heights, create a polygon or circle mask, mutate
the recipe, persist a new region artifact, pin a region into comparison, or
replace the source C3D. Reported area remains `grid-index²`; it is not
calibrated physical area or metrology evidence.

## Ownership and state flow

Before G-12, G-11 ended at the strict Studio adapter and Runner fixture. The
Workbench and Viewer had no typed connected-region consumer, selected-region
state, or shared region-cell overlay.

The current call path is:

```text
C3DConnectedRegionEvaluation
  -> ToolWorkbenchViewModel.SetConnectedRegionPreview
  -> Displayed Outputs / ConnectedRegionReviewItem
  -> WorkbenchViewerDisplayCoordinator
  -> OpenVisionThreeDViewerControl + HeightImageViewerViewModel
  -> HeightImageViewerView and 3D Viewer exact row/column overlays
```

The Workbench owns the accepted output and selected stable `RegionId`. The
Viewer and Height Image own presentation state only. Every accepted output is
checked against the current source entity, source content hash, grid, unit,
frame, mask identity, region count, cell bounds, geometry, and output hash.
Changing the source, source identity fields, or recipe clears the stale typed
output and selection. Selecting or displaying a region does not change recipe
steps, source, ROI, Preview, Publish, Run, or comparison pins.

## User workflow

1. Load the local C3D source through the existing Viewer → Workbench lifecycle.
2. Supply an already evaluated source-bound G-11 output to the Workbench.
3. Review the typed output in `Displayed Outputs`, select a region, and use
   `Show overlay` when the source-bound C3D is displayed in the Viewer.
4. Return to Teach and open the existing Height Image auxiliary view beside
   the 3D Viewer.
5. The same selected region identity and exact source-grid cells appear in
   both views. The selected region uses a white selected outline over the
   neutral amber detected-region styling.

## Changed ownership

- `ToolWorkbenchViewModel.ConnectedRegion.cs` owns the typed output projection,
  strict current-source validation, review items, selection, and clear policy.
- `ToolWorkbenchViewModel.ArtifactRegistry.cs` and
  `ToolWorkbenchViewModel.DisplayedOutputs.cs` expose the source-bound output
  through the existing artifact and display contracts.
- `DisplayedOutputsView.xaml` exposes the region summary, metrics, selection,
  and explicit overlay command with existing semantic theme resources.
- `WorkbenchViewerDisplayCoordinator.cs` and
  `WorkbenchViewerTeachingCoordinator.cs` route the same typed instance and
  selected identity to both viewers.
- `HeightImageViewerViewModel.cs` and `HeightImageViewerView.xaml.cs` draw
  exact source-grid cells in the existing Height Image canvas.
- `MainWindowViewModel.ConnectedRegion.cs`,
  `OpenVisionThreeDViewerControl.WorkbenchConnectedRegion.cs`, and the existing
  3D render path draw the same cells over the current C3D source.
- `ShellConnectedRegionOutputSmoke.cs` exercises the full EXE path, including
  the separate Displayed Outputs tab and the real Height Image auxiliary slot.

## Acceptance evidence

| Criterion | Result | Evidence |
| --- | --- | --- |
| C1. Typed current-source output and per-region metrics without re-execution | Pass | Workbench `9/9`; Release EXE output report records two regions, two foreground cells, source SHA-256, output SHA-256, and the same typed identity in both consumers. |
| C2. Stable selection and explicit display without recipe mutation | Pass | Workbench `9/9`; selection changes only `RegionId`, `Show overlay` uses the current source path, and recipe steps/dirty/run state remain unchanged. |
| C3. Exact 2D/3D selected overlay parity and source invalidation | Pass | Release EXE smoke passes at `1280x760` and `1920x1040`; Height Image reports `overlayChildren=4` for `foregroundCells=2`, and Viewer/Height Image report the same selected region. Source-change clear is covered by Workbench `9/9`. |
| C4. Focused, build, regression, runtime, and hygiene gates | Pass for this bounded slice | Release solution build `0/0`; Runner connected-region `10/10`; Workbench `9/9`; Shell option verifier `49/49`; Release tests `10 passed, 0 failed, 0 skipped`; `git diff --check` exit `0`; both runtime screenshots accepted with selected-monitor intersection. |

## Verification commands and evidence

All generated reports and screenshots are physically under
`D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\G-12\`.

- `dotnet build OpenVisionLab.ThreeDStudio.slnx -c Release --no-restore -v:minimal`
  — `0` warnings, `0` errors.
- `dotnet test OpenVisionLab.ThreeDStudio.slnx -c Release --no-build
  --results-directory D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\G-12\test-results`
  — `10` passed, `0` failed, `0` skipped.
- `OpenVisionLab.ThreeD.Runner.exe --verify-c3d-connected-region --report ...`
  — `10/10 PASS`; report:
  `D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\G-12\c3d-connected-region-release.txt`.
- `OpenVisionLab.ThreeD.Shell.exe --verify-connected-region-workbench ...`
  — `9/9 PASS`; report:
  `D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\G-12\connected-region-workbench-release.txt`.
- `OpenVisionLab.ThreeD.Shell.exe --verify-shell-smoke-command-line ...`
  — `49/49 PASS`; report:
  `D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\G-12\shell-smoke-options-release.txt`.
- Release actual EXE smoke on the dynamically selected leftmost monitor with
  software rendering:
  - Compact `1280x760`: report
    `connected-region-output-release-1280x760.txt`, screenshot
    `connected-region-output-release-1280x760.png`, and accepted quality
    report `connected-region-output-release-1280x760-quality.txt`.
  - Wide `1920x1040`: report
    `connected-region-output-release-1920x1040.txt`, screenshot
    `connected-region-output-release-1920x1040.png`, and accepted quality
    report `connected-region-output-release-1920x1040-quality.txt`.
- `git diff --check` — exit `0`; Git reported only existing line-ending
  normalization warnings, with no whitespace errors.

The current runtime evidence used the dynamically selected leftmost `DISPLAY2`
path at `125%` DPI. The screenshots show the 3D source and Height Image side by
side, with the detected-region overlay visible. Other DPI scales, alternate
themes, held pointer-down coverage, owner R0, hosted CI, representative
maximum-C3D qualification, physical calibration, and release qualification
remain unverified or separately gated.

## Completion record

```text
Status: Complete
Scope: G-11 typed connected-region evaluations are presented as Workbench evidence and synchronized by stable region identity and exact source-grid cells to the existing Height Image and 3D Viewer overlay paths.
Acceptance criteria: C1 typed source-bound output and metrics -> pass; C2 stable selection/Show overlay with no recipe mutation -> pass; C3 exact 2D/3D selected overlay and source-change clear -> pass; C4 focused/build/regression/runtime/hygiene gates -> pass for this bounded slice, with unrun DPI/theme gates explicitly listed.
Verification: Release solution build 0/0; Release tests 10 passed/0 failed/0 skipped; Runner connected-region 10/10; Workbench 9/9; Shell options 49/49; actual Release EXE smoke pass at 1280x760 and 1920x1040; accepted screenshots and monitor-intersection reports; git diff --check exit 0.
Evidence: this document; .proofline/issues/PL-0052.json; D:/OpenVisionLab-TestData/OpenVisionLab-3D-Studio/G-12/.
Boundary / next dependency: no mask authoring or threshold inference, polygon/circle rasterization, recipe persistence, downstream Presence/Fill/Completeness consumer, calibrated physical area, maximum-C3D qualification, owner R0, alternate DPI/theme qualification, commit, push, version, package, tag, release, deployment, or PC restart is implied.
```
