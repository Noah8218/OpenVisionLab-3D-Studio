# OpenVisionLab 3D Next Chat Handoff Prompt

Date: 2026-07-31

Status: Current continuation entry point

## Purpose

Use this document to continue development in a new Codex conversation without
repeating the commercial-video review or reopening completed product slices.

Authoritative detail remains in:

- `docs/OPENVISIONLAB_3D_MASTER_DEVELOPMENT_WORKFLOW_AND_BACKLOG_20260727.md`
  for all `234` backlog items, dependencies, and evidence gates;
- `docs/OPENVISIONLAB_3D_COMMERCIAL_VIDEO_DIRECTION_AND_PRIORITY_20260727.md`
  for the individual review of all 11 supplied commercial videos;
- `docs/OPENVISIONLAB_3D_INDUSTRIAL_UX_AUDIT_20260728.md` for the
  operator-workflow UX findings and current forward-direction summary;
- `docs/OPENVISIONLAB_3D_NEXT_SESSION_HANDOFF.md` for chronological closure
  records.
- `docs/OPENVISIONLAB_3D_GOPXL_BENCHMARK_APPROVED_DIRECTION_20260731.md` for
  the approved benchmark, non-copy, layout, scope, and priority decisions.
- `docs/OPENVISIONLAB_3D_SURFACE_MATCH_OVERLAY_AND_PARITY_20260731.md` for
  the current identified overlay and exact Workbench/Runner parity closure.
- `docs/OPENVISIONLAB_3D_SURFACE_MATCH_ACCEPTANCE_BOUNDS_AND_GOLDENS_20260731.md`
  for the current separate acceptance, finite search, rejection/runtime, and
  matching-golden closure.
- `docs/OPENVISIONLAB_3D_PROPERTY_GRID_THEME_CONSISTENCY_20260731.md` for the
  current parameter-search and PropertyGrid graphite-theme closure and
  reusable control-theme integrity gate.
- `docs/OPENVISIONLAB_3D_SURFACE_EDGE_ARTIFACTS_AND_SEPARATE_SCORE_20260731.md`
  for the current identified model/scene 3D-edge artifacts, false-background
  fixture, separate score, and Viewer evidence closure.
- `docs/OPENVISIONLAB_3D_SURFACE_EDGE_DIAGNOSTICS_THRESHOLDS_AND_REVIEW_20260731.md`
  for the current direction overlay, independent surface/edge limits, retained
  false-positive comparison, and layout evidence closure.

## Paste this request into the next chat

