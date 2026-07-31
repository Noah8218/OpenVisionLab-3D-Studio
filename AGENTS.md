# AGENTS.md

This file defines the working agreement for Codex in this repository.

## Work Location

- Primary work happens in `C:\Git\OpenVisionLab-3D-Studio`.
- `C:\Git\OpenVisionLab_Dev` is the 2D reference repository. Read it for product contracts, UX direction, validation style, and naming patterns.
- Do not modify, stage, commit, or prepare promotion work in `C:\Git\OpenVisionLab_Dev` unless the user explicitly asks for that step.
- Do not run `git push` unless the user explicitly requests it.

## Product Identity

- OpenVisionLab 3D Studio is a rule-based 3D inspection workbench.
- The product is not just a model viewer. The viewer is the first foundation for teaching, measuring, comparing, and validating 3D inspection rules.
- The 2D reference product validates image layers with tools, metrics, overlays, acceptance rules, recipes, and repeatable runner checks. The 3D product should keep that operating model, but use 3D entities instead of images.
- Early scope is local desktop work: load 3D data, inspect it, show overlays/measurements, and build repeatable rule-based validation. Do not start with camera, PLC, robot, cloud, or production-line integration.

## Commercial Benchmark Boundary

- Commercial products, including GoPxL, are evidence for workflow principles,
  not templates to reproduce.
- Learn from current-task clarity, linked configuration/Viewer/evidence,
  progressive disclosure, familiar purposeful icons, collapsible support
  panes, and explicit status/next-action feedback.
- Do not copy a competitor's theme, colors, exact panel proportions, screen
  topology, names, assets, icon artwork, or code. Preserve an independent
  OpenVisionLab visual system and terminology.
- Every benchmark-driven change must name the OpenVisionLab operator problem,
  the abstract principle being adapted, and why the resulting design fits this
  product. Similarity to a competitor screenshot is not an acceptance gate.

## Human R0 And Continued Software Development

- The product owner's unaided Wide/Compact R0 remains required to close
  `A-01`, Workspace v3 `8/8`, and any human-usability or release-acceptance
  claim.
- By explicit product-owner direction on 2026-07-31, missing R0 evidence is
  not a global pause on software development. Continue with dependency-ready,
  deterministic work that can be closed using source, fixtures, reports,
  Runner parity, and current-build evidence.
- Keep R0 listed as an external acceptance task and preserve its current
  package. Do not imply that automated verification replaces human operation.
- When a new software slice changes the R0 binary set, rebuild, refresh the
  fixed hashes, and rerun both `-ValidateOnly` modes before handoff.

## Public README And Product Documentation

- Write the root `README.md` in English.
- Lead with supported inspection workflows and user value. Do not use lists of
  unsupported camera, lighting, PLC, industrial I/O, robot, cloud, or production
  systems as README or public product-documentation copy.
- Name bundled examples by their inspection task, such as `Thickness Coupon`,
  rather than by how the data was produced. Keep generation provenance,
  reproducibility details, and data-safety evidence in development evidence
  documents rather than the user-facing README.
- README GIFs and screenshots must show only the application window. They must
  not expose the operator desktop, taskbar, unrelated applications, file paths,
  account information, or notifications.
- Inspection demonstrations must teach geometrically meaningful regions. A
  Thickness example must keep its paired Reference and Measurement ROI on the
  intended surfaces of the same visible part.
- The repository root must include the approved project license and attribution
  notice. The README must identify the license and link to it.

## UI Layout Integrity Gate

- Every change that affects UI, UX, layout, visible text, navigation, docking,
  or responsive behavior must be checked in a current build at both supported
  `Wide 1920 x 1040` and `Compact 1280 x 760` sizes.
- The check must explicitly look for overlapping controls, clipped or
  truncated required labels/actions, controls rendered outside their pane,
  unreachable controls, and unintended horizontal or nested scroll bars.
- Required navigation, task, step, field, status, and command names must be
  given enough space, wrap, or use an adaptive replacement. Do not use text
  trimming to hide required meaning. Trimming is allowed only for secondary
  identifiers such as paths or internal IDs, and the full value must remain
  available through a tooltip or detail surface.
- Docked, tabbed, expanded, collapsed, empty, loaded, and long localized-text
  states that the change can affect must be included in the smallest practical
  verification matrix. A panel collapse or restore must not execute Preview,
  Publish, Run, Validation, or mutate recipe/source/ROI state.
- A UI task is not `Complete` until fresh current-build before/after evidence
  has been visually compared and the final supported-size captures contain no
  unexplained overlap or required-text clipping. Record the checked sizes,
  state, command or test, evidence paths, and any intentional adaptive
  substitution in the closure document.

## Current Product Target

- Surface-edge artifacts and separate score closure (2026-07-31): `K-02`,
  `K-03`, and `K-06` are Complete. Core owns schema-1 identified model-edge,
  complete-organized-scene-edge, and separate surface/edge score artifacts
  with fail-closed identity and execution-link validation. Data owns atomic
  JSON persistence. Tools owns deterministic topology boundary/crease
  extraction, organized height-step extraction, and positional edge scoring
  at the immutable surface pose. A controlled raised-square and flat-background
  pair both retain `2/2 = 1.0` surface coverage while edge coverage separates
  to `4/4 = 1.0` versus `0/4 = 0.0`. Runner focused verification passes
  `21/21`; Workbench/Runner edge parity passes `12/12`; existing matching
  passes `34/34`; acceptance `14/14`; SurfaceModel `22/22`; source/normal
  `26/26`; Source Quality `18/18`; docking `76/76`; Inspection Workspace
  `63/63`; Validation Set `84/84`; height distribution `25/25`; WPG `38/38`;
  smoke options `26/26`; structure `17/17`; Release builds `0/0`. Current
  application-only Wide `1920 x 1040` and Compact `1280 x 760` captures pass
  the overlap/clipping review with separate Surface and 3D-edge rows. Preserve
  `docs/OPENVISIONLAB_3D_SURFACE_EDGE_ARTIFACTS_AND_SEPARATE_SCORE_20260731.md`
  and `artifacts/current/20260731-surface-edge-score/`. Inventory is
  `122 C / 17 P / 70 N / 9 E / 16 O`; human-owner R0 remains external for
  `A-01`. Refreshed fixed hashes pass both Wide/Compact `-ValidateOnly`
  modes. Next: `K-05/K-07/K-08` with `gpt-5.6-sol`, high. `K-04` remains
  blocked on `B-12`; edge score is diagnostic and does not alter authored
  surface acceptance.

- PropertyGrid theme-consistency closure (2026-07-31): the Surface Match
  parameter search, property-name cells, numeric editors, and interaction
  states now use the existing OpenVision graphite semantic roles instead of a
  view-local light palette. The package contract remains view-local, while 13
  surface/text/editor/focus/read-only/disabled aliases are checked against the
  owning product brushes. Search normal and keyboard-focus states have stable
  automation IDs and current-build evidence. Release passes `0/0`; Recipe
  Manager/WPG passes `38/38`; smoke options pass `26/26`; docking passes
  `76/76`; Inspection Workspace passes `63/63`; Validation Set passes `84/84`;
  height distribution passes `25/25`; and structure passes `17/17`. Current
  application-only Wide `1920 x 1040`, Compact `1280 x 760`, and Compact
  focused-search captures pass the overlap/clipping/theme review. Preserve
  `docs/OPENVISIONLAB_3D_PROPERTY_GRID_THEME_CONSISTENCY_20260731.md` and
  `artifacts/current/20260731-property-grid-theme-consistency/`. Inventory
  remains `119 C / 17 P / 73 N / 9 E / 16 O`; human-owner R0 remains external
  for `A-01`. Refreshed fixed hashes pass both `-ValidateOnly` modes. Next:
  `K-02/K-03/K-06` with `gpt-5.6-sol`, high.

- Surface-match acceptance, authored bounds, and goldens closure (2026-07-31):
  `F-14`, `J-11`, `J-14`, `J-15`, and `M-16` are Complete. Core owns the
  schema-1 identified acceptance policy and assessment, fail-closed authored
  pose-search validation, typed decision/reason, and observational three-stage
  runtime report. Data owns validated atomic assessment/runtime persistence;
  Tools owns the shared raw-execution then separate-acceptance boundary used
  by Runner and Workbench. The Surface Match PropertyGrid separates limits
  from finite rotation/translation/search controls; Apply and reopen do not
  execute. The Viewer keeps raw score/pose/overlay evidence distinct from
  Pass/Fail/Rejected, authored limits, reason, and timing. Known pose passes,
  controlled occlusion fails, and out-of-domain pose rejects with exact
  assessment identities. Release passes `0/0`; acceptance passes `14/14`;
  matching passes `34/34`; parity passes `16/16`; SurfaceModel passes `22/22`;
  source/normal passes `26/26`; Source Quality passes `18/18`; docking passes
  `76/76`; Inspection Workspace passes `63/63`; Validation Set passes
  `84/84`; height distribution passes `25/25`; smoke options pass `25/25`;
  and structure passes `17/17`. Current-build Wide `1920 x 1040` and Compact
  `1280 x 760` expanded-parameter captures pass the overlap/clipping review.
  Preserve
  `docs/OPENVISIONLAB_3D_SURFACE_MATCH_ACCEPTANCE_BOUNDS_AND_GOLDENS_20260731.md`
  and
  `artifacts/current/20260731-surface-match-acceptance-bounds-goldens/`.
  Inventory is `119 C / 17 P / 73 N / 9 E / 16 O`; human-owner R0 remains
  external for `A-01`, and refreshed fixed hashes pass both `-ValidateOnly`
  modes. Next: `K-02/K-03/K-06` with `gpt-5.6-sol`, high. Keep surface and
  edge scores separate and preserve raw evidence independent of acceptance.

- Earlier surface-match overlay and Workbench/Runner parity closure (2026-07-31):
  `J-10` and `J-16` are Complete. Core owns the schema-1 identified
  transformed-model overlay and decision-free execution artifact; Data owns
  validated atomic JSON persistence; Tools owns the shared deterministic
  execution boundary used by Runner and Workbench. The Viewer renders neutral
  Prepared Scene samples, the complete transformed model wireframe, raw
  correspondences, and compact coverage/RMSE/pose/hash evidence without
  defining Pass/Fail. The controlled fixture recovers the documented
  `30 degree` yaw and `(10, -4, 2) mm` translation with `5/5 = 1.0`
  coverage. Runner and Workbench match exactly on pose, coverage, overlay,
  and execution hashes. Release passes `0/0`; matching passes `34/34`;
  parity passes `10/10`; SurfaceModel regression passes `22/22`;
  source/normal passes `26/26`; Source Quality passes `18/18`; docking passes
  `76/76`; Inspection Workspace passes `63/63`; height distribution passes
  `25/25`; and structure passes `17/17`. Current-build Wide `1920 x 1040`
  and Compact `1280 x 760` captures pass the overlap/clipping review. Preserve
  `docs/OPENVISIONLAB_3D_SURFACE_MATCH_OVERLAY_AND_PARITY_20260731.md` and
  `artifacts/current/20260731-surface-match-overlay-parity/`. Inventory is
  `114 C / 17 P / 78 N / 9 E / 16 O`. Human-owner R0 remains external for
  `A-01`; refreshed fixed hashes pass both `-ValidateOnly` modes. Next:
  `J-11/J-14/J-15/M-16` with `gpt-5.6-sol`, high. Keep acceptance policy
  separate from the raw score and overlay contract.

- Earlier Prepared Scene, rigid pose, and coverage closure (2026-07-31): `J-06`,
  `J-08`, and `J-09` are Complete. Core owns the schema-1 identified
  Prepared Scene, canonical Source Quality/scene identities, rigid
  model-to-scene pose/result contract, and explicit decision-free one-way
  coverage evidence. Data owns validated atomic Prepared Scene JSON
  persistence. Tools owns pure scene preparation, bounded deterministic
  Euler/centroid pose search, and unique-nearest coverage scoring. The
  asymmetric five-sample fixture recovers the documented `30 degree` yaw and
  `(10, -4, 2) mm` translation; full coverage is `5/5 = 1.0` and controlled
  occlusion is `4/5 = 0.8`. Release passes `0/0`; matching passes `28/28`;
  SurfaceModel regression passes `22/22`; source/normal passes `26/26`;
  Source Quality passes `18/18`; and structure passes `17/17`. The refreshed
  fixed-hash R0 package passes Wide/Compact `-ValidateOnly`. Preserve
  `docs/OPENVISIONLAB_3D_PREPARED_SCENE_RIGID_POSE_AND_COVERAGE_20260731.md`,
  `docs/OPENVISIONLAB_3D_GOPXL_BENCHMARK_APPROVED_DIRECTION_20260731.md`, and
  `artifacts/current/20260731-surface-matching-foundation/`. Inventory is
  `112 C / 17 P / 80 N / 9 E / 16 O`. Human-owner R0 remains external for
  `A-01`. Next: `J-10/J-16` with `gpt-5.6-sol`, high. Do not combine that
  Viewer/parity slice with Pass/Fail acceptance policy.

- Earlier SurfaceModel preparation foundation closure (2026-07-31): `J-01`, `J-03`,
  and `J-04` are Complete. Core owns the schema-1 identified artifact,
  canonical SHA-256, deterministic even-index triangle schedule, and
  fail-closed point/triangle/normal/sample report. Data owns atomic validated
  JSON save/load. Tools preserves imported geometry and declared normals and
  creates deterministic triangle-centroid samples only after `B-16` passes.
  No repair, internal-surface removal, pose search, score, or UI is included.
  Release passes `0/0`; SurfaceModel verification passes `22/22`; existing
  source/normal verification passes `26/26`; Source Quality passes `18/18`;
  and structure passes `17/17`. Preserve
  `docs/OPENVISIONLAB_3D_SURFACE_MODEL_PREPARATION_FOUNDATION_20260731.md`
  and
  `artifacts/current/20260731-surface-model-foundation/`.
  At this checkpoint inventory was `109 C / 17 P / 83 N / 9 E / 16 O`,
  human-owner R0 remained external for `A-01`, and `J-06/J-08/J-09` was next.
  The newer matching checkpoint above supersedes that inventory and priority.

- Source-channel and dense-normal quality closure (2026-07-31): `B-11` and
  `B-16` are Complete. C3D, GLB/STL, and LAS/LAZ now expose exactly seven
  source-channel decisions with explicit evidence and never promote Viewer
  colors or calculated normals to source data. GLB, ASCII STL, and binary STL
  retain declared normals, including partial presence. LAS/LAZ sampled points
  retain intensity and RGB availability follows the declared point format.
  The WPF-neutral schema-1 normal report fails closed for missing, partial,
  non-finite, zero, non-unit, reversed, invalid-index, and degenerate input.
  Release passes `0/0`; focused source/normal verification passes `26/26`,
  Source Quality passes `18/18`, the full loading matrix passes `128/128`,
  and structure passes `17/17`. Preserve
  `docs/OPENVISIONLAB_3D_SOURCE_CHANNEL_AND_DENSE_NORMAL_QUALITY_20260731.md`
  and
  `artifacts/current/20260731-source-channel-normal-quality/`.
  At this checkpoint inventory was `106 C / 17 P / 86 N / 9 E / 16 O` and
  `J-01/J-03/J-04` was next. The newer SurfaceModel checkpoint above
  supersedes that inventory and priority.

- GoPxL-inspired first-use Authoring clarity closure (2026-07-31): empty
  Authoring now exposes one primary action in the Viewer, `Open 3D input`.
  Recipe Chain shows only the current step rather than the complete four-step
  sentence, and advances from `1 Open 3D input` to `2 Select inspection tool`
  and then `3 Set ROI -> 4 Preview` from existing source/selection state.
  Before input is ready, the duplicate source card, Viewer command row,
  no-step ribbon, and Selected Tool waiting card stay hidden. After input is
  ready, Source Quality and one tool-selection context return without
  executing or mutating recipe, ROI, Preview, Publish, Run, or Validation.
  Release passes `0/0`, Workbench docking `76/76`, Inspection Workspace
  `63/63`, Validation Set `84/84`, and structure `17/17`. Current
  application-only empty, input-ready, and selected-tool captures pass
  first-attempt quality and visual overlap/clipping review at Wide
  `1920 x 1040` and Compact `1280 x 760`. Preserve
  `docs/OPENVISIONLAB_3D_GOPXL_FIRST_USE_AUTHORING_CLARITY_20260731.md`
  and
  `artifacts/current/20260731-gopxl-first-use-authoring-clarity/`.
  Inventory remains `104 C / 17 P / 88 N / 9 E / 16 O`; `A-01` remains
  Partial only for the human owner's unaided Wide/Compact R0. The refreshed
  fixed-hash package and both `ValidateOnly` checks are current. After owner
  R0 passes, begin `J-01/J-03/J-04 SurfaceModel` with `gpt-5.6-sol`, high.

- Authoring panel integrity and side-collapse closure (2026-07-31): the loaded
  Selected Tool now resolves Source Quality visibility through the Workbench
  owner, so the two complete surfaces cannot overlap. Deterministic expanders,
  one vertical scroll owner, a 140-pixel labeled Wide rail, and a compact
  adaptive `한`/`EN` language value keep required labels and actions readable.
  Authoring exposes `1 Input -> 2 Select tool -> 3 ROI -> 4 Preview`.
  Workbench and Advanced task/support tabs can side-auto-hide while the
  dominant Viewer remains fixed; collapse/restore is presentation-only.
  Release passes `0/0`, Workbench docking `75/75`, Inspection Workspace
  `63/63`, Validation Set `84/84`, Height distribution `25/25`, and structure
  `17/17`. Current application-only Wide `1920 x 1040`, Compact
  `1280 x 760`, and Selected Tool collapsed captures pass first-attempt quality
  and visual overlap/clipping review. Preserve
  `docs/OPENVISIONLAB_3D_AUTHORING_PANEL_INTEGRITY_AND_SIDE_COLLAPSE_20260731.md`
  and
  `artifacts/current/20260731-authoring-panel-integrity-and-collapse/`.
  Inventory remains `104 C / 17 P / 88 N / 9 E / 16 O`; `A-01` remains
  Partial only for the human owner's unaided Wide/Compact R0. The fixed-hash
  package and both `ValidateOnly` checks are current. After owner R0 passes,
  begin `J-01/J-03/J-04 SurfaceModel` with `gpt-5.6-sol`, high.

- GoPxL-inspired Workbench v4 evidence and safe-layout closure (2026-07-30):
  v4 is now `3/3` complete. Validate composes Samples, Run Results, Failure
  Analysis, Threshold Review, and Held-out beside the same dominant Viewer;
  staged-sample selection is presentation-only and explicit sample execution
  remains unchanged. Results leads with decision/step/next-action evidence and
  exposes read-only Run Record, Output Compare, and Reports beside the Viewer.
  The Shell and Viewer now share a graphite role system while scientific
  height colors remain independent. A schema-1 allowlisted profile persists
  only safe Wide/Compact pane ratios, selected stable pane IDs, and valid
  window placement. Atomic save, validation, corrupt/incompatible fallback,
  no-auto-overwrite for unsafe profiles, and explicit Reset layout preserve
  recipe/source/ROI/Preview/Run boundaries. Release passes `0/0`, Workbench
  docking `71/71`, Validation Set `84/84`, Inspection Workspace `63/63`,
  Height distribution `25` checks, and structure `17/17`. Current
  application-only Validate/Results captures pass Wide `1920 x 1040` and
  Compact `1280 x 760`; Missing -> Restored and corrupt-profile fallback
  reopen evidence preserves no draft, ROI capture, Preview, or Validation run.
  Preserve
  `docs/OPENVISIONLAB_3D_GOPXL_WORKBENCH_V4_EVIDENCE_AND_SAFE_LAYOUT_20260730.md`
  and
  `artifacts/current/20260730-gopxl-workbench-v4-evidence-and-layout/`.
  Inventory remains `104 C / 17 P / 88 N / 9 E / 16 O`; `A-01` remains
  Partial only for the human owner's unaided Wide/Compact R0. The fixed-hash
  package and launcher validation are current. After owner R0 passes, begin
  `J-01/J-03/J-04 SurfaceModel` with `gpt-5.6-sol`, high.

- GoPxL-inspired Workbench v4-1 Shell and Authoring closure (2026-07-30):
  the application now uses one 56-pixel Job Bar and a responsive left
  responsibility rail instead of horizontal stage navigation plus a repeated
  full-width workspace metadata/command row. Setup and Teach are one visible
  Authoring entry while existing internal state and explicit action contracts
  remain. Wide orders Tool Library/Recipe Chain, Selected Tool, and dominant
  Viewer/Displayed Outputs; Compact uses a 60-pixel icon rail, one support tab
  group, and dominant Viewer. Preview, Publish, Cancel, and Save now belong to
  Selected Tool. Release passes `0/0`, Workbench docking `64/64`, Inspection
  Workspace `63/63`, Validation Set `84/84`, and structure `17/17`. Current
  application-only Wide `1920 x 1040` and Compact `1280 x 760` captures pass
  on the first quality attempt. Preserve
  `docs/OPENVISIONLAB_3D_GOPXL_WORKBENCH_V4_LAYOUT_CONTRACT_20260730.md` and
  `artifacts/current/20260730-gopxl-workbench-v4-shell/`. Inventory remains
  `104 C / 17 P / 88 N / 9 E / 16 O`. This v4-1 checkpoint and its former
  v4-2/v4-3 priority are superseded by the complete v4 checkpoint above.

- Viewer single-row and Height color-range closure (2026-07-30): the normal
  loaded Single Viewer now uses one common top command row instead of stacked
  source, pane-title, and Viewer-status rows. The persistent left measurement
  HUD is removed while the orientation gizmo remains. The right Height legend
  now exposes accessible low/high decrement, value, increment, and AUTO
  controls. A manual interval clamps endpoint colors and linearly remaps
  in-range raw heights without changing source data, ROI, measurement, recipe,
  Preview, Publish, or Run state. Release build passes `0/0`; height
  distribution passes `25/25`, Inspection Workspace `63/63`, Workbench
  docking `59/59`, Validation Set `84/84`, and structure `17/17`. Current
  application-only Wide `1920 x 1040`, Compact `1280 x 760`, and manual
  `11.00..12.50` captures pass on the first quality attempt. Preserve
  `docs/OPENVISIONLAB_3D_VIEWER_SINGLE_ROW_AND_HEIGHT_COLOR_RANGE_20260730.md`
  and
  `artifacts/current/20260730-viewer-single-row-height-range/`. Inventory
  remains `104 C / 17 P / 88 N / 9 E / 16 O`; `A-01` remains Partial for a
  new human-owner unaided R0 on this updated binary set. After owner R0 passes,
  begin `J-01/J-03/J-04 SurfaceModel` with `gpt-5.6-sol`, high.

- Viewer command-bar simplification closure (2026-07-30): the Shell
  Viewer-layout bar and Viewer display bar now use compact, familiar icons for
  Height Image, layout, HUD, projection, fit, overflow, and view-only ROI
  display height. Current states remain visible, every icon-only control has a
  tooltip, localized accessible name, and stable AutomationId, and Viewer
  status feedback remains explicit. Release build passes `0/0`, Inspection
  Workspace passes `63/63`, Workbench docking passes `59/59`, Validation Set
  retains `84` PASS checks, structure passes `17/17`, and current-source Wide
  `1920 x 1040` and Compact `1280 x 760` captures pass on the first quality
  attempt. Preserve
  `docs/OPENVISIONLAB_3D_VIEWER_COMMAND_BAR_SIMPLIFICATION_20260730.md` and
  `artifacts/current/20260730-viewer-command-bar-simplification/`. Inventory
  remains `104 C / 17 P / 88 N / 9 E / 16 O`; `A-01` remains Partial for a
  new human-owner unaided R0 on this updated binary set. After owner R0 passes,
  begin `J-01/J-03/J-04 SurfaceModel` with `gpt-5.6-sol`, high.

- Validation top dock-tab closure (2026-07-30): the multi-pane AvalonDock
  work-surface strip now appears above Validate content instead of at the
  bottom window edge. The strip uses the OpenVision Command Bar, Divider,
  Selected Surface, Accent, Focus, and Disabled tokens; a multi-item pane no
  longer repeats the selected title in a second dark header, while a
  single-item pane retains its normal title. All eight visible tabs expose
  localized accessible names and stable ContentIds, and actual pointer
  selection of Output Compare passes. Release build passes `0/0`, Workbench
  docking passes `59/59`, Validation Set passes `84/84`, and current
  application-only captures pass Wide `1920 x 1040` and Compact `1280 x 760`
  with all eight tabs visible on one row. Preserve
  `docs/OPENVISIONLAB_3D_VALIDATION_TOP_DOCK_TABS_20260730.md` and
  `artifacts/current/20260730-validation-top-tabs/`. Inventory remains
  `104 C / 17 P / 88 N / 9 E / 16 O`; `A-01` remains Partial only for a new
  human-owner unaided R0 on this updated binary set. After owner R0 passes,
  begin `J-01/J-03/J-04 SurfaceModel` with `gpt-5.6-sol`, high.

- Novice information hierarchy and live accessibility closure (2026-07-29):
  Failure Analysis now leads with failed sample, failed rule, reason, and next
  action before the detailed sample/step/metric/overlay evidence. Results
  leads with the decision, executed-step summary, and an explicit Fix in
  Teach route before Run Record sidecars, paths, export, and Advanced.
  `ValidationSetRunAllButton` now has one stable stage-navigation owner and is
  directly found by name and AutomationId in both actual-pointer layouts;
  the prior coordinate fallback is gone. Release build passes `0/0`,
  Workbench docking passes `58/58`, Validation Set passes `84/84`, and final
  application-only Wide `1920 x 1040` and Compact `1280 x 760` videos pass at
  15 fps / 110 s with `3 Pass / 2 Fail / 0 Error`, Advanced geometry, and
  final failure preservation. Preserve
  `docs/OPENVISIONLAB_3D_NOVICE_INFORMATION_HIERARCHY_AND_ACCESSIBILITY_20260729.md`
  and
  `artifacts/current/20260729-novice-hierarchy-accessibility/final/`.
  Inventory remains `104 C / 17 P / 88 N / 9 E / 16 O`; `A-01` remains
  Partial only for the human-owner unaided R0. Do not repeat automated replay
  while current evidence remains valid. After owner R0 passes, begin
  `J-01/J-03/J-04 SurfaceModel` with `gpt-5.6-sol`, high.

- Advanced Viewer reactivation closure (2026-07-29): the current Release
  now explicitly releases the main Viewer from the nested Teach host and
  reactivates both the Advanced workspace property and its live AvalonDock
  presenter before requesting a post-layout frame. The actual-pointer replay
  now rejects hidden/off-screen UI Automation matches and requires visible
  Advanced Viewer and final Failure Analysis postconditions. Release build
  passes `0/0`, Workbench docking passes `55/55`, and application-only Wide
  `1920 x 1040` and Compact `1280 x 760` videos pass at 15 fps / 110 s with
  the C3D surface, ROI, controls, HUD, `3 Pass / 2 Fail / 0 Error`, and final
  preserved failure evidence visible. Preserve
  `docs/OPENVISIONLAB_3D_ADVANCED_VIEWER_REACTIVATION_20260729.md` and
  `artifacts/current/20260729-advanced-viewer-reactivation/`. Inventory
  remains `104 C / 17 P / 88 N / 9 E / 16 O`; `A-01` remains Partial. Its
  historical next P1 software gate is superseded by the closure above.

- Direct simulated-novice full-route repeat (2026-07-29): current Release
  app-only actual-pointer videos preserve the repaired Teach correction
  context, execute the five-sample set (`3 Pass / 2 Fail / 0 Error`), and
  reach Results and Advanced. The repeat is `Incomplete`: Advanced shows a
  dark empty `3D 검사 보기` pane in both Wide and Compact after the same C3D
  source and ROI rendered in Teach. The contextual sample-set action also
  lacks a discoverable AutomationId/accessibility name and required a
  layout-derived pointer fallback. Failure Analysis and Results remain
  clipped and technical-first, especially in Compact. Compact visibly
  returns to preserved Failure Analysis. Wide's final click occurred inside
  the recorded interval, but the historical harness did not assert or retain
  a post-click visible state, so it was unproven rather than failed. Preserve
  `docs/OPENVISIONLAB_3D_DIRECT_NOVICE_REPEAT_ANALYSIS_20260729.md` and
  `artifacts/current/20260729-direct-novice-r0-repeat/`. Inventory remains
  `104 C / 17 P / 88 N / 9 E / 16 O`; `A-01` remains Partial. This
  historical blocker is superseded by the Advanced Viewer reactivation
  closure above.

- Teach failure-correction context closure (2026-07-29): the current Release
  now turns `Validation -> Failure Analysis -> Fix in Teach` into an
  actionable correction route. Teach reattaches the identified C3D Viewer
  and ROI after stage recomposition and carries a read-only sample, rule,
  reason, and exact failed/passed-cell summary. Compact temporarily focuses
  the left workspace on Selected Tool instead of requiring a hidden tab;
  normal composition returns on stage exit. No Preview, Publish, Run, or
  recipe-semantic mutation is introduced. Release build passes `0/0`,
  Workbench docking passes `54/54`, and app-only actual-pointer evidence
  passes Wide `1920 x 1040` / 42 s and Compact `1280 x 760` / 44 s at
  15 fps. Preserve
  `docs/OPENVISIONLAB_3D_TEACH_FAILURE_CORRECTION_CONTEXT_20260729.md` and
  `artifacts/current/20260729-teach-failure-correction/`. Inventory remains
  `104 C / 17 P / 88 N / 9 E / 16 O`; `A-01` remains Partial only for the
  human-owner unaided R0. Do not spend model tokens repeating automated
  replay while owner evidence is unavailable. After owner R0 passes, begin
  `J-01/J-03/J-04 SurfaceModel` with `gpt-5.6-sol`, high.

