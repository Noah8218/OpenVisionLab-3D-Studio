# OpenVisionLab 3D Dedicated Validate Workspace

Date: 2026-07-29

Status: Historical — Incomplete after the recorded Release IA-4 replay. Later
Workspace v3 closures supersede this as a current implementation priority;
the master backlog and human-owner R0 own the remaining acceptance state.

## Actual Release supersession

The 2026-07-29 application-only novice replay supersedes the prior closure
claim. The real MainWindow promotes Validate to the correct full-height pane,
but its five local labels and accessible names are blank, the matching
five-sample manifest is not rendered, and Run All is disabled.

The prior structural, deterministic Validation Set, and non-mutation checks
remain useful. They did not prove the extracted Validate view after live dock
recomposition. Preserve the blocker and corrective gate in:

- `docs/OPENVISIONLAB_3D_NOVICE_STAGE_NAVIGATION_VIDEO_REVIEW_20260729.md`;
- `artifacts/current/20260729-novice-stage-navigation-video-review/`.

## Direction assessment

The project direction is correct: OpenVisionLab 3D Studio should remain a
local, file-first, deterministic 2.5D/3D inspection workbench, but its proven
capabilities must be separated by operator responsibility instead of remaining
visible together.

The owner-supplied commercial GUI review directly informed this slice:

| Commercial lesson | OpenVisionLab decision |
| --- | --- |
| GoPxL separates configuration, teaching, monitoring, and diagnostics | Keep real Setup, Teach, Validate, and Results stages |
| SICK Nova separates Configure and Run and makes limits explicit | Keep sample replay and threshold review in Validate, not Teach |
| SICK Presence Inspection uses Good/Bad evidence before an explicit limit decision | Preserve Good/Bad/Held-out roles, deterministic candidates, Review/Cancel/Apply, and explicit replay |
| MERLIC keeps preparation flow readable while exposing region-level evidence | Keep sample and selected-step metrics/overlays linked without showing every table at once |
| Zivid Studio uses short analyze/propose/review/apply assistants | Keep threshold correction bounded and explicit; do not turn it into autonomous recipe mutation |
| HALCON exposes deep evidence when requested | Retain advanced evidence docks as opt-in diagnostics, not the default operator screen |

This does not expand the product into camera acquisition, PLC/robot/HMI,
stereo reconstruction, cloud/factory management, physical calibration, or
certified metrology.

## Implemented scope pending live integration repair

Validate is now an independent full-height workspace. The 3D Viewer, Setup
tool composition, Teach editor, and Run Record do not compete with validation
on the default screen.

Validate owns five local drill-down sections:

1. `Samples`: stage and label Good/Bad/Held-out inputs.
2. `Run results`: review the selected sample, step metrics, overlays, and
   labeled distributions.
3. `Failure analysis`: move between Fail/Error evidence and open the owning
   recipe step in Teach.
4. `Threshold review`: inspect deterministic candidates, exact errors,
   proposed typed changes, and development replay evidence.
5. `Held-out`: review the separately executed Held-out evidence without
   mixing it into development boundaries or ranking.

Wide and Compact use the same drill-down model. Compact does not retain the
former permanent lower validation table.

## Preserved contracts

- Stage navigation is presentation-only.
- Opening a failed validation step in Teach selects the existing pipeline step
  and does not dirty, execute, preview, publish, or reorder the recipe.
- Good/Bad/Held-out roles, sample SHA identities, exact candidate decisions,
  and Workbench/Runner execution contracts are unchanged.
- Held-out samples remain excluded from development boundaries, ranking,
  confusion counts, and candidate decisions.
- Threshold Review/Cancel/Apply and development/Held-out replay remain
  explicit.
- Active ROI Review, PropertyGrid draft, Preview, or Validation execution
  continues to block stage changes.
- Results retains the Viewer plus explicit Run Record evidence composition.

## Visual evidence

Current Release before implementation:

- `artifacts/current/20260729-validate-workspace-extraction/before-validate-wide.png`
- `artifacts/current/20260729-validate-workspace-extraction/before-validate-compact.png`

Prior generated stage captures after implementation; these are not sufficient
live MainWindow acceptance evidence:

- `artifacts/current/20260729-validate-workspace-extraction/after-validate-wide.png`
- `artifacts/current/20260729-validate-workspace-extraction/after-validate-compact.png`

All four screenshot-quality reports accepted attempt 1. Visual review confirms
that the former dominant Viewer plus compressed lower validation panel is
replaced by one full-height validation task surface. The five local sections
remain visible in one row at both `1920 x 1040` and `1280 x 760`.

## Verification

Current Release evidence:

- solution build: `0` warnings, `0` errors;
- docking and stage composition: `44/44`;
- Validation Set ordered graph, thresholds, correction, and Held-out:
  `84/84`;
- Inspection Workspace selection/ROI lifecycle: `63/63`;
- recipe teaching and save/reopen: `28/28`;
- Artifact Navigator and Output Compare: `31/31`;
- Shell smoke command-line options: `24/24`;
- code structure: `17/17`;
- `git diff --check`: pass.

Reusable reports and captures are under:

`artifacts/current/20260729-validate-workspace-extraction/`

## Completion record

```text
Status: Incomplete
Scope: IA-2 / A-10 dedicated Validate structure exists, but actual MainWindow stage-host integration is blank
Acceptance criteria: full-height Validate ownership -> pass; visible five-section labels and accessible names -> fail; matching five-sample manifest and enabled Run All -> fail; failure-to-Teach live path -> not reachable; deterministic Validation Set contracts -> pass in focused verification
Verification: prior build and focused checks remain green; actual user32/UI Automation video replay exposes empty localization/content/command bindings
Evidence: docs/OPENVISIONLAB_3D_DEDICATED_VALIDATE_WORKSPACE_20260729.md, docs/OPENVISIONLAB_3D_NOVICE_STAGE_NAVIGATION_VIDEO_REVIEW_20260729.md, artifacts/current/20260729-validate-workspace-extraction/, and artifacts/current/20260729-novice-stage-navigation-video-review/
Boundary / next dependency: A-01 and A-10 remain Partial until stage-host ownership is repaired and actual Release replay passes
```

## Next priority

1. `IA-4a live stage-host ownership and MainWindow integration repair` |
   Recommended model: `gpt-5.6-sol` | Reasoning effort: `high`
2. `IA-4b Compact and owner unaided stage replay` | Prerequisite: IA-4a |
   Recommended model: `gpt-5.6-terra` | Reasoning effort: `medium`
3. `J-01/J-03/J-04 SurfaceModel preparation foundation` | Recommended model:
   `gpt-5.6-sol` | Reasoning effort: `high`
