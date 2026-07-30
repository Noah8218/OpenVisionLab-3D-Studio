# OpenVisionLab 3D Teach Failure-Correction Context

Date: 2026-07-29

Status: Complete for the simulated-novice software slice; human-owner R0
remains external

## Outcome

`Validation -> Failure Analysis -> Fix in Teach` is now an actionable
correction route instead of navigation to an empty workspace.

The current Release application carries the selected failure into Teach and
shows:

- failed sample and Fail state;
- owning rule;
- operator-readable reason;
- exact failed/passed cell summary;
- explicit next action;
- the identified C3D source and applied ROI in the 3D Viewer.

At `1280 x 760`, the failure route temporarily gives the compact left
workspace to Selected Tool instead of requiring the operator to find a small
tab. Leaving Teach restores the normal Recipe Chain/Selected Tool ownership.
The route does not invoke Preview, Publish, Run, or change recipe semantics.

## Problems reproduced and resolved

| Direct-novice finding | Resolution | Current evidence |
| --- | --- | --- |
| Teach Viewer was empty after failure routing | Teach composition explicitly reattaches the current Viewer content and requests visible frames after layout reactivation | `keyframes/accepted-wide.png`, `keyframes/accepted-compact.png` |
| Failed sample context disappeared in Teach | Validation creates one read-only correction context containing sample, rule, reason, and cell summary | orange failure-correction card in both accepted captures |
| Compact opened Recipe Chain and hid correction controls | Compact failure correction uses a focused Selected Tool composition; normal composition is restored on stage exit | `keyframes/accepted-compact.png` |
| Video harness could miss the Failure Analysis transition | The pointer replay retries the visible Failure Analysis route when its next visible action is not yet present | final Wide/Compact timelines |

## Evidence

Historical before evidence:

- `artifacts/current/20260729-direct-novice-r0-replay/06-teach-from-failure.png`;
- `artifacts/current/20260729-direct-novice-r0-replay/12-compact-teach.png`.

Accepted current-Release evidence:

- `artifacts/current/20260729-teach-failure-correction/01-wide-ia4b-owner-path.mp4`
  (`1920 x 1040`, 15 fps, 42 seconds);
- `artifacts/current/20260729-teach-failure-correction/02-compact-ia4b-owner-path.mp4`
  (`1280 x 760`, 15 fps, 44 seconds);
- `artifacts/current/20260729-teach-failure-correction/keyframes/accepted-wide.png`;
- `artifacts/current/20260729-teach-failure-correction/keyframes/accepted-compact.png`;
- `artifacts/current/20260729-teach-failure-correction/media-verification.txt`;
- `artifacts/current/20260729-teach-failure-correction/workbench-docking-verification-final.txt`;
- final pointer timelines beside each video.

The recordings contain only the actual application window. Control lookup is
external UI Automation, but all workflow transitions use real `user32`
pointer movement and clicks. This is simulated-novice software evidence, not
human-owner acceptance.

## Verification

```text
dotnet build OpenVisionLab.ThreeDStudio.sln -c Release -p:Platform="Any CPU"
-> 0 warnings / 0 errors

dotnet run --no-build --project src/OpenVisionLab.ThreeD.Shell/OpenVisionLab.ThreeD.Shell.csproj -c Release -- --verify-workbench-docking artifacts/current/20260729-teach-failure-correction/workbench-docking-verification-final.txt
-> Pass 54/54

FFprobe
-> Wide 1920x1040, 15 fps, 42 s
-> Compact 1280x760, 15 fps, 44 s
```

Visual comparison:

- before: the Teach Viewer was blank and no failed-sample context was visible;
- after Wide: source, ROI, failed-sample card, rule, reason, and cell summary
  are simultaneously visible;
- after Compact: the same evidence is visible in a focused Selected Tool
  workspace without a manual tab click.

## Completion record

```text
Status: Complete
Scope: simulated-novice Validation failure -> Teach correction context, Teach Viewer reactivation, and Compact focused Selected Tool composition
Acceptance criteria: failed sample/rule/reason/cell summary carried into Teach -> pass; current source and ROI visible in Wide and Compact -> pass; Compact does not require a manual Selected Tool tab click -> pass; no hidden Preview/Publish/Run or recipe-semantic mutation -> pass
Verification: Release build 0 warnings / 0 errors; Workbench docking 54/54; app-only real-pointer Wide 1920x1040 42 s and Compact 1280x760 44 s; FFprobe and visual frame review
Evidence: docs/OPENVISIONLAB_3D_TEACH_FAILURE_CORRECTION_CONTEXT_20260729.md; artifacts/current/20260729-teach-failure-correction/
Boundary / next dependency: this does not complete human-owner R0, physical calibration, or metrology; A-01 remains Partial and SurfaceModel remains gated until the owner performs the unaided replay
```

## Next priorities

1. Human-owner Wide/Compact R0 replay | Prerequisite: owner operates the
   current Release unaided | Recommended model: none until owner evidence
   exists | Reasoning effort: none
2. `J-01/J-03/J-04 SurfaceModel` preparation foundation | Prerequisite:
   human-owner R0 passes | Recommended model: `gpt-5.6-sol` | Reasoning
   effort: `high`
