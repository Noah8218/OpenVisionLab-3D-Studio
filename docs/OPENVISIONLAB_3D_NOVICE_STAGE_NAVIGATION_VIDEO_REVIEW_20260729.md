# OpenVisionLab 3D Novice Stage-Navigation Video Review

Date: 2026-07-29

Status: Historical IA-4 blocker; superseded by the IA-4a repair

The blocker recorded here is repaired by
`OPENVISIONLAB_3D_STAGE_HOST_INTEGRATION_REPAIR_20260729.md`. Preserve this
document and its videos as the true before evidence. IA-4b and human-owner R0
remain; do not reuse the blank captures as the current UI.

## Purpose and evidence boundary

This review treats the operator as a first-time user with no project
documentation open. It records the actual Release WPF application while
trying to find:

```text
Setup -> Teach -> Validate -> Results -> Advanced
```

The replay uses external UI Automation only to locate visible controls and
`user32` pointer movement/clicks to operate them. It does not invoke hidden
Workbench commands. This is simulated-novice evidence, not the owner's
unaided R0 acceptance.

The reviewed executable SHA-256 is:

`3219C29579F8F93F2D75491F94871CEA7A2FB0472C2A9DF7CF390812FFF70721`

The controlled recipe has one Completeness step. Its validation sidecar
contains two Good, two Bad, and one Held-out sample. The recipe source and
manifest source identities both equal:

`70407500E6579E9CF62BE66A03DC61FF4184F31F0C52BA6ABFA19AC8A778403A`

A schema `1.5` Fail Run Record with one executed step was also supplied.
Therefore the blank Validate and Results surfaces are not accepted as a
legitimate no-input state.

## Recorded evidence

- Wide application-only replay:
  `artifacts/current/20260729-novice-stage-navigation-video-review/01-wide-novice-stage-navigation.mp4`
- Compact application-only replay:
  `artifacts/current/20260729-novice-stage-navigation-video-review/02-compact-novice-stage-navigation.mp4`
- Exact pointer/action timelines:
  `01-wide-novice-stage-navigation-timeline.jsonl` and
  `02-compact-novice-stage-navigation-timeline.jsonl`
- Contact sheets, keyframes, environment identity, and FFprobe verification:
  `artifacts/current/20260729-novice-stage-navigation-video-review/`
- Reusable recorder:
  `scripts/run-novice-stage-navigation-video-review.ps1`

Both final videos contain only the application window. The discarded trial
captures that exposed another window were not retained.

## Findings

### P0 — dedicated stage content loses its live Shell context

The five top-level stages are visible and understandable. Setup renders its
Tool Library and Recipe Chain. After entering Teach, Validate, or Results,
however, content hosted in the dynamically recomposed dock panes does not
retain the data needed by the extracted views.

Observed in both `1920 x 1040` and `1280 x 760`:

- Teach keeps the source card and step rail, but the Selected Tool header,
  status cards, metric values, and channel evidence are blank.
- Validate shows five radio circles without labels. The saved five-sample
  manifest is not rendered, the main table is empty, and Run All is disabled.
- Results shows three radio circles without labels. Its title, detail,
  immutable-evidence notice, Run Record summary, step result, and command
  labels are blank even though a Run Record was supplied.
- Clicking the enabled gear at the Results header produces no visible
  Advanced transition.

This blocks a novice from completing Validate or Results and invalidates the
IA-2/IA-3 live-application acceptance claim. `A-01` and `A-10` remain
Partial.

### P1 — invisible labels also remove accessibility and recovery cues

The same local controls report empty UI Automation names:

- `ValidationSamplesNavigation`
- `ValidationResultsNavigation`
- `ValidationFailuresNavigation`
- `ValidationThresholdNavigation`
- `ValidationHeldOutNavigation`
- `ResultsRunRecordNavigation`
- `ResultsOutputCompareNavigation`
- `ResultsReportsNavigation`
- `ResultsAdvancedDiagnostics`

This is not only a visual text defect. A screen reader, automated acceptance
replay, or user relying on tooltips has no stable explanation of those
controls. The blank, disabled Run All action also has no visible prerequisite
message.

### P1 — static view captures did not prove live integration

The prior IA-2 and IA-3 captures show the extracted views when their expected
ViewModel is assigned directly. The actual Shell replay exercises the
AvalonDock stage recomposition and exposes a different result.

The existing `44/44` and `47/47` docking checks prove stage ownership,
section enum changes, state preservation, and non-mutation. They do not
assert, after a real MainWindow stage transition:

- the hosted child View/DataContext identity;
- non-empty localized labels and accessible names;
- loaded validation-row count and Run Record step count;
- Run All prerequisite state;
- visible Advanced route completion.

