# NIST Overhang X4 Part 2 Visible Workflow

Date: 2026-07-15
Updated: 2026-07-16
Status: Passed locally for the fixed Part 2 identity-frame source/query pair

## Decision

The second NIST physical instance now passes the existing nominal/actual product workflow in the standalone Viewer, hosted Shell, saved recipe, reopened Viewer, and headless Runner.

This closes the Viewer Reliability Phase 2 second-pair checklist item. It does not close Phase 2 as a whole: the difficult-geometry matrix and runtime-neutral registration acceptance policy pass locally and in Windows CI, while approved runtime/result mapping and Viewer/Runner registration parity remain open. It is not arbitrary-geometry, arbitrary-alignment, calibration, uncertainty, or metrology evidence.

## View -> ViewModel -> Model Review

The implementation order was evaluated explicitly:

1. **View:** the existing generic nominal/actual bindings already expose actual, nominal, query, hashes, frame, units, alignment, Preview/Publish state, metrics, selected-point evidence, and result layers. No XAML change was needed.
2. **ViewModel:** `NominalActualComparisonViewModel` already accepts dataset-independent identities and owns state, commands, progress, selection, and Preview/Publish fingerprints. No ViewModel change was needed.
3. **Model/Runner:** Core, Tools, recipe, and Runner contracts already preserve arbitrary stable source IDs and paths. No numerical or persistence model change was needed.
4. **WPF/smoke bridge:** the fixed smoke argument path incorrectly assigned Part 1 IDs to every supplied file. `--smoke-nominal-actual-dataset nist-overhang-x4-part2` now selects Part 2 actual/query IDs while omission preserves the established Part 1 behavior. Unsupported values fail before file processing.

The bridge remains limited to command-line smoke input. Durable workflow state and command behavior remain ViewModel-owned, and comparison calculation remains Tools-owned.

## Fixed Identities

| Role | Stable ID | Bytes | SHA-256 |
| --- | --- | ---: | --- |
| Actual Part 2 source | `source.nist-overhang-x4-actual-part2` | 402,032,984 | `0F74D3A949488C161DAC71681420A171B1EDA3E478ED24D492D33AA6C9F7F032` |
| Nominal source | `source.nist-overhang-x4-nominal-9x5x5` | 145,284 | `D9FC086CA8C0BC3722709E5C03A39C5C1CF60553845FF62F5699780E1D3C1734` |
| Part 2 validation query | `query.nist-overhang-x4-part2-cloudcompare-vertices` | 47,585,417 | `F4831F96B3709DC69AD46F28CA22DE8EB6FF6D751FC693B80196F7B22B5C19F1` |

The step remains `step.nist-overhang-x4-surface-deviation` because it identifies the inspection step, not a manufactured instance. Unit is `mm`, frame is `frame.nist-overhang-x4-321-part`, alignment is `alignment.identity-source-provided`, direction is actual-to-nominal, and evaluation sampling is `full-query`.

## Before And After Evidence

The current-build before run used the Part 2 files but recorded:

```text
actualId=source.nist-overhang-x4-actual-part1
actualSha256=0F74D3A949488C161DAC71681420A171B1EDA3E478ED24D492D33AA6C9F7F032
```

That mismatch proved the visible/provenance defect. The after run records the same source hash under `source.nist-overhang-x4-actual-part2` and the query under `query.nist-overhang-x4-part2-cloudcompare-vertices` in the Shell, Viewer contract, recipe, Runner report, Run Record, result entity, overlay, and selected-point evidence.

Both embedded Viewer and full Shell after captures passed the shared screenshot-quality gate on attempt 1. The reopened standalone Viewer also passed on attempt 1.

## Fixed Result

| Item | Result |
| --- | --- |
| Status | `Fail` at `[-0.3, 0.3] mm` |
| Full-query points | 3,965,430 |
| Below / within / above | 507,115 / 2,794,040 / 664,275 |
| Out of tolerance | 1,171,390 |
| Direct / robust sign | 3,893,224 / 72,206 |
| Signed min / max | `-0.47488307952880859` / `1.221400355129578 mm` |
| Signed mean / population std | `0.018165331599898051` / `0.28679182760389893 mm` |
| Unsigned mean / population std | `0.19439514253736132` / `0.21163662278146758 mm` |
| Balanced display proxy | 59,186 samples, stride 67, budget 60,000 |

These values match the fixed independent CloudCompare/OpenVisionLab Part 2 baseline within its documented tolerances. Display sampling does not affect the full-query result.

