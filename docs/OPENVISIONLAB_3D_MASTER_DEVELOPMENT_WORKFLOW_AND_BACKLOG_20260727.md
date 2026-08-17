# OpenVisionLab 3D Master Development Workflow and Backlog

Date: 2026-07-27

Status: Current execution source of truth for commercial-video-derived product
development after Inspection Workspace v3

This document is the single owner of the current capability inventory,
dependency graph, and development queue. `AGENTS.md` owns operating rules;
the next-session and next-chat documents are short entry points; dated
closure, design, and audit documents preserve historical evidence.

## Purpose

This document converts the commercial-video review into an executable product
development system.

It answers five questions for every future chat:

1. What product are we building?
2. What has already been developed?
3. What is partial or missing?
4. In what dependency order should the remaining work be developed?
5. What evidence closes one item before another begins?

Read this document after:

- `AGENTS.md`;
- `docs/OPENVISIONLAB_3D_NEXT_CHAT_HANDOFF_PROMPT_20260728.md`;
- `docs/OPENVISIONLAB_3D_NEXT_SESSION_HANDOFF.md`;
- `docs/OPENVISIONLAB_3D_COMMERCIAL_VIDEO_DIRECTION_AND_PRIORITY_20260727.md`;
- `docs/OPENVISIONLAB_3D_INDUSTRIAL_UX_AUDIT_20260728.md`.

## Product identity

OpenVisionLab 3D Studio is:

> A local, file-first, deterministic 2.5D/3D rule-based inspection workbench
> for identified height fields, point clouds, and meshes.

The operator:

- loads a measured source and optional reference;
- verifies identity, frame, unit, and data quality;
- teaches typed regions and features;
- configures deterministic preparation, alignment, and inspection steps;
- explicitly Previews, Publishes, or Runs;
- reviews metrics, overlays, tolerance state, and failure evidence;
- replays the recipe across a Validation Set;
- saves a durable recipe and Run Record.

The Viewer is a synchronized teaching and evidence surface. It is not the
entire product.

Commercial products are benchmark evidence, not visual templates. Adapt
principles such as current-task clarity, linked configuration/Viewer/evidence,
progressive disclosure, and explicit status. Do not copy themes, exact panel
geometry, screen topology, terminology, assets, or icon artwork. A
benchmark-driven item closes against an OpenVisionLab operator problem and
product contract, not screenshot similarity.

## Product workflow target

```text
1. Input
   local source/reference identity
      |
2. Source trust
   valid/missing map, distribution, frame, unit, provenance
      |
3. Teaching workspace
   linked Height Image + 3D surface + profile
      |
4. Typed regions/features
   GridRectangle / PointSet / OrientedBox3D / derived region artifacts
      |
5. Preparation
   filter / mask / level / align / transform / re-grid
      |
6. Inspection
   thickness / flatness / presence / completeness / matching / dimensions
      |
7. Explicit execution
   Preview -> Publish where applicable -> Run
      |
8. Evidence
   per-region metrics + overlays + status + timing + failure reason
      |
9. Sample validation
   Good / Bad / Held-out evidence and replay
      |
10. Persistence
    recipe + source identity + Run Record + export
```

## Status legend

| Status | Meaning |
| --- | --- |
| `C` Complete | Present in current source with reusable focused evidence. Preserve it. |
| `P` Partial | A real adjacent capability exists, but the target workflow is not complete. |
| `N` New | No current typed product contract or complete workflow was found. |
| `E` External prerequisite | Completion needs owner operation, physical data, calibration, hardware, or another non-code prerequisite. |
| `O` Out of scope | Deliberately excluded from the current product phase. |

`C` does not mean certified metrology. It means complete for its documented
software scope.

Current inventory count:

| Classification | Count |
| --- | ---: |
| Complete `C` | 139 |
| Partial `P` | 17 |
| New `N` | 54 |
| External prerequisite `E` | 9 |
| Out of scope `O` | 16 |
| Total | 235 |

## Current maturity and first gate

- Inspection Workspace v3 is `7/8` bounded slices (`87.5%`) complete.
- GoPxL-inspired Workbench v4 is `3/3` delivery slices complete. Authoring,
  evidence-linked Validate/Results, the graphite visual system, and safe
  presentation-only layout persistence are closed. Preserve
  `docs/OPENVISIONLAB_3D_GOPXL_WORKBENCH_V4_LAYOUT_CONTRACT_20260730.md` and
  `docs/OPENVISIONLAB_3D_GOPXL_WORKBENCH_V4_EVIDENCE_AND_SAFE_LAYOUT_20260730.md`.
- The 2026-07-30 Viewer single-row and Height color-range slice is complete:
  the loaded Single Viewer shares one common top row, the persistent left HUD
  is removed, and the right legend remaps Height colors through display-only
  low/high bounds. Preserve
  `docs/OPENVISIONLAB_3D_VIEWER_SINGLE_ROW_AND_HEIGHT_COLOR_RANGE_20260730.md`.
- The 2026-07-30 Viewer command-bar presentation slice is complete: current
  Wide/Compact evidence uses compact icon commands without changing the
  explicit Preview/Publish/Run contract. Preserve
  `docs/OPENVISIONLAB_3D_VIEWER_COMMAND_BAR_SIMPLIFICATION_20260730.md`.
- Its historical remaining gate was the owner's unaided exact-source replay.
  The 2026-07-29 information-architecture change reopens `A-01`; after the
  new stages are implemented, that replay must be replaced by the
  Setup/Teach/Validate/Results owner path.
- The current local deterministic recipe/measurement foundation is
  operational.
- The coordinate-true full-size Height Image display foundation is complete.
  Shared 2D/3D native-coordinate hover, synchronized `GridRectangle` ROI
  teaching, and the visible invalid-cell overlay are complete. Typed
  preparation, completeness cell metrics, deterministic cell acceptance,
  aggregate results, linked colored overlays, failed-cell review, and
  repeated-Tab result mapping, Validation Set examples, and Completeness
  threshold assistance are complete. Surface matching now has identified
  pose, raw coverage, transformed-model Viewer evidence, and exact
  Workbench/Runner parity, separate recipe-owned acceptance, authored finite
  search bounds, rejection reasons, observational stage timing, identified
  model/scene 3D-edge artifacts, and separate diagnostic edge scoring.
  Direction diagnostics, independent component thresholds, and one retained
  false-positive review are complete. Published/Candidate parameter
  experimentation with exact no-rerun Publish is also complete. Stable,
  disjoint multiple-match collection and presentation-only selection are now
  complete. Non-wrapping previous/next retained-match review is complete.
  SurfaceModel symmetry declaration and independent symmetry-aware pose
  equivalence are complete. Deterministic model-side key points now have
  stable sample/triangle identities, persistence, and a WPF-neutral display-
  only overlay, while matching consumption remains deliberately absent.
  Production performance claims remain incomplete. Explicit source-frame
  acquisition direction and display-only edge-normal orientation are complete.
- Physical calibration, traceability, uncertainty, GR&R, and production
  tolerance are unverified.

### Current execution checkpoint - Coordinate-confident grid ROI teaching - 2026-08-17

`PL-0017` is Complete. GridRectangle capture now enters the existing Top
orthographic fit and keeps exact start column, start row, column count, and row
count beside Apply and Cancel. Wide no longer requires the operator to find a
deep numeric section, and Compact no longer hides the only exact values.

Current Release EXE evidence starts from Perspective and teaches the saved
Thickness reference and measurement targets with one actual drag each. Target
coverage is `0.9756` and `1.0000`; stable routes are restored by explicit
Apply, no corrective redraw is needed, and Preview/Run remain untouched.
Actual pointer checks retain orbit, pan, wheel zoom, picking/context bindings,
Undo/repick, Esc Cancel, Enter/explicit Apply, move, resize, display-height
adjustment, and camera/authored/execution boundaries.

Release builds with `0` warnings and `0` errors. Focused checks pass Height
Measurement Workbench `56/56`, Tool Recipe teaching `50/50`, Inspection
Workspace selection `64/64`, Workbench docking/theme `87/87`, Teaching capture
ViewModel `25/25`, and Shell options `40/40`. Current Wide `1920 x 1040`
English and Compact `1280 x 760` Korean screenshots pass quality on attempt 1,
remain bounded and themed, and intersect the dynamically selected leftmost
monitor. Preserve:

- `docs/OPENVISIONLAB_3D_GRID_ROI_COORDINATE_CONFIDENCE_20260817.md`;
- `.proofline/issues/PL-0017.json`;
- `D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260817-pl0017-grid-roi-coordinate-confidence\`.

The evidence-bounded commercial authoring-readiness reassessment is `8.6/10`.
This is a qualitative workflow judgment, not telemetry, release acceptance,
certified usability, production approval, or physical-metrology evidence. The
capability inventory is unchanged and no dependency-ready software slice is
selected. Product-owner unaided Wide/Compact R0 remains the acceptance
priority and requires owner operation rather than model execution. The
large-C3D candidate remains blocked by its missing input and accepted budgets.

### Previous execution checkpoint - Shell ordered Thickness Run - 2026-08-17

`PL-0016` is Complete. Validate now exposes one explicit `Run current recipe`
action for a saved, source-ready recipe whose ordered steps have typed replay
adapters. The action uses the existing `ToolRecipeOrderedGraphExecution`
engine, writes schema `1.5` Run Record evidence, and projects it immediately
into Results. Studio and Runner now share one ordered-step Run Record
projection instead of duplicating result arithmetic or identity policy.

Editing invalidates current evidence and requires save before another Run.
Open, Preview, Publish, compatible-variant creation, layout, save, and reopen
do not invoke full Run. Exact ready/incomplete/unsupported reasons, Running,
Pass/Fail/Error, key metric, output identity/hash, and record state remain
together in Validate.

Ten current Release EXE processes at Compact `1280 x 760` match the expected
`Pass 4 / Fail 5 / Error 1`; status, metrics, ordered step identity, output
identity, output hash, and Error representation match production Runner
records `10/10`. Ordered Run duration is p50 `468.425 ms`, p95
`533.351 ms`, and max `533.351 ms`, passing the current fixture-class
interaction budget p95 `<= 600 ms` and max `<= 750 ms`. This excludes EXE
startup and is not a maximum-input or production SLA.

Focused checks pass ordered Run `13/13`, Tool Recipe teaching `50/50`, Run
Record history `12/12`, Recipe Manager/WPG `52/52`, and Shell options `40/40`.
Release builds with `0` warnings and `0` errors. Current Wide and Compact
ready, held pointer-down, executed Fail, and linked Results evidence remains
readable, themed, bounded, and on the dynamically selected leftmost monitor.
Preserve:

- `docs/OPENVISIONLAB_3D_SHELL_ORDERED_THICKNESS_RUN_CLOSURE_20260817.md`;
- `.proofline/issues/PL-0016.json`;
- `D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260817-pl0016-shell-ordered-thickness-run\`.

The evidence-bounded commercial authoring-readiness reassessment is `8.5/10`.
This is a qualitative workflow judgment, not telemetry, release acceptance,
certified usability, production approval, or physical-metrology evidence.
The capability inventory is unchanged. `PL-0017` coordinate-confident grid
ROI teaching is the selected next software priority. Recommended model:
`gpt-5.6-sol`; reasoning effort: `medium`. Product-owner unaided Wide/Compact
R0 remains the separate acceptance priority and requires owner operation
rather than model execution.

### Previous execution checkpoint - Thickness 10-sample EXE UX and performance - 2026-08-17

`PL-0015` is Complete. Ten generated `1280 x 840` C3D height fields cover
nominal, lower and upper boundary, below/above tolerance, noise, gradient,
missing data, insufficient data, and a local defect. The actual Release Shell
EXE created ten current-schema Thickness recipes and explicitly Previewed and
Published each controlled result when Publish was permitted. Ordered Runner
replay matched `Pass 4 / Fail 5 / Error 1`.

Recipe Center now creates a grid-compatible source variant from a saved
recipe. It keeps direct C3D `GridRectangle` ROI identities and coordinates,
ordered steps, routes, and parameters; rebinds them to a different same-size
C3D identity; rejects unsafe selection types and grid-size mismatch; saves a
new recipe; and does not invoke Preview, Publish, Run, or Validation. The
observed repeated workflow decreased from 33 to 11 actions. Variant readiness
was observed no later than `1.916 s`; fresh-process ordered replay remained at
or below `244.75 ms`; and the Thickness step remained at or below `15.02 ms`
for these fixtures. These are workstation regression targets, not production
or maximum-input SLAs.

The insufficient-data controlled Error also exposed non-finite metrics that
prevented JSON Run Record export. The writer now omits non-finite JSON metrics
without changing the controlled Error state or message. Release builds `0/0`;
Recipe Manager/WPG passes `52/52`; height measurement passes `56/56`; and the
artifact-owned Runner passes `19/19`, including this regression. Actual Wide
and Compact current-build evidence remains bounded on leftmost `DISPLAY2`.
Preserve:

- `docs/OPENVISIONLAB_3D_THICKNESS_10_SAMPLE_EXE_UX_PERFORMANCE_STUDY_20260817.md`;
- `.proofline/issues/PL-0015.json` through `PL-0017.json`;
- `D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260817-thickness-10-recipe-ux-performance\`.

At this checkpoint, the evidence-bounded commercial authoring-readiness
reassessment was `8.2/10`.
This is a qualitative workflow judgment, not telemetry, release acceptance,
certified usability, production approval, or physical-metrology evidence.
The capability inventory was unchanged. `PL-0016` was the selected next
software priority and is superseded by the completed checkpoint above.

### Previous execution checkpoint - Studio language popup - 2026-08-16

`PL-0014` is Complete. The responsive width style on
`StudioLanguageSelector` now derives from the existing shared ComboBox style
instead of replacing it with the Windows platform-default template. The
popup, item text, selected state, keyboard focus, hover, disabled state, and
open transition therefore use the existing semantic graphite resources.
Compact keeps the control inside the 60-pixel rail with a 52-pixel control,
4-pixel outer margin, and reduced content padding, so `한` and `EN` remain
visible without changing the Wide `한국어` and `English` labels.

Debug and Release build with zero warnings and zero errors, and Workbench
docking passes `87/87`, including shared-style, Wide/Compact bounds, and
disabled-resource checks. Actual current Release EXE evidence on dynamically
selected leftmost `DISPLAY2` covers Wide `1920 x 1040`, Compact `1280 x 760`,
Korean/English, open, selected, keyboard-focus, and pointer-hover states.
Language selection updates the UI and survives a normal restart while the
recipe, source, ROI, result, and `Preview 0`/`Published 0` state remain
unchanged. Both refreshed fixed-package `-ValidateOnly` modes pass without
launching the application. Preserve:

- `docs/OPENVISIONLAB_3D_LANGUAGE_SELECTOR_POPUP_20260816.md`;
- `.proofline/issues/PL-0014.json`;
- `D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260816-pl0014-language-popup\`.

The evidence-bounded commercial authoring-readiness reassessment is `8.0/10`.
This is a qualitative workflow judgment, not telemetry, release acceptance,
certified usability, production approval, or physical-metrology evidence. The
capability inventory was unchanged and no dependency-ready software slice was
selected at that checkpoint. The current PL-0015 checkpoint above supersedes
that software-priority statement. Product-owner unaided Wide/Compact R0
remains the next acceptance priority; it requires owner operation, so no
model execution is recommended. The large-C3D candidate remains blocked on its representative
input and accepted memory/load-time limits.

### Previous execution checkpoint - Tool Library search context - 2026-08-16

`PL-0012` is Complete. Tool Library search now clears only after a successful
recipe open, new-recipe context creation, or compatible Add. Failed open and
rejected Add retain the visible query, and no search transition invokes
Preview, Publish, Run, or Validation. No persisted preference, additional
control, recipe schema, numerical algorithm, Viewer, or docking behavior was
introduced or changed.

Tool Recipe teaching passes `50/50`, Workbench docking `84/84`, and Debug and
Release build with zero warnings and zero errors. Actual current Release EXE
Compact English and Wide Korean evidence on dynamically selected leftmost
`DISPLAY2` shows representative non-empty input followed by a blank unfiltered
catalog after another recipe opens. Both refreshed fixed-package
`-ValidateOnly` modes pass without launching the application. Preserve:

- `docs/OPENVISIONLAB_3D_TOOL_LIBRARY_SEARCH_CONTEXT_20260816.md`;
- `.proofline/issues/PL-0012.json`;
- `D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260816-pl0012-tool-search-context\`.

The evidence-bounded commercial authoring-readiness reassessment is `7.9/10`.
This is a qualitative workflow judgment, not telemetry, release acceptance,
certified usability, production approval, or physical-metrology evidence. The
capability inventory is unchanged. `PL-0014` language-popup theme and bounds is
the selected next software priority. Recommended model: `gpt-5.6-terra`;
reasoning effort: `low`. Product-owner unaided Wide/Compact R0 remains the
separate acceptance priority and requires owner operation rather than model
execution.

### Previous execution checkpoint - first-use recipe setup - 2026-08-16

`PL-0013` is Complete. Recipe Center now exposes recipe name, folder, C3D
source, optional Empty/Thickness starter, exact target, validation, remembered
setup, and Reset before one explicit Create action. Confirmed setup persists at
workspace scope and restores visibly and editably. Missing restored paths are
explained and disable Create. Open/edit/restore/reset do not create, load, add,
Preview, Publish, Run, or change source/result state.

Recipe Manager + WPG passes `49/49`, Tool Recipe teaching `46/46`, Workbench
docking `84/84`, Shell smoke options `39/39`, and Debug/Release build with zero
warnings and zero errors. Actual current Release EXE empty and Thickness
creation both save/reopen successfully; the Thickness case retains one typed
`thickness` step routed from `source.c3d.height-map`. Wide/Compact
English/Korean valid, focused input, open popup, stale/disabled, and held
pressed evidence remains themed and bounded on dynamically selected leftmost
`DISPLAY2`. Both refreshed fixed-package `-ValidateOnly` modes pass without
launching the application. Preserve:

- `docs/OPENVISIONLAB_3D_FIRST_USE_RECIPE_SETUP_20260816.md`;
- `.proofline/issues/PL-0013.json`;
- `D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260815-pl0013-first-use-setup\`.

The evidence-bounded workflow reassessment is first-use efficiency `8.5/10`
and commercial authoring readiness `7.8/10`. These are qualitative judgments,
not telemetry, release acceptance, certified usability, production approval,
or physical-metrology claims. This slice does not change capability inventory.
`PL-0012` search-context correction is completed in the current checkpoint
above. Product-owner unaided Wide/Compact R0 remains the separate acceptance
priority and requires owner operation rather than model execution.

### Previous execution checkpoint - recipe health navigation - 2026-08-15

`PL-0011` is Complete. Flow now projects every recipe step into exactly one of
`Ready`, `Needs input`, `Needs selection`, `Needs parameters`, `Stale Preview`,
or `Published`, shows exact localized counts, and exposes the selected owning
step and requirement. Non-wrapping Previous/Next navigation selects and scrolls
the exact stable step without invoking Preview, Publish, or Run or changing
recipe, source, result, dirty, layer, or active-input state.

Tool Recipe teaching passes `46/46`, Workbench docking `84/84`, Shell smoke
options `37/37`, and the current Release build has zero warnings and zero
errors. Current Release Wide English and Compact English/Korean evidence on
dynamically selected leftmost `DISPLAY2` shows reachable health actions,
automatic reveal of step 17, and a correctly themed held pointer-down state.
The refreshed fixed package passes both Wide and Compact `-ValidateOnly`
without launching the application. The UX study's evidence-bounded current
reassessment is Compact long-chain overview `7.5/10` and commercial authoring
readiness `7.4/10`; these are workflow judgments, not telemetry, release
acceptance, or physical-metrology claims. Preserve:

- `docs/OPENVISIONLAB_3D_RECIPE_HEALTH_NAVIGATION_20260815.md`;
- `.proofline/issues/PL-0011.json`;
- `D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260815-pl0011-recipe-health-navigation\`.

This software slice does not change capability inventory. Its selected
follow-up, `PL-0013`, is completed in the current checkpoint above.

### Previous execution checkpoint - EXE recipe-authoring UX study - 2026-08-15

The current Release EXE saved and reopened ten current-format recipes against
the bundled Thickness Coupon C3D. All ten files parse and retain `90` total
steps. The eight-step Thickness baseline is the only ready single-task recipe;
the other pending or incompatible chains are retained as authoring evidence,
not as successful inspection runs.

The study confirms a high-risk composition defect: `All tools` can append a
HeightField consumer after a Thickness MeasurementResult, silently route the
incompatible result, and save/reopen the invalid recipe. It also records the
split add/configure/teach path, missing long-chain health navigation, stale
Tool Library search, and fragmented first-use setup. Preserve:

- `docs/OPENVISIONLAB_3D_EXE_RECIPE_AUTHORING_UX_STUDY_20260815.md`;
- `.proofline/issues/PL-0009.json` through `PL-0014.json`;
- `D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260815-exe-recipe-authoring-study\recipes\`.

`PL-0009` is now Complete. Measurement Add resolves the newest compatible
typed artifact, generic HeightField consumers fall back to the identified
source instead of a MeasurementResult, and transformed-only tools remain
unavailable until a `TransformedHeightField` exists. The Tool Library shows
the proposed typed route before Add. Invalid legacy recipes remain loadable
as repairable drafts; selecting the affected step shows a direct bilingual
repair action that expands Inputs and advanced route editing without changing
the recipe or invoking Preview, Publish, or Run.

Current verification passes Tool Recipe teaching `42/42`, Height Measurement
Workbench `54/54`, Recipe Manager + WPG `40/40`, Tool Recipe selections
`29/29`, Artifact Navigator, and full Release build `0 warnings / 0 errors`.
Actual current-Release Wide/Compact English/Korean evidence on leftmost
`DISPLAY2` is under
`D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260815-pl0009-compatible-tool-routing\`.
This correction does not change capability inventory. `PL-0010` contextual
add/configure/teach/repair and `PL-0011` recipe health navigation are complete;
use the current checkpoint above for the selected software priority. Human-
owner Compact R0 remains a separate acceptance prerequisite and was not
completed by these slices.

### Previous execution checkpoint - Workbench run-log retention - 2026-08-15

`PL-0008` is Complete. The production `AppendLog` path now writes every
Workbench event to the existing rolling `OVLog` files before projecting it
into the in-memory session list. The projection retains the newest 3,000
entries in newest-first order and prunes only the oldest overflow. The
localized Application Log caption states that boundary; it does not invoke or
change Preview, Publish, Run, Validation, recipe, source, or result state.

The existing durable-file policy remains 50 MB with 20 backups; this slice
does not add an export format or claim indefinite retention. Focused retention
passes `6/6`; Tool Recipe teaching `35/35`; Validation Set `84/84`; Recipe
Manager + WPG `40/40`; Shell command line `36/36`; Workbench docking `82/82`;
logging integration `4/4`; structure `29/29`; Debug/Release builds `0/0`.
Current-build Wide/Compact English/Korean screenshots pass capture quality,
the refreshed fixed-input R0 package passes both `-ValidateOnly` modes, and
GitHub Actions CI `#76` succeeds for commit `e43bebb`. Preserve:

