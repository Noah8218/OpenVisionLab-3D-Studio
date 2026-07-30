# OpenVisionLab 3D Threshold Manual Correction and Failure Record

Date: 2026-07-28

Status: Complete for backlog items `I-09` and `I-11`

## Completed scope

The threshold assistant now preserves one genuine development-set mismatch,
one operator-authored manual correction, corrected development evidence, and
one separate Held-out replay in a durable shared contract.

```text
explicit development Run
  -> committed 0..20 Thickness limits
  -> Bad-high Mean 20 incorrectly passes
  -> deterministic candidate Review
  -> candidate 2..4 applied to typed draft only
  -> operator changes draft to 1.5..4.5
  -> ordinary PropertyGrid Apply
  -> explicit Good/Bad development replay
  -> mismatch 1 -> 0
  -> explicit Held-out-only replay
  -> portable before/suggested/manual/after/Held-out evidence
```

This extends the completed `I-08/I-10` workflow. Legacy candidate-only
correction evidence remains readable and keeps its previous behavior.

## Ownership

- Core owns typed manual parameter changes and before/after development sample
  evidence.
- Tools owns expected-role mismatch calculation and correction-evidence
  construction.
- Data owns validation, portable path rewriting, backward-compatible load, and
  atomic sidecar save.
- Workbench owns Review, draft Apply, ordinary PropertyGrid handoff, explicit
  development revalidation, Held-out lock/unlock, and evidence presentation.
- PropertyGrid remains the only ordinary typed manual-edit and recipe Apply
  surface.
- XAML owns the visible `Revalidate development` action, manual-value column,
  and before/after evidence table.
- Runner independently regenerates the candidate, applies explicit manual
  values, replays development, and then replays Held-out.

The View does not calculate candidates, write recipe JSON, or execute
inspection.

## Controlled failure and correction evidence

The recipe source identity is:

```text
2D8037AA33F959B0461DB15B7ADA410829F6DF4FD1FA2120C691924837DF3E31
```

The committed pre-correction parameters are:

```text
MinimumThickness = 0
MaximumThickness = 20
```

The development set is:

| Order | Role | Mean | Before status | Expected match | SHA-256 |
| ---: | --- | ---: | --- | --- | --- |
| 1 | Good | 2 | Pass | Match | `183EF644DB3D1695A450271AFD217412B6DA8F1CDE1725A04E5909EE9057C09D` |
| 2 | Good | 4 | Pass | Match | `08B6633FB6CB28490B5D07B6B81C15BE3C7B8150766BEA91E8E5A680CF3693FE` |
| 3 | Bad | -10 | Fail | Match | `39A5D0ACC2F23FC7E4AA9C3B6DD2D56809455C8B2E04411ABADC91B5E08E4D8E` |
| 4 | Bad | 20 | Pass | **Mismatch** | `6E00A03C6A901DFC39EBE41E7E14E3EC1FE8A3F4FBFBFECE9C1E8A5E6DCE9AD9` |

The Bad-high result is an actual typed Thickness execution under the committed
inclusive maximum. It is not a fabricated status or a role changed after
observing the result.

The selected development-only candidate is:

```text
candidate = threshold.0ad7b16eaa3d4362
tool = thickness
metric = Mean
rule = Range
suggested = 2..4
```

The operator-authored committed values are:

```text
MinimumThickness: before 0 -> suggested 2 -> manual 1.5
MaximumThickness: before 20 -> suggested 4 -> manual 4.5
```

The manual values differ from the deterministic suggestion while preserving
the two Good observations and rejecting both Bad observations. Explicit
development replay produces:

| Order | Role | Mean | After status | Expected match |
| ---: | --- | ---: | --- | --- |
| 1 | Good | 2 | Pass | Match |
| 2 | Good | 4 | Pass | Match |
| 3 | Bad | -10 | Fail | Match |
| 4 | Bad | 20 | Fail | Match |

Mismatch count changes from `1` to `0`.

## Held-out separation

Held-out replay is disabled after manual PropertyGrid Apply. It becomes
available only after the explicit corrected development replay has zero
expected-role mismatches.

The Held-out sample is:

| Role | Mean | Status | SHA-256 |
| --- | ---: | --- | --- |
| HeldOut | 3 | Pass | `D9384A7B5A032D28E952E8742619EA224F2763FC5B5B3C431DC895544AA93C3B` |

Held-out never enters candidate boundaries, ranking, confusion counts,
warnings, manual-value selection, or the corrected development replay.

## Durable contract

`<recipe>.threshold-correction.json` retains parent contract version `1.0`
and adds an optional typed manual-correction version `1.0` extension:

- exact before parameters in the original proposal;
- exact deterministic suggested parameters;
- exact manually committed parameters;
- ordered before-development sample paths, SHA identities, roles, statuses,
  expected-match flags, metrics, and messages;
- ordered corrected-development evidence for the same identities and roles;
- before and after mismatch counts;
- separate Held-out evidence.

The Data store validates:

- at least one real before mismatch;
- zero after mismatches;
- at least one manual value different from the suggestion;
- identical ordered before/after development identities and roles;
- no Held-out role in either development collection;
- at least one separate Held-out result.

Existing I-08 evidence without `manualCorrection` remains valid.

## Explicit-action and no-leakage evidence

The focused Workbench verification proves:

- Review does not edit or execute;
- candidate Apply changes the typed draft only;
- manual draft editing does not edit the recipe until ordinary PropertyGrid
  Apply;
- PropertyGrid Apply does not invoke Preview, Publish, Run All, save,
  development replay, or Held-out replay;
- manual correction locks Held-out;
- development revalidation is a separate explicit command and uses Good/Bad
  only;