The acceptance test must be expanded to include those observable contracts.

### P2 — the controlled 6 x 6 source is poor novice teaching evidence

The Viewer is dark for the tiny controlled Completeness fixture. That is not
enough evidence to declare a general Viewer defect, but it is unsuitable as
the only novice teaching demonstration. After the P0 integration fix, the
owner replay should use the public `Thickness Coupon` for teaching and the
controlled Completeness fixture only for deterministic validation evidence.

## Most likely technical cause

This is a source-grounded inference, not yet a patched diagnosis.

`OpenVisionDockWorkspaceView.SetOperatorStage` detaches and reattaches layout
panes for Setup, Teach, Validate, and Results. Several hosted views obtain
their context through inherited bindings such as:

```xml
DataContext="{Binding Workbench}"
DataContext="{Binding}"
```

The actual replay is consistent with those inherited sources becoming empty
when the dock content is recomposed. `ToolRecipeWorkbenchView` already uses
explicit source bindings for some advanced panes, but not for the Teach
Selected Tool, Validate evidence, or Results workspace.

The corrective slice should first prove the runtime child DataContext
identities, then give every stage-hosted view an explicit stable owner. Do
not paper over the problem by hard-coding labels or duplicating ViewModels.

## Required corrective acceptance gate

1. Start the current Release MainWindow with the controlled recipe,
   five-sample validation manifest, and one-step Run Record.
2. In Wide and Compact, use actual pointer input to traverse
   Setup -> Teach -> Validate -> Results -> Advanced -> Results.
3. Teach shows a non-empty selected-tool title, lifecycle/status, parameter
   or metric evidence, and Preview action.
4. Validate shows all five local labels, exactly five Pending sample rows,
   `Good 2 / Bad 2 / Held-out 1`, an enabled Run All action, and a visible
   failure-to-Teach route after explicit execution.
5. Results shows all three local labels, the loaded Fail Run Record, its one
   ordered step, and visible report/export actions.
6. Advanced opens visibly and returning preserves recipe, selected step,
   dirty state, current output, validation evidence, and Run Record.
7. Every local navigation and Advanced control has a non-empty accessible
   name.
8. Add a real MainWindow integration check so direct View captures cannot
   pass while the live Shell is blank.
9. Capture fresh application-only Wide and Compact videos and keyframes.

## Product direction decision

The responsibility-separated IA direction remains correct and still matches
the supplied commercial-video lessons:

- GoPxL: distinct configuration, teaching, and result-review surfaces;
- SICK Nova: explicit Configure/Run responsibility and Good/Bad evidence;
- HALCON: detailed diagnostics only when requested.

The problem is live composition reliability, not the stage model. Do not
return to the former all-in-one screen. Fix the stage-hosted ownership and
prove it in the real Shell before starting SurfaceModel.

Camera acquisition, stereo reconstruction, PLC/robot/HMI, cloud/factory
management, physical calibration, and certified metrology remain outside
this corrective slice.

## Completion record

```text
Status: Incomplete
Scope: IA-4 simulated-novice actual-Release Wide/Compact video replay of Setup -> Teach -> Validate -> Results -> Advanced
Acceptance criteria: application-only Wide/Compact videos -> pass; top-level stage recognition -> pass; Teach selected-tool evidence -> fail; Validate five-section/sample/run workflow -> fail; Results local sections and loaded Run Record -> fail; visible Advanced round trip -> fail; owner unaided acceptance -> not attempted
Verification: FFprobe 1920x1040 and 1280x760, 15 fps, 40 seconds each; exact user32/UI Automation timelines; source/manifest SHA equality; source review of dynamic docking composition and current verification gaps
Evidence: docs/OPENVISIONLAB_3D_NOVICE_STAGE_NAVIGATION_VIDEO_REVIEW_20260729.md and artifacts/current/20260729-novice-stage-navigation-video-review/
Boundary / next dependency: repair and test the live stage-hosted DataContext/command integration, then repeat this replay; human-owner R0 remains external
```

## Next priorities

1. `IA-4a live stage-host ownership and MainWindow integration repair` |
   Recommended model: `gpt-5.6-sol` | Reasoning effort: `high`
2. `IA-4b repeat application-only novice replay and then owner R0` |
   Prerequisite: IA-4a passes | Recommended model: `gpt-5.6-terra` |
   Reasoning effort: `medium`
3. `J-01/J-03/J-04 SurfaceModel preparation foundation` | Prerequisite:
   IA-4b and owner R0 pass | Recommended model: `gpt-5.6-sol` |
   Reasoning effort: `high`