- Direct simulated-novice replay finding (2026-07-29): visible-coordinate
  operation of the current Release passes stage discovery, explicit
  five-sample execution (`3 Pass / 2 Fail / 0 Error`), failure-to-Teach
  routing, Results/Advanced return, and state preservation. It does not pass
  novice failure correction. Wide and Compact Teach show a dark empty Viewer
  after `Fix in Teach`, while Advanced immediately renders the same
  `completeness-taught.C3D` surface and ROI. Teach also loses the selected
  failed sample/reason/cell context. Compact requires a small bottom
  `Selected Tool` tab and then compresses ROI/actions into a narrow scrolling
  column; Failure Analysis and Results expose clipped technical evidence
  before an operator summary. Preserve
  `docs/OPENVISIONLAB_3D_DIRECT_NOVICE_REPLAY_FINDINGS_20260729.md` and
  `artifacts/current/20260729-direct-novice-r0-replay/`. Inventory remains
  `104 C / 17 P / 88 N / 9 E / 16 O`; this historical blocker is superseded
  by the closure above. `A-01` remains Partial for human-owner R0 only. The
  first Wide segment contains an unrelated 2D app foreground interruption
  and is not app-only 3D Studio pass evidence.

- IA-4b automated owner-path replay closure (2026-07-29): current Release
  Wide and Compact application-only videos explicitly run the five-sample
  Completeness set (`3 Pass / 2 Fail / 0 Error`), open the selected failed
  `Completeness Grid` step in Teach, review the supplied one-step Fail Run
  Record, enter Advanced, return to Results, and return to preserved
  Validation failure evidence. The first replay found a real dock boundary
  defect: the visible `Fix in Teach` button had no live Shell command owner.
  `ToolRecipeWorkbenchView` now explicitly binds the hosted Validation
  view's `RunRecordContext` to the Shell owner. Release build passes `0/0`;
  the Window-hosted combined integration and state-preservation verifier
  passes `52/52`; accepted videos are `1920 x 1040` and `1280 x 760`, 15 fps,
  72 seconds. Preserve
  `docs/OPENVISIONLAB_3D_IA4B_OWNER_PATH_REPLAY_20260729.md` and
  `artifacts/current/20260729-ia4b-owner-path-replay/`. `A-01` remains
  Partial and inventory remains `104 C / 17 P / 88 N / 9 E / 16 O` until
  the human owner completes unaided Wide/Compact R0. Do not spend model
  tokens repeating this automated replay while that external evidence is
  unavailable. After owner R0 passes, begin `J-01/J-03/J-04 SurfaceModel`
  with `gpt-5.6-sol` at high reasoning. Physical calibration and metrology
  remain unverified.

- IA-4a live stage-host integration repair closure (2026-07-29): every
  dynamically recomposed stage view now owns an explicit stable Shell or
  Workbench DataContext. Actual Release Wide/Compact videos restore Teach
  Selected Tool content, all five named Validate sections and five Pending
  `2 Good / 2 Bad / 1 Held-out` rows, all three named Results sections, the
  supplied one-step Fail Run Record, and a visible Advanced transition.
  Validate's contextual action is now `샘플 세트 실행` / `Run sample set`,
  distinct from global recipe Run All. A real off-screen WPF Window
  Setup/Teach/Validate/Results integration check passes `48/48`, including
  hosted-owner identity, localized/accessibility navigation, five rows,
  command readiness, Advanced routing, and presentation-only state
  preservation. `A-10` is Complete again; `A-01` remains Partial. Inventory
  is `104 C / 17 P / 88 N / 9 E / 16 O`. Preserve
  `docs/OPENVISIONLAB_3D_STAGE_HOST_INTEGRATION_REPAIR_20260729.md` and
  `artifacts/current/20260729-stage-host-integration-repair/`. Immediate:
  `IA-4b failure-to-Teach, Results/Advanced return-preservation, and owner
  R0`, then SurfaceModel. The Codex simulated replay does not replace
  human-owner acceptance.

- Historical IA-4 simulated-novice actual-Release blocker (2026-07-29): clean
  application-only `1920 x 1040` and `1280 x 760` videos prove that the
  top-level Setup/Teach/Validate/Results navigation is recognizable, but
  dynamic dock recomposition loses the live context required by Teach
  Selected Tool, Validate, Results, and Advanced. Validate shows five
  unlabeled radio circles, omits the matching `2 Good / 2 Bad / 1 Held-out`
  sample set, and leaves Run All disabled. Results shows three unlabeled
  radio circles, omits the supplied one-step Fail Run Record, and its enabled
  Advanced gear produces no visible transition. The same controls expose
  empty accessible names. Prior direct View captures and structural
  `44/44` / `47/47` checks did not assert live MainWindow child context,
  labels, row counts, command readiness, or visible navigation. `A-01` and
  `A-10` are Partial; inventory is `103 C / 18 P / 88 N / 9 E / 16 O`.
  Preserve
  `docs/OPENVISIONLAB_3D_NOVICE_STAGE_NAVIGATION_VIDEO_REVIEW_20260729.md`,
  `artifacts/current/20260729-novice-stage-navigation-video-review/`, and
  `scripts/run-novice-stage-navigation-video-review.ps1`. Immediate:
  `IA-4a live stage-host ownership and MainWindow integration repair`, then
  repeat application-only replay and owner R0. Do not begin SurfaceModel
  until this gate passes or the owner explicitly reprioritizes. The Codex
  replay does not replace human-owner acceptance. The newer IA-4a checkpoint
  above supersedes this blocker and priority.

- Dedicated Results workspace IA-3 closure (2026-07-29): Results is now one
  full-height read-only workspace instead of a dominant Viewer plus a
  compressed lower Run Record. Local navigation separates Run Record, Output
  Compare, and Reports/export. Results no longer exposes Save or editing
  surfaces. Existing messages, performance, profile, fit, intersection,
  correspondence, and other expert docks remain available only through the
  explicit Advanced/Tool Labs route. Stage/local/Advanced navigation
  preserves recipe, selected-step, current-output, and run-evidence state and
  never executes inspection. Current Release evidence passes build `0/0`,
  docking/stage/non-mutation `47/47`, Run Record `10/10`, Artifact Navigator
  `31/31`, Shell options `24/24`, structure `17/17`, and all current
  Wide/Compact/section capture quality. Preserve
  `docs/OPENVISIONLAB_3D_DEDICATED_RESULTS_WORKSPACE_20260729.md` and
  `artifacts/current/20260729-results-workspace-extraction/`. Inventory
  was `104 C / 17 P / 88 N / 9 E / 16 O`; the newer IA-4 replay above
  invalidates the live integration closure. SurfaceModel remains paused
  behind IA-4. Camera/PLC/robot/cloud scope,
  physical calibration, and metrology remain out of scope or unverified.

- Dedicated Validate workspace IA-2/A-10 closure (2026-07-29): Validate is
  now one full-height task workspace rather than a dominant Viewer plus a
  compressed lower Validation Set. Local drill-down separates Samples, Run
  Results, Failure Analysis, Threshold Review, and Held-out. Selected
  Fail/Error evidence opens its owning pipeline step in Teach without
  mutating or executing the recipe. Existing Good/Bad/Held-out roles,
  deterministic candidates/error tables, explicit Review/Cancel/Apply,
  development revalidation, and explicit Held-out replay remain unchanged.
  Current Release evidence passes build `0/0`, docking/stage `44/44`,
  Validation Set `84/84`, Inspection Workspace `63/63`, teaching `28/28`,
  Artifact Navigator `31/31`, Shell options `24/24`, structure `17/17`, and
  current Wide/Compact capture quality. Preserve
  `docs/OPENVISIONLAB_3D_DEDICATED_VALIDATE_WORKSPACE_20260729.md` and
  `artifacts/current/20260729-validate-workspace-extraction/`. `A-10` is
  Complete; `A-01` remains Partial. Inventory is now
  `104 C / 17 P / 88 N / 9 E / 16 O`. The newer IA-3 checkpoint above
  supersedes this historical priority.

- IA-1 Setup/Teach workspace separation closure (2026-07-29): the shell now
  exposes real `Setup`, `Teach`, `Validate`, `Results`, and independent
  `Calibration` navigation. Setup owns Tool Library and the full Recipe Chain
  without Viewer/Selected Tool/lower evidence. Teach owns the compact step
  rail, dominant Viewer, and Selected Tool without Tool Library/lower
  evidence. Compact tabs the support surface instead of showing every
  responsibility. Stage navigation preserves recipe/source/selection and
  never executes; active ROI, PropertyGrid, Preview, or Validation work blocks
  the transition. Current Release evidence passes build `0/0`, docking/stage
  `43/43`, Shell state `75/75`, Inspection Workspace `63/63`, teaching
  `28/28`, measurement `54/54`, Recipe Manager/WPG `37/37`, Validation Set
  `82/82`, Shell options `24/24`, Run Record `10/10`, structure `17/17`, and
  fresh Setup/Teach Wide/Compact capture quality. Preserve
  `docs/OPENVISIONLAB_3D_SETUP_TEACH_WORKSPACE_SEPARATION_20260729.md`,
  `docs/OPENVISIONLAB_3D_WORKSPACE_INFORMATION_ARCHITECTURE_REDESIGN_20260729.md`,
  and `artifacts/current/20260729-workspace-information-architecture/`.
  At this IA-1 closure, `A-01` remained Partial and inventory was
  `103 C / 18 P / 88 N / 9 E / 16 O`. The newer IA-2 checkpoint above
  supersedes that count and priority.

- Completeness Validation Set and threshold assistance H-11/H-12/I-14
  closure (2026-07-29): one controlled Completeness recipe now replays two
  Good, two Bad, and one Held-out sample with real `Pass/Fail/Pass` evidence.
  The assistant derives the policy-equivalent worst cell per sample for
  minimum finite coverage, minimum reference-relative mean, and maximum
  reference-relative mean. Shared threshold contract `2.1` preserves the
  exact `r###.c###` cell locator on every derived observation and sample
  decision; Held-out remains excluded from boundaries, ranking, counts, and
  development decisions. Three exact fail-closed mappings target only the
  existing Completeness policy parameters. Review/Cancel are non-mutating,
  candidate Apply changes only the PropertyGrid draft, explicit development
  replay gates the separate Held-out replay, and no Preview/Publish/Run is
  implicit. Current Release evidence passes build `0/0`, Validation Set
  `82/82`, Completeness golden `23/23`, Inspection Workspace `63/63`, Recipe
  Manager/PropertyGrid `37/37`, docking `33/33`, Shell options `24/24`,
  structure `17/17`, Runner report schema `1.1`/threshold contract `2.1`
  with `57` candidates, `4` development samples, `1` Held-out excluded,
  `0` warnings, and `8` mappings, plus fresh Wide/Compact capture quality.
  Preserve
  `docs/OPENVISIONLAB_3D_COMPLETENESS_VALIDATION_AND_THRESHOLD_ASSISTANCE_20260729.md`
  and
  `artifacts/current/20260729-completeness-threshold-assistance/`. Master
  inventory at this closure was `104 C / 17 P / 88 N / 9 E / 16 O`; the
  newer information-architecture checkpoint above reopens `A-01` and
  supersedes the current count and next priority. `H-09` remains blocked by
  `E-11/G-12`. R0 owner replay and physical metrology remain external or
  unverified.

- Completeness failure navigation and repeated-Tab mapping H-08/H-10 closure
  (2026-07-29): Workbench now owns one view-only selected-cell review
  projection over the existing stable Completeness cell IDs. Previous/Next
  traverses failed cells in deterministic row-major order with wrap; all-pass
  output disables both actions. Height Image and 3D emphasize the same
  selected cell without duplicating decision policy. Ordinary Thickness
  steps named `Tab 1..8 Thickness` map by ordinal to cell-result presentation
  while retaining step/output identities. Navigation never dirties or saves
  the recipe and never invokes Preview, Publish, Run, or Validation Set.
  Current Release evidence passes build `0/0`, height measurement Workbench
  `54/54`, Completeness golden `23/23`, Inspection Workspace `63/63`, recipe
  teaching `28/28`, Artifact Navigator `31/31`, docking `33/33`, Shell
  options `24/24`, Viewer display `103/103`, structure `17/17`, and fresh
  Wide/Compact capture quality. Preserve
  `docs/OPENVISIONLAB_3D_COMPLETENESS_FAILURE_NAVIGATION_AND_TAB_MAPPING_20260729.md`
  and `artifacts/current/20260729-completeness-failure-navigation/`. Master
  inventory is now `101 C / 17 P / 91 N / 9 E / 16 O`.
  `H-11/H-12 Validation Set examples and Completeness threshold assistance`
  is next; `H-09` remains blocked by `E-11/G-12`. R0 owner replay and
  physical metrology remain external or unverified.

- Completeness result and overlays H-05/H-06/H-07 closure (2026-07-29):
  Completeness Grid now accepts one optional typed policy with inclusive
  finite-coverage and reference-relative mean raw-height limits. Existing
  seven-parameter H-02 recipes remain evidence-only `Warning`; a partial or
  invalid policy fails closed. Tools assigns deterministic cell Pass/Fail,
  treats a missing finite mean as Fail, counts passed/failed cells, and sets
  aggregate Pass only when every cell passes. Core owns stable
  coordinate-true overlay descriptors; Height Image and 3D render the same
  green/red cells without owning policy. The controlled mixed fixture
  produces `2` Pass, `2` Fail, aggregate `Fail`, `4` overlays, and SHA-256
  `1B051233FFCCC65FD72A4CB50299C629C8BCE7929E7AC4CA3CA3F33653DBF8CE`;
  an independent all-valid fixture produces aggregate Pass. Current Release
  evidence passes build `0/0`, golden `23/23`, height measurement Workbench
  `50/50`, Tool Recipe selections `29/29`, Inspection Workspace `63/63`,
  recipe teaching `28/28`, Recipe Manager/PropertyGrid `37/37`, docking
  `33/33`, Artifact Navigator `31/31`, Shell options `24/24`, structure
  `17/17`, production Runner parity, and fresh Wide/Compact capture quality.
  Preserve
  `docs/OPENVISIONLAB_3D_COMPLETENESS_RESULTS_AND_OVERLAYS_20260729.md` and
  `artifacts/current/20260729-completeness-results-overlays/`. Master
  inventory at that checkpoint was `99 C / 17 P / 93 N / 9 E / 16 O`.
  That historical next item is complete in the newer H-08/H-10 checkpoint;
  `H-09` remains blocked by `E-11/G-12`. R0 owner replay and physical
  metrology remain external or unverified.

- Completeness Grid H-02/H-03/H-04 closure (2026-07-29): Core now owns one
  typed native-grid rows/columns/X-column pitch/Z-row pitch/cell-size/
  GridRectangle contract, stable row-major cell identities, exact finite
  coverage, and explicit reference-relative mean raw-height evidence. Tools
  deterministically generates non-overlapping cells inside the authored
  Inspection Grid ROI and fails closed when the extent does not fit.
  Workbench preserves ordered Reference/Inspection Grid roles, typed
  PropertyGrid editing, and explicit Preview/Publish; ordered graph and
  production Runner emit the same typed output SHA-256. The controlled
  `8 x 8` fixture produces coverage `1, 0.75, 0.5, 0` and relative means
  `2, 4, -2, missing`. Current Release evidence passes build `0/0`, golden
  `14/14`, height measurement Workbench `50/50`, Inspection Workspace
  `63/63`, Recipe Manager/PropertyGrid `37/37`, Shell options `24/24`,
  structure `17/17`, production Runner parity, and current Wide/Compact
  capture quality. Preserve
  `docs/OPENVISIONLAB_3D_COMPLETENESS_GRID_METRICS_20260729.md` and
  `artifacts/current/20260729-completeness-grid-metrics/`. Master inventory
  at that checkpoint was `96 C / 17 P / 96 N / 9 E / 16 O`. This slice applies no presence
  threshold, aggregate decision, or colored overlay.
  That historical next slice is complete in the newer H-05/H-06/H-07
  checkpoint above. R0 owner replay and physical metrology remain external
  or unverified.

- Threshold-correction Run Record L-11 closure (2026-07-29): ordered graph
  Run Record schema `1.5` now embeds one read-only snapshot of the existing
  recipe-side correction sidecar. It preserves exact candidate, step, tool,
  metric, before, suggested, manually committed, before/corrected development,
  and Held-out identities and values. Missing evidence is `Unavailable`;
  identity differences are `Mismatch`; changed committed parameters are
  `Stale`; malformed or internally inconsistent evidence is `Invalid`.
  Projection never calculates, applies, executes, or replays threshold policy.
  JSON, HTML, and Workbench use the same typed contract. Current Release
  evidence passes build `0/0`, Run Record `10/10`, Validation Set `72/72`,
  Inspection Workspace `63/63`, Recipe Manager/PropertyGrid `37/37`,
  structure `17/17`, production Runner parity, and fresh Wide/Compact capture
  quality. Preserve
  `docs/OPENVISIONLAB_3D_THRESHOLD_CORRECTION_RUN_RECORD_20260729.md` and
  `artifacts/current/20260729-threshold-correction-run-record/`. Master
  inventory at that checkpoint was `93 C / 17 P / 99 N / 9 E / 16 O`.
  Its historical next H-02/H-03/H-04 slice is complete in the newer
  checkpoint above. R0 owner replay and physical metrology remain external
  or unverified.

- Threshold assistant evidence hardening I-12/I-13/I-15 closure
  (2026-07-29): shared candidate report contract `2.0` now owns typed
  missing-Good, missing-Bad, insufficient-Good, insufficient-Bad,
  imbalanced-class, and inseparable-distribution warnings with exact
  supported step/metric ownership, Good/Bad counts, and development sample
  identities. Held-out remains excluded. The published fail-closed mapping
  matrix contains Thickness Mean Minimum/Maximum/Range and Warpage
  PeakToValley/Rms Maximum only. Role edits, warning projection, Review,
  candidate draft Apply, manual PropertyGrid edit/Apply, development replay,
  and Held-out replay preserve explicit execution boundaries. Current Release
  evidence passes build `0/0`, Validation Set `72/72`, Inspection Workspace
  `63/63`, Recipe Manager/PropertyGrid `37/37`, Shell options `24/24`,
  structure `17/17`, Runner report schema `1.1`/threshold contract `2.0` with
  five mappings, and fresh Wide/Compact capture quality. Preserve
  `docs/OPENVISIONLAB_3D_THRESHOLD_ASSISTANT_HARDENING_20260729.md` and
  `artifacts/current/20260729-threshold-assistant-hardening/`. The prior
  11-video analysis is an ongoing product-direction contract through
  `docs/OPENVISIONLAB_3D_COMMERCIAL_VIDEO_DIRECTION_AND_PRIORITY_20260727.md`
  and `docs/OPENVISIONLAB_3D_INDUSTRIAL_UX_AUDIT_20260728.md`, not a
  one-time audit. The historical next `L-11` slice is closed by the newer
  checkpoint above.

- Threshold manual correction I-09/I-11 closure (2026-07-28): one controlled
  committed Thickness draft now proves a genuine expected-role mismatch
  before editing (`MinimumThickness 0`, `MaximumThickness 20`; Bad-high Mean
  `20` incorrectly passes). Candidate
  `threshold.0ad7b16eaa3d4362` suggests `2..4`; the operator changes that
  typed draft to `1.5..4.5` and commits it through the ordinary PropertyGrid
  Apply. A separate explicit development replay reduces mismatch `1 -> 0`
  before Held-out unlocks; the subsequent Held-out-only replay passes the
  exact SHA
  `D9384A7B5A032D28E952E8742619EA224F2763FC5B5B3C431DC895544AA93C3B`.
  The portable correction sidecar preserves before parameters, exact sample
  SHA/metrics/status, suggestion, manual values, corrected development
  evidence, and Held-out evidence. Workbench and Runner agree exactly.
  Current Release evidence passes build `0/0`, Validation Set `66/66`,
  Inspection Workspace `63/63`, Recipe Manager/PropertyGrid `37/37`, Shell
  options `24/24`, structure `17/17`, Runner schema `2.0` parity, and fresh
  Wide/Compact capture quality. Preserve
  `docs/OPENVISIONLAB_3D_THRESHOLD_MANUAL_CORRECTION_AND_FAILURE_RECORD_20260728.md`
  and `artifacts/current/20260728-threshold-manual-correction/`. The master
  inventory at that checkpoint was `89 C / 17 P / 103 N / 9 E / 16 O`.
  The historical next `I-12/I-13/I-15` and `L-11` slices are closed by the
  newer 2026-07-29 checkpoints above. This controlled
  deterministic fixture is software workflow evidence, not a claimed GPT
  transcript, physical calibration, production tolerance, or certified
  metrology.

- Threshold Review/Apply and Held-out replay I-08/I-10 closure
  (2026-07-28): selected development-only candidates now enter one explicit
  Review session with exact typed before/proposed parameter values. Cancel is
  non-mutating. Candidate Apply updates only the supported PropertyGrid draft;
  normal PropertyGrid Apply remains separate and no Preview, Publish, Run All,
  save, or replay occurs automatically. Explicit Held-out replay projects the
  proposal onto an immutable recipe copy and executes Held-out samples only.
  A portable recipe-side correction-evidence contract and production Runner
  preserve the same candidate, parameter changes, sample identities, metrics,
  and result. The controlled Thickness fixture maps
  `MinimumThickness 0->2` and `MaximumThickness 10->4`; Workbench and Runner
  agree on `4` development samples, `1` Held-out, and Held-out `Pass`.
  Current Release evidence passes build `0/0`, Validation Set `58/58`,
  Inspection Workspace `63/63`, recipe teaching `28/28`, Recipe
  Manager/PropertyGrid `37/37`, Artifact Navigator `31/31`, Shell options
  `24/24`, structure `17/17`, Runner parity, and current Wide/Compact capture
  quality. Preserve
  `docs/OPENVISIONLAB_3D_THRESHOLD_REVIEW_APPLY_AND_HELD_OUT_REPLAY_20260728.md`
  and `artifacts/current/20260728-threshold-review-heldout/`. This does not
  prove a genuine failed-draft correction because the controlled Held-out
  already passed the original broad recipe. That historical next item is
  closed by the newer I-09/I-11, I-12/I-13/I-15, and L-11 checkpoints above.
  R0 owner replay, physical calibration, and metrology remain external or
  unverified.

- Next-chat continuation checkpoint (updated 2026-07-29 after IA-4a repair): use
  `docs/OPENVISIONLAB_3D_NEXT_CHAT_HANDOFF_PROMPT_20260728.md` as the concise
  entry point after this file. It joins the current `104 C / 17 P / 88 N /
  9 E / 16 O` master inventory, all 11 commercial-video-derived priority
  trains, the current dirty-working-tree boundary, and the next `IA-4b
  failure-to-Teach, Results/Advanced return-preservation, and owner R0`
  boundary. The
  authoritative item-level source remains
  `docs/OPENVISIONLAB_3D_MASTER_DEVELOPMENT_WORKFLOW_AND_BACKLOG_20260727.md`;
  the chronological evidence source remains
  `docs/OPENVISIONLAB_3D_NEXT_SESSION_HANDOFF.md`. Do not reset the current
  uncommitted implementation or touch user-owned untracked `3D/SSD-Black/`,
  `3D/fccsp/`, or `3D/새 폴더/`. No commit or push is authorized by the
  handoff alone.

- Threshold candidates and exact error table I-06/I-07 closure
  (2026-07-28): explicit-run Good/Bad observations now generate one
  deterministic Minimum, Maximum, and Range candidate per eligible
  step/region metric. Ranking minimizes total errors, false accepts, false
  rejects, then applies a stable tightness rule. Every candidate owns exact
  sample order/path/SHA, expected and predicted roles, decision, value, and
  reproducible confusion counts. Held-out samples are recorded separately
  and never enter boundaries, ranking, counts, or decisions. Workbench
  exposes a collapsed read-only candidate/error panel; selection is
  presentation-only. Runner emits the same shared contract. The controlled
  fixture produces `48` candidates from `4` development samples while
  excluding `1` Held-out; `Mean Range 2..4` has `4` correct and `0` errors.
  Current Release evidence passes build `0/0`, Validation Set `45/45`,
  Inspection Workspace `63/63`, Shell smoke options `24/24`, recipe
  teaching `28/28`, Artifact Navigator/Output Compare `31/31`, code
  structure `17/17`, Runner parity with zero Held-out decisions, and current
  default/expanded/compact capture quality.
  Preserve
  `docs/OPENVISIONLAB_3D_THRESHOLD_CANDIDATES_AND_ERROR_TABLE_20260728.md`
  and `artifacts/current/20260728-threshold-candidates/`. This historical slice
  did not apply candidates or replay Held-out data; those behaviors are closed
  by the newer I-08/I-10 checkpoint above. R0 owner replay, physical
  calibration, and metrology remain external or unverified.

- Labeled sample evidence I-04/I-05 closure (2026-07-28): Validation Set now
  assigns every staged C3D sample one durable `Good`, `Bad`, or `HeldOut`
  role in an atomic recipe-side manifest. Role edits never execute inspection
  or dirty the recipe graph, but they participate in the normal unsaved-state
  and close guard. Save/reopen restores ordered Pending rows without stale
  evidence. Explicit Run calculates per-step metric distributions and routed
  GridRectangle mean raw-height/valid-cell-ratio distributions. Held-out
  values remain visible with `IncludedInDevelopment=false`. The normal panel
  keeps distributions collapsed so sample selection and review remain
  primary; production Runner emits the same shared contract. Current Release
  evidence passes build `0/0`, Validation Set `35/35`, Runner `1/1/1` roles
  with `16` direct-fixture distributions and Held-out exclusion, and current
  Wide/Compact capture quality. Preserve
  `docs/OPENVISIONLAB_3D_LABELED_SAMPLE_EVIDENCE_20260728.md` and
  `artifacts/current/20260728-labeled-sample-evidence/`. This does not
  generate or apply thresholds. `I-06/I-07 deterministic threshold
  candidates and exact error table` is next. R0 owner replay, physical
  calibration, and metrology remain external or unverified.

- Level Surface D-05/D-06 closure (2026-07-28): Prepare now owns a typed
  deterministic least-squares reference-plane leveling rule using one or more
  explicit `GridRectangle` ROIs. Unique finite reference cells define one
  fit; overlapping cells count once; an authored RMS gate fails closed.
  `C3DLevelingTransform` preserves source/grid/frame/unit identity, reference
  regions, slopes, intercept, target height, residual RMS/P2V, an equivalent
  3 x 4 detrend matrix, provenance, and SHA-256. The derived C3D preserves the
  native X/Z grid and missing mask while applying
  `Y' = Y - fittedPlane(X,Z) + referenceMean`; the source is unchanged.
  Workbench exposes typed parameters, `Add reference ROI`, explicit
  Preview/Publish, stale state, residual and transform evidence, renderable
  Show/Pin/Compare output, and save/reopen. Runner produces the same output
  and transform. The known `16 x 12` fixture recovers slopes approximately
  `0.799962/-0.399919` and reduces them to approximately
  `-4.23E-08/1.62E-07`; valid/missing remains `191/1`. Current Release
  evidence passes build `0/0`, golden `9/9`, Workbench `17/17`, Inspection
  Workspace `63/63`, shell options `24/24`, teaching `28/28`, Artifact
  Navigator `31/31`, structure `17/17`, and current Wide/Compact capture
  quality. Preserve `docs/OPENVISIONLAB_3D_LEVEL_SURFACE_20260728.md` and
  `artifacts/current/20260728-level-surface/`. This is a grid-preserving
  raw-height detrend, not rigid pose/re-grid, physical calibration, or
  certified metrology. `I-04/I-05 labeled sample evidence` is next.

- Remove Outlier Pixels D-04 closure (2026-07-28): Prepare now owns a typed
  deterministic `LocalMedianAbsoluteDeviation` rule with an excluded center,
  strict-greater-than threshold, odd `3/5/7` window, explicit minimum valid
  neighbors, preserved source missing cells, available-neighbor boundaries,
  and `SetMissing` outlier output. Data owns one immutable coordinate-true
  row-major LSB-first `C3DOutlierCellMap`; Tools, Workbench, Viewer, Output
  Compare, and production Runner share the derived C3D and mask identity.
  The known `12 x 10` fixture changes valid/missing from `119/1` to `116/4`
  by removing exactly `3` cells. Source SHA-256 remains
  `FAE710BB1886C2D406F66A507D9B45866D42C184C70F31CE9E7DF9724A5415FC`;
  output SHA-256 is
  `08C7B173D30C9ADF0B83CCF7D37DF4A1B3C2B8A15A0D312E9BFAB24263C7DF0E`;
  mask SHA-256 is
  `AE44FA864AD48A1ABF7FEC959137A84962F6E0A8E69D8C53B69F30FF44D3AD3E`.
  Current Release evidence passes build `0/0`, rule golden `9/9`, Workbench
  `14/14`, Inspection Workspace `63/63`, shell options `23/23`, structure
  `17/17`, and current Wide/Compact capture quality. Preserve
  `docs/OPENVISIONLAB_3D_REMOVE_OUTLIER_PIXELS_20260728.md` and
  `artifacts/current/20260728-remove-outlier-pixels/`. Removed cells remain
  missing; interpolation, calibrated units, and metrology are not claimed.
  `D-05/D-06 Level Surface` is next.

- OrientedBox3D Viewer handles E-09 closure (2026-07-28): the schema `1.4`
  persisted volume now renders as a translucent oriented cuboid with a
  rotation ring and fixed-screen-size center, local X/Y/Z resize, height, and
  local-Y rotation handles. Numeric fields and Viewer gestures share one
  transient Review draft; the global Review bar is the sole visible
  Apply/Cancel owner. Axis handles that collapse in Top or side projection
  receive deterministic screen-space fallback positions without changing
  stored geometry. Current Release evidence passes build `0/0`, actual
  Windows pointer Perspective move/X-Y-Z resize/rotate plus Top height and
  side collapsed-axis resize, Inspection Workspace `63/63`, shell options
  `22/22`, teaching `28/28`, height measurement `46/46`, docking `33/33`,
  display `103/103`, structure `17/17`, and current Wide/Compact/side capture
  quality. Preserve
  `docs/OPENVISIONLAB_3D_ORIENTED_BOX_VIEWER_HANDLES_20260728.md` and
  `artifacts/current/20260728-oriented-box-viewer-handles/`. `D-04 Remove
  Outlier Pixels` is complete in the newer checkpoint above. The box still has no downstream inspection
  consumer; free local-X/Z rotation, calibration, physical metrology, and R0
  owner replay remain unverified or external.

