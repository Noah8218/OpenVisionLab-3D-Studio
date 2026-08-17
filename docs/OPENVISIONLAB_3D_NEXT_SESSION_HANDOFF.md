# OpenVisionLab 3D Current Session Handoff

Date: 2026-08-17
Status: Current

This file is a short continuation snapshot. The canonical inventory and
development queue are in
`OPENVISIONLAB_3D_MASTER_DEVELOPMENT_WORKFLOW_AND_BACKLOG_20260727.md`.
Historical session detail through 2026-08-04 is preserved in
`archive/OPENVISIONLAB_3D_NEXT_SESSION_HANDOFF_HISTORY_THROUGH_20260804.md`.

## Product Identity

OpenVisionLab 3D Studio is a local, file-first, deterministic rule-based 3D
inspection workbench for height fields, point clouds, and meshes. It combines
source review, teaching, explicit Preview/Publish/Run, metrics, overlays,
validation samples, records, and recipe replay.

Camera, PLC, robot, cloud, account, and production-line platform scope remains
excluded. Raw-height and synthetic evidence are not calibrated metrology.

## Current Product State

- Inventory: read the canonical current table in the master backlog; this
  handoff intentionally does not duplicate its mutable counts.
- Inspection Workspace v3: `7/8`; `A-01` remains Partial.
- GoPxL-inspired Workbench v4: `3/3` complete.
- Studio numerical migration debt: zero under the current decreasing guard.
- Vendored Vision SDK package: `OpenVisionLab.Vision3D 3.0.0`, built from
  committed `OpenVisionLab-Vision-SDK` source
  `f34fdf912ff38fe20f36dbb063837e14b4f922b3`, SHA-256
  `F7324DC43ABF8E130D6F88C034287C192CFEA89E16A8A906A60F52DE341045B4`.
- B-12 acquisition provenance, K-04 acquisition direction/orientation, L-13
  Surface Match pose/score export, and PL-0002 Runner help exit behavior are
  complete for their documented software scopes.
- `PL-0004` is complete: all `C3DHeightGrid` reads and Viewer resampling use
  one immutable load snapshot. Debug/Release pass `0/0`, focused and affected
  checks pass `127/127`, and refreshed Wide/Compact R0 `-ValidateOnly` passes.
  Preserve
  `OPENVISIONLAB_3D_SHARED_CHAT_ANALYSIS_AND_C3D_LOAD_SNAPSHOT_20260806.md`.
- `PL-0005` is complete: the Studio header reports the most downstream actual
  A3/A2/A1/legacy step and its current `State`; state changes refresh the
  summary without execution. Debug/Release pass `0/0`, Tool Recipe teaching
  passes `35/35`, current Wide/Compact evidence is accepted, and refreshed R0
  `-ValidateOnly` passes. Preserve
  `OPENVISIONLAB_3D_TRUTHFUL_ALIGNMENT_STATUS_SUMMARY_20260806.md`.
- `PL-0006` is complete: the release policy reports the current GitHub
  zero-release/zero-tag state, treats `v0.1.0-rc.1` only as historical
  candidate evidence, and matches source-owned product/Host API/manifest/Run
  Record/recipe versions. No release, tag, asset, commit, or push was created.
  Preserve `OPENVISIONLAB_3D_RELEASE_VERSION_POLICY.md`.
- `PL-0007` is complete: selected recipe-step removal now requires an
  impact-aware themed confirmation, defaults to Cancel, removes only the exact
  step plus selections no remaining step uses, and fails closed during active
  Preview/Run-backed Preview/Surface Match/Validation execution. Focused
  checks pass `40/40`; current Wide/Compact English/Korean normal and held
  pointer-down evidence is accepted; refreshed R0 `-ValidateOnly` passes.
  Preserve `OPENVISIONLAB_3D_RECIPE_STEP_REMOVAL_SAFETY_20260815.md`.
- `PL-0008` is complete: every Workbench event is routed to rolling `OVLog`
  before the newest-first in-memory session projection is bounded at 3,000
  entries. Focused retention passes `6/6`; the affected regression, builds,
  structure, localized Wide/Compact UI, refreshed R0 validation, and GitHub
  Actions CI `#76` pass. Preserve
  `OPENVISIONLAB_3D_WORKBENCH_RUN_LOG_RETENTION_20260815.md`.
