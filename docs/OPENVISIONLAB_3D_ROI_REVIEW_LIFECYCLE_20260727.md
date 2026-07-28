# OpenVisionLab 3D ROI Review Lifecycle

Date: 2026-07-27
Status: Complete

## Outcome

Inspection Workspace v3 implementation slice 4 is complete.

Reference and Measurement ROI now use one shared presentation lifecycle:

```text
Missing -> Drawing -> Review -> Applied
                         |
                         +-> Cancel -> previous Missing or Applied state
```

The selected ROI role in `Selected Tool` is the same role selected in the
Viewer. At both wide and `1280 x 760` widths, the operator can see:

- the active role and its `1/2` or `2/2` position;
- the exact `없음 / 그리는 중 / 검토 / 적용됨` state;
- the next action for that state;
- role-local Draw/Edit, Fit ROI, and Delete actions;
- prominent Apply and Cancel actions while capture is active.

`GridRectangle` remains the existing X=column/Z=row recipe-owned footprint.
Surface remains the default C3D display style.

## Ownership

- `InspectionWorkspaceRegionLifecycleState` is the non-WPF lifecycle identity.
- `SelectedToolWorkspaceViewModel` projects the authoritative recipe selection
  and current teaching-capture state into the two ROI rows.
- `ToolWorkbenchViewModel` remains the recipe/capture command owner.
- `InspectionWorkspaceSelectionSession` remains the presentation-only role and
  selection identity owner.
- `WorkbenchViewerTeachingCoordinator` maps the Selected Tool's Fit ROI request
  to the existing Viewer command.
- The Viewer remains the pointer/OpenGL adapter and already owns candidate
  handles, the no-third-point Review guard, Enter Apply, Esc Cancel, and
  empty-space orbit.

No second ROI state store, recipe geometry type, or duplicate capture service
was introduced.

## Behavior contract

### Missing

- Draw ROI is available.
- Fit ROI and Delete are disabled.
- Starting Draw changes only transient capture state.

### Drawing

- `0/2` and `1/2` remain Drawing.
- Apply is disabled until a valid two-corner rectangle exists.
- Recipe routing, dirty state, Preview, Published output, and Run evidence stay
  unchanged.

### Review

- `2/2` immediately changes to Review.
- A third capture point is rejected.
- Corner, center, numeric, and display-only Y-position editing remain available.
- On a narrow perspective-projected Tab ROI, center and corner hit targets use
  nearest-handle priority. The visible center marker remains Move while an
  actual corner remains Resize, even when their screen-space target radii
  overlap.
- Fit ROI is available for the candidate.
- Apply and Cancel are visible in both Selected Tool and the Viewer header.
- Empty-space left drag remains Viewer orbit.

### Applied

- Apply preserves the selection identity and routes the recipe-owned rectangle.
- Canceling an edit restores the prior Applied selection unchanged.
- Edit ROI, Fit ROI, and Delete are available on the role row.
- Apply changes authored ROI geometry only; it never invokes Preview, Publish,
  Run, Validation Set, or Save.

## UI evidence

Fresh current Release captures:

- baseline wide: `artifacts/current/20260727-roi-review-lifecycle/before-wide.png`;
- baseline compact:
  `artifacts/current/20260727-roi-review-lifecycle/before-compact.png`;
- Applied wide: `artifacts/current/20260727-roi-review-lifecycle/after-wide.png`;
- Applied compact:
  `artifacts/current/20260727-roi-review-lifecycle/after-compact.png`;
- Review wide:
  `artifacts/current/20260727-roi-review-lifecycle/after-review-wide.png`;
- Review compact:
  `artifacts/current/20260727-roi-review-lifecycle/after-review-compact.png`;
- actual replacement drag and Apply:
  `artifacts/current/20260727-roi-review-lifecycle/after-review-apply-pointer.png`.

Before, the two cards exposed only `완료/대기`; the active role and next action
were implicit, Fit ROI was separated in the Viewer, and Apply/Cancel could sit
below the compact viewport. After, the active role, exact lifecycle, role-local
commands, and Review actions are visible together. All four after captures
passed screenshot quality on attempt 1.

The Review screenshot smoke starts an existing Reference ROI edit at `2/2`.
Both wide and compact reports prove:

- `active=True`, `progress=2/2`, and `canApply=True`;
- recipe `dirty=False`;
- schema `1.3`, 16 selections, and the three input identities unchanged;
- Preview remains `NotRun`;
- result collection and Preview object references unchanged.

## Verification

Commands were run from `C:\Git\OpenVisionLab-3D-Studio` against the final
Release source:

