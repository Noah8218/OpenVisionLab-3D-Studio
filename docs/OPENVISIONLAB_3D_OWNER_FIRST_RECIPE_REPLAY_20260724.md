# Owner first-recipe replay - 2026-07-24

## Status

Status: Blocked

The first owner replay stopped after identifying Viewer defects. Those defects
now have a separate corrective evidence checkpoint. The owner explicitly
deferred the second owner-only replay before completing the task. Completion
requires the owner's later direct result; automated replay is not a substitute.

## Recorded session - attempt 1

- Build: pass, `0` warnings and `0` errors
- Session start: `2026-07-24T11:49:09.5318830+09:00`
- Process: PID `10724`, stopped after owner feedback
- Window: `OpenVisionLab 3D Studio`
- EXE SHA-256:
  `E11C52C058FC6C30F8F5CECF694EF62218C143F8D44A4D1F28215F349E083274`
- Shell DLL SHA-256:
  `5D1F4B507F4AB4480B2E8F643C9F3B66331D2639C8DAF779618336370666B198`
- Viewer DLL SHA-256:
  `E0B754E78953928354DACBA3F46986E6B8AA62C3C46B8B9C3A59901F0B505FBA`
- Evidence:
  `artifacts/current/20260724-owner-first-recipe-replay/`

## Direct owner result

- Close zoom was insufficient.
- Double-click Fit was missing.
- The initial C3D presentation did not clearly match the expected inspection
  HeightField workflow.
- The Viewer still felt slow.
- The owner could not continue to recipe acceptance while these basic Viewer
  behaviors were unresolved.

The corrective Viewer checkpoint is recorded in
`docs/OPENVISIONLAB_3D_VIEWER_FIT_ZOOM_HEIGHTFIELD_20260724.md`.

## Recorded session - attempt 2

- Build: pass, `0` warnings and `0` errors
- Session start: `2026-07-24T13:19:57.6805090+09:00`
- Process: PID `8988`, exited after the replay was deferred
- Window: `OpenVisionLab 3D Studio`
- EXE SHA-256:
  `E11C52C058FC6C30F8F5CECF694EF62218C143F8D44A4D1F28215F349E083274`
- Shell DLL SHA-256:
  `7568AC567A90DC43263EE79136E45491FE70AA137E925F3ED17D8E272FF1454D`
- Viewer DLL SHA-256:
  `BD90325468288DD4AB515CDE89EFDEA28A0487FF83BB617FD1B5FCAF796475DB`
- Owner task:
  create a new inspection, load one 3D Map, add and teach one understandable
  compatible tool, invoke Preview deliberately, save, close, reopen, and check
  that the source, step, and parameters remain.
- Evidence:
  `artifacts/current/20260724-owner-first-recipe-replay/current-session.md`

## Recorded session - attempt 3

- Status: active, awaiting direct owner result
- Build: Release, `0` warnings and `0` errors
- Session start: `2026-07-24T16:56:16.0864517+09:00`
- Process: PID `12952`, visible window `OpenVisionLab 3D Studio`
- EXE SHA-256:
  `E11C52C058FC6C30F8F5CECF694EF62218C143F8D44A4D1F28215F349E083274`
- Shell DLL SHA-256:
  `B0041B35F5E671FA5F84A304A0C05D35740CFAB41A41D1C987DAAA45AC623D28`
- Viewer DLL SHA-256:
  `9165AF187F115B42ED0C465E1F067D8DE1B51856DFAFDE79EE050C58B22ABB7D`
- Owner task: use the visible input-first Release EXE without navigation
  guidance and complete criteria 1-7 below.
- Evidence:
  `artifacts/current/20260724-owner-first-recipe-replay/current-session.md`
  and `attempt3-release-start.png`
- Direct owner finding:
  the owner could not determine how to load the 3D input and teach a Thickness
  ROI without asking for guidance. This fails the unaided discoverability
  portion of criteria 2 and 3. The required inspector actions currently use
  the English labels `Capture selection` and `Apply selection`, which do not
  match the Korean guided-flow vocabulary.
- Corrective implementation:
  `docs/OPENVISIONLAB_3D_THICKNESS_ROI_GUIDED_TEACHING_20260724.md` now records
  the bilingual Thickness ROI teaching sequence and current Release actual-EXE
  capture-entry evidence. This closes the identified implementation defect,
  but not the owner-only acceptance gate. Attempt 3 must be treated as failed
  discoverability evidence and the replay must restart on the updated EXE.
- Owner follow-up finding:
  the owner reached the active Thickness `0/2` capture but still could not
  discover how to draw the ROI. Left-drag rotated the camera, and Step
  Parameters was visually separated from Inspection Flow. This proved the
  first text-only correction insufficient.
- Follow-up corrective implementation:
  `docs/OPENVISIONLAB_3D_THICKNESS_ROI_DRAW_GUIDANCE_20260724.md` records the
  reordered dock workflow, two-click/diagonal-drag GridRectangle interaction,
  bilingual in-Viewer prompt, compact ribbon correction, and actual pointer
  evidence at two resolutions. This closes the newly identified implementation
  defects, but the owner-only replay must still restart on the updated EXE.