- The current Release EXE saved and reopened ten current-format recipes using
  the bundled Thickness Coupon C3D. JSON inspection passes `10/10` with `90`
  total steps. Only the eight-step Thickness baseline is ready; the other
  pending or incompatible chains are retained as authoring evidence. Preserve
  `OPENVISIONLAB_3D_EXE_RECIPE_AUTHORING_UX_STUDY_20260815.md` and
  `.proofline/issues/PL-0009.json` through `PL-0014.json`.
- `PL-0009` is complete: measurement Add now resolves a compatible typed
  artifact, generic HeightField consumers avoid MeasurementResult routing,
  transformed-only Add is disabled without a transformed input, and the
  proposed route is visible before insertion. Legacy mismatches remain
  loadable and show a bilingual selected-step repair action that opens Inputs
  and advanced routing without execution or mutation. Tool Recipe teaching
  passes `42/42`, Height Measurement Workbench `54/54`, affected regressions
  pass, full Release is `0/0`, and current Wide/Compact English/Korean EXE
  evidence is preserved under
  `D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260815-pl0009-compatible-tool-routing\`.
- `PL-0010` is complete: Add focuses Selected Tool and the contextual dual-ROI
  setup card keeps compatible input, requirements, readiness, one primary next
  action, and direct Tools return together without implicit execution.
- `PL-0011` is complete: Flow shows exact Ready/input/selection/parameter/stale
  Preview/Published counts and non-wrapping navigation to the exact owning
  step and requirement. Tool Recipe teaching passes `46/46`, Workbench docking
  `84/84`, Shell smoke options `37/37`, the current Release build is `0/0`,
  Wide/Compact English/Korean EXE evidence is accepted, and both fixed
  `-ValidateOnly` modes pass. Preserve
  `OPENVISIONLAB_3D_RECIPE_HEALTH_NAVIGATION_20260815.md` and the
  `20260815-pl0011-recipe-health-navigation` D-backed evidence root.
- `PL-0013` is complete: Recipe Center keeps name, folder, C3D source,
  optional Empty/Thickness starter, exact target, validation, remembered setup,
  and Reset together before explicit Create. Restore and Reset do not execute
  or mutate a recipe. Focused checks pass `49/49`, `46/46`, `84/84`, and
  `39/39`; Debug/Release are `0/0`; actual Release EXE empty and Thickness
  save/reopen pass; Wide/Compact localized UI and fixed `-ValidateOnly` pass.
  Preserve `OPENVISIONLAB_3D_FIRST_USE_RECIPE_SETUP_20260816.md` and the
  `20260815-pl0013-first-use-setup` D-backed evidence root.
- `PL-0012` is complete: Tool Library search clears only after a successful
  recipe open, new-recipe context creation, or compatible Add. Failed
  open/Add retains the query and no transition executes inspection. Tool
  Recipe teaching passes `50/50`, Workbench docking `84/84`, Debug/Release
  are `0/0`, actual Release Compact English and Wide Korean evidence passes,
  and both fixed `-ValidateOnly` modes pass. Preserve
  `OPENVISIONLAB_3D_TOOL_LIBRARY_SEARCH_CONTEXT_20260816.md` and the
  `20260816-pl0012-tool-search-context` D-backed evidence root.
- `PL-0014` is complete: the responsive language-selector style now retains
  the shared semantic ComboBox base, and Compact uses bounded margin/padding
  so `한` and `EN` remain visible. Debug/Release are `0/0`, Workbench docking
  passes `87/87`, actual Release Wide/Compact popup, selection, focus, and
  hover evidence passes, language survives a normal restart, inspection state
  remains unchanged, and both fixed `-ValidateOnly` modes pass. Preserve
  `OPENVISIONLAB_3D_LANGUAGE_SELECTOR_POPUP_20260816.md` and the
  `20260816-pl0014-language-popup` D-backed evidence root.
- `PL-0015` is complete: ten varied synthetic C3D Thickness recipes created
  through the actual Release Shell match `Pass 4 / Fail 5 / Error 1` under
  ordered Runner replay. Recipe Center's grid-compatible variant preserves
  direct-grid ROI identities and coordinates, steps, routes, and parameters,
  safely rebinds the new same-size C3D identity, saves a separate recipe, and
  invokes no Preview, Publish, Run, or Validation. Repeated authoring fell
  from 33 to 11 actions. The fixture targets pass at variant ready no later
  than `1.916 s`, replay at or below `244.75 ms`, and Thickness step at or
  below `15.02 ms`. Controlled Error Run Records now omit non-finite metrics.
  Preserve
  `OPENVISIONLAB_3D_THICKNESS_10_SAMPLE_EXE_UX_PERFORMANCE_STUDY_20260817.md`,
  `.proofline/issues/PL-0015.json` through `PL-0017.json`, and the
  `20260817-thickness-10-recipe-ux-performance` D-backed evidence root.
- `PL-0016` is complete: Validate runs a saved supported current recipe through
  the same ordered graph engine as Runner, writes schema `1.5` evidence, and
  immediately feeds Results. Editing invalidates evidence and requires save;
  open, Preview, Publish, compatible variant, save, and reopen do not auto-run.
  Ten actual Release EXE runs match `Pass 4 / Fail 5 / Error 1`, expected state
  `10/10`, and Runner status/metrics/step/output/hash parity `10/10`. Ordered
  duration is p50 `468.425 ms`, p95 `533.351 ms`, and max `533.351 ms`, within
  the current sample-class p95 `600 ms` and max `750 ms` regression guards.
  Preserve `OPENVISIONLAB_3D_SHELL_ORDERED_THICKNESS_RUN_CLOSURE_20260817.md`,
  `.proofline/issues/PL-0016.json`, and the
  `20260817-pl0016-shell-ordered-thickness-run` D-backed evidence root.
- The Library-Noah-to-Vision-SDK migration is complete on main commit
  `8400b89a788b2a59affb713833001fff15c6aff0`:
  package/bridge/structure `1/1`, `26/26`, and `29/29`; Runner/Shell `46/46`
  and `27/27`; bundled sample `8/8`; self-contained manifest `502/502`.
  Preserve `OPENVISIONLAB_3D_VISION_SDK_3_MIGRATION_20260805.md`. GitHub
  Actions run `31012735944` completed successfully for that commit.

Run `git status --short` and `git log --oneline -5` for live repository state.
This document does not carry an unpushed-commit or dirty-worktree claim.

## Current Acceptance Priority

Product-owner unaided Wide and Compact R0 remains the next acceptance task.
It is required for `A-01`, Workspace v3 `8/8`, and human-usability or release
acceptance.

- Prerequisite: owner operation and observer record.
- Procedure: `OPENVISIONLAB_3D_HUMAN_OWNER_R0_EXECUTION_20260729.md`.
- Launcher: `../scripts/start-human-owner-r0.ps1`.
- Automated `-ValidateOnly` does not close the owner gate.
- The owner reported the observed Wide run as pass. The Compact run was
  interrupted by the recipe-authoring study and is not counted as passed.
- Recommended model: none.
- Reasoning effort: none.

Missing R0 does not prevent a newly approved dependency-ready deterministic
software slice.

## Current Software Queue

`PL-0015` and `PL-0016` are complete.

1. `PL-0017 coordinate-confident grid ROI teaching` is the selected software
   slice. Recommended
   model: `gpt-5.6-sol`; reasoning effort: `medium`.

The large-C3D memory/performance candidate remains blocked until a
representative maximum C3D input and accepted process-memory/load-time limits
are supplied. Recommended model: none until the prerequisite exists;
reasoning effort: none.

## External Maintenance Blocker

`PL-0003` tracks the public-sample remote-retention cleanup described in
`OPENVISIONLAB_3D_SYNTHETIC_THICKNESS_SAMPLE_MIGRATION_20260728.md`.

The authenticated audit and 57-item retired-lineage Actions cleanup are
complete, while all 14 sanitized-lineage artifacts were preserved. GitHub
Support ticket `#4633618` remains Open; the old object still returned HTTP
200 in the parent and fork network immediately after submission. Completion
requires GitHub processing and a fresh resulting reachability check.