```text
Work in C:\Git\OpenVisionLab-3D-Studio.

Read, in order:
1. AGENTS.md
2. docs/OPENVISIONLAB_3D_NEXT_CHAT_HANDOFF_PROMPT_20260728.md
3. docs/OPENVISIONLAB_3D_NEXT_SESSION_HANDOFF.md
4. docs/OPENVISIONLAB_3D_MASTER_DEVELOPMENT_WORKFLOW_AND_BACKLOG_20260727.md
5. docs/OPENVISIONLAB_3D_COMMERCIAL_VIDEO_DIRECTION_AND_PRIORITY_20260727.md
6. docs/OPENVISIONLAB_3D_INDUSTRIAL_UX_AUDIT_20260728.md
7. docs/OPENVISIONLAB_3D_WORKSPACE_INFORMATION_ARCHITECTURE_REDESIGN_20260729.md
8. docs/OPENVISIONLAB_3D_SETUP_TEACH_WORKSPACE_SEPARATION_20260729.md
9. docs/OPENVISIONLAB_3D_DEDICATED_VALIDATE_WORKSPACE_20260729.md
10. docs/OPENVISIONLAB_3D_DEDICATED_RESULTS_WORKSPACE_20260729.md
11. docs/OPENVISIONLAB_3D_NOVICE_STAGE_NAVIGATION_VIDEO_REVIEW_20260729.md
12. docs/OPENVISIONLAB_3D_STAGE_HOST_INTEGRATION_REPAIR_20260729.md
12a. docs/OPENVISIONLAB_3D_IA4B_OWNER_PATH_REPLAY_20260729.md
12b. docs/OPENVISIONLAB_3D_DIRECT_NOVICE_REPLAY_FINDINGS_20260729.md
12c. docs/OPENVISIONLAB_3D_TEACH_FAILURE_CORRECTION_CONTEXT_20260729.md
12d. docs/OPENVISIONLAB_3D_DIRECT_NOVICE_REPEAT_ANALYSIS_20260729.md
12e. docs/OPENVISIONLAB_3D_ADVANCED_VIEWER_REACTIVATION_20260729.md
12f. docs/OPENVISIONLAB_3D_NOVICE_INFORMATION_HIERARCHY_AND_ACCESSIBILITY_20260729.md
12g. docs/OPENVISIONLAB_3D_VALIDATION_TOP_DOCK_TABS_20260730.md
12h. docs/OPENVISIONLAB_3D_HUMAN_OWNER_R0_EXECUTION_20260729.md
12i. docs/OPENVISIONLAB_3D_VIEWER_COMMAND_BAR_SIMPLIFICATION_20260730.md
12j. docs/OPENVISIONLAB_3D_VIEWER_SINGLE_ROW_AND_HEIGHT_COLOR_RANGE_20260730.md
12k. docs/OPENVISIONLAB_3D_GOPXL_WORKBENCH_V4_LAYOUT_CONTRACT_20260730.md
12l. docs/OPENVISIONLAB_3D_GOPXL_WORKBENCH_V4_EVIDENCE_AND_SAFE_LAYOUT_20260730.md
12m. docs/OPENVISIONLAB_3D_AUTHORING_PANEL_INTEGRITY_AND_SIDE_COLLAPSE_20260731.md
12n. docs/OPENVISIONLAB_3D_GOPXL_FIRST_USE_AUTHORING_CLARITY_20260731.md
12o. docs/OPENVISIONLAB_3D_SOURCE_CHANNEL_AND_DENSE_NORMAL_QUALITY_20260731.md
12p. docs/OPENVISIONLAB_3D_SURFACE_MODEL_PREPARATION_FOUNDATION_20260731.md
12q. docs/OPENVISIONLAB_3D_GOPXL_BENCHMARK_APPROVED_DIRECTION_20260731.md
12r. docs/OPENVISIONLAB_3D_PREPARED_SCENE_RIGID_POSE_AND_COVERAGE_20260731.md
12s. docs/OPENVISIONLAB_3D_SURFACE_MATCH_OVERLAY_AND_PARITY_20260731.md
12t. docs/OPENVISIONLAB_3D_SURFACE_MATCH_ACCEPTANCE_BOUNDS_AND_GOLDENS_20260731.md
12u. docs/OPENVISIONLAB_3D_PROPERTY_GRID_THEME_CONSISTENCY_20260731.md
12v. docs/OPENVISIONLAB_3D_SURFACE_EDGE_ARTIFACTS_AND_SEPARATE_SCORE_20260731.md
12w. docs/OPENVISIONLAB_3D_SURFACE_EDGE_DIAGNOSTICS_THRESHOLDS_AND_REVIEW_20260731.md
13. docs/OPENVISIONLAB_3D_THRESHOLD_MANUAL_CORRECTION_AND_FAILURE_RECORD_20260728.md
14. docs/OPENVISIONLAB_3D_THRESHOLD_ASSISTANT_HARDENING_20260729.md
15. docs/OPENVISIONLAB_3D_THRESHOLD_CORRECTION_RUN_RECORD_20260729.md
16. docs/OPENVISIONLAB_3D_COMPLETENESS_GRID_METRICS_20260729.md
17. docs/OPENVISIONLAB_3D_COMPLETENESS_RESULTS_AND_OVERLAYS_20260729.md
18. docs/OPENVISIONLAB_3D_COMPLETENESS_FAILURE_NAVIGATION_AND_TAB_MAPPING_20260729.md
19. docs/OPENVISIONLAB_3D_COMPLETENESS_VALIDATION_AND_THRESHOLD_ASSISTANCE_20260729.md
20. docs/OPENVISIONLAB_3D_THRESHOLD_REVIEW_APPLY_AND_HELD_OUT_REPLAY_20260728.md
21. docs/OPENVISIONLAB_3D_THRESHOLD_CANDIDATES_AND_ERROR_TABLE_20260728.md

First run git status --short and git log --oneline -5. Preserve the current
uncommitted implementation and do not reset or overwrite unrelated files.
Also preserve the user-owned untracked `3D/TLB/` folder.
Do not touch the user-owned untracked folders under 3D/SSD-Black, 3D/fccsp,
or 3D/새 폴더.

Automated IA-4b is complete. Current Release Wide and Compact videos
explicitly run the five-sample set, expose 3 Pass / 2 Fail / 0 Error, open a
real failure in Teach, return through Results -> Advanced -> Results, and
preserve recipe/selection/dirty/output/validation/Run-Record state. The replay
also repaired the docked Validation view's missing Shell RunRecordContext.

The Teach correction slice resolves the earlier blank Teach Viewer. Current
Release Wide and Compact show the source, ROI, failed sample, rule, reason,
and exact cell summary after Fix in Teach. Compact uses a focused Selected
Tool composition without a manual tab click.

The newer Advanced Viewer reactivation slice closes that P0 software blocker.
Current Release Wide and Compact show the same source, ROI, Viewer controls,
and HUD in Advanced. Both 110-second actual-pointer recordings require a
visible Advanced `ViewerFitAll` control and a visible final Failure Analysis
correction action before completing. Release build passes 0/0 and the focused
Workbench docking verifier passes 55/55.

The novice hierarchy and accessibility slice closes the remaining P1 software
gate. Failure Analysis now leads with failed sample, failed rule, reason, and
next action. Results leads with decision, executed-step summary, and Fix in
Teach. The sample-set action is directly discoverable by its live AutomationId
and localized name in both final actual-pointer layouts; no coordinate
fallback remains. Release build passes 0/0, Workbench passes 58/58,
Validation Set passes 84/84, and both 110-second final videos preserve
3 Pass / 2 Fail / 0 Error, Advanced geometry, and final failure evidence.

The 2026-07-30 top dock-tab slice moves the work-surface strip above Validate
content and applies the OpenVisionLab tab states. The duplicate selected-pane
title is removed for multi-item panes, all eight tabs expose localized names
and stable ContentIds, and actual pointer selection passes. Release build is
0/0, Workbench is 59/59, Validation Set is 84/84, and current Wide/Compact
captures pass. Preserve its focused evidence; earlier full-route videos are
historical for the tab position.

The 2026-07-30 Viewer command-bar slice removes the repeated persistent text
from the Shell layout bar, Viewer display bar, and ROI display-height row.
Common presentation commands are compact icons with selected states, tooltips,
localized accessible names, and stable AutomationIds. Viewer status and
explicit Preview/Publish/Run boundaries remain visible. Release build is 0/0,
Inspection Workspace is 63/63, Workbench is 59/59, Validation Set retains 84
PASS checks, structure is 17/17, and current-source Wide/Compact captures pass.

The newer single-row Viewer slice removes the loaded source-ready row, the
redundant Single-pane title, the Viewer status text row, and the persistent
left measurement HUD. Common controls now share one top row. The right Height
legend adds display-only low/high bounds and AUTO; manual bounds clamp
endpoint colors and linearly remap in-range raw heights without changing the
source, ROI, measurements, recipe, Preview, Publish, or Run state. Release is
0/0; height distribution is 25/25, Inspection Workspace is 63/63, Workbench
is 59/59, Validation Set is 84/84, structure is 17/17, and current Wide,
Compact, and manual-range captures pass.

GoPxL-inspired Workbench v4 is now `3/3` complete. Authoring uses one Job Bar,
a responsive responsibility rail, and a dominant Viewer. Validate and Results
compose their evidence beside the same Viewer. The application uses one
graphite role system and a schema-1 allowlisted presentation profile with
atomic save, validated restore, corrupt/incompatible fallback, and explicit
Reset layout. Release is 0/0; Workbench is 71/71, Inspection Workspace is
63/63, Validation Set is 84/84, Height distribution is 25 checks, structure
is 17/17, and current Wide/Compact Validate/Results captures and layout
reopen/fallback evidence pass.

The 2026-07-31 first-use Authoring clarity closure is the current UI package.
Selected Tool and Source Quality are mutually exclusive, required Wide and
Compact labels no longer clip, and Recipe Chain shows only the current action.
Empty Authoring keeps one primary Viewer CTA, `Open 3D input`; duplicate
source, Viewer-row, no-step, and Selected Tool waiting messages remain hidden.
Input-ready Authoring advances to `2 Select inspection tool` and restores
Source Quality plus one tool-selection context. Task/support panes can
side-auto-hide while the Viewer remains fixed, and collapse/restore is
presentation-only. Release is 0/0; Workbench is 76/76, Inspection Workspace is
63/63, Validation Set is 84/84, structure is 17/17, and current empty and
input-ready plus selected-tool Wide/Compact captures pass overlap and clipping
review.

GoPxL is benchmark evidence, not a screen or theme to copy. Adapt
current-task clarity, linked configuration/Viewer/evidence, progressive
disclosure, and purposeful familiar icons. Preserve independent OpenVisionLab
theme, terminology, panel decisions, assets, and code.

`B-11/B-16 source channels and dense normals` is Complete. C3D, GLB/STL, and
LAS/LAZ expose exactly seven channel decisions with source-specific evidence.
GLB/STL retain declared and partial normals; LAS/LAZ retain intensity and
format-declared RGB. Missing, partial, zero, non-finite, non-unit, reversed,
invalid-index, incomplete-index, and degenerate normal evidence fails closed.
Release is 0/0; focused verification is 26/26, Source Quality is 18/18, the
loading matrix is 128/128, and structure is 17/17.

`J-01/J-03/J-04 SurfaceModel preparation foundation` is Complete. The
schema-1 identified artifact preserves source geometry and declared normals,
owns explicit deterministic triangle-centroid sampling parameters, saves and
loads atomically, and fails closed for invalid points, triangles, normals,
samples, schema, or hashes. Release is 0/0; focused verification is 22/22;
existing source/normal is 26/26; Source Quality is 18/18; structure is 17/17.

`J-06/J-08/J-09 Prepared Scene, rigid pose, and coverage` is Complete.
Prepared Scene owns complete Source Quality and canonical scene identities.
The bounded deterministic search recovers the controlled `30 degree` yaw and
`(10, -4, 2) mm` translation; full coverage is `5/5 = 1.0` and controlled
occlusion is `4/5 = 0.8`. Release is 0/0; focused verification is 28/28;
SurfaceModel is 22/22; source/normal is 26/26; Source Quality is 18/18;
structure is 17/17; Wide/Compact R0 `-ValidateOnly` passes. Inventory is
119 C / 17 P / 73 N / 9 E / 16 O.

`J-10/J-16 transformed-model overlay and Workbench/Runner parity` is
Complete. The schema-1 decision-free execution artifact links model, scene,
pose, raw coverage, and transformed-model overlay identities. The Viewer
shows the complete transformed model, scene samples, correspondences, and
compact coverage/RMSE/pose/hash evidence. Runner and Workbench match exactly
on pose, coverage, overlay, and execution hashes. Release is 0/0; matching is
34/34; parity is 10/10; docking is 76/76; Inspection Workspace is 63/63;
height distribution is 25/25; structure is 17/17; current-build Wide/Compact
captures pass the overlap/clipping review; both R0 `-ValidateOnly` modes pass.

`F-14/J-11/J-14/J-15/M-16 acceptance, bounds, runtime, and goldens` is
Complete. The schema-1 policy and assessment remain separate from raw
execution. The typed PropertyGrid persists acceptance and finite search
controls without execution. The Viewer links raw evidence to distinct
Pass/Fail/Rejected, authored limits, reason, timing, and hashes. Known pose,
controlled occlusion, and out-of-domain goldens pass exact identity checks.
Release is 0/0; acceptance is 14/14; matching is 34/34; parity is 16/16;
docking is 76/76; Inspection Workspace is 63/63; Validation Set is 84/84;
height distribution is 25/25; smoke options are 25/25; structure is 17/17;
current-build Wide/Compact expanded-parameter captures pass; both R0
`-ValidateOnly` modes pass. Inventory is
119 C / 17 P / 73 N / 9 E / 16 O.

The PropertyGrid theme-consistency repair is Complete. Surface Match search,
property-name cells, numeric editors, and interaction roles now use the
existing OpenVision graphite tokens instead of a private light palette. The
theme contract remains view-local and 13 package roles are verified against
their product brushes. Release is 0/0; Recipe Manager/WPG is 38/38; smoke
options are 26/26; docking is 76/76; Inspection Workspace is 63/63;
Validation Set is 84/84; height distribution is 25/25; structure is 17/17;
current-build Wide/Compact/focused-search captures pass theme, overlap, and
clipping review; both R0 `-ValidateOnly` modes pass. Preserve
`docs/OPENVISIONLAB_3D_PROPERTY_GRID_THEME_CONSISTENCY_20260731.md` and
`artifacts/current/20260731-property-grid-theme-consistency/`.

`K-02/K-03/K-06 model/scene 3D-edge artifacts and separate score` is
Complete. Model topology yields stable boundary/crease evidence; complete
organized scenes yield stable height-step evidence; incomplete grids and
non-manifold topology fail closed. A controlled raised square and flat
background both retain `2/2 = 100%` surface coverage while edge coverage
separates to `4/4 = 100%` versus `0/4 = 0%`. Runner verification is `21/21`;
edge Workbench/Runner parity is `12/12`; the existing regression matrix and
Wide/Compact layout checks pass. The Viewer presents Surface and 3D-edge
channels separately and identifies edge evidence as diagnostic only. Preserve
`docs/OPENVISIONLAB_3D_SURFACE_EDGE_ARTIFACTS_AND_SEPARATE_SCORE_20260731.md`
and `artifacts/current/20260731-surface-edge-score/`. Current inventory is
`122 C / 17 P / 70 N / 9 E / 16 O`.

`K-05/K-07/K-08 direction diagnostics, independent thresholds, and retained
false-positive review` is Complete. The overlay links the exact model, scene,
pose, edge score, canonical edge direction, and declared model-normal evidence.
PropertyGrid persists separate surface and edge coverage/RMSE limits without
execution; the overall assessment requires both components and defines no
weighted score. The controlled accepted and rejected cases both preserve
Surface `100%` while Edge separates to `100%` Pass and `0%` Fail. The Viewer
shows the current decision, model/scene/normal legend, and retained comparison.
Release is `0/0`; focused verification is `20/20`; Workbench/Runner and
PropertyGrid parity is `13/13`; edge regression is `21/21`; the existing full
matrix passes. Current-build accepted and rejected Wide/Compact captures pass
overlap/clipping review; both R0 `-ValidateOnly` modes pass. Preserve
`docs/OPENVISIONLAB_3D_SURFACE_EDGE_DIAGNOSTICS_THRESHOLDS_AND_REVIEW_20260731.md`
and `artifacts/current/20260731-surface-edge-diagnostic-review/`. Current
inventory is `125 C / 17 P / 67 N / 9 E / 16 O`.

Immediate software priority: `K-11` fixed-fixture matching performance gate.
Recommended model: gpt-5.6-sol. Reasoning effort: high. `K-04` remains blocked
on `B-12`; `K-09` remains blocked on `J-12`.

Human-owner Wide/Compact R0 remains a parallel external acceptance task.
Prerequisite: owner operation and evidence. Recommended model: none until the
owner evidence exists. Reasoning effort: none. Close A-01 only after owner
evidence; automated checks do not replace it.

Keep surface and edge scores separate, and preserve match acceptance policy
as an interpretation of immutable raw evidence. Do not begin camera
acquisition, reconstruction, calibration, or metrology without a separate
explicit reprioritization and the required external evidence.

Update the master backlog, next-session handoff, this next-chat document, and
a focused completion document only after the evidence gate passes.
Do not commit or push unless I explicitly request it.
```