- `docs/OPENVISIONLAB_3D_WORKBENCH_RUN_LOG_RETENTION_20260815.md`;
- `.proofline/issues/PL-0008.json`;
- `D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260815-run-log-retention\`.

This maintenance correction does not change inventory, which remains
`139 C / 17 P / 54 N / 9 E / 16 O`. Its then-current next-priority statement
is superseded by the EXE recipe-authoring checkpoint above. Human-owner
Wide/Compact R0 remains the separate acceptance priority and requires owner
operation rather than model execution.

### Previous execution checkpoint - recipe-step removal safety - 2026-08-15

`PL-0007` is Complete. The selected recipe-step Remove command now requests an
explicit themed confirmation before mutation. The confirmation names the step
and reports the teaching selections that would become unused. Cancel is the
default and preserves steps, selections, selection identity, dirty state, and
Run Log. Confirm rechecks the stable step ID and execution state, then removes
only that step and selections no remaining step uses.

Removal is unavailable and fails closed during any active tool Preview,
Run-backed Preview, Surface Match experiment, or Validation Set execution.
Request, Cancel, and dialog review do not invoke Preview, Publish, Run, or
Validation. Recipe Manager + WPG passes `40/40`; Shell command-line coverage
passes `35/35`; Debug/Release builds pass `0/0`; structure passes `29/29`;
current-build Wide/Compact English/Korean normal and held pointer-down evidence
passes capture-quality checks; refreshed fixed-input Wide/Compact R0
`-ValidateOnly` passes without launching the application. Preserve:

- `docs/OPENVISIONLAB_3D_RECIPE_STEP_REMOVAL_SAFETY_20260815.md`;
- `.proofline/issues/PL-0007.json`;
- `D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260815-recipe-step-removal-safety\`.

This safety correction improves an existing workflow and does not add a new
inventory item, so inventory remains `139 C / 17 P / 54 N / 9 E / 16 O`.
It does not provide general Undo/Redo or expand confirmation to other deletion
paths. The next eligible software maintenance item remains bounded Workbench
run-log retention that preserves durable `OVLog` evidence. Recommended model:
`gpt-5.6-terra`; reasoning effort: `low`. Human-owner Wide/Compact R0 remains
the separate acceptance priority and requires owner operation rather than
model execution.

### Previous execution checkpoint - release-policy reconciliation - 2026-08-06

`PL-0006` is Complete. The current release/version policy now distinguishes
historical `v0.1.0-rc.1` candidate evidence from current GitHub publication
state. GitHub reports no Releases, the Tags page exposes no tag,
`git ls-remote --tags origin` returns zero refs, and historical commit
`ac57687` is neither an ancestor of current `main` nor owned by a current
remote ref. No release, tag, asset, commit, or push was created.

The policy's current-values table now matches source-owned product
`0.1.1-dev`, Viewer Host API `1.0`, Viewer manifest `1.0`, Run Record `1.6`,
and generic Tool Recipe `1.5`. Future publication still requires explicit
owner approval, the complete release gate, and the product owner's unaided
Wide/Compact R0 for the exact release target. Preserve:

- `docs/OPENVISIONLAB_3D_RELEASE_VERSION_POLICY.md`;
- `.proofline/issues/PL-0006.json`;
- `docs/OPENVISIONLAB_3D_SHARED_CHAT_ANALYSIS_AND_C3D_LOAD_SNAPSHOT_20260806.md`.

This documentation correction does not change inventory. The next eligible
software maintenance item is bounded Workbench run-log retention that
preserves durable `OVLog` evidence. Recommended model: `gpt-5.6-terra`;
reasoning effort: `low`. Human-owner Wide/Compact R0 remains the separate
acceptance priority and requires owner operation rather than model execution.

### Current execution checkpoint - truthful alignment status summary - 2026-08-06

`PL-0005` is Complete. The Studio header now selects the most downstream
present alignment stage in A3, A2, A1, then legacy order and displays that
step's actual `State`. Step-state changes raise the header presentation
notification without invoking Preview, Publish, Run, or Validation.

Debug and Release builds pass with `0` warnings and `0` errors. The existing
CI-routed Tool Recipe teaching verifier passes `35/35`. Current-build
application-only Wide `1920 x 1040` and Compact `1280 x 760` before/after
captures pass quality checks and show `A3 Re-grid Height Map | Waiting for
upstream` instead of the stale legacy message. Refreshed Wide and Compact R0
`-ValidateOnly` checks pass without launching the application. Preserve:

- `docs/OPENVISIONLAB_3D_TRUTHFUL_ALIGNMENT_STATUS_SUMMARY_20260806.md`;
- `.proofline/issues/PL-0005.json`;
- `D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260806-alignment-status-summary\`.

This is a truthfulness correction for existing alignment behavior, not a new
inventory item, so inventory remains `139 C / 17 P / 54 N / 9 E / 16 O`.
Its former next correction is now complete as `PL-0006`; use the current
checkpoint above for active priority selection.

### Current execution checkpoint - immutable C3D loaded snapshot - 2026-08-06

`PL-0004` is Complete. One open `C3DHeightGrid` now owns the exact raw samples
parsed at load, and point, row, line-profile, full-map, display-density, and
inspection resampling all use that same snapshot without reopening its mutable
path. Explicitly loading a source again still creates a new identified
snapshot.

Debug and Release builds pass with `0` warnings and `0` errors. The focused
C3D contract passes `14/14`; affected height, plane, deviation, map,
flatness, Gap/Flush, and Volume checks pass `113/113`; and refreshed Wide and
Compact R0 `-ValidateOnly` checks pass without launching the application.
Preserve:

- `docs/OPENVISIONLAB_3D_SHARED_CHAT_ANALYSIS_AND_C3D_LOAD_SNAPSHOT_20260806.md`;
- `.proofline/issues/PL-0004.json`;
- `D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260806-c3d-immutable-load-snapshot\`.

This is a correctness closure for existing `B-01` identity behavior, not a
new inventory item, so inventory remains `139 C / 17 P / 54 N / 9 E / 16 O`.
Its former next correction is now complete as `PL-0005`; use the current
checkpoint above for active priority selection.

### Current execution checkpoint - OpenVisionLab Vision SDK 3 migration - 2026-08-05

The active numerical dependency has moved from `Lib.ThreeD 2.9.1` to the
repository-vendored `OpenVisionLab.Vision3D 3.0.0`, built from committed SDK
source `f34fdf912ff38fe20f36dbb063837e14b4f922b3` with package SHA-256
`F7324DC43ABF8E130D6F88C034287C192CFEA89E16A8A906A60F52DE341045B4`.
The feed, package references, namespaces, adapters, Runner, structure guard,
CI commands, publication check, R0 hashes, and current documents now use the
Vision SDK identity. A clean Studio clone does not require an adjacent SDK
checkout.

SDK Release build and smoke pass `0/0` and `154/154`; the isolated package-only
consumer passes; Studio Release and Debug builds pass `0/0`; package, bridge,
and structure checks pass, respectively, `1/1`, `26/26`, and `29/29`; Runner
and Shell matrices pass `46/46` and `27/27`; the bundled Thickness Coupon
passes `8/8`; and the self-contained manifest passes `502/502`. Normalized
primary-report parity is `73/73`, with two documented one-ULP scaled-distance
changes and their derived hashes. Preserve:

- `docs/OPENVISIONLAB_3D_VISION_SDK_3_MIGRATION_20260805.md`;
- `docs/OPENVISIONLAB_3D_VISION_SDK_TOOL_CONTRACT_AND_MIGRATION_BASELINE_20260805.md`;
- `docs/OPENVISIONLAB_3D_VISION_SDK_PACKAGE_BOUNDARY_20260805.md`;
- `D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260805-vision-sdk-3-migration\`.

This dependency migration does not change inventory classification. It is on
main at `8400b89a788b2a59affb713833001fff15c6aff0`; GitHub Actions run
`31012735944` completed successfully. Human-owner
Wide/Compact R0 remains the next acceptance priority; prerequisite: owner
operation and evidence; recommended model: none; reasoning effort: none.

### Current acceptance checkpoint - Human-owner R0 fixed-input refresh - 2026-08-06

Status is Blocked only on the product owner's unaided Wide and Compact runs.
The current source was rebuilt in Release with `0/0`; seven current binaries,
the unchanged Completeness recipe, and the supplied Fail Run Record have new
fixed SHA-256 evidence; and both `-ValidateOnly` modes pass without launching
the application.

The launcher now chooses the monitor with the smallest `Bounds.Left`, records
the device and bounds, places the live R0 window there, and fails closed unless
the actual window intersects that monitor. Current validation selects
`\\.\DISPLAY2` at `[-1920,365,1920,1080]`. Preserve:

- `docs/OPENVISIONLAB_3D_HUMAN_OWNER_R0_EXECUTION_20260729.md`;
- `scripts/start-human-owner-r0.ps1`;
- `D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260806-alignment-status-summary\`.

Inventory remains `139 C / 17 P / 54 N / 9 E / 16 O`; Workspace v3 remains
`7/8` and `A-01` remains Partial. Next dependency: owner performs Wide first,
records the observer sheet, closes the app, then repeats Compact without
coaching. Prerequisite: owner operation and evidence. Recommended model: none;
reasoning effort: none.

### Previous execution checkpoint - Runner help exit - 2026-08-04

`PL-0002` is resolved. Explicit case-insensitive `--help` writes the shared
usage text to stdout and exits `0`. Missing required values and unknown command
combinations retain the same usage text on stderr and exit `2`. The shared
writer prevents help/error usage copies from drifting.

Release builds pass `0/0`; the direct command matrix passes `4/4` with one
identical usage-content SHA-256; existing L-13 Runner regression passes
`19/19`; and structure passes `29/29` with `0` migration debt. No UI, Viewer,
recipe, execution, numerical, or Library-Noah behavior changed. Preserve:

- `docs/OPENVISIONLAB_3D_RUNNER_HELP_EXIT_20260804.md`;
- `.proofline/issues/PL-0002.json`;
- `D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\20260804-pl0002-runner-help\`.

Inventory remains `139 C / 17 P / 54 N / 9 E / 16 O`. No dependency-ready
software item is selected. The next acceptance priority is human-owner
Wide/Compact R0. Prerequisite: owner operation and evidence. Recommended
model: none until evidence exists; reasoning effort: none.

### Previous execution checkpoint - Surface Match pose and score export - 2026-08-04

`L-13` is Complete. Optional Run Record schema `1.6` retains the exact
identified model, Prepared Scene, execution, row-major pose, transformed
overlay, separate surface and edge score components, and separate authored
assessment. Runner accepts saved artifacts and exports JSON/HTML/CSV without
pose search, scoring, or acceptance evaluation. Matched evidence requires
exact score and assessment links; NoMatch exports no invented pose, overlay,
score, or assessment. Schema-`1.5` Run Records remain readable.

Release builds pass `0/0`; focused L-13 passes `19/19`; existing edge
foundation/review pass `21/21` and `20/20`; direct CLI export exits `0`; and
the structure guard passes `29/29` with `0` migration debt. No UI, Viewer
renderer, Library-Noah package, matching result, raw score, or acceptance
decision changed. Preserve:

- `docs/OPENVISIONLAB_3D_SURFACE_MATCH_POSE_SCORE_EXPORT_20260804.md`;
- `D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\20260804-l13-surface-match-export\`.

Inventory is `139 C / 17 P / 54 N / 9 E / 16 O`. Its former PL-0002 next item
is superseded by the current Runner help closure above. Human-owner
Wide/Compact R0 remains external and requires owner operation, not model-token
spend.

### Previous execution checkpoint - Acquisition direction and edge orientation - 2026-08-04

`K-04` is Complete. The optional source contract now retains an explicit
Available/Unavailable `SensorToScene` direction in the exact source frame.
Source Quality authors one normalized direction through the existing explicit
Apply/reset flow. Legacy recipes without direction remain clean and show the
Unavailable fallback.

Committed Library-Noah `9dd95690d3e439b459c39aea99878880cdcc5808`
owns deterministic normal orientation through vendored `Lib.ThreeD 2.9.1`,
SHA-256
`BDE8D2C01B6DC380EF4579C89DE495F06F79BA4864D4229CD5CE87713BD1CA4E`.
Studio owns the content-addressed link to the existing edge overlay and the
display adapter. Missing/unavailable direction, frame mismatch, and hash
tamper fail closed; geometry is never used to infer direction.

Current builds pass `0/0`; Library-Noah Smoke `138/138`; package bridge
`26/26`; direction artifact `5/5`; source contract `17/17`; edge Workbench
parity/stale handling `16/16`; existing edge foundation/review `21/21` and
`20/20`. Current EXE Wide/Compact Source Quality captures pass on the leftmost
monitor with accepted screenshot quality. Applying a changed direction removes
only stale orientation evidence and does not change or rerun the raw overlay,
score, or assessment. Preserve:

- `docs/OPENVISIONLAB_3D_ACQUISITION_DIRECTION_AND_EDGE_ORIENTATION_20260804.md`;
- `D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\20260804-k04-acquisition-direction\`.

Inventory at that checkpoint was `138 C / 17 P / 55 N / 9 E / 16 O`.
Human-owner R0 remains external. Its former L-13 next item is superseded by
the completed current checkpoint above. Camera integration, calibration,
reconstruction, metrology, and weighted-score changes remain out of scope.

### Previous execution checkpoint - Acquisition/source provenance - 2026-08-04

`B-12` is Complete. `ToolRecipeSource` now carries an optional explicit
Available/Unavailable acquisition provenance contract with required evidence
and limitation notes. Source Quality owns a transient draft with explicit
Apply/reset; Workbench owns source-scoped dirty state and exact save/reopen.
Legacy recipes without the field remain readable and clean. Selecting a
different source resets the source-specific contract.

Release builds with `0` warnings and `0` errors. Focused B-12 passes `14/14`;
Source Quality `18/18`; recipe teaching/selections `28/28` and `29/29`;
Inspection Workspace `64/64`; docking `82/82`; Shell command line `33/33`.
Current Release EXE Wide/Compact source-quality smokes and capture quality
pass on the leftmost monitor. Normal, focus, validation-error, enabled,
disabled, and open-popup graphite-theme states were visually checked. No
camera integration, calibration, viewpoint inference, edge orientation, or
automatic Preview/Publish/Run/Validation was added. Preserve:

- `docs/OPENVISIONLAB_3D_ACQUISITION_SOURCE_PROVENANCE_20260804.md`;
- `D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260804-b12-acquisition-provenance\`.

Inventory is `137 C / 17 P / 56 N / 9 E / 16 O`. Human-owner R0 remains
external. Next is `K-04 Acquisition viewpoint/direction metadata for edge
orientation`. Recommended model: `gpt-5.6-sol`; reasoning effort: high. K-04
must consume explicit operator/import evidence and must not infer a viewpoint
from geometry. `L-13` remains independently dependency-ready.

### Previous execution checkpoint - Model key points and debug overlay - 2026-08-03

`J-07` is Complete. Committed Library-Noah owns deterministic farthest-point
selection from the J-05 retained SurfaceModel samples, including the seed,
nearest-selected distance, strict minimum separation, bounded count, and
stable source-order tie. Studio owns stable source-sample/source-triangle
identity, atomic JSON persistence, and a WPF-neutral display-only position/
normal overlay. Neither artifact changes or executes matching.

Committed Noah `7ed50ea37b3d7cb711c2afe698d209f9073e9217` passes Release
`0/0` and Smoke `122/122`. Vendored `Lib.ThreeD 2.8.12` has SHA-256
`7E5DAF887851CB16C45279CD957260C2546AD0EDBB92B9F4903E23E529BADFE3`.
Studio Release Rebuild passes `0/0`; bridge `21/21`; J-07 `15/15`; legacy byte
parity `5/5`; established matching, Workbench, edge, docking, workspace,
Validation Set, command-line, and structure gates pass; both R0
`-ValidateOnly` modes pass. Preserve:

- `docs/OPENVISIONLAB_3D_MODEL_KEY_POINT_ARTIFACT_AND_DEBUG_OVERLAY_20260803.md`;
- `D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260803-j07-model-key-points\`.

Inventory at that checkpoint was `136 C / 17 P / 57 N / 9 E / 16 O`.
Human-owner R0 remains external. Its former next item B-12 is superseded by
the completed current checkpoint above. `K-04` is now dependency-ready;
`L-13` remains independently dependency-ready.

### Previous execution checkpoint - Model surface selection - 2026-08-03

`J-05` is Complete. SurfaceModel schema `1.2` preserves every imported point,
triangle, and normal while identifying one retained source-triangle domain.
Automatic removal is limited to exact-coordinate duplicates; internal and
unobservable roles require explicit source-triangle locators. Matching samples,
model-edge extraction, and transformed overlays consume the same retained
domain. No-selection schema `1.0/1.1` artifacts remain byte-identical.

Committed Noah `55ea7a61bd1281294e91aa5366d2bafb509d3667` passes Release
`0/0` and Smoke `118/118`. Vendored `Lib.ThreeD 2.8.11` has SHA-256
`AC61E132938AD184F3E3A39622A5BC3C4E48F1419D7C4EC75AC604A8CD1F8A42`.
Studio Release Rebuild passes `0/0`; bridge `21/21`; J-05 `15/15`; legacy byte
parity `5/5`; established matching, Workbench, edge, docking, workspace,
Validation Set, command-line, and structure gates pass; both R0
`-ValidateOnly` modes pass. Preserve:

- `docs/OPENVISIONLAB_3D_MODEL_SURFACE_SELECTION_20260803.md`;
- `D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260803-j05-model-surface-selection\`.

Inventory at that checkpoint was `135 C / 17 P / 58 N / 9 E / 16 O`.
Human-owner R0 remains external. Its former next item `J-07` is superseded by
the completed current checkpoint above. `K-04` remains blocked on `B-12`.

### Previous execution checkpoint - Symmetry-aware pose equivalence - 2026-08-03

`J-13` is Complete. Undeclared schema `1.0` and schema `1.1` `none` use direct
rigid-pose comparison. Declared `x`, `y`, or `z` cyclic model-axis symmetry
uses deterministic `reference rotation * symmetry operation` equivalence with
inclusive translation and rotation limits and lowest-operation-index ties.
This is an independent typed evaluator; J-12 matching search, disjoint result
collection, ordering, identity, persistence, and presentation remain
unchanged.

Committed Noah `f225fd2709de1dd1d0ecfe19b37315cb1f019ee4` passes Release
`0/0` and Smoke `113/113`. Vendored `Lib.ThreeD 2.8.10` has SHA-256
`535CD75D33BE5EC015B1B36215FF3DBDD7E8AEC1A5F2B8FFE1FCCBA18B7877C7`.
Studio Release passes `0/0`; bridge `20/20`; J-13 `15/15`; legacy byte parity
`5/5`; existing matching, Workbench, edge, docking, workspace, Validation Set,
command-line, and structure gates pass; both R0 `-ValidateOnly` modes pass.
Preserve:

- `docs/OPENVISIONLAB_3D_SYMMETRY_AWARE_POSE_EQUIVALENCE_20260803.md`;
- `D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260803-j13-symmetry-aware-pose-equivalence\`.

Inventory at that checkpoint was `134 C / 17 P / 59 N / 9 E / 16 O`.
Human-owner R0 remains external. Its former next item `J-05` is superseded by
the completed current checkpoint above. `K-04` remains blocked on `B-12`.

### Previous execution checkpoint - SurfaceModel symmetry declaration - 2026-08-03

`F-13` is Complete. Existing undeclared schema-`1.0` SurfaceModels preserve
their exact canonical identity and JSON bytes. Schema `1.1` requires explicit
`none` or discrete rotation about model axis `x`, `y`, or `z` with order at
least `2`. The declaration participates in canonical identity and persists
through the existing atomic store. It is metadata only in this slice: matching
and pose-equivalence behavior are unchanged.

Release passes `0/0`; focused SurfaceModel verification `34/34`; legacy byte
parity `5/5`; bridge `19/19`; matching `34/34`; acceptance `14/14`;
performance `18/18`; multiple-match Runner/Workbench `14/14` and `10/10`;
edge/review `21/21` and `20/20`; accepted-input single-match parity `23/23`;
docking `82/82`; Inspection Workspace `64/64`; Validation Set `84/84`;
command line `31/31`; structure `29/29`; and both R0 `-ValidateOnly` modes
pass. Preserve:

- `docs/OPENVISIONLAB_3D_SURFACE_MODEL_SYMMETRY_DECLARATION_20260803.md`;
- `D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260803-f13-surface-model-symmetry-declaration\`.

Inventory is `133 C / 17 P / 60 N / 9 E / 16 O`. Human-owner R0 remains
external. Its former next item `J-13` is superseded by the current J-13
closure above.

### Previous execution checkpoint - Multiple-match issue navigation - 2026-08-03

`K-09` is Complete. Non-wrapping Previous/Next commands and the retained-result
selector share the existing `SelectedSurfaceMatchCollectionItem` owner and
Viewer display path. The first result disables Previous and the last disables
Next. The actions do not execute matching, Preview, Publish, Run, or Validation,
mutate recipe/output/candidate state, or persist the viewed result.

Studio Release passes `0/0`; J-12 Runner `14/14`; K-09 Workbench `10/10`;
current-input single-match parity `14/14`; docking `82/82`; Inspection
Workspace `64/64`; Validation Set `84/84`; command line `31/31`; structure
`29/29`; and both R0 `-ValidateOnly` modes pass. Current Release Wide/Compact
English/Korean, focus/hover, first/last disabled, and leftmost-monitor evidence
passes. Preserve:

- `docs/OPENVISIONLAB_3D_MULTIPLE_MATCH_ISSUE_NAVIGATION_20260803.md`;
- `D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260803-k09-multiple-match-issue-navigation\`.

Inventory was `132 C / 17 P / 61 N / 9 E / 16 O` at that checkpoint.
Human-owner R0 remains external. Its former next item `F-13` is superseded by
the current F-13 closure above.

### Previous execution checkpoint - Multiple Surface Match collection - 2026-08-03

`J-12` is Complete. Committed Noah
`4e301f481cac886f78425197314cd540b653473a` owns bounded repeated pose search,
per-result unique-nearest coverage, disjoint scene-sample claiming, stable
ordering, and bounded termination. Vendored `Lib.ThreeD 2.8.9` has SHA-256
`A3B212E6D8AC487DF668F0FE557C17615845A161412AE7AF6BD7FE4FCC260278`.

Studio owns schema-1 collection identity/persistence, authored acceptance,
explicit lifecycle, evidence, and presentation-only retained-match selection.
The controlled two-object fixture retains two ordered `5/5` matches with zero
shared scene claims. Noah Release/Smoke passes `0/0` and `108/108`; Studio
Release `0/0`; bridge `19/19`; J-12 Runner `14/14`; Workbench `6/6`; existing
matching `34/34`; acceptance `14/14`; performance `18/18`; focused edge/model
regressions pass; docking `82/82`; Inspection Workspace `64/64`; Validation
Set `84/84`; command line `30/30`; and structure `29/29` with zero debt and
`31` reviewed boundaries. Current Release Wide/Compact, popup/theme states,
leftmost-monitor evidence, and both R0 `-ValidateOnly` modes pass. Preserve:

- `docs/OPENVISIONLAB_3D_MULTIPLE_SURFACE_MATCH_RESULT_COLLECTION_20260803.md`;
- `D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260803-j12-multiple-match\`.

Inventory was `131 C / 17 P / 62 N / 9 E / 16 O` at that checkpoint.
Human-owner R0 remains external. Its former next item `K-09` is superseded by
the current K-09 closure above.

### Historical product-owner conversation priority - Layout continuation - 2026-08-01

The product owner selected the layout/UI-design stream for the next
conversation. Use
`docs/OPENVISIONLAB_3D_LAYOUT_REDESIGN_CONVERSATION_HANDOFF_20260801.md` as
the active entry point. Begin with a fresh current-build Wide `1920 x 1040`
and Compact `1280 x 760` integrity audit, then select one bounded
evidence-backed operator problem. Preserve all completed Workbench v4, Viewer,
top dock-tab, Authoring integrity/side-collapse, first-use clarity,
presentation-only layout persistence, and theme closures.

This layout-only priority was explicitly superseded when the owner approved
J-12 on 2026-08-03. Human-owner R0 remains an external acceptance task.

### Current execution checkpoint - Validation-statistics Noah migration - 2026-08-01

`ToolRecipeLabeledEvidenceAnalyzer` and
`ToolRecipeThresholdCandidateAnalyzer` are strict adapters over committed
public `LabeledEvidenceStatisticsTool`, `ThresholdCandidateAnalysisTool`, and
the existing `HeightMapRegionStatisticsTool`. Noah owns role-grouped
descriptive statistics, rectangular ROI aggregation, deterministic threshold
candidate construction, classification, error counting, ranking, and
tie-breaking. Studio retains recipe/Tool/parameter/source/sample/role identity,
grouping and routing, HeldOut exclusion, warnings, canonical candidate IDs,
reports, lifecycle, and UI.

Committed Noah `0fe04bc967fa89918b3c6d937566cce56de69682` passes Release
`0/0` and Smoke `106/106`. Vendored `Lib.ThreeD 2.8.8` has SHA-256
`D62B050710C4CCA0309B3FA49CDCDBB239C675944E29C085E50CD198D4D15405`.
Studio Release is `0/0`; bridge `19/19`; Validation Set `84/84`; normalized
before/after full-report differences `0`; and structure `29/29` with `0`
migration-debt files and `30` reviewed boundaries. Both fixed R0
`-ValidateOnly` modes pass. Preserve:

- `docs/OPENVISIONLAB_3D_VALIDATION_STATISTICS_NOAH_MIGRATION_20260801.md`;
- `D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260801-noah-validation-statistics-migration\`.

Inventory remains `127 C / 17 P / 65 N / 9 E / 16 O`; Human-owner R0 remains
external. Immediate software priority: `J-12 Multiple-match result collection
with stable identities`. New matching arithmetic must be implemented in
committed Noah first. Recommended model: `gpt-5.6-sol`; reasoning effort: high.

### Historical checkpoint - Repeatability-statistics Noah migration - 2026-08-01

`AlignedPointRepeatabilityRule` and `ThicknessRepeatabilityRule` are strict
adapters over committed public `RepeatabilityStatisticsTool`. Noah owns
Welford accumulation, scalar mean/extrema, sample standard deviation,
six-sigma, range, and explicit negative-variance round-off policy. Studio
retains study/run/source/correspondence identity, unit/frame/alignment policy,
authored acceptance, per-point aggregation, metrics, messages, and evidence.

Committed Noah `20963c12b50dfc0658110e2037961d3224feb2d6` passes Release
`0/0` and Smoke `101/101`. Vendored `Lib.ThreeD 2.8.7` has SHA-256
`C40A2EB0239C5BF6063984429CEDB580608CD7EF8C96D08AA13A67C2B3ACF33B`.
Studio Release is `0/0`; bridge `17/17`; focused verification `34/34` and
`33/33` with exact full-report parity; loaders `13/13` and `20/20`;
Calibration ViewModel `75/75`; and structure `28/28` with `2` debt files and
`28` reviewed boundaries. Preserve:

- `docs/OPENVISIONLAB_3D_REPEATABILITY_STATISTICS_NOAH_MIGRATION_20260801.md`;
- `artifacts/current/20260801-noah-repeatability-statistics-migration/`.

Inventory remains `127 C / 17 P / 65 N / 9 E / 16 O`; Human-owner R0 remains
external. Its former validation-statistics priority is superseded by the
current checkpoint above.

### Historical checkpoint - Declared-normal quality and Landmark Correspondence Noah migration - 2026-08-01

Committed public `DeclaredMeshNormalQualityTool` owns normal length, mesh
topology, degenerate-triangle, and corner-alignment evidence. Committed public
`LandmarkCorrespondenceValidationTool` owns exactly-four augmented rank and
normalized tetrahedral volume. Studio retains source/format/lineage/recipe
identity, immutable reports/artifacts, canonical hashes, explicit lifecycle,
metrics, overlays, and Viewer presentation.

Committed Noah `3ef2f52546a9187df465bf8973e26426c30f7634` passes Release
`0/0` and Smoke `98/98`. Vendored `Lib.ThreeD 2.8.6` has SHA-256
`02E0D0B69F9D7CECBA958BF4BDC7F2999D0902539C33CD0F133C48C08C3A25B0`.
Studio Release is `0/0`; bridge `16/16`; focused verification `26/26` and
`5/5`; exact normalized parity `2/2`; Source Quality `18/18`; teaching
`28/28`; Inspection Workspace `63/63`; Validation Set `84/84`; loading
matrix `128/128`; and structure `27/27` with `4` debt files and `26` reviewed
boundaries. Preserve:

- `docs/OPENVISIONLAB_3D_DECLARED_NORMAL_QUALITY_AND_LANDMARK_CORRESPONDENCE_NOAH_MIGRATION_20260801.md`;
- `artifacts/current/20260801-noah-normal-quality-landmark-migration/`.

Inventory remains `127 C / 17 P / 65 N / 9 E / 16 O`; Human-owner R0 remains
external. Immediate software priority: migrate `AlignedPointRepeatabilityRule`
and `ThicknessRepeatabilityRule`, then the two validation-statistics analyzers
before `J-12`.

### Earlier execution checkpoint - Dual Surface Thickness and Height Deviation Noah migration - 2026-08-01

Committed public `DualSurfaceThicknessInspectionTool` and
`HeightDeviationInspectionTool` now own plane-relative residuals, thickness
statistics and limit counts, and low/high/peak deviation with typed decisions.
Studio retains source/unit identity, explicit lifecycle, elapsed time,
ToolResult metrics, overlays, and Viewer presentation.

Committed Noah `ec8f1b3db57bea0065cd82735acb08111f88f3c0` passes Release
`0/0` and Smoke `92/92`. Vendored `Lib.ThreeD 2.8.5` has SHA-256
`3BE4E7F83CC4A9E3542C6FCA9C38C5F13D2BFEE703F78035CB9082DC0B5EBCDB`.
Studio Release is `0/0`; bridge `14/14`; Workbench `54/54`; observable parity
`2/2`; Validation Set `84/84`; focused regressions pass; and structure is
`26/26` with `6` debt files and `24` reviewed boundaries. Preserve:

- `docs/OPENVISIONLAB_3D_DUAL_SURFACE_THICKNESS_AND_HEIGHT_DEVIATION_NOAH_MIGRATION_20260801.md`;
- `artifacts/current/20260801-noah-dual-thickness-height-deviation-migration/`.

Inventory remains `127 C / 17 P / 65 N / 9 E / 16 O`; Human-owner R0 remains
external. Immediate software priority: migrate
`ImportedMeshNormalQualityAnalyzer` and `C3DLandmarkCorrespondenceRule`, then
the remaining ledger before `J-12`.

### Earlier execution checkpoint - Height-map inspection and preparation Noah migration - 2026-08-01

Five committed public Library-Noah Tools now own raw height-grid summary and
distribution calculation, rectangular finite-value statistics, Completeness
Grid cell placement/reference-relative metrics/typed decisions, and declared
or reference-axis point reconstruction. Studio retains C3D decoding,
source/unit/frame/recipe identity, canonical hashes, lifecycle routing,
metrics, overlays, reports, and Viewer-only projection.

Committed Noah `a64c31b1024f154e402d258ade4b70470ad50fb2` passes Release
`0/0` and Smoke `86/86`. Vendored `Lib.ThreeD 2.8.4` has SHA-256
`0F4FB2A1115C0247E03BA85D335BE40241FD02A6F5694FE6E36B872CB3A846F5`.
Studio Release is `0/0`; package bridge `12/12`; map fidelity `10/10`;
Source Quality `13/13`; Completeness Grid `23/23`; Height distribution
`25/25`; generic height-measurement Workbench `54/54`; normalized parity
`5/5`; Height Image `25/25`; artifact-owned ROI `18/18`; Validation Set
`84/84`; and structure `25/25`. The ledger is `8` debt files and `22`
reviewed boundaries. Refreshed Wide/Compact fixed inputs pass both
`-ValidateOnly` modes. Preserve:

- `docs/OPENVISIONLAB_3D_HEIGHT_MAP_INSPECTION_PREPARATION_NOAH_MIGRATION_20260801.md`;
- `artifacts/current/20260801-noah-height-map-inspection-preparation-migration/`.

Inventory remains `127 C / 17 P / 65 N / 9 E / 16 O`; the ownership move
does not change backlog classification. Human-owner R0 remains external.
Immediate software priority: migrate `DualSurfaceThicknessRule` and
`HeightDeviationRule` into committed Noah Tools, then continue the remaining
ledger before `J-12`.

### Earlier execution checkpoint - Nominal comparison and transform diagnostics Noah migration - 2026-08-01

Nominal/actual mesh comparison, triangle-distance lookup, and registration
transform diagnostics now strictly adapt three public sealed Library-Noah
Tools. Noah owns BVH/closest-point distance, direct/robust sign recovery,
streaming tolerance/statistical calculation, display sampling, and rigid
matrix diagnostics. Studio retains source/unit/frame/identity validation,
STL/PLY loading, canonical artifacts, authored acceptance, lifecycle,
evidence, and UI.

Committed Noah `4420c40d3179edc7703cfef6e0ea53ac898f8f3f` passes Release
`0/0` and Smoke `81/81`. Vendored `Lib.ThreeD 2.8.3` has SHA-256
`63F70F92354257E6E2975753BC17A76118478CB6AB0C77EB487C09F5A50F0C39`.
Studio Release is `0/0`; package integrity and bridge `7/7` pass; focused
checks pass `23/23`, `29/29`, and `20/20`; all three pre/post reports are
exact; and structure is `24/24` with `12` debt files and `16` reviewed
boundaries. Refreshed Wide/Compact fixed inputs pass both `-ValidateOnly`
modes. Preserve:

- `docs/OPENVISIONLAB_3D_NOMINAL_COMPARISON_AND_TRANSFORM_DIAGNOSTICS_NOAH_MIGRATION_20260801.md`;
- `artifacts/current/20260801-noah-nominal-registration-migration/`.

Inventory remains `127 C / 17 P / 65 N / 9 E / 16 O`; the ownership move
does not change backlog classification. Human-owner R0 remains external.
Its historical height-map priority is superseded by the current checkpoint
above.

### Earlier execution checkpoint - Outlier filtering and leveling Noah migration - 2026-08-01

The active Remove Outlier Pixels and Level Surface rules now strictly adapt
two public sealed Library-Noah Tools. Noah owns center-excluded available-
neighbor selection, deterministic median and strict deviation filtering,
unique finite reference-cell collection, plane/residual statistics, reference-
mean detrending, missing-mask preservation, and output-plane evidence. Studio
retains exact source/ROI identity, authored RMS acceptance, recipe lifecycle,
immutable mask/transform/derived-C3D composition, metrics, and overlays.

Committed Noah `3a2cbf8e7195d6f251dcafe6a9343b795d53fe79` passes Release
`0/0` and Smoke `78/78`. Vendored `Lib.ThreeD 2.8.2` has SHA-256
`EF397381CDD3344E3BAB7A7F29FF6124451DA6A1FCB1BC007B0BFDB284A0BFD7`.
Studio Release is `0/0`; package integrity and bridge pass; Remove Outlier
Pixels and Level Surface goldens pass `9/9` each; their `28` comparable
pre/post lines are exact; both Workbench checks pass; and structure is
`23/23`. Preserve:

- `docs/OPENVISIONLAB_3D_OUTLIER_FILTER_AND_LEVELING_NOAH_MIGRATION_20260801.md`;
- `artifacts/current/20260801-noah-outlier-leveling-migration/`.

Inventory remains `127 C / 17 P / 65 N / 9 E / 16 O`; the ownership move
does not change backlog classification. Human-owner R0 remains external.
Immediate software priority: migrate nominal/actual mesh comparison and rigid
transform diagnostics to committed Noah Tools before `J-12`.

### Earlier execution checkpoint - Surface preparation and edge Noah migration - 2026-08-01

The active matching preparation and edge chain now uses five committed public
Library-Noah Tools. Studio retains source/unit/frame/identity validation,
Source Quality and normal admission, canonical artifacts, acceptance,
lifecycle, evidence, and UI, but no longer calculates the even sample
schedule, triangle centroid/sample normal, boundary/crease geometry,
organized height steps, or edge coverage/RMSE.

Committed Noah `46cfa0946bb4c23190b0dab75415ce2c637b4c41` passes Release
`0/0` and Smoke `75/75`. Vendored `Lib.ThreeD 2.8.1` has SHA-256
`3C908BB6671D2F89C7BC9DDEC601CD10A33A0905D78A8A24A276DA9BAAFF4445`.
Studio Release is `0/0`; package `7/7`; SurfaceModel `22/22`; matching
`34/34`; acceptance `14/14`; performance `18/18`; edge `21/21`; edge review
`20/20`; Workbench parity `14/14`, `12/12`, and `13/13`; structure `22/22`;
and all 24 corresponding pre/post JSON files are byte-identical. Preserve:

- `docs/OPENVISIONLAB_3D_SURFACE_PREPARATION_EDGE_NOAH_MIGRATION_20260801.md`;
- `artifacts/current/20260801-noah-surface-preparation-edge-migration/`.

Inventory remains `127 C / 17 P / 65 N / 9 E / 16 O`; the ownership move
does not change backlog classification. Human-owner R0 remains external.
Its former filtering/leveling priority is superseded by the current checkpoint
above.

### Current architecture checkpoint - Library-Noah Tool contract and migration baseline - 2026-08-01

The owner requires all reusable numerical, geometric, filtering,
feature-extraction, matching, measurement, inspection, and statistical
algorithms to use the Library-Noah public sealed `XxxTool` form with typed
source-neutral input/options/result and explicit `Execute(...)`. The existing
`IThreeDInspectionTool` remains a narrow HeightMap compatibility contract; it
is not forced onto matching, mesh, or multi-input Tools.

The schema-1 decreasing baseline records `8` Studio migration-debt files and
`22` reviewed Studio boundaries. Landmark Correspondence rank and normalized
volume math is now explicit migration debt rather than a numerical exception.
The code-structure verifier passes `25/25`, detects `14` current numerical
owner candidates, and finds no unclassified or expanded Studio owner. The
vendored `Lib.ThreeD 2.8.4` package boundary also passes with exact source
commit and SHA-256 agreement. Preserve:

- `docs/OPENVISIONLAB_3D_NOAH_TOOL_CONTRACT_AND_MIGRATION_BASELINE_20260801.md`;
- `docs/OPENVISIONLAB_3D_NOAH_TOOL_MIGRATION_BASELINE_20260801.json`;
- `artifacts/current/20260801-noah-tool-ownership-contract/`.

Inventory remains `127 C / 17 P / 65 N / 9 E / 16 O`; this architecture guard
does not change a backlog status. Human-owner R0 remains external. Immediate
software priority: continue the remaining baseline with Dual Surface
Thickness and Height Deviation before `J-12`.

### Earlier execution checkpoint - Library-Noah Surface Match kernel migration - 2026-08-01

The deterministic pose-search and one-way unique-nearest coverage arithmetic
is now owned by committed Library-Noah source
`7d1ad8721ca7aed9efa2a17beaa36409d7dbd718` and consumed through vendored
`Lib.ThreeD 2.8.0`, SHA-256
`7378C02ABDED9C02F1448CDF80577B00A7AD99E78BC2B722E341DD7513CE754C`.
Studio retains source/unit/frame/identity validation, strict adapters,
canonical artifacts, acceptance, lifecycle, evidence, and UI only. The known
pose result remains byte-identical at SHA-256
`4D214BA3684162407332A69D95155C7FF7D780CC7C8B277795DB028619408B5F`.

Noah Release passes `0/0` and Smoke `69/69`. Studio Release Rebuild passes
`0/0`; package/bridge `7/7`; matching `34/34`; acceptance `14/14`; edge
`21/21`; edge review `20/20`; SurfaceModel `22/22`; performance `18/18`;
Workbench/Runner parity `23/23`; Inspection Workspace `63/63`; docking
`76/76`; Validation Set `84/84`; structure `18/18`; and NuGet health
`12/0/0`. Refreshed fixed hashes pass both R0 `-ValidateOnly` modes. Preserve:

- `docs/OPENVISIONLAB_3D_SURFACE_MATCH_NOAH_MIGRATION_20260801.md`;
- `artifacts/current/20260801-surface-match-noah-migration/`.

Inventory remains `127 C / 17 P / 65 N / 9 E / 16 O`; this prerequisite did
not change a backlog status. Human-owner R0 remains external for `A-01`. Its
historical next item was `J-12`; the newer Tool-only contract above requires
the active Surface Match preparation/edge migration first. `K-09` remains
blocked on `J-12`; `K-04` remains blocked on `B-12`.

### Earlier execution checkpoint - Surface-match parameter experiment comparison - 2026-08-01

`K-10` is Complete. Selected Tool now retains one immutable Published Surface
Match baseline while one explicit Preview creates a temporary Candidate. The
operator can switch the same Viewer between Published and Candidate without
recipe mutation or execution, then explicitly Publish the exact candidate
without re-running it or discard it. Parameter Apply after Preview makes the
candidate stale, disables Publish, and restores the Published view. Transient
comparison evidence is not saved or restored and reopen performs no match.

K-10 added no matching mathematics. It orchestrates the existing shared
`SurfaceMatchEvaluationExecutor`. At that checkpoint Studio consumed
`Lib.ThreeD [2.7.9]`; the current migration checkpoint above supersedes that
temporary numerical-ownership exception. Release passes
`0/0`; Workbench/Runner parity `23/23`; matching `34/34`; acceptance `14/14`;
isolated performance `18/18`; edge `21/21`; edge review `20/20`; Noah package
`7/7`; docking `76/76`; Inspection Workspace `63/63`; Validation Set `84/84`;
height `25/25`; Artifact Navigator `31/31`; smoke options `28/28`; and
structure `17/17`. Current-build Wide, Compact, Korean Compact, and focus/hover
captures pass on the dynamically selected leftmost monitor. Preserve:

- `docs/OPENVISIONLAB_3D_SURFACE_MATCH_PARAMETER_EXPERIMENT_COMPARISON_20260801.md`;
- `artifacts/current/20260731-surface-match-experiment-comparison/`.

Inventory is `127 C / 17 P / 65 N / 9 E / 16 O`. Human-owner R0 remains an
external `A-01` task, and refreshed fixed hashes pass both `-ValidateOnly`
modes. The later Library-Noah migration above supersedes this checkpoint's
historical next prerequisite.

### Earlier execution checkpoint - Surface-match performance budget - 2026-07-31

`K-11` is Complete. The Release Runner owns a fixed 256-sample matching matrix
with `10` warm-ups and `25` measured executions for `11`-candidate and
`61`-candidate profiles. It records outer min/median/p95/max and the existing
three internal stages while requiring exact repeated pose, decision, coverage,
RMSE, candidate count, and execution/assessment identities.

Observed median/p95/max is `11.344/17.098/17.846 ms` bounded and
`34.849/73.611/73.629 ms` broad, within the fixed `40/80/150 ms` and
`180/350/700 ms` ceilings. Release passes `0/0`; performance `18/18`;
matching `34/34`; acceptance `14/14`; edge diagnostic/review `20/20`; and
structure `17/17`. The refreshed fixed package passes both R0 `-ValidateOnly`
modes. Preserve:

- `docs/OPENVISIONLAB_3D_SURFACE_MATCH_PERFORMANCE_BUDGET_20260731.md`;
- `artifacts/current/20260731-surface-match-performance-budget/`.

Inventory is `126 C / 17 P / 66 N / 9 E / 16 O`. Human-owner R0 remains an
external `A-01` task. Next: `K-10` matching parameter experiment comparison
with explicit Publish. `M-17` remains open for the combined full-size Height
Image/matching matrix; no cross-hardware, production-performance, metrology,
or human-usability claim is included.

### Earlier execution checkpoint - Surface-edge diagnostics, thresholds, and review - 2026-07-31

`K-05`, `K-07`, and `K-08` are Complete. Core owns schema-1 identified
direction-overlay, independent surface/edge assessment, and retained
accepted/rejected review artifacts. Data owns validated atomic persistence.
Tools uses canonical model-edge ordering and declared normals at the immutable
surface pose, evaluates surface and edge components independently without a
weighted score, and retains exact evidence references.

The controlled accepted and rejected cases both retain `2/2 = 1.0` surface
coverage while edge coverage separates to `4/4 = 1.0` Pass and `0/4 = 0.0`
Fail. PropertyGrid authors and persists four independent limits without
executing Preview, Publish, Run, or Validation. Viewer draws model, scene, and
normal diagnostics above the base wireframe and links the current decision to
the retained false-positive comparison.

Release passes `0/0`; focused verification `20/20`; Workbench/Runner and
PropertyGrid parity `13/13`; existing edge `21/21`; matching `34/34`;
acceptance `14/14`; SurfaceModel `22/22`; source/normal `26/26`; Source
Quality `18/18`; docking `76/76`; Inspection Workspace `63/63`; Validation
Set `84/84`; height distribution `25/25`; WPG `38/38`; smoke options `26/26`;
structure `17/17`. Accepted and rejected current-build captures pass Wide
`1920 x 1040` and Compact `1280 x 760` overlap/clipping review. Preserve:

- `docs/OPENVISIONLAB_3D_SURFACE_EDGE_DIAGNOSTICS_THRESHOLDS_AND_REVIEW_20260731.md`;
- `artifacts/current/20260731-surface-edge-diagnostic-review/`.

Inventory is `125 C / 17 P / 67 N / 9 E / 16 O`. Human-owner R0 remains an
external `A-01` task. Next: `K-11` fixed-fixture matching performance gate.
`K-04` remains blocked on `B-12`, `K-09` remains blocked on `J-12`, and no
acquisition-direction, weighted-score, metrology, or production-performance
claim is included in this closure.

### Earlier execution checkpoint - Surface-edge artifacts and separate score - 2026-07-31

`K-02`, `K-03`, and `K-06` are Complete. Core owns schema-1 identified
model-edge, complete-organized-scene-edge, and separate surface/edge score
artifacts with fail-closed canonical identity and execution linkage. Data owns
atomic validated JSON persistence. Tools owns deterministic topology
boundary/crease extraction, organized adjacent-cell height-step extraction,
and stable unique-nearest positional edge scoring at the immutable surface
pose.

The controlled raised-square and flat-background scenes both preserve
`2/2 = 1.0` surface coverage. Their edge evidence separates to
`4/4 = 1.0`, RMSE `0 mm`, versus `0/4 = 0.0`, RMSE unavailable. Incomplete
organized grids, non-manifold topology, mismatched input identities, tampered
content, and score/execution disagreement fail closed. Viewer evidence keeps
Surface coverage/RMSE and 3D-edge score/RMSE distinct and explicitly labels
edge evidence as diagnostic, not Pass/Fail policy.

Release passes `0/0`; focused edge verification `21/21`; edge
Workbench/Runner parity `12/12`; matching `34/34`; acceptance `14/14`;
SurfaceModel `22/22`; source/normal `26/26`; Source Quality `18/18`; docking
`76/76`; Inspection Workspace `63/63`; Validation Set `84/84`; height
distribution `25/25`; WPG `38/38`; smoke options `26/26`; structure `17/17`.
Current-build Wide `1920 x 1040` and Compact `1280 x 760` captures pass the
explicit overlap/clipping review. Preserve:

- `docs/OPENVISIONLAB_3D_SURFACE_EDGE_ARTIFACTS_AND_SEPARATE_SCORE_20260731.md`;
- `artifacts/current/20260731-surface-edge-score/`.

Inventory is `122 C / 17 P / 70 N / 9 E / 16 O`. Human-owner R0 remains an
external `A-01` task. Next: `K-05/K-07/K-08` edge diagnostic overlay,
independent score thresholds, and false-positive review. `K-04` remains
blocked on `B-12`; do not merge the diagnostic edge score into the authored
surface acceptance policy.

### Earlier execution checkpoint - Surface-match acceptance, bounds, and goldens - 2026-07-31

`F-14`, `J-11`, `J-14`, `J-15`, and `M-16` are Complete. Core owns the
schema-1 identified acceptance policy and assessment, source-independent
finite pose-search validation, typed decision/reason, and observational
three-stage runtime report. Data owns fail-closed atomic assessment and
runtime persistence. Tools owns the shared raw-match then separate-acceptance
execution boundary used by Runner and Workbench.

The `Surface Match` typed PropertyGrid separates acceptance limits from
rotation/translation/search controls. Apply edits the recipe only, save and
reopen restore the authored values without execution, and the Viewer shows
raw state/coverage/RMSE beside the distinct decision, exact limits, reason,
timing, and identities. Timing is excluded from deterministic hashes and is
not a performance budget.

The controlled goldens prove known-pose Pass, controlled-occlusion Fail, and
out-of-domain Rejected with exact policy/assessment identities. Release
passes `0/0`; acceptance passes `14/14`; matching passes `34/34`; parity
passes `16/16`; SurfaceModel passes `22/22`; source/normal passes `26/26`;
Source Quality passes `18/18`; Workbench docking passes `76/76`; Inspection
Workspace passes `63/63`; Validation Set passes `84/84`; height distribution
passes `25/25`; smoke options pass `25/25`; and structure passes `17/17`.
Final current-build Wide `1920 x 1040` and Compact `1280 x 760` expanded
parameter captures pass the explicit overlap/clipping review. Preserve:

- `docs/OPENVISIONLAB_3D_SURFACE_MATCH_ACCEPTANCE_BOUNDS_AND_GOLDENS_20260731.md`;
- `artifacts/current/20260731-surface-match-acceptance-bounds-goldens/`.

Inventory is `119 C / 17 P / 73 N / 9 E / 16 O`. The refreshed R0
fixed-hash package passes Wide/Compact `-ValidateOnly`; human-owner R0 remains
an external `A-01` task. The next dependency-ready software slice is
`K-02/K-03/K-06` identified model/scene 3D-edge artifacts and separate
surface/edge scores. Do not turn observational timing into a performance
claim and do not merge the two score channels.

### Earlier execution checkpoint - Surface-match overlay and parity - 2026-07-31

`J-10` and `J-16` are Complete. Core owns the schema-1 identified
transformed-model overlay and decision-free execution artifact. Data owns
validated atomic JSON save/load. Tools owns the shared deterministic executor
used by Runner and Workbench. Workbench owns evidence selection and explicit
display/clear routing; Viewer owns display-frame mapping and renders neutral
scene samples, the complete transformed SurfaceModel wireframe, raw
correspondences, and compact coverage/RMSE/pose/hash evidence.

The controlled fixture recovers the documented `30 degree` yaw and
`(10, -4, 2) mm` translation with `5/5 = 1.0` coverage. Runner and Workbench
match exactly on pose, coverage, overlay, and execution hashes. Version 1
still does not define Pass/Fail limits, authored search UI, timing budgets,
multiple matches, symmetry, or metrology. Release passes `0/0`; matching
passes `34/34`; parity passes `10/10`; SurfaceModel regression passes
`22/22`; source/normal regression passes `26/26`; Source Quality regression
passes `18/18`; Workbench docking passes `76/76`; Inspection Workspace passes
`63/63`; height distribution passes `25/25`; and structure passes `17/17`.
Current-build Wide `1920 x 1040` and Compact `1280 x 760` captures pass the
overlap/clipping review. Preserve:

- `docs/OPENVISIONLAB_3D_SURFACE_MATCH_OVERLAY_AND_PARITY_20260731.md`;
- `artifacts/current/20260731-surface-match-overlay-parity/`.

Inventory is `114 C / 17 P / 78 N / 9 E / 16 O`. The refreshed R0
fixed-hash package passes Wide/Compact `-ValidateOnly`; human-owner R0 remains
an external `A-01` task. The next dependency-ready software slice is
`J-11/J-14/J-15/M-16` acceptance limits, authored search bounds,
rejection/timing evidence, and matching goldens. Do not merge acceptance
policy into the raw score or overlay contracts.

### Earlier execution checkpoint - Prepared Scene, rigid pose, and coverage - 2026-07-31

`J-06`, `J-08`, and `J-09` are Complete. Core owns the schema-1 identified
Prepared Scene, canonical Source Quality and scene identities, rigid
model-to-scene pose/result contract, and explicit one-way coverage evidence.
Data owns fail-closed atomic Prepared Scene JSON save/load. Tools owns pure
scene preparation, bounded deterministic Euler/centroid pose search, and
decision-free unique-nearest surface coverage. Runner owns the known-pose,
occluded-scene, invalid-input, persistence, and repeatability fixtures.

Version 1 does not define Pass/Fail limits, transformed-model Viewer overlay,
Workbench/Runner parity, timing limits, multiple matches, symmetry, or
metrology. Release passes `0/0`; matching verification passes `28/28`;
SurfaceModel regression passes `22/22`; source/normal regression passes
`26/26`; Source Quality regression passes `18/18`; and structure passes
`17/17`. Preserve:

- `docs/OPENVISIONLAB_3D_PREPARED_SCENE_RIGID_POSE_AND_COVERAGE_20260731.md`;
- `artifacts/current/20260731-surface-matching-foundation/`.

Inventory is `112 C / 17 P / 80 N / 9 E / 16 O`. The refreshed R0 fixed-hash
package passes Wide/Compact `-ValidateOnly`; human-owner R0 remains an
external `A-01` task. The next dependency-ready software slice is
`J-10/J-16` transformed-model Viewer evidence and Workbench/Runner pose,
coverage, overlay, and hash parity.

### Earlier execution checkpoint - SurfaceModel preparation - 2026-07-31

`J-01`, `J-03`, and `J-04` are Complete. Core owns the schema-1 identified
`SurfaceModel`, canonical hash, deterministic triangle schedule, and typed
validity report. Data owns fail-closed atomic JSON save/load. Tools converts
an imported mesh into a full-geometry model plus deterministic
triangle-centroid samples only after the existing `B-16` declared-normal
contract passes. Runner owns known-valid and invalid closure fixtures.

Version 1 does not remove geometry, repair input, search a pose, define a
match score, or add UI. Release passes `0/0`; SurfaceModel verification passes
`22/22`; existing source/normal verification passes `26/26`; Source Quality
passes `18/18`; and structure passes `17/17`. Preserve:

- `docs/OPENVISIONLAB_3D_SURFACE_MODEL_PREPARATION_FOUNDATION_20260731.md`;
- `artifacts/current/20260731-surface-model-foundation/`.

At this checkpoint inventory was `109 C / 17 P / 83 N / 9 E / 16 O`.
Human-owner R0 remained an external `A-01` task and `J-06/J-08/J-09` was
next. The newer Prepared Scene, rigid pose, and coverage checkpoint above
supersedes that inventory and priority.

### Current execution checkpoint - source channels and dense normals - 2026-07-31

`B-11` and `B-16` are Complete. The shared catalog reports exactly Height,
Intensity, Color, Depth, Normal, Confidence, and SNR for C3D, GLB/STL, and
LAS/LAZ. Unsupported channels remain unavailable with a source-specific
reason. Viewer colors and calculated face normals never become source data.

GLB and STL loaders preserve declared normals, including partial presence;
LAS/LAZ sampled points retain intensity and RGB follows the declared LAS point
format. The WPF-neutral schema-1 dense-normal report fails closed for missing,
partial, non-finite, zero, non-unit, reversed, invalid-index, incomplete-index,
and degenerate inputs.

Release passes `0/0`; focused source/normal verification passes `26/26`,
Source Quality passes `18/18`, the data-loading matrix passes `128/128`, and
structure passes `17/17`. Preserve:

- `docs/OPENVISIONLAB_3D_SOURCE_CHANNEL_AND_DENSE_NORMAL_QUALITY_20260731.md`;
- `artifacts/current/20260731-source-channel-normal-quality/`.

At this checkpoint inventory was `106 C / 17 P / 86 N / 9 E / 16 O` and
`J-01/J-03/J-04 SurfaceModel` was next. The newer SurfaceModel checkpoint
above supersedes that inventory and priority.

### Current execution checkpoint - GoPxL-inspired Workbench v4 - 2026-07-30

The Shell now owns one 56-pixel Job Bar and one responsive left
responsibility rail. Workbench and Teach internal modes compose the same
visible Authoring cockpit. Wide orders Tool Library/Recipe Chain, Selected
Tool, and dominant Viewer/Displayed Outputs; Compact uses a 60-pixel icon
rail and one support tab group beside the dominant Viewer. Selected Tool owns
explicit Preview, Publish, Cancel, and Save actions.

Validate and Results now compose their evidence beside the same Viewer.
Staged-sample selection is presentation-only, Results remains read-only, and
all execution/correction actions stay explicit. The graphite role system and
schema-1 layout profile persist only allowlisted presentation state, validate
restore values, fail safely for corrupt/incompatible input, and provide an
explicit reset.

Release passes `0/0`, Workbench docking `71/71`, Inspection Workspace
`63/63`, Validation Set `84/84`, Height distribution `25` checks, and
structure `17/17`. Application-only Wide `1920 x 1040` and Compact
`1280 x 760` Validate/Results captures pass. Layout reopen passes
Missing -> Restored and corrupt fallback without execution.

Preserve:

- `docs/OPENVISIONLAB_3D_GOPXL_WORKBENCH_V4_LAYOUT_CONTRACT_20260730.md`;
- `docs/OPENVISIONLAB_3D_GOPXL_WORKBENCH_V4_EVIDENCE_AND_SAFE_LAYOUT_20260730.md`;
- `artifacts/current/20260730-gopxl-workbench-v4-evidence-and-layout/`.

Inventory remains `104 C / 17 P / 88 N / 9 E / 16 O`. The refreshed fixed
R0 hash package passes Wide/Compact `-ValidateOnly`. Immediate: the human
owner's unaided Wide/Compact R0. SurfaceModel remains paused until it passes.

### Current execution checkpoint - Viewer single row and Height color range - 2026-07-30

The normal loaded Single Viewer no longer stacks a source-ready command row,
an `A / Main` pane title, and a Viewer status text row above the model. The
Shell layout commands now share the Viewer's single top row, and the
persistent left measurement HUD is removed while the orientation gizmo
remains.

The right Height legend owns a display-only minimum and maximum interval with
decrement, direct value, increment, and AUTO controls. Manual bounds clamp
outside colors and linearly remap values inside the interval. AUTO restores
the source bounds; the histogram continues to show the full source
distribution. Source, ROI, measurement, recipe, threshold, Preview, Publish,
Run, and routing state are unchanged.

Preserve:

- `docs/OPENVISIONLAB_3D_VIEWER_SINGLE_ROW_AND_HEIGHT_COLOR_RANGE_20260730.md`;
- `artifacts/current/20260730-viewer-single-row-height-range/`.

Release build passes `0/0`, height distribution `25/25`, Inspection Workspace
`63/63`, Workbench docking `59/59`, Validation Set `84/84`, and structure
`17/17`. Current application-only Wide, Compact, and manual-range captures
pass on the first quality attempt.

Inventory remains `104 C / 17 P / 88 N / 9 E / 16 O`; `A-01` remains
`Partial` only for a fresh human-owner unaided R0 on this updated binary set.
After owner R0 passes, begin `J-01/J-03/J-04 SurfaceModel`.

### Current execution checkpoint - Validation top dock tabs - 2026-07-30

The multi-pane AvalonDock work-surface strip now appears above Validate
content instead of at the bottom window edge. It uses the shared OpenVision
Command Bar, Divider, Selected Surface, Accent, Focus, and Disabled tokens.
Multi-item panes no longer duplicate the active title in a second dark header;
single-item panes retain their normal title.

All eight visible TabItems expose localized titles and stable ContentIds.
Actual UI Automation and pointer evidence finds the eight top tabs and selects
Output Compare. Compact keeps every tab on one row.

Preserve:

- `docs/OPENVISIONLAB_3D_VALIDATION_TOP_DOCK_TABS_20260730.md`;
- `artifacts/current/20260730-validation-top-tabs/`.

Release build passes `0/0`, Workbench docking passes `59/59`, Validation Set
passes `84/84`, and actual application-only captures pass Wide
`1920 x 1040` and Compact `1280 x 760`.

Inventory remains `104 C / 17 P / 88 N / 9 E / 16 O`; `A-01` remains
`Partial` only for a fresh human-owner unaided R0 on this updated binary set.
After owner R0 passes, begin `J-01/J-03/J-04 SurfaceModel`.

### Current execution checkpoint - novice hierarchy and accessibility - 2026-07-29

Failure Analysis now leads with failed sample, failed rule, reason, and next
action before the detailed sample, step, metric, and overlay evidence. Results
leads with the decision, executed-step summary, and a keyboard-focusable Fix
in Teach route before Run Record sidecars, paths, reports, export, and
Advanced.

The contextual sample-set action now has one stable owner in the stage
navigation surface. Current Release Wide and Compact actual-pointer timelines
find `ValidationSetRunAllButton` directly by AutomationId and localized name;
the historical coordinate fallback is absent.

Preserve:

- `docs/OPENVISIONLAB_3D_NOVICE_INFORMATION_HIERARCHY_AND_ACCESSIBILITY_20260729.md`;
- `artifacts/current/20260729-novice-hierarchy-accessibility/before/`;
- `artifacts/current/20260729-novice-hierarchy-accessibility/final/`.

Release build passes `0/0`, Workbench docking passes `58/58`, Validation Set
passes `84/84`, and final media passes Wide `1920 x 1040` and Compact
`1280 x 760`, 15 fps, 110 s. Both layouts preserve
`3 Pass / 2 Fail / 0 Error`, Advanced geometry, and final failure evidence.

Inventory remains `104 C / 17 P / 88 N / 9 E / 16 O`; `A-01` remains
`Partial` only for the human-owner unaided R0. Do not repeat the automated
route while current evidence remains valid. After owner R0 passes, begin
`J-01/J-03/J-04 SurfaceModel`.

### Earlier execution checkpoint - Advanced Viewer reactivation - 2026-07-29

The current Release now explicitly releases the main Viewer from the nested
Teach host and reactivates both the Advanced workspace dependency property
and its live AvalonDock presenter. A post-layout visible-frame request
restores the C3D surface, ROI, Viewer controls, and HUD.

The Wide and Compact actual-pointer replay now rejects off-screen or
zero-sized Automation matches and requires visible Advanced and final Failure
Analysis postconditions. Both layouts execute the five-sample set with
`3 Pass / 2 Fail / 0 Error`, render Advanced geometry, and return to preserved
failure evidence.

Preserve:

- `docs/OPENVISIONLAB_3D_ADVANCED_VIEWER_REACTIVATION_20260729.md`;
- `artifacts/current/20260729-advanced-viewer-reactivation/`.

Release build passes `0/0`; Workbench docking passes `55/55`; media
verification passes Wide `1920 x 1040` and Compact `1280 x 760`, 15 fps,
110 s. Inventory remains `104 C / 17 P / 88 N / 9 E / 16 O`; `A-01`
remains `Partial`.

The historical P1 hierarchy and accessibility slice is complete in the
current checkpoint above.

### Earlier execution checkpoint - direct novice full-route repeat - 2026-07-29

Fresh current Release application-only videos repeat the full Wide and
Compact novice route with actual pointer clicks:

```text
5-sample Run -> 3 Pass / 2 Fail / 0 Error
-> Failure Analysis -> Fix in Teach
-> Results -> Advanced -> Results -> Validate
```

The previous Teach correction remains valid: both layouts render the source,
ROI, selected `Completeness Grid` step, and failed-sample correction card.
The wider route is `Incomplete` because Advanced renders a dark empty
`3D 검사 보기` pane in both layouts. The contextual sample-set command also
cannot be found by its expected AutomationId or accessible name and requires
a layout-derived pointer fallback. Compact visibly restores final Failure
Analysis. Wide's final click occurred inside the recorded interval, but the
historical harness did not assert or retain a post-click visible state, so
Wide final preservation is unproven rather than failed.

Preserve:

- `docs/OPENVISIONLAB_3D_DIRECT_NOVICE_REPEAT_ANALYSIS_20260729.md`;
- `artifacts/current/20260729-direct-novice-r0-repeat/`.

Release build passes `0/0`; media verification passes Wide
`1920 x 1040` / 68 s and Compact `1280 x 760` / 68 s at 15 fps. The
authoritative inventory remains `104 C / 17 P / 88 N / 9 E / 16 O`;
`A-01` remains `Partial`. This historical blocker is superseded by the
Advanced Viewer reactivation checkpoint above.

### Earlier execution checkpoint - Teach failure correction closure - 2026-07-29

The current Release now completes the simulated-novice
`Validation -> Failure Analysis -> Fix in Teach` software route. Teach
reattaches and renders the identified `completeness-taught.C3D` source and
ROI after stage recomposition. A read-only correction card carries the failed
sample, rule, reason, and exact failed/passed-cell summary.

Compact uses a focused Selected Tool composition during failure correction,
so the operator does not need to find a small tab. Leaving Teach restores the
normal Recipe Chain/Selected Tool ownership. The route does not invoke
Preview, Publish, Run, or mutate recipe semantics.

Preserve:

- `docs/OPENVISIONLAB_3D_DIRECT_NOVICE_REPLAY_FINDINGS_20260729.md`;
- `docs/OPENVISIONLAB_3D_TEACH_FAILURE_CORRECTION_CONTEXT_20260729.md`;
- `artifacts/current/20260729-direct-novice-r0-replay/`;
- `artifacts/current/20260729-teach-failure-correction/`.

The authoritative inventory remains
`104 C / 17 P / 88 N / 9 E / 16 O`; `A-01` remains `Partial` only because
the human owner's unaided R0 is external. Release build passes `0/0`,
Workbench docking passes `54/54`, and current app-only actual-pointer videos
pass Wide `1920 x 1040` / 42 s and Compact `1280 x 760` / 44 s at 15 fps.
SurfaceModel remains gated until owner R0 passes.

### Earlier execution checkpoint - IA-4b automated owner path - 2026-07-29

Current Release Wide and Compact application-only videos now execute the
controlled five-sample Completeness set and expose
`3 Pass / 2 Fail / 0 Error`. The selected failure opens its owning
`step.validation.completeness` in Teach. Results shows the supplied one-step
Fail Run Record, Advanced opens, and returning through Results to Validation
preserves the recipe, source, selected step, saved/dirty state, Validation
summary, and Run Record without starting hidden Preview or Run.

The initial replay discovered that the visible `Fix in Teach` button lost its
Shell command owner after dock recomposition. The hosted Validation view now
receives an explicit `RunRecordContext` binding from
`ToolRecipeWorkbenchView`. Release build passes `0/0`, the combined
Window-hosted integration/state-preservation verifier passes `52/52`, and
the accepted videos are `1920 x 1040` and `1280 x 760`, 15 fps, 72 seconds.

Preserve:

- `docs/OPENVISIONLAB_3D_IA4B_OWNER_PATH_REPLAY_20260729.md`;
- `artifacts/current/20260729-ia4b-owner-path-replay/`.

The automated IA-4b software gate is complete. `A-01` remains `Partial` and
the authoritative inventory remains `104 C / 17 P / 88 N / 9 E / 16 O`
until the human owner completes the documented unaided Wide/Compact R0
checklist. Do not repeat automated implementation work while that external
evidence is unavailable. `J-01/J-03/J-04 SurfaceModel` begins only after
owner R0 passes.

### Earlier execution checkpoint - IA-4a live stage-host repair - 2026-07-29

Every dynamically recomposed stage view now owns an explicit stable Shell or
Workbench context. The actual Release Wide and Compact replay restores Teach
Selected Tool content, five named Validate sections with five Pending
`2 Good / 2 Bad / 1 Held-out` rows, three named Results sections with the
supplied one-step Fail Run Record, and a visible Advanced transition.
Validate's local action is now `샘플 세트 실행` / `Run sample set`, distinct
from global recipe Run All.

The Workbench verification now hosts the view in a real off-screen WPF
Window and fails on stage-host owner loss, empty localized/accessibility
navigation, incorrect Validation Set row count, unavailable sample-set
command, or disconnected Advanced command. Release build passes `0/0` and
the focused integration check passes `48/48`. `A-10` returns to `Complete`;
`A-01` remains `Partial` until IA-4b and human-owner R0. The authoritative
inventory is `104 C / 17 P / 88 N / 9 E / 16 O`.

Preserve:

- `docs/OPENVISIONLAB_3D_STAGE_HOST_INTEGRATION_REPAIR_20260729.md`;
- `artifacts/current/20260729-stage-host-integration-repair/`;
- the historical before evidence under
  `artifacts/current/20260729-novice-stage-navigation-video-review/`.

At this earlier checkpoint, IA-4b still had to execute the sample set, open a
failure in Teach, and prove
Results -> Advanced -> Results state preservation, and complete the owner's
unaided Wide/Compact R0. SurfaceModel remains gated behind that acceptance.

### Superseded execution checkpoint - IA-4 novice actual-Release replay blocker - 2026-07-29

The application-only Wide and Compact video replay reaches all five
top-level stages, but the live dock recomposition loses the context required
by Teach Selected Tool, Validate, and Results. Validate renders five
unlabeled radio circles instead of the saved `2 Good / 2 Bad / 1 Held-out`
sample set and leaves Run All disabled. Results renders three unlabeled radio
circles instead of the supplied one-step Fail Run Record, and its enabled
Advanced gear produces no visible transition. The same controls expose empty
accessible names.

The prior IA-2/IA-3 structural checks and generated View captures did not
assert live MainWindow child context, non-empty localized labels, loaded row
counts, command readiness, or visible Advanced navigation. `A-01` stays
`Partial`; `A-10` returns from `Complete` to `Partial`. The authoritative
inventory is therefore `103 C / 18 P / 88 N / 9 E / 16 O`.

Preserve:

- `docs/OPENVISIONLAB_3D_NOVICE_STAGE_NAVIGATION_VIDEO_REVIEW_20260729.md`;
- `artifacts/current/20260729-novice-stage-navigation-video-review/`;
- `scripts/run-novice-stage-navigation-video-review.ps1`.

Immediate: repair stable stage-host ownership and add actual MainWindow
integration assertions, then repeat the Wide/Compact simulated-novice replay
and the owner's unaided R0. Do not begin SurfaceModel until this gate passes
or the owner explicitly reprioritizes.

### Superseded execution checkpoint - IA-3 dedicated Results workspace - 2026-07-29

IA-3 structure exists in current source. Results is one full-height
read-only workspace with local Run Record, Output Compare, and Reports/export
sections. It no longer combines the Viewer with a compressed lower record and
no longer exposes Save or teaching/validation mutation commands. Existing
expert docks remain available only through the explicit Advanced/Tool Labs
route.

Stage/local/Advanced navigation preserves recipe identity, selected-step
identity, step count, dirty state, current Viewer output summary, and Run
Snapshot summary. Current Release evidence passes build `0/0`,
docking/stage/non-mutation `47/47`, Run Record `10/10`, Artifact Navigator
`31/31`, Shell options `24/24`, structure `17/17`, and current
Wide/Compact/section capture quality.

The newer IA-4 actual-Release checkpoint above invalidates the live
integration closure claim. Preserve the prior implementation evidence:

- `docs/OPENVISIONLAB_3D_DEDICATED_RESULTS_WORKSPACE_20260729.md`;
- `artifacts/current/20260729-results-workspace-extraction/`.

`IA-4a` live stage-host integration repair is next. SurfaceModel
`J-01/J-03/J-04` remains the next functional train only after IA-4 and owner
R0.

### Historical execution checkpoint - IA-2 dedicated Validate workspace - 2026-07-29

`IA-2 / A-10` is complete in current Release source. Validate is now the only
full-height task surface and no longer combines a dominant Viewer with a
compressed lower Validation Set. Five local drill-down sections own Samples,
Run Results, Failure Analysis, Threshold Review, and Held-out evidence.
Failure-to-Teach navigation selects the existing owning step without changing
or executing the recipe. Results retains its Viewer plus Run Record
composition.

The implementation reuses the existing deterministic Validation Set,
candidate/error table, correction, and Held-out replay contracts. It does not
rewrite Runner logic or change Held-out exclusion. Current Release evidence
passes build `0/0`, docking/stage `44/44`, Validation Set `84/84`, Inspection
Workspace `63/63`, teaching `28/28`, Artifact Navigator `31/31`, Shell options
`24/24`, structure `17/17`, and current Wide/Compact capture quality.

`A-10` moves from `Partial` to `Complete`. `A-01` remains `Partial` until
Results/Advanced extraction and owner replay close. Inventory is now
`104 C / 17 P / 88 N / 9 E / 16 O`. Preserve:

- `docs/OPENVISIONLAB_3D_DEDICATED_VALIDATE_WORKSPACE_20260729.md`;
- `artifacts/current/20260729-validate-workspace-extraction/`.

The newer IA-3 checkpoint above supersedes this historical next priority.

### Historical execution checkpoint - IA-1 Setup/Teach separation - 2026-07-29

The owner rejected the current all-in-one default Workspace. Tool composition,
selected-step teaching, Viewer interaction, Validation Set/threshold evidence,
and Run Record review are valid capabilities, but they must not permanently
compete on one screen.

The approved design defines real top stages:

```text
Setup -> Teach -> Validate -> Results
```

`IA-1` is now complete in current Release source. Setup owns Tool Library and
the full Recipe Chain without Viewer or lower evidence. Teach owns the compact
step rail, dominant Viewer, and Selected Tool without Tool Library or lower
evidence. Wide and Compact compositions are distinct. Navigation preserves
recipe/source/selection state, never executes, and is guarded by active ROI,
PropertyGrid, Preview, and Validation work.

`Calibration` remains independent and Advanced diagnostics remain opt-in.
At this IA-1 checkpoint, `A-01` remained `Partial` because dedicated
Validate/Results extraction and the new owner replay were still open.
Inventory was
`103 C / 18 P / 88 N / 9 E / 16 O`. Preserve:

- `docs/OPENVISIONLAB_3D_WORKSPACE_INFORMATION_ARCHITECTURE_REDESIGN_20260729.md`;
- `docs/OPENVISIONLAB_3D_SETUP_TEACH_WORKSPACE_SEPARATION_20260729.md`;
- `artifacts/current/20260729-workspace-information-architecture/`.

The historical next item was `IA-2 / A-10`; the newer checkpoint above closes
it.

### Current execution checkpoint - 2026-07-29

`H-11/H-12/I-14 Completeness Validation Set and threshold assistance` is
complete. One controlled recipe replays two Good, two Bad, and one Held-out
sample with real `Pass/Fail/Pass` evidence. The threshold analyzer derives
one policy-equivalent worst-cell observation per sample for minimum finite
coverage, minimum reference-relative mean, and maximum reference-relative
mean. Shared report contract `2.1` carries the exact `r###.c###` cell
locator into every candidate decision. Held-out remains excluded from
candidate boundaries, ranking, counts, and decisions. Three fail-closed
mappings target only the existing Completeness policy parameters.
Review/Cancel are non-mutating; candidate Apply changes the PropertyGrid
draft only; an explicit development-only replay gates the separate Held-out
replay. Current Release evidence passes build `0/0`, Validation Set `82/82`,
Completeness golden `23/23`, Inspection Workspace `63/63`,
Recipe Manager/PropertyGrid `37/37`, docking `33/33`, Shell options `24/24`,
structure `17/17`, Runner schema `1.1`/threshold contract `2.1` with
`57` candidates, `4` development samples, `1` Held-out excluded,
`0` warnings, and `8` mappings, plus current Wide/Compact capture quality.
Preserve
`docs/OPENVISIONLAB_3D_COMPLETENESS_VALIDATION_AND_THRESHOLD_ASSISTANCE_20260729.md`
and
`artifacts/current/20260729-completeness-threshold-assistance/`.
At that closure `J-01/J-03/J-04 SurfaceModel preparation foundation` was
next. The newer information-architecture checkpoint above supersedes the
immediate priority and pauses SurfaceModel behind IA-1.