## Required Reading For The Next Task

1. `../AGENTS.md`.
2. `README.md` for the documentation map.
3. `OPENVISIONLAB_3D_NEXT_CHAT_HANDOFF_PROMPT_20260728.md`.
4. `OPENVISIONLAB_3D_MASTER_DEVELOPMENT_WORKFLOW_AND_BACKLOG_20260727.md`.
5. The active contract or closure document for the requested scope.

For algorithm work, also read
`OPENVISIONLAB_3D_VISION_SDK_TOOL_CONTRACT_AND_MIGRATION_BASELINE_20260805.md` and
inspect the currently vendored public API before implementation.

## Preserved Boundaries

- Explicit Preview, Publish, Run, and Validation.
- No automatic execution from parameter, visibility, layout, or restored-state
  changes.
- Source/result separation and stable identity-based replay.
- OpenVisionLab Vision SDK ownership for new numerical work.
- Current Wide/Compact and semantic-theme gates for visible UI changes.
- D-backed local test evidence and dynamically selected leftmost-monitor EXE
  placement.
- No commit, push, merge, release, or modification of the 2D reference repo
  without explicit user authorization.

## Documentation Consolidation

The 2026-08-05 documentation pass:

- established the master backlog as the single inventory/queue owner;
- replaced append-only active handoffs with short current entry points;
- archived the former handoff and project-instruction chronologies without
  deleting their evidence;
