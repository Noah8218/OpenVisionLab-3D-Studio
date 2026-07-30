# OpenVisionLab 3D Direct Novice Repeat Analysis

Date: 2026-07-29

Status: Incomplete

Historical status note: This finding is superseded by
`docs/OPENVISIONLAB_3D_ADVANCED_VIEWER_REACTIVATION_20260729.md`. The newer
current-Release evidence restores the Advanced Viewer and deterministically
asserts the final visible state in both layouts. This document remains
`Incomplete` for the historical run it records. Its remaining P1 hierarchy
and accessibility findings are closed in
`docs/OPENVISIONLAB_3D_NOVICE_INFORMATION_HIERARCHY_AND_ACCESSIBILITY_20260729.md`.

## Purpose

This record repeats the current Release workflow as a first-time operator,
using only visible application controls and actual pointer clicks. It tests
whether the repaired Teach correction route remains understandable inside:

```text
Validate -> Failure Analysis -> Fix in Teach
-> Results -> Advanced -> Results -> Validate
```

The repeat does not replace the human owner's unaided R0 acceptance.

## Included and excluded scope

Included:

- current Release build;
- application-only Wide and Compact video;
- five-sample Completeness execution;
- failure review and correction routing;
- Results and Advanced navigation;
- visible state-preservation checks;
- novice discoverability, density, clipping, and accessibility review.

Excluded:

- implementation of the defects found in this review;
- camera, PLC, robot, cloud, or production-line integration;
- physical calibration, certified metrology, and production tolerances;
- a claim that Codex-operated replay is human-owner acceptance.

## Verification environment

Build:

```powershell
dotnet build "OpenVisionLab.ThreeDStudio.sln" -c Release -p:Platform="Any CPU"
```

Result: `0` warnings, `0` errors.

Replay:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass `
  -File ".\scripts\run-novice-stage-navigation-video-review.ps1" `
  -ArtifactDirectory "artifacts\current\20260729-direct-novice-r0-repeat" `
  -OwnerPath -WideDurationSeconds 68 -CompactDurationSeconds 68
```

Accepted media:

| Layout | Video | Verified media |
| --- | --- | --- |
| Wide | `01-wide-ia4b-owner-path.mp4` | `1920 x 1040`, 15 fps, 68 s |
| Compact | `02-compact-ia4b-owner-path.mp4` | `1280 x 760`, 15 fps, 68 s |

The videos, timelines, contact sheets, media verification, and extracted
review frames are under:

- `artifacts/current/20260729-direct-novice-r0-repeat/`.

## What passed

Both layouts visibly complete:

- discovery of Validate;
- explicit five-sample execution with `3 Pass / 2 Fail / 0 Error`;
- selection of Failure Analysis;
- `Fix in Teach`;
- selection of `Completeness Grid` in Teach;
- a rendered source and ROI in Teach;
- a read-only failed-sample correction card;
- Results and its supplied one-step Fail Run Record;
- entry into Advanced;
- return from Advanced to Results without an implicit Preview, Publish, Run,
  or recipe-semantic mutation.

Compact also visibly returns to preserved Failure Analysis evidence before
the recording ends.

The previous Teach failure-correction slice remains valid: neither Wide nor
Compact regressed to the earlier blank Teach Viewer.

## Findings

### P0 - Advanced loses the visible 3D frame in both layouts

After the same source and ROI render correctly in Teach, entering Advanced
from Results shows a completely dark `3D 검사 보기` pane in both Wide and
Compact. The surrounding Advanced docks and Run Record remain populated, so
this is not a whole-workspace data-loss symptom.

This blocks beginner diagnostics: the operator can reach Advanced but cannot
relate its technical evidence to visible geometry.

Source inspection suggests, but does not yet prove, a re-hosting/first-frame
defect:

- Teach explicitly reactivates its main Viewer and requests a visible frame;
- `MainWindow.UpdateViewerHost()` attaches `_viewer` to the Expert
  `Workspace.ViewerContent`;
- that Expert branch does not explicitly call the equivalent
  `ReactivateMainViewer(...)` or `_viewer.RequestVisibleFrame()` seam.

The next corrective slice must prove the root cause with a focused host
verification and fresh actual-Release video. The inference above is not a
completed diagnosis by itself.

### P1 - The contextual sample-set action is not discoverable through UI Automation

In both layouts, the visible `샘플 세트 실행` action could not be found by
its expected `AutomationId` or accessible name. The replay had to infer a
pointer coordinate from the visible Samples navigation anchor:

- Wide fallback: `x=1596, y=266`;
- Compact fallback: `x=956, y=266`.