## Recorded session - attempt 4

- Status: ended without a direct owner result
- Source commit:
  `a9c4bd945630ef6a3474da8da5314efcfbb7bc19`
- Build: Release, `0` warnings and `0` errors
- Session start: `2026-07-26T18:02:49.2861438+09:00`
- Process: PID `35236`, visible window `OpenVisionLab 3D Studio`
- EXE SHA-256:
  `9F5288E9DF4EA840855B7259EA1F0F334EDBD54BD4E88C69EA5379F51FE6FB0C`
- Shell DLL SHA-256:
  `330103EB1DC3BE3FC29341C23606818A68A4B038D52C8E3A63C37F02DC2DB3CE`
- Viewer DLL SHA-256:
  `80D6455BAC9B793A80ABEF12E9AD2DC6A37113C28845FDE50E2255254F63022E`
- Owner task: use the visible current Release EXE without navigation guidance
  and complete criteria 1-7 below.
- Evidence:
  `artifacts/current/20260726-owner-first-recipe-replay/current-session.md`
- Boundary: starting the current executable and recording its identity does not
  satisfy the owner-only acceptance gate. The owner result, elapsed time, and
  every confusing or blocking point are still required.

## Recorded session - attempt 5

- Status: failed startup continuity
- Source commit:
  `a9c4bd945630ef6a3474da8da5314efcfbb7bc19`
- Build: Release, `0` warnings and `0` errors
- Session start: `2026-07-26T18:05:42.0164391+09:00`
- Process: PID `17220`, responsive visible window `OpenVisionLab 3D Studio`
- EXE SHA-256:
  `9F5288E9DF4EA840855B7259EA1F0F334EDBD54BD4E88C69EA5379F51FE6FB0C`
- Shell DLL SHA-256:
  `330103EB1DC3BE3FC29341C23606818A68A4B038D52C8E3A63C37F02DC2DB3CE`
- Viewer DLL SHA-256:
  `80D6455BAC9B793A80ABEF12E9AD2DC6A37113C28845FDE50E2255254F63022E`
- Owner task: use the visible current Release EXE without navigation guidance
  and complete criteria 1-7 below.
- Evidence:
  `artifacts/current/20260726-owner-first-recipe-replay/current-session.md`
- Direct owner finding: restarting the application did not restore the last
  loaded recipe. The Workbench started as `Untitled 3D Inspection` with no
  source even though `recent-recipes.json` retained ordered recipe paths.
- Corrective implementation:
  `docs/OPENVISIONLAB_3D_LAST_RECIPE_STARTUP_RESTORE_20260726.md` records the
  normal-start restore path, automated-recent-state isolation, focused checks,
  structured log, and current actual Windows before/after evidence.
- Boundary: the correction closes this specific continuity defect but does not
  complete the full owner-only acceptance gate.

## Recorded session - attempt 6

- Status: failed inspection-step usability
- Source commit:
  `a9c4bd945630ef6a3474da8da5314efcfbb7bc19` plus the uncommitted
  last-recipe startup correction
- Build: Release, `0` warnings and `0` errors
- Session start: `2026-07-26T18:19:05.9448070+09:00`
- Process: PID `13540`, responsive visible window `OpenVisionLab 3D Studio`
- EXE SHA-256:
  `9F5288E9DF4EA840855B7259EA1F0F334EDBD54BD4E88C69EA5379F51FE6FB0C`
- Shell DLL SHA-256:
  `A1D4DCC17EA4731D604240F5F336BD3596A1632EFEC6CC3338991E432C9FA460`
- Viewer DLL SHA-256:
  `80D6455BAC9B793A80ABEF12E9AD2DC6A37113C28845FDE50E2255254F63022E`
- Startup result: the most recent available recipe
  `c3d-xyz-affine-teaching-template.ov3d-teach.json`, its C3D source, and its
  authored steps were restored. Preview, Run, and Publish remained untouched.
- Evidence:
  `artifacts/current/20260726-last-recipe-startup/` and
  `artifacts/current/20260726-owner-first-recipe-replay/current-session.md`
- Direct owner finding: the Filter PropertyGrid looked overlapped because its
  last row was clipped, and Inspection Flow did not make clear what could be
  done with a selected step or how to delete it.
- Corrective implementation:
  `docs/OPENVISIONLAB_3D_INSPECTION_FLOW_PROPERTY_GRID_USABILITY_20260726.md`
  records the expanded PropertyGrid, direct selected-step actions, navigator
  selection highlight, focused command verification, and current Release
  before/after evidence.
- Boundary: the correction closes these specific UI defects but does not
  complete the full owner-only acceptance gate.

## Recorded session - attempt 7

- Status: failed ROI discoverability and capture-state clarity
- Source commit:
  `a9c4bd945630ef6a3474da8da5314efcfbb7bc19` plus the uncommitted
  startup and inspection-flow usability corrections
