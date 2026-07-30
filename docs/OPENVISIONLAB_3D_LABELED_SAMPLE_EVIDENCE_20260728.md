# Labeled Sample Evidence I-04/I-05 Closure

Date: 2026-07-28

Status: Complete for the documented software scope

## Outcome

Validation Set now supports an explicit `Good`, `Bad`, or `HeldOut` role for
each staged C3D sample and produces reproducible per-step and per-region
metric distributions from an explicit Run.

This closes:

- `I-04`: assign and persist Good, Bad, and Held-out sample roles;
- `I-05`: calculate per-step and per-region metric distributions over those
  labeled samples.

It does not generate or apply thresholds. `I-06/I-07` remains the next
eligible slice.

## User workflow

1. Open a saved recipe.
2. Open `Pipeline / Review -> Validation Set`.
3. Add the current input or one or more C3D samples.
4. Select a row and assign `Good`, `Bad`, or `Held-out`.
5. Save the recipe. The role manifest is stored beside it.
6. Select `Run all`.
7. Review sample Pass/Fail/Error evidence and expand `Labeled sample
   distributions` when statistical evidence is needed.
8. Use the same saved recipe in Runner to produce a JSON evidence report.

The distribution section is collapsed by default so sample selection,
role assignment, status filtering, issue navigation, and comparison retain
priority in the normal bottom-pane height.

## Contract and ownership

### Core

`ToolRecipeValidationSetDefinition` schema `1.0` owns:

- recipe name and source SHA-256;
- stable sample order and path;
- exactly one role per sample;
- role values `Good`, `Bad`, and `HeldOut`.

`ToolRecipeLabeledEvidenceReport` contract `1.0` owns:

- role sample counts;
- step-metric and region-metric distributions;
- minimum, maximum, mean, and population standard deviation;
- the explicit `IncludedInDevelopment` flag.

### Data

`ToolRecipeValidationSetDefinitionStore` writes:

```text
<recipe-path>.validation-set.json
```

Paths are portable relative paths when saved and absolute paths when loaded.
The write is atomic. The manifest never changes C3D bytes or the authored
recipe graph.

### Tools

`ToolRecipeValidationSetExecution` preserves role and sample order while
using the existing ordered typed graph executor.

`ToolRecipeLabeledEvidenceAnalyzer` calculates:

- every finite metric emitted by each executed recipe step;
- `Mean raw height` for each routed source-bound `GridRectangle`;
- `Valid cell ratio` for each routed source-bound `GridRectangle`.

Good and Bad statistics have `IncludedInDevelopment=true`. Held-out
statistics remain visible but always have `IncludedInDevelopment=false`.

### Workbench

- role edits invalidate prior distribution evidence but never run Preview,
  Publish, Run All, or Validation Set automatically;
- role edits mark only the sidecar manifest dirty, not the recipe graph;
- the normal save-state indicator and close guard still report the unsaved
  sidecar change;
- save/reopen restores roles as `Pending` and does not restore stale
  execution evidence;
- missing Good, Bad, or Held-out roles produce explicit warnings;
- sample and step evidence remains the primary view; distributions are a
  separate collapsible review section.

### Runner

The production command is:

```powershell
dotnet .\src\OpenVisionLab.ThreeD.Runner\bin\Release\net10.0\OpenVisionLab.ThreeD.Runner.dll `
  --labeled-validation-recipe <recipe.ov3d-recipe.json> `
  --report <labeled-evidence.json>
```

Runner loads the same sidecar, rejects a recipe-source identity mismatch, and
uses the same execution and analyzer contracts as Workbench. A normal
Good/Bad set may produce an aggregate inspection `Fail`; Runner returns
success when replay completed without an execution `Error`.

## Acceptance evidence

- Release solution build: `0` warnings, `0` errors.
- Validation Set focused verification: `35/35`.
- Direct labeled fixture:
  - roles: `Good 1 / Bad 1 / Held-out 1`;
  - distributions: `16`;
  - both routed ROIs expose raw-height evidence;
  - Held-out values are present and excluded from development.
- Full synthetic ordered graph:
  - samples: `Pass / Fail / Error`;
  - role counts: `1 / 1 / 1`;
  - Workbench distributions: `176`;
  - role save/reopen restores `Pending` rows without execution.
- Runner:
  - exit code `0` for the completed Good/Bad/Held-out fixture;
  - counts `1 / 1 / 1`;
  - distributions `16`;
  - Held-out `IncludedInDevelopment=false`.
- Current-source wide and compact WPF captures pass screenshot quality on
  attempt 1.

Evidence:

- `artifacts/current/20260728-labeled-sample-evidence/validation-set.txt`
- `artifacts/current/20260728-labeled-sample-evidence/runner-labeled-evidence.json`
- `artifacts/current/20260728-labeled-sample-evidence/after-default-wide.png`
- `artifacts/current/20260728-labeled-sample-evidence/after-evidence-wide.png`
- `artifacts/current/20260728-labeled-sample-evidence/after-compact.png`

The closest reproducible historical baseline is
`artifacts/current/20260723-validation-set-v1/before-ko.png`. A true
same-build before image is unavailable because the role UI had already been
implemented before the current capture checkpoint; it is not represented as
a true before capture.

## Boundaries

- This is labeled evidence, not automatic threshold teaching.
- No candidate limit, confusion table, overlap decision, or Apply action is
  produced in this slice.
- Held-out exclusion is encoded now so later threshold generation cannot
  silently train on held-out observations.
- Physical units, calibration, uncertainty, GR&R, and certified metrology
  remain external or unverified.
- R0 owner unaided replay remains an external acceptance gate.

## Completion record

Status: Complete

Scope: Good/Bad/Held-out sample-role persistence plus explicit-run step and
region metric distributions in Workbench and Runner.

Acceptance criteria: role persistence without source or recipe-graph
mutation -> pass; explicit execution boundary -> pass; deterministic
step/region statistics -> pass; Held-out visible and excluded from
development -> pass; save/reopen without stale execution evidence -> pass;
Workbench/Runner shared contract -> pass.

Verification: Release build `0/0`; focused Validation Set `35/35`; Runner
fixture exit `0` with `1/1/1`, `16` distributions, and Held-out excluded;
current wide/compact screenshot quality accepted on attempt 1.

Evidence: files under
`artifacts/current/20260728-labeled-sample-evidence/`.

Boundary / next dependency: `I-06/I-07` must generate deterministic candidate
thresholds and an exact supporting-sample error table from development-only
Good/Bad observations. It must continue to exclude Held-out data.
