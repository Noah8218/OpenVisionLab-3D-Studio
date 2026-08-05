# OpenVisionLab 3D Current Session Handoff

Date: 2026-08-05
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
- The Library-Noah-to-Vision-SDK migration is complete in the working tree:
  package/bridge/structure `1/1`, `26/26`, and `29/29`; Runner/Shell `46/46`
  and `27/27`; bundled sample `8/8`; self-contained manifest `502/502`.
  Preserve `OPENVISIONLAB_3D_VISION_SDK_3_MIGRATION_20260805.md`. Hosted CI
  remains unverified until the changes are committed, pushed, and executed.

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
software slice. No such software item is currently selected.

## External Maintenance Blocker

`PL-0003` tracks the public-sample remote-retention re-audit described in
`OPENVISIONLAB_3D_SYNTHETIC_THICKNESS_SAMPLE_MIGRATION_20260728.md`.

The historical record reports old GitHub object accessibility and historical
Actions artifacts. Current external state is unverified. Completion requires
authorized GitHub access, a fresh remote audit, removal or documented retention
of affected artifacts, and the appropriate GitHub sensitive-data cleanup
outcome. Do not treat document age as proof that this blocker expired.

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
Scope: Current handoff and documentation-authority consolidation only
Acceptance criteria: one inventory owner; short current handoff; historical evidence retained; R0 and external retention blockers visible
Verification: documentation link/status/ledger/diff checks recorded by the 2026-08-05 documentation task
Evidence: AGENTS.md, docs/README.md, this file, archived handoff snapshots, PL-0003
Boundary / next dependency: product-owner Wide/Compact R0; authorized GitHub access for PL-0003
```
