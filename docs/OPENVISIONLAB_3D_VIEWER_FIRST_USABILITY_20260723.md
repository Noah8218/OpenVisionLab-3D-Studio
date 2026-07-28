# Viewer-first usability checkpoint — 2026-07-23

## Status

`Complete` for the bounded P0-A UI slice described below.

OpenVisionLab 3D Studio remains a local, rule-based 3D inspection recipe
workbench. This change improves the operator's first path into that workflow;
it does not turn the product into a viewer-only application and does not add
camera, PLC, robot, cloud, or production-line integration.

## Completed scope

- The 3D View command bar exposes a persistent localized `3D 맵 열기` /
  `Open 3D Map` action backed by the existing source-load command.
- `Ctrl+Shift+O` invokes the same command.
- A newly loaded C3D height grid resets to the product-default `Wireframe`
  geometry style. Derived output display does not reset the operator's current
  style.
- The empty Pipeline/Validation area is collapsed at startup and no longer
  expands merely because a recipe step exists. Explicit Preview, Run, Publish,
  or evidence commands still open the evidence area.
- At widths below 1500 pixels, Tool Library and Recipe Flow become two tabs in
  one dock pane. At wider widths they remain separate dock panes. The Viewer
  and Step Parameters therefore retain useful working width at 1280 x 720.
- Long step input/output identities and inspector contract text wrap or expose
  a tooltip instead of silently hiding their meaning.
- Shell screenshot sizing now honors explicit smoke-test dimensions while
  normal product startup continues to use the Windows work-area-aware maximized
  behavior.

## Acceptance evidence

| Criterion | Result | Evidence |
|---|---|---|
| Current solution builds | Pass, 0 warnings / 0 errors | `artifacts/current/20260723-viewer-first-usability/responsive-build.txt` |
| Empty bottom pane stays collapsed and dock actions still work | Pass, 26/26 | `artifacts/current/20260723-viewer-first-usability/responsive-workbench-docking.txt` |
| C3D default geometry resets to Wireframe | Pass, 83 checks | `artifacts/current/20260723-viewer-first-usability/viewer-display-settings.txt` |
| Recipe storage/Preview/Run teaching contracts remain intact | Pass, 23/23 | `artifacts/current/20260723-viewer-first-usability/tool-recipe-teaching.txt` |
| Recipe Center, WPG, and localization contracts remain intact | Pass, 27/27 | `artifacts/current/20260723-viewer-first-usability/recipe-manager-wpg.txt` |
| 1920 x 1040 Korean current-source UI | Accepted on attempt 1 | `artifacts/current/20260723-viewer-first-usability/after-responsive-1920x1040-ko.png` |
| 1920 x 1040 English current-source UI | Accepted on attempt 1 | `artifacts/current/20260723-viewer-first-usability/after-responsive-1920x1040-en.png` |
| 1280 x 720 Korean compact current-source UI | Accepted on attempt 1 | `artifacts/current/20260723-viewer-first-usability/after-responsive-1280x720-ko.png` |

The valid 1920 pre-change capture is
`artifacts/current/20260723-viewer-first-usability/before-1920x1040-ko.png`.
The file named `before-1280x720-ko.png` is not valid 1280 evidence: the old
smoke path inherited maximized startup and produced another 1920 x 1040 image.
It is retained only as historical evidence and must not be cited as a genuine
1280 baseline.

## Boundary and next gate

This checkpoint does not prove different-source C3D load latency, cancellation,
UI-thread responsiveness during decode, pointer input latency, or rendering
throughput while dragging. Those are separate P0-B and P0-C performance gates.
Preview, Publish, and Run remain explicit operator actions.
