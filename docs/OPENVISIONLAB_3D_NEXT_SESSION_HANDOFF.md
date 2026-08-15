# OpenVisionLab 3D Current Session Handoff

Date: 2026-08-15
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
- Recommended model: none.
- Reasoning effort: none.

Missing R0 does not prevent a newly approved dependency-ready deterministic
software slice.

## Current Software Priority

The next eligible maintenance item is bounded Workbench run-log retention.
Define an explicit in-memory retention and export boundary while preserving
durable `OVLog` evidence and without changing Preview/Publish/Run behavior.

- Recommended model: `gpt-5.6-terra`.
- Reasoning effort: `low`.

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
Scope: Refresh the current handoff through recipe-step removal safety
Acceptance criteria: PL-0007 safety scope visible; next maintenance item identified; R0 and PL-0003 external dependencies current
Verification: Recipe Manager + WPG 40/40; Shell command line 35/35; Debug/Release and structure pass; Wide/Compact localized and pointer-state UI evidence accepted; refreshed R0 ValidateOnly and documentation checks pass
Evidence: docs/OPENVISIONLAB_3D_RECIPE_STEP_REMOVAL_SAFETY_20260815.md, .proofline/issues/PL-0007.json, this file
Boundary / next dependency: product-owner Wide/Compact R0; bounded Workbench run-log retention is the next software maintenance item; PL-0003 waits on GitHub Support #4633618
```