## Product identity and lifecycle

OpenVisionLab 3D Studio is:

> A local, file-first, deterministic 2.5D/3D rule-based inspection workbench
> for identified height fields, point clouds, and meshes.

Preserve:

```text
load identified source
  -> review source quality
  -> teach typed regions/features
  -> edit typed parameter draft
  -> explicit Apply
  -> explicit Preview/Publish/Run
  -> inspect metrics, overlays, status, and failure evidence
  -> replay Validation Set
  -> save recipe and durable evidence
```

ROI edits, parameter edits, sample-role changes, Viewer changes, visibility
toggles, and threshold selection must never automatically run inspection.

## Scope boundary

Current scope includes local file loading, teaching, deterministic inspection,
Validation Set replay, Runner parity, and durable local evidence.

The supplied videos also show these capabilities, but they remain out of
scope unless the owner explicitly changes product direction:

- camera discovery, exposure, trigger, scan, and sensor SDK control;
- stereo reconstruction and projector control;
- conveyor, encoder, PLC, fieldbus, robot, and HMI integration;
- cloud, accounts, plant management, fleet health, and production databases;
- opaque AI anomaly training;
- automatic execution caused by ordinary editing;
- physical metrology claims without calibration and uncertainty evidence.

Do not add these exclusions as negative marketing copy to the public README.