A sighted operator may still click the button, but a first-time operator,
keyboard user, assistive technology, and robust acceptance replay do not get
a stable command identity. This also makes the local sample-set action easier
to confuse with the separate global Run All action.

### P1 - Failure Analysis presents clipped implementation evidence before an operator summary

Wide already clips parts of the metric and overlay columns. Compact clips
filenames, reasons, step evidence, metric labels, and overlay identifiers
more severely and requires horizontal scrolling.

The first readable layer should answer:

1. Which sample failed?
2. What rule failed?
3. What should the operator change?
4. Where is the failed geometry?

Raw cell IDs, internal overlay names, and long metric payloads should remain
available as drill-down evidence, not lead the workspace.

### P1 - Results is sparse, technical, and cramped at the same time

Results contains large unused areas while long sidecar paths and technical
evidence strings dominate the populated region. Compact clips row evidence
and crowds export/open actions.

The primary Results surface should lead with the inspection decision,
failed-step count, affected sample/region, and corrective route. Export,
paths, and schema detail should be secondary.

### P2 - Wide final preservation was not asserted by this replay

The Wide timeline records the final `Failure Analysis` pointer click at
approximately 70 seconds of total scenario elapsed time. Recording started
after setup, so the click was inside the accepted 68-second video interval.
However, the historical harness neither waited for a visible postcondition
nor retained a final post-click proof frame. Therefore:

- this is an assertion/evidence-retention defect;
- it is not evidence that the application rejected the click;
- Wide final Failure Analysis preservation remains unproven in this repeat;
- Compact visibly proves the same final preservation route.

The replay must keep recording until the last visible-state assertion passes
and capture a post-click frame, rather than treating a logged coordinate as
proof.

## Beginner workflow assessment

| Operator question | Result | Evidence |
| --- | --- | --- |
| Can I find the main stages? | Pass | Wide and Compact recordings |
| Can I run the supplied sample set? | Partial | Visual click works; stable accessible command identity is missing |
| Can I see pass/fail totals? | Pass | `3 Pass / 2 Fail / 0 Error` |
| Can I route a real failure into Teach? | Pass | Failure Analysis and correction card |
| Can I see the part and ROI while correcting? | Pass | Teach keyframes in both layouts |
| Can I understand Failure Analysis without decoding technical IDs? | Fail | Clipping and technical-first hierarchy |
| Can I inspect visible geometry in Advanced? | Fail | Wide and Compact Advanced Viewer are dark |
| Can I prove state preservation after the complete route? | Partial | Compact pass; Wide historical replay lacks a visible postcondition |

## Required next slice

1. Repair and verify Advanced Viewer reactivation after
   `Results -> Advanced`.
2. Give the contextual sample-set command a stable AutomationId, accessible
   name, keyboard path, and focused UI verification.
3. Make the replay wait for and visually assert its final state before the
   recorder stops.
4. Repeat current Release Wide and Compact actual-pointer video.
5. Keep Failure Analysis and Results hierarchy cleanup as the next UX slice
   unless the rerun exposes a more severe blocker.

SurfaceModel remains gated behind the corrected software replay and the
human owner's unaided R0.

## Completion record

Status: Incomplete

Scope: Current Release Wide/Compact simulated-novice full-route recording and
analysis only.

Acceptance criteria:

- current Release build -> Pass (`0` warnings, `0` errors);
- application-only Wide/Compact videos -> Pass (FFprobe-verified media);
- five-sample execution and failure-to-Teach route -> Pass;
- source and ROI visible in Teach -> Pass;
- source and ROI visible in Advanced -> Fail;
- final preserved Failure Analysis visible in both layouts -> Partial
  (Compact pass; Wide historical harness lacks a post-click visible assertion);
- findings and evidence recorded for reuse -> Pass.

Verification: Release build, actual-pointer owner-path replay, FFprobe media
verification, timeline inspection, extracted-frame visual review, and source
host-path inspection.

Evidence:

- `artifacts/current/20260729-direct-novice-r0-repeat/`;
- this document.

Boundary / next dependency: Superseded by
`docs/OPENVISIONLAB_3D_ADVANCED_VIEWER_REACTIVATION_20260729.md`. The
historical P0 and final-assertion gap are closed there. The historical P1
hierarchy/accessibility findings are closed in
`docs/OPENVISIONLAB_3D_NOVICE_INFORMATION_HIERARCHY_AND_ACCESSIBILITY_20260729.md`.
Human-owner R0 is still external. Physical calibration and metrology remain
unverified.