- Public-safe synthetic Thickness sample migration (2026-07-28): the former
  non-public company-derived C3D fixture, generated recipe, identifiers,
  source-specific hashes/statistics, and README GIF are retired from the
  current tree. The replacement is the deterministic fictional
  `3D/Samples/ThicknessCouponV1` package: an AI-concept-guided but
  procedurally generated `1280 x 840` C3D, ground-truth JSON, preview, and
  schema `1.5` eight-pad Thickness recipe with 16 independent
  `GridRectangle` selections. Source SHA-256 is
  `D879FC9E40678762214E8C3FBEA01F5C9A309701DAAEAD448067E563C5B502F8`;
  the source has `908,436` valid and `166,764` missing cells. Production
  Runner replay passes `8/8`, with signed means matching the authored
  `8, 12, 16, 20, 10, 14, 18, 22` raw-height separations within
  float32 tolerance. Preserve
  `docs/OPENVISIONLAB_3D_SYNTHETIC_THICKNESS_SAMPLE_MIGRATION_20260728.md`,
  `scripts/generate-thickness-coupon-sample.py`, and
  `artifacts/current/20260728-synthetic-thickness-coupon/`. Historical
  private-derived performance evidence is not valid public evidence and must
  not be restored. The remote feature branch still requires an explicitly
  approved history rewrite before the retired blobs disappear from Git
  history. Physical calibration and metrology remain unverified.

- Public README user-facing closure (2026-07-28): the root README is now a
  Korean-first product page instead of a 64 KB developer audit log. The first
  fold presents the product value, current-development badges, and the current
  960 x 520 ROI teaching GIF. Stable sections explain the one-minute product
  summary, explicit inspection workflow, current capabilities, Workbench
  composition, supported formats/tools, quick start, shortcuts, honest
  calibration/scope boundaries, the Viewer-only prerelease, and license
  status. Detailed build, focused Workbench verification, UI replay, Runner,
  data-loading, Viewer DLL, CI, and completion-checklist instructions now live
  in
  `docs/OPENVISIONLAB_3D_DEVELOPMENT_AND_VERIFICATION_GUIDE.md`. Local-link
  verification passes, the documented Debug build command passes `0/0`, and a
  browser render confirms all four badges plus the GIF load at their intended
  dimensions. Preserve
  `docs/OPENVISIONLAB_3D_PUBLIC_README_REDESIGN_20260728.md` and
  `artifacts/current/20260728-readme-user-facing/`. The public `main` page
  remains unchanged until this branch is merged. The newer D-04 checkpoint
  above supersedes this historical priority; `D-04` is complete. R0 owner
  replay and physical metrology remain external or unverified.

- Dual-ROI role preservation closure (2026-07-28): recipe schema `1.5` now
  stores optional first/second GridRectangle role identities on the owning
  dual-ROI inspection step. Deleting Reference preserves Measurement as the
  second role; deleting Measurement preserves Reference; redrawing the
  missing role restores the ordered route. Existing schema `1.3`/`1.4`
  recipes remain readable and infer complete roles from input order until an
  edit promotes them. Shared capture now ends before role advancement, so
  fresh Height Image Reference Apply immediately enables Measurement Draw.
  Current Release evidence passes build `0/0`, recipe selection `29/29`,
  height measurement `46/46`, Inspection Workspace `61/61`, teaching
  `28/28`, Recipe Manager/WPG `37/37`, docking `33/33`, repeat grid `20/20`,
  Artifact Navigator `31/31`, artifact-owned Runner `18/18`, structure
  `17/17`, parser `0` errors, and actual Wide/Compact pointer, Ctrl+S, and
  reopen replay. Preserve
  `docs/OPENVISIONLAB_3D_DUAL_ROI_ROLE_PRESERVATION_20260728.md`,
  `artifacts/current/20260728-dual-roi-role-preservation/`, and
  `docs/assets/openvisionlab-3d-roi-workflow.gif`. The newer D-04 checkpoint
  above supersedes this historical next item. Compact focused ROI authoring and
  gesture-specific instruction remain P1; R0 owner replay and physical
  metrology remain external or unverified.

- Actual operator-video self-review (2026-07-28): the current Release EXE was
  operated with external UI Automation lookup, real `user32` pointer input,
  real keyboard input, and FFmpeg desktop capture. Fresh Reference ROI
  drag/Review/Enter Apply passed in Wide and Compact; OrientedBox3D
  invalid-axis rejection, valid Apply, Ctrl+S, and save/reopen passed. The
  historical replay found two blocking dual-ROI defects: deleting Reference first
  promotes the remaining Measurement selection into the Reference role, and
  fresh Height Image Reference Apply leaves Measurement Draw disabled.
  Height Image drag versus 3D two-point capture also needs clearer instruction,
  Compact needs a larger focused teaching surface, and the video confirms
  `E-09` because numeric OrientedBox3D has no Viewer geometry or handles.
  Preserve
  `docs/OPENVISIONLAB_3D_OPERATOR_VIDEO_SELF_REVIEW_20260728.md`,
  `artifacts/current/20260728-operator-video-self-review/`, and
  `docs/assets/openvisionlab-3d-roi-workflow.gif`. Those two P0 findings are
  superseded by the newer dual-ROI closure above. The remaining gesture,
  Compact findings still apply; the newer checkpoint above closes `E-09`.
  R0, physical calibration, and metrology remain external or unverified.

- OrientedBox3D E-07/E-08 closure (2026-07-28): recipe schema `1.4` now
  owns a distinct persisted `oriented-box-3d` selection with center XYZ,
  right-handed orthonormal axes, and positive half-extents XYZ. Existing
  schema `1.3` artifact-owned recipes remain valid and executable. The normal
  Selected Tool Regions surface exposes numeric MVVM authoring with explicit
  New, Apply, Cancel, and guarded Delete. Apply preserves identity, changes
  only the recipe, and never invokes Preview, Publish, or Run; save/reopen
  preserves exact geometry. Current Release evidence passes build `0/0`,
  selection `25/25`, Inspection Workspace `60/60`, recipe teaching `28/28`,
  height measurement `45/45`, Artifact Navigator `31/31`, docking `33/33`,
  Recipe Manager/WPG `37/37`, artifact-owned Runner `18/18`, synthetic affine
  `18/18`, schema `1.3` affine `4/4`, schema `1.3` correspondence `5/5`,
  shell options `21/21`, structure `17/17`, and Wide/Compact capture quality.
  Preserve
  `docs/OPENVISIONLAB_3D_ORIENTED_BOX_CONTRACT_AND_NUMERIC_EDITOR_20260728.md`
  and `artifacts/current/20260728-oriented-box-contract/`. The newer
  checkpoints above close `E-09` and `D-04`. R0 owner replay, physical
  calibration, and metrology remain external or unverified.

- Visible invalid/missing-cell overlay C-11 closure (2026-07-28): Height Image
  now shows the existing coordinate-true `C3DInvalidCellMap` in magenta by
  default with one direct view-only toggle, color swatch, exact count, and
  percentage. Valid palette pixels and native `pixel X=column / pixel Y=row`
  mapping remain unchanged. The exact
  `thickness-coupon-v1.C3D` source displays
  `166,764` overlay cells (`15.5%`) and retains Source Quality / Height
  Image mask SHA-256
  `44EDC44DEE6D0193DCCF22130487DC3CF80CCE2F68BDAA854A1D16FAA4BDC358`.
  Overlay visibility never changes source data, ROI, recipe, Preview,
  Publish, Run, or current output. Current Release evidence passes build
  `0/0`, Height Image `25/25`, exact-source probe, Inspection Workspace
  `53/53`, invalid map `15/15`, SourceQualityReport `13/13`, docking `33/33`,
  teaching `28/28`, Artifact Navigator `31/31`, height measurement `45/45`,
  shell options `21/21`, structure `17/17`, and Wide/Compact capture quality.
  Preserve
  `docs/OPENVISIONLAB_3D_VISIBLE_INVALID_CELL_OVERLAY_20260728.md` and
  `artifacts/current/20260728-invalid-cell-overlay/`. `E-07/E-08` typed
  `OrientedBox3D` schema and numeric editing are next. R0 owner replay,
  physical calibration, and metrology remain external or unverified.

- Inspection Workspace v3 UX mid-review correction (2026-07-28): current
  `1920 x 1040` and `1280 x 760` synchronized ROI captures were compared
  against the v3 and GoPxL-derived interaction contracts. The architecture
  and linked ROI direction were correct, but the same Apply/Cancel actions
  appeared in the global Review ribbon, Selected Tool, and Height Image, a
  repeated Viewer instruction obscured the model, internal route/adapter
  evidence duplicated Selected Tool, and compact Height Image remained only
  `4.2%`. The global Review ribbon is now the one primary ROI action surface.
  Selected Tool owns role/lifecycle/numeric/output context, Height Image owns
  manipulation and non-capture Delete, and the Viewer no longer repeats the
  instruction toast or selected-step technical context. Active Height Image
  ROI editing temporarily uses `35% 3D / 65% Height Image` and restores the
  prior split after Apply/Cancel; compact evidence now shows `7.9%`. Current
  Release evidence passes build `0/0`, UX structure, actual Windows pointer
  Wide/Compact Review, Apply/save/reopen, Workspace `50/50`, docking `33/33`,
  teaching `28/28`, height measurement `45/45`, shell options `21/21`, and
  structure `17/17`. Preserve
  `docs/OPENVISIONLAB_3D_WORKSPACE_V3_UX_MID_REVIEW_AND_ACCEPTANCE_CORRECTION_20260728.md`
  and
  `artifacts/current/20260728-workspace-v3-ux-acceptance-correction/`.
  Workspace v3 remains `7/8` until the owner completes R0 unaided replay.
  `C-11` is complete; `E-07/E-08 OrientedBox3D` is next.

- Synchronized Height Image / 3D ROI C-09/C-10 closure (2026-07-28):
  Reference ROI is cyan and Measurement ROI is orange in both linked views,
  with the same recipe selection ID and native-grid
  `row/column/rowCount/columnCount`. Height Image now supports direct draw,
  inside move, corner resize, role selection, Review, Apply, Cancel, and
  Delete. `HeightImageRoiWorkspaceViewModel` owns WPF-neutral 2D projection
  and gesture state while the existing Workbench remains the lifecycle and
  recipe owner. Actual Windows pointer evidence proves equal 2D/3D transient
  candidates. Review preserves dirty state, routing, applied geometry,
  inspection output, and 3D camera; Apply preserves selection identity and
  passes save/reopen. Current Release evidence passes build `0/0`, Inspection
  Workspace `50/50`, smoke options `21/21`, wide/compact pointer smoke and
  capture quality, display `103/103`, Height Image `21/21`, invalid map
  `15/15`, SourceQualityReport `13/13`, Source Quality `18/18`, Artifact
  Navigator `31/31`, docking `33/33`, height measurement `45/45`, recipe
  teaching `28/28`, and structure `17/17`. Preserve
  `docs/OPENVISIONLAB_3D_SYNCHRONIZED_HEIGHT_IMAGE_ROI_EDITING_20260728.md`
  and `artifacts/current/20260728-height-image-roi-editing/`. `C-11` is now
  complete; the separate typed `OrientedBox3D` contract is next. R0 owner
  replay, physical calibration, and
  metrology remain external or unverified.

- Shared Height Image / 3D cursor C-08 closure (2026-07-28): the
  full-size Height Image and main 3D Viewer now share one WPF-neutral,
  view-only native-grid cursor with source SHA-256, origin, row, column,
  raw height, validity, and revision. Height Image hover renders the same
  valid point as a yellow/cyan 3D surface marker; 3D hover renders the same
  native pixel as a full Height Image crosshair. Missing Height Image cells
  remain explicitly missing and never fabricate a 3D marker. Source mismatch
  and stale leave events fail closed. The exact
  `thickness-coupon-v1.C3D` source proves
  `column 593 / row 800 / H 633.4000244140625 raw-height` in both directions
  while preserving recipe, execution, current output, and camera state.
  Current Release evidence passes build `0/0`, Inspection Workspace `42/42`,
  smoke options `20/20`, exact-source wide/compact bidirectional smoke and
  capture quality, actual Windows pointer/menu regression, display `103/103`,
  Height Image `21/21`, invalid map `15/15`, SourceQualityReport `13/13`,
  Source Quality `18/18`, Artifact Navigator `31/31`, docking `33/33`,
  recipe teaching `28/28`, and structure `17/17`. Preserve
  `docs/OPENVISIONLAB_3D_SHARED_HEIGHT_CURSOR_20260728.md` and
  `artifacts/current/20260728-shared-height-hover/`. `C-09/C-10`
  synchronized ROI display/editing and `C-11` visible invalid-cell overlay
  are now complete. `C-13` remains open for one shared 2D/3D
  display range. R0 owner
  replay, physical calibration, and metrology remain external or unverified.

- Height Image palette and display-range C-07 closure (2026-07-28): the
  full-size Height Image now exposes Height, Grayscale, and Thermal palettes,
  explicit Auto range, numeric Min/Max, explicit Apply range, active range
  text, and a matching color legend. Auto uses the finite full-source range;
  manual range clips only color normalization. Raw heights, native
  `pixel X=column / pixel Y=row` mapping, invalid cells, recipe, Preview,
  Publish, Run, Validation Set, and Save remain unchanged. Invalid or inverted
  ranges fail closed and retain the last valid display. The exact
  `thickness-coupon-v1.C3D` source changes from the
  Auto Height pixel SHA-256
  `6A6C12F7A729ABF49830F07CBB868FCCCB94C987584856128662109BA377B087`
  to Thermal `0..1200 raw-height` display SHA-256
  `49FE0B0009CDE14BEE44C40C99F7EC0A6571BBC3DCDF8EDA168943E418F531BF`
  while preserving invalid-map SHA-256
  `44EDC44DEE6D0193DCCF22130487DC3CF80CCE2F68BDAA854A1D16FAA4BDC358`.
  Current Release evidence passes build `0/0`, Height Image `21/21`,
  Inspection Workspace `36/36`, smoke options `18/18`, exact-source
  wide/compact non-execution smoke and capture quality, invalid map `15/15`,
  SourceQualityReport `13/13`, Source Quality `18/18`, Artifact Navigator
  `31/31`, docking `33/33`, recipe teaching `28/28`, height distribution
  `22/22`, and structure `17/17`. Preserve
  `docs/OPENVISIONLAB_3D_HEIGHT_IMAGE_DISPLAY_RANGE_20260728.md` and
  `artifacts/current/20260728-height-image-display-range/`. `C-08` shared
  hover and `C-09/C-10` synchronized ROI editing are now complete. `C-11`
  is next.
  `C-13` remains open for a shared manual/auto range in both linked views.
  R0 owner replay, physical calibration, and metrology remain external or
  unverified.

- Unified Source Quality workspace B-08 closure (2026-07-28): the normal
  Inspection Workbench now uses the Selected Tool surface for Source Quality
  whenever an identified C3D source is loaded and no inspection step is
  selected. The Recipe Chain source card exposes a histogram action to return
  to that workspace after step selection. The read-only panel shows native
  grid/cell counts, valid/missing ratios, raw-height range/mean and 32-bin
  distribution, invalid-map identity, frame/unit/provenance, and explicit
  available/unavailable channel evidence without running inspection or
  changing the recipe. The exact
  `thickness-coupon-v1.C3D` source displays
  `1280 x 840`, `908,436` valid (`84.5%`), `166,764` missing (`15.5%`),
  and mask SHA-256
  `44EDC44DEE6D0193DCCF22130487DC3CF80CCE2F68BDAA854A1D16FAA4BDC358`.
  Current Release evidence passes build `0/0`, Source Quality `18/18`, smoke
  options `16/16`, wide/compact exact-source smoke and capture quality,
  Inspection Workspace `30/30`, docking `33/33`, recipe teaching `28/28`,
  Artifact Navigator `31/31`, SourceQualityReport `13/13`, invalid map
  `15/15`, and Height Image `14/14`. Preserve
  `docs/OPENVISIONLAB_3D_SOURCE_QUALITY_WORKSPACE_20260728.md` and
  `artifacts/current/20260728-source-quality-workspace/`. `C-07` Height Image
  display range, `C-08` shared hover, and `C-09/C-10` synchronized ROI
  editing and the visible invalid overlay `C-11` are now complete.
  R0 owner replay, physical calibration, and metrology remain external or
  unverified.

- Coordinate-true invalid-cell map B-09 closure (2026-07-28): Data now owns
  one immutable `C3DInvalidCellMap` with native
  `index=row*width+column`, row-major LSB-first packed bytes, coordinate
  lookup, missing count, and dimension-sensitive SHA-256 identity.
  `C3DSourceQualityAnalyzer` publishes that identity and
  `C3DHeightImageFrame` exposes and consumes the same typed map, removing the
  prior duplicate missing-cell paths. The exact
  `thickness-coupon-v1.C3D` source produces
  `1,075,200` cells, `166,764` missing cells, `134,400` packed bytes, and
  identical Source Quality / Height Image mask SHA-256
  `44EDC44DEE6D0193DCCF22130487DC3CF80CCE2F68BDAA854A1D16FAA4BDC358`.
  Current Release evidence passes build `0/0`, invalid-map `15/15`, Source
  Quality `13/13`, Height Image `14/14`, and the exact-source parity probe.
  Preserve
  `docs/OPENVISIONLAB_3D_INVALID_CELL_MAP_PARITY_20260728.md` and
  `artifacts/current/20260728-invalid-cell-map-parity/`. The visible
  invalid-cell overlay (`C-11`) is now complete. `B-08` Source Quality
  workspace, `C-07` display range, `C-08` shared hover, and `C-09/C-10`
  synchronized ROI editing are also complete. R0 owner replay,
  physical calibration, and metrology remain external or unverified.

- Full-size coordinate-true Height Image C-06 closure (2026-07-27): Data now
  owns an immutable native-grid BGRA frame with
  `pixel X=column / pixel Y=row / no flip / one source cell per pixel`.
  Side-by-side, stacked, and pop-out auxiliary layouts default to Height
  Image while retaining real source/Filter C3D candidates. Fit, 1:1, zoom,
  pan, and row/column/raw-height hover are view-only and never dirty or
  execute the recipe. The exact
  `thickness-coupon-v1.C3D` source produces
  `1280 x 840`, `1,075,200` pixels, `908,436` valid, `166,764` missing,
  and pixel SHA-256
  `6A6C12F7A729ABF49830F07CBB868FCCCB94C987584856128662109BA377B087`.
  Current evidence passes native mapping `11/11`, Workspace `30/30`, Artifact
  Navigator `31/31`, docking `33/33`, Source Quality `12/12`, structure
  `17/17`, and current Release inline/pop-out capture quality. Preserve
  `docs/OPENVISIONLAB_3D_FULL_HEIGHT_IMAGE_VIEWER_20260727.md` and
  `artifacts/current/20260727-full-height-image-viewer/`. `B-09` invalid-cell
  mask/image parity and `B-08` Source Quality workspace are now complete.
  `C-07` manual numeric range and `C-08` shared cross-view hover are now
  complete; `C-09/C-10` ROI editing and `C-11` visible invalid-cell overlay
  are also complete.
  R0 owner replay, physical
  calibration, and metrology remain external or unverified.

- SourceQualityReport B-07 closure (2026-07-27): Core now owns a WPF-neutral
  schema `1.0`; Data calculates exact grid/sample/valid/missing counts,
  raw-height range/mean/distribution, unit/frame/provenance, and a
  locator-sensitive invalid-cell mask SHA-256; Runner generates JSON and
  verifies the contract `12/12`. Unsupported C3D
  intensity/color/depth/normal/confidence/SNR channels are explicitly
  unavailable and never fabricated. The exact
  `thickness-coupon-v1.C3D` source reports
  `1280 x 840`, `908,436` valid, and `166,764` missing cells. Preserve
  `docs/OPENVISIONLAB_3D_SOURCE_QUALITY_REPORT_20260727.md` and
  `artifacts/current/20260727-source-quality-report/`. This is not the
  invalid-cell overlay, a Source Quality UI, calibration, or metrology.
  R0 owner replay remains external. `C-06` Height Image and `B-09` mask
  parity, `B-08` Source Quality workspace, and `C-07` Height Image display
  range, `C-08` shared hover, and `C-09/C-10` synchronized ROI editing are
  now complete; `C-11` visible invalid-cell overlay is also complete.

- Master commercial-video development workflow (2026-07-27): the prior
  high-level commercial direction is expanded into release trains R0-R6, a
  `234`-item backlog (`75` Complete, `17` Partial, `117` New, `9` External
  prerequisite, `16` Out of scope), dependencies, closure evidence, a
  one-slice Definition of Done, and an executable queue. Read
  `docs/OPENVISIONLAB_3D_MASTER_DEVELOPMENT_WORKFLOW_AND_BACKLOG_20260727.md`
  before selecting new product work. R0 remains the owner's unaided current
  Release replay. `B-07 SourceQualityReport`, `C-06` Height Image, and `B-09`
  invalid-cell map, `B-08` Source Quality workspace, and `C-07` through
  `C-10` linked teaching plus `C-11` visible invalid-cell overlay are
  complete. Continue with `OrientedBox3D`, evidence-based threshold
  teaching, Completeness, and only then Surface Matching. Complete one bounded
  backlog item or cohesive dependency pair at a time and update its durable
  status only after the documented evidence gate passes.

- Commercial-video product direction (2026-07-27): all 11 owner-supplied
  GoPxL, SICK Nova, HALCON/MERLIC, Zivid Studio, and Photoneo videos under
  `C:\Git\GoPxL_Video\3D` have been reviewed individually against current
  source. OpenVisionLab 3D Studio remains a local, file-first, deterministic
  2.5D/3D rule-based inspection workbench. After the owner's unaided
  Inspection Workspace v3 replay, the priority order is Source Quality,
  full-size linked Height Image teaching, a separate typed `OrientedBox3D`,
  evidence-based threshold teaching, completeness/cell inspection, typed
  preparation tools, and then surface matching. Preserve explicit
  Apply/Preview/Publish/Run and current typed recipe/Runner evidence. The
  current small linked Height Map is not an interactive teaching workspace;
  `GridRectangle` remains an X/Z footprint and its display-only Y position is
  not a volume. Camera acquisition, stereo reconstruction,
  PLC/robot/fieldbus/HMI, cloud, and plant management remain out of scope.
  Preserve
  `docs/OPENVISIONLAB_3D_COMMERCIAL_VIDEO_DIRECTION_AND_PRIORITY_20260727.md`
  and `artifacts/current/20260727-commercial-video-direction/`.

- Thickness 4 x 2 repeat-grid authoring (2026-07-27): Inspection Workspace v3
  slice 7 is complete. One complete dual-ROI Thickness step now opens a
  display-only repeat review in Selected Tool with columns, rows, X/Z pitch,
  and `Tab {n}` naming. Reference candidates are cyan and Measurement
  candidates are orange. Request edits and Cancel do not change the recipe or
  invoke Preview, Publish, Run, Validation Set, or Save. Explicit Apply creates
  eight ordinary independently editable Thickness steps, eight outputs, and
  16 unique GridRectangle selections; the first identities remain stable and
  all later identities are deterministic. Save/reopen preserves the group and
  all instances. Current Release evidence passes build `0/0`, focused repeat
  authoring `20/20`, Inspection Workspace `26/26`, docking `33/33`, Shell
  smoke options `14/14`, Artifact Navigator `31/31`, height measurement
  `45/45`, recipe teaching `28/28`, Recipe Manager/WPG `37/37`, capture
  `25/25`, Validation Set `25/25`, display `103/103`, logging `4/4`,
  keyboard `3/3`, structure `17/17`, generated exact-source Runner `8/8`,
  actual Windows Viewer pointer/menu regression, and current wide/compact
  Review plus wide Applied screenshot quality.
  Preserve
  `docs/OPENVISIONLAB_3D_THICKNESS_REPEAT_GRID_AUTHORING_20260727.md` and
  `artifacts/current/20260727-thickness-repeat-grid/`. Workspace v3 is now
  `7/8` bounded slices (`87.5%`) complete. The only remaining v3 gate is the
  owner's unaided exact-source replay. Physical calibration/metrology remain
  unverified.

- Viewer Workspace composition (2026-07-27): Inspection Workspace v3
  implementation slice 6 is complete. The normal Workbench now exposes
  Single, side-by-side, stacked, and reusable pop-out Viewer layouts through
  one compact toolbar. A remains the current Workbench Viewer; B is a distinct
  real `OpenVisionThreeDViewerControl` with independent camera, projection,
  display, fit, HUD, and pointer state. The auxiliary selector accepts only
  existing C3D Output Compare candidates, so metric-only and feature-only
  outputs never fabricate a surface. Non-WPF `ViewerWorkspaceSession` owns
  layout, the auxiliary artifact pin, and focused slot; WPF adapters only host
  or move the reusable Viewer. Layout changes do not edit the recipe or invoke
  Preview, Publish, Run, Validation Set, or Save. Current Release evidence
  passes build `0/0`, Inspection Workspace `26/26`, docking/composition
  `32/32`, Shell smoke options `12/12`, Artifact Navigator/Output Compare
  `31/31`, height measurement `45/45`, recipe teaching `28/28`, Recipe
  Manager/WPG `37/37`, capture `25/25`, Validation Set `25/25`, display
  `103/103`, logging `4/4`, keyboard readiness `3/3`, structure `16/16`,
  exact-source Runner `8/8`, and all current split/pop-out screenshot quality
  gates. Preserve
  `docs/OPENVISIONLAB_3D_VIEWER_WORKSPACE_COMPOSITION_20260727.md` and
  `artifacts/current/20260727-viewer-workspace-composition/`. Workspace v3 is
  now `6/8` bounded slices (`75%`) complete. The next slice is bounded
  Thickness `4 x 2` repeat authoring; owner unaided exact-source replay remains
  the final gate. Physical calibration/metrology remain unverified.

- Selected Tool output evidence and actions (2026-07-27): Inspection Workspace
  v3 implementation slice 5 is complete. The normal Selected Tool Outputs
  section now exposes output identity, freshness, primary value, declared
  unit, Pass/Fail/Error state, availability, compare-pin state, and visible
  Show/Pin/Compare actions. Renderable C3D outputs delegate to the existing
  Viewer and Output Compare A/B/C owners; Pin uses the first empty slot and
  Compare activates the existing pane. Measurement and feature outputs retain
  real metric/overlay evidence but do not fabricate a standalone surface, so
  their actions remain visible and explain why they are evidence-only.
  Output selection/actions do not edit the recipe or invoke Preview, Publish,
  Run, Validation Set, or Save. Current Release evidence passes build `0/0`,
  selected-output/Artifact Navigator `29/29`, Inspection Workspace `21/21`,
  docking `31/31`, height measurement `45/45`, recipe teaching `28/28`,
  Recipe Manager/WPG `37/37`, capture `25/25`, Validation Set `25/25`,
  logging `4/4`, structure `15/15`, display `103/103`, exact-source Runner
  `8/8`, Shell smoke options `10/10`, keyboard readiness `3/3`, final Surface
  pointer/menu regression, and current screenshot quality. Preserve
  `docs/OPENVISIONLAB_3D_SELECTED_OUTPUT_ACTIONS_20260727.md` and
  `artifacts/current/20260727-selected-output-actions/`. Workspace v3 is now
  `5/8` bounded slices (`62.5%`) complete. The next slice is Viewer
  split/pop-out composition; bounded `4 x 2` repeat and owner acceptance
  remain later gates. Physical calibration/metrology remain unverified.

- ROI Review lifecycle and active-role contract (2026-07-27): Inspection
  Workspace v3 implementation slice 4 is complete. Reference and Measurement
  ROI now share one non-WPF `Missing -> Drawing -> Review -> Applied`
  lifecycle. Selected Tool shows the synchronized active role, `1/2` or `2/2`
  position, exact localized state, next action, and role-local Draw/Edit, Fit
  ROI, and Delete controls at wide and `1280 x 760` widths. At `2/2`, the
  candidate immediately enters Review; a third point is rejected; Apply/Cancel
  are prominent in both Selected Tool and the Viewer; Enter/Esc and
  empty-space orbit remain intact. Apply preserves the selection identity,
  while Cancel restores the previous Missing or Applied state. Fit ROI remains
  presentation-only. Narrow perspective-projected Tab ROIs use nearest-handle
  priority so the visible center marker remains Move and an actual corner
  remains Resize when their screen-space targets overlap. Current Release
  evidence passes build `0/0`, ROI
  lifecycle `21/21`, display `103/103`, docking `31/31`, recipe teaching
  `28/28`, height measurement `44/44`, Recipe Manager/WPG `37/37`, capture
  `25/25`, Validation Set `25/25`, logging `4/4`, structure `15/15`,
  exact-source Runner `8/8`, actual pointer regression, exact-source
  center-move/corner-resize/display-Y/same-ID Apply, two real `2/2` Review
  smoke boundaries, and Applied/Review screenshot quality. Preserve
  `docs/OPENVISIONLAB_3D_ROI_REVIEW_LIFECYCLE_20260727.md` and
  `artifacts/current/20260727-roi-review-lifecycle/`. Workspace v3 is now
  `4/8` bounded slices (`50%`) complete. The next slice is selected-output
  Show/Pin/Compare in Selected Tool; Viewer slots, bounded `4 x 2` repeat, and
  owner acceptance remain later gates. Physical calibration/metrology remain
  unverified.

- Viewer Top orthographic and Fit ROI (2026-07-27): Inspection Workspace v3
  implementation slice 3 is complete. The Viewer now exposes first-class
  `Top`, `Perspective`, `Fit all`, and `Fit ROI` actions at wide and
  `1280 x 760` widths. Top is a true X/Z orthographic projection rather than
  the prior near-top `pitch 80` perspective preset. Perspective restores the
  camera active before Top; empty-space left orbit exits Top into perspective;
  pan, wheel zoom, screen projection, pick rays, selected ROI corners, and
  fixed screen-space handles all use the active projection. Fit all preserves
  the current projection and Fit ROI centers the selected transient/applied
  Reference or Measurement GridRectangle without changing recipe geometry,
  measurement, or execution state. Surface remains the default C3D geometry
  style. Current Release evidence passes build `0/0`, display/projection
  `103/103`, docking `31/31`, recipe teaching `28/28`, height measurement
  `44/44`, Recipe Manager/WPG `37/37`, capture `24/24`, Validation Set
  `25/25`, logging `4/4`, structure `15/15`, exact-source Runner `8/8`,
  actual Windows pointer/menu/LOD regression, and current wide/compact
  screenshot quality. Preserve
  `docs/OPENVISIONLAB_3D_VIEWER_TOP_ORTHOGRAPHIC_AND_FIT_ROI_20260727.md` and
  `artifacts/current/20260727-viewer-top-fit-roi/`. Workspace v3 is now
  `3/8` bounded slices (`37.5%`) complete. The next slice is the compact
  dual-role ROI Review lifecycle; selected-output commands, Viewer slots,
  4 x 2 repeat, and owner acceptance remain later gates. Physical
  calibration/metrology remain unverified.