`H-08/H-10 completeness failure navigation and repeated-Tab result mapping`
is complete. Workbench now owns a view-only selected-cell review projection
over the existing H-07 stable cell IDs. Previous/Next traverses failed cells
in deterministic row-major order with wrap; all-pass output disables both
actions. Height Image and 3D emphasize the same selected cell without
changing cell policy. Ordinary Thickness steps named `Tab 1..8 Thickness`
map by ordinal to cell-result presentation while retaining their step and
output identities. Navigation does not dirty, save, Preview, Publish, Run, or
replay Validation Set. Current Release evidence passes build `0/0`, height
measurement Workbench `54/54`, Completeness golden `23/23`, Inspection
Workspace `63/63`, recipe teaching `28/28`, Artifact Navigator `31/31`,
docking `33/33`, Shell options `24/24`, Viewer display `103/103`, structure
`17/17`, and current Wide/Compact capture quality. Preserve
`docs/OPENVISIONLAB_3D_COMPLETENESS_FAILURE_NAVIGATION_AND_TAB_MAPPING_20260729.md`
and `artifacts/current/20260729-completeness-failure-navigation/`.
Its historical next `H-11/H-12` slice is complete in the newer checkpoint
above.
`H-09` remains blocked by the missing typed detected-region route
`E-11/G-12`.

