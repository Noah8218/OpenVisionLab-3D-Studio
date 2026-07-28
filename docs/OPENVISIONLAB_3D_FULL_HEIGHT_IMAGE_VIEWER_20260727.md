# OpenVisionLab 3D Full-Size Height Image Viewer

Date: 2026-07-27

Backlog item: `C-06`

Status: Complete

## Outcome

The normal Inspection Workbench now exposes a full-size, coordinate-true
Height Image as a first-class auxiliary Viewer content.

Opening a side-by-side, stacked, or pop-out auxiliary view now defaults to the
Height Image instead of a duplicate 3D source view. Existing renderable C3D
source and Filter output candidates remain selectable in the same auxiliary
content selector.

The Height Image is view-only:

- it does not replace the main 3D Viewer input;
- it does not edit or dirty the recipe;
- it does not invoke Preview, Publish, Run, Validation Set, or Save;
- it does not reinterpret `GridRectangle` as a volume ROI.

## Coordinate contract

`C3DHeightImageFrame` owns the WPF-neutral mapping:

```text
pixel X = C3D column
pixel Y = C3D row
one source cell = one image pixel
no X/Y flip
no resampling
no interpolation
no dilation
```

The exact contract string is:

```text
pixelX=column;pixelY=row;no-flip;one-source-cell-per-pixel
```

Finite source values use the existing C3D height palette. Zero or non-finite
source cells remain missing and receive one deterministic dark display pixel.
That dark rendering is not yet the explicit, selectable invalid-cell overlay
required by `B-09` and `C-11`.

## Ownership

| Owner | Responsibility |
| --- | --- |
| `OpenVisionLab.ThreeD.Data/C3DHeightImageFrame` | Immutable native-grid values, BGRA pixels, coordinate lookup, statistics, source/pixel SHA |
| `HeightImageViewerViewModel` | Async/cancellable source load, hover state, zoom state, Fit/1:1/zoom commands |
| `HeightImageViewerView` | Frozen `BitmapSource`, nearest-neighbor rendering, wheel zoom, middle-drag pan, Fit, 1:1, pointer-to-cell adapter |
| `ViewerWorkspaceSession` | Layout, focus, and generic auxiliary content identity |
| `ToolWorkbenchViewModel` | Height Image and existing real 3D candidate composition; presentation-only commands |
| `ViewerWorkspaceView` / pop-out window | Reusable inline/pop-out WPF host |

The former `AuxiliaryArtifactId` ownership was renamed to
`AuxiliaryContentId`, because the auxiliary slot now supports both a
diagnostic Height Image and renderable 3D artifacts.

## Operator workflow

1. Load or open an identified C3D recipe source.
2. Select `Height Image`.
3. Use `Fit`, `1:1 pixels`, `Zoom out`, `Zoom in`, or the mouse wheel.
4. Use middle-button drag to pan when the image is larger than the viewport.
5. Move the pointer over the image to inspect exact `column`, `row`, and raw
   height `H`.
6. Use the auxiliary selector to switch back to a real source or Filter C3D
   when 3D comparison is needed.
7. Return to Single without changing recipe or inspection state.

## Exact owner-source evidence

Source:

```text
3D/SyntheticValidation/ThicknessCouponV1/synthetic-thickness-coupon-v1.C3D
```

Recorded by the headless Height Image probe:

| Field | Value |
| --- | ---: |
| Width | 1,466 |
| Height | 2,269 |
| Source cells / image pixels | 1,075,200 |
| BGRA bytes | 13,305,416 |
| Valid cells | 908,436 |
| Missing cells | 166,764 |
| Source SHA-256 | `5D3625B1A5A65EF8BEAB366FF7A007918D28FB614136414BBD30A441E85C8937` |
| Height Image pixel SHA-256 | `D6B402B870622F25C73C10C6D312DF1BB8EC837BC3EFC7A9B5BA8FB8EF432C4A` |

The valid/missing counts agree with the existing `SourceQualityReport`
evidence. `B-09` must next prove invalid-mask byte/pixel identity, not merely
repeat these counts.

## Verification

Commands:

