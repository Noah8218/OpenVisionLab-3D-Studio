# OpenVisionLab 3D Typed ROI/Crop Preparation Closure

Date: 2026-08-23

Status: Complete

Owner issue: `PL-0037` / `D-03`

## Completed Scope

`ROI / Crop` is now an executable preparation step rather than a catalog-only
entry. One exact source-owned `GridRectangle` produces a separate smaller
`HeightField`; it never mutates or replaces the source. The output preserves
finite values, missing cells, root-source SHA-256, declared unit/frame, and an
accumulated source-grid row/column origin.

The normal operator path is:

1. add or select `ROI / Crop`;
2. teach one exact source-grid rectangle;
3. run explicit Preview;
4. review the smaller output, origin, valid/missing counts, metrics, and ROI
   overlay;
5. Publish the exact Preview without recalculation;
6. optionally route the Published crop to Thickness, Warpage, or Completeness
   and teach later ROIs in the cropped local grid;
7. run the authored recipe in order and save/reopen it.

Changing the source, crop ROI, route, output, or fixed policy makes the crop
Preview stale. Changing a Published crop input also makes an existing dependent
measurement Preview stale. Neither invalidation executes inspection.

## Ownership And Provenance

- SDK owner: public sealed `HeightMapCropTool` with typed controlled
  `HeightMapCropResult`, exact row-major copy, cancellation, and output-origin
  arithmetic.
- SDK source commit:
  `7da6631e714a9257af36c3da575474df9331ff36`.
- Vendored package:
  `OpenVisionLab.Vision3D 3.0.1-dev.20260823.crop.1`.
- Package SHA-256:
  `9858329EC19BCD9140805B35DA305FF64BA2ED5D827FA36846E8B259EB9C467A`.
- Studio owner: source/selection/output identity validation, immutable C3D
  snapshot composition, recipe routing, Preview/Publish/Run lifecycle,
  artifact-owned later selections, evidence, persistence, and presentation.
- Numerical migration debt remains zero; the reviewed Studio-boundary
  inventory is `34` and the structure guard is `68/68`.

The Studio uses only the exact vendored package. It has no SDK
`ProjectReference` and contains no duplicate crop loop.

## Changed Product Areas

- SDK: `FeatureExtraction/HeightMapCropTool.cs`, smoke suite, and SDK docs.
- Data: immutable crop output, root identity, and source-grid origin in
  `C3DHeightFieldSnapshot`; `HeightField` selection binding.
- Core: strict ROI/Crop and compatible later-input validation.
- Tools: `C3DRoiCropRule`, recipe adapter, ordered graph execution, and later
  height-measurement preparation.
- Shell: one ROI/Crop execution owner, generic Preview/Publish routing,
  Viewer/compare/artifact/evidence projection, cropped-output teaching, and
  dependent Preview invalidation.
- Runner: `--verify-c3d-roi-crop --report <path>`.
- UI: existing graphite Workbench surface; no new XAML control, resource,
  template, animation, or framework was added.

## Acceptance Evidence

| Criterion | Result | Evidence |
| --- | --- | --- |
| SDK typed crop, cancellation, exact values/mask/origin | Pass | SDK Release `0/0`; smoke `163/163` |
| Immutable Studio output identity and evidence | Pass | Runner ROI/Crop `6/6`; Workbench exact values/hash/origin/metrics/overlay |
| Explicit Preview/Publish, stale, display, compare | Pass | Workbench `19/19`; current Wide/Compact EXE captures |
| Compatible later-tool teaching and execution | Pass | Exact `HeightField` owner/hash/grid binding; crop-to-Warpage Preview and two-step ordered replay in Workbench `19/19` |
| Save/reopen and source unchanged | Pass | Workbench output-hash round trip and byte-for-byte source check |
| Package and structural boundary | Pass | package verifier Pass; structure `68/68`; migration debt `0` |
| Related preparation regression | Pass | Remove Outlier Workbench `14/14`, Runner `9/9`; Level Surface Workbench `17/17`, Runner `9/9` |
| Full Studio build | Pass | Release solution `0` warnings, `0` errors |

Commands actually run included:

```powershell
dotnet build OpenVisionLab.ThreeDStudio.sln -c Release --nologo
scripts\verify-vision-sdk-package.ps1
scripts\verify-code-structure.ps1 -ReportPath <D-backed-report>
OpenVisionLab.ThreeD.Shell.exe --verify-roi-crop-workbench <D-backed-report>
OpenVisionLab.ThreeD.Runner.exe --verify-c3d-roi-crop --report <D-backed-report>
```

The canonical local evidence root is:

`D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260823-pl0037-roi-crop`

Important files:

- `sdk/release-build-final.txt`
- `sdk/smoke-final.txt`
- `verification/studio-package-verification-final.txt`
- `verification/solution-release-build-final.txt`
- `verification/roi-crop-workbench-final.txt`
- `verification/runner-roi-crop-golden-final.txt`
- `verification/code-structure-report-final.txt`
- `ui/wide-preview-current.png`
- `ui/compact-preview-current.png`
- corresponding `*-open.txt` and `*-quality.txt` reports

## Runtime UI Review

Affected view/control/state: Workbench selected `ROI / Crop` step, taught
GridRectangle, Preview-ready output card, crop Viewer display, Wide layout,
Compact layout, and persistent bottom status boundary.

Current Release EXE screenshots show the exact `3 x 3` cropped output with
source origin `(2, 1)`, `8` valid cells, `1` missing cell, visible ROI overlay,
and no observed required-text clipping, overlap, off-pane rendering, or
platform-default control leak. The dynamically selected left small monitor was
`-2400,456..0,1806`; Wide and Compact windows intersected it. The available
runtime scale was 125%. Source inspection found only the supported graphite
theme and no theme switch.

No shared style or ControlTemplate changed, so normal/hover/pressed/disabled
appearance was not reimplemented by this slice. Existing commands were
exercised for enabled/disabled readiness, explicit Preview, Publish without
rerun, cancellation plumbing, stale recovery, and repeated ordered execution.
100%, 150%, 175%, and 200% DPI were unavailable in this run and remain
unverified; no claim is made for those scales.

## Closure Boundary

- Human-owner unaided Wide/Compact R0 remains deferred and is not replaced by
  these automated or Codex-observed checks.
- The changed SDK and Studio source invalidate the prior frozen Phase 1
  release-candidate package/CI identity. A new Studio commit, hosted CI run,
  fixed package, and owner R0 require separate approval and qualification.
- No Studio commit, push, version change, release, or original-repository sync
  was performed.
- Raw-height evidence is not calibrated metrology, Gauge R&R, or production
  approval.

## Durable Completion Record

```text
Status: Complete
Scope: Typed ROI/Crop SDK tool, immutable Studio output, explicit Preview/Publish, compatible later-tool teaching, ordered Runner, save/reopen, and current Wide/Compact runtime evidence.
Acceptance criteria: PL-0037 C1-C6 -> pass; Workbench 19/19; Runner 6/6; Release 0/0; structure 68/68.
Verification: SDK build/smoke, package verifier, full Studio Release build, focused Workbench/Runner regressions, structure guard, git diff check, current actual EXE Wide/Compact capture.
Evidence: docs/OPENVISIONLAB_3D_ROI_CROP_TYPED_PREPARATION_CLOSURE_20260823.md and D-backed 20260823-pl0037-roi-crop evidence root.
Boundary / next dependency: Human-owner R0 and a newly frozen/hosted release candidate are separate; next dependency-ready software slice is coherent proven-decoder Import.
```
