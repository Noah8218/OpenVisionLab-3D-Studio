# OpenVisionLab 3D Surface Match Pose And Score Export

Date: 2026-08-04

Status: Complete for the documented software scope

## Outcome

`L-13 Surface-match pose/score component export` is complete. Run Record
schema `1.6` can retain one optional, typed Surface Match evidence snapshot
containing the exact identified model, Prepared Scene, execution, pose,
transformed overlay, separate surface and edge score components, and the
separate authored assessment.

The export path accepts already saved artifacts and produces JSON, HTML, and
CSV without pose search, score calculation, or acceptance evaluation. A
matched execution requires exact linked score and assessment artifacts. A
`NoMatch` execution is exported without inventing a pose, overlay, score, or
assessment. Schema-`1.5` Run Records remain readable with no Surface Match
evidence.

## User and developer workflow

Use the existing Runner with a recipe context and the saved matching
artifacts:

```powershell
dotnet run --project src/OpenVisionLab.ThreeD.Runner/OpenVisionLab.ThreeD.Runner.csproj -c Release -- `
  --tool-recipe <recipe.json> `
  --surface-match-model <surface-model.json> `
  --surface-match-scene <prepared-scene.json> `
  --surface-match-execution <surface-match-execution.json> `
  --surface-match-score <surface-edge-score.json> `
  --surface-match-assessment <surface-edge-assessment.json> `
  --report <export.txt> `
  --run-record <run-record.json> `
  --html-report <run-record.html> `
  --csv-report <run-record.csv>
```

For a `NoMatch` execution, omit both `--surface-match-score` and
`--surface-match-assessment`. Supplying only one of the two is invalid.

## Preserved contracts

- JSON retains the typed source artifacts and their existing content hashes.
- HTML shows exact identities, row-major pose matrix, translation, independent
  surface/edge score tables, and independent assessment limits and decisions.
- CSV uses explicit component/field rows and the source artifact SHA-256 for
  each value.
- Raw surface and edge channels remain separate; no weighted or composite
  score is created.
- Assessment remains an interpretation of immutable raw score evidence.
- Model, scene, execution, score, or assessment mismatches fail closed.
- The Data projection assembly has no dependency on
  `OpenVisionLab.ThreeD.Tools`, so the projection cannot execute matching.
- Source artifact files remain byte-identical before and after export.
- Export does not Preview, Publish, Run, Validate, change a recipe, or mutate
  Viewer state.

No Library-Noah package or algorithm changed. No UI control, layout, text,
theme, or Viewer renderer changed, so Wide/Compact screenshot evidence is not
part of this non-UI closure.

## Verification evidence

Physical evidence root:

`D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\20260804-l13-surface-match-export\`

| Gate | Result | Evidence |
| --- | --- | --- |
| Release solution build | Pass, `0` warnings / `0` errors | `dotnet build OpenVisionLab.ThreeDStudio.sln -c Release -p:Platform="Any CPU"` |
| L-13 Release focused verification | Pass, `19/19` | `surface-match-run-record-export-release-verification.txt` |
| Existing surface-edge foundation | Pass, `21/21` | Replayed by the focused verification |
| Existing diagnostic review | Pass, `20/20` | Replayed by the focused verification |
| JSON/HTML/CSV identified-value parity | Pass | `surface-match.run-record.json/.html/.csv` |
| Matched, NoMatch, mismatch, tamper, and legacy gates | Pass | Focused verification report |
| Direct Release Runner CLI export | Pass, exit `0` | `cli-release/export.txt` and `cli-release/run-record.*` |
| Code structure and Noah ownership | Pass, `29/29`; migration debt `0` | `code-structure-report.txt` |

## Files and ownership

- Core owns the optional WPF-neutral Run Record evidence contract.
- Data owns identity validation and projection only.
- Runner owns command routing and JSON/HTML/CSV presentation.
- Existing matching and assessment artifacts remain authoritative; reporting
  does not copy numerical ownership into Studio.

## Completion record

Status: Complete

Scope: Optional schema-`1.6` Surface Match Run Record evidence, exact linked
artifact validation, matched and NoMatch export, JSON/HTML/CSV output, direct
Runner command, and focused regression evidence

Acceptance criteria: Exact model/scene/execution/pose/overlay identities ->
pass; separate surface and edge scores -> pass; separate assessment -> pass;
JSON/HTML/CSV parity -> pass; no recomputation boundary -> pass; mismatch and
tamper fail closed -> pass; NoMatch remains explicit -> pass; schema-`1.5`
legacy read -> pass

Verification: Release solution `0/0`; focused L-13 `19/19`; existing edge
foundation/review `21/21` and `20/20`; direct CLI exit `0`; structure `29/29`

Evidence: This document and the D-drive evidence root above

Boundary / next dependency: No matching, scoring, acceptance, Library-Noah,
UI, camera, calibration, reconstruction, metrology, or weighted-score change.
Its former `PL-0002 Runner --help successful exit` next item is complete. The
remaining acceptance priority is human-owner Wide/Compact R0 and requires
owner operation before model-token spend.
