# OpenVisionLab 3D Surface Edge Artifacts and Separate Score

Date: 2026-07-31

Status: Complete

Backlog scope: `K-02`, `K-03`, `K-06`

## Outcome

OpenVisionLab 3D Studio now owns identified model and organized-scene 3D-edge
artifacts plus a separate, decision-free edge score at an already identified
surface pose. The existing surface coverage remains unchanged. The Viewer
shows both channels as distinct evidence and explicitly labels the edge score
as diagnostic rather than Pass/Fail policy.

The product principle is that configuration, Viewer, and evidence stay linked.

## Included scope

### Core contracts

- `ModelSurfaceEdgeArtifact` stores stable vertex-pair locators, endpoint
  geometry, midpoint anchor, length, boundary/crease classification, strength,
  source SurfaceModel identity, parameters, and canonical SHA-256.
- `SceneSurfaceEdgeArtifact` stores stable adjacent-grid locators, endpoint
  geometry, higher endpoint anchor, absolute Z step, adjacency axis, source
  Prepared Scene identity, grid dimensions, parameters, and canonical SHA-256.
- `SurfaceAndEdgeMatchScoreArtifact` links one immutable surface-match
  execution and the two edge artifacts. It stores surface and edge components
  separately and owns no acceptance threshold.
- The common validator rejects unsupported schemas/semantics, invalid or
  duplicated locators, unstable order, non-finite/inconsistent geometry,
  invalid thresholds, tampered identities, inconsistent counts/RMSE, and a
  surface component that does not match its linked execution.

### Deterministic extraction and scoring

- Model v1 visits canonical undirected mesh edges. Boundary edges are optional;
  two-face crease edges use their dihedral angle. More than two owning faces
  fails closed as non-manifold input.
- Scene v1 accepts only a complete organized XYZ grid. It visits adjacent
  column/row cells in stable order, retains steps meeting the authored absolute
  Z threshold, and uses the higher endpoint as the positional anchor.
- Missing cells, incomplete grid/point correspondence, and unorganized input
  fail closed. No repair or guessed adjacency is permitted.
- Edge score v1 transforms model-edge anchors with the existing identified
  model-to-scene pose and performs stable greedy unique-nearest positional
  matching. It does not rerun pose search or overwrite surface coverage.
- Atomic JSON persistence validates before save and after load. A rejected
  save does not replace the previous valid file.

### Workbench and Viewer

- Workbench owns the optional score evidence and routes it to the same Viewer
  as the immutable surface execution.
- Viewer shows `Surface coverage`, `Surface RMSE`, `3D-edge score`, and
  `3D-edge RMSE` as separate rows.
- The compact boundary text states `Edge diagnostic only · no Pass/Fail
  decision` when no authored acceptance assessment is supplied.
- Full edge-artifact and score identities remain available in the evidence
  tooltip. Short hashes remain secondary display values.
- Show and clear are presentation-only: recipe, source, ROI, pipeline,
  Preview, Publish, Run, and Validation state do not change.

## Controlled false-background fixture

The nominal model is a `6 x 6 mm` two-triangle square at `Z=1`. Model
extraction produces four boundary edges and excludes the flat shared diagonal.

Two complete `9 x 9` organized scenes are compared at the same identity pose:

| Scene | Surface coverage | 3D-edge score | Meaning |
| --- | ---: | ---: | --- |
| Raised square with a true perimeter step | `2/2 = 100%` | `4/4 = 100%`, RMSE `0 mm` | Surface and edge evidence agree |
| Flat `Z=1` background | `2/2 = 100%` | `0/4 = 0%`, RMSE unavailable | Surface-only false background is exposed |

The score channels stay separate; this slice does not invent a combined score
or a Pass/Fail threshold.

Canonical identities for the accepted height-edge case:

```text
SurfaceModel  06D7D0A7AEB996654721F3F350A4AEFDB46B05EA05013CBCC8115B275EE445D3
PreparedScene EA85BD7F3365D744D56F021314018FC2FA8793C7C5459B67789BB0C475AF9E82
Execution     7A8B7D68AA1493C586D412AF685F9A4D49F78AB87B8CBD9C4DEB52A3FE8F16F3
ModelEdges    47F5C3105B01E178D76EC60869BF3D4239F525D91E17588504F757E80A84F06C
SceneEdges    61A4BD77C0E6372DFBE10925E3FC690E95F5376A856E104DE390F649D37CB496
SeparateScore CDBD5B58DCE5949B08DBAEC88796E3B7242E5829AFDDA6B70B1F242598F43DF2
```

