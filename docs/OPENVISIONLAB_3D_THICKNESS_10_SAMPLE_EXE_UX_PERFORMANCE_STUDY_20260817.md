# OpenVisionLab 3D Thickness 10-Sample EXE UX And Performance Study

Date: 2026-08-17
Status: Complete

## Claim Boundary

This study covers deterministic software behavior on ten generated C3D
height fields. Values are `raw-height`; they are not calibrated physical
thickness, certified metrology, Gauge R&R, or production approval. Codex
operated the current Release EXE; this does not replace the product owner's
unaided Wide and Compact R0.

## Scope And Acceptance

The requested outcome was to create varied thickness inputs and ten recipe
projects, teach and execute them through the actual application as an
operator would, identify usability and visual defects, establish measured
speed targets, and implement the highest-value safe improvement.

Acceptance criteria were:

- ten distinct `1280 x 840` C3D inputs and preview images exist;
- ten current-schema Thickness recipes are created through the actual Shell
  EXE and explicitly Previewed and Published when the controlled result
  permits Publish;
- ordered Runner replay agrees with the expected controlled state;
- repeated same-grid authoring is materially shorter without automatic
  Preview, Publish, or Run;
- current-build Wide `1920 x 1040` and Compact `1280 x 760` surfaces remain
  readable and bounded on the selected test monitor;
- observed execution speed meets an evidence-bounded workstation target.

## Controlled Samples And Results

All recipes use the same direct C3D grid ROIs and `7.75..8.25 raw-height`
inclusive limits with a minimum of `3,000` finite measurement samples. Pass
requires every finite separation to remain within the limits; mean alone is
not sufficient.

| # | Sample | Intended condition | Result | Ordered replay | Thickness step | Mean | Min | Max | Valid |
| ---: | --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| 1 | `01-nominal` | nominal 8.00 | Pass | 230.58 ms | 14.14 ms | 8.00 | 8.00 | 8.00 | 9,701 |
| 2 | `02-thin-pass` | lower-bound pass 7.80 | Pass | 224.73 ms | 14.06 ms | 7.82 | 7.80 | 8.00 | 9,504 |
| 3 | `03-thin-fail` | below lower limit 7.40 | Fail | 235.73 ms | 15.02 ms | 7.46 | 7.40 | 8.00 | 9,504 |
| 4 | `04-thick-pass` | upper-bound pass 8.20 | Pass | 235.81 ms | 13.33 ms | 8.18 | 8.00 | 8.20 | 9,504 |
| 5 | `05-thick-fail` | above upper limit 8.60 | Fail | 229.10 ms | 14.20 ms | 8.54 | 8.00 | 8.60 | 9,504 |
| 6 | `06-noisy` | noise crosses both limits | Fail | 241.94 ms | 14.14 ms | 8.00 | 7.66 | 8.36 | 9,504 |
| 7 | `07-gradient` | gradient crosses both limits | Fail | 228.65 ms | 13.65 ms | 7.99 | 7.40 | 8.57 | 9,504 |
| 8 | `08-missing-40` | 40% missing, enough finite data | Pass | 220.02 ms | 10.63 ms | 8.00 | 8.00 | 8.00 | 6,097 |
| 9 | `09-insufficient` | 85% missing, below minimum count | Error | 226.72 ms | 5.43 ms | n/a | n/a | n/a | n/a |
| 10 | `10-local-defect` | local +3.00 defect | Fail | 221.30 ms | 13.87 ms | 8.28 | 8.00 | 11.00 | 9,504 |

The controlled distribution is `Pass 4 / Fail 5 / Error 1`. A final rebuild
replay of sample 9 remained the intended Error at `244.75 ms`, wrote a valid
JSON Run Record with no non-finite metrics, and returned the product's
controlled Error exit code `4`.

## Operator Workflow Findings

### Before

Teaching the first recipe was understandable once the correct grid regions
were known, but repeating the same teaching for another same-size input took
about 33 observed UI input actions. The operator had to create or reopen
context, reproduce both ROI roles, re-enter limits, save, Preview, and
Publish. This was repetitive work rather than new inspection intent.

The perspective Viewer also made a screen click's exact grid row and column
hard to predict. One first attempt selected the wrong region and had to be
corrected. The result was recoverable, but the workflow did not give enough
coordinate confidence before Apply.

### Implemented improvement

Recipe Center now offers **Create grid-compatible variant**. The single setup
surface is prefilled from the saved current recipe and requires only a new
name and a different C3D input. It:

- requires the same C3D grid dimensions;
- preserves ordered steps, typed routes, parameters, stable selection IDs,
  and direct C3D `GridRectangle` coordinates;
- rebinds every retained direct-grid ROI to the new source content identity;
- rejects point, oriented-box, correspondence, and derived-height-field
  selections rather than guessing their compatibility;
- saves a new file without changing the original recipe;
- clears transient Preview, Publish, and validation evidence;
- never invokes Preview, Publish, Run, or Validation;
- immediately enables another compatible variant after creation.

The repeated workflow for samples 5 through 10 was 11 observed actions:
open Recipe Center, choose variant, edit name, edit source, Create, explicit
Preview, and explicit Publish when enabled. That is 22 fewer actions, or a
`66.7%` reduction from the 33-action baseline.

### Visual and interaction review

- Wide and Compact Recipe Center and variant setup fit inside the application
  without overlapping controls, clipped required labels, unreachable actions,
  or nested/global horizontal scrolling.