- Build: Release, `0` warnings and `0` errors
- Session start: `2026-07-26T18:46:09.0217432+09:00`
- Process: PID `24316`, responsive visible window `OpenVisionLab 3D Studio`
- EXE SHA-256:
  `9F5288E9DF4EA840855B7259EA1F0F334EDBD54BD4E88C69EA5379F51FE6FB0C`
- Shell DLL SHA-256:
  `BFE14FB27A878CBD4A41F4F0D61D133C69B36357BCC7B55C6B243CF47AE6EAD2`
- Viewer DLL SHA-256:
  `80D6455BAC9B793A80ABEF12E9AD2DC6A37113C28845FDE50E2255254F63022E`
- Startup result: the most recent available recipe, source, and steps were
  restored. Inspection Flow exposes the selected-step actions and the Filter
  PropertyGrid rows are visually separated.
- Evidence:
  `artifacts/current/20260726-inspection-flow-property-grid-usability/`
- Direct owner findings:
  - Reference and Measurement ROI actions did not look actionable.
  - After a two-corner ROI was ready, the capture still felt as if it wanted
    the owner to keep drawing until Apply.
  - The ROI display-height control did not make the Y-height/Z-row coordinate
    distinction clear.
- Corrective implementation:
  `docs/OPENVISIONLAB_3D_COMMERCIAL_ROI_WORKFLOW_AND_REVIEW_MODE_20260726.md`
  records the official commercial-tool comparison, primary ROI actions,
  explicit post-`2/2` review mode, Enter Apply/Esc Cancel, height-axis label,
  shortcut set, and current Release actual-pointer evidence.
- Boundary: the direct owner result remains the only completion evidence for
  criteria 1-7. The replay must restart on the corrected Release build.

## Recorded session - attempt 8

- Status: active, awaiting direct owner result
- Source commit:
  `a9c4bd945630ef6a3474da8da5314efcfbb7bc19` plus the uncommitted
  startup, Inspection Flow, shortcut, and commercial ROI workflow corrections
- Build: Release, `0` warnings and `0` errors
- Session start: `2026-07-26T20:15:40.7767437+09:00`
- Process: PID `27588`, responsive visible window `OpenVisionLab 3D Studio`
- Recipe:
  `C:\Git\OpenVisionLab-3D-Studio\3D\Synthetic Thickness Coupon v1\new-inspection.ov3d-recipe.json`
- EXE SHA-256:
  `9F5288E9DF4EA840855B7259EA1F0F334EDBD54BD4E88C69EA5379F51FE6FB0C`
- Shell DLL SHA-256:
  `CAFFA8B419A04FE6EC99EDE1EE843E9A3F8DF1DC620456FECBCD94B99AF983F5`
- Viewer DLL SHA-256:
  `310BE00FF05B1067A7D9DFE6618FF1E4B1425C84267BB1CB3FD87DB581365DCD`
- Startup result: the owner's saved Reference-only Thickness draft and its
  C3D source were opened in the current Release build. Preview, Run, and
  Publish remain untouched.
- Owner task: use the primary Measurement `ROI 그리기` action, draw the two
  corners, confirm that the UI visibly changes to review mode, adjust the ROI
  or Y display handle if needed, press Enter or Apply, then continue the
  explicit Preview/save/reopen workflow.
- Evidence:
  `artifacts/current/20260726-shortcuts-and-roi-capture/`
- Boundary: current build/process identity and automated evidence do not
  replace the owner's unaided criteria 1-7 result.

## Pass criteria for the next replay

1. A named zero-step recipe can be created and saved before adding a tool.
2. The owner can find and load a 3D Map without navigation guidance.
3. Exactly one compatible tool can be added and its input, parameters, and
   output can be understood.
4. Editing does not execute inspection automatically; Preview is invoked
   deliberately.
5. Save, close, and reopen retain source, step, and parameters.
6. No clipping, unexplained disabled state, duplicate window/dialog, or
   unexpected mutation blocks completion.
7. Elapsed time and every confusing label, pane, or decision are recorded.

## Completion record

Status: Blocked

Scope: Current-build owner-only first-recipe creation, input load, one-tool
teaching/Preview, save, close, and reopen replay.

Acceptance criteria: Attempt 1 failed to reach criteria 1-7 because the owner
stopped at the Viewer usability defects listed above. Attempt 2 was deferred
before an owner result was produced. Attempt 3 failed the unaided
discoverability portions of criteria 2 and 3; the corrective UI has separate
automated and actual-EXE evidence but has not been owner-replayed.

Verification: The recorded build/process identity is valid owner-session
evidence. The separate corrective Viewer checkpoint passes its own automated
and actual-pointer gates; it does not substitute for this owner recipe replay.

Evidence: `artifacts/current/20260724-owner-first-recipe-replay/` and this
document.

Boundary / next dependency: the owner later resumes the unaided replay and
reports completion, elapsed time, and every confusing or blocking point. This
external evidence does not block internal product development.