- added `docs/README.md` as the documentation map;
- retained current user documentation and verified local links/script paths;
- registered the external public-sample retention blocker as `PL-0003`.

## Completion Record

```text
Status: Complete
Scope: Complete PL-0009 compatible Add, visible typed-route proposal, legacy mismatch repair entry, and explicit no-auto-execution behavior
Acceptance criteria: incompatible Add unavailable -> pass; HeightField consumer avoids MeasurementResult route -> pass; proposed contracts visible -> pass; valid save/reopen -> pass; legacy repair opens exact input editor -> pass; Add/repair do not Preview/Publish/Run -> pass
Verification: Debug Shell 0/0; Tool Recipe teaching 42/42; Height Measurement Workbench 54/54; Recipe Manager + WPG 40/40; Tool Recipe selections 29/29; Artifact Navigator pass; full Release 0/0; actual Release EXE Wide/Compact English/Korean on DISPLAY2; git diff --check pass
Evidence: docs/OPENVISIONLAB_3D_EXE_RECIPE_AUTHORING_UX_STUDY_20260815.md, .proofline/issues/PL-0009.json, D:/OpenVisionLab-TestData/OpenVisionLab-3D-Studio/artifacts/current/20260815-pl0009-compatible-tool-routing/, this file
Boundary / next dependency: the ten original recipes remain reproduction evidence; PL-0010 workflow consolidation is recorded below; owner Compact R0 remains external; large-C3D optimization still needs representative input and accepted budgets; PL-0003 waits on GitHub Support #4633618
```

```text
Status: Complete
Scope: Complete PL-0010 Add-to-Selected focus and the contextual dual-ROI setup path for input, requirements, readiness, teaching, catalog return, save/reopen, and safe recipe reset
Acceptance criteria: Add opens Selected Tool without execution -> pass; compatible input plus both ROI and parameter/readiness requirements visible -> pass; exactly one primary next action and direct Tools return -> pass; Reference and Measurement Missing -> Drawing -> Review -> Applied reachable in Compact with Viewer visible -> pass; save/reopen restores setup without execution -> pass; new-recipe reset clears setup without execution -> pass
Verification: Debug Shell 0/0; Tool Recipe teaching 43/43; Height Measurement Workbench 56/56; Workbench docking 83/83; final full Release 0/0; actual current Release EXE on DISPLAY2 at physical Compact 1280x760 and Wide 1920x1032, including English/Korean and two-ROI authoring; git diff --check pass
Evidence: docs/OPENVISIONLAB_3D_EXE_RECIPE_AUTHORING_UX_STUDY_20260815.md, .proofline/issues/PL-0010.json, D:/OpenVisionLab-TestData/OpenVisionLab-3D-Studio/artifacts/current/20260815-pl0010-contextual-step-setup/, this file
Boundary / next dependency: PL-0011 recipe-wide health navigation is completed below; PL-0014 owns the independently observed language-popup theme leak; owner Compact R0 remains external
```

