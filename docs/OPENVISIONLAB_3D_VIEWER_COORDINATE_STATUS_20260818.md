# OpenVisionLab 3D Viewer Coordinate Status

Date: 2026-08-18
Status: Complete
Issue: `PL-0021`

## Operator problem

After selecting geometry in the Viewer, the operator had to look away from
the persistent bottom status area to find the selected point. This separated
the camera context from the coordinate needed for teaching and review.

## Product decision

The Viewer bottom status now keeps two related facts together:

1. model unit, view mode, and camera state;
2. the currently selected `X / Y / Z` coordinate.

The coordinate row is right-aligned, uses the existing graphite/cyan semantic
status treatment, and exposes a stable automation ID and accessible name. A
raw-height C3D pick retains its existing `raw` value after `X / Y / Z`; an
empty selection is explicit as `(없음)` or `(none)`.

This is presentation only. It binds to the existing
`MainWindowViewModel.PickCoordinate` value used by cube, C3D, point-cloud, and
mesh picking. It introduces no mouse-hover scan, new coordinate calculation,
selection event, Preview, Publish, Run, or recipe mutation.

## Acceptance checklist

- [x] Empty state visibly identifies the `X / Y / Z` fields.
- [x] A selected C3D point shows `X`, `Y`, `Z`, and its existing raw height.
- [x] Existing camera and unit status remains fully visible.
- [x] Wide `1920 x 1040` and Compact `1280 x 760` remain bounded and legible.
- [x] Korean and English labels are runtime-localized.
- [x] Pointer pick, orbit, pan, zoom, double-click Fit, and context menus retain
  their existing behavior.
- [x] Release build, docking/theme, command-line, structure, and R0 package
  validation pass.

## Current-build evidence

Physical evidence root:

`D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260818-pl0021-viewer-coordinate-status\`

Before captures show that the same C3D pick did not appear in the bottom
status:

- `before\before-wide-picked-coordinate.png`;
- `before\before-compact-picked-coordinate.png`.

After captures show the current Release implementation:

- `after\after-wide-picked-coordinate.png`;
- `after\after-compact-picked-coordinate.png`;
- `after\after-compact-english-picked-coordinate.png`;
- `after\after-compact-empty-coordinate.png`.

All six screenshot-quality reports were accepted on attempt 1. The after
captures intersect the dynamically selected leftmost monitor reported as
`[-2400,456,2400,1350]`. Compact initially exposed a missing empty-state row;
the final build separates label and value elements so `(없음)` is visible on
first entry as well as after a pick.

Focused evidence under `verification\`:

- Viewer display/runtime localization: `103/103`;
- Workbench docking/theme: `87/87`;
- Shell smoke command line: `40/40`;
- actual pointer input regression: Pass, including pick coordinate, orbit,
  pan, zoom, double-click Fit, and context-menu routes;
- screenshot quality and selected-monitor intersection: Pass;
- code structure and numerical-ownership guard: `29/29`;
- full Release solution build: `0` warnings, `0` errors;
- refreshed Wide and Compact human-owner R0 `-ValidateOnly`: Pass.

## Completion record

```text
Status: Complete
Scope: Persistent Viewer bottom-status display of the existing selected X/Y/Z coordinate, raw C3D height when available, localized empty state, accessibility metadata, and Wide/Compact layout
Acceptance criteria: visible selected and empty states -> pass; existing PickCoordinate ownership reused -> pass; camera/unit status retained without clipping -> pass; no inspection side effect -> pass; current-build UI and regression verification -> pass
Verification: Viewer display/runtime 103/103; docking/theme 87/87; Shell options 40/40; actual pointer regression pass; structure 29/29; Release 0 warnings/0 errors; Wide/Compact screenshot quality and monitor intersection pass; R0 Wide/Compact ValidateOnly pass
Evidence: this document; .proofline/issues/PL-0021.json; D:/OpenVisionLab-TestData/OpenVisionLab-3D-Studio/artifacts/current/20260818-pl0021-viewer-coordinate-status/
Boundary / next dependency: displayed values are source/viewer coordinates and raw software height, not calibrated physical metrology; product-owner unaided Wide/Compact R0 remains external; L-12 remains the next dependency-ready software priority
```