`H-05/H-06/H-07 completeness result and overlays` is complete. The optional
typed policy adds inclusive finite-coverage and reference-relative mean
raw-height limits while preserving seven-parameter H-02 recipes as
evidence-only `Warning`. Tools produces deterministic cell Pass/Fail, fails
closed when a cell has no finite mean, counts passed/failed cells, and sets
aggregate Pass only when every cell passes. Core owns stable coordinate-true
overlay descriptors; Height Image and 3D render the same green/red cells
without owning decision policy. The mixed `8 x 8` fixture produces `2` Pass,
`2` Fail, aggregate `Fail`, `4` overlays, and output SHA
`1B051233FFCCC65FD72A4CB50299C629C8BCE7929E7AC4CA3CA3F33653DBF8CE`;
an independent all-valid fixture produces aggregate Pass. Current Release
evidence passes build `0/0`, golden `23/23`, height measurement Workbench
`50/50`, Inspection Workspace `63/63`, Recipe Manager/PropertyGrid `37/37`,
Artifact Navigator `31/31`, Shell options `24/24`, structure `17/17`,
production Runner parity, and current Wide/Compact capture quality. Preserve
`docs/OPENVISIONLAB_3D_COMPLETENESS_RESULTS_AND_OVERLAYS_20260729.md` and
`artifacts/current/20260729-completeness-results-overlays/`. Its historical
next slice is complete in the newer H-08/H-10 checkpoint above.

