# Repeatability statistics Library-Noah migration

Date: 2026-08-01

## Outcome

`AlignedPointRepeatabilityRule` and `ThicknessRepeatabilityRule` are strict
Studio adapters over the committed public Library-Noah
`RepeatabilityStatisticsTool`. Studio no longer owns Welford accumulation,
sample-variance, square-root, six-sigma, or scalar-range calculation.

The operator workflow and product decisions are unchanged. Studio continues
to own study, run, source, correspondence, unit, frame, alignment, acceptance,
metric, message, and result identity. Preview, Publish, Run, recipe, source,
and Viewer state remain explicit and are not executed or mutated by this
migration.

## Included and excluded scope

Included:

- finite scalar mean, minimum, maximum, sample standard deviation, six-sigma
  spread, and range;
- an explicit source-neutral negative-variance round-off policy preserving the
  two established Studio contracts;
- strict adapters for one whole-study Thickness series and per-correspondence
  Aligned Point series;
- package provenance, direct bridge verification, exact golden parity,
  study-loader regression, Calibration ViewModel regression, and decreasing
  ownership ledger.

Excluded:

- Gauge R&R, reproducibility, bias, linearity, stability, uncertainty,
  traceability, calibration, metrology, or production tolerance claims;
- threshold-candidate and labeled-evidence statistics, which remain the final
  two migration-debt files;
- UI, theme, layout, visible text, navigation, and responsive behavior.

## Ownership boundary

| Concern | Library-Noah | Studio |
| --- | --- | --- |
| Scalar Welford accumulation | `RepeatabilityStatisticsTool` | Converts validated values in established order |
| Mean, extrema, sample standard deviation, 6σ, range | `RepeatabilityStatisticsTool` | Maps the typed result into product evidence |
| Negative-variance round-off contract | Explicit Tool option | Selects the existing rule-specific option |
| Study/run/source identity | None | Validates and retains it |
| Unit, frame, alignment, correspondence coverage | None | Validates and retains it |
| Limits and Pass/Fail decision | None | Authored acceptance and result composition |
| Gauge R&R or physical claims | None | Explicitly disclaimed |

Aligned Point retains only its product-level maximum across already calculated
per-correspondence results. That aggregation drives Studio metrics and
acceptance; it is not a second implementation of scalar repeatability
statistics.

## Exact package provenance

| Item | Value |
| --- | --- |
| Noah worktree | `C:\Git\Library-Noah-surface-match-kernel` |
| Source commit | `20963c12b50dfc0658110e2037961d3224feb2d6` |
| Package | `Lib.ThreeD 2.8.7` |
| SHA-256 | `C40A2EB0239C5BF6063984429CEDB580608CD7EF8C96D08AA13A67C2B3ACF33B` |
| Vendored path | `third_party/LibraryNoah/Lib.ThreeD.2.8.7.nupkg` |
| Target | `netstandard2.0` |

The exact committed Noah source passes Release build and full Smoke before the
package is packed. Studio verifies the package ID, version, repository commit,
hash, license entries, and target assembly.

## Observable compatibility

- Thickness Repeatability: baseline/current `34/34`; full report differences
  `0`.
- Aligned Point Repeatability: baseline/current `33/33`; full report
  differences `0`.
- Thickness Study Loader: `13/13`.
- Aligned Point Study Loader: `20/20`.
- Calibration Center ViewModel: `75/75` after fixing its verification-only
  language-service initialization order.

These checks prove deterministic software compatibility for the fixed
fixtures. They do not prove a physical repeatability study or Gauge R&R.

## Verification

| Check | Result |
| --- | --- |
| Noah Release build | Pass: `0` warnings, `0` errors |
| Noah full Smoke | Pass: `101/101`, including three Tool cases |
| Package integrity and provenance | Pass |
| Studio Release build | Pass: `0` warnings, `0` errors |
| Direct Library-Noah bridge | Pass: `17/17` |
| Thickness Repeatability golden | Pass: `34/34` |
| Aligned Point Repeatability golden | Pass: `33/33` |
| Full report parity | Pass: `0` differences in both reports |
| Thickness/Aligned study loaders | Pass: `13/13`, `20/20` |
| Calibration Center ViewModel | Pass: `75/75` |
| Structure and decreasing ledger | Pass: `28/28`; `2` debt / `28` boundaries |
| NuGet vulnerable/deprecated audit | Pass: no findings |
| Refreshed fixed R0 package | Pass: Wide and Compact `-ValidateOnly` |

No UI-affecting file changed, so no before/after UI screenshot is required or
claimed. Human-owner unaided Wide/Compact R0 remains external.

## Evidence

Logical path:

`artifacts/current/20260801-noah-repeatability-statistics-migration/`

Physical path:

`D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260801-noah-repeatability-statistics-migration`

## Completion record

Status: Complete

Scope: two Studio repeatability rules migrated to one committed public Noah
Tool with exact package provenance, strict product adapters, exact focused
parity, study/UI-model regressions, decreasing ledger, and refreshed fixed R0
inputs.

Acceptance criteria: committed Tool source -> Pass; exact vendored package ->
Pass; duplicate Studio scalar-statistics formulas removed -> Pass; established
variance policies preserved -> Pass; full golden reports unchanged -> Pass;
focused and integration regressions -> Pass; ledger decreased -> Pass; fixed
R0 validation -> Pass.

Verification: Noah `0/0`, `101/101`; Studio `0/0`; bridge `17/17`; golden
`34/34`, `33/33`; report parity `0/0` differences; loaders `13/13`, `20/20`;
Calibration ViewModel `75/75`; structure `28/28`; both fixed `-ValidateOnly`
checks.

Evidence:
`artifacts/current/20260801-noah-repeatability-statistics-migration/`.

Boundary / next dependency: the final two migration-debt files are
`ToolRecipeLabeledEvidenceAnalyzer` and
`ToolRecipeThresholdCandidateAnalyzer`. Migrate them to committed Noah Tools
before beginning `J-12 Multiple-match result collection`. Human-owner R0
remains external.
