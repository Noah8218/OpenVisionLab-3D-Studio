# OpenVisionLab 3D GoPxL Benchmark Approved Direction

Date: 2026-07-31

Status: Approved product direction

Canonical user source:
`C:\Users\user\Downloads\GoPxL_3D_검사_소프트웨어_벤치마킹_보고서.docx`

Historical reviewed-copy label (encoding-damaged in source checkout):
`GoPxL_3D_검사_소프트웨어_벤치마킹_보고서_OpenVisionLab_검토본.docx`

## Decision

GoPxL is benchmark evidence for workflow principles. It is not a UI, theme,
feature list, or platform scope to reproduce.

OpenVisionLab 3D Studio remains a local, file-first, deterministic rule-based
3D inspection workbench. Its normal workflow is:

```text
identified local input
  -> source quality
  -> typed preparation and teaching
  -> explicit Preview / Publish / Run
  -> metrics, overlays, decision, and failure evidence
  -> repeatable validation and durable results
```

## Principles To Adapt

- keep the selected tool, its configuration, Viewer evidence, and result
  context linked;
- treat coordinate frames, transforms, typed artifacts, and validity as
  first-class contracts;
- keep display state separate from inspection input and decision state;
- preserve intermediate evidence and deterministic replay;
- present the current task, status, and next safe action clearly;
- keep support panes collapsible while the Viewer remains the dominant
  teaching and evidence surface.

## Explicit Non-Copy Boundary

Do not copy GoPxL:

- theme or colors;
- exact panel sizes, proportions, topology, or docking defaults;
- product, navigation, tool, or command names;
- icon artwork, assets, screenshots, or code;
- the complete commercial platform scope.

Every benchmark-driven change must identify:

1. the OpenVisionLab operator problem;
2. the abstract workflow principle being adapted;
3. the independent OpenVisionLab design;
4. evidence that the design solves the problem.

Visual similarity to GoPxL is not an acceptance criterion.

## Layout Decision

There is no approved full-layout redesign.

Preserve the current:

- one Job Bar and responsibility rail;
- Authoring, Validate, Results, Calibration, and Advanced responsibilities;
- dominant Viewer;
- collapsible support panes;
- safe Wide/Compact layout profile;
- explicit Preview, Publish, Run, and Validation boundaries.

A local UI change is allowed only when a new inspection result requires
evidence that cannot be understood or reached in the current composition.
It must remain limited to the owning workspace and pass the current-build
Wide `1920 x 1040` and Compact `1280 x 760` layout-integrity gate.

## Current Baseline

The approved direction starts from the completed:

- GoPxL-inspired Workbench v4 `3/3`;
- Viewer single-row and Height color-range work;
- Authoring first-use clarity and side-collapse work;
- Source Quality and declared-normal evidence;
- `J-01/J-03/J-04` identified, deterministic, fail-closed SurfaceModel
  foundation.

Inventory before the approved matching slice:

```text
109 Complete / 17 Partial / 83 Not started / 9 External / 16 Out of scope
```

Completed foundations are preserved and are not reimplemented in GoPxL form.

## Approved Development Order

1. `J-06/J-08/J-09`: identified Prepared Scene, bounded rigid pose search,
   and explicit one-way model-surface coverage semantics.
   Recommended model: `gpt-5.6-sol`.
   Reasoning effort: high.
2. `J-10/J-16`: transformed model overlay and Workbench/Runner pose, score,
   overlay, and hash parity.
   Recommended model: `gpt-5.6-sol`.
   Reasoning effort: high.
3. `J-11/J-14/J-15/M-16`: acceptance limits separated from raw score,
   authored search bounds, rejection/timing evidence, and matching golden
   suites.
   Recommended model: `gpt-5.6-sol`.
   Reasoning effort: high.
4. `K-02/K-03/K-06`: model and scene 3D-edge artifacts plus separate surface
   and edge scores.
   Recommended model: `gpt-5.6-sol`.
   Reasoning effort: high.
5. `K-08/K-11/L-13/M-17`: false-positive review, fixed-fixture performance
   budget, result export, and Release performance evidence.
   Recommended model: `gpt-5.6-sol`.
   Reasoning effort: high.

Human-owner `A-01` Wide/Compact R0 remains a parallel external acceptance
task. It requires the product owner's unaided operation and no model tokens.
It does not globally pause dependency-ready deterministic development.

## Current Scope Boundary

The following remain outside the current approved development scope:

- sensor acquisition, trigger, exposure, and multi-sensor control;
- GoMax or distributed device runtime;
- PLC, industrial I/O, robot, and production-line integration;
- GoHMI or a general operator-screen designer;
- accounts, permissions, audit, and central fleet operation;
- arbitrary plugin, script, or AI platform work;
- physical calibration, certified metrology, uncertainty, or traceability
  claims without independent evidence.

## Prepared Scene Slice Closure

