# Surface ROI display-height control - 2026-07-25

## Outcome

The selected C3D `GridRectangle` is no longer drawn on the whole-source mean
plane. Viewer presentation now places it on a local surface estimate and gives
the operator four view-only ways to adjust its visible Y position:

- drag the yellow `Y ↕` handle;
- enter a raw-height offset in the Viewer ribbon;
- use the `-` and `+` buttons;
- hold `Alt` while using the mouse wheel.

`To surface / 표면으로` resets the offset to zero. The ribbon shows automatic
surface height, offset, and resulting display height together. The Step
Parameters row-count label is now `Z length (rows) / Z 길이 (행)` so it is not
mistaken for an editable Y height.

## Display-height contract

The automatic display height is the median of finite loaded C3D render samples
inside the ROI. If the loaded sample set contains no point inside a small ROI,
the nearest finite loaded sample to the ROI center is used. This is a stable,
bounded Viewer estimate, not a metrology result.

The operator offset is held per selection identity in Viewer state. It is
intentionally:

- not written to `ToolRecipeSelection`;
- not used by Thickness, Plane Flatness, or another measurement adapter;
- not an input to Preview, Publish, Run, or Validation Set;
- cleared when the C3D source changes;
- not persisted when the application closes.

The authored contract remains `X = column`, `Z = row`. Thickness still fits
the Reference ROI samples and evaluates Measurement ROI samples using their
actual C3D heights. A true XYZ volume remains a separate future
`OrientedBox3D` type.

## Interaction and diagnostics

The height handle is a screen-space control so it stays usable in the default
near-top Surface view, where the world Y axis is strongly foreshortened. Moving
or resizing the X/Z footprint still requires an active replacement capture and
explicit Apply. The view-only height handle can be used on the selected applied
ROI or its active replacement candidate without Apply.

Each completed handle, numeric, button, reset, or `Alt+wheel` action writes a
structured `OpenVisionLab.Logging` entry containing selection ID, automatic
height, offset, effective height, and:

```text
viewOnly=true | recipeChanged=false | inspectionRun=false
```

## Verification

Current Release evidence:

- solution build: pass, `0` warnings / `0` errors;
- teaching-capture ViewModel: pass, `24/24`;
- generic height measurement Workbench: pass, `42/42`;
- Tool Recipe teaching: pass, `27/27`;
- docking workspace: pass, `28/28`;
- logging infrastructure: pass, `4/4`;
- actual Windows pointer: move, corner resize, and Y-handle hover/drag pass;
- pointer boundary: authored ROI, Preview/result references, and camera remain
  unchanged;
- actual pointer display state: automatic `147.288`, offset `588.531`,
  effective `735.819`;
- current wide and `1280 x 760` Release screenshots: quality accepted on
  attempt 1.

Evidence:

- `artifacts/current/20260725-roi-display-height/before-no-roi-display-height.png`
- `artifacts/current/20260725-roi-display-height/after-roi-display-height-surface.png`
- `artifacts/current/20260725-roi-display-height/after-roi-display-height-1280x760.png`
- `artifacts/current/20260725-roi-display-height/roi-height-pointer-state.drag-pointer.txt`
- `artifacts/current/20260725-roi-display-height/teaching-capture-viewmodel-final.txt`
- `artifacts/current/20260725-roi-display-height/height-measurement-workbench-final.txt`
- `artifacts/current/20260725-roi-display-height/recipe-teaching-final.txt`
- `artifacts/current/20260725-roi-display-height/docking-final.txt`
- `artifacts/current/20260725-roi-display-height/logging-final.txt`
- `src/OpenVisionLab.ThreeD.Shell/bin/Release/net10.0-windows10.0.19041/Log/OpenVisionLab-ALL.log`

## Completion record

Status: Complete

Scope: local-surface ROI display placement, transient per-selection display
offset, Y-handle/numeric/button/Alt-wheel/reset interaction, row-count naming
correction, structured diagnostics, and current Release evidence.

Acceptance criteria:

- automatic local surface placement -> pass, Viewer reports the local median;
- convenient height adjustment -> pass, pointer, numeric, buttons, reset, and
  `Alt+wheel` are exposed;
- footprint and measurement semantics unchanged -> pass, actual pointer and
  `42/42` height-measurement verification;
- recipe and execution remain unchanged -> pass, actual pointer report;
- current UI evidence -> pass, wide and compact quality accepted.

Verification: Release build plus the focused verification and actual-pointer
commands listed above.

Evidence: `artifacts/current/20260725-roi-display-height/`.

Boundary / next dependency: this does not add physical calibration, persisted
display annotations, an XYZ volume, or metrology proof. The next product
evidence gate is the owner's unaided first-recipe replay on this Release EXE.