`H-02/H-03/H-04 completeness grid metrics` is complete. Core owns the typed
rows/columns/native X-column and Z-row pitch/cell-size/GridRectangle profile,
stable row-major cell identity, exact finite coverage, and explicit
reference-relative mean raw-height output. Tools generates deterministic
non-overlapping cell geometry inside one Inspection Grid ROI and fails closed
when the extent does not fit. Workbench preserves ordered Reference and
Inspection Grid ROI roles, typed PropertyGrid editing, and explicit
Preview/Publish. Ordered graph and production Runner emit the same typed
output SHA-256. The controlled `8 x 8` fixture produces four cells with
coverage `1, 0.75, 0.5, 0` and relative means `2, 4, -2, missing`. Current
Release evidence passes build `0/0`, golden `14/14`, height measurement
Workbench `50/50`, Inspection Workspace `63/63`, Recipe Manager/PropertyGrid
`37/37`, Shell options `24/24`, structure `17/17`, production Runner parity,
and current Wide/Compact capture quality. Preserve
`docs/OPENVISIONLAB_3D_COMPLETENESS_GRID_METRICS_20260729.md` and
`artifacts/current/20260729-completeness-grid-metrics/`. This slice applies
no acceptance policy or aggregate decision. That historical next slice is
complete in the newer H-05/H-06/H-07 checkpoint above.

`I-12/I-13/I-15 threshold-assistant evidence hardening` is complete. The
shared candidate report contract `2.0` now owns deterministic missing-Good,
missing-Bad, insufficient-Good, insufficient-Bad, imbalanced-class, and
inseparable-distribution warnings with exact step/metric ownership,
Good/Bad counts, and development-sample SHA identities. Held-out remains
excluded. Warnings are limited to explicitly supported assistant metrics so
unmapped ROI statistics do not create misleading parameter warnings. The
published fail-closed coverage matrix contains Thickness Mean
Minimum/Maximum/Range and Warpage PeakToValley/Rms Maximum only. Role edits,
warning-state changes, Review, candidate draft Apply, manual PropertyGrid
edits/Apply, development replay, and Held-out replay retain their explicit
execution boundaries. Current Release evidence passes build `0/0`,
Validation Set `72/72`, Inspection Workspace `63/63`,
Recipe Manager/PropertyGrid `37/37`, Shell options `24/24`, structure
`17/17`, Runner report schema `1.1`/threshold contract `2.0` with the same
five mappings, and fresh Wide/Compact capture quality.
Preserve
`docs/OPENVISIONLAB_3D_THRESHOLD_ASSISTANT_HARDENING_20260729.md` and
`artifacts/current/20260729-threshold-assistant-hardening/`.

The earlier 11-video analysis is a durable product-direction input, not a
one-time audit. Future items must trace to the source-by-source lessons in
`docs/OPENVISIONLAB_3D_COMMERCIAL_VIDEO_DIRECTION_AND_PRIORITY_20260727.md`
and the operator-focused findings in
`docs/OPENVISIONLAB_3D_INDUSTRIAL_UX_AUDIT_20260728.md`: GoPxL responsibility
separation, SICK evidence-based threshold/completeness, HALCON explicit model
and scene preparation plus pose/score diagnostics, MERLIC Height Image cell
inspection, and Zivid/Photoneo source-quality trust. This direction does not
authorize camera, reconstruction, factory-integration, cloud, or implicit
execution scope.

`L-11 threshold-correction evidence in Run Record` is complete. Ordered graph
Run Record schema `1.5` now embeds one read-only snapshot of the existing
recipe-side correction sidecar. It preserves exact candidate, step, tool,
metric, before, suggested, manually committed, before/corrected development,
and Held-out identities and values. Missing evidence is `Unavailable`;
identity differences are `Mismatch`; changed committed parameters are
`Stale`; malformed or internally inconsistent evidence is `Invalid`.
Projection never recalculates a threshold, applies a parameter, executes
inspection, or replays development/Held-out samples. JSON, HTML, and the
Workbench Run Record tab share the same typed contract. Current Release
evidence passes build `0/0`, Run Record `10/10`, Validation Set `72/72`,
Inspection Workspace `63/63`, Recipe Manager/PropertyGrid `37/37`, structure
`17/17`, production Runner JSON/HTML parity, and fresh Wide/Compact capture
quality. Preserve
`docs/OPENVISIONLAB_3D_THRESHOLD_CORRECTION_RUN_RECORD_20260729.md` and
`artifacts/current/20260729-threshold-correction-run-record/`.

The next implementation item is
`J-01/J-03/J-04 SurfaceModel preparation foundation`.

`I-09/I-11 manual parameter correction and durable failure -> correction ->
Held-out evidence` is complete. A controlled committed Thickness draft
`0..20` produces one genuine expected-role mismatch: Bad-high SHA
`6E00A03C6A901DFC39EBE41E7E14E3EC1FE8A3F4FBFBFECE9C1E8A5E6DCE9AD9`,
Mean `20`, passes incorrectly. The deterministic Range candidate remains
`threshold.0ad7b16eaa3d4362`, suggested `2..4`. The operator changes the typed
draft to `1.5..4.5`, commits through ordinary PropertyGrid Apply, then invokes
an explicit development-only replay. That replay preserves the same four
sample SHA identities and changes mismatch `1 -> 0`; it does not run Held-out.
Only then does the separate explicit Held-out command unlock. Held-out Mean
`3`, SHA
`D9384A7B5A032D28E952E8742619EA224F2763FC5B5B3C431DC895544AA93C3B`
passes. The portable evidence extension stores before, suggested, manual,
corrected development, and Held-out records. Workbench and Runner schema
`2.0` agree exactly. Current Release evidence passes build `0/0`, Validation
Set `66/66`, Inspection Workspace `63/63`, Recipe Manager/PropertyGrid
`37/37`, Shell options `24/24`, code structure `17/17`, Runner parity, and
fresh Wide/Compact capture quality. Preserve
`docs/OPENVISIONLAB_3D_THRESHOLD_MANUAL_CORRECTION_AND_FAILURE_RECORD_20260728.md`
and `artifacts/current/20260728-threshold-manual-correction/`.

`I-08/I-10 explicit threshold Review/Cancel/draft Apply and Held-out replay`
is complete. Exact candidate mappings fail closed against existing typed
Thickness/Warpage parameters. Review is non-mutating; Cancel preserves the
recipe, PropertyGrid, and execution state; candidate Apply changes only the
typed PropertyGrid draft. Ordinary PropertyGrid Apply remains separate.
Explicit Held-out replay projects the proposal onto an immutable recipe copy,
executes only Held-out samples, and saves a portable correction-evidence
sidecar. Workbench and Runner agree on candidate
`threshold.0ad7b16eaa3d4362`, `MinimumThickness 0->2`,
`MaximumThickness 10->4`, four development samples, one Held-out sample, and
the exact Held-out SHA. Current Release evidence passes build `0/0`,
Validation Set `58/58`, Inspection Workspace `63/63`, recipe teaching
`28/28`, Recipe Manager/PropertyGrid `37/37`, Artifact Navigator/Output
Compare `31/31`, Shell smoke options `24/24`, code structure `17/17`, Runner
parity, and current Wide/Compact capture quality. Preserve
`docs/OPENVISIONLAB_3D_THRESHOLD_REVIEW_APPLY_AND_HELD_OUT_REPLAY_20260728.md`
and `artifacts/current/20260728-threshold-review-heldout/`.

The historical next slice named here is now closed by the newer I-09/I-11,
I-12/I-13/I-15, L-11, and H-02/H-03/H-04 checkpoints above.
`H-05/H-06/H-07` is complete in the newer checkpoint above.

`I-06/I-07 threshold candidates and exact error table` is complete.
Explicit-run Good/Bad observations now produce one deterministic Minimum,
Maximum, and Range candidate per eligible step/region metric. Ranking
minimizes total errors, then false accepts, false rejects, and finally uses a
stable tightness rule. Every candidate owns exact sample decisions and
reproducible confusion counts. Held-out observations are recorded as excluded
and never enter boundaries, ranking, counts, or decisions. Workbench exposes
read-only candidate/error tables without editing or executing; Runner emits
the same contract. Current Release evidence passes build `0/0`, Validation
Set `45/45`, Inspection Workspace `63/63`, Shell smoke options `24/24`,
recipe teaching `28/28`, Artifact Navigator/Output Compare `31/31`, code
structure `17/17`, Runner parity with zero Held-out decisions, and current
default/expanded/compact screenshot quality. Preserve
`docs/OPENVISIONLAB_3D_THRESHOLD_CANDIDATES_AND_ERROR_TABLE_20260728.md` and
`artifacts/current/20260728-threshold-candidates/`.

The durable next-chat startup request, current working-tree boundary, full
commercial-video-derived priority train summary, and next
`J-01/J-03/J-04` acceptance boundary are maintained in
`docs/OPENVISIONLAB_3D_NEXT_CHAT_HANDOFF_PROMPT_20260728.md`.

Height Image press-drag-release versus 3D two-point instruction and a focused
Compact ROI teaching surface remain P1 UX items. The owner R0 replay and
physical metrology remain external.

