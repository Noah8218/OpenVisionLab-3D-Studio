# OpenVisionLab 3D Surface Match Parameter Experiment Comparison

Date: 2026-08-01

Status: Complete

## Outcome

`K-10` is complete. A published Surface Match result can now remain as the
immutable operator baseline while one explicitly requested parameter Preview
is held as a temporary candidate. The operator can switch the Viewer between
Published and Candidate evidence, compare coverage/RMSE/decision/hash, publish
the exact candidate without re-running it, or discard it and return to the
published baseline.

This is an OpenVisionLab workflow solution, not a GoPxL screen copy. The
operator problem was the risk of losing the known result while experimenting.
The adapted commercial principle is linked configuration, Viewer, and evidence
with an explicit next action. The implementation keeps the existing graphite
theme, OpenVisionLab terminology, responsive rail, and explicit execution
contracts.

## Operator workflow

1. Start from identified Published Surface Match evidence.
2. Edit search or acceptance values in the existing PropertyGrid.
3. Apply commits only recipe parameters; it does not execute inspection.
4. Preview explicitly creates exactly one temporary Candidate.
5. Switch Published/Candidate in the same Viewer without mutating either.
6. Publish explicitly promotes the exact Preview artifact without another
   match execution, or discard restores Published.
7. A parameter Apply after Preview marks the candidate stale, disables Publish,
   and restores the Published view until Preview is run again.

## State and side-effect contract

| Action | Published | Candidate | Viewer | Recipe | Execution |
| --- | --- | --- | --- | --- | --- |
| Apply parameters | unchanged | stale if present | Published | draft values updated | none |
| Preview | unchanged | replaced by one current candidate | Candidate | unchanged by execution | one explicit match |
| Show Published | unchanged | retained | Published | unchanged | none |
| Show Candidate | unchanged | retained | Candidate | unchanged | none |
| Publish | exact candidate promoted | cleared | Published | published evidence updated | no re-run |
| Discard | unchanged | cleared | Published | unchanged | none |
| Save/reopen | persisted recipe only | not persisted | normal restored view | restored after validation | none |

## Ownership

- `SurfaceMatchExperimentSession` owns only Published/Candidate/stale
  comparison state.
- `ToolWorkbenchViewModel.SurfaceMatchExperiment` is a thin Workbench command
  adapter. It is not a numerical or architectural replacement for the matcher.
- Preview calls the existing shared `SurfaceMatchEvaluationExecutor.Execute`
  boundary used by Runner and Workbench.
- No pose, coverage, nearest-neighbor, transform, distance, or acceptance math
  was added or changed in K-10.
- This K-10 checkpoint originally consumed `Lib.ThreeD [2.7.9]` and retained
  unchanged legacy matching adapters. The later 2026-08-01 migration
  supersedes that temporary exception: `Lib.ThreeD 2.8.0` now owns the
  pose-search and coverage arithmetic, while the same Studio entry points are
  strict validation/adaptation boundaries.

During K-10, the `C:\Git\Library-Noah` working tree was inspected read-only at
`584f233e33dc36da8b6039dbb5dcbea82015ee94`; it contains unrelated user changes
and was not modified. The completed migration used a clean dedicated worktree,
committed exact source, packed that commit, vendored the package and checksum,
and preserved Studio parity. See
`docs/OPENVISIONLAB_3D_SURFACE_MATCH_NOAH_MIGRATION_20260801.md`.

## UI and layout

- The comparison card is located with the Selected Tool, next to the same
  Viewer evidence it controls.
- Wide keeps `Inspection Flow`, `Inspection Tools`, and `Selected Tool`.
- Compact adaptively uses `Flow`, `Tools`, and `Selected`; Korean keeps the
  meaningful localized labels.
- New status, evidence labels, buttons, help text, tooltips, and accessible
  names are localized for English/Korean where this slice owns the text.
- Icon-only discard and output actions use the existing symbol library and have
  localized tooltips, accessible names, and stable AutomationIds.
- The card reuses graphite semantic brushes and shared controls. No competitor
  colors, assets, proportions, or screen topology were copied.

