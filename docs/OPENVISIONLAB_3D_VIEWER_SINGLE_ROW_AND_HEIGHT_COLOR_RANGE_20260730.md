# OpenVisionLab 3D Viewer Single Row and Height Color Range

Date: 2026-07-30

Status: Complete

## User goal

Reduce the loaded Teach Viewer from several stacked command/header rows to one
compact common row, remove the persistent text HUD from the left side of the
3D canvas, and let the operator narrow the raw-height interval used by the
Height color map.

## Scope and boundary

Included:

- collapse the loaded-source command row after the source is ready;
- remove the redundant single-pane `Main` title row;
- overlay the Shell layout controls into the Viewer's common top row;
- remove the persistent left measurement HUD while retaining the lower-left
  orientation gizmo;
- move the less-frequent ROI display-height commands into overflow;
- add visible Height color minimum, maximum, increment, decrement, and `AUTO`
  controls to the existing right-side legend;
- clamp values outside the chosen interval to its endpoint colors and linearly
  remap values inside the interval;
- expose localized tooltips, accessible names, help text, and stable
  AutomationIds for the new controls.

Excluded:

- changes to source values, ROI geometry, measurements, recipe parameters,
  thresholds, or output routing;
- automatic Preview, Publish, Run, save, or validation;
- replacing the existing Height palette;
- changing Deviation, Solid, or imported-color semantics;
- the external human-owner R0 and SurfaceModel work.

The source command surface remains available while a source is loading or a
source-selection action is active. Multi-pane Viewer layouts retain their
pane title because it identifies the active surface.

## Result

The normal loaded Single Viewer now uses one shared top command row:

```text
geometry | Height Image + layout | projection + fit + overflow
```

The former source-ready row, `A / Main` pane title, Viewer status text row, and
left measurement HUD no longer consume the model canvas. Load or selection
states can still restore the source surface when it is needed.

The right legend now owns a display-only interval:

```text
H [-] [maximum] [+]
  height palette and source histogram
L [-] [minimum] [+]
AUTO
```

`AUTO` uses the exact finite source minimum and maximum. A manual interval
changes only the rendering normalization. Values below the selected minimum
use the palette's low endpoint, values above the maximum use its high
endpoint, and values within the interval are normalized linearly. The
histogram continues to represent the full source distribution so narrowing
the display range does not misrepresent source evidence.

## Current UI evidence

Before:

- `artifacts/current/20260730-viewer-single-row-height-range/before/wide-teach-before.png`
- `artifacts/current/20260730-viewer-single-row-height-range/before/compact-teach-before.png`

After, automatic full-source range:

- `artifacts/current/20260730-viewer-single-row-height-range/after/wide-teach-after.png`
- `artifacts/current/20260730-viewer-single-row-height-range/after/compact-teach-after.png`

After, manual `11.00..12.50` raw-height range:

- `artifacts/current/20260730-viewer-single-row-height-range/after/wide-manual-height-range.png`

All three after captures passed the application-only screenshot quality gate
on the first attempt. The manual-range capture visibly changes the surface
color distribution while retaining the same source, ROI, camera, and model
geometry.

## Verification

```text
dotnet build OpenVisionLab.ThreeDStudio.sln -c Release
Build succeeded: 0 warnings, 0 errors

--verify-c3d-height-distribution
Result: Pass (25/25)

--verify-inspection-workspace-selection
Result: Pass (63/63)

--verify-workbench-docking
Result: Pass (59/59)

--verify-validation-set
Result: Pass (84/84)

scripts/verify-code-structure.ps1
Result: Pass (17/17)

scripts/start-human-owner-r0.ps1 -Layout Wide -ValidateOnly
Validation passed. No application was launched.

scripts/start-human-owner-r0.ps1 -Layout Compact -ValidateOnly
Validation passed. No application was launched.
```

The height-distribution verifier covers full-source initialization, manual
endpoint clamping, linear midpoint normalization, one-twentieth-span endpoint
steps, exact `AUTO` restoration, source clearing, and the absence of implicit
Preview or Publish.

Verification reports are under:

- `artifacts/current/20260730-viewer-single-row-height-range/verification/`.

The fixed R0 Shell assembly SHA-256 is:

```text
6E7B2F7B300E3A3BEBAB56A2F1DC21D971890BD28B7106D052D97EDCFAD65764
```

## Durable completion record

Status: Complete

Scope: single-row loaded Viewer command surface, removal of the persistent
left HUD, and display-only adjustable Height color normalization.

Acceptance criteria:

- loaded Single Viewer no longer spends three stacked rows on common Viewer
  commands -> Pass, current Wide and Compact captures;
- left persistent measurement text is removed while orientation remains ->
  Pass, current captures;
- right legend retains source context and adds editable low/high bounds ->
  Pass, automatic and manual-range captures;
- selected interval controls the rendered Height colors -> Pass, `25/25`
  normalization checks and manual-range capture;
- controls are discoverable and accessible -> Pass, tooltips, localized
  accessible names, help text, and stable AutomationIds in current XAML;
- Viewer changes remain presentation-only -> Pass, explicit verifier checks
  and unchanged Preview/Publish/Run surfaces;
- current Release and target layouts pass -> Pass, build `0/0`, regression
  verifiers, and first-attempt screenshot-quality reports.

Verification: commands and results listed above.

Evidence:
`artifacts/current/20260730-viewer-single-row-height-range/`.

Boundary / next dependency: inventory remains
`104 C / 17 P / 88 N / 9 E / 16 O`. `A-01` remains Partial until the human
owner completes unaided Wide and Compact R0 on the updated fixed hashes. After
that external gate passes, begin `J-01/J-03/J-04 SurfaceModel`.