```powershell
dotnet build "OpenVisionLab.ThreeDStudio.sln" -c Release -p:Platform="Any CPU"

OpenVisionLab.ThreeD.Shell.exe --verify-inspection-workspace-selection <report>
OpenVisionLab.ThreeD.Shell.exe --verify-workbench-docking <report>
OpenVisionLab.ThreeD.Shell.exe --verify-tool-recipe-teaching <report>
OpenVisionLab.ThreeD.Shell.exe --verify-tool-height-measurement-workbench <report>
OpenVisionLab.ThreeD.Shell.exe --verify-recipe-manager-wpg <report>
OpenVisionLab.ThreeD.Shell.exe --verify-teaching-capture-viewmodel <report>
OpenVisionLab.ThreeD.Shell.exe --verify-validation-set <report>
OpenVisionLab.ThreeD.Shell.exe --verify-logging <report>
OpenVisionLab.ThreeDStudio.exe --verify-display-viewmodel <report> --smoke-screenshot <capture>
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify-code-structure.ps1

OpenVisionLab.ThreeD.Runner.exe --tool-recipe <eight-tab-recipe> `
  --source <exact-c3d> --report <report> --expect-status Pass
```

Results:

- Release build: `0` warnings, `0` errors;
- Inspection Workspace ROI lifecycle: `21/21`;
- display/projection ViewModel: `103/103`;
- Workbench docking/composition: `31/31`;
- recipe teaching: `28/28`;
- height measurement: `44/44`;
- Recipe Manager/WPG: `37/37`;
- teaching capture, including third-point rejection: `25/25`;
- Validation Set: `25/25`;
- logging: `4/4`;
- structure: `15/15`;
- exact-source ordered Runner: `8/8`;
- actual Windows pointer/menu/staged-LOD regression: pass;
- exact-source Reference ROI center-move, corner-resize, display-Y drag, and
  explicit same-ID replacement Apply: pass;
- Applied and Review Shell screenshot quality: accepted on attempt 1.

The focused exact-source ROI pointer smoke passed on the first run after the
hit-test correction. The broader OS-injected Viewer pointer regression passed
on retry 3; its first two runs missed middle-button and/or short-right-click
delivery while render, LOD, orbit, right-drag pan, and zoom remained healthy.
This is retained as input-injection harness timing risk, not hidden as a
first-attempt pass.

All current reports and captures are under:

- `artifacts/current/20260727-roi-review-lifecycle/`

## Remaining Workspace v3 priorities

1. Add selected-output Show/Pin/Compare commands in Selected Tool. |
   Recommended model: `gpt-5.6-sol` | Reasoning effort: `medium`
2. Add Viewer split/pop-out composition. |
   Recommended model: `gpt-5.6-sol` | Reasoning effort: `high`
3. Implement bounded Thickness `4 x 2` repeat authoring and exact-source
   replay. | Recommended model: `gpt-5.6-sol` | Reasoning effort: `high`
4. Run the owner's unaided end-to-end acceptance replay.
   Prerequisite: owner availability and physical datum/calibration/tolerance
   decisions. Do not spend model tokens claiming metrology readiness until
   those inputs exist.

## Completion record

Status: Complete

Scope: Workspace v3 slice 4 shared Reference/Measurement ROI lifecycle,
active-role presentation, compact role-local commands, Selected Tool and Viewer
Review actions, presentation-only Fit ROI bridge, and current Applied/Review
evidence.

Acceptance criteria: both roles expose Missing/Drawing/Review/Applied -> pass;
active role is synchronized -> pass; `0/2`, `1/2`, and `2/2` map correctly ->
pass; Review rejects a third point -> pass; Apply preserves selection identity
-> pass; Cancel restores the previous authored state -> pass; Fit ROI is
presentation-only -> pass; no Preview/Publish/Run/Save occurs implicitly ->
pass; overlapping center/corner targets retain distinct Move/Resize feedback ->
pass; wide/compact Applied and Review actions remain visible -> pass.

Verification: Release build `0/0`; lifecycle `21/21`; display `103/103`;
docking `31/31`; teaching `28/28`; height measurement `44/44`; Recipe
Manager/WPG `37/37`; capture `25/25`; Validation Set `25/25`; logging `4/4`;
structure `15/15`; exact-source Runner `8/8`; pointer regression pass; four
after screenshot-quality reports accepted on attempt 1; exact-source
replacement drag/resize/display-Y/Apply smoke pass.

Evidence: `artifacts/current/20260727-roi-review-lifecycle/`.

Boundary / next dependency: Workspace v3 is `4/8` bounded slices (`50%`)
complete. Selected-output commands, Viewer slots, bounded `4 x 2` repeat, and
owner acceptance remain. Physical datum, calibration, traceable units,
uncertainty, and production tolerances remain unverified. The general
OS-injected pointer harness also retains the retry timing risk recorded above.