- Inspection Workspace default composition (2026-07-27): v3 implementation
  slice 2 is complete. The normal Workbench is now `Tool Catalog -> Recipe
  Chain -> Selected Tool -> dominant 3D Viewer`; the permanent journey strip
  is replaced by one compact recipe/input/state/selected-tool command bar with
  explicit Preview, Run all, and Save. Existing recipes hide the Catalog's
  large first-use card. `RecipeChainView` owns ordered step scanning and keeps
  the former entity/reference explorer under a collapsed advanced route.
  `SelectedToolWorkspaceView` is the one normal Inputs/Parameters/Regions/
  Outputs/Help surface, reuses the exact typed PropertyGrid, and starts with
  compact Reference/Measurement ROI actions visible even at `1280 x 760`.
  The prior full Inspector remains in Advanced layout and specialized Tool Lab
  routes remain available. Current Release evidence passes build `0/0`,
  selection `12/12`, docking/composition `31/31`, recipe teaching `28/28`,
  height measurement `44/44`, Recipe Manager/WPG `37/37`, capture `24/24`,
  logging `4/4`, structure `15/15`, and current wide/compact screenshot
  quality. Preserve
  `docs/OPENVISIONLAB_3D_INSPECTION_WORKSPACE_DEFAULT_COMPOSITION_20260727.md`
  and
  `artifacts/current/20260727-inspection-workspace-layout/`. The Top
  orthographic/Fit ROI priority named by this historical slice is complete in
  the newer checkpoint above. ROI Review polish, selected-output commands,
  Viewer slots, and 4 x 2 repeat remain later gates. Owner unaided replay and
  physical calibration/metrology remain unverified.

- Inspection Workspace selection boundary (2026-07-27): v3 implementation
  slice 1 is complete without changing default XAML.
  `InspectionWorkspaceSelectionSession` now owns the non-WPF identity for the
  selected step, input, ROI role/selection, output, and focused Viewer slot.
  `SelectedToolWorkspaceViewModel` projects the selected step into
  Inputs/Parameters/Regions/Outputs/Help while reusing the exact existing
  PropertyGrid draft and established recipe, ROI, artifact, and execution
  owners. The root Workbench synchronizes existing step and Viewer ROI
  selection with the session; selection-only changes cannot dirty, reroute, or
  execute the recipe. Current Release evidence passes build `0/0`, focused
  selection `12/12`, recipe teaching `28/28`, height measurement `44/44`,
  docking `29/29`, and structure `15/15`. Preserve
  `docs/OPENVISIONLAB_3D_INSPECTION_WORKSPACE_SELECTION_BOUNDARY_20260727.md`
  and
  `artifacts/current/20260727-inspection-workspace-selection/`. The next
  bounded priorities named by this historical slice, default workspace XAML
  composition and Top/Fit ROI, are complete in the newer checkpoints above.
  Compact ROI lifecycle, outputs, Viewer slots, and 4 x 2 repeat remain later
  gated slices. Physical calibration and metrology remain unverified.

- GoPxL supplied-video workflow redirection (2026-07-27): the two owner-
  supplied local GoPxL videos and subtitles were reviewed over their full
  `04:04.874` and `11:44.494` durations, with full contact sheets and 24 key
  scenes. The owner's concern is valid: adding more journey cards, ribbons,
  and dock tabs to the current default Workbench would increase later Shell
  rework. Preserve the validated C3D loader, Viewer renderer, typed recipe and
  artifact contracts, Tools adapters, Runner, ROI geometry, persistence, and
  verification suites. Pause broad tool/UI expansion and first approve and
  implement the bounded Inspection Workspace v3 contract: Catalog -> Recipe
  Chain -> one Selected Tool with Inputs/Parameters/Regions/Outputs/Help -> a
  dominant Viewer with Top/Perspective/Profile and visible output pins.
  Preview, Publish, Run, and save boundaries remain explicit. Prove the new
  composition with the exact eight-Tab Thickness workflow before resuming tool
  breadth. Preserve
  `docs/OPENVISIONLAB_3D_GOPXL_VIDEO_WORKFLOW_GAP_AND_REDIRECTION_20260727.md`
  `docs/OPENVISIONLAB_3D_INSPECTION_WORKSPACE_V3_INTERACTION_SPEC_20260727.md`,
  `artifacts/current/20260727-gopxl-gap-analysis/`, and
  `artifacts/current/20260727-inspection-workspace-v3/`. The fixed first-slice
  path is New -> exact C3D -> Thickness -> typed input -> Reference ROI ->
  Measurement ROI -> parameters -> explicit Preview -> bounded `4 x 2`
  repeat candidate -> per-Tab review -> explicit Run/Save -> reopen.
  Repeat-grid Apply creates eight ordinary steps and 16 unique selections but
  never executes inspection. This is a Workbench redirection, not a full
  rewrite, copied GoPxL visual design, free-form graph, hardware-platform
  expansion, or physical-metrology claim.

- Eight-Tab Thickness self-test integration (2026-07-26): the exact
  `thickness-coupon-v1.C3D` source is now covered by
  one reproducible schema `1.3` model containing eight independently named
  Thickness steps and 16 artifact-owned GridRectangle selections. Each Tab
  retains its instance name after save/reopen and remains editable through the
  existing Reference/Measurement ROI workflow. Validation Set now stages the
  current recipe input with an explicit `Add current input` command; staging
  never runs inspection. Explicit Run All produces eight real
  `DualSurfaceThicknessRule` records. Current Release evidence passes build
  `0/0`, structure `15/15`, recipe teaching `28/28`, Validation Set `25/25`,
  height measurement `44/44`, Recipe Manager/WPG `37/37`, docking `29/29`,
  actual Runner `8/8`, and current before/after screenshot quality. Preserve
  `docs/OPENVISIONLAB_3D_TAB_THICKNESS_SELF_TEST_DESIGN_20260726.md`,
  `scripts/generate-thickness-coupon-sample.py`, and
  `artifacts/current/20260726-tab-thickness-model/`. The starter limits are
  deliberately broad software-connectivity limits in `raw-height`; the owner
  must confirm the physical datum, calibration, units, and production
  tolerances before interpreting the values as certified thickness.

- ROI overlay Y-position clarity correction (2026-07-26): owner review
  correctly identified that the former `ROI 표시 높이` control did not increase
  ROI height. It moved one X/Z overlay plane along Y and therefore looked
  unchanged or misleading. The Viewer now labels it
  `ROI 오버레이 Y 위치 · 보기 전용`, states that ROI size and measurement are
  unchanged, and shows `surface -> overlay | ΔY`. A non-zero offset renders a
  cyan local-surface outline plus broken corner guides to the selected yellow
  overlay; it deliberately draws no solid vertical walls. Current Release
  evidence passes build `0/0`, teaching capture `24/24`, height measurement
  `44/44`, recipe teaching `27/27`, docking `29/29`, logging `4/4`, structure
  `15/15`, and current actual-window before/after comparison. Preserve
  `docs/OPENVISIONLAB_3D_ROI_OVERLAY_Y_POSITION_CLARITY_20260726.md` and
  `artifacts/current/20260726-roi-overlay-y-position/`. `GridRectangle`
  remains an X/Z footprint; a persisted Y extent still requires a separate
  typed ROI and measurement/filter contract. The next product evidence gate
  remains the owner's unaided first-recipe replay; physical
  calibration/metrology remain unverified.

- Commercial ROI workflow and review-mode correction (2026-07-26): owner
  replay showed that Reference/Measurement ROI actions did not look
  actionable, a ready `2/2` candidate still felt like endless drawing, and
  the display-height axis was unclear. Official Cognex, Autodesk, MVTec, and
  Artec workflows were compared. The bounded correction uses primary
  `ROI 그리기` actions, switches a ready GridRectangle from crosshair capture
  to an explicit `그리기 완료 · 검토 모드`, rejects additional capture,
  returns empty-space left drag to Viewer orbit, retains corner/center/Y
  handles, and provides Enter Apply/Esc Cancel. The Viewer labels display
  height `Y축 · Z=행`; this remains view-only. Bounded shortcuts now cover
  Ctrl+N/O/S, Ctrl+Shift+S/O, F5, Ctrl+F5, Enter, and Esc. Current Release
  evidence passes build `0/0`, Recipe Manager/WPG `37/37`, height measurement
  `44/44`, docking/shortcuts `29/29`, capture `24/24`, teaching `27/27`,
  actual-pointer stability `3/3`, and current screenshot quality. Preserve
  `docs/OPENVISIONLAB_3D_COMMERCIAL_ROI_WORKFLOW_AND_REVIEW_MODE_20260726.md`
  and `artifacts/current/20260726-shortcuts-and-roi-capture/`.
  `GridRectangle` remains an X/Z footprint, not a persisted XYZ volume. The
  next product evidence gate remains the owner's unaided first-recipe replay;
  physical calibration/metrology remain unverified.

- Inspection Flow and PropertyGrid usability correction (2026-07-26): owner
  replay showed that the fixed `150 px` typed PropertyGrid clipped its last
  Filter row and that reorder/delete were hidden in the collapsed English
  advanced section. The PropertyGrid is now `210 px`; the selected inspection
  step directly exposes localized detailed-settings, up, down, and remove
  actions through existing ViewModel commands; the current navigator item is
  highlighted; and the advanced section retains only Step ID. Reorder/remove
  remain recipe-only and do not invoke Preview, Run, or Publish. Current
  Release evidence passes build `0/0`, Recipe Manager/WPG `37/37`, recipe
  teaching `27/27`, docking `28/28`, and current actual-window comparison.
  Preserve
  `docs/OPENVISIONLAB_3D_INSPECTION_FLOW_PROPERTY_GRID_USABILITY_20260726.md`
  and
  `artifacts/current/20260726-inspection-flow-property-grid-usability/`.
  Attempt 7 later exposed the ROI workflow defect recorded by the newer
  commercial-ROI checkpoint above. The full unaided recipe workflow and
  physical calibration/metrology remain unverified.

- Last-recipe startup restoration (2026-07-26): owner replay proved that a
  normal restart discarded the prior recipe session even though
  `recent-recipes.json` retained ordered paths. A normal Shell start now opens
  the most recent available recipe through the existing recipe/source loading
  adapter without Preview, Run, or Publish. Workbench verification instances
  use process-temporary recent state, while normal composition explicitly
  owns the persistent LocalAppData path, preventing smoke fixtures from
  becoming the operator's startup recipe. Current Release evidence passes
  build `0/0`, Recipe Manager/WPG `35/35`, Shell command-line isolation `9/9`,
  recipe teaching `27/27`, structured startup diagnostics, and actual
  before/after Windows captures. Preserve
  `docs/OPENVISIONLAB_3D_LAST_RECIPE_STARTUP_RESTORE_20260726.md` and
  `artifacts/current/20260726-last-recipe-startup/`. The corrected EXE is open
  for owner replay attempt 6. This does not yet complete the owner's full
  unaided recipe creation/save/reopen gate or physical calibration/metrology.

- Executable code-structure guard (2026-07-26):
  `scripts/verify-code-structure.ps1` now enforces 12-project `.sln`/`.slnx`
  parity, exact runtime-neutral Core/Data/Tools/Runner references, forbidden
  UI/renderer packages, Shell/Runner command routers, Workbench Viewer display
  ownership, and shared model-transform ownership. The current workspace
  passes `15/15`; an intentional temporary `.slnx` drift removing
  MessageDialogs fails `14/15` with exit code `1` and the exact missing
  project. Preserve
  `docs/OPENVISIONLAB_3D_CODE_STRUCTURE_GUARD_20260726.md` and
  `artifacts/current/20260726-code-structure-guard/`. Run this guard before
  completing future structural work. It does not replace semantic MVVM review
  or focused tests. The next product evidence gate remains the owner's
  unaided first-recipe replay.

- Refactor baseline and code-rules closure (2026-07-26): Shell verification
  dispatch, non-teaching Workbench-to-Viewer display lifecycle, Tool Lab smoke
  orchestration, Runner CLI dispatch, and shared model-transform calculation
  now have explicit owners. The standard `.sln` and `.slnx` contain the same 12
  code projects. Current Release evidence passes build `0/0`, Shell command
  routing `9/9`, docking `28/28`, Recipe Manager/WPG `34/34`, Artifact
  Navigator/Output Compare `25/25`, Validation Set `24/24`, height measurement
  `42/42`, teaching `27/27`, logging `4/4`, Runner affine apply `4/4`, and
  byte-identical before/after Shell screenshots. Preserve
  `docs/OPENVISIONLAB_3D_FINAL_REFACTOR_AND_CODE_RULES_20260726.md`,
  `docs/OPENVISIONLAB_3D_CODE_RULES.md`, and
  `artifacts/current/20260726-final-refactor-and-code-rules/`. This is an
  MVVM/ownership baseline, not a zero-code-behind claim: WPF dialogs,
  AvalonDock, PropertyGrid flush, OpenGL/pointer rendering, and screenshot
  capture remain View adapters. Do not reopen the refactor solely for file
  length. The next product evidence gate remains the owner's unaided
  first-recipe replay; physical calibration/metrology remain unverified.

- Output Compare session refactor (2026-07-26): session-only candidate, A/B/C
  pin, pin-preservation, lookup, and summary state now belongs to
  `ToolWorkbenchOutputCompareSession`; `ToolWorkbenchViewModel` only discovers
  candidates from current recipe/artifact/validation state and preserves the
  existing binding facade. Viewer creation/loading remains a WPF/OpenGL View
  adapter. Current Release evidence passes build `0/0`, Artifact
  Navigator/Output Compare `25/25`, Validation Set `24/24`, docking `28/28`,
  and current screenshot quality on attempt 1. Preserve
  `docs/OPENVISIONLAB_3D_OUTPUT_COMPARE_SESSION_REFACTOR_20260726.md` and
  `artifacts/current/20260726-output-compare-session-refactor/`. Do not extract
  Validation Set behind callback-heavy abstractions without a new independent
  execution seam. The next product evidence gate remains the owner's unaided
  first-recipe replay; physical calibration/metrology remain unverified.

- MVVM View-boundary checkpoint (2026-07-26): Workbench review-tab state,
  validation-source selection, PropertyGrid dirty/apply/discard actions, and
  Tool Lab step selection now route through `ToolWorkbenchViewModel`
  properties, commands, and request events. Shell file dialogs, AvalonDock
  layout, PropertyGrid binding flush, and OpenGL/pointer rendering remain
  explicit View adapters. Current Release evidence passes build `0/0`,
  docking/navigation `28/28`, Recipe Manager/WPG/MVVM commands `34/34`,
  Validation Set `24/24`, and current screenshot quality on attempt 1.
  Preserve `docs/OPENVISIONLAB_3D_MVVM_VIEW_BOUNDARIES_20260726.md` and
  `artifacts/current/20260726-mvvm-view-boundaries/`. This is a structural
  ownership checkpoint, not a zero-code-behind claim. The next product evidence
  gate remains the owner's unaided first-recipe replay; physical
  calibration/metrology remain unverified.

- Surface ROI display-height update (2026-07-25): selected C3D `GridRectangle` overlays now use a local finite-sample median instead of the whole-source mean and expose a screen-space `Y ↕` drag handle, numeric raw-height offset, `-`/`+`, `Alt+wheel`, and `To surface` reset in the Viewer. The offset is per-selection, view-only, cleared on source change, and never enters the recipe, measurement, Preview, Publish, Run, or Validation Set. Row count is now labeled `Z length (rows)` rather than Height. Completed adjustments write structured `viewOnly=true | recipeChanged=false | inspectionRun=false` diagnostics. Current Release evidence passes build `0/0`, teaching capture `24/24`, height measurement `42/42`, recipe teaching `27/27`, docking `28/28`, logging `4/4`, actual Windows-pointer move/resize/Y-handle drag with authored/execution/camera unchanged, and wide/`1280 x 760` screenshot quality on attempt 1. Preserve `docs/OPENVISIONLAB_3D_ROI_DISPLAY_HEIGHT_20260725.md` and `artifacts/current/20260725-roi-display-height/`. `GridRectangle` remains an X=column/Z=row footprint; this is not persisted annotation height, an XYZ volume, physical calibration, or metrology. The next product evidence gate remains the owner's unaided first-recipe replay.

- Owner ROI/save diagnostics correction (2026-07-25): owner use reopened the dual-surface workflow because the two ROI cards hid the only generic Delete action, current Reference-only Thickness routes were misread as legacy Measurement-only routes, incomplete teaching drafts could not be saved, and Workbench session actions were not persisted to the application log. Reference and Measurement cards now have direct bilingual Delete actions; Viewer selection synchronizes the exact ROI role; current schema `1.3` Reference-only routes enable Measurement capture while schema `1.2` legacy one-ROI routes retain Measurement semantics; incomplete routes save as drafts while strict Preview/Run remains blocked. The existing Dev-derived `OpenVisionLab.Logging`/Controls projects and DLLs were already present, so no duplicate logger was added; Workbench capture/apply/reject/delete/save events now write structured `key=value` evidence to `Log\OpenVisionLab-ALL.log`. New-step selection is folded into the existing authored-recipe refresh instead of performing a second full selected-step refresh; at the historical `1920 x 1040` viewport the current three-run medians are `2.869 ms` tool selection, `140.890 ms` add, `58.234 ms` selected-step refresh, and `112.892 ms` UI apply, with one marginal `153.468/158.239 ms` add/UI outlier retained rather than hidden. Current Release evidence passes build `0/0`, height measurement `42/42`, logging `4/4`, recipe selections `17/17`, recipe teaching `27/27`, docking `28/28`, Recipe Center/WPG `28/28`, teaching capture `20/20`, actual Viewer-pointer Measurement delete/recreate/Apply/save, and current screenshot quality. Preserve `docs/OPENVISIONLAB_3D_OWNER_ROI_SAVE_DIAGNOSTICS_20260725.md` and `artifacts/current/20260725-owner-roi-save-diagnostics/`. The next product evidence gate remains the owner's unaided first-recipe replay; physical calibration/metrology remain unverified.

- Dual-surface Thickness and Surface-default update (2026-07-25): generic `Thickness` now consumes `HeightField -> Reference GridRectangle -> Measurement GridRectangle`, fits `height = a*u + b*v + c` on the reference surface, and checks every finite measurement sample's signed H-axis separation against the existing minimum/maximum limits. Evidence includes mean/min/max/range/RMS spread, reference-fit H RMS, both sample counts, out-of-limit counts, and four typed overlays. A legacy one-ROI step preserves that ROI as Measurement but fails closed until a Reference ROI is taught; teaching upgrades the route without Preview/Publish/Run. New C3D sources with surface topology default to `Surface`; Wireframe remains selectable. Current Release evidence passes build `0/0`, display `96/96`, height measurement `33/33`, recipe teaching `27/27`, docking `28/28`, artifact-owned Runner `18/18`, Synthetic Affine `18/18`, Validation Set replay, actual Windows-pointer move/resize plus explicit same-ID Apply, and screenshot quality at `1920 x 1040` and `1280 x 760`. Preserve `docs/OPENVISIONLAB_3D_DUAL_SURFACE_THICKNESS_AND_SURFACE_DEFAULT_20260725.md` and `artifacts/current/20260725-dual-surface-thickness/`. This is declared-unit software evidence, not physical calibration, uncertainty, or metrology proof. The next product evidence gate remains the unaided owner first-recipe replay.

- Surface ROI usability correction (2026-07-25): owner review reopened the prior practical-usability claim. The active C3D `GridRectangle` is now rendered depth-independently with a translucent yellow fill, `4 px` outline, four `16 px` handles, and a center move marker; the authored rectangle remains a thin blue outline while replacement is active. Interaction uses fixed `18 px` screen-space handle targets, hover cursor/status feedback, and X/Z mean-footprint plane intersection instead of requiring a valid rendered surface point, so missing cells do not interrupt capture, move, or resize. The compact numeric editor is first in Teaching Selections and exposes Start X/Start Z/Width/Height at `1280 x 760`; the Viewer ribbon remains the primary Apply/Cancel location. Current Release evidence passes build `0/0`, display/camera `96/96`, teaching capture `20/20`, height measurement `32/32`, recipe teaching `27/27`, docking `28/28`, actual Windows-pointer hover/move/corner-resize, explicit same-identity Apply, and screenshot quality at `1920 x 1040` and `1280 x 760`. Preserve `docs/OPENVISIONLAB_3D_SURFACE_ROI_USABILITY_CORRECTION_20260725.md` and `artifacts/current/20260725-surface-roi-usability-correction/`. Editing never invokes Preview/Publish/Run. `GridRectangle` remains an X=column/Z=row height-field footprint; `OrientedBox3D` remains a separate future type. The next product evidence gate is the unaided owner first-recipe replay; current Thickness remains one-ROI scalar height statistics, not calibrated two-surface physical thickness.

- Surface ROI editing update (2026-07-24): the existing C3D `GridRectangle` now has a synchronized selected state across Viewer, Inspection Flow, and Step Parameters. The selected footprint is yellow with four corner handles and a bilingual label; `Replace ROI` opens the authored rectangle as a ready `2/2` transient candidate; inside drag moves it, corner drag resizes it, and row/column/count numeric fields validate against the exact bound source grid. Cancel retains authored geometry. Explicit Apply replaces the same selection identity and never invokes Preview/Publish/Run. Current Release evidence passes build `0/0`, teaching capture `20/20`, height measurement `32/32`, recipe teaching `27/27`, docking `28/28`, Recipe Center/WPG `28/28`, artifact/navigator `24/24`, actual Windows-pointer edit plus explicit Apply, and screenshot quality at `1920 x 1040` and `1280 x 760`. Preserve `docs/OPENVISIONLAB_3D_SURFACE_ROI_EDITING_20260724.md` and `artifacts/current/20260724-surface-roi-editing/`. `GridRectangle` remains an X=column/Z=row height-field footprint; any XYZ center/size/rotation volume is a separate typed `OrientedBox3D`. The next product evidence gate is the unaided owner first-recipe replay; current Thickness is still one-ROI scalar height statistics, not calibrated two-surface physical thickness.

- Tool-add and Workbench-response update (2026-07-24): the full Inspection Tools catalog now exposes a bilingual inline `+` action on every row plus double-click add, removes the off-screen global footer action, and uses recycling virtualization. Selection remains read-only; add creates one selected typed step and never invokes Preview/Publish/Run. Pure selection no longer rebuilds compatibility or logs, selected-step focus refreshes only its adapter state, recipe reorder suppresses recursive refresh, derived presentation collections use one Reset, and loaded WPG refreshes coalesce. Three actual Release EXE runs pass fixed local budgets with medians `3.533 ms` tool selection, `90.421 ms` add, `31.142 ms` step focus, and `122.333 ms` UI apply; build `0/0`, teaching `27/27`, docking `28/28`, Recipe Center/WPG `28/28`, height measurement `28/28`, artifact/navigator `24/24` plus explicit add, and screenshot quality pass. Preserve `docs/OPENVISIONLAB_3D_TOOL_ADD_AND_WORKBENCH_RESPONSE_20260724.md` and `artifacts/current/20260724-tool-add-workbench-response/`. The next internal UI priority is Surface ROI selected/move/resize/numeric editing. Keep existing `GridRectangle` as a height-field footprint; any center/size/rotation XYZ volume must be a separate typed `OrientedBox3D`, not a reinterpretation of saved recipes. These timings are fixed local smoke evidence, not broad hardware/general-UI proof.

- Thickness ROI draw-guidance update (2026-07-24): the owner follow-up proved the prior text-only guidance was insufficient because GridRectangle left-drag still rotated the camera and Step Parameters was separated from Inspection Flow. The wide dock order is now `Inspection Tools -> Inspection Flow -> Step Parameters -> 3D View`; compact layout tabs the first two and retains Parameters before Viewer. GridRectangle capture accepts either two opposite left-clicks or one diagonal left-drag, with crosshair cursor, yellow rubber-band, and bilingual in-Viewer `0/2 -> 1/2 -> ready` guidance. The capture ribbon spans the full Viewer header so Apply is visible at `1280 x 760`. Current Release evidence passes build `0/0`, docking `28/28`, height measurement Workbench `28/28`, capture ViewModel `18/18`, and actual Windows-pointer drag plus current screenshot quality at both `1920 x 1040` and `1280 x 760`; camera, authored recipe, Preview, and result state remain unchanged until explicit Apply/Preview. Preserve `docs/OPENVISIONLAB_3D_THICKNESS_ROI_DRAW_GUIDANCE_20260724.md` and `artifacts/current/20260724-thickness-roi-draw-guidance/`. The owner first-recipe replay must restart; current Thickness remains one-ROI scalar height statistics, not calibrated two-surface physical thickness.

- Thickness ROI guided-teaching update (2026-07-24): the generic Thickness step now exposes a bilingual, explicit `Capture/Replace ROI -> two opposite Viewer corners -> Apply -> tolerances -> Preview` sequence in Step Parameters. Generic selection controls and the active capture ribbon are localized and use the existing WPF UI icon set; teaching remains recipe-only and never invokes Preview/Publish/Run. Current Release evidence passes build `0/0`, generic height measurement Workbench `28/28`, teaching capture `18/18`, actual-EXE Thickness replacement-capture entry at `0/2` without dirtying or execution, and Korean/English/active-state screenshot quality on attempt 1. Preserve `docs/OPENVISIONLAB_3D_THICKNESS_ROI_GUIDED_TEACHING_20260724.md` and `artifacts/current/20260724-thickness-roi-guided-teaching/`. The current Thickness adapter is still one-ROI scalar height statistics, not calibrated two-surface physical thickness. The unaided owner first-recipe replay must restart on the updated EXE; real four-landmark trust remains externally blocked.

- Release multi-C3D performance update (2026-07-24): the actual input-first Shell EXE now reuses the exact loaded Viewer C3D identity for Viewer-to-Workbench handoff instead of re-reading and re-analyzing the same source. Redundant already-empty teaching/tool overlay clears are idempotent, so one successful source application retains one final render while intermediate requests remain coalesced. On the largest available `1621 x 3317`, `21,507,436`-byte fixture, Release whole transition fell `1,563 -> 565 ms` (`-63.9%`) and Workbench source binding/state fell `886.538 -> 32.951 ms` (`-96.3%`). A seven-source, six-grid, three-repetition Release actual-EXE matrix passes `21/21` up to `21.5 MB`; all runs use VBO/IBO with GPU-ready state and zero fallback, Workbench remains under `200 ms`, Viewer apply remains under `300 ms`, and cancellation/missing-source retention pass. Preserve `docs/OPENVISIONLAB_3D_SYNTHETIC_THICKNESS_SAMPLE_MIGRATION_20260728.md`, `scripts/verify-c3d-release-matrix.ps1`, and `artifacts/current/20260724-release-multi-c3d-performance/`. This is fixed local Windows/NVIDIA GTX 1060 evidence, not multi-GPU, arbitrary-large-data, production batch, physical calibration, or metrology proof. The next product evidence gate is an unaided owner first-recipe replay; real four-landmark trust remains externally blocked.

- Validation failure-analysis UX update (2026-07-24): the existing docked bilingual `Validation Set` now exposes Pass/Fail/Error counts and filters, previous/next issue navigation, progress/cancel, first-problem selection, and read-only per-step Metric/Overlay evidence. `Open 3D comparison` pins the authored source in A and the selected validation C3D in B without modifying the recipe or main Viewer input; no intermediate surface is fabricated. Validation Set uses an analysis-height dock ratio, with a larger compact-height allocation at `1280 x 760`; Output Compare now uses `0.82:1` so both real Viewer cards remain visible at `1920 x 1040`, superseding the historical `1.2:1` split. Current evidence passes build `0/0`, Validation Set `24/24`, docking `27/27`, recipe teaching `25/25`, Synthetic Affine `18/18`, and Korean/English actual-EXE screenshot quality on attempt 1. Preserve `docs/OPENVISIONLAB_3D_VALIDATION_FAILURE_ANALYSIS_UX_20260724.md` and `artifacts/current/20260724-validation-failure-analysis/`. This is local synthetic repeat-analysis evidence, not production batch/history infrastructure, arbitrary DAG replay, real alignment, physical calibration, or metrology. The next internal development priority is Release/large-data/multi-C3D Viewer performance generalization; trusted real four-landmark data remains an external prerequisite.

- Viewer owner-feedback correction (2026-07-24): C3D now loads into a current-bounds near-top inspection Fit (`yaw 0`, `pitch 80`) rather than fixed `yaw 34 / pitch 52 / distance 13.2`; Fit All, selected C3D Fit, load, and left double-click share the same transformed-position/aspect/FOV camera-fit path. Repeated C3D zoom reaches `1%` of the source Fit distance, projection and pick-ray near planes match at `0.01`, wheel renders coalesce, and the visible bilingual hint exposes double-click Fit. The C3D contract remains `X=column / Y=height / Z=row` with valid sampled-grid neighbors connected; default Wireframe is not vertical ground extrusion. After VBO adoption, a new same-viewport three-run check supersedes the older Display List 60 FPS rejection: 60 FPS median next-frame average/maximum is `16.140/29.510 ms` versus the current 30 FPS checkpoint `21.626/43.825 ms`, with pointer/double-click `3/3`, upload delta `0`, source reload `False`, and immediate MouseMove renders `0`; 60 FPS is retained. Current evidence passes build `0/0`, display/camera `95/95`, geometry `12/12`, recipe `25/25`, docking `27/27`, actual C3D load, and screenshot quality. Preserve `docs/OPENVISIONLAB_3D_VIEWER_FIT_ZOOM_HEIGHTFIELD_20260724.md` and `artifacts/current/20260724-owner-viewer-feedback/`. The unaided owner first-recipe replay is `Incomplete` and must restart; physical calibration/metrology remain unverified.





