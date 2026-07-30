# OpenVisionLab 3D Direct Novice Replay Findings

Date: 2026-07-29

Status: Historical before evidence; the software findings are resolved by
`OPENVISIONLAB_3D_TEACH_FAILURE_CORRECTION_CONTEXT_20260729.md`

## Resolution update - 2026-07-29

The required correction slice is complete in the current Release:

- Teach reattaches and renders the identified source and ROI after
  `Fix in Teach`;
- sample, rule, reason, and exact failed/passed cell summary accompany the
  selected step;
- Compact opens a focused Selected Tool correction surface without a manual
  tab click;
- explicit Preview/Publish/Run and recipe semantics remain unchanged.

Preserve the findings below as historical before evidence. Current accepted
Wide/Compact video, screenshots, verification, and the completion record are
in:

- `docs/OPENVISIONLAB_3D_TEACH_FAILURE_CORRECTION_CONTEXT_20260729.md`;
- `artifacts/current/20260729-teach-failure-correction/`.

## Purpose

This review operates the current Release application as a first-time user by
looking at the visible UI and clicking visible controls. It does not invoke
hidden product commands or use control Automation IDs to choose the workflow.

The replay asks:

1. Can a novice find Validate and execute the intended five-sample set?
2. Can the novice understand the two failures?
3. Can the novice open a failed step in Teach and see what must be corrected?
4. Do Results, Advanced, and the return path preserve evidence?
5. Does the same workflow remain usable at `1280 x 760`?

## Inputs and recording

Current Release input:

- recipe:
  `artifacts/current/20260729-completeness-threshold-assistance/validation-set-fixture/completeness-threshold-fixture.ov3d-recipe.json`;
- Validation Set: `2 Good / 2 Bad / 1 Held-out`;
- supplied Fail Run Record:
  `artifacts/current/20260729-completeness-results-overlays/runner-record.json`.

Evidence root:

- `artifacts/current/20260729-direct-novice-r0-replay/`.

Videos:

- `01-direct-novice-wide.mp4`: `1920 x 1040`, 15 fps, 150 seconds;
- `02-direct-novice-wide-results.mp4`: `1920 x 1040`, 15 fps, 90 seconds;
- `03-direct-novice-compact.mp4`: `1280 x 760`, 15 fps, 75 seconds.

The first Wide segment includes an unrelated OpenVisionLab 2D foreground
window that interrupted the recording environment. It is not app-only 3D
Studio evidence and is excluded from product-pass claims. The second Wide
segment, Compact segment, and named screenshots are the UI analysis sources.

## What passed

- Setup, Teach, Validate, Results, and Advanced are recognizable at the top.
- Validate separates disabled global `Run all` from the active contextual
  `Run sample set`.
- Explicit execution returns `5 complete / 3 pass / 2 fail / 0 error`.
- Pass and Fail rows use color, status text, time, and a readable reason.
- Failure Analysis automatically selects a failed sample and its failed
  `Completeness Grid` step.
- `Fix in Teach` opens the owning recipe step without dirtying the recipe or
  starting Preview/Run.
- Results exposes a one-step Fail Run Record and an explicit Advanced route.
- Advanced -> Results -> Validate preserves the Run Record and Validation
  failure evidence.

## Blocking and priority findings

### P0 - Teach does not render the source after failure routing

Both Wide and Compact show a dark empty Viewer after
`Failure Analysis -> Fix in Teach`. The source is still identified as ready
and `Completeness Grid` is selected. Advanced immediately renders the same
`completeness-taught.C3D`, its surface, and both ROI overlays.

This blocks novice correction because the operator cannot see the part, ROI,
or failed cells in the screen where correction is expected.

Source inspection shows that stage recomposition moves the same Viewer
anchorable between panes, but `ApplyOperatorStage` does not explicitly
reactivate or request a first frame after composing Teach. This is a
root-cause hypothesis grounded in source and visual state, not yet a proven
fix.

Evidence:

- `06-teach-from-failure.png`;
- `12-compact-teach.png`;
- contrast with `08-advanced.png` and `15-compact-advanced.png`.

### P0 - Failure correction loses the failed-sample context