```powershell
dotnet build "OpenVisionLab.ThreeDStudio.sln" -c Release -p:Platform="Any CPU"

dotnet run --no-build --project src/OpenVisionLab.ThreeD.Runner/OpenVisionLab.ThreeD.Runner.csproj -c Release -- `
  --verify-c3d-height-image `
  --report artifacts/current/20260727-full-height-image-viewer/verify-height-image.txt

dotnet run --no-build --project src/OpenVisionLab.ThreeD.Runner/OpenVisionLab.ThreeD.Runner.csproj -c Release -- `
  --height-image-c3d 3D/SyntheticValidation/ThicknessCouponV1/synthetic-thickness-coupon-v1.C3D `
  --entity-id source.c3d.height-map `
  --unit raw-height `
  --frame frame.c3d-grid-index `
  --report artifacts/current/20260727-full-height-image-viewer/actual-source-height-image.txt
```

Current focused evidence:

| Gate | Result |
| --- | --- |
| Release build | `0` warnings / `0` errors |
| Native-grid Height Image golden | `11/11` |
| Exact owner-source Height Image probe | Pass, `1280 x 840`, `1,075,200` pixels |
| Inspection Workspace selection/non-execution | `30/30` |
| Artifact Navigator / real 3D auxiliary regression | `31/31` |
| Workbench docking and pop-out regression | `33/33` |
| SourceQualityReport regression | `12/12` |
| Executable structure guard | `17/17` |
| Wide side-by-side screenshot quality | accepted on attempt 1 |
| Reusable pop-out screenshot quality | accepted on attempt 1 |

## UI evidence

Before: auxiliary side-by-side opened a second 3D Viewer for the same source.

- `artifacts/current/20260727-full-height-image-viewer/before-vertical-duplicate-3d.png`

After: auxiliary side-by-side shows the full native Height Image.

- `artifacts/current/20260727-full-height-image-viewer/after-vertical-height-image.png`

After pop-out: the same reusable Height Image content is hosted in its own
window.

- `artifacts/current/20260727-full-height-image-viewer/after-popout-height-image.png`

## Boundaries and next dependencies

This completion does not claim:

- an explicit invalid-cell mask overlay or mask/image SHA parity (`B-09`,
  `C-11`);
- a saved/manual numeric palette range in this `C-06` slice (`C-07` was
  completed later on 2026-07-28);
- shared Height Image and 3D hover/crosshair identity as part of `C-06`; the
  separate `C-08` slice is now complete;
- shared ROI rendering or Height Image ROI editing as part of `C-06`;
  `C-09/C-10` were completed later on 2026-07-28;
- calibrated physical units, traceability, uncertainty, GR&R, or metrology.

Next dependency-correct order:

1. `B-09 coordinate-true invalid-cell map and mask identity` | Completed 2026-07-28

2. `B-08 unified Source Quality workspace` | Completed 2026-07-28

3. `C-07 manual/auto display-range contract` | Completed 2026-07-28

4. `C-09/C-10 synchronized Height Image / 3D ROI editing` | Completed 2026-07-28

5. `C-11 visible invalid-cell overlay` | Recommended model: `gpt-5.6-terra` | Reasoning effort: medium

## Completion record

Status: Complete

Scope: `C-06` full-size coordinate-true, view-only Height Image Viewer in
inline split, stacked, and reusable pop-out auxiliary hosts.

Acceptance criteria:

- one C3D source cell maps to one image pixel without flip or sampling ->
  pass, headless `11/11`;
- full-size Fit/1:1/zoom/pan/coordinate inspection -> pass, current Release UI
  and Workbench verification;
- auxiliary layouts retain existing real 3D candidates -> pass, Artifact
  Navigator `31/31` and docking `33/33`;
- recipe/input/Preview/Run remain unchanged -> pass, Inspection Workspace
  `30/30`;
- exact owner source is recorded -> pass, `1,075,200` pixels with source and
  pixel SHA-256.

Verification: commands and results listed above.

Evidence:

- this document;
- `artifacts/current/20260727-full-height-image-viewer/`.

Boundary / next dependency: `B-09` invalid-cell mask/image parity is next.
R0 owner replay, physical calibration, and metrology remain external or
unverified.
