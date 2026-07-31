# OpenVisionLab 3D Viewer Command Bar Simplification

Date: 2026-07-30

Status: Complete for the bounded presentation-only command-bar slice

## User goal

Make the 3D Viewer easier to scan by reducing persistent labels and moving
familiar presentation commands to icons, using the supplied GoPxL Viewer as
the density reference.

## Scope and boundaries

Included:

- the Shell Viewer-layout command bar;
- the Viewer's geometry, HUD, projection, fit, overflow, and ROI display-height
  controls;
- visible selected states, tooltips, accessible names, and stable
  AutomationIds;
- current Release Wide and Compact evidence.

Excluded:

- Preview, Publish, Run, Apply, Delete, and stage-navigation wording;
- recipe, ROI geometry, measurement, threshold, and execution semantics;
- Viewer pan, zoom, drag, ROI overlay, comparison, docking, and window
  behavior;
- the human-owner R0 and SurfaceModel implementation.

## Result

The Shell layout bar now presents Height Image, Single, side-by-side, stacked,
and pop-out as a compact icon group. The current layout remains visible
through selected-surface and accent states. The auxiliary output selector
retains the selected output text only when an auxiliary slot exists.

The Viewer command bar now keeps only the selected geometry-style text.
HUD, Top, Perspective, Fit All, Fit ROI, and overflow are icon controls.
Advanced and less-frequent commands remain in the ellipsis menu.

The former full-width ROI display-height explanation is now a compact,
view-only inline control with decrease, numeric offset, increase, and reset.
Its full safety explanation remains in the tooltip and accessibility help
text. The Viewer status line remains visible so load and rendering feedback is
not hidden.

Every icon-only control has a tooltip, an accessible name, and a stable
AutomationId. The shell layout selection is exposed through four read-only
ViewModel state properties and changes presentation only.

## Before and after evidence

Before:

- `artifacts/current/20260730-viewer-command-bar-simplification/before/wide-teach-before.png`
- `artifacts/current/20260730-viewer-command-bar-simplification/before/compact-teach-before.png`

After:

- `artifacts/current/20260730-viewer-command-bar-simplification/after/wide-teach-after.png`
- `artifacts/current/20260730-viewer-command-bar-simplification/after/compact-teach-after.png`

Both after captures passed the screenshot quality gate on the first attempt.
The Compact comparison moves the dark model canvas upward by about 66 pixels;
the Wide comparison gains about 68 pixels. No toolbar control is clipped in
either layout.

## Verification

```text
dotnet build OpenVisionLab.ThreeDStudio.sln -c Release -p:Platform="Any CPU"
Build succeeded: 0 warnings, 0 errors

--verify-inspection-workspace-selection
Result: Pass (63/63 checks)

--verify-workbench-docking
Result: Pass (59/59 checks)

--verify-validation-set
84 PASS lines

scripts/verify-code-structure.ps1
Result: Pass (17/17 checks)

scripts/start-human-owner-r0.ps1 -Layout Wide -ValidateOnly
Validation passed. No application was launched.

scripts/start-human-owner-r0.ps1 -Layout Compact -ValidateOnly
Validation passed. No application was launched.
```

Verification reports are under:

- `artifacts/current/20260730-viewer-command-bar-simplification/verification/`

The Shell assembly used by this historical closure was:

```text
4BD0377AEF472A23EE0B830A7546ACD0E33ED90DB1B3ADCD83360D254D714BFD
```

That R0 candidate is superseded by
`OPENVISIONLAB_3D_VIEWER_SINGLE_ROW_AND_HEIGHT_COLOR_RANGE_20260730.md` and
must not be used for current owner acceptance.

## Durable completion record

Status: Complete

Scope: presentation-only simplification of the Shell Viewer-layout bar and
the Viewer's common display/ROI-height command bar.

Acceptance criteria:

- familiar common commands use compact icons -> Pass, current-source Wide and
  Compact captures;
- icon-only controls keep tooltip, accessible name, and AutomationId -> Pass,
  XAML source audit;
- selected layout and projection states remain visible -> Pass, current-source
  captures and `63/63` layout-state verification;
- view/layout changes do not dirty, reroute, Preview, Publish, or Run -> Pass,
  `63/63` inspection-workspace verification;
- important execution and error text remains explicit -> Pass, unchanged
  stage/action surfaces and retained Viewer status line;
- current Release and both target layouts pass -> Pass, build `0/0` and
  first-attempt screenshot-quality reports.

Verification: commands and results listed above.

Evidence:
`artifacts/current/20260730-viewer-command-bar-simplification/`.

Boundary / next dependency: inventory remains
`104 C / 17 P / 88 N / 9 E / 16 O`. `A-01` remains Partial until the human
owner completes unaided Wide and Compact R0 on the updated fixed hashes. After
that external gate passes, begin `J-01/J-03/J-04 SurfaceModel`.