Teach selects the owning recipe step, but it does not retain a visible card
for:

- failed sample name and role;
- failed rule/cell count;
- expected versus actual value;
- failed-cell overlay or comparison target;
- direct return to the selected failure.

The current route proves navigation, not an actionable
failure -> correction loop.

### P1 - Failure Analysis exposes technical evidence before an operator summary

The right evidence area shows raw metric and overlay identifiers in narrow
columns. Values are clipped and require horizontal scrolling. Compact makes
this substantially worse.

Show one operator summary first:

```text
Failed sample: completeness-bad-low.C3D
Rule: Completeness Grid
Failed cells: 3 of 4
Reason: finite coverage / relative mean outside allowed range
Next action: inspect failed cells in Teach
```

Keep raw metric/overlay identifiers in a collapsed technical section.

### P1 - Compact Teach hides and compresses the correction surface

At `1280 x 760`, the operator must find the small bottom `Selected Tool` tab.
Once opened, ROI cards, coordinate fields, and actions are compressed into a
roughly 295-pixel column with extensive vertical scrolling. The failed
sample context is still absent.

When entering Teach from a failure, Compact should automatically activate a
focused correction surface rather than the normal Recipe Chain tab.

### P1 - Results is read-only but not decision-oriented

Results correctly preserves a Fail record, but most of the workspace is
empty and the one row is dominated by a technical evidence string. A novice
cannot connect it to the failed Validation sample or visual correction.

Add a selected-result summary with sample, rule, expected/actual, failed
cells, thumbnail/overlay, and `Open in Teach`.

### P2 - Primary Validate action is far from the sample table

At Wide size, `Run sample set` is aligned near the far-right edge. It remains
discoverable and is not blocking, but a contextual primary action placed
closer to the Validation title/summary would reduce scanning distance.

## Historical required next slice

Implement one cohesive failure-correction context slice:

1. reactivate/request the Teach Viewer first frame after stage recomposition;
2. prove the source and ROI render in both Wide and Compact;
3. carry a read-only selected-failure context from Validation into Teach;
4. automatically activate Compact Selected Tool/failure context;
5. add an operator-first failure summary while retaining collapsed technical
   evidence;
6. replay Results/Advanced/Validation and prove no mutation or hidden
   Preview/Run.

Do not change recipe semantics, sample roles, acceptance thresholds, or
execution contracts in this slice.

## Historical completion record

```text
Status: Incomplete
Scope: direct simulated-novice five-sample validation, failure-to-Teach correction, Results/Advanced return, and Compact usability review
Acceptance criteria: visible stage navigation -> pass; explicit 3/2/0 Validation result -> pass; failure-to-Teach route -> pass; visible actionable correction context in Teach -> fail because Viewer is blank and failed-sample context is absent; state preservation -> pass; Compact correction usability -> fail because Selected Tool is hidden by default and densely clipped
Verification: current Release build 0 warnings / 0 errors; FFprobe Wide 1920x1040 150 s and 90 s, Compact 1280x760 75 s, all 15 fps; direct screenshot and contact-sheet visual review; source inspection of stage composition and failure routing
Evidence: docs/OPENVISIONLAB_3D_DIRECT_NOVICE_REPLAY_FINDINGS_20260729.md; artifacts/current/20260729-direct-novice-r0-replay/
Boundary / next dependency: repair Teach Viewer reactivation and selected-failure context before repeating simulated-novice and human-owner R0; physical calibration and metrology are not claimed
```

## Superseded next priorities

1. Teach Viewer reactivation and selected-failure correction context |
   Recommended model: `gpt-5.6-sol` | Reasoning effort: `high`
2. Repeat current Release Wide/Compact direct novice replay |
   Recommended model: `gpt-5.6-terra` | Reasoning effort: `low`
3. Human-owner unaided R0 | Prerequisite: both replay defects pass |
   Recommended model: none until owner evidence exists | Reasoning effort:
   none
4. `J-01/J-03/J-04 SurfaceModel` | Prerequisite: owner R0 passes |
   Recommended model: `gpt-5.6-sol` | Reasoning effort: `high`