`I-04/I-05 labeled sample evidence` is complete. Each Validation Set sample
has one durable `Good`, `Bad`, or `HeldOut` role in a portable recipe-side
manifest. Explicit Run produces per-step metric distributions plus routed
`GridRectangle` mean raw-height and valid-cell-ratio distributions. Held-out
observations remain visible with `IncludedInDevelopment=false`. Role edits
never execute inspection or dirty the recipe graph; normal save/close state
still protects the sidecar change. Workbench save/reopen restores Pending
roles without stale evidence, and production Runner emits the same contract.
Preserve
`docs/OPENVISIONLAB_3D_LABELED_SAMPLE_EVIDENCE_20260728.md` and
`artifacts/current/20260728-labeled-sample-evidence/`.

`D-05/D-06 Level Surface` is complete. One or more explicit reference
`GridRectangle` ROIs define a least-squares raw-height plane; overlapping
finite cells count once. The derived C3D preserves the source grid and missing
mask while applying
`Y' = Y - fittedPlane(X,Z) + referenceMean`. The typed leveling transform
records source identity, every reference region, residual evidence,
coefficients, the equivalent matrix, provenance, and SHA-256. The authored
RMS gate fails closed. Workbench typed Apply, explicit Preview/Publish,
multi-ROI addition, save/reopen, Viewer/Output Compare, and Runner parity pass
on the known tilted fixture. Preserve
`docs/OPENVISIONLAB_3D_LEVEL_SURFACE_20260728.md` and
`artifacts/current/20260728-level-surface/`.

`D-04 Remove Outlier Pixels` is complete. The typed
`LocalMedianAbsoluteDeviation` preparation rule excludes the center sample,
uses a strict-greater-than threshold, supports odd `3/5/7` windows and an
explicit minimum-neighbor gate, preserves source missing cells, uses available
neighbors at boundaries, and sets detected outliers missing. Data owns one
immutable coordinate-true outlier mask; Tools, Workbench, Viewer, Output
Compare, and Runner share its identity and the derived C3D. The known
`12 x 10` fixture removes exactly `3` cells and changes valid/missing from
`119/1` to `116/4`, while the source hash remains unchanged. Preserve
`docs/OPENVISIONLAB_3D_REMOVE_OUTLIER_PIXELS_20260728.md` and
`artifacts/current/20260728-remove-outlier-pixels/`.

`E-09 OrientedBox3D Viewer outline and pointer handles` is complete. The
persisted schema `1.4` volume now renders as a translucent oriented cuboid
with a rotation ring and fixed-screen-size center, X/Y/Z resize, height, and
local-Y rotation handles. When a projected axis collapses in Top or side
views, its screen-space fallback remains visible and draggable. Viewer
gestures and numeric fields edit one synchronized transient Review candidate;
the global Review bar is the sole visible Apply/Cancel owner, Enter/Esc remain
available, and Apply preserves the selection identity without running
inspection. Real Windows pointer evidence passes Perspective move/X/Y/Z
resize/rotate, Top height resize, and side collapsed-axis resize while recipe,
execution, and gesture camera state remain unchanged. Preserve
`docs/OPENVISIONLAB_3D_ORIENTED_BOX_VIEWER_HANDLES_20260728.md` and
`artifacts/current/20260728-oriented-box-viewer-handles/`.

The E-09 checkpoint preceded the newer D-04 closure above.

The two P0 findings from the current Release operator-video review are closed.
Schema `1.5` stores first/second ROI identities on the owning inspection step,
so deleting Reference cannot promote the surviving Measurement selection.
The shared capture now ends before the role advances, so fresh Height Image
Reference Apply immediately enables Measurement Draw.

External pointer/keyboard replay completes Reference and Measurement
`Missing -> Drawing -> Review -> Applied`, Preview readiness, Ctrl+S, and
save/reopen at Wide and Compact widths. The workflow does not invoke Preview
or Run implicitly. Preserve
`docs/OPENVISIONLAB_3D_DUAL_ROI_ROLE_PRESERVATION_20260728.md`,
`artifacts/current/20260728-dual-roi-role-preservation/`, and the updated
`docs/assets/openvisionlab-3d-roi-workflow.gif`.

That dual-ROI closure was the prerequisite immediately before E-09; the newer
checkpoint above supersedes its former next-item statement.

`E-07/E-08 OrientedBox3D contract and numeric editing` is complete:

- schema `1.4` adds a persisted `oriented-box-3d` selection with center,
  right-handed orthonormal axes, and positive half-extents;
- existing schema `1.3` artifact-owned recipes remain valid and executable;
- the Selected Tool Regions surface owns a numeric MVVM editor with explicit
  New, Apply, Cancel, and guarded Delete;
- Apply preserves identity, changes only the recipe, and never invokes
  Preview, Publish, or Run;
- exact save/reopen, invalid-axis/extent/payload rejection, and old affine
  adapters pass;
- focused evidence passes Release build `0/0`, selection `25/25`, Inspection
  Workspace `60/60`, teaching `28/28`, height measurement `45/45`, Artifact
  Navigator `31/31`, docking `33/33`, Recipe Manager/WPG `37/37`,
  artifact-owned Runner `18/18`, synthetic affine `18/18`, schema `1.3`
  affine `4/4`, schema `1.3` correspondence `5/5`, shell options `21/21`,
  structure `17/17`, and Wide/Compact screenshot quality.

Preserve
`docs/OPENVISIONLAB_3D_ORIENTED_BOX_CONTRACT_AND_NUMERIC_EDITOR_20260728.md`
and `artifacts/current/20260728-oriented-box-contract/`.

That checkpoint was the persisted numeric contract before rendering.
The newer E-09 checkpoint above closes the Viewer outline and pointer handles.

The current Wide and Compact synchronized ROI captures were reviewed against
the v3 and GoPxL-derived interaction contracts. One concrete v3 acceptance
gap was corrected:

- the global Review ribbon is now the only visible primary ROI Apply/Cancel
  owner;
- duplicate Selected Tool and Height Image Apply/Cancel controls and the
  Viewer instruction toast were removed;
- the Viewer no longer repeats the selected-step title, route IDs, output ID,
  or typed-adapter status already owned by the global bar and Selected Tool;
- local ROI capture hides the unrelated Thickness repeat card;
- inline Height Image editing temporarily changes the split to
  `35% 3D / 65% Height Image` and restores the existing ratio afterward;
- compact exact-source evidence improves from `4.2%` to `7.9%`.

Preserve
`docs/OPENVISIONLAB_3D_WORKSPACE_V3_UX_MID_REVIEW_AND_ACCEPTANCE_CORRECTION_20260728.md`
and
`artifacts/current/20260728-workspace-v3-ux-acceptance-correction/`.
Workspace v3 remains `7/8` because R0 is still an external owner replay.

The owner explicitly requested continued development while R0 remains
available only as a later unaided acceptance gate. `C-09/C-10 synchronized
Height Image / 3D ROI editing` is complete:

- Reference cyan and Measurement orange overlays use the same selection ID
  and native-grid rectangle in both views;
- `HeightImageRoiWorkspaceViewModel` owns WPF-neutral 2D projection and
  gestures while the existing Workbench owns lifecycle and recipe mutation;
- Height Image supports draw, move, corner resize, role selection, Review,
  Apply, Cancel, and Delete;
- actual Windows pointer evidence proves the Height Image and 3D transient
  candidates remain equal;
- Review preserves dirty state, steps, selections, routing, applied geometry,
  current output, and camera;
- Apply preserves selection ID and passes save/reopen;
- focused evidence passes build `0/0`, Workspace `50/50`, smoke options
  `21/21`, wide/compact pointer smoke, display `103/103`, Height Image
  `21/21`, docking `33/33`, height measurement `45/45`, recipe teaching
  `28/28`, and structure `17/17`.

Preserve
`docs/OPENVISIONLAB_3D_SYNCHRONIZED_HEIGHT_IMAGE_ROI_EDITING_20260728.md`
and `artifacts/current/20260728-height-image-roi-editing/`.

`C-11 visible invalid/missing-cell overlay` is complete:

- Height Image shows the shared native invalid-cell map in magenta by default
  and exposes a direct view-only toggle;
- the legend reports the exact missing count and percentage;
- valid palette pixels remain unchanged and hiding/re-enabling the overlay is
  deterministic;
- the exact Thickness Coupon v1 source shows `166,764` overlay pixels (`15.5%`) and
  retains mask SHA-256
  `44EDC44DEE6D0193DCCF22130487DC3CF80CCE2F68BDAA854A1D16FAA4BDC358`;
- focused evidence passes build `0/0`, Height Image `25/25`, exact-source
  probe, Inspection Workspace `53/53`, invalid map `15/15`,
  SourceQualityReport `13/13`, docking `33/33`, recipe teaching `28/28`,
  Artifact Navigator `31/31`, height measurement `45/45`, shell options
  `21/21`, structure `17/17`, and Wide/Compact screenshot quality.

Preserve
`docs/OPENVISIONLAB_3D_VISIBLE_INVALID_CELL_OVERLAY_20260728.md` and
`artifacts/current/20260728-invalid-cell-overlay/`.

`C-08 Shared Height Image / 3D cursor` is complete:

- one WPF-neutral presentation session owns source identity, cursor origin,
  native row/column, raw height, valid/missing state, and revision;
- Height Image hover renders the same valid point as a yellow/cyan marker in
  the main 3D Viewer;
- 3D hover renders the same picked C3D point as a Height Image crosshair;
- source mismatch, missing cells, and stale leave events fail closed;
- recipe, execution, output, and camera state remain unchanged;
- the exact Thickness Coupon v1 source proves
  `column 593 / row 800 / H 633.4000244140625 raw-height` in both directions;
- focused evidence passes Inspection Workspace `42/42`, smoke options
  `20/20`, and wide/compact actual-window bidirectional smoke.

Preserve
`docs/OPENVISIONLAB_3D_SHARED_HEIGHT_CURSOR_20260728.md` and
`artifacts/current/20260728-shared-height-hover/`.

`C-07 Height Image palette and display range` is complete:

- Height, Grayscale, and Thermal palettes are first-class Height Image state;
- Auto range uses the finite full-source minimum and maximum;
- numeric Min/Max is fail-closed and requires explicit Apply;
- active range text and a matching color legend remain visible at wide and
  `1280 x 760` widths;
- palette/range changes regenerate only immutable display pixels and preserve
  native coordinates, raw heights, invalid cells, recipe, and execution;
- the exact Thickness Coupon v1 source changes from Auto Height SHA-256
  `6A6C12F7A729ABF49830F07CBB868FCCCB94C987584856128662109BA377B087`
  to Thermal `0..1200 raw-height` SHA-256
  `49FE0B0009CDE14BEE44C40C99F7EC0A6571BBC3DCDF8EDA168943E418F531BF`;
- focused evidence passes Height Image `21/21`, Inspection Workspace `36/36`,
  and wide/compact exact-source non-execution smoke.

Preserve
`docs/OPENVISIONLAB_3D_HEIGHT_IMAGE_DISPLAY_RANGE_20260728.md` and
`artifacts/current/20260728-height-image-display-range/`.

`B-08 Unified Source
Quality workspace` is complete:

- the normal Selected Tool surface presents the current report whenever a
  source is loaded and no inspection step is selected;
- the source card exposes explicit read-only navigation back to quality;
- grid, coverage, height statistics/distribution, invalid-map identity,
  frame/unit/provenance, and actual channel availability are visible;
- exact-source wide/compact smoke proves recipe and execution state remain
  unchanged;
- focused workspace verification passes `18/18` and current wide/compact
  captures pass on attempt 1.

Preserve
`docs/OPENVISIONLAB_3D_SOURCE_QUALITY_WORKSPACE_20260728.md` and
`artifacts/current/20260728-source-quality-workspace/`.

`B-09 Coordinate-true
invalid-cell map` is complete:

- Data owns one immutable row-major LSB-first packed map and stable identity;
- Source Quality and Height Image consume the same map owner;
- synthetic invalid-map verification passes `15/15`, Source Quality
  regression `13/13`, and Height Image regression `14/14`;
- the exact Thickness Coupon v1 Thickness source has `1,075,200` cells, `166,764`
  missing cells, `134,400` packed bytes, and identical Source Quality / Height
  Image mask SHA-256
  `44EDC44DEE6D0193DCCF22130487DC3CF80CCE2F68BDAA854A1D16FAA4BDC358`.

Preserve
`docs/OPENVISIONLAB_3D_INVALID_CELL_MAP_PARITY_20260728.md` and
`artifacts/current/20260728-invalid-cell-map-parity/`.

`C-06 Full-size
coordinate-true Height Image Viewer` is complete:

- Data owns an immutable native-grid frame with
  `pixel X=column / pixel Y=row / no flip / one cell per pixel`;
- the Workbench auxiliary slot defaults to Height Image and still accepts
  existing real source/Filter C3D candidates;
- Fit, 1:1, wheel zoom, middle-drag pan, and exact row/column/raw-height hover
  are view-only;
- the exact Thickness Coupon v1 Thickness source produces `1280 x 840`,
  `1,075,200` pixels, `908,436` valid, and `166,764` missing cells;
- focused mapping passes `11/11`, Workspace non-execution `30/30`, Artifact
  Navigator `31/31`, docking `33/33`, Source Quality `12/12`, and structure
  `17/17`.

Preserve
`docs/OPENVISIONLAB_3D_FULL_HEIGHT_IMAGE_VIEWER_20260727.md` and
`artifacts/current/20260727-full-height-image-viewer/`.

`B-07 SourceQualityReport` is also complete:

- schema `1.0` is WPF-neutral and owned by Core;
- Data calculates exact counts, raw-height statistics/distribution, source
  identity, frame/unit/provenance, and invalid-cell mask identity;
- unsupported C3D intensity/color/depth/normal/confidence/SNR channels are
  explicit and never fabricated;
- Runner verification passes `12/12`;
- the exact Thickness Coupon v1 Thickness source produces a `1280 x 840` report with
  `908,436` valid and `166,764` missing cells.

Preserve
`docs/OPENVISIONLAB_3D_SOURCE_QUALITY_REPORT_20260727.md` and
`artifacts/current/20260727-source-quality-report/`.

### G0 owner acceptance prerequisite

Prerequisite:

- the owner is available at the running current Release application;
- the exact Thickness Coupon v1 C3D and documented 12-step workflow are used;
- no assistant guidance is supplied during the replay.

Pass:

- New -> source -> Thickness -> Reference ROI -> Measurement ROI ->
  parameters -> Preview -> repeat `4 x 2` -> per-Tab review -> Run -> Save ->
  reopen is completed unaided.

Fail:

- the operator cannot discover the next action;
- a visible state does not match the recipe state;
- an ROI cannot be created, corrected, deleted, or applied;
- Save/reopen or Run does not preserve the expected recipe.

Do not recommend or spend model tokens on repeated implementation verification
while this external prerequisite is unavailable.

## Release-train sequence

| Train | Outcome | Entry gate | Exit gate | Recommended model | Reasoning effort |
| --- | --- | --- | --- | --- | --- |
| R0 | Workspace v3 owner acceptance | Current Release evidence passes | Owner completes unaided replay | External owner prerequisite | No model until available |
| R1 | Source Trust and Linked Teaching | R0 accepted | SourceQualityReport and linked Height Image pass current-source UI/Runner gates | `gpt-5.6-sol` | high |
| R2 | Typed 3D Regions and Preparation | R1 accepted | `OrientedBox3D`, invalid/outlier handling, and Level Surface pass round-trip/execution gates | `gpt-5.6-sol` | high |
| R3 | Evidence-Assisted Presence Inspection | R2 accepted | Good/Bad/Held-out threshold teaching and Completeness pass Workbench/Runner replay | `gpt-5.6-sol` | high |
| R4 | Surface Matching Foundation | R1 source trust accepted; R3 need not block prototype | One identified model/scene fixture returns reproducible pose, scores, and overlay | `gpt-5.6-sol` | high |
| R5 | Matching Optimization and Diagnostics | R4 accepted | normals/keypoints/constraints/edge scores/multiple-match review pass | `gpt-5.6-sol` | high |
| R6 | Physical Measurement Credibility | trusted units/calibration artifacts available | documented uncertainty/repeatability gate passes | External data prerequisite first | No model until available |

## Development backlog

### A. Product workflow, navigation, and lifecycle

Recommended model: `gpt-5.6-sol`

Reasoning effort: medium for localized changes, high for cross-workspace state
changes

| ID | Status | Development item | Dependency | Closure evidence |
| --- | --- | --- | --- | --- |
| A-01 | P | Real Setup -> Teach -> Validate -> Results structure, live hosted ownership, automated failure-to-Teach, actionable Teach correction context, Wide/Compact Viewer/ROI recovery, and Results/Advanced return preservation pass; human-owner R0 remains | None | `OPENVISIONLAB_3D_TEACH_FAILURE_CORRECTION_CONTEXT_20260729.md`; current app-only Wide/Compact replay; owner R0 |
| A-02 | C | One synchronized selected step/input/ROI/output/Viewer-slot identity | None | `InspectionWorkspaceSelectionSession` focused verification |
| A-03 | C | Explicit parameter Apply/Discard | None | PropertyGrid verification and recipe non-execution checks |
| A-04 | C | Explicit ROI Review/Apply/Cancel/Delete | None | ROI lifecycle and actual-pointer evidence |
| A-05 | C | Explicit Preview/Publish/Run separation | None | Workbench/Runner verification |
| A-06 | C | Save, Save As, recent recipe, last-recipe startup restoration | None | Recipe Manager and startup verification |
| A-07 | C | Selected output Show/Pin/Compare | None | Artifact Navigator and Output Compare verification |
| A-08 | C | Single, split, stacked, and pop-out Viewer layouts | None | Viewer Workspace verification |
| A-09 | P | Configure/Review/Run state language remains understandable across every tool | A-01 | Owner replay plus cross-tool state-text review |
| A-10 | C | Validate and Results local drill-down retains live content, localized/accessibility navigation, failure-to-Teach routing, and an explicit visible Advanced route after stage recomposition | A-01 | `OPENVISIONLAB_3D_IA4B_OWNER_PATH_REPLAY_20260729.md`; actual Release Wide/Compact video; Window-hosted `52/52` |
| A-11 | N | Consistent per-tool empty, incomplete, stale, ready, running, pass, fail, and error presentation matrix | A-09 | One shared state contract and focused UI verification |
| A-12 | C | Global current-source quality state beside recipe/input state | B-08 | `OPENVISIONLAB_3D_GLOBAL_CURRENT_SOURCE_QUALITY_STATE_20260803.md`; current Release Wide/Compact Authoring/Validate/Results captures; read-only Source Quality smoke |
| A-13 | N | Task-specific assistant host using `analyze -> propose -> review -> explicit apply` | H-03 or D-04 | One assistant with Cancel/non-mutation and Apply evidence |
| A-14 | N | In-product first-use checklist limited to current inspection task | R1 | Owner can dismiss/reopen; no permanent journey strip |
| A-15 | P | Keyboard command coverage for common recipe, execution, and ROI actions | None | Existing shortcut verifier plus new Height Image/assistant actions |
| A-16 | C | Advanced workspace semantic-theme parity for Data/Layers, Tool/Inspector, Evidence Workbench, linked evidence, and generated child controls | Current layout audit | `OPENVISIONLAB_3D_ADVANCED_SEMANTIC_THEME_PARITY_20260803.md`; fresh Wide/Compact English/Korean and open-popup theme-state evidence |

### B. Source identity, quality, and provenance

Recommended model: `gpt-5.6-sol`

Reasoning effort: high

| ID | Status | Development item | Dependency | Closure evidence |
| --- | --- | --- | --- | --- |
| B-01 | C | C3D path, byte length, SHA-256, grid width/height identity | None | Current source binding verifier |
| B-02 | C | Local C3D, GLB, STL, LAS, and LAZ loading | None | Existing loader/sample matrix |
| B-03 | C | Asynchronous/cancellable C3D load with previous-source retention on failure | None | Release source-load verification |
| B-04 | C | Valid/missing counts and height distribution | None | Height distribution verification |
| B-05 | C | Frame and declared-unit fields in recipe/result contracts | None | Recipe round-trip and Runner evidence |
| B-06 | P | Bounds, coverage, collision, and missing evidence are distributed across outputs | None | Existing C3D/re-grid summaries |
| B-07 | C | WPF-neutral `SourceQualityReport` contract | B-01, B-04 | Release build `0/0`, headless `12/12`, exact owner-source JSON |
| B-08 | C | Unified Source Quality workspace/panel | B-07 | Release build `0/0`, workspace `18/18`, exact-source wide/compact non-execution smoke and capture quality |
| B-09 | C | Coordinate-true invalid-cell map and mask identity | B-07, C-06 | Release build `0/0`, map `15/15`, Source Quality `13/13`, Height Image `14/14`, exact-source pixel/cell/SHA parity |
| B-10 | N | Grid monotonicity, duplicate locator, non-finite coordinate, and topology diagnostics | B-07 | Deterministic malformed fixtures |
| B-11 | C | Available-channel catalog: height, intensity, color, depth, normal, confidence/SNR | B-07 | Release build `0/0`; focused `26/26`; C3D, GLB/STL, LAS/LAZ exact seven-entry evidence |
| B-12 | C | Acquisition/source provenance text and limitation notes | B-07 | Explicit Available/Unavailable evidence and limitations; exact save/reopen; legacy fallback; source isolation; no execution; focused `14/14` |
| B-13 | N | Source quality gate consumed by compatible-tool suggestions | B-07 | Invalid source disables only unsupported tools with exact reason |
| B-14 | N | Before/after quality delta for each preparation output | D-01 | Derived artifact report with valid/missing/outlier changes |
| B-15 | P | Normal inspection for imported mesh pick exists only at one selected surface point | None | Current mesh pick normal overlay |
| B-16 | C | Dense normal availability/consistency report when source supports normals | B-11 | Release build `0/0`; known-valid and missing/partial/reversed/invalid/degenerate fixtures; public GLB/STL evidence |
| B-17 | N | Source limitation flags for reflective, transparent, textureless, clipped, or low-coverage acquisition | B-12 | Operator-authored or imported flags persist in recipe/session evidence |

