# OpenVisionLab 3D Recipe Manager + WPG Implementation Evidence

Date: 2026-07-19
Status: Complete for the original local Recipe Manager v1 lifecycle scope; its former docked Workbench placement is superseded by the 2026-07-19 approved [GoPxL-Informed Tool Lab Direction](OPENVISIONLAB_3D_GOPXL_TOOL_LAB_DIRECTION_20260719.md).

## Outcome

The docked Workbench now opens one active `*.ov3d-teach.json` session through a compact Recipe Manager and teaches the selected Filter or Height Difference Edge parameters through an app-local typed WPG PropertyGrid.

The implementation preserves the inspection boundary:

- Open, selection, PropertyGrid edit, Apply, Save, visibility changes, and source repair do not run Preview, Run, or Publish.
- WPG edits remain detached until explicit `Apply parameters`.
- Invalid values leave the authored step unchanged.
- Unknown parameters and unsupported steps remain present through save/reopen.
- A missing or identity-mismatched source opens in repair state, disables execution, and clears stale Viewer geometry.
- `Preview`, `Run`, and `Publish` remain explicit commands.

## Implemented scope

- Recipe Manager commands: New, Open, Save, Save As, and a ten-entry Recent list.
- Active recipe name, schema, path, saved/modified state, source readiness, and adapter coverage.
- Candidate-first open: invalid JSON leaves the active session unchanged.
- Relative source resolution plus byte-length, SHA-256, and grid identity checks.
- Atomic sibling-temp save with flush, replace, and cleanup.
- Explicit dirty-draft and dirty-recipe close/session-change prompts.
- Typed Filter and Height Difference Edge WPG drafts with enum and numerical editors.
- Explicit Apply/Discard, validation, invariant numeric serialization, missing-known-property restoration, and unknown-property merge.
- Read-only compatibility UI for steps without a typed adapter.
- View-local WPG theme resources; no application-global implicit control style.
- Exact Shell-only WPG package reference through the repository-local package feed.

## WPG package boundary

- Package: `OpenVisionLab.WpfPropertyGrid.ThreeD` `1.0.0-ovl3d.1`
- Target: `net10.0-windows10.0.19041`
- Source commit: `2050f36a144f8c4c6964ff5777ec21aa03e89877`
- Vendored SHA-256: `9B1A2E5CFD5275B17D55C9D8F3D8CFC0CAA64D88DE2BA837AFC97B0B581780EC`
- Reference owner: `OpenVisionLab.ThreeD.Shell` only
- Theme scope: assembly/view-local semantic keys

The legacy WPG project and its existing consumers were not changed by the Studio package reference.

## Current-task verification

| Gate | Result | Evidence |
| --- | --- | --- |
| Debug solution build | Pass, 0 warnings / 0 errors | `dotnet build OpenVisionLab.ThreeDStudio.sln -c Debug` |
| WPG package integrity/theme metadata | Pass | `artifacts/verification/20260719-recipe-manager-wpg-v1/wpg-package.txt` |
| Recipe Manager + WPG | Pass, 17/17 | `artifacts/verification/20260719-recipe-manager-wpg-v1/recipe-manager-wpg.txt` |
| Tool recipe teaching | Pass, 16/16 | `artifacts/verification/20260719-recipe-manager-wpg-v1/tool-recipe-teaching.txt` |
| Structured selections | Pass, 17/17 | `artifacts/verification/20260719-recipe-manager-wpg-v1/tool-recipe-selections.txt` |
| Docking workspace | Pass, 15/15 | `artifacts/verification/20260719-recipe-manager-wpg-v1/workbench-docking.txt` |
| Filter Golden/Runner | Pass, 13/13 | `artifacts/verification/20260719-recipe-manager-wpg-v1/filter-golden.txt` |
| Height Difference Edge Golden/Runner | Pass, 13/13 | `artifacts/verification/20260719-recipe-manager-wpg-v1/edge-golden.txt` |
| Verification process cleanup | Pass, 0 remaining | `artifacts/verification/20260719-recipe-manager-wpg-v1/verification-process-cleanup.txt` |

The Recipe Manager verifier covers detached editing, no auto-run, invalid edit rejection, Apply, unknown retention, unsupported-step preservation, atomic save, save/reopen, candidate rejection, missing/mismatched sources, bounded Recent files, WPG category/search/object-swap/commit behavior, and view-local theme isolation.

## UI evidence

All after captures were generated from the current Debug build. Quality reports accepted each capture.

- Baseline before: `artifacts/ui/20260719-recipe-manager-wpg-v1/before.png`
- Recipe Manager + Filter WPG: `artifacts/ui/20260719-recipe-manager-wpg-v1/after-filter-1920x1080.png`
- Invalid Edge draft with visible correction and Apply/Discard: `artifacts/ui/20260719-recipe-manager-wpg-v1/after-edge-invalid-1280x760.png`
- Missing-source repair state with stale geometry cleared: `artifacts/ui/20260719-recipe-manager-wpg-v1/after-missing-source-1280x760.png`
- Partially supported step: `artifacts/ui/20260719-recipe-manager-wpg-v1/after-partial-1280x760.png`
- No selected step: `artifacts/ui/20260719-recipe-manager-wpg-v1/after-empty-1280x760.png`

Visual review confirmed that Recipe Manager, typed values, combo text, correction text, and Apply/Discard remain visible at the fixed capture sizes. Missing-source state no longer leaves a previously loaded C3D surface visible. WPG semantic resources remain local to the Step Parameters host.

## Claim boundary and next dependency

This gate proves local recipe lifecycle and parameter teaching for two typed adapters. It does not prove a generic multi-step executor, Line Fit, intersections, XYZ affine solving, derived-map construction, physical scale, calibration, uncertainty, Gauge R&R, metrology, or production integration.

The next product implementation candidate is the separately designed 3D Line Fit typed adapter. Its nine owner decisions remain the approval checkpoint before production code begins.

```text
Status: Complete
Scope: Approved Recipe Manager v1 and typed WPG teaching for Filter and Height Difference Edge
Acceptance criteria: lifecycle, repair state, staged edit, atomic persistence, compatibility preservation, dependency/theme isolation, docking, and current-build UI evidence -> passed
Verification: build 0/0; WPG package pass; Recipe Manager/WPG 17/17; teaching 16/16; selections 17/17; docking 15/15; Filter 13/13; Edge 13/13; remaining process count 0; accepted screenshots
Evidence: docs/OPENVISIONLAB_3D_RECIPE_MANAGER_WPG_IMPLEMENTATION_20260719.md; artifacts/verification/20260719-recipe-manager-wpg-v1; artifacts/ui/20260719-recipe-manager-wpg-v1
Boundary / next dependency: Line Fit remains unimplemented and requires approval of its nine design decisions; physical calibration and metrology evidence remain unavailable
```