- Viewer/runtime localization update (2026-07-24): this checkpoint supersedes the remaining Viewer slice named by the 2026-07-23 fixed-label checkpoint. Shared Viewer controls, geometry/color/density display values, context commands, orientation label, measurement HUD, camera/model status, and selected Expert comparison/evidence summaries now switch between Korean and English through the existing localization service. Localization is display-only: stored geometry/color IDs, typed entity IDs, recipe JSON, coordinate symbols, algorithm contracts, and raw Runner report payloads remain unchanged. Current Debug evidence passes build `0/0`, Viewer display/runtime verification `92/92`, docking `27/27`, and four current actual-EXE Workbench/Expert captures at `1920 x 1040` or `1280 x 760` on attempt 1. Preserve `docs/OPENVISIONLAB_3D_VIEWER_RUNTIME_LOCALIZATION_20260724.md` and `artifacts/current/20260724-viewer-runtime-localization/`. This does not mean every technical or persisted runtime string is translated. The next product evidence priority is an unaided owner first-recipe replay; do not rescore the owner-approved scoped UI `85/100` without that replay or infer physical calibration/metrology readiness.

- UI localization/density update (2026-07-23): the fixed structural labels in all ten exposed Tool Labs, Calibration, and Expert now switch between separate Korean and English states through the existing localization service. Tool Lab titles and commands, Calibration navigation/tables/status, Expert sections/forms/evidence tabs, and view-only WPG display names/categories/search are localized; the XYZ Affine Solve typed-route badge is no longer clipped and the WPG name column is wider. Stored CLR property names, recipe JSON, typed IDs, coordinate symbols, and algorithm contracts are unchanged. Current evidence passes build `0/0`, Calibration ViewModel `72/72`, docking `27/27`, Recipe Center/WPG `28/28`, and eight Korean/English actual-EXE captures at `1920 x 1040`, `1280 x 760`, or the Tool Lab default size on attempt 1. Preserve `docs/OPENVISIONLAB_3D_UI_LOCALIZATION_AND_DENSITY_20260723.md` and `artifacts/current/20260723-ui-localization-density/`. Shared Viewer HUD/control text and dynamic execution/evidence summaries remain the next localization slice. Do not claim every runtime string is localized, rescore the owner-approved UI `85/100` without a new owner replay, or infer physical calibration/metrology readiness.

- Calibration availability UI update (2026-07-23): Calibration Center now exposes only the implemented `Overview` and `Repeatability` sections as selectable. Height Calibration, Sensor Alignment, Calibration History, Profile History, Sensor Transform, and profile Validate/Activate remain visible but disabled with bilingual `준비 중 / Coming soon` state and explanation; typed selection attempts are ignored. The narrow inspector keeps the implemented Calculate action and no longer clips three misleading lifecycle buttons. Current evidence passes build `0/0`, Calibration Center ViewModel `70/70`, docking `27/27`, and Korean/English actual-EXE screenshot quality on attempt 1. Preserve `docs/OPENVISIONLAB_3D_CALIBRATION_AVAILABILITY_UI_20260723.md` and `artifacts/current/20260723-calibration-availability/`. Do not enable a roadmap section until its typed contract, workflow, and evidence gate exist; this is not physical calibration, Gauge R&R, or metrology evidence.

- Output Compare usable-default update (2026-07-23): selecting the docked `Output Compare` view now changes only the existing AvalonDock vertical split from the standard `2:1` workbench/evidence ratio to `1.2:1`, then restores `2:1` when another bottom view is selected. The A/B/C cards remove the redundant repeated label and keep a `390` minimum content height, so a loaded comparison Viewer is visible at the default `1920 x 1040` work area instead of being reduced to a thin strip. Existing float/dock and explicit output pinning remain unchanged. Current evidence passes build `0/0`, docking `27/27`, artifact navigation, actual EXE exit `0`, and Shell/embedded-Viewer screenshot quality on attempt 1. Preserve `docs/OPENVISIONLAB_3D_OUTPUT_COMPARE_USABLE_DEFAULT_20260723.md` and `artifacts/current/20260723-output-compare-usable-default/`. This proves default layout usability for the fixed local resolution and source slot; it does not prove physical parity, multi-monitor DPI coverage, or three simultaneously published real outputs.

- Run Record history/export UX update (2026-07-23): the existing docked bilingual `Run Record` tab now exposes Open record, current JSON/HTML/CSV, Open folder, collision-safe bundle export, and a newest-first bounded list of ten recent valid record paths. Loading a record is read-only and never edits the recipe, changes the Viewer source, or executes inspection. Invalid JSON retains the current record; schema `1.3` and `1.4` load; export copies existing JSON/HTML/CSV byte-for-byte into a new `RunRecord-<RunId>` folder without silent overwrite. Current evidence passes Shell build `0/0`, focused history/load/export `8/8`, docking `27/27`, recipe teaching `25/25`, and Korean/English actual-EXE screenshot quality including `1920 x 1040` and `1280 x 760` on attempt 1. Preserve `docs/OPENVISIONLAB_3D_RUN_RECORD_HISTORY_UX_20260723.md` and `artifacts/current/20260723-run-record-history-ux/`. This is local recent-file UX, not a production result database, batch execution/history, trend analytics, audit retention, physical calibration, or metrology. The next evidence gate is an unaided owner open/reopen/export replay.

- General ordered graph Run Record update (2026-07-23): the existing `ToolRecipeOrderedGraphExecution` remains the sole execution owner, while the production Runner and existing docked bilingual Run Record view now consume its complete result. Schema `1.4` records all 27 authored sequential typed steps with exact order, tool/input/output IDs, status/message/time, metrics, overlays, and optional per-step output-content SHA-256; feature-only rows remain present in HTML/CSV. The fixed Synthetic Affine Plate completes Pass `27/27`, and a controlled measurement sample completes expected Fail `27/27` while retaining later evidence. Current Debug evidence passes build `0/0`, writer `21/21`, schema `1.3` regression `21/21`, recipe teaching `25/25`, docking `26/26`, and Korean/English actual-EXE screenshot quality on attempt 1. Preserve `docs/OPENVISIONLAB_3D_GENERAL_GRAPH_RUN_RECORD_20260723.md` and `artifacts/current/20260723-general-graph-run-record/`. Schema `1.2` single-step and schema `1.3` bounded multi-step paths remain compatible. This closes durable reporting for the registered sequential graph, not arbitrary DAG execution, production batch/history infrastructure, trusted real alignment, physical calibration, or metrology.

- Ordered graph Validation Set update (2026-07-23): the dockable bilingual `Validation Set` now replays each explicit same-grid C3D sample through all currently executable typed tools in authored INPUT -> OUTPUT order: Filter, Height Difference Edge, 2-Point Line, 3-Point Plane, datum-plane deviation, 3D Line Fit, Line Intersection, Landmark Correspondence, XYZ Affine Solve/Apply, Re-grid, and seven measurement adapters. Per-sample rebinding is ephemeral; raw PointSet captures and artifact-owned A3 selections are refreshed from exact locators/owner/hash/grid/frame/unit identity, while the authored recipe and Viewer source remain unchanged. Upstream Error/NotRun/missing artifacts fail closed; a measurement tolerance Fail preserves later evidence. The 27-step Synthetic Affine Plate passes all steps, its selected output hashes match the established direct adapters, a modified measurement sample completes Fail with later Warpage evidence, and a missing-edge sample stops at step 2. Current Debug evidence passes build `0/0`, Validation Set `16/16`, Synthetic Affine Plate `18/18`, recipe teaching `25/25`, docking `26/26`, and Korean/English actual-EXE screenshot quality on attempt 1. Preserve `docs/OPENVISIONLAB_3D_ORDERED_GRAPH_VALIDATION_20260723.md` and `artifacts/current/20260723-ordered-graph-validation/`. The next external gate is trusted real multi-piece four-landmark data with unit/frame/provenance; otherwise the next internal gate is durable general graph Run Record/export. Do not claim arbitrary DAG replay, production batch infrastructure, physical calibration, or metrology.

- Input-first Guided Flow update (2026-07-23): normal Shell startup and Recipe Center New now produce a source-less recipe/Viewer; the fixed Thickness sample is retained only for existing automated compatibility paths. A named zero-step/source-less recipe remains saveable, while tool addition requires the complete source-readiness contract. Before input, the full catalog and palette details are hidden and the bilingual Inspection Tools, Inspection Flow, and central Viewer all point to explicit `Open 3D Map`; after input, compatible tools appear and selected-step guidance moves to Step Parameters and explicit Preview. Current Debug evidence passes build `0/0`, recipe teaching `25/25`, Recipe Center/WPG `27/27`, docking `26/26`, actual EXE New through Don't Save with an empty persisted source and zero steps, actual EXE empty -> fixed C3D transition, and Korean/English `1920 x 1040` screenshot quality. Preserve `docs/OPENVISIONLAB_3D_INPUT_FIRST_GUIDED_FLOW_20260723.md` and `artifacts/current/20260723-input-first-guided-flow/`. The next internal UX gate is multi-sample repeat validation; Viewer UI-apply/first-render performance follows. An unaided owner first-recipe replay and physical/metrology trust remain unverified.

- C3D load performance update (2026-07-23): the fixed actual-EXE Thickness -> Warpage transition is instrumented by grid read/statistics, distribution, render-point, hash, topology, position, worker, and UI-apply/first-render stages. The largest baseline stage, `C3DHeightGridRenderProxy.Create`, now uses packed cell keys, single-pass bounds, and direction-owned edge occupancy while preserving exact triangle/wire/grid/surface-edge ordering. Across three identical Debug runs, median topology time fell from `2,821.555 ms` to `48.088 ms` (`-98.3%`) and median whole transition from `5,447 ms` to `2,891 ms` (`-46.9%`). Current evidence passes build `0/0`, display/topology `83`, loader `22`, recipe teaching `25/25`, docking `26/26`, three actual-EXE completions, cancellation retention, missing-source retention, post-load pointer regression, and Korean `1920 x 1040` screenshot quality. Preserve `docs/OPENVISIONLAB_3D_SYNTHETIC_THICKNESS_SAMPLE_MIGRATION_20260728.md` and `artifacts/current/20260723-c3d-load-performance/`. The next bounded performance gate is UI apply and first render, currently about `1.09 s`; the owner first-recipe replay and trusted real four-landmark acquisition remain separate external gates. Do not claim Release/large-data/GPU portability, calibration, or metrology from this fixed local Debug evidence.

- Viewer pointer-render update (2026-07-23): orbit, middle/right pan, and profile-endpoint drag no longer call synchronous OpenGL rendering per `MouseMove`; the existing SharpGL 30 FPS scheduler coalesces state into the next frame. The actual-EXE fixed C3D smoke passes `3/3` with handler averages `0.134-0.184 ms`, maxima `0.963-1.528 ms`, next-frame maxima `36.787-51.914 ms`, and zero immediate drag renders, versus the instrumented `22.884 ms` average / `108.186 ms` maximum / 6 immediate-render baseline. A post-load Warpage run passes at handler maximum `0.889 ms`, next-frame maximum `46.416 ms`, and display-list build `37.581 ms`; build `0/0`, display `83`, docking `26/26`, and recipe teaching `25/25` pass. Preserve `docs/OPENVISIONLAB_3D_VIEWER_RENDER_COALESCING_20260723.md` and `artifacts/current/20260723-viewer-render-coalescing/`. This is fixed local software evidence; display-list creation remains a synchronous first-frame operation with an observed `37.168-41.758 ms` range. The next product evidence priority is an unaided owner first-recipe replay; the real four-landmark trust gate remains externally blocked. Do not claim large-data LOD, GPU portability, physical calibration, or metrology.

- Asynchronous C3D load update (2026-07-23): the Viewer `Open 3D Map` path decodes, calculates full-source distribution, creates sampled render points, and prepares render topology off the UI thread. A localized determinate ribbon exposes progress and Cancel; successful preparation is committed atomically, while cancellation and failure retain the current Viewer and recipe source. Current Debug evidence passes build `0/0`, loader `22`, recipe teaching `25/25`, docking `26/26`, actual EXE different-source completion, cancellation, and missing-source retention. Preserve `docs/OPENVISIONLAB_3D_ASYNC_C3D_LOAD_20260723.md` and `artifacts/current/20260723-async-c3d-load/`. The fixed completion still takes `3,814 ms`; Dispatcher activity proves message-pump progress but not bounded input latency or smooth rendering. The later P0-C pointer-render checkpoint closes the fixed-scene input-latency gap but does not shorten decode/preparation time or prove large-data rendering. Do not claim physical calibration or metrology from this evidence.

- Main-window work-area update (2026-07-22): the custom-chrome Shell starts maximized and handles `WM_GETMINMAXINFO` using the current monitor's Windows work area so the taskbar remains visible and the bottom application UI is not clipped. The normal restore size is `1600 x 900`. Current live evidence on the `1920 x 1080` desktop passes initial maximize `(0,0)-(1920,1040)`, restore `(78,78)-(1678,978)`, and re-maximize `(0,0)-(1920,1040)`; build is `0/0` and current screenshot quality passes on attempt 1. Preserve `docs/OPENVISIONLAB_3D_MAIN_WINDOW_WORK_AREA_20260722.md` and `artifacts/current/20260722-shell-work-area/`. Do not regress custom title-bar minimize/maximize/restore/close behavior or size custom-chrome maximization to the full monitor bounds.

- Empty recipe lifecycle correction (2026-07-22): recipe persistence and inspection execution readiness are separate contracts. `ToolRecipeDocumentStore` uses `ValidateForStorage`, so a named draft may be saved and reopened with no selected source and zero inspection steps; strict `ToolRecipeValidator.Validate` still requires a source path and at least one step for Preview/Run readiness. A fresh Workbench and automatic startup-source synchronization must remain clean. Recipe Center New resolves current changes, obtains the new path, creates and immediately saves a named zero-step recipe, then activates Workbench. Its unsaved prompt must use explicit bilingual `Save / Don't Save / Cancel` actions; Don't Save continues New/Open. Saveable zero-step drafts are labeled as needing execution preparation, not structural correction, and successful Save/Open must refresh the localized state to Saved. Recipe dialogs are owned by the visible Recipe Center. Open must reuse an identical loaded Viewer C3D rather than decode it again. Current Debug evidence passes build `0/0`, Recipe Center/WPG `27/27`, recipe teaching `23/23`, docking `25/25`, actual EXE New through the real Don't Save button, and exact Open-handler reuse in `774 ms`; screenshots pass quality on attempt 1. Preserve `docs/OPENVISIONLAB_3D_EMPTY_RECIPE_LIFECYCLE_20260722.md` and `artifacts/current/20260722-recipe-lifecycle-recheck/`. Do not fabricate a placeholder step or weaken strict Preview/Run validation. Manual native-picker replay and different-source load performance remain unverified.

- First Recipe UX update (2026-07-22): the separate single-instance recipe window is `Recipe Center`, and the default authoring workspace separates `Tool Library`, `Recipe Flow`, `3D View`, and `Step Parameters` with a five-stage first-recipe journey. At zero steps, show one contextual next action and keep the empty bottom pipeline closed. New/Open are primary Recipe Center actions; current recipe/source/save state and path-qualified recent recipes are bilingual. Current Debug evidence passes build `0/0`, docking `25/25`, Recipe Center/WPG/localization `24/24`, recipe teaching `18/18`, and Korean/English `1920 x 1080` captures on attempt 1. Preserve `docs/OPENVISIONLAB_3D_FIRST_RECIPE_UX_AND_RECIPE_CENTER_20260722.md` and `artifacts/current/20260722-first-recipe-ux/` as the evidence sources. This closes the implementation defect exposed by the owner's failed first attempt, but an unaided owner replay remains external evidence; do not cite the historical `85/100` UI score as proof of first-task usability. Do not move recipe lifecycle into the Workbench, create a decorative free-form node editor, or weaken explicit Preview/Publish/Run.

- `Synthetic Affine Inspection Plate v1` passed locally on 2026-07-22 as the deterministic whole-chain software golden: `240 x 160` C3D -> Median Filter -> eight Edge/Line Fit paths -> four exact CornerAnchors -> four-pair Landmark Correspondence -> A1/A2/A3 -> ordered Thickness/Warpage. The focused verifier passes `16/16`; four anchor errors are `0`, A1 maximum matrix error is `1.7053025658242404E-13`, A3 preserves `38,348` populated and `52` missing cells with zero collisions, and independent measurement checks match within `6.7501559897209518E-14`. Preserve `docs/OPENVISIONLAB_3D_SYNTHETIC_AFFINE_INSPECTION_PLATE_V1_20260722.md` and `3D/SyntheticValidation/AffineInspectionPlateV1` as the evidence sources. This is synthetic display-frame evidence only; the next gate is a distinct real four-landmark acquisition with trusted frame/unit/provenance/reference-grid evidence. Do not claim physical calibration, sensor fidelity, Gauge R&R, or metrology trust from this sample.

- Generic recipe update (2026-07-22): `ToolRecipeDocument` schema `1.3` preserves `TransformedHeightField`-owned `GridRectangle` and `PointSet(2)` selections with exact owner entity ID, artifact/root-source SHA-256, grid, unit, and frame. Shell Viewer teaching may select populated A3 cells; save/reopen retains the binding; Thickness/Warpage consume either raw C3D or the exact Published A3, while Plane Flatness, Point Pair, Gap/Flush, Volume, and Cross-section Dimensions require the exact Published A3. Generic Ordered Recipe Executor v1 executes authored A3 then every supported measurement step from an explicit Published A2, preserves later evidence after a tolerance Fail, and matches direct-adapter hashes; focused verification passes `18/18`. Preserve legacy raw bindings and fail closed on wrong owner/hash/grid/frame/order/tool/output. Do not claim full A1-to-result graph replay, real alignment, calibration, physical volume/dimensions, or metrology. See `docs/OPENVISIONLAB_3D_CROSS_SECTION_TOOL_RECIPE_20260722.md` and `docs/OPENVISIONLAB_3D_ARTIFACT_OWNED_ROI_AND_ORDERED_RUNNER_20260722.md`.

- Durable multi-step Run Record update (2026-07-22): the bounded A3 -> seven-measurement executor emits schema `1.3` JSON/HTML/CSV with eight ordered `Steps`, exact typed input/output IDs, per-step status/message/time/metrics/overlays, and an aggregate result that preserves later evidence after a tolerance `Fail`. The current dockable Tool Workbench shows the same record in a bilingual read-only `Run Record` tab. Focused verification passes `21/21`; Korean and English `1920 x 1080` captures pass screenshot quality. Existing single-step execution remains schema `1.2` with `Step` and no fabricated `Steps`; Shell compatibility with older optional fields is preserved. See `docs/OPENVISIONLAB_3D_MULTI_STEP_RUN_RECORD_20260722.md`. This is not arbitrary graph replay, batch infrastructure, real alignment, or metrology evidence.

- Generic recipe architecture update (2026-07-22): `docs/OPENVISIONLAB_3D_GENERIC_TOOL_RECIPE_ARCHITECTURE_20260722.md` is the canonical recipe/UI ownership decision. The primary product is an `Inspection Recipe` Workbench backed by `ToolRecipeDocument`; Thickness, Warpage, Plane Flatness, Point Pair, Gap/Flush, Volume, and Cross-section Dimensions are ordinary Measure tool adapters, never workspace modes or separate recipe lifecycles. New files use `*.ov3d-recipe.json` while existing `*.ov3d-teach.json` ToolRecipeDocument files remain readable. Preserve explicit PropertyGrid editing, Preview, Publish, typed INPUT/OUTPUT IDs, and save/reopen. The closed A2 -> A3 -> seven-measurement executor passes `18/18`; arbitrary whole-graph replay remains unproven.

- Identity/commercial benchmark update (2026-07-22): `docs/OPENVISIONLAB_3D_IDENTITY_DIRECTION_AND_GOPXL_COMPLETENESS_20260722.md` records the current product identity, GoPxL/ZEISS/PolyWorks lessons, explicit scope boundary, and non-marketing completeness denominators. Keep UI `85/100`, narrow software MVP `65-70%`, GoPxL Tools/Chaining core about `60%`, full GoPxL platform about `35-40%`, and physical/metrology `Unverified` separate. Do not present one number as overall readiness.

- The local 2026-07-16 pointer smoke target-readiness check passes Viewer `5/5`, Shell `5/5`, fixed matrix `128/128`, and current `0.1.1-dev` BinaryHost manifest/outputs/Host API `13/13`, `12/12`, `3/3`. Preserve Viewer-native root-HWND target validation before every gesture plus the smoke-only temporary foreground input-queue attachment and `finally` detach; do not call it from normal Viewer/Shell interaction. Keep this logic in the View/rendering boundary and keep camera/selection state ViewModel-owned. See `docs/OPENVISIONLAB_3D_VIEWER_POINTER_TARGET_RELIABILITY_20260716.md`.