### C. Linked Height Image, 3D Viewer, and diagnostic views

Recommended model: `gpt-5.6-sol`

Reasoning effort: high

| ID | Status | Development item | Dependency | Closure evidence |
| --- | --- | --- | --- | --- |
| C-01 | C | Surface is the default C3D geometry | None | Viewer display verification |
| C-02 | C | Perspective and true X/Z Top orthographic projection | None | Camera math and display verification |
| C-03 | C | Orbit, left/empty-space behavior, middle/right pan, wheel zoom, Fit all, Fit ROI | None | Actual pointer verification |
| C-04 | C | Height palette and full-source distribution legend | None | Height distribution verification |
| C-05 | P | Small read-only linked Height Map preview | None | Current Shell linked-view contract |
| C-06 | C | Full-size coordinate-true Height Image Viewer | B-07 | Native-grid `11/11`, exact owner-source probe, current Release inline/pop-out evidence |
| C-07 | C | Height Image pan, zoom, fit, palette, and numeric range controls | C-06 | Release build `0/0`, Height Image `21/21`, Workspace `36/36`, exact-source wide/compact manual-range smoke |
| C-08 | C | Shared hover row/column/raw-height between Height Image and 3D | C-06 | Release build `0/0`, Workspace `42/42`, smoke options `20/20`, exact-source wide/compact bidirectional smoke |
| C-09 | C | Shared selected ROI and role colors between Height Image and 3D | C-06, E-01 | Same selection ID and geometry in both views; Workspace `50/50`; current Release wide/compact evidence |
| C-10 | C | Height Image ROI draw/move/resize/delete/review/apply/cancel | C-09 | Actual Windows pointer Review and Apply/save/reopen; recipe non-execution before Apply |
| C-11 | C | Invalid/missing mask overlay in Height Image | B-09, C-06 | Pixel count matches SourceQualityReport |
| C-12 | P | Height range palette selection exists in the 3D display | C-04 | Current height distribution |
| C-13 | C | Manual/auto display range in both linked views without recipe mutation | C-06 | `OPENVISIONLAB_3D_LINKED_VIEW_DISPLAY_RANGE_CONSISTENCY_20260803.md`; exact bidirectional/AUTO current-build reports; Wide/Compact evidence |
| C-14 | C | Height profile and endpoint interaction | None | Profile UI/pointer verification |
| C-15 | N | Linked crosshair/profile line between Height Image, Profile, and 3D | C-06, C-14 | One coordinate identity across three views |
| C-16 | P | Intensity/color/depth display varies by file type and available source data | B-11 | Current GLB/LAS color plus C3D height evidence |
| C-17 | N | First-class diagnostic map selector driven by available channels | B-11 | Channel-specific view with unavailable reasons |
| C-18 | N | Normal map/normal-vector diagnostic mode when source supports normals | B-16 | Known-normal fixture and no fabricated C3D normals |
| C-19 | C | Viewer split/stack/pop-out with independent cameras | None | Viewer Workspace verification |
| C-20 | P | Per-Viewer source/output pinning exists for renderable artifacts | None | Current Output Compare/Viewer Workspace |
| C-21 | N | Per-Viewer diagnostic channel, palette, overlay, and linked-camera options | C-17 | Two real Viewers with independent and linked states |
| C-22 | C | Screenshot capture and evidence artifact | None | Existing Viewer/Shell smoke captures |

### D. Height-field and point-cloud preparation

Recommended model: `gpt-5.6-sol`

Reasoning effort: high

| ID | Status | Development item | Dependency | Closure evidence |
| --- | --- | --- | --- | --- |
| D-01 | C | Median Filter creates a separate `FilteredHeightField` | None | Filter adapter and Runner verification |
| D-02 | C | Missing mask preserved and available-neighbor boundary policy | None | Filter contract verification |
| D-03 | P | ROI/Crop is cataloged, while full preparation-output workflow is incomplete | E-01 | Typed output identity and Runner execution |
| D-04 | C | Remove Outlier Pixels tool with explicit rule and mask evidence | B-09 | Known outlier fixture, before/after counts, Viewer/Runner parity |
| D-05 | C | Level Surface from one or more explicit reference ROIs | F-01, C-06 | Tilted fixture levels with residual and fail-closed gate evidence |
| D-06 | C | Preserve leveling transform as typed output, not hidden image mutation | D-05 | Save/reopen, Workbench/Viewer, and Runner transform parity |
| D-07 | N | Reduce Domain/Mask tool | E-11, D-03 | Outside cells remain missing in derived output |
| D-08 | N | Height-threshold background removal | B-07 | Known foreground/background fixture |
| D-09 | N | Saved-background identity and subtraction | B-01 | Background SHA, aligned grid, delta output, mismatch rejection |
| D-10 | N | Distance-based point-cloud background filter | B-07 | Synthetic separated cloud fixture |
| D-11 | N | Region-growing component preparation | G-11 | Known connected-region fixture |
| D-12 | P | Display render-density sampling exists but does not change inspection data | None | Current Viewer density contract |
| D-13 | N | Typed point-cloud voxel/grid downsample with source/result separation | B-07 | Count reduction, bounds tolerance, deterministic hash |
| D-14 | N | Normal calculation/validation preparation when algorithm and licensing are approved | B-16 | Known analytic surface fixture |
| D-15 | C | Full-XYZ affine apply and explicit re-grid | None | A1/A2/A3 verification |
| D-16 | C | Re-grid reports coverage, missing cells, and collisions | None | Re-grid verification |
| D-17 | N | Preparation chain quality comparison view | B-14, C-19 | Source and prepared output shown with numeric quality delta |
| D-18 | N | Preparation presets as editable drafts, never automatic execution | A-13 | Analyze/propose/review/apply contract |

### E. Typed selections and region artifacts

Recommended model: `gpt-5.6-sol`

Reasoning effort: high

| ID | Status | Development item | Dependency | Closure evidence |
| --- | --- | --- | --- | --- |
| E-01 | C | `GridRectangle` X=column/Z=row footprint | None | Schema `1.3` recipe verification |
| E-02 | C | PointSet(2), PointSet(3), and landmark correspondence selections | None | Existing feature/datum verification |
| E-03 | C | Reference and Measurement role ownership | None | Dual-ROI Thickness/Flatness verification |
| E-04 | C | GridRectangle Review state and same-ID replacement | None | ROI lifecycle verification |
| E-05 | C | Numeric row/column/count editing | None | Teaching verification |
| E-06 | C | `4 x 2` repeat-grid display review and ordinary-step Apply | None | Repeat authoring `20/20` |
| E-07 | C | New `OrientedBox3D` selection kind | B-05 | Core/Data schema and validator verification |
| E-08 | C | `OrientedBox3D` center, axes, and half-extents numeric editor | E-07 | Round-trip and invalid-axis rejection |
| E-09 | C | Top/side/perspective move, resize, rotate, and height handles | E-07, C-06 | `docs/OPENVISIONLAB_3D_ORIENTED_BOX_VIEWER_HANDLES_20260728.md`; actual Windows pointer Perspective/Top/side evidence |
| E-10 | N | Distinguish view-only GridRectangle overlay Y from persisted volume extent | E-07 | UI wording and contract verification |
| E-11 | N | Region artifact output that downstream tools can consume | E-07 or E-01 | Typed route and Artifact Registry evidence |
| E-12 | N | Region-source relationship and transform propagation | E-11, F-05 | Same physical region after typed alignment |
| E-13 | N | Per-tool declaration of supported selection kinds and roles | E-07 | Compatible-tool matrix and fail-closed validator |
| E-14 | N | GridCircle selection for circular 2D height-field regions | C-06, E-13 | Draw/numeric/save/Runner evidence |
| E-15 | N | GridPolygon selection for irregular masks | C-06, E-13 | Vertex edit/save/mask output evidence |
| E-16 | N | Convert selected connected region into editable region artifact | G-11, E-11 | Detection output -> editable derived region without source mutation |

### F. Feature, datum, alignment, and coordinate frames

Recommended model: `gpt-5.6-sol`

Reasoning effort: high

| ID | Status | Development item | Dependency | Closure evidence |
| --- | --- | --- | --- | --- |
| F-01 | C | 3-Point Plane and plane-fit primitives | None | Existing feature and measurement verification |
| F-02 | C | Height Difference Edge | None | Edge adapter/diagnostics |
| F-03 | C | 2-Point Line and deterministic 3D Line Fit | None | Existing feature verification |
| F-04 | C | Line Intersection/CornerAnchor | None | Existing intersection verification |
| F-05 | C | Landmark Correspondence -> XYZ Affine Solve -> Apply -> Re-grid | None | Current A1/A2/A3 chain |
| F-06 | P | Manual/deterministic alignment exists; general object alignment does not | None | Current affine and fixed nominal/actual evidence |
| F-07 | N | Level-frame artifact derived from explicit surface ROIs | D-05 | Transform identity and residual evidence |
| F-08 | N | 2D height-image border/feature alignment for moving parts | C-06, F-02 | Known translated/rotated height-image fixture |
| F-09 | N | Rigid point-pair/manual alignment distinct from full affine | E-02 | Known rigid transform and Runner parity |
| F-10 | N | Constrained best-fit alignment policy | F-09 | Synthetic known-transform fixture and failure gates |
| F-11 | N | Alignment confidence/residual/coverage evidence | F-07 or F-10 | Explicit metric and acceptance state |
| F-12 | N | Named coordinate-frame hierarchy and visible transform chain | F-07 | Source/reference/result frame display |
| F-13 | C | Symmetry declaration for later matching | J-01 | Schema-1.0 byte parity `5/5`; schema-1.1 none/discrete-axis declaration, identity, validation, and round trip `34/34` |
| F-14 | C | Allowed pose/rotation/search range contract | J-01 | Invalid range rejection and saved parameters |
| F-15 | E | Physical calibration frame and traceable unit validation | Trusted calibration artifact | Independent physical evidence |

### G. Deterministic measurement and inspection tools

Recommended model: `gpt-5.6-sol`

Reasoning effort: medium for one established-rule adapter, high for new
geometry or numerical policy

| ID | Status | Development item | Dependency | Closure evidence |
| --- | --- | --- | --- | --- |
| G-01 | C | Dual-surface Thickness | E-01 | Reference fit, signed separation, limits, overlays |
| G-02 | C | Warpage | E-01 | P2V/RMS/valid-sample evidence |
| G-03 | C | Plane Flatness | F-01 | Reference/measurement residual evidence |
| G-04 | C | Point Pair Dimensions | E-02 | distance/width/height/angle evidence |
| G-05 | C | Gap/Flush | E-01 | signed gap and flush evidence |
| G-06 | C | Volume | F-01, E-01 | reference-plane integrated volume evidence |
| G-07 | C | Cross-section Dimensions | E-01 | width/height-range evidence |
| G-08 | C | Datum Plane Raw-Height Deviation | F-01 | P2V/RMS overlays |
| G-09 | C | Min/max/tolerance parameters and Pass/Fail/Error results | None | PropertyGrid and Runner evidence |
| G-10 | C | Per-step metrics and overlays | None | Artifact Registry, Validation Set, Run Record |
| G-11 | N | Connected Region / Blob Finder for height-field masks | D-04, C-06 | Known connected-component fixture |
| G-12 | N | Region count, area, center, orientation, and bounding artifact outputs | G-11 | Per-region metrics and selected overlay |
| G-13 | N | Presence Check using explicit height/coverage features | G-11 or E-07 | Good/present and missing fixtures |
| G-14 | N | Fill Height per region against a reference surface | D-05, E-01 | Known fill-level synthetic fixture |
| G-15 | N | Aggregate `all regions accepted` result preserving per-region evidence | G-13 | Aggregate and child status parity |
| G-16 | P | Width/height/area outputs exist across several tools but not one detected-region dimension tool | G-12 | One region-dimension adapter |
| G-17 | N | Selected output enable/disable policy stored in recipe when execution semantics require it | G-10 | Disabled output remains declared and non-fabricated |
| G-18 | N | Tool-specific help example and expected overlay for every new tool | A-11 | Localized help and screenshot gate |

### H. Completeness, repeated cells, and presence workflow

Recommended model: `gpt-5.6-sol`

Reasoning effort: high

| ID | Status | Development item | Dependency | Closure evidence |
| --- | --- | --- | --- | --- |
| H-01 | C | `4 x 2` repeated Tab ROI authoring | E-06 | Existing repeat authoring evidence |
| H-02 | C | Completeness tool with rows, columns, pitch, and cell-shape contract | C-06, E-01 | Deterministic grid generation |
| H-03 | C | Per-cell finite-coverage metric | H-02, B-07 | Known missing-cell fixture |
| H-04 | C | Per-cell height statistic relative to reference | H-02, D-05 | Known height fixture |
| H-05 | C | Per-cell presence threshold and Pass/Fail | H-03, H-04 | Workbench/Runner parity |
| H-06 | C | Failed-cell count and aggregate completeness result | H-05 | Aggregate equals child statuses |
| H-07 | C | Per-cell colored overlay and stable cell identity | H-02 | Height Image and 3D display |
| H-08 | C | Previous/next failed-cell navigation | H-07, K-08 | UI selection verification |
| H-09 | N | Use detected/oriented region artifact as completeness input | E-11, G-12 | Typed upstream route |
| H-10 | C | Map existing Tab 1..8 names to cell results without replacing ordinary Thickness steps | H-02 | Stable recipe and output identities |
| H-11 | C | Good/bad completeness examples in Validation Set | I-01, H-05 | Two Good Pass, two Bad Fail, one separate Held-out Pass |
| H-12 | C | Completeness assistant that proposes height/coverage thresholds | I-04 | Exact sample/cell error table, draft Apply, development gate, Held-out replay |

### I. Sample evidence, threshold teaching, and correction

Recommended model: `gpt-5.6-sol`

Reasoning effort: high

| ID | Status | Development item | Dependency | Closure evidence |
| --- | --- | --- | --- | --- |
| I-01 | C | Validation Set stages same-grid C3D samples without running on add | None | Current validation verification |
| I-02 | C | Explicit Run across samples with progress/cancel | None | Current Validation Set UI |
| I-03 | C | Pass/Fail/Error filters, issue navigation, per-step metrics/overlays | None | Current failure-analysis verification |
| I-04 | C | Assign `Good`, `Bad`, and `Held-out` sample roles | I-01 | Role persistence without source mutation |
| I-05 | C | Per-step and per-region metric distribution over labeled samples | I-04 | Reproducible statistics |
| I-06 | C | Candidate threshold generation for one or two scalar limits | I-05 | Deterministic candidate set |
| I-07 | C | Confusion/error table with exact supporting sample IDs | I-06 | Counts reproduce from raw sample results |
| I-08 | C | Explicit threshold suggestion Review/Cancel/Apply | I-06, A-13 | Cancel non-mutation; Apply updates draft only |
| I-09 | C | Manual parameter correction after suggestion | I-08 | Ordinary PropertyGrid Apply commits values distinct from suggestion |
| I-10 | C | Held-out replay gate after applied correction | I-04, I-08 | Held-out data excluded from suggestion and then replayed |
| I-11 | C | Failure -> correction -> held-out evidence record | I-10 | Durable exact before/suggested/manual/after/Held-out record |
| I-12 | C | Sample balance, overlap, and insufficient-evidence warnings | I-05 | Release build `0/0`, Validation Set `72/72`, Runner contract `2.0`, controlled missing/imbalanced/overlap sets |
| I-13 | C | Threshold assistant for Thickness/Warpage first | I-08 | Explicit five-entry mapping matrix, Thickness end-to-end correction, Warpage typed proposal verification |
| I-14 | C | Threshold assistant for Presence/Completeness second | H-05, I-08 | Contract `2.1`; three exact Completeness mappings with worst-cell evidence |
| I-15 | C | Never auto-run or auto-apply after sample role/threshold edits | I-08 | Pending/evidence state plus Review/draft/manual/PropertyGrid command verification |

### J. Surface-model matching foundation

Recommended model: `gpt-5.6-sol`

Reasoning effort: high

| ID | Status | Development item | Dependency | Closure evidence |
| --- | --- | --- | --- | --- |
| J-01 | C | Identified `SurfaceModel` artifact contract | B-05 | Save/load and content identity |
| J-02 | P | Mesh import and fixed nominal/actual comparison exist, but are not a matching model | None | Current mesh/nominal evidence |
| J-03 | C | Model preparation step with sampling parameters | J-01 | Deterministic sampled-model hash |
| J-04 | C | Model point/triangle/normal validity checks | J-03, B-16 | Known-valid and invalid model fixtures |
| J-05 | C | Remove internal/redundant/unobservable model surfaces | J-03 | Noah-owned exact duplicate and explicit source-triangle selection; schema-1.2 active domain; controlled `15/15`; legacy byte parity `5/5` |
| J-06 | C | Scene preparation contract tied to SourceQualityReport | B-07 | Explicit prepared-scene identity |
| J-07 | C | Noah-owned deterministic farthest-point model samples; identified source-sample/source-triangle artifact; atomic persistence; WPF-neutral display-only position/normal overlay; no matching effect | J-03 | Committed Noah `7ed50ea`; vendored `Lib.ThreeD 2.8.12`; focused `15/15`; stable two-point IDs; save/reopen/tamper; legacy bytes `5/5` |
| J-08 | C | Pose-search executor returning rigid pose | J-03, J-06 | Known-pose synthetic fixture |
| J-09 | C | Explicit surface-coverage score semantics | J-08 | Occluded fixture with documented expected range |
| J-10 | C | Transformed-model scene overlay | J-08, C-19 | Workbench and screenshot evidence |
| J-11 | C | Match Pass/Fail limits distinct from raw score display | J-09 | PropertyGrid/Runner evidence |
| J-12 | C | Multiple-match result collection with stable identities | J-08 | Two stable ordered `5/5` results, zero shared scene claims, save/load/tamper evidence, Runner `14/14`, Workbench `6/6` |
| J-13 | C | Symmetry-aware pose equivalence | F-13, J-08 | Noah Tool, non-commutative axis fixture, direct/`x2`/`y3`/`z4` evidence, strict identity rejection, and legacy byte parity |
| J-14 | C | Bounded translation/rotation/search domain | F-14, J-08 | Runtime and false-positive comparison |
| J-15 | C | Matcher runtime and rejection reason evidence | J-08 | Per-stage timing and fail-closed reason |
| J-16 | C | Workbench/Runner pose, score, overlay, and hash parity | J-08 | Focused execution verification |

### K. Edge-supported matching and advanced diagnostics

Recommended model: `gpt-5.6-sol`

Reasoning effort: high

| ID | Status | Development item | Dependency | Closure evidence |
| --- | --- | --- | --- | --- |
| K-01 | C | Height Difference Edge, Line Fit, and Line diagnostics exist as inspection features | None | Current feature evidence |
| K-02 | C | Model 3D edge extraction for a SurfaceModel | J-03 | Stable topology boundary/crease artifact; non-manifold rejection; `21/21` focused fixture |
| K-03 | C | Scene 3D edge extraction for matching | J-06 | Stable complete-organized-grid height-step artifact; incomplete grid rejection; `21/21` focused fixture |
| K-04 | C | Acquisition viewpoint/direction metadata for edge orientation | B-12 | Explicit normalized SensorToScene source-frame contract; committed Noah Tool; linked facing/away/grazing artifact; no inference or score change; focused `5/5`, contract `17/17`, Workbench `16/16` |
| K-05 | C | Normal/edge-direction diagnostic overlay | B-16, K-02 | Known outward-normal fixture; identified overlay and `20/20` focused verification |
| K-06 | C | Separate surface and 3D-edge match scores | J-08, K-02, K-03 | Equal `100%` surface coverage separates to `100%` versus `0%` edge coverage; Runner `21/21`; parity `12/12` |
| K-07 | C | Independent thresholds for score components | K-06 | Separate PropertyGrid persistence and exact Runner/Workbench assessment parity; no weighted score |
| K-08 | C | False-positive review with original scene, samples, model, pose, and scores | K-06 | Retained `100/100` accepted versus `100/0` rejected comparison |
| K-09 | C | Multiple-match issue navigation | J-12 | Non-wrapping selector-synchronized Previous/Next; Workbench `10/10`; current Wide/Compact first/last state evidence |
| K-10 | C | Matching parameter experiment comparison without changing current published result | J-15 | One temporary Preview candidate, Published/Candidate Viewer switch, exact no-rerun Publish, stale/discard/reopen boundaries, parity `23/23` |
| K-11 | C | Matching performance budget over fixed fixtures | J-15 | Release `18/18`; fixed 256-sample, 11/61-candidate timing matrix |
| K-12 | O | Calibrated 2D intensity or extra-camera fusion in current phase | Separate scope approval | Not scheduled |

Algorithm ownership note: all reusable numerical algorithms belong in
OpenVisionLab-Vision-SDK public sealed Tools. Active Studio adapters consume
the exact vendored `OpenVisionLab.Vision3D 3.0.0` package. The schema-1
decreasing migration baseline contains zero Studio debt and `33` reviewed
boundaries; do not reintroduce arithmetic or expand a boundary ceiling.

### L. Results, validation, reporting, and diagnostics

Recommended model: `gpt-5.6-sol` for multi-file result-state work

Reasoning effort: medium

Use `gpt-5.6-terra` with low effort for narrow documentation/export-path
verification only.

