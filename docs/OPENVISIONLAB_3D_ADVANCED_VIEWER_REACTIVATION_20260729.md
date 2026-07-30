# OpenVisionLab 3D Advanced Viewer Reactivation

Date: 2026-07-29

Status: Complete

Continuation note: The P1 information-hierarchy and sample-set accessibility
dependency recorded below is complete in
`docs/OPENVISIONLAB_3D_NOVICE_INFORMATION_HIERARCHY_AND_ACCESSIBILITY_20260729.md`.

## Purpose

Close the current simulated-novice software blocker in:

```text
Validate -> Failure Analysis -> Fix in Teach
-> Results -> Advanced -> Results -> Validate
```

The previous current-Release replay preserved Teach correction context but
showed a dark Advanced Viewer in both Wide and Compact. It also logged the
last Failure Analysis click without asserting that the final state was
visible.

## Included and excluded scope

Included:

- explicit Viewer release from the nested Teach host;
- explicit Advanced dock and live `ContentPresenter` reactivation;
- a post-layout visible-frame request;
- visible-element filtering in the actual-pointer replay;
- deterministic Advanced and final Failure Analysis assertions;
- current Release Wide and Compact replay.

Excluded:

- redesign of Failure Analysis or Results information hierarchy;
- correction of the contextual sample-set command's missing live UI
  Automation identity;
- human-owner unaided R0 acceptance;
- camera, PLC, robot, cloud, calibration, or certified metrology.

## Root cause and repair

The blank Viewer was a two-part host-ownership defect. Updating
`Workspace.ViewerContent` was insufficient after the Viewer had been hosted
inside Teach:

1. the nested Teach `ViewerWorkspaceView` still needed an explicit release;
2. the AvalonDock workspace dependency property could contain the Viewer
   while its live `ContentPresenter` did not.

A frame request alone did not repair that ownership mismatch.

The repair:

- explicitly releases the main Viewer from Teach before entering Advanced;
- clears the Teach Viewer content;
- reattaches the requested Viewer to both the Advanced workspace dependency
  property and its live presenter;
- selects and activates the Advanced Viewer pane;
- requests a visible frame after layout reaches `ContextIdle`.

The focused docking verifier now checks that Advanced owns the exact requested
Viewer in its live presenter.

## Replay hardening

The current replay now:

- rejects UI Automation matches that are off-screen, zero-sized, or outside
  the application window;
- requires the visible `ViewerFitAll` control after entering Advanced;
- requires the visible `OpenValidationIssueInTeach` action after the final
  return to Failure Analysis;
- fails the run when either visible postcondition is absent.

The contextual sample-set command is retried through UI Automation and then
uses the existing layout-derived pointer fallback. The fallback keeps this
visible-pointer replay executable, but it does not close the accessibility
defect.

## Verification

Build:

```powershell
dotnet build "OpenVisionLab.ThreeDStudio.sln" -c Release -p:Platform="Any CPU"
```

Result: `0` warnings, `0` errors.

Focused integration:

```text
Workbench docking verification: 55/55 Pass
Advanced reactivation owns the requested Viewer in its live presenter:
viewer=True
```

Current Release application-only actual-pointer replay:

| Layout | Video | Verified media | Required postconditions |
| --- | --- | --- | --- |
| Wide | `01-wide-ia4b-owner-path.mp4` | `1920 x 1040`, 15 fps, 110 s | Advanced Viewer visible; final Failure Analysis action visible |
| Compact | `02-compact-ia4b-owner-path.mp4` | `1280 x 760`, 15 fps, 110 s | Advanced Viewer visible; final Failure Analysis action visible |

Both layouts:

- execute five samples with `3 Pass / 2 Fail / 0 Error`;
- preserve the identified C3D source, ROI, selected step, validation evidence,
  and Run Record;
- render the surface, ROI, Viewer controls, and HUD in Advanced;
- visibly return to the preserved Failure Analysis state;
- introduce no implicit Preview, Publish, Run, or recipe-semantic mutation.

Evidence:

- `artifacts/current/20260729-advanced-viewer-reactivation/`;
- `artifacts/current/20260729-advanced-viewer-reactivation/workbench-docking-verification.txt`;
- `artifacts/current/20260729-advanced-viewer-reactivation/analysis-keyframes/wide-advanced-visible.png`;
- `artifacts/current/20260729-advanced-viewer-reactivation/analysis-keyframes/compact-advanced-visible.png`;
- `artifacts/current/20260729-advanced-viewer-reactivation/analysis-keyframes/wide-final-failure-visible.png`;
- `artifacts/current/20260729-advanced-viewer-reactivation/analysis-keyframes/compact-final-failure-visible.png`.

## Historical next product priority

At this checkpoint, the next software slice was the P1 novice information and
accessibility gap:

1. lead Failure Analysis with sample, failed rule, corrective action, and
   geometry before raw cell and overlay identifiers;
2. lead Results with decision, failed-step count, affected sample/region, and
   correction route before paths and schema detail;
3. make the contextual sample-set action discoverable through the live UI
   Automation tree with a stable name and keyboard path;
4. repeat the current Release Wide/Compact route after that visible change.

The human owner's unaided R0 remains an external acceptance prerequisite.
SurfaceModel begins only after that gate passes unless the owner explicitly
reprioritizes it.

## Completion record

Status: Complete

Scope: Advanced Viewer reactivation and deterministic final-state assertions
for the current Release simulated-novice Wide/Compact route.

Acceptance criteria:

- current Release build -> Pass (`0` warnings, `0` errors);
- exact live Advanced Viewer ownership -> Pass (`55/55` focused checks);
- visible source, ROI, controls, and HUD in Advanced -> Pass in both layouts;
- visible final Failure Analysis preservation -> Pass in both layouts;
- application-only Wide/Compact media -> Pass (FFprobe-verified);
- no implicit execution or recipe-semantic mutation -> Pass.

Verification: Release build, focused Window-hosted docking verification,
PowerShell parser check, current-Release actual-pointer replay, timeline
postcondition inspection, FFprobe media verification, and extracted-frame
visual comparison.

Evidence: This document and
`artifacts/current/20260729-advanced-viewer-reactivation/`.

Boundary / next dependency: The historical P1 dependency is superseded by
`docs/OPENVISIONLAB_3D_NOVICE_INFORMATION_HIERARCHY_AND_ACCESSIBILITY_20260729.md`.
Human-owner R0 is external; physical calibration and metrology remain
unverified.
