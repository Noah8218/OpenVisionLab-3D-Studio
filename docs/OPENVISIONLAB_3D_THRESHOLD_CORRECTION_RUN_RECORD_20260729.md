# Threshold Correction Evidence in Run Record

Date: 2026-07-29

Status: Complete for `L-11`

## Product-direction source

This slice continues the approved 11-video product direction recorded in:

- `docs/OPENVISIONLAB_3D_COMMERCIAL_VIDEO_DIRECTION_AND_PRIORITY_20260727.md`;
- `docs/OPENVISIONLAB_3D_INDUSTRIAL_UX_AUDIT_20260728.md`.

The direct lesson remains SICK Nova's evidence-based threshold workflow, with
OpenVisionLab's stricter separation of deterministic suggestion, explicit
operator correction, development replay, Held-out replay, and durable
reporting. GoPxL's separation of recipe, selected-tool editing, execution, and
results also remains intact.

Camera acquisition, stereo reconstruction, PLC/robot/fieldbus, cloud/plant
management, implicit execution, and physical-metrology claims remain out of
scope.

## Completed scope

Ordered graph Run Records now use schema `1.5` and contain one typed,
reporting-owned threshold-correction snapshot.

The snapshot records:

- `Available`, `Unavailable`, `Stale`, `Mismatch`, or `Invalid`;
- sidecar path and SHA-256;
- the original candidate, step, tool, metric, and limit identities;
- exact before and suggested typed parameter values;
- exact manually committed values when present;
- exact before/corrected development sample identities, roles, status,
  metrics, and mismatch counts;
- exact Held-out identities, status, and metrics.

The projection reads the existing
`<recipe>.threshold-correction.json` sidecar. It does not calculate candidates,
rank thresholds, apply parameters, run inspection, replay development
samples, replay Held-out samples, or save either the recipe or sidecar.

Fail-closed behavior is explicit:

- a missing sidecar is `Unavailable`;
- recipe, source, candidate, step, or tool identity differences are
  `Mismatch`;
- changed committed recipe parameter text is `Stale`;
- malformed or internally inconsistent evidence is `Invalid`.

JSON stores the full typed snapshot. HTML renders state, sidecar identity,
candidate identity, before/suggested/committed parameters, development
mismatch and identities, and Held-out identities. CSV remains the existing
per-step metric export; `L-11` did not redefine its row model.

The Workbench Run Record tab reads the same JSON without execution and shows a
compact evidence card before artifact actions. Wide layout shows candidate and
parameter rows; Compact layout keeps the state and summary visible.

## Controlled evidence

The existing deterministic manual-correction fixture was replayed through the
production Runner:

- candidate: `threshold.0ad7b16eaa3d4362`;
- before: `MinimumThickness=0`, `MaximumThickness=20`;
- suggested: `MinimumThickness=2`, `MaximumThickness=4`;
- manual: `MinimumThickness=1.5`, `MaximumThickness=4.5`;
- development mismatch: `1 -> 0`;
- Held-out: one exact sample, `Pass`.

The recipe itself produces an overall `Fail` on its taught source under the
corrected range. This is expected inspection output and is independent of the
embedded correction evidence, which is `Available`.

## Verification

Commands actually run:

```powershell
dotnet build "OpenVisionLab.ThreeDStudio.slnx" -c Release

OpenVisionLab.ThreeD.Runner.exe `
  --tool-recipe <manual-correction-recipe> `
  --report artifacts/current/20260729-threshold-correction-run-record/runner.txt `
  --run-record artifacts/current/20260729-threshold-correction-run-record/run-record.json `
  --html-report artifacts/current/20260729-threshold-correction-run-record/run-record.html `
  --csv-report artifacts/current/20260729-threshold-correction-run-record/run-record.csv

OpenVisionLab.ThreeD.Shell.exe `
  --verify-run-record-history `
  artifacts/current/20260729-threshold-correction-run-record/run-record-history.txt
```

Results:

- Release build: `0` warnings, `0` errors;
- Run Record history/projection: `10/10`;
- Validation Set regression: `72/72`;
- Inspection Workspace regression: `63/63`;
- Recipe Manager/PropertyGrid regression: `37/37`;
- code structure: `17/17`;
- production Runner JSON: schema `1.5`, correction state `Available`, exact
  before/suggested/manual/development/Held-out parity;
- production HTML: the same evidence rendered;
- current-Release Wide and Compact screenshot quality: accepted on attempt
  `1`;
- `git diff --check`: pass.

## UI evidence

- `before-run-record.png`: fresh current-Release schema `1.4` baseline captured
  before implementation; it has no threshold-correction evidence surface.
- `after-run-record-wide.png`: schema `1.5`, `Available`, exact candidate and
  before/suggested/manual values are visible.
- `after-run-record-compact.png`: schema `1.5` state and non-execution summary
  remain visible at `1280 x 760`.

All captures contain only the application window.

## Completion record

Status: Complete

Scope: `L-11` typed threshold-correction evidence projection in JSON/HTML Run
Record and read-only Workbench display.

Acceptance criteria: exact before/suggested/manual/corrected-development/
Held-out evidence -> pass; recipe/source/candidate/sample identities -> pass;
missing/stale/mismatch/invalid fail-closed states -> pass; no threshold-policy
duplication or execution -> pass; Workbench/Runner parity -> pass; legacy Run
Record readability -> pass.

Verification: Release build `0/0`; Run Record `10/10`; Validation Set `72/72`;
Inspection Workspace `63/63`; Recipe Manager/PropertyGrid `37/37`; structure
`17/17`; production JSON/HTML and current Wide/Compact captures pass.

Evidence:
`artifacts/current/20260729-threshold-correction-run-record/`.

Boundary / next dependency: this does not approve production tolerances,
physical calibration, traceability, uncertainty, GR&R, or metrology. The next
implementation slice is `H-02/H-03/H-04 completeness grid metrics`. Owner R0
remains an external acceptance gate.