- `docs/OPENVISIONLAB_3D_PRODUCT_TARGET_AND_SELF_EVALUATION_20260711.md` is the current product-direction and commercial-comparison source of truth. Update it when a product gate passes or the target changes.
- `docs/OPENVISIONLAB_3D_VIEWER_RELIABILITY_PHASES_20260714.md` is the Viewer trust-roadmap and reliability-claim source of truth. Follow Phase 1 software/visual -> Phase 2 geometric/algorithm -> Phase 3 physical/metrology order and do not collapse those decisions into one marketing percentage.
- `docs/OPENVISIONLAB_3D_RELEASE_VERSION_POLICY.md` is the release/version source of truth. Product, Host API, manifest, Run Record, and recipe versions change independently according to that policy; do not create a tag or packaged release without its gates and explicit user approval.
- Target an explainable, local, sensor-neutral 3D inspection recipe workbench for height maps, point clouds, and meshes.
- The target workflow is measured/nominal data -> units/frame/reference/ROI -> ordered inspection steps -> explicit Preview -> metrics/tolerance/overlays -> explicit Publish -> recipe save -> headless Runner replay -> run record/report.
- Measure algorithm ownership update (2026-07-22): reusable least-squares height-field plane fit, Plane Flatness distance/RMS/extrema/projection arithmetic, height-axis-relative Point Pair arithmetic, signed Gap/Flush arithmetic, signed Volume integration, and source-neutral Cross-section width/height-range arithmetic now belong to committed Library-Noah `Lib.ThreeD 2.7.9` at `e36d9c07baab967fd4252e7052345563f29872a3`; Studio keeps C3D/A3 identity, ordered ROI/point bindings, PropertyGrid, explicit Preview/Publish, recipe, overlays, and Runner adaptation. The exact vendored package SHA-256 is `B21A6266AFD470B7EE8A4C857496E53561F4D399F2460FEE2939AAE85AD0FF92`. Preserve this dependency direction and package hash gate. Cross-section Dimensions is a generic `Published TransformedHeightField + one-row GridRectangle -> MeasurementResult` node using A3 U/H axes; it survives schema `1.3` save/reopen and ordered Runner replay. See `docs/OPENVISIONLAB_3D_CROSS_SECTION_TOOL_RECIPE_20260722.md`. This is deterministic software evidence, not calibrated physical dimension or metrology validation.
- `docs/OPENVISIONLAB_3D_CALIBRATION_AND_THEME_UI_CONCEPT_20260717.md` is the Calibration Center and application-theme direction. Phase A-C established the accepted View, ViewModel, and typed Thickness Repeatability Model; Phase D now passes locally with Model `34/34`, closed-schema/file-identity Study loader `13/13`, ViewModel workflow `48/48`, and accepted loaded/not-calculated plus calculated `1280 x 760` captures. Core owns immutable study/run/policy/result contracts; Data owns JSON/source length/SHA-256/distinct-acquisition verification; Tools owns validation separated from explicit `N-1` sample-standard-deviation, `6 x s`, range, and independent-limit calculation; the ViewModel owns loaded/calculated state and commands; View code-behind is limited to file-dialog and smoke bridges. Preserve the empty product default, keep Calculate disabled until an explicit valid Study is loaded, clear stale results on every reload, and reject duplicate paths or byte-identical sources as separate acquisitions. The next gate is a real Run Chart with shared Grid/Chart selection, followed by per-point 3D selection only after aligned repeated-point evidence exists; then add Height Calibration and Sensor Alignment as separate typed slices. Do not claim Gauge R&R, physical calibration, or metrology trust until separate evidence gates pass.
- Calibration update (2026-07-17): the preceding historical Phase D next-gate sentence is superseded. Phase E LiveCharts2 Run Chart and shared Grid/Chart selection pass locally; Phase F `AlignedPointRepeatability` Model/Tool passes `33/33`; Phase G `Data` Study/Mapping loader plus the headless `Runner` execution/report pass `20/20` synthetic cases. Study and Mapping identity must be computed from the same bytes parsed into contracts, including supported UTF-8 BOM JSON. Keep immutable contracts in `Core`, JSON/file/hash/mapping verification in `Data`, numerical acceptance in `Tools`, Golden evidence and headless reports in `Runner`, and future visible linked-selection state/commands in ViewModel. Do not add a View/code-behind shortcut or a synthetic 3D map. The next implementation gate requires distinct real aligned acquisitions with source unit/frame/alignment provenance and trusted source-to-correspondence export evidence; physical calibration, Gauge R&R, and metrology claims remain blocked.
- Calibration remote CI closure (2026-07-17): Studio commit `c45ce78` passed Windows Actions run `29569056102`; all 41 job steps succeeded, including vendored Library-Noah package integrity, Library-Noah bridge `7/7`, aligned Model `33/33`, aligned Study/Runner `20/20`, Thickness gates, Calibration Center ViewModel, and established Viewer/Runner gates. Artifact metadata is ID `8402387241`, `3,727,932` bytes, digest `sha256:24080e4ef536a56a5c56a5178822ecfb885c4ae71d96c145e339ded4e0045787`. The public archive download requires authentication and was not locally rehashed or inspected. Library-Noah commit `c2b5860` separately passed Build run `29569055985`. This is CI execution evidence, not physical calibration, metrology, or authenticated artifact-content evidence.
- Viewer Foundation v1 passed on 2026-07-11 for the current C3D/GLB/STL/LAS/LAZ fixed sample matrix. Preserve it as a regression baseline; this is not a production-readiness claim.
- Viewer Foundation v1 was revalidated from the current source on 2026-07-12: the fixed data/loading/interaction/Shell matrix recorded 129 passing checks and no failures, and the C3D detailed display, pick, two-point measurement, independent Python mapping, and Open3D interchange checks passed. Viewer-only expansion is no longer the active priority; physical calibration remains a separate blocked trust gate.
- C3D map display-frame fidelity passed on 2026-07-11 for the fixed Thickness sample: the local reference PNG confirms dimensions and unflipped row/column orientation, a 10/10 synthetic golden suite fixes the mapping contract including a finite single-cell case, and 66,212 sampled Viewer points roundtrip through neutral PLY with zero XYZ/RGB error. Physical scale remains unverified because the official format and calibration metadata are unavailable.
- CloudCompare 2.13.2 full-resolution Viewer-frame parity passed on 2026-07-13: all 1,653,562 ordered C3D vertices and RGB values survive an independent load/re-save within `5.00000001e-7` Viewer units, CloudCompare C2C reports mean `4.91657e-7` and standard deviation `1.49337e-7`, and the selected point-pair distance, width, height delta, and angle remain within the documented display-frame tolerances. This closes trust gate T3 for the fixed sample only; T4 physical calibration and T5 licensed metrology comparison remain blocked. Preserve `docs/OPENVISIONLAB_3D_SYNTHETIC_THICKNESS_SAMPLE_MIGRATION_20260728.md` as the evidence source.
- Windows CI revalidated the Viewer trust baseline at commit `cebdc8f` in Actions run `29288595132` on 2026-07-14. BinaryHost, Shell screenshot quality, Runner/golden/map checks, actual C3D roundtrip, the stride-aligned independent Python point-pair check, PLY signature, and artifact upload all passed. Artifact `8294167228` is `1,167,597` bytes with digest `sha256:485c6bbcfb0389ed2af2584eb9dfb359365fd95927bcfb3e3b2ccd4342d9b7bc`.
- Windows CI passed the NuGet package-health gate at commit `6779881` in Actions run `29297655730` on 2026-07-14. The independent self-test and live 8-project audit passed before Build, all existing BinaryHost/Shell/Runner/golden/map gates remained green, and artifact `8297372590` is `1,168,807` bytes with digest `sha256:66a3a2650a720aa8810ca4a433f73f08d97053122f77750f740455e6b9385fde`. A fresh authenticated download matched that digest and contained parseable vulnerable/deprecated JSON plus `projects=8`, `vulnerable=0`, and `deprecated=0` summary evidence.
- Inspection Recipe v1 baseline passed on 2026-07-11 for one C3D numeric-reference-ROI plane-flatness step with stable input/reference IDs, recipe save/reopen, Viewer/Runner metric and status parity, Publish evidence, and a real Shell step row. This is a one-step baseline, not a general recipe graph or metrology claim.
- Plane/flatness algorithm credibility baseline passed on 2026-07-11 with an analytic synthetic plane, exact signed-offset flatness/RMS answers, Pass/Fail thresholds, and controlled empty/insufficient/degenerate/non-finite/invalid-tolerance cases. This is not calibration or external metrology validation.
- The second typed slice, C3D point-pair distance/width/signed-elevation-angle, passed on 2026-07-11 with explicit source-cell references, separate metric tolerances, Preview/Publish, recipe roundtrip, Viewer/Runner parity, Shell evidence, and 9/9 analytic/error golden cases. It measures selected cells; it does not find edges or fitted features.
- The third typed slice, C3D Gap/Flush, passed on 2026-07-12 with two explicit recipe-owned regions, signed aligned-X gap, signed raw-height flush, separate tolerances, Preview/Publish, recipe roundtrip, Viewer/Runner parity, a real Shell step row, and 8/8 analytic/error golden cases. It does not perform automatic seam/edge detection or calibrated physical measurement.
- The fourth typed slice, C3D Volume, passed on 2026-07-12 with an explicit reference-plane ROI and measurement ROI, signed above/below/net values, Preview/Publish, recipe roundtrip, Viewer/Runner parity, a real Shell step row, and 9/9 analytic/error golden cases. Its `model^3` values belong to the uncalibrated display frame and are not physical volume.
- The fifth typed slice, C3D Cross-section Dimensions, passed on 2026-07-12 with an exact source row and inclusive column range, aligned-X width, raw-height range, separate tolerances, Preview/Publish, recipe roundtrip, Viewer/Runner parity, a real Shell step row, and 9/9 analytic/error golden cases. It does not find edges/features or provide calibrated physical dimensions.
- Durable Run Record v1.2 passed locally on 2026-07-14 for the fixed Cross-section run: JSON, HTML, and CSV now carry stable step `step.c3d-cross-section-dimensions`, source `source.c3d-thickness`, and reference `reference.c3d-row-range` alongside recipe/source hashes, status, metrics/overlays, Matched state, artifact paths, and execution identity. The current Shell reads schema `1.0`, `1.1`, and `1.2`. Earlier authenticated schema `1.1` records from Windows CI runs `29297655730` and `29297867087` have identical normalized business/evidence payload SHA-256 `59ab1baf854ef23da98bdf7e977a3fd69d9675e81f0efedb99dbc7f5be1cd2d8`; only Run ID, UTC time, elapsed time, and Git commit differ. This proves same-source repeatability, not multi-piece/batch infrastructure or a general multi-step executor. Legacy Height Deviation and LAZ recipe `1.0` documents still have no stable step ID and therefore emit `Step=null`.
- .NET 10 migration passed on 2026-07-12: Core/Data/Tools/Runner target `net10.0`; Viewer/Docking/Shell/app target `net10.0-windows`; restore/build, all six golden suites, SharpGL C3D/textured-GLB rendering, WPF-UI/AvalonDock Shell, LASzip decode, and the 128-check matrix pass. Preserve `docs/OPENVISIONLAB_3D_DOTNET10_MIGRATION_20260712.md` as the compatibility source of truth.
- Viewer binary-host boundary passed on 2026-07-12: the minimal external WPF Host has zero `ProjectReference`, compiles from the published DLL bundle, carries all 12 required host/runtime outputs, and its generated EXE directly renders and picks the C3D sample with current screenshot/contract evidence.
- Windows CI binary-host gate passed on 2026-07-12 in Actions run `29195744796`: the direct-EXE step and all Runner/golden/map steps succeeded, and `openvisionlab-3d-ci-artifacts` was uploaded with the binary-host report, contract, and screenshot.
- Windows CI Shell quality and release identity gates passed on 2026-07-12 in Actions run `29196380343`: BinaryHost, full Shell C3D screenshot quality, central/manifest/Run Record identity, all Runner/golden/map checks, and artifact upload succeeded.
- Release candidate `0.1.0-rc.1` is published as GitHub prerelease tag `v0.1.0-rc.1` at commit `ac57687`. Windows Actions run `29198517611`, the uploaded Viewer manifest/Run Record, and the public Viewer ZIP agree on product `0.1.0-rc.1`, Host API `1.0`, clean commit identity, and `Matched` Cross-section Viewer/Runner state. The Viewer ZIP SHA-256 is `b9a9b6d002f507da63da32934d93bf6e8deaff2d7c1b00ff70a6f36d6b784a83`.
- Post-RC development source identifies as product `0.1.1-dev` from 2026-07-14. This separates current builds, manifests, and Run Records from the immutable public `v0.1.0-rc.1` evidence; it is not a tag, packaged release, or stable-version promotion.
- The current `0.1.1-dev` working tree passed a local pre-push regression on 2026-07-14: zero-warning/error build, zero-finding eight-project NuGet audit, all six golden suites, five typed Run Record step identities, two legacy `Step=null` cases, .NET/Python C3D map checks, BinaryHost, and Shell schema `1.0`/`1.1`/`1.2` screenshot-quality checks passed.
- Windows CI validated the post-RC identity, Run Record schema `1.2`, and LF-stable recipe provenance at commit `e704f6f` in Actions run `29302323300`. Every job step passed, including BinaryHost, Shell/Viewer screenshot quality, typed-step JSON/HTML/CSV checks, all golden/map checks, and the recipe raw-byte hash gate. Authenticated artifact `8298975554` is `1,323,767` bytes with digest `sha256:70935ecfb48978cc20abeda446b62fd0ba8d67fb29809a932b122b7a77fa5d00`; its clean Windows Run Record uses recipe SHA-256 `f9355976ebd179f20719e20d24736a6f61d8b6711e98bad4b543ced1ae279666`, matching the local LF checkout and full selected business/evidence payload.
- Public-bundle host acceptance passed on 2026-07-13: a fresh GitHub asset download matched the published ZIP SHA-256, and the BinaryHost verifier enforced all 13 manifest file paths, sizes, and SHA-256 values before building. The zero-`ProjectReference` Host then built with zero warnings/errors and directly rendered/picked the C3D sample with 12/12 outputs and an accepted screenshot-quality report; a 4/4 rejection matrix blocked outside-bundle, missing, wrong-size, and same-size hash-mismatched entries before Host build.
- Viewer Host API v1.0 consumer acceptance passed locally on 2026-07-13 against both the public RC bundle and a current-source bundle: BinaryHost records a real `HostState` snapshot, nonzero `HostStateChanged` events, `ResetView`/`FitAll`/`FitSelection` invocation, and a successful `SaveRecipe` JSON. Its `Application.Run` result now propagates to the process; a controlled missing-recipe smoke records and returns exit code `1`.
- Windows CI passed the Host API consumer gate at commit `95dd8da` in Actions run `29216983045`. The BinaryHost step and all Shell/Runner/golden/map steps succeeded; artifact `8266920376` is `1,167,342` bytes with digest `sha256:254145a80071df39f88d4c199372d1c30c64057f6b931062de4c8dfbdc476c16`.
- Registration-engine prototype review on 2026-07-13 accepted Open3D `DemoICPPointClouds` only as an external alignment golden and the Open3D `0.19.0` C++ API only as a separate-process candidate. Same-tag source commit `1e7b17438687a0b0c1e5a7187321ac7044afe275` now passes both the recovered build and an independent clean single-shot non-GUI Release build/install. The clean install has the same 873 paths and 88,977,375 bytes; 871 file hashes match the recovered install, while the two rebuilt DLLs preserve sizes, export contracts, dependency lists, and registration behavior despite different PE timestamps and hashes. The three-file probe runtime remains 58,520,064 bytes. Clean-build output matches the official binary exactly in 33/33 robustness runs and 3/3 current `0 -> 1` DemoICP runs and is deterministic, but only 5 predeclared robustness outcomes match. Never treat registration RMSE alone as success: require correspondences and fitness first. Both runtimes reject `1 -> 2` because `cloud_bin_2.pcd` contains 771 non-finite normals, so its older pre-hardening metrics are historical only. A schema-valid 52-component CycloneDX candidate records 27 direct and 25 observed support/transitive components. Assimp's fixed clean Release closure passes with 232/232 source/object mappings, 15/15 bundled-zlib mappings, and a deterministic 48-importer/22-exporter registry, but exact vendored snapshot/modification provenance remains open. Fixed oneMKL provenance passes exact three-wheel identity and RECORD integrity, 179/179 archive/install payload hashes, 4/4 Release link inputs, and byte-identical two-run canonical reassembly. VTK `9.1.0` source/recipe/payload/transitive closure passes with 1,156/1,156 archive/install files, 14 explicit Release link inputs, 20 reachable targets, 16 static libraries, seven exact child components, and 8/8 source-matched licenses; its documented VS2019 workflow conflicts with `_MSC_VER=1900` in all 30 packaged C++ libraries, so exact binary/toolchain reproducibility remains open. Distribution also remains blocked by BoringSSL binary/toolchain reproducibility, final notices, Microsoft VC/OpenMP prerequisite and clean-host evidence, product integration impact, and owner/legal approval in `docs/OPENVISIONLAB_3D_OPEN3D_DISTRIBUTION_AUDIT_20260713.md`; Viewer/Runner parity remains open. `PclNET 0.8.3` remains rejected. No product dependency, PCD loader, or fixed sample was added.
- Controlled VTK Release-rebuild evidence passed locally on 2026-07-16 without adding a product dependency. The legacy VTK archive's generated `vtkConfigureDeprecated.h` records Visual Studio 2019 MSVC `14.29.30133`; the current VS2022 `yvals.h` deliberately emits the same `_MSC_VER=1900` mismatch marker, so the earlier VS2019-versus-marker conflict claim is withdrawn. The no-patch controlled `v143` Release build preserves all legacy Release paths, 22/22 VTK target contracts, 16/16 directive sets, and a direct link/run smoke, while intentionally omitting Debug and retaining different library hashes. A same-source `USE_SYSTEM_VTK=ON` Open3D `0.19.0` Release build now completes against that VTK candidate: its 873-path install, 29 dependency entries, 16,000 ordinal/name exports, three DemoICP `0 -> 1` results, controlled `1 -> 2` rejection, and 33 robustness results match the independent clean Open3D build within the documented elapsed-time exclusion. Historical byte identity, an exact legacy-toolchain rerun, Debug reconstruction, distribution approval, and Viewer/Runner parity remain open. Preserve `docs/OPENVISIONLAB_3D_VTK_CONTROLLED_REBUILD_20260716.md` and `docs/OPENVISIONLAB_3D_OPEN3D_VTK_CANDIDATE_RUNTIME_20260716.md` as the evidence sources.
- Windows Sandbox clean-host prerequisite evidence passed on 2026-07-16 for the fixed `USE_SYSTEM_VTK=ON` Open3D candidate and fixed DemoICP `0 -> 1` input pair. A Windows 10 Enterprise x64 guest independently verified all nine staged payload hashes and no adjacent VC/OpenMP runtime DLLs; pre-install preflight returned exit `1` with `system=0/4` and no probe report, the reviewed Microsoft-signed `vc_redist.x64.exe` `14.51.36247.0` installer returned `0` without restart, post-install preflight returned `0` with `system=4/4`, and the registration JSON matched the fixed baseline after removing only `elapsedMilliseconds`. This supersedes earlier references that clean-host installation evidence remains open. It closes only the technical clean-host prerequisite for this fixed candidate; REDIST/notice/legal review, owner approval, redistribution, product integration, real result mapping, and Viewer/Runner parity remain blocked. Preserve `docs/OPENVISIONLAB_3D_OPEN3D_CLEAN_HOST_EXECUTION_PROTOCOL_20260716.md` as the evidence source.
- BoringSSL controlled-rebuild preflight on 2026-07-16 found a usable VS2022 developer environment with CMake and MSVC `14.44.35207`, but no Perl, Go, or NASM on its command path. The official Open3D `build_boringssl.ps1` explicitly needs all three; do not install them or replace the official recipe without explicit owner approval. `docs/OPENVISIONLAB_3D_BORINGSSL_CONTROLLED_REBUILD_PLAN_20260716.md` fixes the source/script/archive identity, approved-run checklist, two-run archive/topology/directive/link comparison, and claim boundary. This is a precise preflight block, not binary reproducibility or distribution evidence.
- Assimp source-snapshot identity passed locally on 2026-07-16 for the fixed clean Open3D build: the CMake-selected official `v5.4.2.zip` SHA-256 `03e38d123f6bf19a48658d197fd09c9a69db88c076b56a476ab2da9f5eb87dcc` and all `2,940` archive/build-source files match by ordinal path, length, and content SHA-256; CMake's generated update and patch commands are empty. This closes only official archive-to-build source identity. Other vendored-component upstream revisions/modification provenance, notices, and distribution approval remain open. Preserve `docs/OPENVISIONLAB_3D_ASSIMP_SOURCE_SNAPSHOT_IDENTITY_20260716.md` as the evidence source.
- Poly2Tri original source-content and current path-history identity passed locally on 2026-07-16: the official Google Code source archive SHA-256 `02092826bf5c539ed5a904386a2439eb608cc4d1d008adc7034ae3a2230a05bb` was checked out at Mercurial `5de9623d6a500d8b0ad3126a48957c5152c15ad2` with artifact-local Mercurial `7.2.3` only, not a global installation. Its `hg archive` and `greenm01/poly2tri@99927efa011013154460ca4cb06bcd64d4768edb` `core.autocrlf=false` Git archive match exactly in all `35/35` ordinal files and raw bytes; the equal canonical manifest SHA-256 is `c8a0845fb300289b219e3bf06d07180c4d33ca18609741b6513f72aad29622e7`. Official Assimp `v5.4.2` path history then passes with 28 complete official entries, 28 validated detail responses, 15/15 initial/current blob trees, direct `14` paths / `1,407` additions / `1,328` deletions, and `14/14` path coverage by 27 post-initial commits. This proves source content and path-level history, not Git-mirror signature/ownership or single-line-to-single-commit blame. Notices and distribution remain blocked. Preserve `docs/OPENVISIONLAB_3D_ASSIMP_POLY2TRI_LINEAGE_20260716.md`, `docs/OPENVISIONLAB_3D_ASSIMP_POLY2TRI_DELTA_ATTRIBUTION_20260716.md`, `scripts/verify-assimp-poly2tri-origin.ps1`, and `scripts/verify-assimp-poly2tri-delta-attribution.ps1` as the evidence sources.
- Clipper `6.4.2` fixed official-release, Assimp-import, and current-tag provenance passed locally on 2026-07-16. The official archive SHA-256 `a14320d82194807c4480ce59c98aa71cd4175a5156645c4e2b3edd330b930627`, Assimp import `aa1996e1437777af62aac549d55591f1849f90de`, latest Clipper path commit `bb9101ae9eb2938cadfeadd4690bbdf910ca57f4`, and current `v5.4.2` build input form a checked chain. The bounded archive-to-import delta is `7/6`, import-to-current is `4/4`, and the build source matches official current blob `c0a8565bb98568dcca4a5350ca52fa08152bea51`. This is source-content and bounded-change evidence only, not upstream signature, individual-line attribution, final notices, binary reproducibility, or distribution approval. Preserve `docs/OPENVISIONLAB_3D_ASSIMP_CLIPPER_PROVENANCE_20260716.md` and `scripts/verify-assimp-clipper-provenance.ps1` as the evidence sources.
- `stb_image.h` `v2.29` fixed upstream-commit, Assimp-update, current-tag, and build-source identity passed locally on 2026-07-16. Official upstream commit `0bc88af4de5fb022db643c2d8e549a0927749354`, Assimp update `3ff7851ff9ad3004bb934fedaf657ffad0572573`, the `v5.4.2` tag source, and the clean-build input all match source SHA-256 `c54b15a689e6a1f32c75e2ec23afa442e3e0e37e894b73c1974d08679b20dd5c` and blob `a632d543510ebf4410f124369b07a303e1d096d6`. CMake, implementation, and wrapper evidence confirm compiler use. Captured upstream tags/releases are empty, and upstream history is paginated, so this is exact commit identity only, not a release/tag or complete-history claim. Preserve `docs/OPENVISIONLAB_3D_ASSIMP_STB_PROVENANCE_20260716.md` and `scripts/verify-assimp-stb-provenance.ps1` as the evidence sources.
- Assimp `kuba--/zip` compiler-input provenance passed locally on 2026-07-16. The CMake `0.3.0` metadata is not treated as source identity: the checked source basis is public `kuba--/zip v0.3.1` commit `550905d883b29f0b23e433fdb97f6299b628d4a9`, Assimp import `83d7216726726a07e9e40f86cc2322b22fec11fa`, and `v5.4.2` commit `ddb74c2bbdee1565dda667e85f0c82a0588c8053`. `miniz.h`, `zip.c`, and `zip.h` all match the actual clean-build input at their fixed current blobs; CRLF-normalized upstream-to-import/import-to-current deltas and the one `miniz.h` plus two `zip.c` post-import commits pass. This is bounded source/delta evidence, not upstream-only byte identity, release-state proof, an independent miniz audit, notices, binary reproducibility, or distribution approval. Preserve `docs/OPENVISIONLAB_3D_ASSIMP_KUBAZIP_PROVENANCE_20260716.md` and `scripts/verify-assimp-kubazip-provenance.ps1` as the evidence sources.
- Assimp `pugixml` compiler-input provenance passed locally on 2026-07-16. The checked source basis is public `zeux/pugixml v1.13` commit `a0e064336317c9347a91224112af9933598714e9`, Assimp import `62cefd5b275628ff97a77d0cd9220e1c35794a3f`, and `v5.4.2` commit `ddb74c2bbdee1565dda667e85f0c82a0588c8053`. The three effective header-only inputs (`pugiconfig.hpp`, `pugixml.hpp`, and `pugixml.cpp`) all match fixed current clean-build blobs. The import retains one explicit header-only configuration change, then the only fixed-range post-import commit updates copyright text (`2/2` per file). CMake `VERSION 1.9` is stale standalone metadata, not source identity. This is bounded source/delta and input-chain evidence, not upstream signature/history, notices, binary reproducibility, distribution, product integration, or Viewer/Runner parity. Preserve `docs/OPENVISIONLAB_3D_ASSIMP_PUGIXML_PROVENANCE_20260716.md` and `scripts/verify-assimp-pugixml-provenance.ps1` as the evidence sources.
- Assimp `UTF8-CPP` compiler-input provenance passed locally on 2026-07-16. Official `nemtrif/utfcpp v3.2.3` commit `79835a5fa57271f07a90ed36123e30ae9741178e`, Assimp update `ce59d49dd9ce93ccf8585f78c70e58cb0e5d4961`, current `v5.4.2`, and the clean build all share the exact four compiler-read header blobs. The fixed post-update path history is empty, while CMake, the `utf8.h` include chain, and the closure's four-file SHA-256 are checked. This is fixed source identity for four headers only, not optional UTF8-CPP headers, upstream signature/history, notices, binary reproducibility, distribution, product integration, or Viewer/Runner parity. Preserve `docs/OPENVISIONLAB_3D_ASSIMP_UTF8CPP_PROVENANCE_20260716.md` and `scripts/verify-assimp-utf8cpp-provenance.ps1` as the evidence sources.
- Assimp MiniZip compiler-input provenance passed locally on 2026-07-16. Public `madler/zlib v1.3.1` commit `51b7f2abdade71cd9bb0e7a373ef2610ec6f9daf`, Assimp update `64d88276ef7117c09165e468dbb9acd999e324ac`, current `v5.4.2`, and the clean build share exact blobs for `ioapi.c`, `ioapi.h`, `unzip.c`, and `unzip.h`; fixed post-update history is empty. Although CMake lists `crypt.h`, `unzip.c` defines `NOUNCRYPT` before its conditional include, so it is not compiler-read in this fixed build. This is bounded four-file zlib-contrib evidence, not complete MiniZip/Info-ZIP/`crypt.h` provenance, notices, binary reproducibility, distribution, product integration, or Viewer/Runner parity. Preserve `docs/OPENVISIONLAB_3D_ASSIMP_MINIZIP_PROVENANCE_20260716.md` and `scripts/verify-assimp-minizip-provenance.ps1` as the evidence sources.
- Assimp `miniz` compiler-input provenance passed locally on 2026-07-16 for the fixed `kuba--/zip v0.3.1` to Assimp/build chain. Kuba tag `550905d883b29f0b23e433fdb97f6299b628d4a9`, Assimp PR `#5499` baseline, its fixed `2/0` and `4/1` header changes, merge `83d7216726726a07e9e40f86cc2322b22fec11fa`, post-merge `1/1` change `0d546b3d2edb5ae737c11971b26233f5a5316a43`, current `v5.4.2`, and the clean-build input are verified by blobs and CMake/closure evidence. PR-head-to-merge content matches but is not Git ancestry. This does not independently prove original `richgel999/miniz` source identity/history, notices, binary reproducibility, distribution, product integration, or Viewer/Runner parity. Preserve `docs/OPENVISIONLAB_3D_ASSIMP_MINIZ_PROVENANCE_20260716.md` and `scripts/verify-assimp-miniz-provenance.ps1` as the evidence sources.
- Assimp OpenDDL Parser compiler-input provenance passed locally on 2026-07-16. Public `kimkulling/openddl-parser v0.5.1` commit `ffad343385f550b933c7e498e9bd0a861605102c` and Assimp baseline `bc7ef58b4947a01f4f7163b47b96ca273473d7eb` match all `13/13` compiler-read blobs. Only `OpenDDLCommon.h` (`12/15`, `7cbf4c4136bf9884fad408e6e388b10ba3ace635`) and `OpenDDLParser.cpp` (`3/1`, `081cae6a950204ced52f5ca09b78fe7446286967`) change through `v5.4.2` and the clean build. The shared static `0.4.0` source string is metadata, not the fixed source-tag identity. This is bounded source/delta/build evidence, not upstream signature/history, notices, binary reproducibility, distribution, product integration, or Viewer/Runner parity. Preserve `docs/OPENVISIONLAB_3D_ASSIMP_OPENDDL_PROVENANCE_20260716.md` and `scripts/verify-assimp-openddl-provenance.ps1` as the evidence sources.
- Assimp zlib core compiler-input provenance passed locally on 2026-07-16. Public `madler/zlib v1.2.13` commit `04f42ceca40f73e2978b50e93806c2a18c1281fc`, Assimp update `8741da2036cba41cf55fd5805e7a9730a70d2a3a`, current `v5.4.2`, and the clean build share all `25/25` fixed source blobs; the source-path history is empty after the import for every checked input. CMake builds 15 C sources plus 9 private headers and `zlib.h`; `zconf.h` is a generated header and remains explicitly outside the upstream source-identity subset. A wrong expected zlib revision fails closed with exit code `1`. This is bounded source/delta/build evidence, not upstream signature/history, notices, binary reproducibility, distribution, product integration, or Viewer/Runner parity. Preserve `docs/OPENVISIONLAB_3D_ASSIMP_ZLIB_PROVENANCE_20260716.md` and `scripts/verify-assimp-zlib-provenance.ps1` as the evidence sources.
- Assimp RapidJSON compiler-input provenance passed locally on 2026-07-16. Public `Tencent/rapidjson v1.1.0` tag `f54b0e47a08782a6131cc3d60f94d038fa6e0a51` is an ancestor, not the source-identity claim; the exact public post-tag baseline is `676d99db96e2108724e62342a47e28c8e991ed3b`. Assimp update `4a3e0e46ac45867c8c8fac9cbcdee3bc30e99f92`, current `v5.4.2`, and the clean build share all `29/29` fixed header blobs, with empty post-import source-path history. The update changes 16 headers from its first parent. CMake include/definition markers and the header-only closure are checked. A wrong expected baseline revision fails closed with exit code `1`. This is bounded source/delta/build evidence, not an official release-tag claim for the post-tag baseline, upstream signature/history, notices, binary reproducibility, distribution, product integration, or Viewer/Runner parity. Preserve `docs/OPENVISIONLAB_3D_ASSIMP_RAPIDJSON_PROVENANCE_20260716.md` and `scripts/verify-assimp-rapidjson-provenance.ps1` as the evidence sources.
- Assimp Open3DGC compiler-input provenance passed locally on 2026-07-16. Public `KhronosGroup/glTF` `mesh-compression-open3dgc` snapshot `7b61d5e065f98058fa12fadfec821546f486d960` and Assimp import `054820e6ffc03f1a914f2bc688d7f030cf01894b` match all `29/29` compiler-read blobs. The fixed `v5.4.2` tag has exactly `16` bounded Open3DGC path changes, and every fixed-tag blob matches the clean-build input; CMake and closure contracts pass. The effective source notice evidence is `MIT AND BSD-2-Clause`: the core notice is MIT while the two arithmetic-codec files carry BSD-2-Clause text. A wrong expected Khronos revision fails closed with exit code `1`. This is a public carrier-snapshot/import/delta/build claim, not historical AMD remote availability, upstream signature/release proof, final notices, binary reproducibility, distribution, product integration, or Viewer/Runner parity. Preserve `docs/OPENVISIONLAB_3D_ASSIMP_OPEN3DGC_PROVENANCE_20260716.md` and `scripts/verify-assimp-open3dgc-provenance.ps1` as the evidence sources.
- Assimp closure notice-manifest candidate passed locally on 2026-07-16. `scripts/verify-assimp-closure-notice-manifest.ps1` cross-checks the fixed `v5.4.2` closure, fresh `2,940/2,940` archive-to-build source snapshot, CycloneDX candidate, and current source hashes. It records Assimp core plus `12` compiler-read components as `13` entries, `125` compiler-read paths, and `15` separate source notice records; Open3DGC remains `MIT AND BSD-2-Clause` rather than an MIT-only record. The deterministic contract SHA-256 is `ce51a50d6852cb3229c6406d85e7bb181a296006d0a72bae934959883babc43c`, and a wrong archive identity fails closed with exit code `1`. This is a candidate manifest only, not final `THIRD-PARTY-NOTICES.txt`, legal approval, redistribution approval, product integration, or Viewer/Runner parity. Preserve `docs/OPENVISIONLAB_3D_ASSIMP_CLOSURE_NOTICE_MANIFEST_20260716.md` and `scripts/verify-assimp-closure-notice-manifest.ps1` as the evidence sources.
- Assimp `miniz` original-origin boundary audit passed locally on 2026-07-16 without resolving original provenance. A full, non-shallow official `richgel999/miniz` clone enumerated `1,368` current reachable objects; neither the fixed Kuba baseline blob nor the actual clean-build blob was directly reachable, and `v114` is not an ancestor of `3.0.2`. The audit therefore fixes `OriginIdentityStatus=Unresolved`: non-observation in current reachable refs is not evidence that no historical relation exists. The clean build contains both Unlicense and MIT source-text markers, which supports retaining the candidate source-text expression `MIT OR Unlicense` but is not final notice or legal evidence. Preserve `docs/OPENVISIONLAB_3D_ASSIMP_MINIZ_ORIGIN_BOUNDARY_20260716.md` and `scripts/verify-assimp-miniz-origin-boundary.ps1` as the evidence sources.
- Windows CI revalidated the hardened BinaryHost gate at commit `c50d196` in Actions run `29215566528`: every workflow step passed, including BinaryHost, Shell screenshot quality, Runner/golden/map checks, actual C3D roundtrip, independent Python mapping, and artifact upload. Artifact `8266449434` has digest `sha256:230b5607524e668ed47f59d85e08514bace873e631f676bb44a32282d2eb4c65`.
- NIST AMMT `Overhang Part X4` was accepted on 2026-07-14 as the first local external measured/nominal trust candidate, not as a committed fixed sample or completed product slice. The `9 x 5 x 5 mm` nominal STL loads with 2,904 triangles and exact `(0,0,0)..(9,5,5)` bounds. The corresponding Part 1 XCT surface is distinct, has 8,560,096 triangles, and remains above the 1,000,000-triangle Viewer limit. Binary STL preflight now rejects it before reading the complete 428,004,884-byte file, reducing observed smoke peak working set from 574,529,536 to 152,526,848 bytes while preserving the 129/129 fixed matrix. Preserve `docs/OPENVISIONLAB_3D_MEASURED_NOMINAL_SAMPLE_REVIEW_20260714.md` as the intake decision and do not silently decimate inspection geometry.
- The independent NIST deviation-baseline prerequisite passed on 2026-07-14 in CloudCompare 2.13.2. In the NIST-provided 3-2-1 part frame, all 4,223,524 CloudCompare-unique measured vertices retain exact XYZ while unsigned C2M reports mean/std `0.192040211` / `0.208181684 mm` and signed C2M reports mean/std `0.0124131265` / `0.282957542 mm`. Independent binary-PLY verification matches CloudCompare logs within `1e-6 mm`, and `abs(signed)` matches unsigned within `2.38418579e-7 mm`. This is a vertex-weighted external baseline, not OpenVisionLab algorithm parity or metrology certification. Preserve `docs/OPENVISIONLAB_3D_NIST_CLOUDCOMPARE_DEVIATION_BASELINE_20260714.md` as the evidence source.
- The OpenVisionLab NIST stream/distance foundation passed locally on 2026-07-14. `BinaryStlInspectionReader` streamed all 8,560,096 measured triangles in source order, reproduced the original SHA-256 and bounds, and used a 25 ms sampled peak process working set of `21.367 MiB` for the 428,004,884-byte file. The render-independent stream, ordered-PLY, distance, robust-sign, and controlled-error golden passes `17/17`. The solution build and 128-check matrix plus separate build gate pass.
- OpenVisionLab full-resolution NIST/CloudCompare algorithm parity passed locally on 2026-07-14 for the fixed identity-frame query set. All 4,223,524 ordered measured vertices retain exact raw-float XYZ. Unsigned C2M differs by at most `7.301871997600351e-7 mm`; mean/std deltas are `4.7576901307522235e-9` / `3.043473556507692e-9 mm`, and all five threshold counts match. Direct face-interior signing resolves 4,145,609 points; the independently implemented robust candidate selection resolves the remaining 77,915 edge/vertex cases with zero sign mismatches, 100% coverage, and maximum signed difference `7.116761328584964e-7 mm`. This closes the fixed-sample non-visual algorithm gate only. Preserve `docs/OPENVISIONLAB_3D_NIST_CLOUDCOMPARE_DEVIATION_BASELINE_20260714.md` as the algorithm evidence source.
- The fixed NIST measured/nominal end-to-end slice passed locally on 2026-07-14. Core owns typed source/input/result/fingerprint and published-result contracts, Data owns the ordered binary-PLY reader, Tools owns the render-independent full-query executor and typed recipe, `NominalActualComparisonViewModel` owns commands/progress/presentation, and `MainWindowViewModel` owns active input, published state, and display-sample budgets. The executor/recipe/result verification passes `26/26`, including display-budget measurement invariance plus controlled missing-source, empty-unit/frame, corrupt/truncated-query, hash/length-mismatch, same-path, direction, and tolerance rejection; the ViewModel verification passes `60` checks. Standalone Viewer and full Shell calculate all `4,223,524` query points, explicitly publish a separate result entity/layer, save and reopen the typed recipe, and report `1,233,381` points outside `[-0.3, 0.3] mm`. A current-source Fast/Balanced/Detailed Viewer matrix renders `24,992` / `59,487` / `145,639` signed display samples while all three modes retain one normalized measurement/published-evidence SHA-256 `2FD93EF942D12C621A76964EF681816EE831CD8DEA214EF0A201F602BA30D1C9`; all three screenshot-quality checks pass. Headless Runner replay matches Viewer status and statistics, and schema `1.2` JSON plus HTML/CSV preserve actual, nominal, query, step, metric, overlay, and execution identity. Current-source evidence under `artifacts/nominal_actual_publish_20260714` includes accepted Viewer and Shell captures, zero-difference stable contract reopen parity, and `ViewerRunnerComparison|Matched`; `artifacts/nominal_actual_render_density_20260714` adds before/after density evidence, the `128/128` matrix, all typed goldens, and BinaryHost preservation. This closes one fixed identity-frame product slice only. Semantic mismatch between non-empty declared unit/frame values and source truth remains uncheckable until independent source metadata exists. Other samples, non-identity alignment, uncertainty, metrology certification, and redistribution approval remain open. Preserve `docs/OPENVISIONLAB_3D_NIST_NOMINAL_ACTUAL_END_TO_END_20260714.md` as the product evidence source.
- The selected nominal/actual point provenance gate passed locally on 2026-07-14. A real Balanced Viewer pointer-ray smoke selected ordered query point `2,724,128`, retained actual/query source IDs, signed/unsigned deviation `-0.39734458923339844` / `0.39734458923339844 mm`, nearest nominal triangle `725`, closest nominal point, direct/robust sign path, and `Below lower tolerance` status. Viewer HUD, standalone Inspector, Shell Tool/Inspector, and linked state show the selection; new Preview/input/tolerance changes clear it. Current evidence under `artifacts/nominal_actual_selected_point_20260714` passes executor/result `27/27`, ViewModel `65/65`, fixed matrix `128/128`, screenshot quality, and BinaryHost manifest `13/13` plus outputs `12/12`. The original actual STL has no proven one-to-one vertex index, so do not invent one; use the ordered query index and stable source identities.
- The current-versus-next-Preview density-state gate passed locally on 2026-07-15. A completed Balanced nominal/actual result remains explicitly identified as `59,487` display samples, stride `71`, and budget `60,000` after Detailed is selected; `Next Preview: Detailed` and `explicitPreviewRequired=True` appear without rerunning or changing the published result. A subsequent explicit Detailed Preview becomes current with `145,639` samples and stride `29`. Fast/Balanced/Detailed and pending-transition measurement/published evidence share SHA-256 `2FD93EF942D12C621A76964EF681816EE831CD8DEA214EF0A201F602BA30D1C9`. Evidence under `artifacts/nominal_actual_density_state_20260715` passes ViewModel `71` checks, nominal/actual golden `27/27`, fixed matrix `128/128`, BinaryHost manifest `13/13` plus outputs `12/12`, and the extended density regression. Preserve explicit Preview; render-density changes must not auto-run nominal/actual comparison.
- The deterministic WPF pointer-input gate passed locally on 2026-07-15 in both the standalone Viewer and the docked Shell. Real Windows input produced WPF `MouseDown=3`, `MouseMove=12`, `MouseUp=3`, and `MouseWheel=1`, selected the generated cube with a non-empty coordinate and synchronized linked selection summary, changed yaw/pitch through right-button orbit, changed the camera target through middle-button pan, and reduced camera distance through wheel zoom. A second run in each host produced byte-identical reports with Viewer SHA-256 `4D6C926DA834ED6AE017D98FEB84BCB043C1FA77AD3364A36D1B1EB842C7CF4E` and Shell SHA-256 `2F2CBB688D8C3293C3176100CC6AE2D985BFF1A8F19DE840E77D98D72CCEA2A0`. Evidence under `artifacts/pointer_input_regression_20260715` also passes current-source Viewer/Shell screenshot quality, the fixed `128/128` matrix, and BinaryHost manifest `13/13`, outputs `12/12`, and Host API commands `3/3`. Keep the native input helper inside the Viewer WPF/rendering boundary; camera and selection state remain ViewModel-owned.
- The hosted dual-capture gate passed locally on 2026-07-15. Shell is the sole smoke application-lifecycle owner when it hosts the Viewer: it applies smoke actions once, captures the embedded Viewer first, captures the full Shell second, and shuts down only after both quality gates complete. The pre-fix full NIST nominal/actual command returned `1` with zero artifacts; the fixed command returns `0`, accepts `411 x 380` Viewer and `1280 x 800` Shell frames on attempt 1, and records all `4,223,524` points, selected query point `2,724,128`, Published state, and explicit result entity in the same Viewer contract. Evidence under `artifacts/dual_capture_orchestration_20260715` passes build `0/0`, fixed matrix `128/128`, unchanged pointer-report hashes, and BinaryHost manifest `13/13`, outputs `12/12`, Host API commands `3/3`. Preserve `--smoke-screenshot` as embedded Viewer evidence and `--shell-smoke-screenshot` as full-workbench evidence; when both are requested, never give the hosted Viewer an independent shutdown handler.
- Windows CI closed the Phase 1 hosted dual-capture and WPF pointer-input portability gates on 2026-07-15. Commit `7bebc62` passed observation run `29378562022`; authenticated artifact `8328811080` is `1,592,037` bytes with digest `sha256:90f4c9aae4ab5dee126ebfc59ea81d85006ef249bf9481d8107aa6677ec229f0`, and both Viewer/Shell reports record exit `0`, `pass=True`, routed events `3/12/3/1`, and successful pick/orbit/pan/zoom. Commit `8a841a6` then promoted pointer input to a mandatory gate and passed run `29378878976`; artifact `8328930089` is `1,593,122` bytes with digest `sha256:3179673b1d98406daaebc29bb1c4902e977bc9c49bf23a5d233e6dba5a5d8247`. The same run preserves accepted hosted Viewer/Shell captures, BinaryHost manifest `13/13`, outputs `12/12`, Host API commands `3/3`, and NuGet projects/vulnerable/deprecated `8/0/0`.
- Phase 2 intake on 2026-07-15 accepted NIST Overhang X4 Part 2 as a second physical-instance candidate and Stanford Drill as a published non-identity transform candidate. The ignored Part 2 archive is `197,482,785` bytes with SHA-256 `BDA2BC07B0F2E2920E3F5AE378849319D75B22F36AE078FCAF6ED5CB12AC96F9`; its extracted STL is `402,032,984` bytes with SHA-256 `0F74D3A949488C161DAC71681420A171B1EDA3E478ED24D492D33AA6C9F7F032`. The current Runner streams all `8,040,658` source triangles and records bounds `(-0.081858255,-0.114424519,-0.150348008)..(8.97986984,5.03950977,4.82653236) mm`. Stanford's 12 scans and `.conf` are research-only and not product assets. Preserve `docs/OPENVISIONLAB_3D_MEASURED_NOMINAL_SAMPLE_REVIEW_20260714.md` as the intake source.
- The NIST Part 2 external and non-visual algorithm baseline passed locally on 2026-07-15. The exact Part 1 CloudCompare `2.13.2` executable extracted `3,965,430` ordered validation vertices. Independent signed/unsigned PLY verification preserves exact XYZ; OpenVisionLab unsigned and signed maximum differences are `7.1853447186631669e-7 mm`, direct sign coverage is `98.179112984%`, all `72,206` edge/vertex cases are recovered, material sign mismatches are zero, one float-epsilon near-zero sign is explicitly equivalent, and final coverage is 100%. The current synthetic gate passes `18/18` and still rejects material opposite signs. Preserve `docs/OPENVISIONLAB_3D_NIST_PART2_CLOUDCOMPARE_DEVIATION_BASELINE_20260715.md` as the algorithm evidence source.
- The fixed NIST Part 2 visible product slice passed locally on 2026-07-15. The existing generic View, `NominalActualComparisonViewModel`, Core/Tools contracts, recipe, and Runner required no duplicated Part 2 workflow; only the smoke input bridge needed an explicit `nist-overhang-x4-part2` identity profile because it previously assigned Part 1 IDs to Part 2 files. Viewer and Shell complete explicit Preview, selected-point evidence, Publish, recipe save/reopen, and all `3,965,430` query points with `507,115` below, `2,794,040` within, and `664,275` above `[-0.3, 0.3] mm`. Runner reports `ViewerRunnerComparison|Matched`; schema `1.2` evidence preserves Part 2 actual/nominal/query IDs and hashes. Build `0/0`, nominal/actual `27/27`, ViewModel `71`, fixed matrix `128/128`, BinaryHost `13/13`, `12/12`, Host API `3/3`, and current Viewer/Shell pointer input pass. Preserve `docs/OPENVISIONLAB_3D_NIST_PART2_VISIBLE_WORKFLOW_20260715.md` as the product evidence source.
- The Stanford Drill non-identity transform gate passed locally on 2026-07-15. The official VripPack guide plus version `0.31` parser/render/bbox source fix the convention as `transpose(ShoemakeQuaternionMatrix(q))*point+translation` for `x,y,z,w`; the camera row is not applied. Independent Python and Runner implementations parse all `12` binary-big-endian range-grid scans and `50,643` points, match `36` ordered checkpoints plus every per-scan and aggregate statistic with maximum observed difference `0`, and reject a `0.001` tampered point with exit `5`. CloudCompare `2.13.2` independently reads each original scan and applies the generated 4x4 matrix; all ordered points pass with maximum difference `3.0913966692081019e-8` under a `1e-7` float32-output tolerance. Units remain source-unspecified, and Stanford data stays ignored, research-only, and excluded from product/CI assets. Preserve `docs/OPENVISIONLAB_3D_STANFORD_TRANSFORM_BASELINE_20260715.md` as the evidence source.
- The source-aware Viewer display-settings View, ViewModel, Model, C3D rendering, and local performance checkpoints passed on 2026-07-15. `ViewerDisplaySettingsViewModel` owns source capabilities, choices, fallback, and render-only notification; Viewer-local typed source/style/color identifiers and immutable `ViewerDisplaySettingsSnapshot` define the effective-state contract. SharpGL consumes that snapshot through a cached C3D display proxy that triangulates only complete stride-adjacent source cells and never bridges holes. C3D switches among Points, Wireframe, Surface, and Surface + Edges; LAS/LAZ stays point-only. Two final 31-frame Fast/Balanced/Detailed runs pass all `24/24` style-density cases on the recorded GTX 1060 3GB machine; the observed minima are Fast `46.786`, Balanced `32.574`, and Detailed `18.352 FPS`. Static geometry uses an OpenGL display list, while result-owned Plane Flatness Deviation coloring bypasses it. Picking and two-point measurement contracts remain invariant. Current evidence under `artifacts/c3d_geometry_performance_20260715` also passes display verification `64`, nominal/actual ViewModel `71`, build `0/0`, fixed matrix `128/128`, BinaryHost manifest `13/13`, outputs `12/12`, Host API commands `3/3`, hosted dual-capture, and Viewer/Shell pointer input. Preserve `docs/OPENVISIONLAB_3D_C3D_GEOMETRY_STYLE_PERFORMANCE_20260715.md` as the local performance source. This is not a cross-machine performance guarantee, a new inspection surface, or a physical-accuracy claim; Windows CI has not yet revalidated the new performance gate.
- The C3D Grayscale/Thermal Color Map checkpoint passed locally and in Windows CI on 2026-07-15. The existing Viewer/Shell View binds to one Viewer-owned display surface; the ViewModel exposes `Solid`, `Grayscale`, `Height`, and `Thermal`, plus result-owned `Deviation`; Viewer-local `ViewerColorMapPalette` owns deterministic normalized-scalar RGB mapping; and SharpGL consumes it through the existing typed display-list key. Display verification passes `71` checks. Balanced 33,761-point 90-frame local smokes record Grayscale `75.303 FPS / 5.272 ms` and Thermal `37.049 FPS / 10.438 ms`; build `0/0`, fixed matrix `128/128`, BinaryHost manifest `13/13`, outputs `12/12`, Host API commands `3/3`, and established Viewer/Shell pointer hashes pass. Commit `3136ebe` added a mandatory Windows gate; Actions run `29409271743` passed both LUT captures, screenshot quality, typed contracts, 71-check display verification, distinct image hashes, and all prior CI steps. Artifact `8340434196` is `2,062,684` bytes with digest `sha256:a9d8c4454fac7d4f66e280f42868e7ab474c9b70a5f14bd0c35939d6378de0d4`. Preserve `docs/OPENVISIONLAB_3D_C3D_COLOR_MAPS_20260715.md`. This is a display-only fixed-sample claim; physical color calibration, manual ranges, legends, inversion, imported-mesh palettes, and cross-machine performance remain open.
- The GLB/STL Geometry Style checkpoint passed locally and in Windows CI on 2026-07-15. The existing standalone and Shell Views still bind to the Viewer-owned display child; imported triangle meshes now enable typed Points, Wireframe, Surface, and Surface + Edges choices, and SharpGL consumes the immutable display snapshot. The focused BoxTextured, BoxVertexColors, and Tetrahedron matrix passes `15/15`: source texture, vertex colors, and Solid fallback remain effective, all four style hashes are distinct per sample, and pick plus two-point measurement contracts are invariant. Display verification passes `79`, build `0/0`, fixed matrix `128/128`, BinaryHost manifest `13/13`, outputs `12/12`, Host API commands `3/3`, hosted Viewer/Shell captures, and established pointer hashes pass. Commit `c1ea4cb` passed Windows Actions run `29413823276`, including the mandatory imported-mesh verifier and every existing gate. Authenticated artifact `8342304881` is `3,721,333` bytes with digest `sha256:baa41a597d4cd55894aff2d9cc8bcbe811c853e52402f51d18c084407f95866e`; the downloaded archive matched that digest and contained 12 screenshots, 12 contracts, 12 quality reports, and the `15/15` summary. Preserve `docs/OPENVISIONLAB_3D_IMPORTED_MESH_GEOMETRY_STYLES_20260715.md`. This remains a fixed-sample display claim; large-mesh style performance and arbitrary material behavior are open.
- The Phase 2 difficult-geometry controlled-outcome checkpoint passed locally and in Windows CI on 2026-07-15. Existing Runner verifiers cover duplicate-vertex deterministic ties, non-finite stored STL normals, open-surface local-normal sign semantics, direct and robust edge/vertex outcomes, separate sparse/dense full-query inputs, and empty mesh/query rejection. Mesh deviation passes `23/23`, nominal/actual execution passes `29/29`, build passes `0/0`, and the fixed Viewer/Shell matrix remains `128/128`. Commit `0f89450` passed every step in Windows Actions run `29418511898`, including the mandatory fail-closed Phase 2 report gate. Authenticated artifact `8344275224` is `3,725,380` bytes with digest `sha256:36ce274d5f1ffd09d2c4b27d1baec130f2ce2a81852291bed3cd7afb636e5021`; a fresh authenticated download matched that digest and contained 95 files with all 11 selected Phase 2 report assertions. Preserve `docs/OPENVISIONLAB_3D_PHASE2_DIFFICULT_GEOMETRY_GOLDENS_20260715.md`. This remains fixed synthetic evidence and does not by itself close registration acceptance.
- The runtime-neutral registration acceptance policy passed locally and in Windows CI on 2026-07-16. `RegistrationAcceptanceRule` requires explicit units and scenario-specific minimum correspondence, minimum fitness, maximum RMSE, maximum translation/rotation, and rigid-transform tolerance. It evaluates correspondence count -> fitness -> RMSE -> rigid transform -> translation -> rotation; later metrics remain `NotRun` after an earlier rejection. The Runner golden passes `20/20`, including `0 correspondence / RMSE 0` rejection, non-homogeneous/scaled/reflected transforms, non-finite evidence, unit mismatch, and invalid policy guards. Commit `13f143a` passed every step in Actions run `29454088343`; job `87483200712` completed the mandatory registration gate as step 15. Authenticated artifact `8358732707` matched `3,726,847` bytes and digest `sha256:fced1dde391124d89b761336c907957d597b73dfbecbdc9d2dff62f4bf18b9f7`, with 97 archive entries and the expected registration report/summary. No Open3D/PCD dependency or visible workflow was added. Preserve `docs/OPENVISIONLAB_3D_REGISTRATION_ENGINE_PROTOTYPE_20260713.md`.
- Viewer reliability phase decision on 2026-07-16: Phase 1 is passed for the fixed supported scope locally and in the current Windows CI workflow, including Foundation, selected-point provenance, density-state clarity, hosted dual-capture, and mandatory standalone/Shell pointer input. This remains a fixed-workflow engineering claim, not general geometric correctness or metrology. Phase 2 is not passed: both fixed NIST physical instances have external/non-visual and visible Viewer/Runner evidence, the separate Stanford known-transform gate passes, and the difficult-geometry controlled-outcome matrix plus runtime-neutral registration policy pass locally and in Windows CI. The approved registration runtime mapping and Viewer/Runner execution path remain open. Phase 3 is blocked because calibration and uncertainty prerequisites are unavailable.
- Standalone Viewer and Shell smoke screenshots share the same WPF pixel-quality assessment. Both preserve rejected attempts, retry at most three times, and fail the smoke when no acceptable frame is captured; an existing file path alone is not screenshot evidence.
- Emulate commercial products where they are strongest: ZEISS-style traceable parametric steps, PolyWorks-style explicit references/alignment and sequences, Geomagic-style repeatable scan comparison, and Gocator/Cognex-style ROI-based measurement tools with thresholds and visual evidence.
- Do not attempt full CAD/GD&T, broad device integration, enterprise SPC/data management, production HMI, or AI recipe tuning in the current phase.
- Do not claim calibrated, certified, or metrology-grade accuracy without explicit units, calibration provenance, uncertainty assumptions, golden datasets, and independent validation.

