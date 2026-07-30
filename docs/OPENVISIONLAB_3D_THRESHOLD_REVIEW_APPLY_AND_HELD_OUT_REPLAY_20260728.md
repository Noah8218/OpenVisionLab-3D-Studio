# OpenVisionLab 3D Threshold Review, Draft Apply, and Held-out Replay

Date: 2026-07-28

Status: Complete for backlog items `I-08` and `I-10`

## Completed scope

The deterministic candidate table now has one explicit correction workflow:

```text
selected development-only candidate
  -> Review
  -> Cancel
       -> recipe, PropertyGrid, and execution state unchanged
  or Apply to draft
       -> supported typed PropertyGrid draft only
       -> normal PropertyGrid Apply remains separate
       -> explicit Held-out replay becomes available
       -> projected recipe copy executes Held-out samples only
       -> recipe-side correction evidence can be saved and reopened
```

The first fail-closed mappings are:

- `Thickness / Mean / Minimum` -> `MinimumThickness`;
- `Thickness / Mean / Maximum` -> `MaximumThickness`;
- `Thickness / Mean / Range` -> both Thickness limits;
- `Warpage / PeakToValley / Maximum` -> `MaximumPeakToValley`;
- `Warpage / Rms / Maximum` -> `MaximumRms`.

There is no display-name guessing or fuzzy parameter matching. Region metrics
and unsupported tool/metric/rule combinations remain non-applicable.

## Ownership

- Core owns the typed proposal, before/proposed parameter changes, and
  Held-out correction-evidence contract.
- Tools owns explicit candidate-to-parameter mapping, immutable document
  projection, and correction-evidence construction.
- Data owns the atomic portable
  `<recipe>.threshold-correction.json` sidecar.
- Workbench owns Review/Cancel/Apply state, the PropertyGrid draft handoff,
  explicit replay, and save/reopen presentation.
- Runner owns the headless development-only candidate regeneration and
  Held-out-only projected replay.
- XAML owns only the visible command and evidence surfaces.

## Controlled evidence

The controlled Thickness set is:

- development Good: `2`, `4`;
- development Bad: `-10`, `20`;
- Held-out: `3`.

The selected candidate is
`threshold.0ad7b16eaa3d4362`, `Mean Range 2..4`.
Review presents:

```text
MinimumThickness: 0 -> 2
MaximumThickness: 10 -> 4
```

Candidate Apply leaves the committed recipe at `0..10` and changes only the
typed PropertyGrid draft to `2..4`. The separate normal PropertyGrid Apply
commits `2..4` without invoking Preview, Publish, Run All, or Held-out replay.

The explicit replay executes exactly one Held-out sample. Workbench and Runner
agree on:

- candidate ID `threshold.0ad7b16eaa3d4362`;
- the two exact parameter changes;
- `4` development samples;
- `1` Held-out sample;
- Held-out role `HeldOut`;
- Held-out status `Pass`;
- Held-out SHA-256
  `D9384A7B5A032D28E952E8742619EA224F2763FC5B5B3C431DC895544AA93C3B`.

## Acceptance criteria

| Criterion | Result |
| --- | --- |
| Explicit, fail-closed metric-to-typed-parameter mapping | Pass |
| Review shows exact before/proposed values | Pass |
| Cancel is non-mutating | Pass |
| Candidate Apply changes PropertyGrid draft only | Pass |
| Ordinary PropertyGrid Apply remains separate | Pass |
| Held-out replay is unavailable before candidate Apply | Pass |
| Replay runs only explicit Held-out samples | Pass |
| Held-out never enters candidate decisions | Pass |
| Correction evidence saves and reopens | Pass |
| Workbench and Runner agree | Pass |
| No automatic Preview, Publish, Run All, save, or replay | Pass |
| Fresh current-build Wide and Compact UI evidence | Pass |

## Verification

- Release solution build: `0` warnings, `0` errors.
- Validation Set focused verification: `58/58`.
- Inspection Workspace selection: `63/63`.
- Tool Recipe teaching: `28/28`.
- Recipe Manager / typed PropertyGrid: `37/37`.
- Artifact Navigator / Output Compare: `31/31`.
- Shell smoke command line: `24/24`.
- Code structure: `17/17`.
- Runner correction replay: `4` development, `1` Held-out, Held-out `Pass`.
- Current-build screenshot quality: Wide, tall evidence, and Compact pass on
  attempt `1`.
- `git diff --check`: pass.

## Evidence

- `artifacts/current/20260728-threshold-review-heldout/validation-set.txt`
- `artifacts/current/20260728-threshold-review-heldout/runner-threshold-correction.json`
- `artifacts/current/20260728-threshold-review-heldout/runner-console.txt`
- `artifacts/current/20260728-threshold-review-heldout/release-build.txt`
- `artifacts/current/20260728-threshold-review-heldout/before-review-apply.png`
- `artifacts/current/20260728-threshold-review-heldout/after-review-apply-heldout-tall.png`
- `artifacts/current/20260728-threshold-review-heldout/after-review-apply-heldout-compact.png`

## Boundary and next dependency

This closes `I-08` and `I-10`. It does not close `I-09` or `I-11`: the
controlled Held-out value already passed the original broad `0..10` recipe,
so this is not a genuine failed first draft corrected and replayed on
Held-out data. Do not describe it as failure-to-correction evidence.

The next bounded slice is `I-09/I-11`: preserve a real failed parameter draft,
perform an explicit manual correction through the ordinary PropertyGrid, and
replay separate Held-out data into a durable before/after failure-correction
record. `I-12/I-13/I-15` and `L-11` follow.

Physical calibration, traceability, uncertainty, GR&R, production tolerance,
and certified metrology remain external or unverified.

## Completion record

Status: Complete

Scope: `I-08` explicit candidate Review/Cancel/draft Apply and `I-10`
explicit Held-out-only projected replay with Workbench/Runner parity.

Acceptance criteria: all criteria in this document pass.

Verification: Release build `0/0`; focused Validation Set `58/58`; related
Workbench, PropertyGrid, Shell, navigation, structure, Runner, and current UI
evidence pass.

Evidence:
`artifacts/current/20260728-threshold-review-heldout/`.

Boundary / next dependency: a genuine failed draft and manual correction are
still required for `I-09/I-11`; R0 owner replay and physical metrology remain
external.
