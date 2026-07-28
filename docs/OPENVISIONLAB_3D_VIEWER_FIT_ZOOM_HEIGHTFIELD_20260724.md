# Viewer Fit, Zoom, HeightField Presentation - 2026-07-24

## Decision

Status: Complete

The Viewer presents C3D as a 2.5D height field:

```text
X = source column
Y = source height
Z = source row
valid orthogonal neighbors = connected display lines/polygons
```

It does not extrude every sample vertically to a ground plane. The default
remains the owner-approved line-based `Wireframe`; Points, Surface, and
Surface + Edges remain explicit display choices.

The supplied `Thickness_Teaching.mp4` and `Warpage_Teaching.mp4` show the same
product model: the whole map first appears in a fitted top/height-map view,
then perspective is used to inspect shape. The previous fixed
`yaw 34 / pitch 52 / distance 13.2 / target 0` load presentation obscured this
model even though the source mapping and neighbor topology were correct.

## Commercial reference

LMI GoPxL documents separate Surface/Top and Perspective modes, a zoom-to-fit
command, pan/orbit/zoom, and simplified display density when zoomed out:

- <https://am.lmi3d.com/manuals/gopxl/gopxl-1.2/LMILaserLineProfiler/Content/WebInterface/Acquire/DataViewer/DataViewer1.htm>
- <https://am.lmi3d.com/manuals/gopxl/gopxl-1.2/LMILaserLineProfiler/Content/WebInterface/Acquire/DataViewer/SurfaceMode.htm>

Its Mesh surface mode connects surface samples with polygons. Its optional
Sidewall display can hide/show nearly vertical polygons without changing scan
data or measurements. OpenVisionLab 3D does not silently remove steep
connections: such behavior needs an explicit display option and source-unit
policy before implementation.

GoPxL centers the clicked point on Perspective double-click. The owner
explicitly chose double-click Fit for this product, so OpenVisionLab 3D follows
that local interaction contract instead of copying GoPxL exactly:

- <https://am.lmi3d.com/manuals/gopxl/gopxl-1.2/LMILaserLineProfiler/Content/WebInterface/InterfaceOverview/Keyboard_and_mouse_shortcuts.htm>

## Implemented behavior

- Successful C3D load computes Fit from current rendered positions, viewport
  aspect, FOV, and the selected camera orientation.
- Initial C3D presentation uses the maximum safe near-top camera
  `yaw 0 / pitch 80`, then fits the complete sampled bounds.
- Fit All, selected C3D Fit, successful source load, and Viewer double-click
  share the same bounds-derived path.
- Repeated C3D wheel zoom can reach `1%` of that source's fitted distance,
  rather than stopping at the previous fixed `2.4`.
- Projection and pick-ray near planes both use `0.01`, preserving close-view
  picking consistency.
- Wheel events are coalesced into the Viewer frame scheduler instead of
  synchronously rendering every wheel event.
- The visible bilingual hint exposes double-click Fit and right-drag Pan.

Camera state remains ViewModel-owned. The Viewer only supplies current display
positions and viewport aspect. Core, Data, Tools, Runner, recipe, Library-Noah,
ROI/profile, SourceCells measurement, Preview, Publish, and Run are unchanged.

## VBO frame-schedule result

The earlier 60 FPS experiment used the former Display List renderer and was
correctly rejected. After the VBO/IBO renderer removed the CPU submission
bottleneck, the same native schedule was measured again at the same
`1920 x 1040` work area and `931 x 607` embedded Viewer.

| Pointer metric | Current 30 FPS checkpoint | VBO 60 FPS median, 3 runs |
| --- | ---: | ---: |
| MouseMove handler average | `0.208 ms` | `0.126 ms` |
| Next-frame average | `21.626 ms` | `16.140 ms` |
| Next-frame maximum | `43.825 ms` | `29.510 ms` |
| Immediate MouseMove renders | `0` | `0` |
| GPU uploads during interaction | `0` | `0` |
| Double-click Fit | Pass | Pass `3/3` |

All three 60 FPS runs kept one source/display GPU upload, VBO/IBO rendering,
Coarse -> Medium -> Precise LOD restoration, no source reload, and zero
fallbacks. The 60 FPS schedule is therefore retained for the VBO path. The
reported forced-smoke FPS is diagnostic and is not a display-refresh or GPU
timestamp claim.

## Verification

- Debug solution build: Pass, `0` warnings / `0` errors.
- Viewer display/camera verification: Pass, `95/95`.
- C3D geometry density/style matrix: Pass, `12/12`.
- Actual WPF pointer runs at 60 FPS: Pass, `3/3`.
- Tool recipe teaching: Pass, `25/25`.
- Docking workspace: Pass, `27/27`.
- Actual C3D async load and current Viewer/Shell captures: Pass.
- Screenshot quality: Pass on attempt 1.

Evidence:

- `artifacts/current/20260724-owner-viewer-feedback/`
- `artifacts/current/20260724-owner-viewer-feedback/after-final-shell.png`
- `artifacts/current/20260724-owner-viewer-feedback/after-60fps-run1-pointer.txt`
- `artifacts/current/20260724-owner-viewer-feedback/after-60fps-run2-pointer.txt`
- `artifacts/current/20260724-owner-viewer-feedback/after-60fps-run3-pointer.txt`

## Boundary

This is fixed-machine local Debug evidence for the established Thickness C3D
and geometry matrix. It does not prove Release behavior, every data shape,
all GPUs, physical scale, calibration, Gauge R&R, or metrology.

The owner first-recipe replay remains incomplete because the replay was
stopped when these Viewer defects were found. It must restart from a clean
current EXE after this checkpoint.

## Completion record

Status: Complete

Scope: current-bounds C3D Fit, double-click Fit, expanded close zoom,
top-oriented load presentation, wheel render coalescing, and VBO-backed 60 FPS
schedule.

Acceptance criteria: all requested Viewer behaviors are implemented; C3D
remains neighbor-connected HeightField/Wireframe rather than ground
extrusions; source and inspection contracts remain unchanged.

Verification: build `0/0`; display/camera `95/95`; geometry `12/12`; pointer
`3/3`; recipe `25/25`; docking `27/27`; current actual-EXE screenshots pass.

Evidence: `artifacts/current/20260724-owner-viewer-feedback/`.

Boundary / next dependency: restart the unaided owner first-recipe replay; an
optional visible Sidewall display toggle is a later UX decision, not part of
this checkpoint.