## Current maturity snapshot

Source: master backlog and focused completion records dated 2026-07-29.

| Measure | Current state |
| --- | --- |
| Master backlog | `234` items |
| Complete | `104` |
| Partial | `17` |
| New | `88` |
| External prerequisite | `9` |
| Out of scope | `16` |
| Inspection Workspace v3 | `7/8`, `87.5%` |
| Remaining Workspace gate | human-owner unaided R0 |
| Physical calibration/metrology | external or unverified |

Keep these denominators separate. Do not turn them into one readiness score.

## Commercial-video lessons already adopted

All 11 supplied GoPxL, SICK Nova, HALCON/MERLIC, Zivid Studio, and Photoneo
videos were individually reviewed.

This prior review is the durable forward product direction, not a historical
appendix. New backlog work must trace to the recorded video lesson and master
item while preserving OpenVisionLab's explicit lifecycle and file-first
scope. The source-by-source evidence lives in the commercial direction
document and the Korean industrial UX audit.

- GoPxL: one understandable inspection chain, one selected-tool surface,
  dominant Viewer, visible outputs, and compact region/parameter teaching;
- SICK Nova: task-oriented presence/completeness tools and clear per-region
  Pass/Fail evidence;
- HALCON surface matching: identified model preparation, bounded search,
  explainable scores, pose evidence, and distinct model/scene phases;