| ID | Status | Development item | Dependency | Closure evidence |
| --- | --- | --- | --- | --- |
| L-01 | C | Selected output identity, freshness, value, unit, and status | None | Selected Tool output verification |
| L-02 | C | Show/Pin/Compare without fabricating a surface | None | Artifact Navigator verification |
| L-03 | C | Per-step metrics, overlays, status, message, and output SHA | None | Ordered Runner and Validation Set |
| L-04 | C | JSON/HTML/CSV Run Record | None | General Run Record verification |
| L-05 | C | Recent Run Record open and collision-safe bundle export | None | Run Record history verification |
| L-06 | C | Viewer/Runner comparison evidence | None | Current parity gates |
| L-07 | C | Local structured session logging | None | Logging verification |
| L-08 | P | Performance timing exists in reports/diagnostics but is not uniform for every future stage | None | Current performance evidence |
| L-09 | N | Standard per-step stage timing contract for preparation and matching | D-04 or J-08 | Timing fields in UI and Run Record |
| L-10 | N | Source Quality report included in Run Record | B-07 | Same source-quality identity in UI/Runner |
| L-11 | C | Threshold-correction evidence included in Run Record | I-11 | Schema `1.5`, exact before/suggested/manual/development/Held-out JSON/HTML and Workbench parity, `10/10` fail-closed projection checks |
| L-12 | N | Completeness per-cell result export | H-06 | HTML/CSV child rows |
| L-13 | C | Surface-match pose/score component export | J-16 | Schema `1.6`, focused `19/19`, JSON/HTML/CSV exact identified-value parity, direct CLI, NoMatch/legacy/fail-closed evidence |
| L-14 | N | One support/diagnostic bundle for recipe, log excerpt, source identity, quality report, and current result | B-07 | Bundle manifest and missing-sensitive-data policy |
| L-15 | P | Validation is local ordered sample replay, not production batch/history | None | Current boundary retained |
| L-16 | O | Plant database, long-term trend/SPC service, and retention policy | Product-scope decision | Not scheduled |

### M. Reliability, architecture, and verification

Recommended model: `gpt-5.6-sol`

Reasoning effort: medium for established focused tests, high for numerical,
renderer, or cross-module state changes

| ID | Status | Development item | Dependency | Closure evidence |
| --- | --- | --- | --- | --- |
| M-01 | C | 12-project solution parity and executable structure guard | None | `verify-code-structure.ps1` |
| M-02 | C | Core/Data/Tools/Runner remain runtime-neutral | None | Structure guard |
| M-03 | C | MVVM selected-tool, selection, Viewer workspace, and output-compare owners | None | Focused non-WPF verification |
| M-04 | C | WPF dialogs, AvalonDock, PropertyGrid flush, OpenGL, and pointer behavior remain View adapters | None | Code rules and structure evidence |
| M-05 | C | Current C3D GPU VBO/IBO and staged LOD performance baseline | None | Release matrix and pointer verification |
| M-06 | C | Current-source before/after screenshot discipline | None | Existing artifact checkpoints |
| M-07 | C | Deterministic synthetic whole-chain fixture | None | Synthetic Affine Plate verification |
| M-08 | C | Exact Thickness Coupon v1 Tab Thickness self-test | None | Generated model/Runner `8/8` |
| M-09 | N | SourceQualityReport malformed/edge-case fixture suite | B-07 | Finite/missing/topology cases |
| M-10 | C | Height Image coordinate and pointer verification suite | C-06 | Native-grid/hover checks, actual Windows pointer Review, 2D/3D edit parity, Apply/save/reopen, and Wide/Compact current-source evidence pass |
| M-11 | N | Cross-view selection atomicity suite | C-09 | No duplicate selection or execution |
| M-12 | N | `OrientedBox3D` schema/geometry/pointer/Runner suite | E-07 | Round-trip and degenerate-axis cases |
| M-13 | N | Preparation-tool before/after hash and source-immutability suite | D-04 | One suite per typed preparation tool |
| M-14 | N | Good/Bad/Held-out split and no-leakage suite | I-04 | Held-out excluded from suggestions |
| M-15 | N | Completeness known-cell golden suite | H-02 | Expected per-cell result matrix |
| M-16 | C | Surface-matching known-pose and false-positive suite | J-08 | Pose/score/rejection goldens |
| M-17 | N | Release performance matrix for full-size Height Image and matching | C-06 or J-08 | Fixed viewport/source repeated runs |
| M-18 | N | Accessibility names/tooltips for new icon-only or ambiguous controls | Each UI item | Automation-name verification |
| M-19 | N | Localization coverage for all new user-visible states | Each UI item | Korean/English current-source captures |
| M-20 | E | Owner unaided acceptance for every major workflow train | Current Release application | Owner replay record |

### N. Physical measurement credibility

Prerequisite first:

- trusted calibrated source data;
- declared unit and traceable scale;
- reference artifact or calibration procedure;
- repeat acquisition samples;
- production tolerance owner decision.

Do not recommend model spending until the required physical evidence exists.

| ID | Status | Development item | Dependency | Closure evidence |
| --- | --- | --- | --- | --- |
| N-01 | P | Software calibration capability and repeatability views exist | None | Current calibration center evidence |
| N-02 | E | Verify physical datum definition for Thickness | Owner/metrology input | Approved datum document |
| N-03 | E | Verify C3D raw-height to physical-unit mapping | Calibration data | Independent scale check |
| N-04 | E | Establish traceability chain | Calibration artifact | Traceability record |
| N-05 | E | Measurement uncertainty budget | N-02 to N-04 | Reviewed uncertainty document |
| N-06 | E | Gauge R&R / repeatability and reproducibility | Repeated operator/hardware data | GR&R result |
| N-07 | E | Production tolerance and guard-band decision | Process owner | Approved acceptance limits |
| N-08 | E | Certified claim wording and report boundary | N-05 to N-07 | Owner/legal/metrology review |

## Deliberately deferred commercial-platform features

These were visible in the supplied videos but are not current backlog items.
They require an explicit product-scope change.

| ID | Status | Deferred capability | Reason |
| --- | --- | --- | --- |
| O-01 | O | Camera discovery and connection | Current product is file-first |
| O-02 | O | Exposure, projector, acquisition-frame, and filter control | Sensor-specific acquisition scope |
| O-03 | O | Trigger Scan and Free Run | Would introduce live hardware/runtime state |
| O-04 | O | Assisted capture settings and SDK export | Acquisition product scope |
| O-05 | O | Multi-sensor grouping and alignment | Hardware system scope |
| O-06 | O | Stereo camera calibration and disparity reconstruction | Separate reconstruction product |
| O-07 | O | Pairwise/fusion reconstruction engine | Separate algorithm/runtime scope |
| O-08 | O | Encoder, conveyor, and motion synchronization | Production integration scope |
| O-09 | O | Ethernet/IP, Profinet, Modbus, ASCII, and PLC outputs | Industrial control scope |
| O-10 | O | Robot pose/gripping integration | Robot application scope |
| O-11 | O | HMI deployment | Runtime/platform scope |
| O-12 | O | Cloud, accounts, plant health, and fleet management | Platform scope |
| O-13 | O | Production database, retention, and SPC service | Platform/data-governance scope |
| O-14 | O | Arbitrary AI anomaly training | Conflicts with current deterministic rule target |

## How one development item is executed

Every `P` or `N` item must follow this loop.

### Step 1. Rebuild current status

- run `git status --short` and `git log --oneline -5`;
- read `AGENTS.md`, the current handoff, this master backlog, and the owning
  feature document;
- confirm that no newer evidence has completed, blocked, or reordered the
  item.

### Step 2. Select one bounded item

- select the first incomplete item whose dependencies pass;
- name included behavior;
- name excluded behavior;
- name external prerequisites;
- do not combine unrelated backlog IDs merely because they touch one screen.

### Step 3. Define evidence before code

Record:

- exact source/sample identity;
- typed input;
- typed output;
- parameter/selection ownership;
- expected success result;
- at least one expected failure/rejection;
- Workbench and Runner evidence requirement;
- UI before/after requirement when visible behavior changes;
- claim boundary.

### Step 4. Establish a real responsibility owner

Prefer:

- Core contract for durable identity and result types;
- Data adapter for file/serialization/source binding;
- Tools service/rule for deterministic preparation or inspection;
- Runner adapter for headless execution;
- non-WPF ViewModel/session for presentation state;
- WPF/OpenGL View only for dialogs, hosting, rendering, and pointer input.

Do not use a new partial file as the architectural boundary.

### Step 5. Preserve explicit lifecycle

Unless a newer approved product contract says otherwise:

- selection, visibility, view mode, palette, and layout never run inspection;
- parameter/ROI Apply changes the recipe or draft only;
- Preview calculates temporary selected-step evidence;
- Publish promotes an eligible current Preview;
- Run executes the recipe;
- Validation Set runs only through its explicit command;
- Save never silently runs inspection.

### Step 6. Implement in dependency order

For a new typed feature:

```text
Core contract
  -> validation and serialization
  -> deterministic Tools rule/service
  -> Runner execution
  -> ViewModel/session state
  -> View/render/pointer adapter
  -> commands and localization
```

### Step 7. Verify at meaningful checkpoints

Minimum:

- focused contract/math test;
- invalid/failure case;
- recipe save/reopen if persisted;
- Workbench/Runner parity if executable;
- source immutability;
- no hidden Preview/Run;
- current Release build for UI work;
- fresh wide and compact captures for visible changes;
- code-structure guard for ownership changes.

### Step 8. Owner replay when workflow changes

- use the current built EXE;
- provide the task goal, not click-by-click guidance;
- record where the operator hesitates or fails;
- reopen the item only when a stated acceptance criterion fails.

### Step 9. Durable closure

Update:

- owning feature document;
- this backlog item status;
- `AGENTS.md` current product target;
- next-session handoff;
- current artifact folder.

Use exactly one state:

```text
Status: Complete | Blocked | Incomplete
Scope:
Acceptance criteria:
Verification:
Evidence:
Boundary / next dependency:
```

## Definition of Done for a product slice

A slice is `Complete` only when:

- its selected backlog IDs are explicit;
- every dependency is satisfied;
- typed inputs/outputs and ownership are recorded;
- success and failure cases pass;
- recipe/source/result identity remains stable;
- explicit execution boundaries pass;
- focused verification passes;
- Workbench and Runner agree where execution exists;
- current-source UI evidence exists when visible behavior changed;
- no unresolved core TODO remains;
- the durable handoff names the next eligible item;
- the result does not overclaim physical calibration or commercial-platform
  scope.

## Current executable queue

R0 remains an external owner acceptance gate. It is not a global software
development pause after the owner's 2026-07-31 reprioritization. `B-07`,
`C-06`, `B-09`, `B-08`, `C-07`, `C-08`, `C-09`, `C-10`, `C-11`, `E-07`,
and `E-08` were completed ahead of that gate by explicit owner direction.
`B-11`, `B-16`, `F-14`, `J-01`, `J-03`, `J-04`, `J-06`, `J-08`, `J-09`,
`J-10`, `J-11`, `J-14`, `J-15`, `J-16`, `K-02`, `K-03`, `K-05`, `K-06`,
`K-07`, `K-08`, `K-10`, `K-11`, and `M-16` are now also complete.
The remaining order follows typed dependencies.

Execute only one queue item at a time.

Completed before this queue:

- `Workbench v4-2 Validate/Results linked evidence composition` — Complete;
- `Workbench v4-3 visual system and safe persisted layout` — Complete.

External acceptance, in parallel with software work:

- Human-owner Wide/Compact R0 replay | Prerequisite: product-owner operation
  and evidence; refreshed fixed-hash package already passes both
  `-ValidateOnly` checks | Recommended model: none until the owner evidence
  exists | Reasoning effort: none

Software queue:

The owner explicitly left the layout-only stream and completed item 11 on
2026-08-03. Continue from item 12.

1. `Library-Noah Surface Match kernel migration prerequisite` - Complete;
   committed Noah `7d1ad8721ca7aed9efa2a17beaa36409d7dbd718`, vendored
   `Lib.ThreeD 2.8.0`, and preserved exact Studio artifacts/parity.
2. `Library-Noah Tool contract and no-new-debt guard` - Complete; schema-1
   decreasing baseline now records `0` migration-debt files and `31` reviewed
   boundaries; structure passes `29/29`.
3. `Surface Match preparation/edge Tool migration` - Complete; committed
   Noah `46cfa0946bb4c23190b0dab75415ce2c637b4c41`, vendored `Lib.ThreeD
   2.8.1`, and exact persisted artifact parity.
4. `Local-median outlier filtering and Level Surface Tool migration` -
   Complete; committed Noah `3a2cbf8e7195d6f251dcafe6a9343b795d53fe79`,
   vendored `Lib.ThreeD 2.8.2`, exact focused report parity, and zero-signal
   Studio adapters.
5. `Nominal/actual mesh comparison and rigid-transform diagnostics migration`
   - Complete; committed Noah
   `4420c40d3179edc7703cfef6e0ea53ac898f8f3f`, vendored `Lib.ThreeD 2.8.3`,
   exact focused report parity, and zero-signal Studio adapters.
6. `Height-map summary/completeness/preparation Tool migration` - Complete;
   committed Noah `a64c31b1024f154e402d258ade4b70470ad50fb2`, vendored
   `Lib.ThreeD 2.8.4`, normalized focused parity `5/5`, and strict Studio
   adapters.
7. `Dual Surface Thickness and Height Deviation Tool migration` - Complete;
   committed Noah `ec8f1b3db57bea0065cd82735acb08111f88f3c0`, vendored
   `Lib.ThreeD 2.8.5`, exact focused parity `2/2`, and strict Studio adapters.
8. `Declared-normal quality and Landmark Correspondence Tool migration` -
   Complete; committed Noah `3ef2f52546a9187df465bf8973e26426c30f7634`,
   vendored `Lib.ThreeD 2.8.6`, exact focused parity `2/2`, and strict Studio
   adapters.
9. `Repeatability statistics Tool migration` - Complete; committed Noah
   `20963c12b50dfc0658110e2037961d3224feb2d6`, vendored `Lib.ThreeD 2.8.7`,
   exact Thickness/Aligned Point report parity, and strict Studio adapters.
10. `Validation statistics Tool migration` - Complete; committed Noah
    `0fe04bc967fa89918b3c6d937566cce56de69682`, vendored `Lib.ThreeD 2.8.8`,
    Validation Set `84/84`, normalized report difference `0`, and zero
    inventoried Studio numerical debt.
11. `J-12 Multiple-match result collection with stable identities` - Complete;
    committed Noah `4e301f481cac886f78425197314cd540b653473a`, vendored
    `Lib.ThreeD 2.8.9`, stable/disjoint two-object evidence, Runner `14/14`,
    Workbench `6/6`, and presentation-only result selection.
12. `K-09 Multiple-match issue navigation` - Complete; non-wrapping
    Previous/Next uses the existing retained selector state and Viewer route;
    Workbench `10/10`, current Wide/Compact state evidence, and no-execution
    boundaries pass.
13. `F-13 Symmetry declaration for later matching` - Complete; schema `1.1`
    owns explicit none/discrete model-axis declarations while undeclared
    schema `1.0` preserves exact content and JSON bytes; focused verification
    `34/34` and legacy byte parity `5/5` pass.
14. `J-13 Symmetry-aware pose equivalence` - Complete; committed Noah
    `f225fd2709de1dd1d0ecfe19b37315cb1f019ee4`, vendored
    `Lib.ThreeD 2.8.10`, focused `15/15`, direct and cyclic-axis fixtures,
    strict Studio adapter, legacy byte parity `5/5`, and unchanged matching
    execution.
15. `J-05 Remove internal/redundant/unobservable model surfaces` - Complete;
    committed Noah `55ea7a61bd1281294e91aa5366d2bafb509d3667`, vendored
    `Lib.ThreeD 2.8.11`, focused `15/15`, one immutable source topology and
    active domain shared by preparation/matching/edges/overlay, plus legacy
    byte parity `5/5`.
16. `J-07 Model key-point artifact and debug overlay` - Complete; committed
    Noah `7ed50ea37b3d7cb711c2afe698d209f9073e9217`, vendored
    `Lib.ThreeD 2.8.12`, stable source-sample/source-triangle identities,
    focused `15/15`, atomic persistence, and display-only overlay evidence.
17. `B-12 Acquisition/source provenance text and limitation notes` - Complete;
    explicit Available/Unavailable evidence, required limitations, draft/
    Apply isolation, save/reopen, legacy fallback, source reset, focused
    `14/14`, and current Wide/Compact/theme evidence.
18. `K-04 Acquisition viewpoint/direction metadata for edge orientation` -
    Complete; explicit normalized SensorToScene source-frame contract,
    committed Noah classification Tool, linked facing/away/grazing display
    evidence, legacy fallback, no inference, and unchanged score/assessment.
19. `L-13 Surface-match pose/score component export` - Complete; optional Run
    Record schema `1.6`, exact linked artifacts, JSON/HTML/CSV parity, matched
    and NoMatch behavior, focused `19/19`, direct CLI, no recomputation, and
    legacy schema-`1.5` read compatibility.
20. `PL-0002 Runner --help successful exit` - Complete; case-insensitive help
    exits `0` on stdout, invalid and incomplete commands retain exit `2` on
    stderr, the usage body is shared and byte-identical, Release is `0/0`,
    direct command matrix is `4/4`, and existing L-13 regression is `19/19`.
21. `PL-0004 immutable C3D loaded snapshot` - Complete; point, row, profile,
    full-map, display-density, and Viewer inspection sampling share the exact
    loaded sample identity; focused `14/14`, affected checks `113/113`, Debug
    and Release `0/0`, and refreshed Wide/Compact R0 validation pass.
22. `PL-0005 truthful alignment status summary` - Complete; most-downstream
    A3/A2/A1/legacy precedence, actual step state, state-change notification
    without execution, focused `35/35`, current Wide/Compact evidence, and
    refreshed R0 validation pass.
23. `PL-0006 release-policy reconciliation` - Complete; current GitHub
    zero-release/zero-tag state, source-owned version values, historical
    candidate boundary, future owner approval, and full release gate are now
    explicit. No release operation occurred.
24. `PL-0008 bounded Workbench run-log retention` - Complete; production
    `AppendLog` routes every event to durable rolling `OVLog` before keeping
    the newest 3,000 session entries, localized UI explains the boundary,
    focused `6/6` and hosted CI `#76` pass.
25. `PL-0009 compatible Add and input-route correctness` - Complete; shared
    typed-route resolution blocks unavailable transformed-only Add, avoids
    MeasurementResult auto-routing, previews the proposed contracts, and
    exposes non-executing legacy repair in the selected step.
26. `PL-0010 contextual add/configure/teach/repair path` - Complete; Add now
    activates Selected Tool, where one compact dual-ROI setup card keeps the
    compatible input, missing ROI/parameter/readiness state, one primary next
    action, and direct Tools return together without implicit execution.
27. `PL-0011 recipe health summary and issue navigation` - Complete; Flow
    exposes exact six-state counts and non-wrapping, presentation-only
    requirement navigation for the seventeen-step chain.
28. `PL-0013 first-use recipe/source/task setup` - Complete; one explicit
    surface now owns recipe identity, location, C3D source, optional compatible
    starter, confirmed remembered setup, stale validation, and reset without
    automatic execution.
29. `PL-0012 Tool Library search reset/visibility` - Complete; successful
    recipe open, new-recipe context creation, and compatible Add clear the
    search, while failed open/Add retain the visible query without execution.
30. `PL-0014 Studio language-popup theme and bounds` - Complete; the responsive
    width style retains the shared semantic ComboBox base, Wide and Compact
    labels remain visible, language persists, and inspection state is not
    executed or mutated.
31. `PL-0015 Thickness same-grid variant and 10-sample EXE baseline` -
    Complete; ten varied synthetic recipes match `Pass 4 / Fail 5 / Error 1`,
    repeated authoring falls from 33 to 11 actions, measured fixture targets
    pass, and controlled Error Run Records omit non-finite JSON metrics.
32. `PL-0016 Shell ordered Run for Thickness` - Complete; one explicit saved
    current-recipe action uses the shared ordered engine, writes schema `1.5`
    evidence into Results, preserves no-auto-run contracts, matches ten
    Runner records, and meets the current fixture-class interaction budget.
33. `PL-0017 coordinate-confident grid ROI teaching` - Complete; GridRectangle
    capture enters the existing Top orthographic fit, the teaching ribbon
    shows exact live row/column starts and counts before Apply, and actual
    one-drag reference/measurement target teaching preserves navigation,
    adjustment, explicit Apply/Cancel, and no-execution contracts.
34. `Large-C3D memory/performance target` - Blocked; prerequisite:
    representative maximum C3D input plus accepted process-memory and
    load-time limits. Recommended model: none until the prerequisite exists;
    reasoning effort: none.

## Documentation decision

This master backlog supersedes short feature lists in older commercial-review
documents when selecting future work. Older documents remain evidence for
their completed historical slices.

Current document authority is:

1. `AGENTS.md` for stable operating rules;
2. this master backlog for inventory, dependencies, and queue;
3. `OPENVISIONLAB_3D_NEXT_SESSION_HANDOFF.md` for the current handoff;
4. `OPENVISIONLAB_3D_NEXT_CHAT_HANDOFF_PROMPT_20260728.md` for the next-chat
   entry prompt;
5. `docs/archive/` and dated documents for state-at-the-time evidence.

Do not duplicate mutable Git status, unpushed-commit claims, or current
inventory tables in historical documents.

Do not mark a backlog item `C` based only on:

- a catalog label;
- a mock-up;
- a screenshot without behavior verification;
- XML/JSON validity alone;
- one successful execution without semantic result evidence;
- an adjacent feature with a similar name.

## Completion record

Status: Complete

Scope: Commercial-video direction expanded into a complete product workflow,
release-train order, detailed status inventory, dependency graph, evidence
gates, execution loop, Definition of Done, explicit deferred scope, and a
20-item first executable queue. At creation, the inventory contained 234
unique items: 65 Complete, 17 Partial, 127 New, 9 External prerequisite, and
16 Out of scope. Current execution status is maintained in the inventory
table above.

Acceptance criteria:

- full workflow from source identity through persistence -> pass;
- developed/partial/new/external/out-of-scope classification -> pass;
- commercial-video-derived functional backlog -> pass;
- dependencies and closure evidence per item -> pass;
- release-train and per-item development workflow -> pass;
- future-chat selection and durable closure rules -> pass.

Verification: Cross-checked against the current tool catalog, recipe selection
kinds, Viewer/Height Map state, Validation Set, current commercial-video
analysis, Inspection Workspace v3 handoff, and current completion documents.
Markdown whitespace and referenced handoff links are checked in the owning
documentation task.

Evidence:

- this document;
- `docs/OPENVISIONLAB_3D_COMMERCIAL_VIDEO_DIRECTION_AND_PRIORITY_20260727.md`;
- `artifacts/current/20260727-commercial-video-direction/`;
- current `AGENTS.md` and next-session handoff.

Boundary / next dependency: This planning task is complete. R0 remains an
external owner replay prerequisite. Physical metrology and deferred
commercial-platform scope remain unverified or out of scope.
