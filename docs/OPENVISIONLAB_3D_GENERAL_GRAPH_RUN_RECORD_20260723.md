# General Ordered Graph Run Record

Date: 2026-07-23
Status: Complete for the currently registered sequential typed graph

## Outcome

The general ordered recipe executor is now connected to the existing durable
Run Record and docked read-only Run Record view. The execution owner remains
`ToolRecipeOrderedGraphExecution`; reporting maps that result and does not
re-execute tools.

The fixed Synthetic Affine Inspection Plate recipe records all 27 authored
steps:

```text
C3D
  -> Filter
  -> Edge / Line Fit / Intersection paths
  -> Landmark Correspondence
  -> XYZ Affine Solve / Apply
  -> Re-grid
  -> seven ordered measurement tools
  -> one JSON / HTML / CSV Run Record
```

Run Record schema `1.4` preserves the schema `1.3` ordered `Steps` shape and
adds an optional `OutputContentSha256` to each step. Every step records its
authored order, stable step/tool identity, exact input and output entity IDs,
status, message, elapsed time, metrics, overlays, and output-content identity.
Feature-only steps with no measurement metric are still emitted in HTML and
CSV instead of disappearing from the audit trail.

The production Runner accepts a generic recipe through:

```powershell
dotnet run --project src\OpenVisionLab.ThreeD.Runner -- `
  --tool-recipe 3D\SyntheticValidation\AffineInspectionPlateV1\inspection-recipe.ov3d-recipe.json `
  --run-record artifacts\current\20260723-general-graph-run-record\run.json `
  --html-report artifacts\current\20260723-general-graph-run-record\run.html `
  --csv-report artifacts\current\20260723-general-graph-run-record\run.csv `
  --expect-status Pass
```

`--source` may explicitly replace the recipe source for controlled same-grid
repeat validation. It does not rewrite the recipe.

## Compatibility

- Schema `1.4`: general sequential graph, optional per-step output hash.
- Schema `1.3`: existing A3 plus seven-measurement record remains readable and
  its focused verifier still passes.
- Schema `1.2`: existing single-step `Step` path remains unchanged.
- Schema `1.0` and `1.1`: current Shell compatibility remains unchanged.

The Shell uses the existing bilingual docked Run Record tab. When an output
hash exists it shows a short SHA-256 suffix beside the exact route and key
metric; older records simply omit that suffix.

## Verification

| Check | Result | Evidence |
| --- | --- | --- |
| Full Debug build | Pass, 0 warnings / 0 errors | current command output |
| General graph writer verifier | Pass, 21/21 | `artifacts/current/20260723-general-graph-run-record/writer-verification.txt` |
| Production Runner, fixed source | Pass, 27/27 | `runner-report.txt`, `run.json`, `run.html`, `run.csv` |
| Production Runner, controlled measurement failure | Fail as expected, 27/27 with later evidence | `runner-fail-report.txt`, `run-fail.json`, `run-fail.html`, `run-fail.csv` |
| Schema `1.3` bounded multi-step regression | Pass, 21/21 | `schema13-regression.txt` |
| Recipe teaching regression | Pass, 25/25 | `recipe-teaching-regression.txt` |
| Docking regression | Pass, 26/26 | `docking-regression.txt` |
| Korean current-EXE Run Record capture | Pass, quality attempt 1 | `after-ko.png`, `after-ko-quality.txt` |
| English current-EXE Run Record capture | Pass, quality attempt 1 | `after-en.png`, `after-en-quality.txt` |

Evidence root:

```text
artifacts/current/20260723-general-graph-run-record/
```

## Completion Record

```text
Status: Complete
Scope: Map the existing general sequential typed executor into schema 1.4 JSON/HTML/CSV and the existing bilingual docked Run Record view.
Acceptance criteria: 27 authored steps retain exact route/status/time/evidence/output hash; Pass and tolerance-Fail runs complete; schema 1.3 remains compatible; current Korean/English EXE views show the record.
Verification: Debug build 0/0; writer 21/21; production Runner Pass 27/27 and expected Fail 27/27; schema 1.3 21/21; teaching 25/25; docking 26/26; both screenshot quality checks pass on attempt 1.
Evidence: artifacts/current/20260723-general-graph-run-record/
Boundary / next dependency: This proves registered sequential typed graph recording, not arbitrary DAG execution, production batch/history infrastructure, trusted real alignment, physical calibration, or metrology. The next external gate requires distinct real four-landmark acquisitions with declared unit/frame/provenance.
```