- HALCON edge-supported matching: separate surface and edge score components
  plus false-positive evidence;
- MERLIC: the height image is a first-class fill/completeness teaching view;
- Zivid and Photoneo: source quality, invalid/missing data, range, and
  acquisition limitations must be visible before trusting measurements.

Do not copy vendor visual styling, hardware platforms, free-form graph
complexity, acquisition controls, or implicit execution.

## Completed foundation to preserve

### Workspace and workflow

- Catalog -> Recipe Chain -> Selected Tool -> dominant Viewer;
- shared selected step/input/ROI/output/Viewer-slot identity;
- ROI Review/Apply/Cancel/Delete and same-ID replacement;
- Top orthographic, Perspective, Fit all, and Fit ROI;
- selected-output Show/Pin/Compare;
- single, split, stacked, and pop-out Viewer layouts;
- Thickness `4 x 2` repeat authoring;
- common recipe/execution/ROI shortcuts and last-recipe restoration;
- English public README, application-only GIF, license, and public-safe
  Thickness Coupon sample.

### Source trust and linked teaching

- `SourceQualityReport` and unified Source Quality workspace;
- coordinate-true invalid-cell map and visible overlay;
- full-size native-grid Height Image;
- manual/auto range, palette, fit, zoom, and pan;
- shared Height Image/3D hover;
- synchronized Height Image/3D GridRectangle draw, move, resize, delete,
  Review, Apply, and Cancel.

### Typed regions and preparation

- GridRectangle, point sets, landmarks, and persisted OrientedBox3D;
- OrientedBox3D numeric editor and Perspective/Top/side handles;
- Median Filter and Remove Outlier Pixels;
- Level Surface from one or more reference ROIs with typed transform output;
- affine solve/apply and explicit re-grid.

### Inspection, evidence, and persistence

- Thickness, Warpage, Plane Flatness, Point Pair, Gap/Flush, Volume,
  Cross-section, and Datum Plane Raw-Height Deviation;
- typed metrics, overlays, output identity/freshness, status, and comparison;
- ordered recipe execution, Validation Set, and Runner;
- JSON/HTML/CSV Run Record, history, export, and structured logging;
- executable structure guard and MVVM ownership baseline.

