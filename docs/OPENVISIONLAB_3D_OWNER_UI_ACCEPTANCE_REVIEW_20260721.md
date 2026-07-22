# OpenVisionLab 3D Owner UI Acceptance Review Packet

Updated: 2026-07-22
Status: Complete — owner accepted the current UI/UX gate at 85/100 on 2026-07-21.

## Scope

This packet refreshes the P4 UI/UX review against the current Debug source
after the GoPxL chain-readability G1-G7 work, the current 3-Point Plane / Datum
Plane views, the Viewer Geometry selector refinement, and the expanded Tool
Labs menu. It evaluates only the local 3D recipe-workbench UI.

It does not accept a new algorithm, camera, PLC, robot, cloud, physical unit,
calibration, or metrology claim. The governing manual checklist remains
`docs/OPENVISIONLAB_3D_OWNER_UI_ACCEPTANCE_PROTOCOL_20260720.md`.

## Completed current-source evidence

| Check | Result | Evidence |
| --- | --- | --- |
| Debug solution build | Pass — 0 warnings, 0 errors | `dotnet build OpenVisionLab.ThreeDStudio.slnx -c Debug -p:Platform="Any CPU" --no-restore` |
| Recipe teaching | Pass — 18/18 | `artifacts/ui/20260721-owner-ui-acceptance-review/tool-recipe-teaching-report.txt` |
| Docking workspace | Pass — 25/25 | `artifacts/ui/20260721-owner-ui-acceptance-review/workbench-docking-report.txt` |
| Artifact Navigator | Pass — 24/24 | `artifacts/ui/20260721-owner-ui-acceptance-review/artifact-navigator-report.txt` |
| Keyboard automation at current host scale | Pass — 5/5 | `artifacts/ui/20260721-owner-ui-acceptance-review/keyboard-ui-automation-report.txt` |
| Korean Workbench 1920 x 1080 | Screenshot quality accepted on attempt 1 | `artifacts/ui/20260721-owner-ui-acceptance-review/workbench-ko-1920.png` |
| Korean Workbench 1280 x 760 | Screenshot quality accepted on attempt 1 | `artifacts/ui/20260721-owner-ui-acceptance-review/workbench-ko-1280.png` |
| English Workbench 1920 x 1080 | Screenshot quality accepted on attempt 1 | `artifacts/ui/20260721-owner-ui-acceptance-review/workbench-en-1920.png` |
| Recipe Manager, 3-Point Plane Tool Lab, Datum Tool Lab | Screenshot quality accepted on attempt 1 | `artifacts/ui/20260721-owner-ui-acceptance-review/` |

The fixture contains an authored 3-Point Plane followed by Datum Plane
Deviation. It is deliberately not expanded into an artificial full Tool-Lab
chain merely to manufacture evidence.

## Evidence-based provisional score

| Area | Available | Candidate points | Evidence and deduction |
| --- | ---: | ---: | --- |
| Workbench information hierarchy | 20 | 17 | The current source, selected route, typed input/output, state, compatible next tool, explicit Add, Viewer, and Pipeline are visible together. Deduct 3 until a first-time operator completes the protocol without explanation. |
| Visual system and 1920 layout | 20 | 16 | The 1920 Korean/English views use the established navy/light/teal system and the refined Geometry control. At 1280, the long Viewer status text compresses before the `R-drag: Pan` hint; deep Tool Lab and Recipe Manager strings are still intentionally mixed technical English. Deduct 4 pending that compact viewer-header refinement and real high-DPI review. |
| Docking and window behavior | 15 | 15 | Docking passes 25/25. The current Recipe Manager plus captured Tool Labs use custom title views and existing single-instance ownership. |
| Recipe and tool workflow | 20 | 18 | Tree-first routing, read-only Input -> Parameters -> Output summaries, Flow Map, explicit Preview/Publish, and focused Tool Labs are present. Deduct 2 until a first-time operator confirms the multi-step workflow. |
| Feedback and accessibility | 15 | 12 | Current states retain text, color, icons, tooltips, and Automation names; keyboard automation passes 5/5. Deduct 3 until the owner completes the keyboard-only pane navigation and a real 150% DPI/assistive review. |
| Evidence and visual polish | 10 | 7 | Fresh Workbench, Recipe Manager, 3-Point Plane, and Datum captures pass. Deduct 3 because current-source captures do not yet cover every available Tool Lab and all docked comparison panes at this review point. |
| **Total** | **100** | **85** | **Owner accepted on 2026-07-21** |

## Compact Viewer-header follow-up - 2026-07-22

Status: Complete

The Viewer command/status strip now keeps Geometry/HUD and the independent
no-wrap `R-drag: Pan`/View controls on the first row. Viewer status occupies a
separate full-width row with intentional ellipsis, a full-value tooltip, and
an accessible name. The selected 3-Point Plane fixture now shows the complete
`C3D source loaded for teaching: Ori_20240116_094414.C3D` status at both
`1280 x 760` and `1920 x 1080`; Viewer interaction, selection,
Preview/Publish/Run, and rendering bindings were not changed.

Acceptance criteria and current-task evidence:

- Compact status remains readable while `R-drag: Pan` remains independent:
  before/after `1280 x 760` captures in
  `artifacts/current/20260722-viewer-header-status/`.
- Normal-width hierarchy remains intact: after `1920 x 1080` capture in the
  same folder.
- Current Debug build: pass, `0` warnings and `0` errors.
- Workbench docking: pass, `25/25`.
- Viewer display ViewModel: pass, `82` checks.
- Both after captures passed screenshot quality on attempt 1.

Boundary: this closes only the recorded non-gate P1 layout issue. It does not
rescore the owner-approved `85/100`, complete the deferred manual reviews, or
add an algorithm, calibration, metrology, or production-integration claim.

## Deferred manual evidence

The following cannot be completed by an automated source review and remain
explicitly unverified rather than treated as passing evidence:

1. Owner keyboard-only protocol: visible focus, candidate selection, explicit
   Add, blocked explanation, and pane navigation.
2. Operator-display 150% DPI protocol: main, compact, English, and overflow
   checks with the actual display scale and resolution recorded.
3. First-time-operator protocol: an engineer who did not implement this UI
   identifies source, selected route, typed chain, next safe action, and the
   non-mutating candidate-selection boundary.

## Owner decision

**Accept — 2026-07-21.** The owner accepted the current local UI/UX gate at
the evidence-based 85/100 score. This clears the UI-priority hold for choosing
the next bounded product task. It does not convert the deferred 150% DPI,
first-time-operator, keyboard-only operator, or assistive review into passing
evidence, and it does not make a physical calibration, metrology, or
production-readiness claim.

The former compact Viewer-header issue is closed by the 2026-07-22 follow-up
above. The owner decision and score remain the recorded 2026-07-21 decision;
the deferred manual evidence still requires separate owner/operator review.
