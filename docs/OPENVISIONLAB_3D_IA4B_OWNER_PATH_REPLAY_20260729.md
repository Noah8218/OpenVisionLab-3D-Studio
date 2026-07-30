# OpenVisionLab 3D IA-4b Owner-Path Replay

Date: 2026-07-29

Status: Complete for the automated IA-4b software and actual-Release replay;
human-owner R0 remains an external acceptance prerequisite

Newer evidence:
`docs/OPENVISIONLAB_3D_DIRECT_NOVICE_REPLAY_FINDINGS_20260729.md` preserves
this navigation/state closure but shows that actionable correction is
Incomplete because Teach does not render the source or carry the selected
failure context. Repair and direct replay now precede human-owner R0.

## Outcome

The current Release application completes this exact operator path in both
Wide and Compact layouts:

```text
Validate
  -> Run sample set
  -> 5 complete / 3 pass / 2 fail / 0 error
  -> Failure Analysis
  -> Fix in Teach
  -> failed Completeness Grid selected in Teach
  -> Results / one-step Fail Run Record
  -> Advanced
  -> Results
  -> Validate / preserved failure evidence
```

The route does not modify the recipe, start Preview, or start a hidden
inspection execution. The top status remains saved, the selected failed step
remains `step.validation.completeness`, and the Validation Set and supplied
Run Record remain available after the Results/Advanced round trip.

This is automated simulated-novice evidence. It does not replace the human
owner's unaided R0 acceptance.

## Video-review finding and repair

The first Compact replay exposed a real integration defect:

- `Fix in Teach` was visible and its failure row/step evidence was selected;
- pointer click and focused Space activation both left the application in
  Failure Analysis;
- direct ViewModel command verification had passed because it bypassed the
  docked view's live command binding.

The Validation view keeps `Shell.Workbench` as its normal DataContext, but the
`Fix in Teach` command belongs to `ShellMainWindowViewModel`. Its
`RunRecordContext` binding relied on a visual-tree ancestor that is not stable
after AvalonDock stage recomposition.

`ToolRecipeWorkbenchView` now gives the hosted
`RecipePipelineReviewView.RunRecordContext` an explicit binding to the live
Shell owner. The stable stage-host contract also verifies that this owner is
present. No new ViewModel, recipe mutation, automatic execution, or hidden
navigation command was introduced.

Before/after visual evidence:

- before fix:
  `artifacts/current/20260729-ia4b-owner-path-replay/compact-teach-after-click-23s.png`;
- after fix:
  `artifacts/current/20260729-ia4b-owner-path-replay/compact-teach.png`;
- Wide after fix:
  `artifacts/current/20260729-ia4b-owner-path-replay/wide-teach.png`.

## Current actual-Release evidence

Artifact root:

- `artifacts/current/20260729-ia4b-owner-path-replay/`

Videos:

- `01-wide-ia4b-owner-path.mp4`: `1920 x 1040`, 15 fps, 72 seconds;
- `02-compact-ia4b-owner-path.mp4`: `1280 x 760`, 15 fps, 72 seconds.

The folder also contains:

- exact pointer/UI Automation timelines for each layout;
- current keyframes and contact sheets;
- FFprobe media verification;
- Release EXE and assembly timestamps and SHA-256 identities;
- the focused `52/52` Workbench docking report.

The final timelines show the pointer click itself transitions from Failure
Analysis to Teach. The keyboard fallback in the capture harness was not used
in either accepted video.

## Verification

```text
dotnet build "OpenVisionLab.ThreeDStudio.sln" -c Release -p:Platform="Any CPU"
```

Result: `0` warnings, `0` errors.

```text
OpenVisionLab.ThreeD.Shell.exe
  --verify-workbench-docking
  artifacts/current/20260729-ia4b-owner-path-replay/workbench-docking.txt
```

Result: `52/52`.

The focused verifier additionally proves:

- the controlled recipe opens with five labeled samples and one Fail Run
  Record step;
- explicit sample-set execution returns `3 pass / 2 fail / 0 error`;
- the selected failed Validation step opens its owning Teach step;
- dirty state remains false and neither Validation nor Preview is running;
- Results -> Advanced -> Results preserves recipe path, source, selected
  step, dirty state, Validation summary, Run Record summary, and its one
  ordered step.

## Human-owner R0 checklist

Run this without documentation or Codex control using the current Release
application:

1. Open the controlled Completeness validation recipe and supplied Fail Run
   Record.
2. Select **Validate** and confirm five samples:
   `2 Good / 2 Bad / 1 Held-out`.
3. Click **Run sample set** and confirm
   `5 complete / 3 pass / 2 fail / 0 error`.
4. Open **Failure Analysis** and inspect one failed Bad sample.
5. Click **Fix in Teach** and confirm **Completeness Grid** is selected in
   Teach.
6. Confirm the recipe is still saved and no Preview or Run started.
7. Open **Results** and confirm the one-step Fail Run Record.
8. Open **Advanced**, then return to **Results**.
9. Return to **Validate -> Failure Analysis** and confirm the same failure
   evidence remains.
10. Repeat once at Wide and once at Compact size.

Pass only if every action is discoverable without assistance and all stated
state is preserved. Record any hesitation, misleading label, clipped action,
or lost state as a failure rather than coaching around it.

## Completion record

```text
Status: Complete
Scope: automated IA-4b five-sample failure-to-Teach and Results/Advanced return-preservation route, including the docked RunRecordContext repair
Acceptance criteria: explicit five-sample execution -> pass with 3/2/0; selected failure opens owning Teach step -> pass; no recipe mutation or hidden Preview/Run -> pass; Results/Advanced/Results and Validation return preserve state -> pass; current Wide and Compact videos -> pass
Verification: Release build 0 warnings / 0 errors; Workbench docking 52/52; FFprobe 1920x1040 and 1280x760 at 15 fps for 72 s; visual before/after and final timeline review
Evidence: docs/OPENVISIONLAB_3D_IA4B_OWNER_PATH_REPLAY_20260729.md; artifacts/current/20260729-ia4b-owner-path-replay/
Boundary / next dependency: human-owner unaided R0 is external and still required before A-01 can close or SurfaceModel work begins; physical calibration and metrology are not claimed
```

## Next priorities

1. Human-owner Wide/Compact R0 replay | Prerequisite: owner performs the
   checklist above | Recommended model: none until the owner evidence exists |
   Reasoning effort: none
2. `J-01/J-03/J-04 SurfaceModel preparation foundation` | Prerequisite:
   owner R0 passes | Recommended model: `gpt-5.6-sol` | Reasoning effort:
   `high`