### Threshold work completed

`I-04` through `I-13` and `I-15` are complete:

- durable Good, Bad, and HeldOut sample roles;
- per-step and per-region labeled metric distributions;
- deterministic Minimum, Maximum, and Range candidates;
- exact development-sample error table;
- auditable Held-out exclusion;
- shared Workbench/Runner analyzer;
- explicit candidate Review/Cancel and typed PropertyGrid draft Apply;
- separate ordinary PropertyGrid Apply;
- explicit Held-out-only projected replay;
- portable correction evidence and Workbench/Runner parity;
- typed missing/balance/overlap evidence warnings with exact development
  sample identities and Held-out exclusion;
- published fail-closed Thickness/Warpage mapping coverage;
- role, warning, Review, draft, manual, and PropertyGrid no-auto-run guards.

Latest focused evidence:

- Release build `0/0`;
- Validation Set `72/72`;
- Inspection Workspace `63/63`;
- Shell smoke options `24/24`;
- recipe teaching `28/28`;
- Artifact Navigator/Output Compare `31/31`;
- code structure `17/17`;
- controlled fixture `48` candidates from `4` development samples with `1`
  Held-out excluded;
- selected `Mean Range 2..4`: `4` correct, `0` errors, and `0` Held-out
  decisions;
- candidate `threshold.0ad7b16eaa3d4362` maps
  `MinimumThickness 0->2` and `MaximumThickness 10->4`;
- Workbench/Runner replay `4` development and `1` Held-out with exact
  Held-out identity and `Pass`;
- balanced/missing/imbalanced/overlap fixtures plus Runner report schema
  `1.1`, threshold contract `2.0`, and five explicit mappings.

Preserve:

- `docs/OPENVISIONLAB_3D_THRESHOLD_CANDIDATES_AND_ERROR_TABLE_20260728.md`;
- `docs/OPENVISIONLAB_3D_THRESHOLD_REVIEW_APPLY_AND_HELD_OUT_REPLAY_20260728.md`;
- `docs/OPENVISIONLAB_3D_THRESHOLD_ASSISTANT_HARDENING_20260729.md`;
- `artifacts/current/20260728-threshold-candidates/`;
- `artifacts/current/20260728-threshold-review-heldout/`;
- `artifacts/current/20260729-threshold-assistant-hardening/`.

## Ordered remaining priorities

The master backlog contains the exact item definitions and closure evidence.

### 0. Workspace information architecture

IA-1 Setup/Teach separation, IA-2 dedicated Validate, IA-3 dedicated Results,
IA-4a live stage-host integration, and the automated IA-4b navigation/state
path are complete. The direct novice correction software task is also
complete.

Parallel external acceptance: human-owner unaided Wide/Compact R0.
Prerequisite: owner operation and evidence. Recommended model: none until
evidence exists. Reasoning effort: none.

Immediate software priority: `K-11` fixed-fixture matching performance gate.
Prerequisite: `J-15`, Complete. Recommended model: `gpt-5.6-sol`.
Reasoning effort: high. Then consider `K-10` experiment comparison with
explicit Publish; `K-04` and `K-09` remain dependency-blocked.

Then:

```text
IA-1 Setup/Teach separation [complete]
  -> IA-2 dedicated Validate stage [complete]
  -> IA-3 read-only Results and opt-in Advanced [complete]
  -> IA-4a integration repair [complete]
  -> IA-4b automated failure-to-Teach and return preservation [complete]
  -> direct novice actionable correction [complete]
  -> repeat Wide/Compact direct novice replay [complete]
  -> human-owner unaided Wide/Compact R0
```

The current all-in-one Workspace remains functional regression evidence but
is no longer the approved default. Do not add SurfaceModel UI to that layout.

The navigation/state blocker is repaired. Teach Viewer and selected-failure
correction context are the current software blockers; human-owner R0 follows.

### 0a. R0 owner acceptance gate

The historical all-in-one owner replay was:

```text
New -> source -> Thickness -> Reference ROI -> Measurement ROI
-> parameters -> Preview -> 4 x 2 repeat -> per-Tab review
-> Run -> Save -> reopen
```

It remains historical external evidence. Once IA-1 through IA-3 change the
default workflow, IA-4 must replace it with an unaided
Setup -> Teach -> Validate -> Results replay.

Prerequisite: owner at the current Release application after IA-1 through
IA-3 pass.

### 1. Completeness grid metrics — complete

Completed: `H-02/H-03/H-04`.

Required result:

- a typed rows, columns, X/Z pitch, and initial cell-shape contract;
- deterministic native-grid cell geometry and stable cell identities;
- exact per-cell finite valid/total coverage;
- exact per-cell height statistic relative to an explicit reference;
- known missing-cell and known-height golden fixtures;
- source immutability, explicit lifecycle, and Workbench/Runner parity;
- no per-cell acceptance policy or aggregate result in this slice.

Recommended model: `gpt-5.6-sol`

Reasoning effort: high

### 2. Completeness result and overlays — complete

Completed: `H-05/H-06/H-07`.

