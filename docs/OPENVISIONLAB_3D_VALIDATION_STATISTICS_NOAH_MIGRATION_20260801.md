# OpenVisionLab 3D Validation Statistics Library-Noah Migration

Date: 2026-08-01

Status: Complete

## Outcome

The final two numerical owners in the Studio migration ledger are now strict
adapters over committed, public Library-Noah Tools:

- `ToolRecipeLabeledEvidenceAnalyzer` uses
  `LabeledEvidenceStatisticsTool` for role-grouped count, extrema, mean, and
  population-standard-deviation evidence;
- `ToolRecipeThresholdCandidateAnalyzer` uses
  `ThresholdCandidateAnalysisTool` for deterministic candidate construction,
  classification, error counting, ranking, and tie-breaking;
- C3D rectangular-ROI mean and coverage in the labeled-evidence adapter use the
  existing `HeightMapRegionStatisticsTool` rather than Studio arithmetic.

Studio still owns recipe, Tool, parameter, source, sample, role, development,
and HeldOut identity; input grouping and eligibility; warning and report text;
canonical candidate IDs; evidence locators; explicit lifecycle routing; and UI.
Neither Tool knows Studio recipe JSON, paths, WPF, Preview, Publish, Run, or
Validation state.

## Immutable Library-Noah input

| Item | Value |
| --- | --- |
| Source worktree | `C:\Git\Library-Noah-surface-match-kernel` |
| Source branch | `codex/surface-match-kernel` |
| Source commit | `0fe04bc967fa89918b3c6d937566cce56de69682` |
| Package | `Lib.ThreeD 2.8.8` |
| Target | `netstandard2.0` |
| Vendored path | `third_party/LibraryNoah/Lib.ThreeD.2.8.8.nupkg` |
| SHA-256 | `D62B050710C4CCA0309B3FA49CDCDBB239C675944E29C085E50CD198D4D15405` |

The package was built and packed only after the exact Noah source had been
committed. The packed, vendored, and verified package identities agree. No
cross-repository `ProjectReference` was introduced.

## Adapter-parity finding

The first Studio adapter revision mapped C3D `width` and `height` directly to
the Noah region Tool's `row` and `column` arguments. The focused Validation Set
still executed, but the full before/after report exposed changed distributions
and threshold candidates. The adapter was corrected to pass
`(source.Height, source.Width)`. The final normalized full-report comparison has
zero differences.

This is retained as migration evidence because it demonstrates that the report
parity gate checked observable behavior rather than only compilation or API
shape.

## Verification

- Library-Noah Release build: `0` warnings, `0` errors.
- Library-Noah full Smoke: `106/106` passed.
- Package ID/version/source-commit/SHA/target integrity: passed.
- Studio Release build: `0` warnings, `0` errors.
- Direct Library-Noah bridge: `19/19` passed.
- Validation Set: `84/84` passed before and after migration.
- Normalized full Validation Set report: `0` differing lines.
- Structure guard: `29/29` passed; `0` migration-debt files and `30`
  reviewed Studio boundaries; no unclassified or expanded numerical owner.
- Fixed Wide and Compact R0 packages: both `-ValidateOnly` modes passed after
  the changed binary hashes were refreshed.
- NuGet health: all `12` Studio projects reported no vulnerable or deprecated
  package.

The change does not affect UI, UX, layout, visible text, navigation, docking,
or responsive behavior. Therefore the UI before/after capture gate does not
apply to this slice; Wide/Compact binary validation was still rerun because the
R0 binary set changed.

## Evidence

Current-task reports and exact package artifacts are stored physically under:

`D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260801-noah-validation-statistics-migration\`

Important files include:

- `before\validation-set.txt`;
- `after\validation-set.txt`;
- `after\validation-set-parity.txt`;
- `after\noah-release-build.txt` and `after\noah-smoke.txt`;
- `after\noah-package-provenance.txt` and `after\package-integrity.txt`;
- `after\studio-release-build.txt`;
- `after\library-noah-bridge.txt`;
- `after\code-structure.txt`;
- `after\r0-wide-validate-only.txt` and
  `after\r0-compact-validate-only.txt`;
- `after\nuget-vulnerable.txt` and `after\nuget-deprecated.txt`.

## Completion record

Status: Complete

Scope: Migrate labeled-evidence descriptive statistics, C3D ROI statistics,
and threshold-candidate analysis from Studio arithmetic to committed public
Library-Noah Tools while preserving Studio identity, routing, reporting, and
explicit-action contracts.

Acceptance criteria: Public sealed source-neutral Tools and controlled typed
results exist; Studio analyzers are strict adapters; the exact committed Noah
package is vendored and verified; the migration ledger reaches zero without a
signal-ceiling increase; observable Validation reports remain exact; changed
R0 binaries pass both fixed-package validation modes.

Verification: Noah Release `0/0` and Smoke `106/106`; package integrity pass;
Studio Release `0/0`; bridge `19/19`; Validation Set `84/84`; normalized
before/after report difference `0`; structure `29/29` with `0` debt and `30`
reviewed boundaries; Wide/Compact `-ValidateOnly` pass; NuGet health clean for
all `12` projects.

Evidence: This document and
`D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260801-noah-validation-statistics-migration\`.

Boundary / next dependency: This closes the inventoried Studio numerical debt,
but it does not prove physical calibration, metrology, production readiness,
or human usability. `J-12 Multiple-match result collection` is the next
dependency-ready software slice and must add any new matching arithmetic to
committed Library-Noah first. Human-owner Wide/Compact R0 remains an external
acceptance prerequisite for `A-01`.
