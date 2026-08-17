# OpenVisionLab 3D Surface Edge Diagnostics, Thresholds, and Review

Date: 2026-07-31

Status: Complete

Backlog scope: `K-05`, `K-07`, `K-08`

## Outcome

Surface Match now explains the direction evidence behind its 3D-edge score,
applies surface and edge acceptance limits independently, and retains one
controlled accepted/rejected comparison that exposes a surface-only false
positive. The Viewer keeps the original model, Prepared Scene samples, fixed
pose, raw scores, and diagnostic overlays linked by canonical identities.

The operator problem was that equal surface coverage could hide a missing
physical edge and the previous Viewer could not explain the edge direction or
the resulting decision. The product principle is linked
configuration, Viewer, and evidence. OpenVisionLab keeps its own graphite
roles, terminology, layout, assets, and explicit Apply/Preview/Run contracts.

## Included scope

### Direction diagnostic overlay

- Core owns a schema-1 identified overlay linking the exact SurfaceModel,
  Prepared Scene, immutable surface-match execution, model-edge artifact,
  scene-edge artifact, and separate edge score.
- Tools transforms canonical model edges by the identified pose and derives
  display directions only from retained declared model normals and canonical
  edge ordering. It never guesses an acquisition viewpoint.
- Viewer renders matched model edges in green, unmatched model edges in red,
  scene edges in amber, and declared model-normal directions in purple. The
  diagnostic layer is drawn after the base wireframe so it remains readable.
- Known `+Z` outward normals, unit edge directions, stable segment order, exact
  linkage, and tamper rejection are covered by the focused fixture.

### Independent surface and edge limits

- `SurfaceAndEdgeMatchAcceptancePolicy` contains the existing surface limits
  and a separate edge coverage/RMSE policy. It defines no weighted score.
- The evaluator produces separate surface and edge component decisions; the
  overall decision passes only when both components pass.
- Surface Match PropertyGrid exposes four durable values under distinct
  `Surface acceptance` and `Edge acceptance` groups.
- Apply validates and persists the four values but does not execute Preview,
  Publish, Run, or Validation. Save/reopen retains all four values.
- Missing, non-finite, out-of-range, negative, inconsistent, or tampered policy
  and assessment evidence fails closed.

### Retained false-positive review

- The review artifact retains the accepted and rejected assessment, overlay,
  source scene, model, execution pose, and score identities.
- The controlled raised-square case is accepted with Surface `100%` and Edge
  `100%`. The flat-background case is rejected with the same Surface `100%`
  but Edge `0%`.
- The Viewer summary presents the current decision first and then the retained
  accepted/rejected comparison. Full evidence identities remain available in
  the tooltip.
- Show and clear are presentation-only and preserve recipe, source, ROI,
  pipeline, Preview, Publish, Run, and Validation state.

## Deterministic evidence

```text
Accepted overlay    B8D6A8331B20B722F2042281E203E97E149D9A3FB354ACD855C8C5F95832BFE9
Rejected overlay    15C20FDE81096D42E4E3B1A33609BBD01A1D89FD84D81B38460416F451AF1F8C
Accepted assessment 7CF362DA9F8E7CCD97FF682D1DF69520E2E18D6B6E3522DB306AB184344F1C31
Rejected assessment D442B7B002A1BE7A02F2488C91420F21B7C8D63B2ECAC0232BC3B702AD3D58E7
Review              D083FB9408DC56172BC52C4513D46F140642F77A96303821708B2508D5C6622B
```

## UI layout integrity

Fresh application-only captures were compared at both supported sizes.

| Size | State | Result |
| --- | --- | --- |
| Wide `1920 x 1040` | accepted overlay and retained comparison | Pass |
| Compact `1280 x 760` | accepted overlay and retained comparison | Pass |
| Wide `1920 x 1040` | rejected surface-only false positive | Pass |
| Compact `1280 x 760` | rejected surface-only false positive | Pass |

The visual review checked overlapping controls, clipped required text,
controls outside their pane, unreachable controls, unintended horizontal or
nested scroll bars, decision prominence, diagnostic legend visibility, and
accepted/rejected comparison readability. All four After captures passed
automatic screenshot quality on attempt `1`; no unexplained overlap or
required-text clipping was found.

Before, captured from the prior `9ff8105` Release with the same accepted
fixture:

- `artifacts/current/20260731-surface-edge-diagnostic-review/before/wide-teach-before.png`;
- `artifacts/current/20260731-surface-edge-diagnostic-review/before/compact-teach-before.png`.

After:

- `artifacts/current/20260731-surface-edge-diagnostic-review/after/wide-teach-after.png`;
- `artifacts/current/20260731-surface-edge-diagnostic-review/after/compact-teach-after.png`;
- `artifacts/current/20260731-surface-edge-diagnostic-review/after/wide-rejected-after.png`;
- `artifacts/current/20260731-surface-edge-diagnostic-review/after/compact-rejected-after.png`.

## Verification

| Check | Result |
| --- | --- |
| Release solution rebuild | Pass, `0` warnings / `0` errors |
| Diagnostic, independent policy, and review fixture | Pass, `20/20` |
| Workbench/Runner and PropertyGrid parity | Pass, `13/13` |
| Existing surface-edge extraction/scoring | Pass, `21/21` |
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
| Human-owner R0 fixed package | Pass, Wide/Compact `-ValidateOnly`; unaided operation remains external |

Reusable evidence is under
`artifacts/current/20260731-surface-edge-diagnostic-review/`, including the
identified JSON artifacts, reports, regression matrix, and screenshots.

## Explicit boundaries

- Acquisition viewpoint/direction remains unavailable; `K-04` is still
  blocked on `B-12`.
- The direction overlay uses declared model normals and canonical edge
  direction only. It does not infer sensor-facing orientation.
- Surface and edge scores remain separate. There is no weighted score.
- This slice retains one controlled comparison; multiple-match result
  collection/navigation remains owned by `J-12/K-09`.
- Observed runtime is not a production performance budget. `K-11` owns the
  next fixed-fixture Release timing matrix.
- No physical calibration, traceability, uncertainty, GR&R, or metrology claim
  is made. Camera, PLC, robot, cloud, and production-line integration remain
  out of scope.

## Closure record

Status: Complete

Scope: `K-05/K-07/K-08` identified normal/edge-direction overlay, independent
surface and edge acceptance policies, recipe-owned PropertyGrid persistence,
and one retained accepted/rejected false-positive review.

Acceptance criteria: known outward-normal fixture exposes direction evidence
-> pass; surface and edge thresholds are authored and evaluated independently
without a weighted score -> pass; accepted/rejected comparison retains model,
Prepared Scene samples, pose, scores, and identities -> pass; Workbench/Runner
hash parity and non-execution contracts -> pass; Wide/Compact accepted and
rejected layout integrity -> pass.

Verification: the exact Release build, focused checks, regression matrix,
screenshot quality reports, and R0 `-ValidateOnly` reports are recorded under
the current artifact folder.

Evidence: this document and
`artifacts/current/20260731-surface-edge-diagnostic-review/`.

Boundary / next dependency: human-owner R0 remains external for `A-01`.
`K-11` is the next dependency-ready software item; `K-04` remains blocked on
`B-12` and `K-09` remains blocked on `J-12`.

1. `K-11 fixed-fixture matching performance gate` | Recommended model: `gpt-5.6-sol` | Reasoning effort: `high`
2. `Human-owner Wide/Compact R0` | Prerequisite: product-owner unaided operation and evidence | Recommended model: none | Reasoning effort: none