`J-06/J-08/J-09` is Complete. Preserve:

- `docs/OPENVISIONLAB_3D_PREPARED_SCENE_RIGID_POSE_AND_COVERAGE_20260731.md`;
- `artifacts/current/20260731-surface-matching-foundation/`.

Current inventory:

```text
112 Complete / 17 Partial / 80 Not started / 9 External / 16 Out of scope
```

Next: `J-10/J-16` transformed-model Viewer evidence and Workbench/Runner
pose, coverage, overlay, and hash parity. This is a local evidence-integration
slice, not a full layout redesign.

## Matching Slice Acceptance Checklist

The first approved software slice is complete only when:

- [x] Prepared Scene has explicit source-quality, source-content, unit, frame,
      coordinate, parameters, and content identity;
- [x] invalid, inconsistent, or non-finite scene evidence fails closed;
- [x] the pose executor searches a finite deterministic domain and returns a
      rigid model-to-scene transform;
- [x] a known-pose fixture recovers the documented transform within explicit
      tolerance;
- [x] surface coverage names its direction, correspondence rule, distance
      rule, numerator, and denominator;
- [x] an occluded fixture produces the documented expected coverage range;
- [x] no Viewer, recipe, ROI, Preview, Publish, Run, or Validation state is
      changed by preparation or matching;
- [x] Release build, focused Runner verification, affected regressions, and
      code-structure verification pass;
- [x] completion evidence and remaining boundaries are recorded for reuse.

## Overlay and Parity Slice Closure

`J-10/J-16` is Complete. Preserve:

- `docs/OPENVISIONLAB_3D_SURFACE_MATCH_OVERLAY_AND_PARITY_20260731.md`;
- `artifacts/current/20260731-surface-match-overlay-parity/`.

Current inventory:

```text
114 Complete / 17 Partial / 78 Not started / 9 External / 16 Out of scope
```

The shared decision-free execution artifact links the exact model, scene,
pose, raw coverage, and transformed-model overlay identities. Runner and
Workbench prove exact pose/coverage/overlay/execution parity. The Viewer keeps
the dominant work surface and adds only compact OpenVisionLab-specific
geometry and numeric evidence. Current-build Wide and Compact captures pass
the explicit overlap and required-text clipping review.

Next: `J-11/J-14/J-15/M-16` acceptance limits, authored search bounds,
rejection/timing evidence, and matching goldens. Acceptance policy remains
separate from the completed raw score and overlay contracts.

## Acceptance, Bounds, and Goldens Slice Closure

`F-14`, `J-11`, `J-14`, `J-15`, and `M-16` are Complete. Preserve:

- `docs/OPENVISIONLAB_3D_SURFACE_MATCH_ACCEPTANCE_BOUNDS_AND_GOLDENS_20260731.md`;
- `artifacts/current/20260731-surface-match-acceptance-bounds-goldens/`.

Current inventory:

```text
119 Complete / 17 Partial / 73 Not started / 9 External / 16 Out of scope
```

The raw execution remains decision-free. A separate identified recipe policy
produces Pass, Fail, or Rejected with typed reasons. PropertyGrid exposes
acceptance and finite search controls through progressive disclosure; Apply
and reopen do not execute matching. The Viewer links the distinct decision,
authored limits, rejection reason, and observational timing to the same raw
coverage/RMSE/pose/overlay evidence. Current-build Wide and Compact expanded
parameter captures pass the overlap and required-text clipping review.

Next: `K-02/K-03/K-06` identified model and scene 3D-edge artifacts plus
separate surface and edge scores. This continues the approved evidence model;
it is not a GoPxL screen, theme, topology, or asset reproduction.

## Surface-edge Evidence and Review Closure

`K-02`, `K-03`, `K-05`, `K-06`, `K-07`, and `K-08` are Complete. Preserve:

- `docs/OPENVISIONLAB_3D_SURFACE_EDGE_ARTIFACTS_AND_SEPARATE_SCORE_20260731.md`;
- `docs/OPENVISIONLAB_3D_SURFACE_EDGE_DIAGNOSTICS_THRESHOLDS_AND_REVIEW_20260731.md`;
- `artifacts/current/20260731-surface-edge-score/`;
- `artifacts/current/20260731-surface-edge-diagnostic-review/`.

Current inventory:

```text
125 Complete / 17 Partial / 67 Not started / 9 External / 16 Out of scope
```

OpenVisionLab now links the independently authored surface and edge limits to
the same Viewer evidence, explains model/scene/declared-normal diagnostics,
and retains one accepted/rejected surface-only false-positive comparison. It
keeps its own graphite visual roles and information hierarchy. The adapted
principle is evidence continuity, not a copied competitor layout or theme.

Next: `K-11` fixed-fixture matching performance. Acquisition direction `K-04`
remains blocked on `B-12`, and no camera, calibration, or metrology scope is
implied.