## Default Product Priority

1. Preserve the locally and Windows-CI-passed Phase 1 Viewer baseline. Keep hosted dual-capture and deterministic Viewer/Shell pointer input mandatory in CI; do not weaken the fixed Viewer/Shell matrix, BinaryHost, selected-point provenance, current/next density state, or explicit Preview/Publish contracts.
2. Preserve the locally and Windows-CI-passed C3D Geometry Style and Color Map checkpoints, including the static-cache key, dynamic Deviation bypass, display-only invariants, mandatory LUT CI smoke, and measurement evidence.
3. Preserve the locally and Windows-CI-passed GLB/STL Geometry Style checkpoint. Keep the `15/15` verifier mandatory and preserve texture/vertex-color/Solid, pick, measurement, screenshot-distinctness, and display-only contracts; do not infer topology for LAS/LAZ.
4. Preserve the locally and Windows-CI-passed mandatory `23/23` mesh-deviation, `29/29` nominal/actual difficult-geometry, and `20/20` runtime-neutral registration gates without weakening their required report assertions. Preserve both NIST visible slices and the Stanford transform evidence. Registration product integration remains blocked until an approved runtime/distribution maps real results into the policy and proves Viewer/Runner parity.
5. Start Phase 3 C3D physical mapping only when X/Z pitch, height scale/offset, units, axis directions, origins, and calibration identity are available. Until then preserve and label the current profile as unitless/raw-height and recommend `execution deferred`.
6. Extend durable reporting only after multiple real runs expose a concrete need; do not jump to batch trends, PDF, database, or enterprise reporting.
7. Preserve the published `v0.1.0-rc.1` Shell-quality, Viewer-quality, binary-host, release-identity, archive-hash, and Viewer/Runner `Matched` gates. Do not promote it to stable `0.1.0` or replace release assets without explicit owner approval and new evidence.

## Next Priority Model Guidance

- Every next-priority item reported to the user must include `Recommended model` and `Reasoning effort` so the user can choose a lower-cost run when the work does not require deep reasoning.
- Recommend the least expensive currently available Codex-capable model that can complete the item safely. If a model named below is unavailable in the current surface, name the closest available equivalent explicitly.
- Use `codex-mini-latest` with `low` for documentation, repository status, narrow research, and simple command verification when available.
- Use `GPT-5.3-Codex` with `low` for small localized code edits with clear acceptance criteria.
- Use `GPT-5.3-Codex` with `medium` for normal feature slices, multi-file MVVM work, Viewer/Runner parity, and test-driven bug fixes.
- Use `GPT-5.3-Codex` with `high` for architecture changes, ambiguous cross-module defects, numerical reliability, physical calibration, metrology comparison, security, or difficult performance work.
- Do not recommend `xhigh` by default. Use it only after `high` is insufficient or an explicitly high-risk task justifies the added reasoning cost.
- If a priority is blocked by missing calibration data, sample data, credentials, hardware, or another prerequisite, state the prerequisite first and recommend `execution deferred` instead of spending model tokens prematurely.
- Required compact format: `1. Implement C3DMappingProfile | Recommended model: GPT-5.3-Codex | Reasoning effort: medium`.
## Stable Contracts

- Viewer Foundation v1 has passed. New rule/algorithm work may proceed only as an end-to-end inspection slice while all viewer-gate smokes remain green.
- Before adding new visible Viewer/Shell features, place them in the workbench layout contract in `docs/OPENVISIONLAB_3D_WORKBENCH_LAYOUT_DESIGN_20260707.md`; layout skeleton work comes before filling new feature behavior.
- Viewer completion means reliable display, camera control, object/layer visibility, picking, selection, measurement/result overlay rendering, color modes, and screenshot smoke evidence.
- The first viewer implementation uses SharpGL because the project owner is already comfortable reading and debugging SharpGL-based code.
- The 3D viewer must remain a separate project/library. The eventual main workspace should host it as a document/tool view instead of merging viewer internals into the main shell.
- Treat `OpenVisionLab.ThreeD.Viewer` as a separately releasable WPF DLL boundary. Build distributable output with `scripts/build-viewer-dll.ps1`; ship the complete validated dependency bundle and manifest rather than copying only `OpenVisionLab.ThreeD.Viewer.dll`.
- Host applications may own windows, docking, WPF-UI themes, and navigation, but must not copy SharpGL rendering or Viewer ViewModel logic out of the Viewer project.
- Keep external hosts on the versioned `IOpenVisionThreeDViewerHost` state/event/command contract. The concrete Viewer ViewModel remains an internal Shell binding compatibility surface and must not become the default external integration API.
- Preserve `samples/OpenVisionLab.ThreeD.Viewer.BinaryHost` as the binary-boundary proof: it must contain no `ProjectReference`, build from the published Viewer bundle, and pass `scripts/verify-viewer-dll-host.ps1` by launching its generated EXE directly. Use `-ViewerBundlePath` to verify an extracted release bundle without rebuilding it from source; the verifier must reject missing, outside-bundle, wrong-size, or SHA-256-mismatched manifest files before Host build. It must also prove Host API version/state/events/commands/recipe save and propagate a failed Viewer smoke to a nonzero process exit code.
- For the main workspace, follow the `C:\Git\OpenVisionLab_Dev` docking boundary: docking ownership belongs in a dedicated controls library like `Library\OpenVisionLab.Docking.Controls`; do not add AvalonDock or raw docking package usage directly to the app project.
- For app-level WPF UI styling, follow the Dev repository's `WPF-UI` boundary: the Shell app owns `WPF-UI` package/theme resources, while Viewer and Docking.Controls stay free of direct `WPF-UI` dependencies unless a reusable control explicitly needs that dependency.
- The repository targets .NET 10. Preserve project platform boundaries, `global.json`, CI `10.0.x`, and the SharpGL/WPF-UI/AvalonDock/LASzip runtime evidence before changing SDK feature bands or package versions.
- Windows CI must run `scripts/verify-nuget-package-health.py` after restore and fail when any direct or transitive NuGet package is reported as vulnerable or deprecated. Also fail closed when the JSON version, required query parameters, or project set is incomplete or inconsistent. Preserve the raw JSON responses and summary report in the CI artifact.
- MVVM is the target application structure. For visible workflow work, develop in View -> ViewModel -> Model order: place or adjust the binding surface first, put durable state/commands/comparison logic in the ViewModel next, and change model/contract/parser code only when the existing data shape cannot support the workflow.
- Keep view code-behind as a thin UI/OpenGL event bridge, and move durable state, commands, result data, and workflow logic into ViewModel, Controller, Presenter, Runtime, or Service classes as soon as they stop being trivial.
- Keep source geometry and result geometry separate. A validation result must not silently mutate the imported source model.
- Keep preview and publish separate. Preview is review state; publish creates or updates an explicit result layer/entity.
- Every validation tool must expose metrics and visual evidence, not only OK/NG text.
- A new inspection tool is incomplete until it has parameters, controlled validation, metrics, overlay evidence, tolerance status, recipe persistence, Runner replay, and current Viewer/Shell evidence.
- Use stable step IDs and explicit entity/reference inputs. Runner replay must not depend on display names or implicit active selection.
- Tracked `recipes/*.recipe.json` files use LF through `.gitattributes`. Run Record recipe SHA-256 is the hash of the exact executed bytes; preserve the LF contract so clean Windows and local checkouts produce the same provenance hash.
- Keep measurement sampling independent from render density.
- C3D source-grid and display-frame fidelity are separate from physical fidelity. Preserve `column -> X`, `raw height -> Y`, `row -> Z`, source hashes, sample stride, and explicit `unitless`/`raw-height` labels until a calibration-backed mapping profile exists.
- Neutral PLY map exports contain exact rendered sample vertices. Their optional faces exist only for external-viewer compatibility and must never be used as inspection geometry.
- Point-pair dimension recipes own exact C3D row/column references. Viewer and Runner must resolve those cells from the source file; render-density samples are display data, not measurement inputs.
- Shared evidence lines for source entities, entity layers, tool results, metrics, overlays, and published result entities belong in `OpenVisionLab.ThreeD.Core.InspectionContractText`; do not duplicate those line formats in Viewer, Shell, Runner, or Tools.
- Units must be explicit. Store model units, display units, tolerances, and transforms with the data they affect.
- Selection, picking, measurement, and camera state are product contracts. Do not break orbit, pan, zoom, fit-to-view, object visibility, or result overlay toggles while adding tools.
- Viewer-internal HUD/toolbar owns essential inspection facts that must remain visible when the Viewer is hosted elsewhere: coordinate frame, axis meaning, selected mode, pick state, distance/height measurement summary, and performance state. Shell panes may mirror or organize this information, but must not be the only place it exists.
- Prefer simple, inspectable rule-based tools before AI or automatic tuning.
- Registration acceptance must require an explicit minimum correspondence count and fitness before RMSE is evaluated. Record correspondence count, fitness, RMSE, transform plausibility, and controlled failure state separately; zero correspondences with RMSE `0` is a failure.
- Do not commit to a CAD kernel, point-cloud stack, or rendering engine without a small local prototype and verification evidence.

## Completion Means Evidence

Do not mark work complete by explanation alone. Completion requires the smallest meaningful evidence for the touched area.

Treat Viewer and Shell screenshot smokes as passing only when their built-in pixel-quality check accepts the frame. Preserve rejected attempts and do not substitute an older screenshot when all retries fail.

For documentation-only work:

```powershell
git diff --check
rg -n "OpenVisionLab 3D|3D Viewer|rule-based|C:\\Git\\OpenVisionLab_Dev" .
```

For C3D map fidelity work:

```powershell
dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --verify-c3d-map-fidelity --report artifacts\map_fidelity\c3d_map_fidelity_golden.txt
dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --c3d-map-probe 3D\Samples\ThicknessCouponV1\thickness-coupon-v1.C3D --ply artifacts\map_fidelity\openvision_c3d_detailed.ply --report artifacts\map_fidelity\c3d_map_fidelity_actual.txt --max-sampled-points 140000
python scripts\verify-c3d-map-ply.py --source 3D\Samples\ThicknessCouponV1\thickness-coupon-v1.C3D --ply artifacts\map_fidelity\openvision_c3d_detailed.ply --report artifacts\map_fidelity\c3d_map_fidelity_python.txt --max-sampled-points 140000 --first-cell 85,1190 --second-cell 10,995
python scripts\ply-coordinate-signature.py --ply artifacts\map_fidelity\openvision_c3d_detailed.ply --report artifacts\map_fidelity\openvision_c3d_detailed_signature.txt
```

For an explicit full-resolution C3D audit, add `--point-only --max-sampled-points 2147483647` to the Runner probe and pass the same budget to the Python verifier. Do not upload the resulting large PLY as a routine CI artifact.

For external-viewer parity work, compare a PLY exported by OpenVisionLab with the same PLY re-saved by CloudCompare, ZEISS INSPECT, PolyWorks, Open3D, MeshLab, or another trusted tool using:

```powershell
python scripts\ply-coordinate-signature.py --reference artifacts\map_fidelity\openvision_c3d_detailed.ply --candidate artifacts\map_fidelity\external_resaved_c3d_detailed.ply --report artifacts\map_fidelity\external_resaved_c3d_detailed_compare.txt --ignore-faces --tolerance 0.00001
```

For the NIST streamed-mesh and distance foundation, run:

```powershell
dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --verify-mesh-deviation --report artifacts\mesh_deviation\mesh_deviation_golden_20260714.txt
dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --stl-stream-probe "artifacts\research_samples\nist_overhang_x4\OverhangPartX4 Part1 Surface_cleaned.stl" --unit mm --report artifacts\mesh_deviation\nist_overhang_x4_stl_stream_20260714.txt
```

For the runtime-neutral registration acceptance contract, run:

```powershell
dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --verify-registration-acceptance --report artifacts\registration_acceptance_20260715\registration_acceptance_golden.txt
```

For the fixed NIST measured/nominal end-to-end slice, run the synthetic contracts first, then use the ignored local NIST files for current-source Publish, recipe, Runner, and UI evidence:

```powershell
dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --verify-nominal-actual-comparison --report artifacts\nominal_actual_publish_20260714\nominal_actual_golden_final.txt
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --verify-nominal-actual-viewmodel artifacts\nominal_actual_publish_20260714\nominal_actual_viewmodel_verification_final.txt --smoke-screenshot artifacts\nominal_actual_publish_20260714\viewmodel_verification_final.png
$nist = 'artifacts\research_samples\nist_overhang_x4'
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\nominal_actual_publish_20260714\viewer_after_publish.png --smoke-contracts artifacts\nominal_actual_publish_20260714\viewer_after_publish_contract.txt --smoke-nominal-actual "$nist\OverhangPartX4 Part1 Surface_cleaned.stl" "$nist\cloudcompare_deviation_20260714\measured_vertices_full.ply" "$nist\OverhangPart_9x5x5mm.STL" --smoke-publish-result --smoke-save-recipe artifacts\nominal_actual_publish_20260714\nist_nominal_actual.recipe.json
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\nominal_actual_publish_20260714\viewer_after_reopen.png --smoke-contracts artifacts\nominal_actual_publish_20260714\viewer_after_reopen_contract.txt --smoke-recipe artifacts\nominal_actual_publish_20260714\nist_nominal_actual.recipe.json
dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --recipe artifacts\nominal_actual_publish_20260714\nist_nominal_actual.recipe.json --report artifacts\nominal_actual_publish_20260714\nist_runner_report.txt --expect-status Fail --compare-contract artifacts\nominal_actual_publish_20260714\viewer_after_publish_contract.txt --viewer-screenshot artifacts\nominal_actual_publish_20260714\viewer_after_publish.png --run-record artifacts\nominal_actual_publish_20260714\nist_run_record.json --html-report artifacts\nominal_actual_publish_20260714\nist_run_report.html --csv-report artifacts\nominal_actual_publish_20260714\nist_run_report.csv
```

