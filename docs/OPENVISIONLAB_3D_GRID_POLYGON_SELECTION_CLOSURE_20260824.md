# GridPolygon Selection Authoring Closure

Date: 2026-08-24
Issue: `PL-0050 / E-15`
Status: Complete

## Scope

OpenVisionLab 3D Studio now owns one durable, source-bound `GridPolygon`
selection for height-field grid authoring. The payload stores an ordered list
of finite row/column vertices in the source grid frame. The Viewer draws the
outline and handles; the Workbench exposes the ordered numeric editor with
add, remove, edit, and reorder actions. Draft changes remain transient until
explicit Apply, and Cancel restores the applied selection.

The E-13 matrix contains one explicit `grid-polygon-authoring` pseudo-step.
The fixed vendored `OpenVisionLab.Vision3D`
`3.0.1-dev.20260823.grid-diagnostics.1` package was inspected and exposes no
polygon or mask API. E-15 therefore closes at the authoring, persistence, and
Runner-readable route boundary. No polygon-to-mask arithmetic, region
artifact, or inspection consumer was added.

## Contract And Compatibility

- Generic Tool Recipe schema is `1.7` for `GridPolygon`; schema `1.6` keeps
  its `GridCircle` meaning and rejects a polygon payload.
- A polygon requires `3..256` vertices. Every vertex must be finite and stay
  within the inclusive recorded source-grid row/column bounds.
- Validation rejects duplicate vertices, zero-area outlines,
  self-intersecting or overlapping non-adjacent edges, mixed payloads, stale
  source bindings, incompatible routes, and undeclared consumers.
- Vertex order, selection identity, source/frame binding, and the explicit
  authoring route survive JSON save/reopen and the existing Runner document
  loading path.
- Enter applies and Escape cancels through the existing
  `ToolRecipeWorkbenchView` teaching `InputBinding` contract; no
  polygon-specific keyboard route was duplicated.

## Verification

| Gate | Result |
| --- | --- |
| Release solution build | Pass, 15 projects, 0 warnings / 0 errors |
| Core/Data selection contract | Pass, `63/63`; GridPolygon subset `12/12` |
| Viewer teaching ViewModel | Pass, `34/34`; GridPolygon subset `4/4` |
| Workbench teaching | Pass, `59/59`; GridPolygon save/reopen and transient-Apply checks pass |
| Maximum-count transient editor check | Pass, current EXE smoke opens `256` vertices, restores the six-vertex candidate, and leaves authored/execution state unchanged |
| D-backed recipe storage/execution inspection | Pass, schema `1.7`, one step, one selection, execution-valid; reordered self-intersecting outline rejected |
| Current Wide EXE smoke | Pass; teaching report, lifecycle, screenshot quality, and monitor intersection |
| Current Compact EXE smoke | Pass; teaching report, lifecycle, screenshot quality, and monitor intersection |
| `git diff --check` | Pass |

Focused commands:

```text
dotnet build OpenVisionLab.ThreeDStudio.sln --configuration Release --no-restore
dotnet run --no-build --project src/OpenVisionLab.ThreeD.Runner/OpenVisionLab.ThreeD.Runner.csproj --configuration Release -- --verify-grid-polygon --report D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\20260824-e15\selection-contract-final.txt
dotnet run --no-build --project src/OpenVisionLab.ThreeD.Shell/OpenVisionLab.ThreeD.Shell.csproj --configuration Release -- --verify-teaching-capture-viewmodel D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\20260824-e15\viewer-capture-final.txt
dotnet run --no-build --project src/OpenVisionLab.ThreeD.Shell/OpenVisionLab.ThreeD.Shell.csproj --configuration Release -- --verify-tool-recipe-teaching D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\20260824-e15\workbench-teaching-final.txt
```

The actual EXE runs used the dynamic two-monitor rule. Windows reported two
independent monitors, so `\\.\DISPLAY2` was selected as the smaller working
area on the left. The external evidence records intersecting window rectangles
for Wide `1920 x 1040` and Compact `1280 x 760`. The current workstation ran
at 125% DPI; 100%, 150%, 175%, and 200% remain unverified. The wrapper did not
expose a process exit code, so the evidence claim is based on the internal
teaching/lifecycle reports, screenshot-quality reports, and external window
intersection checks.

Evidence root:
`D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\20260824-e15`

Key evidence:

- `selection-contract-final.txt`
- `viewer-capture-final.txt`
- `workbench-teaching-final.txt`
- `grid-polygon-runtime.ov3d-recipe.json`
- `runtime-wide/grid-polygon-teaching-smoke.txt`
- `runtime-wide/grid-polygon-open-recipe-lifecycle.txt`
- `runtime-wide/grid-polygon-wide.png` and `grid-polygon-wide-quality.txt`
- `runtime-wide/monitor-window-evidence.txt`
- `runtime-compact/grid-polygon-teaching-smoke.txt`
- `runtime-compact/grid-polygon-open-recipe-lifecycle.txt`
- `runtime-compact/grid-polygon-compact.png` and `grid-polygon-compact-quality.txt`
- `runtime-compact/monitor-window-evidence.txt`

## Boundaries

This closure proves deterministic software authoring, validation, persistence,
and Runner parity only. It does not prove a filled mask, region artifact,
downstream inspection behavior, calibrated dimensions, physical metrology,
Gauge R&R, production suitability, owner R0, hosted CI, or release readiness.
The product version remains `0.1.1-dev`; no package, checksum, tag, release,
commit, or push was created by this task.

## Completion Record

```text
Status: Complete
Scope: schema 1.7 GridPolygon contract, fail-closed geometry validation, explicit Viewer/Workbench authoring, shared keyboard Apply/Cancel route, exact save/reopen, Runner parity, and current Wide/Compact runtime evidence
Acceptance criteria: C1 typed source-bound contract and malformed/incompatible rejection -> pass; C2 ordered vertex authoring, transient edit, Apply/Cancel, and keyboard contract -> pass; C3 one explicit E-13 authoring declaration with no mask consumer -> pass; C4 JSON/Workbench/Runner round-trip -> pass; C5 focused verification, Release build, runtime evidence, and diff hygiene -> pass
Verification: Release 0/0; selection 63/63 with GridPolygon 12/12; Viewer 34/34; Workbench 59/59; current Wide/Compact EXE reports include the 256-vertex transient editor check and screenshot quality; D-backed schema/execution inspection; dynamic monitor intersection; git diff --check
Evidence: this document; .proofline/issues/PL-0050.json; D:/OpenVisionLab-TestData/OpenVisionLab-3D-Studio/20260824-e15/
Boundary / next dependency: no mask arithmetic or inspection consumer; 100/150/175/200% DPI, hosted CI, owner R0, package, commit, push, RC, tag, release, and deployment remain separate gates
```