## UI layout integrity

Fresh current-build evidence was reviewed at both supported sizes:

| Size | State | Result |
| --- | --- | --- |
| Wide `1920 x 1040` | Teach, identified surface and 3D-edge evidence | Pass |
| Compact `1280 x 760` | Teach, identified surface and 3D-edge evidence | Pass |

The comparison explicitly checked for overlapping controls, clipped required
labels/actions, controls outside their pane, unreachable controls, and
unintended horizontal or nested scroll bars. The two new score rows and the
diagnostic-only boundary remain fully visible in both layouts. Screenshot
quality accepted attempt `1` in both final captures.

Before:

- `artifacts/current/20260731-surface-edge-score/before/wide-teach-before.png`;
- `artifacts/current/20260731-surface-edge-score/before/compact-teach-before.png`.

After:

- `artifacts/current/20260731-surface-edge-score/after/wide-teach-after.png`;
- `artifacts/current/20260731-surface-edge-score/after/compact-teach-after.png`.

## Verification

| Check | Result |
| --- | --- |
| Release solution build | Pass, `0` warnings / `0` errors |
| Surface-edge extraction/scoring fixture | Pass, `21/21` |
| Surface-edge Workbench/Runner parity | Pass, `12/12` |
| Existing surface matching | Pass, `34/34` |
| Existing surface-match acceptance | Pass, `14/14` |
| SurfaceModel | Pass, `22/22` |
| Source-channel/dense-normal quality | Pass, `26/26` |
| Source Quality workspace | Pass, `18/18` |
| Workbench docking | Pass, `76/76` |
| Inspection Workspace | Pass, `63/63` |
| Validation Set | Pass, `84/84` |
| Height distribution | Pass, `25/25` |
| Recipe Manager/WPG | Pass, `38/38` |
| Shell smoke command-line parser | Pass, `26/26` |
| Code structure | Pass, `17/17` |
| Human-owner R0 fixed package | Pass, Wide/Compact `-ValidateOnly`; unaided owner operation remains external |

Reusable evidence:

- `artifacts/current/20260731-surface-edge-score/surface-edge-matching-verification.txt`;
- `artifacts/current/20260731-surface-edge-score/surface-edge-workbench-parity.txt`;
- `artifacts/current/20260731-surface-edge-score/regression/`;
- the identified JSON artifacts in
  `artifacts/current/20260731-surface-edge-score/`.

## Explicit boundaries

- This is positional edge scoring only. Acquisition viewpoint and
  edge-direction weighting remain outside v1.
- No edge diagnostic overlay is included; `K-05` owns that work.
- No independent edge acceptance threshold or combined decision is included;
  `K-07` owns policy authoring.
- No false-positive review workspace is included; `K-08` owns that workflow.
- No physical calibration, traceability, uncertainty, GR&R, or metrology claim
  is made.
- Camera, PLC, robot, cloud, and production-line integration remain out of
  scope.

## Closure record

Status: Complete

Scope: `K-02/K-03/K-06` identified model/scene 3D-edge artifacts, atomic
persistence, separate positional edge score, shared Runner/Workbench boundary,
and Viewer evidence rows.

Acceptance criteria: stable model-edge artifact -> pass; stable complete-grid
scene-edge artifact -> pass; false-background fixture separates equal surface
coverage into `100%` versus `0%` edge scores -> pass; tampered/incomplete/
non-manifold/mismatched input fails closed -> pass; Runner/Workbench exact
score identity -> pass; Wide/Compact layout integrity -> pass.

Verification: commands and results are recorded above and under the current
artifact folder. The refreshed fixed-hash R0 package passes both Wide and
Compact `-ValidateOnly` modes; this does not replace unaided owner operation.

Evidence: this document and
`artifacts/current/20260731-surface-edge-score/`.

Boundary / next dependency: human-owner R0 remains external for `A-01`.
Dependency-ready software work is `K-05/K-07/K-08`; acquisition viewpoint
work `K-04` remains blocked on `B-12`.

1. `K-05/K-07/K-08 edge diagnostics, independent thresholds, and false-positive review` | Recommended model: `gpt-5.6-sol` | Reasoning effort: `high`
2. `Human-owner Wide/Compact R0` | Prerequisite: product-owner unaided operation and evidence | Recommended model: none | Reasoning effort: none
