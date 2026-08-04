# OpenVisionLab 3D Linked-view Display-range Consistency

Date: 2026-08-03

Status: Complete

## User goal

Complete `C-13` so the 3D Viewer and the full Height Image use one numeric
manual/auto display range when they show the same C3D source. Range changes
must remain presentation-only.

## Scope and boundary

Included:

- synchronize manual range changes in either linked view;
- synchronize `AUTO` in either linked view;
- preserve exact numeric bounds, including a Height Image interval outside
  the native source extrema;
- synchronize only when the C3D source-content SHA-256 matches;
- retain each view's own palette and source-distribution presentation;
- prove that range changes do not mutate or execute inspection state.

Excluded:

- combining or replacing source histograms;
- persisting display range in a recipe;
- changing source values, invalid masks, ROI, measurements, thresholds,
  decisions, or result routing;
- automatically invoking Preview, Publish, Run, or Validation;
- physical calibration, uncertainty, GR&R, or metrology claims.

## Responsibility and data-flow change

Before, `MainWindowViewModel` and `HeightImageViewerViewModel` independently
owned ranges for the same loaded C3D source. The current-build baseline showed
`100..120` in the 3D Viewer and `95..110` in Height Image simultaneously.

After:

```text
3D range change
  -> C3DHeightColorRangeRevision
  -> ViewerWorkspaceView source-SHA guard
  -> HeightImageViewerViewModel linked numeric range

Height Image Apply/AUTO
  -> DisplayRangeRevision
  -> ViewerWorkspaceView source-SHA guard
  -> MainWindowViewModel linked numeric range/AUTO
```

`ViewerWorkspaceView` already owned linked 2D/3D cursor coordination, so it
also owns this small presentation bridge. It blocks feedback loops and does
not own numerical normalization, recipe state, or execution. Dependency
direction remains Shell -> Viewer; Viewer does not reference Shell.

The existing 3D manual text/button path keeps its source-bounded policy. The
linked API preserves the exact range supplied by Height Image so established
Height Image behavior is not silently coerced. The existing full-source 3D
histogram and source SHA remain unchanged.

## Current UI evidence

Physical root:

```text
D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260803-c13-linked-view-display-range-consistency\
```

True current-build before evidence:

- `before/wide-visible-independent-ranges.png`;
- `before/compact-visible-independent-ranges.png`.

The same source visibly has 3D `100..120` and Height Image `95..110`.

Current-build after evidence:

- `after/wide-linked-range.png`;
- `after/compact-linked-range.png`.

Both linked views visibly finish at `95..110`. The two screenshot quality
gates passed on attempt 1. Visual review found no required-label/action
clipping, overlap, out-of-pane control, unreachable action, or unintended
horizontal/nested scrollbar at Wide `1920 x 1040` or Compact `1280 x 760`.

This slice adds no control, visible string, icon, control template, or theme
role. Existing localized labels, Automation properties, focus/hover/disabled
templates, palette selector, and invalid-range presentation remain unchanged.
Manual state is covered by the final captures; exact AUTO round-trip and
invalid manual input are covered by the focused smoke and Inspection
Workspace verifier.

The active leftmost monitor was `\\.\DISPLAY1`, bounds
`0,0,1920,1080`; it was the only monitor. Both actual EXE window sizes
intersected it. See `verification/monitor-topology.txt`.

## Verification

Current source is the dirty workspace based on Git HEAD `877c64c`. Test-only
TEMP/TMP and evidence were routed to the D-drive evidence root.

| Gate | Result |
| --- | --- |
| Release solution build | Pass, `0` warnings / `0` errors |
| Actual linked-range Wide smoke | Pass; both directions, AUTO, final shared range |
| Actual linked-range Compact smoke | Pass; both directions, AUTO, final shared range |
| C3D height distribution | `26/26` |
| C3D Height Image | `25/25` |
| Inspection Workspace | `64/64` |
| Generic height-measurement Workbench | `54/54` |
| Docking workspace | `82/82` |
| Validation Set | `84/84` |
| Shell command line | `28/28` |
| Code structure | `29/29` |
| R0 Wide `-ValidateOnly` | Pass |
| R0 Compact `-ValidateOnly` | Pass |

The Wide and Compact range reports record:

```text
sourceMatch=True
heightImageToThreeD=True
mismatchedSourceIsolated=True
threeDToHeightImage=True
auto=True
finalShared=True
dirty=False->False
previewRunning=False->False
outputSame=True
```

Fixed R0 binary hashes changed only where expected:

```text
Shell DLL  575EE754CCAF594C4910994DE505613E9BDE83217527842C002390852346E01A
Viewer DLL 22B5C598E8F9775BC4A875176F1DB15EBFC044780D81FC690DF5B1D4C3971374
```

## Durable completion record

Status: Complete

Scope: `C-13` exact manual/AUTO display-range synchronization between the
same-source 3D Viewer and full Height Image, with source-identity and
presentation-only boundaries.

Acceptance criteria:

- manual range applied in Height Image reaches the 3D Viewer exactly -> Pass,
  actual Wide/Compact linked reports;
- manual range applied in the 3D Viewer reaches Height Image exactly -> Pass,
  actual reports and precision verifiers;
- `AUTO` restores both views to the same native source bounds -> Pass, actual
  reports;
- unrelated source identities do not synchronize -> Pass, actual tracked
  mismatched-C3D round-trip and explicit source-content SHA guard;
- palettes and source distributions remain independent and source evidence is
  unchanged -> Pass, `26/26` and `25/25`;
- recipe and execution state remain unchanged -> Pass, actual reports,
  `64/64`, and `54/54`;
- Wide and Compact UI remain usable -> Pass, fresh before/after captures and
  attempt-1 quality reports.

Verification: commands and results are recorded under the physical evidence
root above.

Evidence: this document and the D-backed evidence root.

Boundary / next dependency: no dependency-ready standalone layout item remains
in the current approved candidate list. `A-11` requires `A-09` and the human
owner's unaided `A-01` R0 evidence; do not spend model tokens before those
prerequisites exist. Returning to `J-12 Multiple-match result collection`
requires an explicit owner decision to leave the layout-only stream.

1. `A-11` prerequisite evidence | Prerequisite: `A-09` plus human-owner `A-01`; no model tokens until available.

2. `J-12 Multiple-match result collection` after owner approval | Recommended model: `gpt-5.6-sol` | Reasoning effort: high.
