# OpenVisionLab 3D Workbench Theme and 1920 Baseline

Updated: 2026-07-19
Status: Complete - P4 Workbench baseline and P5 teaching-window title-view closure

## Purpose

This document fixes the first visual-system baseline for the docked Tool Workbench. It responds to the requirement that the application must read as one authored product rather than a mix of unrelated white controls, dark dock backgrounds, and default selection colors.

The target is a normal Windows desktop at `1920 x 1080`. It is a design baseline, not a claim that every monitor, DPI setting, popup, or expert workspace has received separate visual approval.

## Product visual contract

- Use OpenVisionLab's own light work surfaces, navy application chrome, and teal workflow state. Do not reproduce a commercial product's colors, pane coordinates, assets, or terminology.
- Keep the Viewer visually dominant. The standard Workbench at 1920 pixels uses left/viewer/right star ratios `0.82 : 2.45 : 1.05` and top/bottom ratios `2.35 : 1`.
- The default main window starts at `1920 x 1080`; its safe lower bound is `1180 x 720` so docked content remains usable below the reference size.
- Use a navy title and dock-header band, cool white panel surfaces, a restrained blue-gray divider, teal for the active/selected workflow state, and dark text on all light information surfaces.
- A panel must set its own readable foreground where it sits inside the dark docking host. A dark host foreground must not leak into an information card.
- Keep text labels, tooltips, and automation names when an icon is present. Icons assist recognition; they are never the only command cue.

## Implemented scope

1. Replaced the fragmented generic palette with shared Shell theme tokens for application chrome, panels, command bars, controls, dividers, text, viewport, selection, warning, and information surfaces.
2. Made the title bar and dock headers one navy system, while Toolbox, Step Parameters, Pipeline, and Viewer command surfaces use the same cool-light work surface family.
3. Increased the primary Viewer share of the docked workspace and slightly increased header/command spacing for a 1920-pixel work area.
4. Replaced the default bright-blue selected pipeline row with a teal selected surface and teal boundary.
5. Fixed the Tool Inspector and Toolbox panel foreground inheritance so execution/provenance cards remain legible inside the dark docking host.

## Acceptance record

Status: Complete
Scope: Shared Workbench visual system and 1920x1080 layout baseline only. No Tool, recipe, Preview, Publish, Viewer interaction, or persistence behavior changed.
Acceptance criteria:

- Current-source 1920x1080 screen shows one navy/light/teal visual system: Pass. `after-workbench-1920-final.png` was visually compared with the fresh baseline.
- Viewer is the largest top work surface and panels remain docked/resizable: Pass. The fixed star ratios are in the Docking view and docking verification passes `15/15`.
- Selected workflow state does not fall back to the OS-blue ListBox selection: Pass. The final Pipeline row uses the theme's teal selected surface.
- Light information cards use readable dark text: Pass. The final Tool Inspector capture shows readable execution evidence.
- Existing teaching/navigation behavior remains intact: Pass. Focused verifiers listed below all pass.

Verification:

```text
dotnet build OpenVisionLab.ThreeDStudio.sln -c Debug -p:Platform="Any CPU"
  -> Pass: 0 warnings, 0 errors

--verify-workbench-docking
  -> Pass: 15/15
--verify-recipe-manager-wpg
  -> Pass: 17/17
--verify-tool-recipe-teaching
  -> Pass: 16/16
--verify-tool-recipe-selections
  -> Pass: 17/17
--verify-artifact-navigator
  -> Pass: 9/9
```

Evidence:

- Fresh before capture: `artifacts/ui/20260719-workbench-theme-1920/before-workbench-1920.png`
- Fresh final capture: `artifacts/ui/20260719-workbench-theme-1920/after-workbench-1920-final.png`
- Final screenshot quality: accepted on attempt 1; `blackRatio=0.0444`, `whiteRatio=0.3794`, `luminance=0..255`, `sampledPixels=2073600`.
- Focused reports: `artifacts/verification/20260719-workbench-theme-1920`.

Boundary / next dependency:

- The Main Window, Recipe Manager, Filter Tool Lab, and Edge Tool Lab now have separate current-build visual approval captures. Advanced and Calibration remain Main Window workspaces rather than separate WPF Window chrome.
- Do not change Line Fit execution while visual work is being evaluated. The next algorithm gate is still owner approval of the nine Line Fit selection, fit, residual, and acceptance decisions in `OPENVISIONLAB_3D_LINE_FIT_TYPED_ADAPTER_DESIGN_20260719.md`.
- Physical calibration, uncertainty, metrology, device control, cloud, and free-form graph editing remain out of scope.

## P5 teaching-window title-view closure

Status: Complete
Scope: Apply one in-app `StudioTitleBarView` and shared WindowChrome policy to every Shell WPF Window: Main Window, Recipe Manager, Filter Tool Lab, and Edge Tool Lab.
Acceptance criteria:

- Each Window uses `WindowStyle=None` with a 56-pixel custom title view: Pass. Fresh captures show no native Windows title bar.
- Each title view exposes app identity, current window context, minimize, maximize/restore, and close commands: Pass. The controls are visible, named for automation, and invoke the owning Window state/Close API; a real UI Automation smoke passes maximize, restore, and close.
- Tool Lab visual hierarchy remains clear: Pass. The title view supplies product/window context; the second navy band supplies Tool description and explicit Preview/Publish commands.
- Recipe/session behavior remains unchanged: Pass. Existing focused lifecycle, teaching, selection, artifact, and Edge checks all pass.

Verification:

```text
dotnet build OpenVisionLab.ThreeDStudio.sln -c Debug -p:Platform="Any CPU"
  -> Pass: 0 warnings, 0 errors
--verify-workbench-docking -> Pass: 15/15
--verify-recipe-manager-wpg -> Pass: 17/17
--verify-tool-recipe-teaching -> Pass: 16/16
--verify-tool-recipe-selections -> Pass: 17/17
--verify-artifact-navigator -> Pass: 9/9
--verify-tool-edge-workbench -> Pass: 11/11
Windows UI Automation title-bar command smoke -> Pass: maximize=Maximized; restore=Normal; close=True
```

Evidence:

- Main: `artifacts/ui/20260719-teaching-window-theme-1920/after-main-titleview.png`
- Recipe Manager: `artifacts/ui/20260719-teaching-window-theme-1920/after-recipe-manager-titleview-final.png`
- Filter Tool Lab: `artifacts/ui/20260719-teaching-window-theme-1920/after-filter-tool-lab-titleview-final.png`
- Edge Tool Lab with actual Preview: `artifacts/ui/20260719-teaching-window-theme-1920/after-edge-tool-lab-titleview-preview-final.png`
- Reports: `artifacts/verification/20260719-teaching-window-theme-1920`.

Boundary:

- This verifies current-build title-view rendering, named controls, actual maximize/restore/close Window command wiring, and existing workflow behavior. Automated physical title-bar drag, resize-border drag, and minimize were not separately recorded.
- The custom chrome does not alter Preview, Publish, recipe dirty-state confirmation, Viewer gestures below the title view, or Tool execution.

## Review checklist for later screens

- [ ] Panel background, foreground, divider, and selection use shared `ThreeD.*` resources.
- [ ] The active workflow is teal and not a default OS selection color.
- [ ] The Viewer remains the largest relevant work surface at the target window size.
- [ ] Icon-only controls retain tooltip and accessible name; ambiguous commands retain text.
- [ ] A fresh current-build screenshot is captured before accepting a new workspace or window.
- [ ] Every new WPF Window uses `StudioTitleBarView`; do not restore a native Windows title bar.
