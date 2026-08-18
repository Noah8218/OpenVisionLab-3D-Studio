# OpenVisionLab 3D Completeness Per-cell Run Record Export

Date: 2026-08-18
Status: Complete
Backlog: `PL-0022` / `L-12`

## Operator problem

The Completeness inspection already calculated deterministic grid-cell results,
but an ordered Run Record retained only the step summary. An operator could not
trace a failed aggregate result back to the exact cell, source-grid region,
sample coverage, raw-height value, or decision reason in exported evidence.

## Product decision

Run Record schema `1.9` preserves the exact typed
`C3DCompletenessGridMetricOutput` produced by ordered execution. Reporting
projects that evidence; it does not reload the source, rebuild the grid, or
execute the algorithm again.

The contract retains, per cell:

- stable cell identity and zero-based grid row/column;
- source-region row/column and row/column counts;
- total, finite, and missing sample counts plus finite-coverage ratio;
- nullable mean raw height, reference mean, and nullable reference-relative
  mean;
- unit, frame, decision, decision reason, and Completeness content SHA-256.

JSON keeps the typed hierarchy. HTML adds a readable, horizontally scrollable
cell table with related coordinates and counts grouped together. CSV keeps the
existing step/metric rows and adds explicit `completenessCell` child rows with
separate machine-readable columns.

## Integrity and compatibility

- A successful current Completeness step without cell evidence is rejected.
- Cell count must match the authored profile, cell IDs must be unique, regions
  and counts must be valid, finite values must remain finite, and identity,
  unit, frame, decision, reason, and content hash must be present.
- Malformed current evidence fails before Run Record projection.
- Non-Completeness steps remain unchanged.
- Legacy schema `1.8` records without this optional field remain readable.
- The structured evidence remains observational and does not change status,
  deterministic identity, recipe state, Preview, Publish, or Run behavior.

## Verification

Evidence root:

`D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260818-pl0022-completeness-cell-export\verification`

| Gate | Result |
| --- | --- |
| Completeness golden/export contract | Pass, `30/30` |
| Exact ordered-output instance reused | Pass; same typed instance and SHA-256 |
| JSON/HTML/CSV known-cell parity | Pass, `4/4` cells |
| Artifact-owned ordered Runner | Pass, `22/22` |
| Synthetic Affine plate | Pass, `21/21` |
| Surface Match Run Record | Pass, `23/23` |
| Shell ordered Run | Pass, `15/15` |
| Run Record history | Pass, `12/12` |
| Workbench docking/theme | Pass, `87/87` |
| Shell command-line options | Pass, `40/40` |
| Structure and algorithm ownership | Pass, `29/29`, zero migration debt |
| Final Release solution build | Pass, `0` warnings / `0` errors |
| Refreshed Wide/Compact R0 `-ValidateOnly` | Pass; no application launched |

Representative artifacts:

- `known-completeness-grid-run-record.json`
- `known-completeness-grid-run-record.html`
- `known-completeness-grid-run-record.csv`
- `json-html-csv-parity.txt`
- `release-build-final.txt`
- `code-structure-report.txt`
- `human-owner-r0-wide-validate.txt`
- `human-owner-r0-compact-validate.txt`

The controlled in-app browser rejected direct local-file navigation under its
security policy, so the HTML was not claimed as visually browser-inspected.
Current-source verification instead proves the complete HTML structure, all
four cell identities, values, responsive overflow container, and JSON/CSV
parity. This does not affect the export contract but remains the boundary of
the evidence.

## Reusable check

For any future per-cell result export:

1. prove that execution passes the original typed result into the Run Record;
2. compare JSON cells with CSV child rows field by field;
3. confirm HTML contains every stable cell identity and nullable value policy;
4. reject missing or malformed current evidence;
5. deserialize the immediately previous schema without inventing evidence;
6. rerun ordered, unrelated-result, history, structure, Release, and fixed R0
   prerequisite checks.

## Completion record

Status: Complete
Scope: Preserve exact Completeness grid-cell evidence in schema `1.9` Run
Records and expose it as typed JSON, readable HTML, and structured CSV child
rows without rerunning inspection.
Acceptance criteria: exact typed output retained -> Pass; required cell fields
and identity exported consistently -> Pass; current missing/malformed evidence
fails closed -> Pass; legacy and unrelated records remain readable -> Pass;
focused and affected verification -> Pass.
Verification: Release `0/0`; Completeness `30/30`; artifact-owned Runner
`22/22`; Synthetic Affine `21/21`; Surface Match `23/23`; ordered Run `15/15`;
history `12/12`; docking/theme `87/87`; Shell options `40/40`; structure
`29/29`; JSON/HTML/CSV parity `4/4`; Wide/Compact R0 `-ValidateOnly` pass.
Evidence: this document, `.proofline/issues/PL-0022.json`, and the D-backed
evidence root above.
Boundary / next dependency: browser-rendered visual inspection of the local
HTML file was unavailable under the controlled browser policy; product-owner
unaided Wide/Compact R0 remains external and is not replaced by this software
closure.