```text
Status: Complete
Scope: Complete PL-0011 exact recipe-health counts and non-wrapping, localized, presentation-only requirement navigation
Acceptance criteria: every step classified exactly once across six states -> pass; exact owner/requirement reveal without wrapping or mutation -> pass; seventeen-step Wide/Compact review with reachable actions and no clipped required text -> pass
Verification: Debug and Release 0/0; Tool Recipe teaching 46/46; Workbench docking 84/84; Shell smoke options 37/37; current Release EXE Wide/Compact English/Korean on DISPLAY2; last-requirement and held pointer-down states accepted; fixed Wide/Compact -ValidateOnly pass; git diff --check pass
Evidence: docs/OPENVISIONLAB_3D_RECIPE_HEALTH_NAVIGATION_20260815.md, .proofline/issues/PL-0011.json, D:/OpenVisionLab-TestData/OpenVisionLab-3D-Studio/artifacts/current/20260815-pl0011-recipe-health-navigation/, this file
Boundary / next dependency: product-owner unaided Wide/Compact R0 remains external; its PL-0013 follow-up is completed in the record below
```

```text
Status: Complete
Scope: Complete PL-0013 one-surface recipe identity, folder, C3D source, optional compatible starter, confirmed remembered setup, stale validation, and reset behavior
Acceptance criteria: all four inputs visible before explicit Create -> pass; confirmed setup save/reload/reopen with no restore action -> pass; stale paths explained and Create disabled -> pass; Reset safe and action-free -> pass; Wide/Compact localized focus/popup/disabled/pressed states themed, reachable, and on selected monitor -> pass
Verification: Debug and Release 0/0; Recipe Manager + WPG 49/49; Tool Recipe teaching 46/46; Workbench docking 84/84; Shell smoke options 39/39; actual Release EXE empty and Thickness save/reopen pass; fixed Wide/Compact -ValidateOnly pass; git diff --check pass
Evidence: docs/OPENVISIONLAB_3D_FIRST_USE_RECIPE_SETUP_20260816.md, .proofline/issues/PL-0013.json, D:/OpenVisionLab-TestData/OpenVisionLab-3D-Studio/artifacts/current/20260815-pl0013-first-use-setup/, this file
Boundary / next dependency: product-owner unaided Wide/Compact R0 remains external; PL-0012 is completed in the record below
```

```text
Status: Complete
Scope: Complete PL-0012 success-boundary Tool Library search reset with failure retention and no automatic execution
Acceptance criteria: successful recipe/new/Add context leaves no hidden stale filter -> pass; failed open/Add retains visible query -> pass; deterministic behavior invokes no Preview/Publish/Run -> pass; Wide/Compact localized current-build states remain reachable and themed -> pass
Verification: Debug and Release 0/0; Tool Recipe teaching 50/50; Workbench docking 84/84; actual Release EXE Compact English and Wide Korean on DISPLAY2; fixed Wide/Compact -ValidateOnly pass; git diff --check pass
Evidence: docs/OPENVISIONLAB_3D_TOOL_LIBRARY_SEARCH_CONTEXT_20260816.md, .proofline/issues/PL-0012.json, D:/OpenVisionLab-TestData/OpenVisionLab-3D-Studio/artifacts/current/20260816-pl0012-tool-search-context/, this file
Boundary / next dependency: PL-0014 is completed in the record below; product-owner unaided Wide/Compact R0 remains external
```

