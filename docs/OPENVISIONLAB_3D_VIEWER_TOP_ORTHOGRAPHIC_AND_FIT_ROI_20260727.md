# Viewer Top Orthographic and Fit ROI

Date: 2026-07-27

Status: Complete

## Outcome

Inspection Workspace v3 implementation slice 3 is complete. The Viewer now
exposes the ROI-placement camera actions as first-class controls:

- `Top`: true X/Z orthographic projection;
- `Perspective`: restores the perspective camera that was active before Top;
- `Fit all`: fits the full C3D in the current projection;
- `Fit ROI`: fits the selected Reference or Measurement GridRectangle in the
  current projection.

This is a visible workflow change over the prior near-top perspective-only
Viewer. It is not the end of the Workspace v3 redesign. Three of the eight
bounded implementation slices are now complete (`37.5%` by slice count).

## Interaction contract

The normal ROI placement path is now:

```text
select Reference or Measurement ROI
  -> Top
  -> Fit ROI
  -> draw/move/resize with the existing fixed screen-space handles
  -> Apply or Cancel explicitly
  -> Perspective when depth review is needed
```

Top is not another `pitch 80` camera preset. It uses an orthographic OpenGL
projection with `X=column` and `Z=row`. Perspective remains the default
projection when a C3D source is loaded, while C3D surface topology continues
to default to `Surface`.

Empty-space left orbit from Top deliberately exits to a near-top Perspective
camera before rotating. Middle/right drag pans in either projection. Wheel
zoom changes camera distance in Perspective and orthographic view height in
Top. Double-click fits the full source without silently changing the current
projection.

The toolbar uses short text commands rather than new decorative icons because
the Viewer does not own a matching icon family and the projection distinction
must remain unambiguous. Every new action has a tooltip, accessible name, and
stable automation ID.

## Geometry and ownership

`ViewerProjectionMode` and the presentation camera state belong to the Viewer
ViewModel. The OpenGL View adapter owns projection and `LookAt` application.
`CameraMath` owns:

- stable top-camera up-vector selection;
- perspective and orthographic screen projection;
- perspective and orthographic pick rays;
- orthographic bounds fitting;
- projection-aware pan scaling.

All screen-to-grid and grid-to-screen ROI paths now use the same active
projection. This keeps the selected overlay, four corner handles, center move,
Y-position handle, and empty-cell footprint intersection synchronized in Top.

`Fit ROI` reads the existing transient/applied selected GridRectangle and
computes a view-only fit from its four displayed corners. It does not create,
replace, resize, or save an ROI.

## Preserved boundaries

- Top, Perspective, Fit all, and Fit ROI are view-only.
- No view command invokes Preview, Publish, Run, Validation Set, or save.
- Recipe JSON, selection identity, ROI size, ROI Y-position offset, and
  measurement values remain unchanged.
- `GridRectangle` remains an X/Z footprint rather than an XYZ volume.
- Surface remains the default C3D geometry style.
- Profile remains available through the View menu; making it a compact
  first-class workflow control belongs to a later composition slice.

## Current evidence

The exact current input and recipe:

- recipe:
  `3D/SyntheticValidation/ThicknessCouponV1/inspection-recipe.ov3d-recipe.json`;
- recipe SHA-256:
  `D0DEDF827985BFCC8EF5AC37777E22442222ECE6DE6101F02E475123D2C191A1`;
- C3D:
  `3D/SyntheticValidation/ThicknessCouponV1/synthetic-thickness-coupon-v1.C3D`;
- C3D SHA-256:
  `5D3625B1A5A65EF8BEAB366FF7A007918D28FB614136414BBD30A441E85C8937`;
- selected step: `step.tab-thickness.01`;
- selected ROI: Tab 1 Reference ROI;
- language: Korean.

The true pre-edit compact capture did not finish because the first GUI
capture command returned before the process completed. The immediately
preceding Release current-source captures from slice 2 are therefore copied
and explicitly named as closest reproducible baselines:

- wide baseline:
  `artifacts/current/20260727-viewer-top-fit-roi/before-wide-baseline.png`;
- compact baseline:
  `artifacts/current/20260727-viewer-top-fit-roi/before-compact-baseline.png`.

Current-source after evidence:

- wide Top + Fit ROI:
  `artifacts/current/20260727-viewer-top-fit-roi/after-wide.png`
  (`1920 x 1040`);
- compact Top + Fit ROI:
  `artifacts/current/20260727-viewer-top-fit-roi/after-compact.png`
  (`1280 x 760`).