- development revalidation does not run Held-out;
- Held-out replay is a separate explicit command and uses HeldOut only;
- `ValidationSetSummary` remains unchanged through Review, manual Apply, and
  corrected development replay;
- save/reopen restores evidence with Pending samples and does not execute.

## Workbench and Runner parity

Production Runner accepts:

```text
--threshold-manual-values "MinimumThickness=1.5;MaximumThickness=4.5"
```

Runner report schema `2.0` agrees exactly with the Workbench sidecar on:

- candidate ID;
- before mismatch count `1`;
- after mismatch count `0`;
- both manual parameter changes;
- all four ordered before sample identities, roles, statuses, and
  expected-match flags;
- all four ordered corrected sample identities, roles, statuses, and
  expected-match flags;
- Held-out identity and `Pass` status.

## Acceptance criteria

| Criterion | Result |
| --- | --- |
| Committed draft produces a real expected-role mismatch | Pass |
| Exact before parameters, sample SHA, metric, status, and mismatch preserved | Pass |
| Candidate Review/draft Apply reuse I-08 without execution | Pass |
| Operator changes typed suggested values | Pass |
| Ordinary PropertyGrid Apply remains explicit | Pass |
| Suggested and manually committed values remain distinguishable | Pass |
| Corrected development replay is explicit and Good/Bad only | Pass |
| Held-out remains locked until corrected development passes | Pass |
| Held-out replay is explicit and HeldOut only | Pass |
| Before, suggestion, manual, after, and Held-out evidence is durable | Pass |
| Legacy I-08 correction evidence remains readable | Pass |
| Workbench and Runner agree | Pass |
| No automatic Preview, Publish, Run, save, Apply, or replay | Pass |
| Fresh before/after Wide and Compact UI evidence | Pass |

## Verification

Commands actually run:

```powershell
dotnet build "OpenVisionLab.ThreeDStudio.sln" -c Release -p:Platform="Any CPU"

OpenVisionLab.ThreeD.Shell.exe `
  --verify-validation-set `
  artifacts/current/20260728-threshold-manual-correction/validation-set.txt

OpenVisionLab.ThreeD.Runner.exe `
  --threshold-correction-recipe <controlled-recipe> `
  --threshold-candidate-id threshold.0ad7b16eaa3d4362 `
  --threshold-manual-values "MinimumThickness=1.5;MaximumThickness=4.5" `
  --report artifacts/current/20260728-threshold-manual-correction/runner-manual-correction.json
```

Results:

- Release build: `0` warnings, `0` errors.
- Validation Set focused verification: `66/66`.
- Inspection Workspace selection: `63/63`.
- Recipe Manager / typed PropertyGrid: `37/37`.
- Shell smoke command-line options: `24/24`.
- Code structure: `17/17`.
- Runner manual correction: before mismatch `1`, after mismatch `0`,
  Held-out `1/1 Pass`.
- Workbench/Runner relevant-field parity: all equal.
- Wide and Compact current-Release screenshot quality: accepted on attempt
  `1`.
- `git diff --check`: pass.

## UI evidence

- `before-manual-correction.png` is the fresh current-Release pre-change I-08
  baseline captured before implementation. It shows the prior
  before/proposed-only command surface.
- `after-manual-correction-wide.png` shows the new explicit development
  revalidation command and the exact summary:
  `Before 0..20 | Suggested 2..4 | Manual 1.5..4.5 | mismatch 1->0 |
  Held-out Pass 1/1`.
- `after-manual-correction-compact.png` proves the same command and summary
  remain visible in the compact layout.

All three captures contain only the application window.

## Evidence

- `artifacts/current/20260728-threshold-manual-correction/release-build.txt`
- `artifacts/current/20260728-threshold-manual-correction/validation-set.txt`
- `artifacts/current/20260728-threshold-manual-correction/runner-manual-correction.json`
- `artifacts/current/20260728-threshold-manual-correction/runner-console.txt`
- `artifacts/current/20260728-threshold-manual-correction/runner-parity.txt`
- `artifacts/current/20260728-threshold-manual-correction/before-manual-correction.png`
- `artifacts/current/20260728-threshold-manual-correction/after-manual-correction-wide.png`
- `artifacts/current/20260728-threshold-manual-correction/after-manual-correction-compact.png`

## Boundary and next dependency

This closes `I-09` and `I-11` for a controlled deterministic software
workflow. It does not claim a GPT transcript, arbitrary-tool threshold
coverage, production tolerance approval, physical calibration, traceability,
uncertainty, GR&R, or certified metrology.

The next bounded slice is:

1. `I-12/I-13/I-15 evidence warnings, Thickness/Warpage first-tool coverage,
   and no-auto-run guards` | Recommended model: `gpt-5.6-sol` | Reasoning
   effort: high.
2. `L-11 threshold-correction evidence in Run Record` | Recommended model:
   `gpt-5.6-sol` | Reasoning effort: medium.

R0 owner unaided replay remains an external acceptance gate.

## Completion record

Status: Complete

Scope: `I-09` manual parameter correction after suggestion and `I-11`
failure -> correction -> separate Held-out durable evidence, with legacy
compatibility and Workbench/Runner parity.

Acceptance criteria: every criterion in this document passes.

Verification: Release build `0/0`; focused Validation Set `66/66`; related
Workspace, PropertyGrid, Shell, structure, Runner, and current UI checks pass.

Evidence:
`artifacts/current/20260728-threshold-manual-correction/`.

Boundary / next dependency: deterministic controlled software workflow only;
`I-12/I-13/I-15` is next. R0 and physical metrology remain external or
unverified.
