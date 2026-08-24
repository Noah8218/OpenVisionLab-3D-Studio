# GridCircle Selection Closure

Date: 2026-08-24
Issue: `PL-0048 / E-14`
Status: Complete

## Scope

OpenVisionLab 3D Studio now owns one durable `GridCircle` selection for a
bound C3D height-field grid. The payload stores an integer center row and
column plus a finite radius in cell-center units. The complete circle must
remain inside the exact source grid.

The Viewer creates or replaces the geometry from a center pick and boundary
pick. Workbench numeric center-row, center-column, and radius edits stay
transient until explicit Apply. Enter applies and Esc cancels through the
existing teaching contract. The current authoring-only pseudo-step declares
the role explicitly; no inspection tool consumes the circle or produces a
mask implicitly.

## Contract And Compatibility

- Generic Tool Recipe schema is `1.6` for `GridCircle`.
- Schemas `1.0` through `1.5` retain their bounded prior meanings and reject a
  circle payload instead of reinterpreting it.
- Validation rejects a missing or mixed payload, radius below `1`, non-finite
  radius, out-of-grid footprint, stale source binding, wrong input route, and
  undeclared consumer.
- The E-13 compatibility matrix now contains `16` tools and `21` role rows.
- Save/reopen preserves the selection ID, source/frame identity, center, and
  exact radius without Preview, Publish, or Run.

## Verification

| Gate | Result |
| --- | --- |
| Release solution build | Pass, 15 projects, 0 warnings / 0 errors |
| Shell and Runner selection contract | Pass, `49/49`; GridCircle subset `9/9` |
| Workbench teaching | Pass, `55/55` |
| Viewer teaching ViewModel | Pass, `30/30` |
| Inspection Workspace selection | Pass, `67/67` |
| Ordered Run | Pass, `16/16` |
| Workbench docking/theme audit | Pass, `98/98`; all `316` current button declarations audited |
| Validation Set | Pass, exit `0` |
| Standard .NET test facade | Pass, `2/2` |
| Code structure | Pass, `68/68` |
| `git diff --check` | Pass |

Actual Release EXE review used the dynamically selected leftmost monitor. Wide
`1920 x 1040` and Compact `1280 x 760` screenshots passed the built-in image
quality check and intersected that monitor. Actual pointer/keyboard review
covered applied circle display, Replace, Top orthographic transition, enabled
numeric editing, candidate-versus-applied overlays, Tab commit, Esc cancel,
disabled restoration, and Compact Selected Tool reachability. The current
graphite theme remained legible with no required circle label or value clipped.

Evidence root:
`D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\20260824-e14-grid-circle`

Key evidence:

- `grid-circle-runner.txt`
- `grid-circle-shell.txt`
- `tool-recipe-teaching-final.txt`
- `teaching-capture-viewmodel-final.txt`
- `inspection-workspace-selection.txt`
- `current-recipe-ordered-run.txt`
- `workbench-docking.txt`
- `code-structure.txt`
- `grid-circle-wide.png` and `grid-circle-wide-quality.txt`
- `grid-circle-compact.png` and `grid-circle-compact-quality.txt`
- `standard-tests/*.trx`

## Boundaries

This closure proves deterministic software authoring and persistence only. It
does not add `GridPolygon`, a region artifact, a mask-producing algorithm, an
inspection consumer, calibrated dimensions, physical metrology, owner R0,
hosted CI, a fixed package, a commit, a push, a tag, or a release. Runtime DPI
was the workstation's current 125% scaling; 100%, 150%, 175%, and 200% remain
unverified.

Product version remains `0.1.1-dev`. Recipe schema `1.6` is an independent
durable-contract version, not a product release version.

## Completion Record

```text
Status: Complete
Scope: GridCircle contract, fail-closed validation, explicit authoring, numeric editing, save/reopen, Runner parity, and current Wide/Compact runtime evidence
Acceptance criteria: C1 contract and rejection boundaries -> pass; C2 draw/numeric/Apply/Cancel/no-execution -> pass; C3 exact persistence and Runner route -> pass; C4 build/regression/UI/docs/evidence gates -> pass
Verification: Release 0/0; selection 49/49 with GridCircle 9/9; teaching 55/55 and 30/30; workspace 67/67; ordered Run 16/16; docking 98/98; standard tests 2/2; structure 68/68; Wide/Compact actual EXE and screenshot quality pass; git diff --check pass
Evidence: this document; .proofline/issues/PL-0048.json; D:/OpenVisionLab-TestData/OpenVisionLab-3D-Studio/20260824-e14-grid-circle/
Boundary / next dependency: no implicit inspection consumer or mask output; product 0.1.1-dev unchanged; owner R0, hosted CI, package, commit, push, RC, tag, and release are separate gates
```