- Long paths are clipped inside editable text boxes in Compact, but remain
  editable. A tooltip or path-tail presentation would improve scanning; this
  is a low-priority refinement, not a blocker.
- Normal and disabled variant-button states use the existing graphite button
  system. The click path showed no platform-light post-click flash. A held
  pointer-down capture was not available in the automation API, so this study
  does not claim independent held-state screenshot coverage.
- The insufficient-data message is specific and actionable, and Publish is
  unavailable for the Error result.
- A true pre-change Compact capture was unavailable. The report therefore
  uses current Compact after evidence and does not mislabel it as before.

## Performance Baseline And Targets

These are workstation regression targets for the generated `1280 x 840`
fixtures, not production hardware SLAs or maximum-input claims.

| Measure | Target | Observed | Decision |
| --- | ---: | ---: | --- |
| Repeated same-grid authoring | <= 12 actions | 11 actions | Pass |
| Variant Create click to ready | <= 2.5 s | no later than 1.916 s for samples 6-10 | Pass |
| Fresh-process ordered replay | <= 250 ms | max 244.75 ms including final Error replay | Pass |
| Thickness algorithm step | <= 20 ms | max 15.02 ms | Pass |

The product bottleneck was operator repetition, not the thickness algorithm.
The implementation therefore shortened the user path instead of adding
numerical caching or concurrency that the evidence did not justify.
Screenshot capture can pause WPF continuation and was excluded from timing.

## Defects And Priorities

1. `PL-0016 Shell ordered Run for Thickness` — the Runner EXE replays the
   recipe correctly, but the Shell's full Run path remains specialized around
   filtering and does not offer the equivalent Thickness Run. This is the
   highest next software priority because explicit Run is part of the product
   operator loop. Recommended model: `gpt-5.6-sol`; reasoning effort: `medium`.
2. `PL-0017 coordinate-confident grid ROI teaching` — expose an exact live
   row/column locator or an orthographic teaching aid before Apply while
   preserving normal Viewer navigation. Recommended model: `gpt-5.6-sol`;
   reasoning effort: `medium`.
3. Show measured elapsed time in the result/evidence surface instead of only
   in the exported Run Record. Recommended model: `gpt-5.6-terra`; reasoning
   effort: `medium`.
4. Product-owner unaided Wide and Compact R0 remains the acceptance priority.
   Prerequisite: owner operation and observer record. Recommended model: none;
   reasoning effort: none.

The commercial lesson retained is shorter context-preserving variant setup,
linked authoring/evidence, progressive disclosure, and a visible next action.
No competitor screen, theme, topology, name, asset, icon artwork, or code was
copied. Camera, PLC, robot, cloud, account, deployment, and production-line
control remain excluded.

## Implementation And Verification

Changed production ownership:

- Recipe Center and first-use recipe workflow own compatible-variant setup;
- Main Window loads the selected source and supplies its verified binding;
- Run Record serialization omits non-finite metric values while preserving
  the controlled Error status and message.

Verification performed from current source with test `TEMP` and `TMP` routed
to the D-backed evidence root:

- `dotnet build OpenVisionLab.ThreeDStudio.sln -c Release` — `0` warnings,
  `0` errors;
- Recipe Manager/WPG verification — `52/52`;
- generic height-measurement Workbench verification — `56/56`;
- artifact-owned ordered Runner verification, including the non-finite
  Run Record regression — `19/19`;
- ten ordered recipe replays — all ten expected controlled states matched;
- sample 9 final Error replay — Run Record written, zero non-finite metrics;
- actual Release Shell EXE on dynamically selected leftmost `DISPLAY2`, bounds
  `-1920,365,1920 x 1080`, at Wide and Compact sizes;
- current-build screenshot review of Recipe Center, variant setup, result,
  missing-data, and insufficient-data states;
- `git diff --check` — pass.

Evidence root:

`D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260817-thickness-10-recipe-ux-performance\`

Important contents:

- `sample-manifest.json` — sample identities, SHA-256 values, expectations,
  actual metrics, and Run Record links;
- `samples\<id>\` — ten C3D inputs and ten preview images;
- `recipes\` — ten current-schema recipe projects;
- `before\` and `after\` — current EXE UI evidence;
- `logs\` — build, focused checks, text reports, and JSON Run Records.

## Completion Record

```text
Status: Complete
Scope: Ten varied synthetic thickness inputs and recipes, actual EXE teaching/Preview/Publish review, ordered Runner replay, UX and speed baseline, compatible-grid variant authoring, and controlled-Error Run Record correction
Acceptance criteria: ten C3D inputs/previews/recipes -> pass; ten expected controlled states -> pass; repeated workflow <=12 actions -> pass at 11; variant ready <=2.5 s -> pass at <=1.916 s observed; replay <=250 ms -> pass at <=244.75 ms; thickness step <=20 ms -> pass at <=15.02 ms; current Wide/Compact review -> pass within stated capture limits
Verification: Release build 0/0; Recipe Manager/WPG 52/52; height measurement 56/56; artifact-owned Runner 19/19; ten ordered replays matched; sample 9 final controlled Error record serialized; git diff --check pass
Evidence: this document; .proofline/issues/PL-0015.json; D:/OpenVisionLab-TestData/OpenVisionLab-3D-Studio/artifacts/current/20260817-thickness-10-recipe-ux-performance/
Boundary / next dependency: raw-height synthetic evidence is not physical metrology; owner R0 remains external; PL-0016 owns Shell full-Run parity and PL-0017 owns coordinate-confident grid ROI teaching
```
