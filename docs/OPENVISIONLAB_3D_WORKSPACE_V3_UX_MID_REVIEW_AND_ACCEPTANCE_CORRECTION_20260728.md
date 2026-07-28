# Inspection Workspace v3 UX Mid-review and Acceptance Correction

Date: 2026-07-28

Status: Complete for the bounded ROI Review UX correction

## Why this review was reopened

The current `1920 x 1040` and `1280 x 760` synchronized ROI captures were
reviewed against:

- `OPENVISIONLAB_3D_INSPECTION_WORKSPACE_V3_INTERACTION_SPEC_20260727.md`;
- `OPENVISIONLAB_3D_GOPXL_VIDEO_WORKFLOW_GAP_AND_REDIRECTION_20260727.md`;
- `OPENVISIONLAB_3D_COMMERCIAL_VIDEO_DIRECTION_AND_PRIORITY_20260727.md`.

The implementation direction was correct: Catalog, Recipe Chain, Selected
Tool, and Viewer were composed as specified; Reference and Measurement ROI
used stable role colors; Height Image and 3D shared one selection and Review
lifecycle; Preview, Run, and Save remained explicit.

The capture nevertheless exposed a concrete acceptance failure:

- the same ROI Apply/Cancel actions appeared in the global Review ribbon,
  Selected Tool, and Height Image;
- the same Review instruction appeared again as a 3D Viewer toast;
- the Viewer repeated the selected-step title, internal route, output ID, and
  typed-adapter status already owned by the global bar and Selected Tool;
- the Thickness repeat card stayed expanded during a local ROI correction;
- a `1280 x 760` vertical split gave Height Image only half of the Viewer,
  reducing the exact source to `4.2%` and making the ROI difficult to inspect.

This contradicted the v3 acceptance rule that the default path must not
duplicate selected-step titles or primary actions.

## Corrected interaction ownership

| Responsibility | Visible owner |
| --- | --- |
| Current selected-tool state | Global command bar |
| Current tool title, role, lifecycle, numeric geometry, output summary | Selected Tool |
| ROI Apply, Cancel, undo and keyboard hints during capture | One global Review ribbon |
| Height Image role colors, current lifecycle, pointer manipulation and Delete outside capture | Height Image |
| 3D geometry and candidate review | Main Viewer |

The Selected Tool duplicate Review card and Height Image Apply/Cancel buttons
were removed. The Viewer instruction toast was removed. Enter and Esc remain
global shortcuts and the existing commands/lifecycle owners are unchanged.

## ROI editing focus

When the auxiliary content is Height Image and ROI capture is active:

- vertical split changes from the operator's current ratio to `35% 3D /
  65% Height Image`;
- horizontal split changes to `35% 3D / 65% Height Image`;
- the Thickness repeat card is hidden because it is unrelated to local ROI
  correction;
- the existing split ratio is restored when Apply or Cancel ends capture.

This is presentation-only. It does not edit the recipe, run inspection, move
the camera, or change the selected source.

In the compact current-source capture, Height Image increased from `4.2%` to
`7.9%` while the 3D surface remained visible as a review reference.

## Current UI evidence

Before:

- `artifacts/current/20260728-workspace-v3-ux-acceptance-correction/before-wide-review-duplicated-actions.png`;
- `artifacts/current/20260728-workspace-v3-ux-acceptance-correction/before-compact-review-duplicated-actions.png`.

After:

- `artifacts/current/20260728-workspace-v3-ux-acceptance-correction/after-wide-review-focused.png`;
- `artifacts/current/20260728-workspace-v3-ux-acceptance-correction/after-compact-review-focused.png`;
- `artifacts/current/20260728-workspace-v3-ux-acceptance-correction/after-wide-applied-restored-split.png`.

Both after screenshots passed current screenshot quality on attempt 1.

## Verification

| Check | Result |
| --- | --- |
| Release build | `0 warnings / 0 errors` |
| UX structure check | Pass |
| Actual Windows pointer Wide Review | Pass |
| Actual Windows pointer Compact Review | Pass |
| Actual Windows pointer Apply + save/reopen | Pass |
| Inspection Workspace | `50/50` |
| Workbench docking/composition | `33/33` |
| Recipe teaching | `28/28` |
| Height measurement | `45/45` |
| Shell smoke options | `21/21` |
| Code structure | `17/17` |

The UX structure check proves:

- exactly one visible primary ROI Review ribbon;
- no duplicate Selected Tool Review action card;
- no Height Image Apply button during capture;
- no Viewer instruction toast;
- the global bar shows selected-tool state rather than another selected-tool
  title;
- both split orientations own the `35/65` focus ratio.

## Remaining user acceptance

This correction removes the concrete duplication and compact editing defect
found in the current capture. It does not replace R0. The owner must still run
the exact 12-step recipe path without guidance before Workspace v3 becomes
`8/8`.

Other later UX work must be evidence-triggered rather than broad restyling.
The next dependency-correct product item remains `C-11`, the visible
invalid/missing-cell overlay.

## Completion record

```text
Status: Complete
Scope: current-capture UX audit, one primary ROI Review action surface, removal of duplicate review/status chrome, and reversible 35/65 Height Image editing focus
Acceptance criteria: one visible primary Apply/Cancel owner -> pass; no Viewer instruction occlusion -> pass; Height Image dominates during ROI edit and restores split afterward -> pass; Review/Apply/Cancel/Delete lifecycle preserved -> pass; Wide/Compact current-source evidence -> pass
Verification: Release build 0/0; UX structure pass; actual pointer Wide/Compact Review pass; Apply/save/reopen pass; Workspace 50/50; docking 33/33; teaching 28/28; height measurement 45/45; shell options 21/21; structure 17/17
Evidence: docs/OPENVISIONLAB_3D_WORKSPACE_V3_UX_MID_REVIEW_AND_ACCEPTANCE_CORRECTION_20260728.md and artifacts/current/20260728-workspace-v3-ux-acceptance-correction/
Boundary / next dependency: R0 unaided owner replay remains external; C-11 visible invalid/missing-cell overlay is next; physical metrology remains unverified
```
