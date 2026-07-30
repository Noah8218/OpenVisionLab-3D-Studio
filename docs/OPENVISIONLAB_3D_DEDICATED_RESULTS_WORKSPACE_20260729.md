# OpenVisionLab 3D Dedicated Results Workspace

Date: 2026-07-29

Status: Incomplete after actual Release IA-4 replay

## Actual Release supersession

The 2026-07-29 application-only novice replay supersedes the prior closure
claim. In the real MainWindow, Results is promoted to the correct full-height
pane, but its title/detail, three local navigation labels, immutable notice,
loaded Run Record evidence, command labels, and accessible names are blank.
The enabled Advanced gear also produces no visible transition.

The prior structural/non-mutation checks and generated captures remain useful
implementation evidence, but they did not prove the extracted view after live
dock recomposition. Preserve the recorded blocker and corrective gate in:

- `docs/OPENVISIONLAB_3D_NOVICE_STAGE_NAVIGATION_VIDEO_REVIEW_20260729.md`;
- `artifacts/current/20260729-novice-stage-navigation-video-review/`.

## Direction assessment

The current product direction remains correct: OpenVisionLab 3D Studio is a
local, file-first, deterministic 2.5D/3D rule-based inspection workbench.
Commercial GUI review supports separating operator responsibilities instead
of keeping every proven capability visible at once:

| Commercial lesson | OpenVisionLab decision |
| --- | --- |
| GoPxL separates configuration, teaching, run review, and diagnostics | Results is a dedicated stage, not a lower dock under the Viewer |
| SICK Nova separates Configure and Run | Results contains recorded evidence and no recipe mutation command |
| MERLIC presents inspection results by region and status | Run Record keeps ordered step status and evidence visible |
| HALCON exposes deep fit and matching diagnostics on demand | Fit, intersection, correspondence, messages, and performance remain in explicit Advanced/Tool Labs |
| Zivid and Photoneo use task-specific work surfaces | Results local navigation changes the evidence surface without changing the inspection state |

This does not expand the product into camera acquisition, PLC/robot/HMI,
stereo reconstruction, cloud/factory management, physical calibration, or
certified metrology.

## Implemented scope pending live integration repair

Results is now one full-height read-only workspace. It no longer combines a
dominant 3D Viewer with a compressed lower Run Record, and the former Results
`Save` command has been removed.

Results owns three local sections:

1. `Run Record`: immutable run/source/recipe identity, status, timing,
   ordered step results, recent records, and recorded threshold-correction
   evidence.
2. `Output Compare`: three view-only pinned artifact slots over the existing
   typed artifact registry.
3. `Reports & export`: current JSON, HTML, CSV, folder, and result-bundle
   actions with the recorded evidence summary.

An explicit `Advanced diagnostics` action opens the existing full dock layout.
Messages, performance, profile, fit, intersection, correspondence, and other
expert evidence are not part of the default Results composition.

## Preserved contracts

- Entering Results and changing its local section are presentation-only.
- Recipe identity, selected pipeline-step identity, step count, dirty state,
  current Viewer output summary, and Run Snapshot summary remain unchanged.
- Results does not expose Tool Library, recipe mutation, ROI handles,
  PropertyGrid Apply, sample-role editing, threshold Apply, Preview, Publish,
  Run, or Save.
- Run Record history remains bounded and reopening a recorded result does not
  execute inspection.
- Output comparison remains selection-only and uses the existing typed
  artifact identities.
- Advanced remains an explicit route and preserves recipe/output/run
  evidence when entering and returning.
- Existing deterministic inspection, Validation Set, and Runner contracts
  were not rewritten.

## Visual evidence

Current Release before implementation:

- `artifacts/current/20260729-results-workspace-extraction/before-results-wide.png`
- `artifacts/current/20260729-results-workspace-extraction/before-results-compact.png`

They show the Viewer consuming most of Results while Run Record is compressed
below it; Compact leaves the recorded evidence especially shallow.

Prior generated stage captures after implementation; these are not sufficient
live MainWindow acceptance evidence:

- `artifacts/current/20260729-results-workspace-extraction/after-results-wide.png`
- `artifacts/current/20260729-results-workspace-extraction/after-results-compact.png`
- `artifacts/current/20260729-results-workspace-extraction/after-results-output-compare-wide.png`
- `artifacts/current/20260729-results-workspace-extraction/after-results-reports-compact.png`

All screenshot-quality reports accepted attempt 1. Visual review confirms one
full-height Results task surface, persistent local navigation at both sizes,
readable ordered Run Record evidence, a three-slot Output Compare workspace,
and a Compact report/export surface without Setup or Teach editors.

## Verification

Current Release evidence:

- solution build: `0` warnings, `0` errors;
- docking, stage composition, Results local navigation, Advanced route, and
  non-mutation checks: `47/47`;
- Run Record load/recent/export/non-execution contracts: `10/10`;
- Artifact Navigator and Output Compare contracts: `31/31`;
- Shell smoke command-line options: `24/24`;
- code structure: `17/17`;
- all six before/after screenshot-quality reports: accepted on attempt 1;
- `git diff --check`: pass.

Reusable reports and captures are under:

`artifacts/current/20260729-results-workspace-extraction/`

## Completion record

```text
Status: Incomplete
Scope: IA-3 dedicated Results structure exists, but actual MainWindow stage-host integration is blank
Acceptance criteria: full-height Results ownership -> pass; visible Run Record/Output Compare/Reports labels and content in actual Release -> fail; visible Advanced round trip -> fail; non-mutation contracts -> pass in focused verification; current application-only Wide/Compact replay -> fail
Verification: prior build and focused checks remain green; actual user32/UI Automation video replay exposes empty localization/content/command bindings
Evidence: docs/OPENVISIONLAB_3D_DEDICATED_RESULTS_WORKSPACE_20260729.md, docs/OPENVISIONLAB_3D_NOVICE_STAGE_NAVIGATION_VIDEO_REVIEW_20260729.md, artifacts/current/20260729-results-workspace-extraction/, and artifacts/current/20260729-novice-stage-navigation-video-review/
Boundary / next dependency: repair stage-host ownership, add live MainWindow integration assertions, and repeat IA-4 before owner R0 or SurfaceModel
```

## Next priority

1. `IA-4a live stage-host ownership and MainWindow integration repair` |
   Recommended model: `gpt-5.6-sol` | Reasoning effort: `high`
2. `J-01/J-03/J-04 SurfaceModel preparation foundation` | Recommended model:
   `gpt-5.6-sol` | Reasoning effort: `high`
3. `J-06/J-08/J-09 scene matching, pose, and score` | Recommended model:
   `gpt-5.6-sol` | Reasoning effort: `high`
