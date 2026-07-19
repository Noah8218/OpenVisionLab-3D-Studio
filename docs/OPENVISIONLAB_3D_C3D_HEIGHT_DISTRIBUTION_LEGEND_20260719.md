# OpenVisionLab 3D C3D Height Distribution Legend

Updated: 2026-07-19

Status: Complete for the current local C3D Viewer display contract.

## Purpose

The Viewer needs to answer two different questions without opening another analysis window:

1. Which raw-height value does each displayed color represent?
2. Which raw-height interval contains most of the currently loaded C3D source?

A color scale answers only the first question. The implemented right-side overlay therefore combines a vertical color scale with a full-source 32-bin histogram and an explicit peak interval.

## Implemented Contract

- `C3DHeightGrid.Load` calculates the distribution from every finite, non-zero source cell while the complete source buffer is already available.
- The histogram does not use render-stride `Points`, so Fast, Balanced, and Detailed rendering cannot change its values.
- Zero and non-finite C3D cells are missing values and do not enter the histogram.
- The fixed automatic range is the loaded source minimum to maximum. Mean, valid count, missing count, and the most-populated bin are shown.
- Every non-constant source uses that exact minimum-to-maximum span for both rendering and the legend; only a constant source uses the zero-span fallback. The focused fixture also covers a non-constant span smaller than `0.0001`.
- Raw-height labels retain adaptive precision for small spans, and the mean label is placed at its normalized height instead of at a fixed visual midpoint.
- Height, Grayscale, and Thermal use gradient stops generated from the same palette functions as rendering.
- Solid and Deviation hide the source-height overlay. Deviation remains owned by the existing result legend; non-C3D sources do not show stale C3D values.
- The overlay is inside the Viewer, anchored at the top-right, hit-test-free, and independent of Shell `SidePanelsVisible`, so it works in the docked Workbench and an external DLL host.
- The state is display-only. Loading or changing the legend does not run Preview, Run, or Publish and does not alter recipe/result evidence.

For the fixed local C3D bytes used by the current Workbench smoke, the contract records:

```text
valid=1,653,562
missing=905,505
minimum=-525.416015625 raw-height
mean=1462.92620252028 raw-height
maximum=5359.89599609375 raw-height
peak=1681.5759887695312..1865.4919891357422 raw-height
peakFraction=0.49062206315820028
```

These are full-source raw scalar values. The range includes outliers and is not an ROI distribution, calibrated distance, or physical height claim.

## Verification Evidence

| Check | Result | Evidence |
| --- | --- | --- |
| Full solution build | Pass, 0 warnings / 0 errors | `dotnet build "OpenVisionLab.ThreeDStudio.sln" -c Debug -p:Platform="Any CPU"` |
| Distribution/model/display-only contract | Pass `20/20` | `artifacts/ui/20260719-c3d-height-distribution/height-distribution-verification.txt` |
| Existing display settings | Pass `82` checks | `artifacts/ui/20260719-c3d-height-distribution/display-viewmodel-regression.txt` |
| C3D profile regression | Pass `10/10`; Profile ViewModel `8` | `height-profile-regression.txt`, `profile-viewmodel-regression.txt` in the same artifact folder |
| Docking regression | Pass `15/15` | `artifacts/ui/20260719-c3d-height-distribution/docking-regression.txt` |
| Shell inactive teaching boundary | Pass; Preview `NotRun`, results `0`, authored state unchanged | `after-report.txt`, `after-contracts.txt` |
| Height Workbench UI | Pass screenshot quality on attempt 1; overlay visible and unclipped | `before.png`, `after.png`, `after-quality.txt` |
| Grayscale / Thermal | Pass current Viewer captures and contracts; distinct image hashes | `viewer-grayscale.*`, `viewer-thermal.*` |
| Pointer/Profile coexistence | Pass Profile `6/6`, right-drag/context menus, and accepted current UI | `after-profile-pointer.txt`, `after-profile-viewer-pointer.txt`, `after-profile.png` |
| DLL-only external host | Pass manifest `14/14`, outputs `12/12`, Host API `3/3`, exit `0` | `artifacts/ui/20260719-c3d-height-distribution/binary-host` |

The CI color-map gate now also requires the visible distribution contract, display verification `82`, and the focused distribution verification `18`. No remote Actions run is claimed for this local working tree.

An initial profile-only auxiliary smoke failed endpoint-drag from the untouched default camera before the feature source edits; `before-profile-pointer.txt` preserves that baseline. During final recapture, the first identical combined run also missed an OS-injected right-drag/context action and one Profile endpoint handle, then the immediate no-code-change rerun passed the Viewer-pointer and Profile reports. The accepted final reports prove the hit-test-free overlay does not own pointer input, while the one retry remains a recorded focus-sensitive smoke-harness risk rather than a claim of perfectly deterministic OS input injection.

```text
Status: Complete
Scope: Full-source C3D raw-height color scale, distribution histogram, peak interval, palette switching, and embedded Viewer presentation.
Acceptance criteria: density-independent full-source bins -> pass; Height/Grayscale/Thermal palette parity -> pass; Solid/Deviation/non-C3D hiding -> pass; no clipping or pointer interception -> pass; Preview/Run/Publish boundary -> pass.
Verification: solution build 0 warnings/0 errors; focused distribution 20/20; display 82; docking 15/15; C3D profile 10/10; Profile ViewModel 8; established Profile pointer 6/6; current Shell/Viewer screenshot quality accepted; BinaryHost 14/14, 12/12, 3/3.
Evidence: artifacts/ui/20260719-c3d-height-distribution and this document.
Boundary / next dependency: Values remain global full-source raw-height in the uncalibrated Viewer display contract; manual range/ROI distribution, physical scale, uncertainty, and metrology remain out of scope.
```