The deterministic pointer-ray smoke selects ordered query point `2,479,201` at `(6.294690, 3.821311, 4.582564) mm`, signed/unsigned deviation `-0.41743612289435522` / `0.41743612289435522 mm`, nearest nominal triangle `725`, direct sign, and `Below lower tolerance`. The selection preserves the Part 2 actual/query IDs.

## Recipe, Runner, And Run Record

The Viewer-saved recipe is 1.0 typed `nominal-actual-surface-deviation` with SHA-256:

```text
04D61ABC67770CF7414C5F06BFCCF3FF4D96EB724E52B7CEEE6BA213213CC2F1
```

Runner replay reports `ViewerRunnerComparison|Matched`. Schema `1.2` JSON, HTML, and CSV preserve the Part 2 source, nominal reference, query measurement, step, metrics, overlay, execution identity, and `Matched` state.

Viewer reopen reproduces byte-identical contract lines for input identity, signed/unsigned statistics, display sampling, selected-point evidence, result source, and result overlay. Only volatile elapsed-time/status wording is excluded from the stable comparison.

## Regression Evidence

| Gate | Result |
| --- | --- |
| Solution build | Pass, 0 warnings / 0 errors |
| Nominal/actual executor/recipe verification | Pass, 27/27 |
| Nominal/actual ViewModel verification | Pass, 71 checks |
| Unsupported dataset | Controlled `No inputs`, smoke exit 1 |
| Existing Part 1 default profile | Pass, established IDs and 4,223,524-point result preserved |
| Viewer/Shell fixed matrix | Pass, 128/128 |
| BinaryHost | Pass, zero ProjectReference, manifest 13/13, outputs 12/12, Host API commands 3/3 |
| Viewer WPF pointer input | Pass, routed events 3/12/3/1 and pick/orbit/pan/zoom |
| Shell WPF pointer input | Pass, routed events 3/12/3/1 and pick/orbit/pan/zoom |

Evidence is under ignored `artifacts/nist_part2_visible_20260715`:

- `before/viewer_before.png`, `before/shell_before.png`, and `before/viewer_before_contract.txt`;
- `after/viewer_after.png`, `after/shell_after.png`, and `after/viewer_after_contract.txt`;
- `after/viewer_reopen.png` and `after/viewer_reopen_contract.txt`;
- `after/nist_part2_nominal_actual.recipe.json`;
- `after/nist_part2_runner_report.txt`, Run Record JSON, HTML, and CSV;
- `regression` reports, matrix, BinaryHost, and pointer evidence.

## Reproduction

The ignored local NIST inputs are required:

```powershell
$actual = 'artifacts\research_samples\nist_overhang_x4_part2\OverhangPartX4 Part2 Surface_cleaned.stl'
$query = 'artifacts\research_samples\nist_overhang_x4_part2\cloudcompare_deviation_20260715\measured_vertices_full.ply'
$nominal = 'artifacts\research_samples\nist_overhang_x4\OverhangPart_9x5x5mm.STL'
$out = 'artifacts\nist_part2_visible_20260715\after'

dotnet build OpenVisionLab.ThreeDStudio.slnx -c Debug

dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- `
  --smoke-screenshot "$out\viewer_after.png" `
  --smoke-screenshot-quality-report "$out\viewer_after_quality.txt" `
  --smoke-contracts "$out\viewer_after_contract.txt" `
  --shell-smoke-screenshot "$out\shell_after.png" `
  --shell-screenshot-quality-report "$out\shell_after_quality.txt" `
  --smoke-density Balanced `
  --smoke-nominal-actual $actual $query $nominal `
  --smoke-nominal-actual-dataset nist-overhang-x4-part2 `
  --smoke-pick nominal-actual `
  --smoke-publish-result `
  --smoke-save-recipe "$out\nist_part2_nominal_actual.recipe.json"

dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- `
  --recipe "$out\nist_part2_nominal_actual.recipe.json" `
  --report "$out\nist_part2_runner_report.txt" `
  --expect-status Fail `
  --compare-contract "$out\viewer_after_contract.txt" `
  --viewer-screenshot "$out\viewer_after.png" `
  --run-record "$out\nist_part2_run_record.json" `
  --html-report "$out\nist_part2_run_report.html" `
  --csv-report "$out\nist_part2_run_report.csv"
```

## Remaining Phase 2 Gates

Phase 2 remains open. The difficult-geometry audit passes locally and in Windows CI with mesh deviation `23/23` and nominal/actual execution `29/29`; see `docs/OPENVISIONLAB_3D_PHASE2_DIFFICULT_GEOMETRY_GOLDENS_20260715.md`. Registration acceptance must separately record correspondence count and fitness before RMSE and reject zero-correspondence false success in the accepted product execution path.
