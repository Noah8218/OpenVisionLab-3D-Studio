# Main Window Work-Area Contract

Date: 2026-07-22
Status: Complete

## Observed defect

On the owner's `1920 x 1080` desktop with a bottom taskbar, the main custom-
chrome window opened as a normal `1920 x 1080` window. Its measured bounds
were `(26,26)-(1946,1106)`, while the Windows work area was
`(0,0)-(1920,1040)`. The taskbar therefore covered the lower 40 pixels and
the application bottom was not visible.

Setting only `WindowState=Maximized` was insufficient: WPF `WindowChrome`
still produced `(-7,-7)-(1927,1087)`. That intermediate result was rejected.

## Implemented contract

- Main Window starts maximized.
- `WM_GETMINMAXINFO` constrains the custom-chrome maximum bounds to the
  current monitor's `rcWork`, not the full monitor rectangle.
- This follows taskbars placed on any edge and the monitor containing the
  window.
- Normal restore size is `1600 x 900`, so restore remains fully reachable on
  the validated desktop.
- Minimize, restore, maximize, close, Viewer interaction, docking, and recipe
  behavior are unchanged.

Regression checklist:

1. Start the current Debug EXE: main-window bounds equal current-monitor work
   area.
2. Restore: the complete window fits inside that work area.
3. Maximize again: bounds equal the work area again.
4. Confirm the taskbar, Viewer status bar, and bottom Tool Library action are
   all visible.

## Current evidence

The current Debug build passed with 0 warnings and 0 errors. The live window
probe recorded:

```text
INITIAL_MAX|rect=0,0,1920,1040|workArea=0,0,1920,1040|match=True
RESTORE|rect=78,78,1678,978|fits=True
REMAXIMIZE|rect=0,0,1920,1040|match=True
```

The current Shell screenshot quality check passed on attempt 1. Evidence is
under `artifacts/current/20260722-shell-work-area/`:

- `before-user-observed.png` - owner-observed clipped baseline;
- `maximized-after-desktop.png` - complete app plus visible taskbar;
- `maximized-after-window.png` - current-window capture;
- `maximized-after-window-quality.txt` - screenshot quality report;
- `window-work-area-report.txt` - initial/restore/re-maximize bounds.

## Completion record

```text
Status: Complete
Scope: Main custom-chrome startup, maximize work-area bounds, and usable restore bounds only.
Acceptance criteria: Taskbar remains visible; full main content ends above it; initial maximize and re-maximize equal the current work area; restored window fits; current build and screenshot quality pass.
Verification: Build 0 warnings/errors; live HWND/work-area probe 3/3; Shell screenshot accepted on attempt 1; current desktop capture visually reviewed.
Evidence: artifacts/current/20260722-shell-work-area/ and this document.
Boundary / next dependency: Validated on the current 1920 x 1080 Windows desktop. No algorithm, recipe, Runner, or physical/metrology claim changed.
```
