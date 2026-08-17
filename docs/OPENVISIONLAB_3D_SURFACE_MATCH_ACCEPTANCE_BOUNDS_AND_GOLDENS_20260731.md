# Surface Match Acceptance, Authored Bounds, and Goldens

Date: 2026-07-31

Status: Complete

## Scope

This checkpoint completes `F-14`, `J-11`, `J-14`, `J-15`, and `M-16` for
the current deterministic surface-matching foundation.

The operator problem was that the prior Viewer exposed a valid raw pose,
coverage, RMSE, overlay, and identities, but did not answer whether the
evidence met a recipe-owned rule. Search bounds were executor inputs rather
than an editable, persisted inspection-tool contract, and rejection/timing
evidence had no focused golden suite.

The implemented result keeps four responsibilities separate:

1. the raw match and transformed-model overlay remain decision-free;
2. a recipe-owned acceptance policy interprets the identified raw evidence;
3. a finite authored pose-search domain controls search without changing the
   acceptance rule;
4. observed stage timing is diagnostic evidence and is excluded from hashes,
   decisions, and performance claims.

## Product workflow

`Surface Match` is now a typed Workbench tool. Its progressive PropertyGrid
groups the minimum coverage and maximum inlier RMSE separately from finite X,
Y, and Z rotation ranges, translation limits, correspondence distance,
minimum match count, and the maximum candidate guard.

Editing and applying those parameters updates the recipe step only. It does
not execute Preview, Publish, Run, or Validation. Save and reopen restore the
authored values without executing matching.

When identified execution evidence is selected, the Viewer keeps the raw
state, coverage, RMSE, pose, model hash, and overlay hash visible. A distinct
decision block shows `Pass`, `Fail`, or `Rejected`, the exact authored limits,
the rejection or failure reason, and observational three-stage elapsed time.
The raw score and transformed geometry are not recolored or rewritten by the
decision.

## Contract ownership

### Core

- `SurfaceMatchAcceptancePolicy` is schema `1.0` identified policy evidence
  with minimum one-way coverage and maximum inlier RMSE semantics.
- `SurfaceMatchAssessmentArtifact` links one immutable raw execution to one
  policy and records `Pass`, `Fail`, or `Rejected` plus a typed reason.
- `RigidSurfacePoseSearchParameterValidator` validates finite ordered
  rotation ranges in `[-180, 180]`, finite ordered translation bounds,
  positive correspondence distance, minimum matched sample count, and a
  hard maximum of `1,000,000` candidates.
- `SurfaceMatchRuntimeReport` contains exactly `pose-search`,
  `execution-artifact`, and `acceptance-evaluation` stages. Timing and the
  observation timestamp are deliberately outside canonical identities.

### Data

Assessment and runtime JSON persistence validates before save and after load,
uses atomic replacement, rejects malformed or tampered content, and preserves
the prior valid artifact when a replacement is unsafe.

### Tools and execution boundary

`SurfaceMatchEvaluationExecutor` is shared by Runner and Workbench. It runs
the raw search, creates the identified raw execution, applies the separate
acceptance policy, and records observational stage timing. The same inputs
therefore produce identical raw and assessment identities in both hosts.

### Workbench and Viewer

- the typed adapter exposes the policy and bounded-search values as recipe
  parameters;
- Apply is an edit action, not execution;
- selected evidence is presentation-only and can be cleared without changing
  recipe, source, ROI, Preview, Publish, Run, or Validation state;
- the Viewer uses the existing OpenVisionLab graphite role system and compact
  evidence card.

## OpenVisionLab product principle

The product principle is linked configuration/Viewer/evidence with progressive
disclosure. The
OpenVisionLab implementation keeps the existing Authoring responsibility
rail, typed PropertyGrid, explicit action contracts, dominant Viewer, and
OpenVisionLab terminology.

## Controlled golden evidence

Policy:

```text
minimum coverage = 0.90
maximum inlier RMSE = 0.25
policy SHA-256 = 2113FB3D6E13D582993BA9CE7EF7AA531F438605650895FE53135B159BA3569E
```

Golden cases:

| Case | Raw evidence | Assessment | Golden assessment SHA-256 |
| --- | --- | --- | --- |
| Known pose | `Matched`, `5/5`, coverage `1.0`, RMSE `8.8817841970012523E-16` | `Pass / MeetsAuthoredLimits` | `EBB504571A2E3FEDDEEFD4645A14B29C6940574B2F1FDFC97F32F784F197698A` |
| Controlled occlusion | `Matched`, `4/5`, coverage `0.8`, RMSE `0.8062257706404623` | `Fail / CoverageBelowMinimum` | `B4643B32DA87D2541053C2B97B911B08F005D4DDE75CEDFC34D4EB29EE128854` |
| Pose outside authored domain | `NoMatch`, coverage `0.0` | `Rejected / PoseSearchNoMatch` | `D9C8EF83D73E8683CB3BA122BAD011B9D39A03607B0F005A00224CE878F58210` |

### Current Vision SDK 3.0 golden normalization — 2026-08-05

The table above preserves the original `Lib.ThreeD 2.9.1` checkpoint.
`OpenVisionLab.Vision3D 3.0.0` commit
`f34fdf912ff38fe20f36dbb063837e14b4f922b3` includes the SDK's
overflow/underflow-safe distance and scaled RMSE accumulation. For the
controlled-occlusion fixture, the mathematically equivalent calculation moves
one representable `double` value from `0.8062257706404623` to
`0.80622577064046241`. Coverage, correspondence count, acceptance decision,
reason, and authored limits are unchanged.