```text
Status: Complete
Scope: Complete PL-0014 shared semantic language-selector popup, bounded Wide/Compact labels, language persistence, and no inspection-state mutation
Acceptance criteria: Korean/English normal, open, selected, keyboard-focus, pointer-hover, click/open, and disabled semantics remain dark and legible -> pass; Wide/Compact controls and popups stay bounded -> pass; language updates and survives normal restart without recipe/source/ROI/result/Preview/Publish/Run mutation -> pass
Verification: Debug and Release 0/0; Workbench docking 87/87; actual current Release EXE Wide 1920x1040 and Compact 1280x760 on DISPLAY2 with Korean/English popup, focus, selection, and hover evidence; refreshed fixed Wide/Compact -ValidateOnly pass; git diff --check pass
Evidence: docs/OPENVISIONLAB_3D_LANGUAGE_SELECTOR_POPUP_20260816.md, .proofline/issues/PL-0014.json, D:/OpenVisionLab-TestData/OpenVisionLab-3D-Studio/artifacts/current/20260816-pl0014-language-popup/, this file
Boundary / next dependency: at PL-0014 closure no dependency-ready software slice was selected; the PL-0015 record below supersedes that priority state; product-owner unaided Wide/Compact R0 remains external; large-C3D work still requires representative input and accepted budgets
```

```text
Status: Complete
Scope: Complete PL-0015 ten-sample synthetic Thickness EXE study, safe same-grid recipe variants, measured workflow/replay targets, and controlled-Error Run Record serialization
Acceptance criteria: ten inputs/previews/recipes -> pass; expected Pass 4 / Fail 5 / Error 1 -> pass; repeated workflow <=12 actions -> pass at 11; variant ready <=2.5 s -> pass at <=1.916 s observed; replay <=250 ms -> pass at <=244.75 ms; step <=20 ms -> pass at <=15.02 ms; current Wide/Compact review -> pass within documented capture limits
Verification: Release 0/0; Recipe Manager/WPG 52/52; height measurement 56/56; artifact-owned Runner 19/19; ten ordered replays matched; sample 9 controlled Error record serialized; git diff --check pass
Evidence: docs/OPENVISIONLAB_3D_THICKNESS_10_SAMPLE_EXE_UX_PERFORMANCE_STUDY_20260817.md; .proofline/issues/PL-0015.json; D:/OpenVisionLab-TestData/OpenVisionLab-3D-Studio/artifacts/current/20260817-thickness-10-recipe-ux-performance/
Boundary / next dependency: synthetic raw-height is not physical metrology; owner R0 remains external; PL-0016 owns Shell full-Run parity and PL-0017 owns coordinate-confident grid ROI teaching
```

```text
Status: Complete
Scope: Complete PL-0016 Shell explicit ordered Run for saved supported Thickness recipes, shared Runner projection, Run Record/Results routing, no-auto-run invalidation, current UI states, and ten-sample interaction budget
Acceptance criteria: valid saved Thickness enables Run and exact invalid/unsupported reasons disable it -> pass; Pass/Fail/Error status, metrics, step/output/content identities and Run Record match Runner -> pass 10/10; edits/Preview/Publish/save/reopen do not auto-run -> pass; Wide/Compact current-build ready/pressed/result states are themed, readable, bounded, and keyboard reachable -> pass
Verification: Release 0/0; ordered Run 13/13; Tool Recipe teaching 50/50; Run Record history 12/12; Recipe Manager/WPG 52/52; Shell options 40/40; ten actual EXE Runs and expected states 10/10; Runner parity 10/10; p95 533.351 ms <= 600 ms and max 533.351 ms <= 750 ms; Wide/Compact monitor intersection and current direct-click review pass; git diff --check pass
Evidence: docs/OPENVISIONLAB_3D_SHELL_ORDERED_THICKNESS_RUN_CLOSURE_20260817.md; .proofline/issues/PL-0016.json; D:/OpenVisionLab-TestData/OpenVisionLab-3D-Studio/artifacts/current/20260817-pl0016-shell-ordered-thickness-run/
Boundary / next dependency: synthetic raw-height is not physical metrology; product-owner unaided R0 remains external; PL-0017 owns coordinate-confident grid ROI teaching; large-C3D claims require a representative maximum input and accepted budgets
```