The typed optional policy now produces deterministic per-cell Pass/Fail,
passed/failed counts, aggregate status, and the same coordinate-true
green/red overlays in Height Image and 3D. Existing evidence-only H-02
recipes remain compatible. The mixed fixture is `2` Pass / `2` Fail /
aggregate `Fail`; an independent all-valid fixture is aggregate `Pass`.

Recommended model: `gpt-5.6-sol`

Reasoning effort: high

### 3. Failed-cell review and repeated-Tab result mapping — complete

Completed: `H-08/H-10`.

Previous/Next now traverses failed cells in deterministic row-major order,
wraps at both ends, and selects the same stable cell in Height Image and 3D.
All-pass output disables failure navigation. Existing `Tab 1..8 Thickness`
names map to result presentation without changing ordinary Thickness step or
output identities.

Recommended model: `gpt-5.6-sol`

Reasoning effort: high

### 3a. Completeness Validation Set and threshold assistance — complete

Completed: `H-11/H-12/I-14`.

Two Good, two Bad, and one Held-out sample now replay with real
`Pass/Fail/Pass` evidence. Contract `2.1` preserves exact worst-cell
coverage/lower-height/upper-height evidence, three exact typed mappings,
Held-out exclusion, non-mutating Review/Cancel, draft-only Apply, explicit
development replay, and separate Held-out replay.

Recommended model: `gpt-5.6-sol`

Reasoning effort: high

### 4. Remaining preparation and reusable region artifacts

Develop only for a concrete inspection sample:

- `D-03/D-07`: ROI/Crop and Reduce Domain/Mask;
- `D-08/D-09`: height-threshold and saved-background removal;
- `D-13/D-14/D-17`: typed downsample, normals, and quality comparison;
- `E-11/E-12/E-13`: reusable regions, transform propagation, and supported
  selection declarations;
- `E-14/E-15`: GridCircle and GridPolygon after the selection contract;
- `G-11/G-12`: connected regions and typed region metrics.

Every preparation step preserves the source, emits a separate typed output,
records before/after quality, and keeps Preview/Publish explicit.

Recommended model: `gpt-5.6-sol`

Reasoning effort: high

### 5. Presence, fill, and detected-region inspection

After region/completeness foundations:

- `G-13/G-14/G-15/G-16`;
- `L-12`.

Deliver deterministic Presence Check, Fill Height, aggregate child status,
detected-region dimensions, and child-row export. `I-14` Completeness
threshold assistance is already complete and must be preserved.

Recommended model: `gpt-5.6-sol`

Reasoning effort: high

### 6. Surface matching foundation

Dependency sequence:

```text
J-01/J-03/J-04 [complete]
  -> J-06/J-08/J-09 [complete]
  -> J-10/J-11/J-15/J-16
```

Deliver an identified SurfaceModel, deterministic model/scene preparation,
validity checks, rigid pose, explicit surface-coverage score, transformed
overlay, separate acceptance limits, timing/rejection evidence, and
Workbench/Runner parity.

Nominal/actual deviation, affine solving, and Line Fit are not general surface
matching.

Recommended model: `gpt-5.6-sol`

Reasoning effort: high

### 7. Matching optimization and diagnostics

Only after the matching foundation:

- `J-05/J-07/J-12/J-13/J-14`;
- `K-02` through `K-11`;
- `L-13`.

This covers model cleanup/keypoints, multiple matches, symmetry, bounded
search, model/scene edges, normal/viewpoint diagnostics, separate
surface/edge scores, false-positive review, experiment comparison, and a
fixed-fixture performance budget.

Recommended model: `gpt-5.6-sol`

Reasoning effort: high

### 8. Cross-cutting reliability and UX

Close alongside the owning feature:

- `A-09` through `A-15`: state language, useful lower panes, assistant host,
  first-use checklist, and keyboard coverage;
- `B-10` through `B-14`: malformed-source diagnostics, channels, provenance,
  source gates, and preparation deltas;
- `C-17` through `C-21`: linked Viewer and comparison options;
- `L-09/L-10/L-14`: timing, Source Quality in Run Record, and support bundle;
- `M-09` through `M-19`: malformed, atomicity, region, preparation,
  no-leakage, completeness, matching, performance, accessibility, and
  localization gates.

Recommended model: `gpt-5.6-sol`

Reasoning effort: medium for narrow established UI/test work; high for
cross-module, numerical, or renderer work.

### 9. Physical measurement credibility

Blocked until the owner supplies calibrated data, unit/scale evidence,
physical datum, repeated acquisitions, and production tolerance decisions.

Then address `N-02` through `N-08`: datum, scale, traceability, uncertainty,
GR&R, tolerance, and approved claim wording.

Prerequisite first; do not spend model tokens before it exists.

## Completed H-11/H-12/I-14 contract

Ownership:

- Core: extend the shared threshold evidence contract only where a typed
  Completeness metric-to-parameter mapping is actually supported;
- Tools/Runner: preserve the H-02 through H-10 cell/result contract and emit
  deterministic per-sample/per-cell evidence;