The current assessment SHA-256 is therefore
`9B9E711B9CB72DF0F4A2DC9E520B5A2EE8715BBB4B5B586FF8BB886B78557C95`.
The known-pose and outside-domain goldens remain byte-identical. This is an
explicit SDK numerical-hardening normalization, not a change to Studio's
acceptance policy.

The known-pose fixture retains the documented `30 degree` yaw and
`(10, -4, 2) mm` translation. The bounded comparison enumerates `7`
candidates versus `25` in the broader controlled range. Recorded elapsed
times vary by run and are evidence that the stages are observed; they are not
a performance budget or proof that fewer candidates always takes less wall
time.

## UI layout integrity

Fresh current-build evidence was checked at both supported sizes:

| Size | State | Result |
| --- | --- | --- |
| Wide `1920 x 1040` | Teach, Surface Match selected, Parameters expanded, identified Pass evidence | Pass |
| Compact `1280 x 760` | Teach, Selected Tool focused, Parameters expanded, identified Pass evidence | Pass |

The visual comparison explicitly checked:

- no overlapping controls;
- no clipped required task, field, status, or command text;
- no controls rendered outside their pane;
- no unreachable required control;
- no unintended horizontal or nested scroll bars;
- the dominant Viewer remains usable beside the expanded parameter surface;
- raw score, decision, authored limits, timing, and compact identities remain
  distinguishable;
- secondary shortened hashes remain available through the evidence tooltip.

Closest reproducible before evidence, captured before this slice:

- `artifacts/current/20260731-surface-match-acceptance-bounds-goldens/before/wide-before.png`;
- `artifacts/current/20260731-surface-match-acceptance-bounds-goldens/before/compact-before.png`.

Final current-build after evidence:

- `artifacts/current/20260731-surface-match-acceptance-bounds-goldens/after/wide-parameters.png`;
- `artifacts/current/20260731-surface-match-acceptance-bounds-goldens/after/compact-parameters.png`.

All four associated screenshot quality reports accept attempt `1`.

## Verification

| Check | Result |
| --- | --- |
| Release solution build | Pass, `0` warnings / `0` errors |
| Acceptance/bounds/rejection/runtime goldens | Pass, `14/14` |
| Existing matching and overlay regression | Pass, `34/34` |
| Workbench/Runner raw, decision, recipe, and persistence parity | Pass, `16/16` |
| SurfaceModel regression | Pass, `22/22` |
| Source-channel and dense-normal regression | Pass, `26/26` |
| Source Quality regression | Pass, `18/18` |
| Workbench docking/layout regression | Pass, `76/76` |
| Inspection Workspace regression | Pass, `63/63` |
| Validation Set regression | Pass, `84/84` |
| Height distribution regression | Pass, `25/25` |
| Shell smoke command-line parser | Pass, `25/25` |
| Code structure | Pass, `17/17` |
| Wide R0 package `-ValidateOnly` | Pass; no application launched |
| Compact R0 package `-ValidateOnly` | Pass; no application launched |

The first parallel regression attempt shared a verification scratch filename
between two independent verifiers and produced one transient atomic-overwrite
failure. The verifiers were rerun in separate evidence folders; the isolated
current-build results above are the accepted evidence.

Reusable evidence:

- `artifacts/current/20260731-surface-match-acceptance-bounds-goldens/`;
- `verification/acceptance/report.txt`;
- `verification/matching/report.txt`;
- `verification/surface-model/report.txt`;
- `verification/parity/report.txt`;
- the regression, layout, R0, identified JSON, and recipe round-trip artifacts
  below the same folder.

## Boundaries

This checkpoint does not prove or add:

- a production performance budget or throughput guarantee;
- multiple-match collections or issue navigation;
- symmetry-aware pose equivalence;
- model/scene 3D-edge extraction or an edge score;
- physical calibration, uncertainty, traceability, or metrology;
- camera, reconstruction, PLC, robot, cloud, or production-line integration;
- human-owner Wide/Compact usability acceptance.

Human-owner R0 remains the external prerequisite for `A-01`. Automated
`-ValidateOnly` verifies the fixed package but does not replace unaided owner
operation.

## Completion record

Status: Complete

Scope: Separate identified match acceptance, finite authored search bounds,
typed rejection and observational runtime evidence, PropertyGrid recipe
round-trip, Viewer-linked decision evidence, Workbench/Runner parity, and
known-pose/false-positive/out-of-domain goldens.

Acceptance criteria: acceptance does not mutate raw execution -> Pass;
authored ranges validate and persist without execution -> Pass; Pass, Fail,
and Rejected fixtures produce exact identities and reasons -> Pass; runtime
stages remain observational -> Pass; Wide/Compact expanded and collapsed
surfaces have no unexplained overlap or required-text clipping -> Pass;
affected regressions and fixed R0 package validation -> Pass.

Verification: Release `0/0`; acceptance `14/14`; matching `34/34`; parity
`16/16`; SurfaceModel `22/22`; source/normal `26/26`; Source Quality `18/18`;
docking `76/76`; Inspection Workspace `63/63`; Validation Set `84/84`;
height distribution `25/25`; smoke parser `25/25`; structure `17/17`; final
Wide/Compact current-build screenshots accepted; both R0 `-ValidateOnly`
modes passed.

Evidence:
`docs/OPENVISIONLAB_3D_SURFACE_MATCH_ACCEPTANCE_BOUNDS_AND_GOLDENS_20260731.md`
and
`artifacts/current/20260731-surface-match-acceptance-bounds-goldens/`.

Boundary / next dependency: `K-02/K-03/K-06` must separately create identified
model/scene 3D-edge artifacts and keep surface and edge scores distinct.
Human-owner R0 remains external for `A-01`.
