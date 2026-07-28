# NIST Nominal/Actual End-To-End Baseline

Checked: 2026-07-15

## Decision

The first fixed measured/nominal product slice passes locally for NIST AMMT `Overhang Part X4` in its source-provided 3-2-1 part frame. OpenVisionLab now completes this explicit workflow:

```text
typed actual/nominal/query inputs
  -> explicit Preview over the full validation query
  -> signed deviation metrics and color evidence
  -> explicit Publish to a separate result entity/layer
  -> typed recipe save and reopen
  -> headless Runner replay
  -> Viewer/Runner comparison
  -> schema 1.2 JSON plus HTML/CSV evidence
```

This is a fixed-sample, identity-transform baseline. It is not proof of arbitrary alignment, broad mesh compatibility, physical uncertainty, certified metrology, or redistribution rights.

## Product Contract

| Item | Fixed baseline |
| --- | --- |
| Recipe type/version | `nominal-actual-surface-deviation` / `1.0` |
| Step | `step.nist-overhang-x4-surface-deviation` |
| Direction | `ActualToNominal` |
| Evaluation sampling | `full-query` |
| Unit | `mm` |
| Frame | `frame.nist-overhang-x4-321-part` |
| Alignment | `alignment.identity-source-provided` |
| Tolerance | `[-0.3, 0.3] mm` |
| Actual source | Original Part 1 XCT STL, `428,004,884` bytes, SHA-256 `2108E1B17B2CCE59138C74E5DF4951D407F52A3649C257C3FE942DE874FACA00` |
| Nominal source | Original `9 x 5 x 5 mm` STL, `145,284` bytes, SHA-256 `D9FC086CA8C0BC3722709E5C03A39C5C1CF60553845FF62F5699780E1D3C1734` |
| Validation query | CloudCompare topology-de-duplicated ordered vertex PLY, `50,682,545` bytes, SHA-256 `447CDC6E7703DFDE98431F0A1BA154802FEA02E476F2FC7D06AA09F022874B50` |

The query PLY is a traceable validation derivative. It does not replace the original actual source identity. The original measured STL remains above the Viewer mesh limit; the Viewer renders an independent query sample while Tools and Runner inspect all query points.

## Result

The Viewer and Runner reproduce the same result:

```text
Status: Fail
Compared points: 4,223,524
Below -0.3 mm: 548,207
Within tolerance: 2,990,143
Above 0.3 mm: 685,174
Out of tolerance: 1,233,381
Direct signs: 4,145,609
Robustly recovered signs: 77,915
Signed min/max: -0.45432090759277344 / 1.2232200953324692 mm
Signed mean/std: 0.012413132880575592 / 0.28295754702281667 mm
Unsigned mean/std: 0.19204021594811641 / 0.20818168661083186 mm
Viewer/Runner: Matched
```

`Fail` is the expected inspection outcome for this tolerance. It is not an execution failure.

## MVVM And Ownership Evidence

- View: existing workbench zones expose separate actual, nominal, query, preview, published, selected-deviation, current-display-density, and next-Preview-density state plus explicit Preview and Publish commands.
- ViewModel: `NominalActualComparisonViewModel` owns workflow commands, selected-point state, applied/next display-density snapshots, tolerance classification, presentation, and stale-state clearing; `MainWindowViewModel` owns the active typed input, selected global render density, and published result state used by the Viewer.
- Model: Core owns stable identities, fingerprints, metrics, overlay, inspection-step, result-entity contracts, and display-sample provenance.
- Data: the ordered binary PLY reader and streaming STL reader are UI-independent.
- Tools: the typed recipe and full-query executor are independent of WPF, SharpGL, and render density.
- Runner: recipe dispatch uses the same executor and independently compares persisted Viewer contract evidence.
- View code-behind remains the file/CLI/OpenGL/event bridge. It does not own comparison policy or durable result state.

Publish creates `result.nominal-actual-surface-deviation` on a separate result layer. The actual, nominal, and validation-query source entities remain unchanged.

## Evidence

The initial end-to-end evidence is under `artifacts/nominal_actual_publish_20260714`; the selected-point evidence is under `artifacts/nominal_actual_selected_point_20260714`; the current/next density-state evidence is under `artifacts/nominal_actual_density_state_20260715`:

| Evidence | Path / result |
| --- | --- |
| Before Viewer | `viewer_before.png`, accepted |
| Published Viewer | `viewer_final.png`, accepted |
| Before Shell | `shell_before.png`, accepted |
| Published and matched Shell | `shell_final_matched.png`, accepted |
| Viewer contract | `viewer_final_contract.txt` |
| Saved typed recipe | `nist_nominal_actual.recipe.json` |
| Reopen parity | `recipe_reopen_parity.txt`, zero stable-line differences |
| Runner report | `nist_runner_report.txt`, `ViewerRunnerComparison|Matched` |
| Run Record | `nist_run_record.json`, schema `1.2`, `ViewerRunnerMatchState=Matched` |
| HTML / CSV | `nist_run_report.html`, `nist_run_report.csv` |
| Selected-point Viewer / contract | `after/viewer_selected_after.png`, `after/viewer_selected_after_contract.txt`, accepted and `selected=True` |
| Selected-point Shell | `after/shell_selected_after.png`, accepted; failed functional attempt preserved separately |
| Executor/recipe/result verification | `after/nominal_actual_golden_after.txt`, `27/27` |
| ViewModel verification | `after/nominal_actual_viewmodel_after.txt`, `65/65` |
| Fixed loading regression | `regression/matrix_smoke_summary_after.txt`, `128/128` |
| Binary-only Viewer host | `binary_host`, zero project references, manifest `13/13`, outputs `12/12`, Host API commands `3/3` |
| Viewer render-density independence | `artifacts/nominal_actual_render_density_20260714/after/nominal_actual_render_density_summary.txt`, Fast/Balanced/Detailed display `24,992` / `59,487` / `145,639`, one normalized measurement SHA-256 |
| Current/next density-state gate | `artifacts/nominal_actual_density_state_20260715`, Balanced current `59,487` / stride `71`, Detailed next pending, explicit Detailed current `145,639` / stride `29`, ViewModel `71`, fixed matrix `128/128`, BinaryHost passed |

The schema `1.2` Run Record uses the original actual STL as its primary source, the nominal source as the step reference, and the query derivative as the step measurement input. It carries 13 metrics and one signed color-map overlay.

## Repeatable Commands

The NIST inputs are intentionally ignored local evidence. Set the root before running the commands:

```powershell
$nist = 'artifacts\research_samples\nist_overhang_x4'
$out = 'artifacts\nominal_actual_publish_20260714'
$selected = 'artifacts\nominal_actual_selected_point_20260714\after'

dotnet build OpenVisionLab.ThreeDStudio.slnx -c Debug
dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --verify-nominal-actual-comparison --report "artifacts\nominal_actual_rejection_20260714\nominal_actual_rejection_matrix.txt"
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --verify-nominal-actual-viewmodel "$out\nominal_actual_viewmodel_verification_final.txt" --smoke-screenshot "$out\viewmodel_verification_final.png"
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot "$out\viewer_after_publish.png" --smoke-contracts "$out\viewer_after_publish_contract.txt" --smoke-nominal-actual "$nist\OverhangPartX4 Part1 Surface_cleaned.stl" "$nist\cloudcompare_deviation_20260714\measured_vertices_full.ply" "$nist\OverhangPart_9x5x5mm.STL" --smoke-publish-result --smoke-save-recipe "$out\nist_nominal_actual.recipe.json"
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot "$selected\viewer_selected_after.png" --smoke-contracts "$selected\viewer_selected_after_contract.txt" --smoke-nominal-actual "$nist\OverhangPartX4 Part1 Surface_cleaned.stl" "$nist\cloudcompare_deviation_20260714\measured_vertices_full.ply" "$nist\OverhangPart_9x5x5mm.STL" --smoke-pick nominal-actual --smoke-publish-result
dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --shell-smoke-screenshot "$selected\shell_selected_after.png" --smoke-nominal-actual "$nist\OverhangPartX4 Part1 Surface_cleaned.stl" "$nist\cloudcompare_deviation_20260714\measured_vertices_full.ply" "$nist\OverhangPart_9x5x5mm.STL" --smoke-pick nominal-actual --smoke-publish-result
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot "$out\viewer_after_reopen.png" --smoke-contracts "$out\viewer_after_reopen_contract.txt" --smoke-recipe "$out\nist_nominal_actual.recipe.json"
dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --recipe "$out\nist_nominal_actual.recipe.json" --report "$out\nist_runner_report.txt" --expect-status Fail --compare-contract "$out\viewer_after_publish_contract.txt" --viewer-screenshot "$out\viewer_after_publish.png" --run-record "$out\nist_run_record.json" --html-report "$out\nist_run_report.html" --csv-report "$out\nist_run_report.csv"
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\verify-nist-nominal-actual-render-density.ps1 -ArtifactDir artifacts\nominal_actual_render_density
```

