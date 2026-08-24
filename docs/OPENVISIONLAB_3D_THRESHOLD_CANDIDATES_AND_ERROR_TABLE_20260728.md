# Threshold Candidates and Exact Error Table I-06/I-07 Closure

Date: 2026-07-28

Status: Complete for the documented software scope

> Follow-up qualification (2026-08-23): `PL-0043/M-14` extends the current
> existing Validation Set verifier with one counterfactual Held-out fixture.
> With the same four development samples, changing only Held-out value and
> identity must leave the complete candidate, limit, ranking, warning,
> confusion, and development-decision fingerprint unchanged. The original
> implementation record below remains historical evidence for its scope.

## Outcome

Validation Set now converts explicit-run Good/Bad metric observations into a
deterministic, review-only threshold candidate set and an exact
sample-decision table.

This closes:

- `I-06`: candidate generation for a minimum, maximum, or two-sided range;
- `I-07`: confusion/error counts backed by exact sample order, source path,
  content identity, expected role, predicted role, decision, and value.

It does not edit parameters or execute a Held-out replay. `I-08/I-10`
remains the next eligible slice.

## User workflow

1. Stage and label Validation Set samples.
2. Select explicit `Run all`.
3. Expand `Threshold candidates and error table`.
4. Select a candidate.
5. Review its rule, limit values, correct/error counts, false accepts, false
   rejects, and every supporting development sample.

The panel states `Review only · recipe unchanged`. It is collapsed by
default so normal sample and issue review remains primary.

## Candidate policy

For each step or source-region metric having at least one finite Good and one
finite Bad observation, Tools evaluates:

- `Minimum`: predicted Good when `value >= minimum`;
- `Maximum`: predicted Good when `value <= maximum`;
- `Range`: predicted Good when
  `minimum <= value <= maximum`.

Candidate boundaries come only from finite development observations.
Exactly one deterministic candidate per rule kind is retained.

Ranking is:

1. fewest total errors;
2. fewest false accepts;
3. fewest false rejects;
4. the tighter deterministic boundary for equivalent decisions.

The false-accept tie break is intentionally fail-safe and visible. It is not
an automatic production policy because Apply remains outside this slice.

Candidates across metrics are ordered by the same error ranking and then by
stable scope, owner, metric, and rule identities. Candidate IDs are SHA-256
derived from the metric identity, rule kind, and exact limits.

## Error-table semantics

Good means the sample is expected to be accepted. Bad means it is expected
to be rejected.

| Expected | Predicted | Decision |
| --- | --- | --- |
| Good | Good | `CorrectGood` |
| Good | Bad | `FalseReject` |
| Bad | Bad | `CorrectBad` |
| Bad | Good | `FalseAccept` |

The candidate aggregate is reproduced directly from those decisions:

- Good accepted;
- Good rejected;
- Bad rejected;
- Bad accepted;
- correct count;
- error count.

## Held-out no-leakage boundary

Held-out observations never enter:

- candidate boundary generation;
- ranking;
- confusion counts;
- sample decisions.

The report separately records every excluded Held-out sample identity and the
number of Held-out observations, making the exclusion auditable. Held-out
evaluation remains reserved for `I-10` after an explicit Apply.

## Ownership

### Core

`ToolRecipeThresholdCandidateContracts.cs` owns:

- finite metric observations;
- limit and decision enums;
- candidate identity and limits;
- exact sample decisions;
- confusion counts;
- report-level Held-out exclusion evidence.

### Tools

`ToolRecipeLabeledEvidenceAnalyzer.CollectObservations` is the shared
observation owner.

`ToolRecipeThresholdCandidateAnalyzer` owns deterministic candidate
enumeration, evaluation, ranking, IDs, and Held-out exclusion.

### Workbench

- candidate and decision tables are read-only;
- selecting a candidate is presentation-only;
- candidate selection does not dirty the recipe or sidecar;
- no Preview, Publish, Run All, Validation Set, or Save action is invoked;
- full sample identities are available in the table and as cell tooltips.

### Runner

The existing labeled-validation command now adds
`thresholdCandidates` to the JSON report:

```powershell
dotnet .\src\OpenVisionLab.ThreeD.Runner\bin\Release\net10.0\OpenVisionLab.ThreeD.Runner.dll `
  --labeled-validation-recipe <recipe.ov3d-recipe.json> `
  --report <evidence.json>
```

Workbench and Runner call the same Tools analyzer.

## Controlled fixture

The `4 x 4` fixture uses one dual-ROI Thickness step:

| Role | Signed mean |
| --- | ---: |
| Good | `2` |
| Good | `4` |
| Bad | `-10` |
| Bad | `20` |
| Held-out | `3` |

The selected two-limit `Mean` candidate is `2 .. 4`:

- Good accepted: `2`;
- Good rejected: `0`;
- Bad rejected: `2`;
- Bad accepted: `0`;
- correct: `4`;
- errors: `0`.

The best one-limit minimum and maximum candidates each expose one false
accept because Bad evidence exists on both sides. This proves the error table
is not hidden by a convenient one-sided fixture.

## Verification

- Release solution build: `0` warnings, `0` errors.
- Focused Validation Set: `45/45`.
- Inspection Workspace selection and typed-region regression: `63/63`.
- Shell smoke command-line contract: `24/24`.
- Recipe teaching regression: `28/28`.
- Artifact Navigator/Output Compare regression: `31/31`.
- Executable code-structure guard: `17/17`.
- Candidate report:
  - `48` deterministic candidates;
  - `4` development samples;
  - `1` Held-out sample excluded;
  - exact `Mean Range 2..4`;
  - `4` exact supporting decisions;
  - no Held-out decision.
- Full Workbench graph:
  - `528` review-only candidates;
  - selected-candidate decisions reproduce the two development samples;
  - candidate selection preserves source, recipe dirty state, and execution
    summary.
- Runner:
  - candidate count `48`;
  - development/Held-out `4/1`;
  - `Mean Range 2..4`;
  - errors `0`;
  - decisions `4`.
- Current-source default wide, expanded evidence, and compact captures pass
  screenshot quality on attempt 1.

Evidence:

- `artifacts/current/20260728-threshold-candidates/validation-set.txt`
- `artifacts/current/20260728-threshold-candidates/runner-threshold-candidates.json`
- `artifacts/current/20260728-threshold-candidates/runner-summary.txt`
- `artifacts/current/20260728-threshold-candidates/before-threshold-assistant.png`
- `artifacts/current/20260728-threshold-candidates/after-default-wide.png`
- `artifacts/current/20260728-threshold-candidates/after-threshold-evidence.png`
- `artifacts/current/20260728-threshold-candidates/after-compact.png`

The before capture is the immediately preceding current-build labeled
Validation Set UI from the completed I-04/I-05 checkpoint.

## Boundaries

- No candidate is automatically selected as a recipe edit.
- There is no Review/Cancel/Apply state machine yet.
- No candidate is mapped to a particular tool parameter yet.
- No Held-out replay occurs.
- Balance, overlap, and insufficient-evidence guidance remains `I-12`.
- Physical calibration, uncertainty, GR&R, and certified metrology remain
  external or unverified.
- R0 owner unaided replay remains external.

## Completion record

Status: Complete

Scope: deterministic one-limit and two-limit threshold candidates plus an
exact development-sample confusion/error table in Workbench and Runner.

Acceptance criteria: development-only candidate inputs -> pass; minimum,
maximum, and range candidates -> pass; deterministic IDs/order -> pass;
counts reproduce from exact decisions -> pass; Held-out exclusion -> pass;
Workbench/Runner shared result -> pass; candidate selection non-mutation ->
pass.

Verification: Release build `0/0`; Validation Set `45/45`; Inspection
Workspace `63/63`; Shell smoke options `24/24`; recipe teaching `28/28`;
Artifact Navigator/Output Compare `31/31`; code structure `17/17`; Runner
`48` candidates with development/Held-out `4/1`, `Mean Range 2..4`, `0`
errors, `4` exact decisions, and `0` Held-out decisions; current UI capture
quality accepted on attempt 1.

Evidence: files under
`artifacts/current/20260728-threshold-candidates/`.

Boundary / next dependency: `I-08/I-10` must add an explicit
Review/Cancel/Apply state machine, map a selected candidate to a supported
tool parameter draft, and run Held-out samples only after Apply.