Fresh current-build review covered:

- Wide `1920 x 1040`;
- Compact `1280 x 760`;
- Compact Korean `1280 x 760`;
- Compact keyboard-focus and pointer-hover state;
- normal, selected, disabled, focus, and hover states;
- no applicable popup or validation-error state for the changed controls.

No overlap, required-text clipping, out-of-pane control, unreachable action, or
unintended horizontal/nested scroll bar was found.

## Verification

- Release solution build: `0` warnings, `0` errors.
- Surface Match Workbench/Runner parity: `23/23`.
- Surface matching foundation: `34/34`.
- Surface Match acceptance: `14/14`.
- isolated Release performance budget: `18/18`.
  - bounded 11 candidates: median/p95/max
    `10.282/12.820/17.399 ms` within `40/80/150 ms`;
  - broad 61 candidates: median/p95/max
    `40.485/65.976/74.632 ms` within `180/350/700 ms`.
- Surface-edge matching: `21/21`.
- Surface-edge diagnostics/review: `20/20`.
- Library-Noah vendored-package verification: `7/7`.
- Workbench docking: `76/76`.
- Inspection Workspace: `63/63`.
- Validation Set: `84/84`.
- Height distribution: `25/25`.
- Artifact Navigator: `31/31`.
- Shell smoke command-line contract: `28/28`.
- Structure guard: `17/17`.
- refreshed R0 fixed hashes: Wide and Compact `-ValidateOnly` both pass
  without launching the application.

All generated K-10 reports and captures are physically stored under
`D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio`; the repository path is a
verified junction. Actual EXE captures were placed on the dynamically selected
leftmost monitor, `\\.\DISPLAY2`, bounds `-1920,360,1920,1080`, and every
captured window rectangle intersected it.

## Evidence

- `artifacts/current/20260731-surface-match-experiment-comparison/before/`
- `artifacts/current/20260731-surface-match-experiment-comparison/after/`
- `artifacts/current/20260731-surface-match-experiment-comparison/verification/`
- `verification/surface-match-workbench-parity.txt`
- `verification/ui-layout-review.txt`
- `verification/leftmost-monitor-placement.txt`
- `verification/library-noah-boundary-audit.txt`
- `verification/d-drive-storage-migration.txt`

The storage record intentionally documents one limitation: the initial
cross-volume move did not prove pre-delete source/target SHA equivalence because
Windows PowerShell 5.1 lacked the attempted `Path.GetRelativePath` API and the
errors were non-terminating. Required final reports and after captures were
therefore regenerated directly on D. The copied before PNGs were decoded,
dimension-checked, and hashed after migration; no unsupported pre-delete hash
claim is made.

## Completion record

Status: Complete

Scope: K-10 Published/Candidate Surface Match parameter experiment comparison,
explicit Preview/Publish/discard boundaries, responsive localized UI, and
current-build evidence.

Acceptance criteria: one candidate per explicit Preview -> pass `23/23`;
Published preserved until explicit Publish -> pass; exact no-rerun promotion ->
pass; stale/discard/save/reopen boundaries -> pass; Wide/Compact theme and
layout integrity -> pass; no new Studio matching math -> pass.

Verification: Release `0/0`; matching `34/34`; acceptance `14/14`; performance
`18/18`; edge `21/21`; edge review `20/20`; Noah package `7/7`; docking `76/76`;
Inspection Workspace `63/63`; Validation Set `84/84`; height `25/25`; Artifact
Navigator `31/31`; smoke options `28/28`; structure `17/17`; both R0
`-ValidateOnly` modes pass.

Evidence: `artifacts/current/20260731-surface-match-experiment-comparison/` and
this document.

Boundary / next dependency: Human-owner unaided Wide/Compact R0 remains
external and is not replaced by automation. The required Library-Noah kernel
migration is complete in `Lib.ThreeD 2.8.0`; `J-12 Multiple-match result
collection` is the next dependency-ready matching slice and must extend Noah
first for any new numerical behavior. No cross-hardware,
production-throughput, physical-metrology, or human-usability claim is
included.
