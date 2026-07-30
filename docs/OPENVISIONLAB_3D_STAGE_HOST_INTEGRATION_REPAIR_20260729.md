# OpenVisionLab 3D Stage-Host Integration Repair

Date: 2026-07-29

Status: Complete for IA-4a; automated IA-4b is complete in the newer focused
record; human-owner R0 remains

## Scope

This slice repairs the actual Release integration failure found by the
simulated-novice video replay. It does not redesign the approved
Setup/Teach/Validate/Results architecture. It makes every dynamically
recomposed dock surface retain its intended state owner.

Included:

- stable Shell or Workbench ownership for every stage-hosted view;
- visible and accessible Validate/Results local navigation;
- live five-sample Validation Set and one-step Run Record rendering;
- a working Results -> Advanced route;
- actual WPF Window-hosted stage integration verification;
- fresh application-only Wide and Compact video evidence.

Excluded:

- human-owner unaided R0 acceptance;
- explicit failure -> Teach -> return-preservation replay in this IA-4a
  video; the newer IA-4b record closes that software gate;
- physical calibration, certified metrology, camera, PLC, robot, cloud, or
  stereo-reconstruction scope.

## Root cause and repair

`OpenVisionDockWorkspaceView.SetOperatorStage` detaches and reattaches
AvalonDock panes. The hosted views relied on inherited bindings such as
`DataContext="{Binding Workbench}"`. After live recomposition, those
bindings no longer had a reliable source.

`ToolRecipeWorkbenchView` now gives each hosted surface one explicit,
stable owner:

| Hosted surface | Owner |
| --- | --- |
| Tool Library, Recipe Chain, Selected Tool | `Shell.Workbench` |
| Viewer host | `Shell` with its inner Viewer workspace bound to `Workbench` |
| Validate evidence | `Shell.Workbench` |
| Results workspace | `Shell` |
| Output Compare, Displayed Outputs, Session Log | `Shell.Workbench` |

No replacement ViewModel, hard-coded content, automatic execution, or recipe
mutation was introduced.

Validate's contextual command is now labeled `샘플 세트 실행` /
`Run sample set`, which distinguishes it from the global recipe `전체 실행` /
`Run all` command.

## Regression contract

`ToolWorkbenchDockingVerification` now hosts the Workbench in an actual,
off-screen WPF `Window` before performing:

```text
Setup -> Teach -> Validate -> Results -> Advanced -> Results -> Setup
```

The verification fails unless:

- every stage-hosted child still owns the intended live DataContext;
- Validate exposes five non-empty localized/accessibility navigation names;
- exactly five Validation Set rows are present and the sample-set command is
  executable;
- Results exposes three non-empty localized/accessibility navigation names;
- the Advanced command is executable;
- stage navigation remains presentation-only.

Current result: `48/48`.

## Before/after actual-Release evidence

Historical before evidence:

- `artifacts/current/20260729-novice-stage-navigation-video-review/`

Current after evidence:

- `artifacts/current/20260729-stage-host-integration-repair/`
- Wide: `01-wide-novice-stage-navigation.mp4`, `1920 x 1040`, 58 seconds;
- Compact: `02-compact-novice-stage-navigation.mp4`, `1280 x 760`, 52
  seconds;
- current keyframes under `keyframes/`;
- exact UI Automation/pointer timelines, FFprobe output, executable and Shell
  assembly SHA-256 identity, and the `48/48` report.

The current videos show:

- Teach Selected Tool content and Preview action;
- Validate's five named sections, five Pending rows, and
  `Good 2 / Bad 2 / Held-out 1`;
- Results' three named sections, supplied Fail Run Record, one ordered step,
  and report/export commands;
- a visible Advanced diagnostics transition in Wide and Compact.

The historical videos show blank local labels/content for the same inputs.

## Newer IA-4b result and remaining owner gate

The newer
`docs/OPENVISIONLAB_3D_IA4B_OWNER_PATH_REPLAY_20260729.md` closes the
automated IA-4b software and actual-Release replay:

- five explicit samples complete with `3 Pass / 2 Fail / 0 Error`;
- the selected failure opens its owning Completeness Grid Teach step;
- Results -> Advanced -> Results and the Validation return preserve state;
- Release build passes `0/0`, focused integration passes `52/52`, and current
  Wide/Compact video evidence passes.

`A-01` remains Partial only because the human owner's unaided Wide and
Compact R0 replay is still external. Only after owner R0 passes should the
product begin the SurfaceModel foundation.

### Superseded IA-4a remaining-gate statement

IA-4a is complete, but `A-01` remains Partial until the owner-path gate is
closed. IA-4b must still:

1. execute the visible `샘플 세트 실행` action;
2. open a real failure in Teach;
3. return through Results -> Advanced -> Results;
4. prove recipe, selected step, dirty state, current output, validation
   evidence, and Run Record preservation;
5. complete the human owner's unaided Wide and Compact R0 replay.

Only after IA-4b and owner R0 pass should the product begin the SurfaceModel
foundation.

## Completion record

```text
Status: Complete
Scope: IA-4a stable live stage-host ownership, localized/accessibility navigation, live validation/result content, and Advanced route repair
Acceptance criteria: explicit hosted-owner identity -> pass; actual Window Setup/Teach/Validate/Results recomposition -> pass; five localized Validate sections -> pass; five Validation Set rows and executable sample-set command -> pass; three localized Results sections -> pass; loaded one-step Fail Run Record -> pass in video; visible Advanced transition -> pass in Wide and Compact; recipe execution side effects -> none
Verification: Release build 0 warnings / 0 errors; --verify-workbench-docking 48/48; FFprobe Wide 1920x1040 58 s and Compact 1280x760 52 s; visual before/after comparison; exact UI Automation and pointer timelines
Evidence: docs/OPENVISIONLAB_3D_STAGE_HOST_INTEGRATION_REPAIR_20260729.md; artifacts/current/20260729-stage-host-integration-repair/
Boundary / next dependency: automated IA-4b is complete in OPENVISIONLAB_3D_IA4B_OWNER_PATH_REPLAY_20260729.md; human-owner R0 remains external; physical calibration and metrology are not claimed
```

## Next priorities

1. Human-owner Wide/Compact R0 replay | Prerequisite: owner performs the
   focused checklist | Recommended model: none until owner evidence exists |
   Reasoning effort: none
2. `J-01/J-03/J-04 SurfaceModel preparation foundation` | Prerequisite:
   owner R0 passes | Recommended model: `gpt-5.6-sol` |
   Reasoning effort: `high`
