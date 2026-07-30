# Completeness Grid Metrics

Date: 2026-07-29

Status: Complete for `H-02/H-03/H-04`

## Product-direction source

This slice implements the next bounded item derived from the review of all 11
owner-supplied commercial videos:

- SICK Nova: task-oriented presence/completeness inspection with clear
  per-region evidence;
- MVTec MERLIC: Height Image cells as a first-class completeness inspection
  surface;
- GoPxL: separate recipe authoring, selected-tool editing, explicit
  execution, and result evidence.

OpenVisionLab remains a local, file-first, deterministic 2.5D/3D rule-based
inspection workbench. Camera acquisition, stereo reconstruction,
PLC/robot/fieldbus, cloud/plant management, and physical-metrology claims
remain outside this slice.

## Completed scope

`Completeness Grid` is an ordinary typed Measure step:

```text
HeightField
  + Reference GridRectangle
  + Inspection GridRectangle
  + rows / columns / X pitch / Z pitch / cell width / cell height / shape
  -> CompletenessGridMetrics
```

The persisted profile uses native source-grid coordinates:

- X is source column;
- Z is source row;
- `GridRectangle` is the only v1 cell shape;
- pitch and cell sizes are positive integers;
- pitch must be at least cell size, so generated cells do not overlap;
- the complete generated extent must fit inside the authored Inspection Grid
  ROI.

Cell IDs are stable row-major identities:
`r001.c001`, `r001.c002`, and so on. Each cell records its exact
`ToolRecipeGridRectangle`, total/finite/missing cell counts, finite coverage
ratio, optional mean raw height, explicit Reference ROI mean, and optional
reference-relative mean raw height.

The typed output has a deterministic SHA-256 over source, selection, profile,
cell geometry, and metric evidence. Source bytes and the authored recipe are
not changed by Preview or Runner execution.

## Explicit lifecycle and boundary

Workbench uses the existing two-role teaching lifecycle:

1. teach Reference ROI;
2. teach Inspection Grid ROI;
3. edit the typed grid profile in PropertyGrid;
4. explicitly apply the parameter draft;
5. explicitly Preview;
6. optionally Publish the exact Preview without recalculation.

Teaching and PropertyGrid edits do not run metrics. The result status is
`Warning`, with the explicit message that no acceptance policy was applied.
This avoids presenting calculation success as an inspection Pass.

This slice intentionally does not implement:

- per-cell presence thresholds or Pass/Fail (`H-05`);
- failed-cell aggregation (`H-06`);
- colored cell overlays (`H-07`);
- failed-cell navigation, threshold assistance, or sample-derived limits;
- calibrated X/Z pitch, physical units, or metrology.

## Controlled fixture

The `8 x 8` fixture uses one four-cell Reference ROI with mean raw height
`10`, and a `2 x 2` inspection grid of `2 x 2` cells.

| Cell | Region `(row, column, rows, columns)` | Finite / total | Coverage | Mean relative to reference |
| --- | --- | ---: | ---: | ---: |
| `r001.c001` | `2, 0, 2, 2` | `4 / 4` | `1.00` | `2` |
| `r001.c002` | `2, 2, 2, 2` | `3 / 4` | `0.75` | `4` |
| `r002.c001` | `4, 0, 2, 2` | `2 / 4` | `0.50` | `-2` |
| `r002.c002` | `4, 2, 2, 2` | `0 / 4` | `0.00` | missing |

Identities:

- source SHA-256:
  `634CEA27B3483D51173145884D78B88A313D596D048358F14456B0312AAB0042`;
- typed output SHA-256:
  `C535D7C8DF40C585E5A22EBF5594D48768A89A20DF257A82DE6F3E75752BED6C`.

The direct adapter, repeated evaluation, ordered graph, and production Runner
emit the same output SHA-256. A `3 x 2` profile fails closed because its
`4 x 6` extent does not fit the `4 x 4` Inspection Grid ROI.

## Verification

Commands actually run:

```powershell
dotnet build "OpenVisionLab.ThreeDStudio.slnx" -c Release

OpenVisionLab.ThreeD.Runner.exe `
  --verify-c3d-completeness-grid `
  --report artifacts/current/20260729-completeness-grid-metrics/golden-report.txt

OpenVisionLab.ThreeD.Shell.exe `
  --verify-tool-height-measurement-workbench `
  artifacts/current/20260729-completeness-grid-metrics/workbench-report.txt

OpenVisionLab.ThreeD.Runner.exe `
  --tool-recipe <controlled-recipe> `
  --source <controlled-c3d> `
  --report <runner-report> `
  --run-record <json> `
  --html-report <html> `
  --csv-report <csv>
```

Results:

- Release build: `0` warnings, `0` errors;
- Completeness Grid golden: `14/14`;
- generic height measurement Workbench: `50/50`;
- Tool Recipe selections: `29/29`;
- Inspection Workspace: `63/63`;
- Tool Recipe teaching: `28/28`;
- Recipe Manager/PropertyGrid: `37/37`;
- Workbench docking: `33/33`;
- Artifact Navigator/Output Compare: `31/31`;
- Shell smoke options: `24/24`;
- code structure: `17/17`;
- production Runner: `Warning`, `1/1` step, exact output SHA parity;
- Wide and Compact current-Release screenshot quality: accepted on attempt
  `1`;
- `git diff --check`: pass.

## UI evidence

- `before-workbench.png`: fresh Release baseline captured before H-02/H-03/
  H-04 implementation; no Completeness Grid step or output exists.
- `after-wide.png`: the opened controlled recipe shows separate Reference and
  Inspection Grid ROI cards plus the four-cell evidence-only Preview.
- `after-compact.png`: the same `4` cell count, `Warning` state, minimum
  coverage, missing-cell count, reference mean, and no-acceptance message
  remain visible at `1280 x 760`.

All captures contain only the application window.

## Completion record

Status: Complete

Scope: `H-02/H-03/H-04` typed deterministic cell grid, exact per-cell finite
coverage, exact per-cell reference-relative mean raw height, explicit
Workbench Preview/Publish, and production Runner parity.

Acceptance criteria: typed rows/columns/pitch/cell shape -> pass;
coordinate-true stable cell identities and geometry -> pass; known
missing-cell coverage -> pass; known reference-relative height -> pass;
source immutability -> pass; out-of-footprint failure -> pass; no implicit
execution or acceptance -> pass; Workbench/Runner identity parity -> pass.

Verification: Release build `0/0`; golden `14/14`; Workbench `50/50`;
selection `29/29`; Inspection Workspace `63/63`; teaching `28/28`; Recipe
Manager/PropertyGrid `37/37`; docking `33/33`; Artifact Navigator `31/31`;
Shell options `24/24`; structure `17/17`; production Runner and current
Wide/Compact captures pass.

Evidence:
`artifacts/current/20260729-completeness-grid-metrics/`.

Boundary / next dependency: this evidence does not decide presence or
completeness. Its historical `H-05/H-06/H-07` dependency is complete in
`docs/OPENVISIONLAB_3D_COMPLETENESS_RESULTS_AND_OVERLAYS_20260729.md`.
Owner R0 replay and physical calibration/metrology remain external or
unverified.