- Workbench: reuse Good/Bad/Held-out roles, explicit Run, candidate Review,
  Cancel, Apply, development replay, and separate Held-out replay;
- Viewer/Height Image: retain H-08 selected-cell review without owning
  threshold policy.

Required sequence:

```text
identified Good + Bad development samples and Held-out sample
  -> explicit Validation Set Run
  -> exact Completeness sample/cell metric table
  -> supported deterministic candidate generation
  -> explicit Review / Cancel / Apply
  -> development-only replay
  -> separate explicit Held-out replay
```

Do not fabricate a failure, include Held-out in candidate boundaries, add
detected-region routing, trigger implicit execution, mutate source data, or
claim calibration/metrology in this slice.

## H-11/H-12/I-14 completion checklist

- [x] At least one Good, one Bad, and one Held-out Completeness sample replays.
- [x] Held-out is visible but excluded from candidate boundaries and ranking.
- [x] Every supported candidate owns exact sample, cell, metric, value, role,
  decision, and confusion evidence.
- [x] Unsupported Completeness metrics produce no misleading candidate.
- [x] Review and Cancel do not change recipe parameters or execute inspection.
- [x] Apply changes only mapped Completeness parameters and makes evidence
  stale until explicit development replay.
- [x] Development replay and Held-out replay remain separate explicit actions.
- [x] H-08 failed-cell review and ordinary Thickness identities remain intact.
- [x] Existing I-04 through L-11 evidence remains readable and unchanged.
- [x] Release build and structure regression pass.
- [x] Fresh before/after UI captures pass quality review.
- [x] Master backlog, handoff, and completion record are updated.

## Working-tree warning

The workspace contains the current uncommitted Level Surface, labeled-sample,
threshold-candidate, UI, Runner, and documentation changes. Inspect and
preserve them. Do not reset, discard, overwrite, or stage unrelated work.

These untracked folders are user-owned and must remain untouched:

- `3D/SSD-Black/`;
- `3D/fccsp/`;
- `3D/새 폴더/`.

No commit or push is authorized by this handoff.

## Durable documentation rule

After completing a slice:

1. update its master-backlog status;
2. keep inventory counts totaling `234`;
3. put the newest closure at the top of the next-session handoff;
4. update this document's snapshot and immediate priority;
5. update commercial-video reconciliation only when capability state changes;
6. create one focused completion record with commands, results, artifacts,
   boundaries, and next dependency;
7. keep R0 and physical-metrology prerequisites explicit.

## Handoff completion record

Status: Complete

Scope: current implementation, all commercial-video-derived priority trains,
the completed I-09/I-11, I-12/I-13/I-15, L-11, and H-02 through H-12/I-14
boundaries, completed automated IA-4b owner path, next-chat startup request,
repository warnings, and durable update rules.

Acceptance criteria: current counts recorded -> pass; video-derived trains
represented -> pass; immediate item and dependencies explicit -> pass;
external/out-of-scope boundaries explicit -> pass; copy/paste request
included -> pass; user-owned folders recorded -> pass.

Verification: cross-checked against `AGENTS.md`, the current next-session
handoff, the `234`-item master backlog, the 11-video direction document, and
the current threshold/completeness completion records.

Evidence: linked documents,
`docs/OPENVISIONLAB_3D_WORKSPACE_INFORMATION_ARCHITECTURE_REDESIGN_20260729.md`,
`docs/OPENVISIONLAB_3D_DEDICATED_RESULTS_WORKSPACE_20260729.md`,
`docs/OPENVISIONLAB_3D_NOVICE_STAGE_NAVIGATION_VIDEO_REVIEW_20260729.md`,
`docs/OPENVISIONLAB_3D_STAGE_HOST_INTEGRATION_REPAIR_20260729.md`,
`docs/OPENVISIONLAB_3D_IA4B_OWNER_PATH_REPLAY_20260729.md`,
`docs/OPENVISIONLAB_3D_DIRECT_NOVICE_REPLAY_FINDINGS_20260729.md`,
`docs/OPENVISIONLAB_3D_TEACH_FAILURE_CORRECTION_CONTEXT_20260729.md`,
`artifacts/current/20260729-completeness-grid-metrics/`, and
`artifacts/current/20260729-completeness-results-overlays/`, and
`artifacts/current/20260729-completeness-failure-navigation/`, and
`artifacts/current/20260729-results-workspace-extraction/`, and
`artifacts/current/20260729-novice-stage-navigation-video-review/`, and
`artifacts/current/20260729-stage-host-integration-repair/`, and
`artifacts/current/20260729-ia4b-owner-path-replay/`, and
`artifacts/current/20260729-direct-novice-r0-replay/`, and
`artifacts/current/20260729-teach-failure-correction/`.

Boundary / next dependency: human-owner Wide/Compact R0 remains required for
`A-01` acceptance but does not pause dependency-ready software work.
SurfaceModel `J-01/J-03/J-04` and matching foundation `J-06/J-08/J-09` are
Complete; `J-10/J-16` is now eligible.
Physical calibration/metrology remain external.