For SharpGL viewer work, run:

For broad data-loading changes, also run `scripts\run-data-loading-matrix-smoke.ps1`; it treats expected missing/corrupt GLB/STL/LAZ loader failures as passing checks when the process exit code is `1`.

```powershell
dotnet build OpenVisionLab.ThreeDStudio.slnx -c Debug
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_pick_after_cube.png --smoke-pick cube
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\glb_import_after.png --smoke-glb 3D\PublicSamples\glTF\Box.glb --smoke-contracts artifacts\glb_import_after.txt
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\glb_vertex_color_after.png --smoke-glb 3D\PublicSamples\glTF\BoxVertexColors.glb --smoke-contracts artifacts\glb_vertex_color_after.txt
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\glb_textured_after.png --smoke-glb 3D\PublicSamples\glTF\BoxTextured.glb --smoke-contracts artifacts\glb_textured_after.txt
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\stl_tetrahedron_after.png --smoke-stl 3D\PublicSamples\STL\Tetrahedron.stl --smoke-contracts artifacts\stl_tetrahedron_after.txt
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\stl_tetrahedron_pick_after.png --smoke-stl 3D\PublicSamples\STL\Tetrahedron.stl --smoke-pick mesh --smoke-contracts artifacts\stl_tetrahedron_pick_after.txt
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\stl_tetrahedron_measure_after.png --smoke-stl 3D\PublicSamples\STL\Tetrahedron.stl --smoke-measure mesh-two-point --smoke-contracts artifacts\stl_tetrahedron_measure_after.txt
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\laz_metadata_after.png --smoke-laz 3D\PublicSamples\PointCloud\xyzrgb_manuscript.laz --smoke-contracts artifacts\laz_metadata_after.txt
dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --laz-probe 3D\PublicSamples\PointCloud\xyzrgb_manuscript.laz --report artifacts\laz_points_probe_after.txt --max-sampled-points 50000
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\laz_points_after.png --smoke-laz-points 3D\PublicSamples\PointCloud\xyzrgb_manuscript.laz --smoke-contracts artifacts\laz_points_after.txt
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\laz_pick_after.png --smoke-laz-points 3D\PublicSamples\PointCloud\xyzrgb_manuscript.laz --smoke-pick laz --smoke-contracts artifacts\laz_pick_after.txt
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\laz_two_point_after.png --smoke-laz-points 3D\PublicSamples\PointCloud\xyzrgb_manuscript.laz --smoke-measure two-point --smoke-contracts artifacts\laz_two_point_after.txt
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\laz_acceptance_inspector_viewer_after.png --smoke-laz-points 3D\PublicSamples\PointCloud\xyzrgb_manuscript.laz --smoke-measure two-point --smoke-publish-result --smoke-contracts artifacts\laz_acceptance_inspector_viewer_after.txt
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\laz_acceptance_edit_save_viewer_after.png --smoke-laz-points 3D\PublicSamples\PointCloud\xyzrgb_manuscript.laz --smoke-measure two-point --smoke-edit-parameters laz-acceptance --smoke-save-recipe artifacts\saved_laz_two_point_acceptance.recipe.json --smoke-contracts artifacts\laz_acceptance_edit_save_viewer_after.txt
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\laz_acceptance_recipe_reopen_viewer_after.png --smoke-recipe artifacts\saved_laz_two_point_acceptance.recipe.json --smoke-contracts artifacts\laz_acceptance_recipe_reopen_viewer_after.txt
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_c3d_after.png --smoke-c3d thickness
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_c3d_pick_after.png --smoke-c3d thickness --smoke-pick c3d
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_contracts_after.png --smoke-c3d thickness --smoke-contracts artifacts\viewer_contracts_after.txt
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_tool_result_after.png --smoke-overlay result --smoke-contracts artifacts\viewer_tool_result_after.txt
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_publish_after.png --smoke-overlay result --smoke-publish-result --smoke-contracts artifacts\viewer_publish_after.txt
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_height_rule_after.png --smoke-rule height-deviation --smoke-contracts artifacts\viewer_height_rule_after.txt
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_recipe_height_rule_after.png --smoke-recipe recipes\c3d-height-deviation.recipe.json --smoke-contracts artifacts\viewer_recipe_height_rule_after.txt
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_recipe_ui_after.png --smoke-recipe recipes\c3d-height-deviation.recipe.json --smoke-contracts artifacts\viewer_recipe_ui_after.txt
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_deviation_legend_after.png --smoke-recipe recipes\c3d-height-deviation.recipe.json --smoke-contracts artifacts\viewer_deviation_legend_after.txt
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_render_controls_after.png --smoke-recipe recipes\c3d-height-deviation.recipe.json --smoke-point-size 4 --smoke-density Detailed --smoke-contracts artifacts\viewer_render_controls_after.txt
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_recipe_save_after.png --smoke-recipe recipes\c3d-height-deviation.recipe.json --smoke-tolerance 1500 --smoke-save-recipe artifacts\saved_c3d_height_deviation.recipe.json --smoke-contracts artifacts\viewer_recipe_save_after.txt
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_section_profile_after.png --smoke-recipe recipes\c3d-height-deviation.recipe.json --smoke-selection section --smoke-contracts artifacts\viewer_section_profile_after.txt
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_height_map_after.png --smoke-recipe recipes\c3d-height-deviation.recipe.json --smoke-contracts artifacts\viewer_height_map_after.txt
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_two_point_after.png --smoke-c3d thickness --smoke-measure two-point --smoke-contracts artifacts\viewer_two_point_after.txt
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_flatness_after.png --smoke-recipe recipes\c3d-plane-flatness.recipe.json --smoke-publish-result --smoke-save-recipe artifacts\saved_c3d_plane_flatness.recipe.json --smoke-contracts artifacts\viewer_flatness_after.txt
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_flatness_reopen_after.png --smoke-recipe artifacts\saved_c3d_plane_flatness.recipe.json --smoke-contracts artifacts\viewer_flatness_reopen_after.txt
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_dimensions_after.png --smoke-c3d thickness --smoke-measure dimensions --smoke-publish-result --smoke-save-recipe artifacts\saved_c3d_point_pair_dimensions.recipe.json --smoke-contracts artifacts\viewer_dimensions_after.txt
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_dimensions_reopen_after.png --smoke-recipe artifacts\saved_c3d_point_pair_dimensions.recipe.json --smoke-contracts artifacts\viewer_dimensions_reopen_after.txt
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_roi_step_after.png --smoke-c3d thickness --smoke-measure roi-step --smoke-contracts artifacts\viewer_roi_step_after.txt
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_roi_interactive_after.png --smoke-c3d thickness --smoke-alignment offset --smoke-measure roi-interactive --smoke-contracts artifacts\viewer_roi_interactive_after.txt
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_roi_recipe_save_after.png --smoke-c3d thickness --smoke-alignment offset --smoke-measure roi-interactive --smoke-save-recipe artifacts\saved_roi_alignment.recipe.json --smoke-contracts artifacts\viewer_roi_recipe_save_after.txt
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_roi_recipe_roundtrip_after.png --smoke-recipe artifacts\saved_roi_alignment.recipe.json --smoke-contracts artifacts\viewer_roi_recipe_roundtrip_after.txt
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_alignment_after.png --smoke-c3d thickness --smoke-alignment offset --smoke-contracts artifacts\viewer_alignment_after.txt
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_recipe_parameter_edit_after.png --smoke-recipe artifacts\saved_roi_alignment.recipe.json --smoke-edit-parameters roi-align --smoke-save-recipe artifacts\saved_roi_alignment_edited.recipe.json --smoke-contracts artifacts\viewer_recipe_parameter_edit_after.txt
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_interactive_alignment_after.png --smoke-recipe artifacts\saved_roi_alignment.recipe.json --smoke-align-from-roi --smoke-save-recipe artifacts\saved_roi_alignment_auto.recipe.json --smoke-contracts artifacts\viewer_interactive_alignment_after.txt
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_roi_validation_valid_after.png --smoke-recipe artifacts\saved_roi_alignment.recipe.json --smoke-align-from-roi --smoke-save-recipe artifacts\saved_roi_validation_valid.recipe.json --smoke-contracts artifacts\viewer_roi_validation_valid_after.txt
$invalidPath = 'artifacts\saved_roi_validation_invalid.recipe.json'; if (Test-Path $invalidPath) { Remove-Item -LiteralPath $invalidPath -Force }; dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_roi_validation_invalid_after.png --smoke-recipe artifacts\saved_roi_alignment.recipe.json --smoke-invalid-roi overlap --smoke-save-recipe $invalidPath --smoke-contracts artifacts\viewer_roi_validation_invalid_after.txt; if ($LASTEXITCODE -ne 1) { exit 1 }; if (Test-Path $invalidPath) { exit 1 }
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_height_rule_publish_after.png --smoke-rule height-deviation --smoke-publish-result --smoke-contracts artifacts\viewer_height_rule_publish_after.txt
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_selection_after_point.png --smoke-selection point
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_selection_after_box.png --smoke-selection box
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_selection_after_section.png --smoke-selection section
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_result_overlay_after.png --smoke-overlay result
```

For shell/docking work, also run:

`--smoke-screenshot` captures the embedded Viewer control. Use `--shell-smoke-screenshot` when the evidence must include Shell docking panes. Use `--shell-evidence-tab history` when the capture must prove the Evidence Workbench run-history list.

```powershell
dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --smoke-screenshot artifacts\shell_c3d_after.png --smoke-c3d thickness --smoke-contracts artifacts\shell_c3d_after.txt
dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --smoke-screenshot artifacts\shell_result_overlay_after.png --smoke-overlay result --smoke-contracts artifacts\shell_result_overlay_after.txt
dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --smoke-screenshot artifacts\shell_height_rule_after.png --smoke-rule height-deviation --smoke-contracts artifacts\shell_height_rule_after.txt
dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --smoke-screenshot artifacts\shell_recipe_height_rule_after.png --smoke-recipe recipes\c3d-height-deviation.recipe.json --smoke-contracts artifacts\shell_recipe_height_rule_after.txt
dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --smoke-screenshot artifacts\shell_recipe_ui_after.png --smoke-recipe recipes\c3d-height-deviation.recipe.json --smoke-contracts artifacts\shell_recipe_ui_after.txt
dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --smoke-screenshot artifacts\shell_workbench_layout_viewer_after.png --smoke-recipe recipes\c3d-height-deviation.recipe.json --smoke-contracts artifacts\shell_workbench_layout_after.txt
dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --smoke-screenshot artifacts\shell_deviation_legend_viewer_after.png --smoke-recipe recipes\c3d-height-deviation.recipe.json --smoke-contracts artifacts\shell_deviation_legend_after.txt
dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --smoke-screenshot artifacts\shell_render_controls_viewer_after.png --smoke-recipe recipes\c3d-height-deviation.recipe.json --smoke-point-size 4 --smoke-density Detailed --smoke-contracts artifacts\shell_render_controls_after.txt
dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --smoke-screenshot artifacts\shell_recipe_save_viewer_after.png --smoke-recipe recipes\c3d-height-deviation.recipe.json --smoke-tolerance 1500 --smoke-save-recipe artifacts\saved_shell_viewer_c3d_height_deviation.recipe.json --smoke-contracts artifacts\shell_recipe_save_after.txt
dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --smoke-screenshot artifacts\shell_laz_points_viewer_after.png --smoke-laz-points 3D\PublicSamples\PointCloud\xyzrgb_manuscript.laz --smoke-contracts artifacts\shell_laz_points_after.txt
dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --shell-smoke-screenshot artifacts\shell_laz_points_after.png --smoke-laz-points 3D\PublicSamples\PointCloud\xyzrgb_manuscript.laz
dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --smoke-screenshot artifacts\shell_laz_pick_viewer_after.png --smoke-laz-points 3D\PublicSamples\PointCloud\xyzrgb_manuscript.laz --smoke-pick laz --smoke-contracts artifacts\shell_laz_pick_after.txt
dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --shell-smoke-screenshot artifacts\shell_laz_pick_after.png --smoke-laz-points 3D\PublicSamples\PointCloud\xyzrgb_manuscript.laz --smoke-pick laz
dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --smoke-screenshot artifacts\shell_laz_two_point_viewer_after.png --smoke-laz-points 3D\PublicSamples\PointCloud\xyzrgb_manuscript.laz --smoke-measure two-point --smoke-contracts artifacts\shell_laz_two_point_after.txt
dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --shell-smoke-screenshot artifacts\shell_laz_two_point_after.png --smoke-laz-points 3D\PublicSamples\PointCloud\xyzrgb_manuscript.laz --smoke-measure two-point
dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --smoke-screenshot artifacts\shell_laz_acceptance_inspector_viewer_after.png --smoke-laz-points 3D\PublicSamples\PointCloud\xyzrgb_manuscript.laz --smoke-measure two-point --smoke-publish-result --smoke-contracts artifacts\shell_laz_acceptance_inspector_after.txt
dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --shell-smoke-screenshot artifacts\shell_laz_acceptance_inspector_after.png --smoke-laz-points 3D\PublicSamples\PointCloud\xyzrgb_manuscript.laz --smoke-measure two-point --smoke-publish-result
dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --smoke-screenshot artifacts\shell_laz_acceptance_edit_viewer_after.png --smoke-laz-points 3D\PublicSamples\PointCloud\xyzrgb_manuscript.laz --smoke-measure two-point --smoke-edit-parameters laz-acceptance --smoke-save-recipe artifacts\saved_shell_laz_two_point_acceptance_contract.recipe.json --smoke-contracts artifacts\shell_laz_acceptance_edit_after.txt
dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --shell-smoke-screenshot artifacts\shell_laz_acceptance_edit_after.png --smoke-laz-points 3D\PublicSamples\PointCloud\xyzrgb_manuscript.laz --smoke-measure two-point --smoke-edit-parameters laz-acceptance --smoke-save-recipe artifacts\saved_shell_laz_two_point_acceptance.recipe.json
dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --smoke-screenshot artifacts\shell_laz_acceptance_recipe_reopen_viewer_after.png --smoke-recipe artifacts\saved_laz_two_point_acceptance.recipe.json --smoke-contracts artifacts\shell_laz_acceptance_recipe_reopen_after.txt
dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --shell-smoke-screenshot artifacts\shell_laz_acceptance_recipe_reopen_after.png --smoke-recipe artifacts\saved_laz_two_point_acceptance.recipe.json
dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --recipe-comparison-contract artifacts\laz_acceptance_recipe_reopen_viewer_after.txt --recipe-comparison-report artifacts\runner_laz_run_history_after.txt --shell-smoke-screenshot artifacts\shell_laz_run_history_after.png --shell-evidence-tab history --smoke-recipe artifacts\saved_laz_two_point_acceptance.recipe.json
dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --smoke-screenshot artifacts\shell_flatness_viewer_after.png --smoke-contracts artifacts\shell_flatness_after.txt --recipe-comparison-contract artifacts\viewer_flatness_reopen_after.txt --recipe-comparison-report artifacts\runner_flatness_after.txt --shell-smoke-screenshot artifacts\shell_flatness_after.png --shell-evidence-tab steps --smoke-recipe artifacts\saved_c3d_plane_flatness.recipe.json
dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --smoke-screenshot artifacts\shell_dimensions_viewer_after.png --smoke-contracts artifacts\shell_dimensions_after.txt --recipe-comparison-contract artifacts\viewer_dimensions_reopen_after.txt --recipe-comparison-report artifacts\runner_point_pair_dimensions_after.txt --shell-smoke-screenshot artifacts\shell_dimensions_after.png --shell-evidence-tab steps --smoke-recipe artifacts\saved_c3d_point_pair_dimensions.recipe.json
dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --smoke-screenshot artifacts\shell_gap_flush_viewer_after.png --smoke-contracts artifacts\shell_gap_flush_after.txt --recipe-comparison-contract artifacts\viewer_gap_flush_reopen_after.txt --recipe-comparison-report artifacts\runner_gap_flush_after.txt --shell-smoke-screenshot artifacts\shell_gap_flush_after.png --shell-evidence-tab steps --smoke-recipe artifacts\saved_c3d_gap_flush.recipe.json
```

For recipe/runner work, also run:

```powershell
dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --recipe recipes\c3d-height-deviation.recipe.json --report artifacts\runner_c3d_height_rule_after.txt --expect-status Fail
dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --recipe artifacts\saved_c3d_plane_flatness.recipe.json --report artifacts\runner_flatness_after.txt --expect-status Fail --compare-contract artifacts\viewer_flatness_reopen_after.txt
dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --verify-plane-flatness --report artifacts\plane_flatness_golden_after.txt
dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --recipe artifacts\saved_c3d_point_pair_dimensions.recipe.json --report artifacts\runner_point_pair_dimensions_after.txt --expect-status Pass --compare-contract artifacts\viewer_dimensions_reopen_after.txt
dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --verify-point-pair-dimensions --report artifacts\point_pair_dimensions_golden_after.txt
dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --recipe artifacts\saved_c3d_gap_flush.recipe.json --report artifacts\runner_gap_flush_after.txt --expect-status Pass --compare-contract artifacts\viewer_gap_flush_reopen_after.txt
dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --verify-gap-flush --report artifacts\gap_flush_golden_after.txt
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_volume_after.png --smoke-recipe recipes\c3d-volume.recipe.json --smoke-publish-result --smoke-save-recipe artifacts\saved_c3d_volume.recipe.json --smoke-contracts artifacts\viewer_volume_after.txt
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_volume_reopen_after.png --smoke-recipe artifacts\saved_c3d_volume.recipe.json --smoke-contracts artifacts\viewer_volume_reopen_after.txt
dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --recipe artifacts\saved_c3d_volume.recipe.json --report artifacts\runner_volume_after.txt --expect-status Pass --compare-contract artifacts\viewer_volume_reopen_after.txt
dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --verify-volume --report artifacts\volume_golden_after.txt
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_cross_section_after.png --smoke-recipe recipes\c3d-cross-section-dimensions.recipe.json --smoke-publish-result --smoke-save-recipe artifacts\saved_c3d_cross_section_dimensions.recipe.json --smoke-contracts artifacts\viewer_cross_section_after.txt
dotnet run --project src\OpenVisionLab.ThreeDStudio\OpenVisionLab.ThreeDStudio.csproj -c Debug --no-build -- --smoke-screenshot artifacts\viewer_cross_section_reopen_after.png --smoke-recipe artifacts\saved_c3d_cross_section_dimensions.recipe.json --smoke-contracts artifacts\viewer_cross_section_reopen_after.txt
dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --recipe artifacts\saved_c3d_cross_section_dimensions.recipe.json --report artifacts\runner_cross_section_after.txt --expect-status Pass --compare-contract artifacts\viewer_cross_section_reopen_after.txt
dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --verify-cross-section --report artifacts\cross_section_golden_after.txt
dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --recipe artifacts\saved_c3d_cross_section_dimensions.recipe.json --report artifacts\run_record_cross_section\runner.txt --expect-status Pass --compare-contract artifacts\viewer_cross_section_reopen_after.txt --viewer-screenshot artifacts\viewer_cross_section_reopen_after.png --run-record artifacts\run_record_cross_section\run.json --html-report artifacts\run_record_cross_section\report.html --csv-report artifacts\run_record_cross_section\metrics.csv
dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --recipe recipes\laz-two-point-measurement.recipe.json --report artifacts\runner_laz_two_point_after.txt --expect-status Pass --compare-contract artifacts\laz_two_point_publish_after.txt
dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --recipe recipes\laz-two-point-measurement-fail.recipe.json --report artifacts\runner_laz_two_point_fail_after.txt --expect-status Fail
dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --recipe artifacts\saved_laz_two_point_acceptance.recipe.json --report artifacts\runner_laz_acceptance_edit_save_after.txt --expect-status Pass --compare-contract artifacts\laz_acceptance_edit_save_viewer_after.txt
dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --recipe artifacts\saved_laz_two_point_acceptance.recipe.json --report artifacts\runner_laz_acceptance_recipe_reopen_after.txt --expect-status Pass --compare-contract artifacts\laz_acceptance_recipe_reopen_viewer_after.txt
dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --recipe artifacts\saved_laz_two_point_acceptance.recipe.json --report artifacts\runner_laz_run_history_after.txt --expect-status Pass --compare-contract artifacts\laz_acceptance_recipe_reopen_viewer_after.txt
dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --recipe recipes\c3d-height-deviation.recipe.json --report artifacts\runner_recipe_compare_after.txt --expect-status Fail --compare-contract artifacts\viewer_recipe_height_rule_after.txt
dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --recipe recipes\c3d-height-deviation.recipe.json --report artifacts\runner_recipe_ui_compare_after.txt --expect-status Fail --compare-contract artifacts\viewer_recipe_ui_after.txt
dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --recipe recipes\c3d-height-deviation.recipe.json --report artifacts\runner_shell_recipe_ui_compare_after.txt --expect-status Fail --compare-contract artifacts\shell_recipe_ui_after.txt
dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --recipe recipes\c3d-height-deviation.recipe.json --report artifacts\runner_shell_recipe_compare_after.txt --expect-status Fail --compare-contract artifacts\shell_recipe_height_rule_after.txt
dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --recipe recipes\c3d-height-deviation.recipe.json --report artifacts\runner_shell_recipe_comparison_after.txt --expect-status Fail --compare-contract artifacts\shell_recipe_comparison_after.txt
dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --recipe recipes\c3d-height-deviation.recipe.json --report artifacts\runner_shell_workbench_layout_after.txt --expect-status Fail --compare-contract artifacts\shell_workbench_layout_after.txt
dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --recipe recipes\c3d-height-deviation.recipe.json --report artifacts\runner_shell_deviation_legend_after.txt --expect-status Fail --compare-contract artifacts\shell_deviation_legend_after.txt
dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --recipe recipes\c3d-height-deviation.recipe.json --report artifacts\runner_shell_render_controls_after.txt --expect-status Fail --compare-contract artifacts\shell_render_controls_after.txt
dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --recipe artifacts\saved_c3d_height_deviation.recipe.json --report artifacts\runner_recipe_save_after.txt --expect-status Fail --compare-contract artifacts\viewer_recipe_save_after.txt
dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --recipe artifacts\saved_shell_c3d_height_deviation.recipe.json --report artifacts\runner_shell_recipe_save_after.txt --expect-status Fail --compare-contract artifacts\viewer_recipe_save_after.txt
dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --recipe artifacts\saved_roi_alignment.recipe.json --report artifacts\runner_roi_alignment_recipe_after.txt --expect-status Fail
dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --recipe artifacts\saved_roi_alignment.recipe.json --report artifacts\runner_roi_alignment_recipe_compare_after.txt --expect-status Fail --compare-contract artifacts\viewer_roi_recipe_roundtrip_after.txt
dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --recipe artifacts\saved_roi_alignment_edited.recipe.json --report artifacts\runner_recipe_parameter_edit_after.txt --expect-status Fail --compare-contract artifacts\viewer_recipe_parameter_edit_after.txt
dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --recipe artifacts\saved_roi_alignment_auto.recipe.json --report artifacts\runner_interactive_alignment_after.txt --expect-status Fail --compare-contract artifacts\viewer_interactive_alignment_after.txt
dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --recipe artifacts\saved_roi_validation_valid.recipe.json --report artifacts\runner_roi_validation_valid_after.txt --expect-status Fail --compare-contract artifacts\viewer_roi_validation_valid_after.txt
dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --recipe-comparison-contract artifacts\shell_recipe_comparison_after.txt --recipe-comparison-report artifacts\runner_shell_recipe_comparison_after.txt --shell-smoke-screenshot artifacts\shell_recipe_comparison_after.png --smoke-recipe recipes\c3d-height-deviation.recipe.json
dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --recipe-comparison-contract artifacts\shell_workbench_layout_after.txt --recipe-comparison-report artifacts\runner_shell_workbench_layout_after.txt --shell-smoke-screenshot artifacts\shell_workbench_layout_after.png --smoke-recipe recipes\c3d-height-deviation.recipe.json
dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --recipe-comparison-contract artifacts\shell_deviation_legend_after.txt --recipe-comparison-report artifacts\runner_shell_deviation_legend_after.txt --shell-smoke-screenshot artifacts\shell_color_legend_after.png --smoke-recipe recipes\c3d-height-deviation.recipe.json
dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --recipe-comparison-contract artifacts\shell_render_controls_after.txt --recipe-comparison-report artifacts\runner_shell_render_controls_after.txt --shell-smoke-screenshot artifacts\shell_render_controls_after.png --smoke-recipe recipes\c3d-height-deviation.recipe.json --smoke-point-size 4 --smoke-density Detailed
dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --recipe-comparison-contract artifacts\viewer_recipe_save_after.txt --recipe-comparison-report artifacts\runner_recipe_save_after.txt --shell-smoke-screenshot artifacts\shell_recipe_save_after.png --smoke-recipe recipes\c3d-height-deviation.recipe.json --smoke-tolerance 1500 --smoke-save-recipe artifacts\saved_shell_c3d_height_deviation.recipe.json
dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --recipe-comparison-contract artifacts\viewer_section_profile_after.txt --recipe-comparison-report artifacts\runner_recipe_save_after.txt --shell-smoke-screenshot artifacts\shell_section_profile_after.png --smoke-recipe recipes\c3d-height-deviation.recipe.json --smoke-selection section
dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --recipe-comparison-contract artifacts\viewer_height_map_after.txt --recipe-comparison-report artifacts\runner_recipe_save_after.txt --shell-smoke-screenshot artifacts\shell_height_map_after.png --smoke-recipe recipes\c3d-height-deviation.recipe.json
dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --shell-smoke-screenshot artifacts\shell_viewer_internal_hud_after.png --smoke-measure two-point
dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --shell-smoke-screenshot artifacts\shell_roi_step_after.png --smoke-measure roi-step
dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --shell-smoke-screenshot artifacts\shell_roi_interactive_after.png --smoke-c3d thickness --smoke-alignment offset --smoke-measure roi-interactive
dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --shell-smoke-screenshot artifacts\shell_roi_recipe_roundtrip_after.png --smoke-recipe artifacts\saved_roi_alignment.recipe.json
dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --shell-smoke-screenshot artifacts\shell_alignment_after.png --smoke-c3d thickness --smoke-alignment offset
dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --shell-smoke-screenshot artifacts\shell_recipe_parameter_edit_after.png --smoke-recipe artifacts\saved_roi_alignment.recipe.json --smoke-edit-parameters roi-align
dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --shell-smoke-screenshot artifacts\shell_interactive_alignment_after.png --smoke-recipe artifacts\saved_roi_alignment.recipe.json --smoke-align-from-roi
dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --shell-smoke-screenshot artifacts\shell_roi_validation_invalid_after.png --smoke-recipe artifacts\saved_roi_alignment.recipe.json --smoke-invalid-roi overlap
dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --recipe recipes\c3d-height-deviation.recipe.json --report artifacts\runner_run_history_after.txt --expect-status Fail --compare-contract artifacts\viewer_height_map_after.txt
dotnet run --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Debug --no-build -- --recipe-comparison-contract artifacts\viewer_height_map_after.txt --recipe-comparison-report artifacts\runner_run_history_after.txt --shell-smoke-screenshot artifacts\shell_run_history_after.png --shell-evidence-tab history --smoke-recipe recipes\c3d-height-deviation.recipe.json
```

For GitHub Actions CI workflow work, keep CI headless and Windows-based:

```powershell
dotnet restore OpenVisionLab.ThreeDStudio.slnx
python scripts\verify-nuget-package-health.py --self-test
python scripts\verify-nuget-package-health.py --solution OpenVisionLab.ThreeDStudio.slnx --report artifacts\ci\nuget_package_health.txt --json-directory artifacts\ci\nuget-package-health
dotnet build OpenVisionLab.ThreeDStudio.slnx -c Debug --no-restore
dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --recipe recipes\c3d-height-deviation.recipe.json --report artifacts\ci\runner_c3d_height_rule.txt --expect-status Fail
```

UI/UX work requires current screenshots from the running build. Store before/after captures in an artifact folder and report the paths.

## Priority Direction

1. Preserve the passed Viewer Foundation v1, C3D display-frame fidelity, and all five typed inspection-slice baselines.
2. Obtain the C3D calibration contract and add an explicit mapping profile; never infer or advertise physical units.
3. Add one measured/nominal comparison slice without a CAD kernel when a distinct local sample pair is available.
4. Preserve Durable Run Record v1.2 typed-step identity and older-minor Shell compatibility; extend it only after multiple real runs expose a concrete need.
5. Preserve binary-only Viewer DLL hosting and add its verification to Windows CI before expanding the Host API.
6. Introduce shared parser/executor abstractions only when concrete duplication across completed tools justifies them.
7. Expand CAD precision, device integration, enterprise data, and AI assistance only after the local inspection loop is verified.

When starting after orientation, state the immediate priority and the remaining project priority before editing files or running follow-up commands.

When finishing any task, always include the next recommended priority in the final response. Base it on the current repository evidence, this priority direction, and the next-session handoff. If the task was documentation-only, still include a concrete next priority.

## No Guessing

- Check files, commands, sources, or local prototypes before making factual claims.
- If evidence conflicts, surface the conflict.
- If an engine or library is only a candidate, call it a candidate.
- If a behavior is inferred from the 2D reference repo, label it as an inference.

## Simplicity First

- Prefer the smallest viewer that proves load, render, camera, picking, measurement, overlay, and screenshot smoke.
- Prefer direct, understandable SharpGL code over a broad 3D framework until the MVP proves a missing capability.
- Add abstractions only after a second real use case exists or the 2D reference repo has a matching proven pattern.
- Do not scaffold broad plugin systems, hardware integrations, or CAD editing workflows before the viewer and validation contracts exist.