## Viewer Render-Density Independence

The fixed NIST Viewer matrix runs explicit Preview and Publish once per Fast, Balanced, and Detailed mode. The modes cap nominal/actual display samples at `25,000`, `60,000`, and `150,000`, producing `24,992`, `59,487`, and `145,639` rendered signed samples with strides `169`, `71`, and `29`. Every run still evaluates all `4,223,524` query points and reproduces the same `Fail` status, tolerance counts, robust-sign counts, signed/unsigned statistics, execution fingerprint, 13 published metrics, and color-map overlay. After removing only index/calculation/total elapsed fields, all three measurement contracts have SHA-256 `2FD93EF942D12C621A76964EF681816EE831CD8DEA214EF0A201F602BA30D1C9`.

All three after screenshots passed the built-in pixel-quality check on the first attempt. Visual comparison with the three fresh before captures confirms that Fast is visibly sparser, Balanced preserves the previous `59,487`-sample baseline, Detailed is visibly denser, and the HUD, legend, scene, and inspection panels remain readable without overlap. This proves display-density independence for this fixed input and identity-frame executor only; it is not cross-sample, transform, calibration, or metrology proof.

## Current Versus Next Preview Density

The Viewer and Shell now retain the density name and display budget snapshotted by each Preview. After a Balanced result is completed and published, selecting Detailed changes only `Next Preview`; the current color map remains Balanced with `59,487` samples and stride `71`, the result remains Published, and the UI states `run Preview to apply`. The contract records `current=Balanced`, `next=Detailed`, `changePending=True`, and `explicitPreviewRequired=True`.

A separate explicit Detailed Preview changes current display sampling to `145,639` samples and stride `29`, then records `changePending=False`. Fast, Balanced, Detailed, and the pending transition retain normalized measurement/published-evidence SHA-256 `2FD93EF942D12C621A76964EF681816EE831CD8DEA214EF0A201F602BA30D1C9`. The extended `scripts/verify-nist-nominal-actual-render-density.ps1` reproduces this four-run gate.

## Selected-Point Provenance

The fixed Balanced Viewer pointer-ray smoke selected ordered query point `2,724,128` from the `59,487` rendered samples. The current contract records position `(6.270, 3.926, 4.603)`, closest nominal point `(6.270, 3.926, 5.000)`, signed deviation `-0.39734458923339844 mm`, unsigned distance `0.39734458923339844 mm`, nearest nominal triangle `725`, direct sign resolution, and `Below lower tolerance`. It also retains actual source `source.nist-overhang-x4-actual-part1` and query source `query.nist-overhang-x4-cloudcompare-vertices`.

This is display-sample selection, not a new measurement path: the full result still evaluates all `4,223,524` query points, and changing which rendered point is selected does not run Preview or mutate source/result geometry. The original actual STL and CloudCompare-derived ordered query PLY are separate evidence files; no one-to-one actual-STL vertex index is known, so the UI deliberately exposes the query index and stable source identities instead of inventing a source index.

The Viewer draws the selected query point, closest nominal point, and connecting evidence line. `NominalActualComparisonViewModel` owns selection state and clears it when Preview, input identity, or tolerance changes. Standalone Viewer and full Shell show the same summary in the internal HUD and host panes. A first Shell-only attempt that captured before applying the configured pick is preserved as `shell_selected_attempt1_missing_pick.png`; the corrected Shell smoke invokes the same Viewer pointer-ray selection and passes.

## Remaining Trust Gates

1. Obtain a second independently sourced measured/nominal pair with known transform truth before generalizing this result.
2. Prove a non-identity source transform or alignment recipe with independent expected output; do not infer success from RMSE alone.
3. The executor/recipe/result matrix now passes `27/27`, adding display-sample provenance to display-budget invariance, missing recipe/direct sources, empty unit/frame declarations, corrupt/truncated query PLY, same-file inputs, hash/byte-length mismatch, invalid direction, and invalid tolerance. Detecting a non-empty but semantically wrong unit/frame remains blocked until independently derived source metadata exists.
4. Keep the NIST source and derivative files ignored until redistribution approval, derivative policy, and practical CI sizing are resolved.
5. Do not claim metrology-grade accuracy until calibration provenance, uncertainty assumptions, and independent licensed-tool validation are available.