Both after captures passed screenshot quality on attempt 1. The comparison
shows the new one-click controls at both widths, an active Top state, explicit
`Top orthographic` status, the selected ROI centered and fitted, and the
existing edit handles retained.

## Verification

Commands were run from `C:\Git\OpenVisionLab-3D-Studio` against the final
current Release build:

```powershell
dotnet build "OpenVisionLab.ThreeDStudio.sln" -c Release -p:Platform="Any CPU"

OpenVisionLab.ThreeD.Shell.exe --verify-display-viewmodel <report>
OpenVisionLab.ThreeD.Shell.exe --verify-workbench-docking <report>
OpenVisionLab.ThreeD.Shell.exe --verify-tool-recipe-teaching <report>
OpenVisionLab.ThreeD.Shell.exe --verify-tool-height-measurement-workbench <report>
OpenVisionLab.ThreeD.Shell.exe --verify-recipe-manager-wpg <report>
OpenVisionLab.ThreeD.Shell.exe --verify-teaching-capture-viewmodel <report>
OpenVisionLab.ThreeD.Shell.exe --verify-validation-set <report>
OpenVisionLab.ThreeD.Shell.exe --verify-logging <report>
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify-code-structure.ps1

OpenVisionLab.ThreeD.Runner.exe --tool-recipe <eight-tab-recipe> `
  --source <exact-c3d> --report <report> --expect-status Pass
```

The actual Windows pointer regression used the current Viewer with Wireframe
only for the established interaction-LOD gate. It passed pick, orbit,
middle/right pan, zoom, double-click fit, short-right-click menu, all eight
View/context commands, render coalescing, staged LOD, zero interaction GPU
uploads, and no source reload.

Results:

- Release build: `0` warnings, `0` errors;
- display/projection ViewModel and camera math: `103/103`;
- Workbench docking/composition: `31/31`;
- recipe teaching: `28/28`;
- height measurement: `44/44`;
- Recipe Manager/WPG: `37/37`;
- teaching capture: `24/24`;
- Validation Set: `25/25`;
- logging: `4/4`;
- structure: `15/15`;
- exact-source ordered Runner: `8/8`;
- actual pointer regression: pass;
- wide and compact screenshot quality: accepted on attempt 1.

All reports and captures are under:

- `artifacts/current/20260727-viewer-top-fit-roi/`

## Remaining Workspace v3 priorities

The compact dual-role ROI Review lifecycle is complete in the newer
`OPENVISIONLAB_3D_ROI_REVIEW_LIFECYCLE_20260727.md` checkpoint.

1. Add selected-output Show/Pin/Compare commands in Selected Tool.
   | Recommended model: `gpt-5.6-sol` | Reasoning effort: `medium`
2. Add Viewer split/pop-out composition.
   | Recommended model: `gpt-5.6-sol` | Reasoning effort: `high`
3. Implement bounded Thickness `4 x 2` repeat authoring and exact-source
   replay. | Recommended model: `gpt-5.6-sol` | Reasoning effort: `high`
4. Run the owner's unaided end-to-end acceptance replay.
   Prerequisite: owner availability and physical datum/calibration/tolerance
   decisions. Do not spend model tokens claiming metrology readiness until
   those inputs exist.

## Completion record

Status: Complete

Scope: Workspace v3 slice 3 true Top orthographic projection, perspective
restore, projection-preserving Fit all, selected Reference/Measurement Fit
ROI, projection-aware ROI projection/picking/pan/zoom, first-class toolbar and
menu actions, actual pointer regression, and current wide/compact evidence.

Acceptance criteria: Top and Perspective are distinct one-click modes -> pass;
Top is true orthographic rather than pitch-only -> pass; Fit ROI uses the
selected ROI -> pass; overlay and handles remain synchronized -> pass; view
commands do not mutate or execute the recipe -> pass; wide/compact commands
remain visible -> pass.

Verification: Release build `0/0`; display/projection `103/103`; docking
`31/31`; teaching `28/28`; height measurement `44/44`; Recipe Manager/WPG
`37/37`; capture `24/24`; Validation Set `25/25`; logging `4/4`; structure
`15/15`; exact-source Runner `8/8`; actual pointer regression pass; current
after screenshot quality accepted on attempt 1.

Evidence: `artifacts/current/20260727-viewer-top-fit-roi/`.

Boundary / next dependency: the full UI/UX redirection is not complete. The
ROI Review-state slice is complete in the newer checkpoint. Selected-output
actions, Viewer slots, `4 x 2` repeat, and owner unaided replay remain.
Physical datum, calibration, traceable units, uncertainty, and production
tolerances remain unverified.
