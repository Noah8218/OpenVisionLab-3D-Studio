# OpenVisionLab 3D Novice Information Hierarchy and Accessibility

Date: 2026-07-29

Status: Complete

## Purpose

Close the P1 novice workflow gap that remained after Advanced Viewer
reactivation:

- Failure Analysis exposed technical tables before telling the operator what
  failed and what to do;
- Results led with Run Record sidecars and paths instead of the decision and
  correction route;
- the visible sample-set action declared an AutomationId in XAML but was not
  present in the live UI Automation tree.

The human owner's unaided R0 remains separate external acceptance.

## Scope and acceptance criteria

Included:

- current Release Wide and Compact before evidence;
- Failure Analysis operator-first summary;
- Results operator-first summary and correction route;
- one stable, keyboard-focusable sample-set action in the stage-level
  navigation surface;
- focused and full-route regression verification;
- current Release Wide and Compact final evidence.

Excluded:

- removal of detailed metrics, overlays, Run Record, reports, or export;
- automatic Preview, Publish, Run, or recipe mutation;
- camera, PLC, robot, cloud, physical calibration, or certified metrology.

Acceptance criteria:

1. Failure Analysis shows failed sample, failed rule, reason, and next action
   before detailed tables.
2. Results shows decision, executed-step summary, and correction route before
   sidecar paths and technical evidence.
3. `ValidationSetRunAllButton` is found directly in the live UI Automation
   tree in both layouts; no coordinate fallback is used.
4. The action remains explicit and keyboard-focusable.
5. Validation, Teach, Results, Advanced, and return-state contracts remain
   unchanged.

## Implementation

### Validate

The sample-set action moved from the content-only TabControl header into the
stable stage navigation surface. This gives it one durable live owner beside
the local Samples/Run Results/Failure Analysis/Threshold/Held-out navigation.
It retains:

- `AutomationId=ValidationSetRunAllButton`;
- a localized accessible name and help text;
- normal Tab focus plus Space/Enter activation;
- the existing explicit `RunValidationSetCommand`.

Failure Analysis now leads with one warning summary card:

```text
failed sample -> failed rule -> reason -> next action
```

The sample table, ordered step evidence, metrics, overlays, issue navigation,
and 3D comparison remain below it.

### Results

Results now leads with:

- final decision and key measurement;
- ordered-step summary;
- a short next-action explanation;
- an explicit `Fix in Teach` route when a selected validation failure exists.

Run Record, threshold sidecar state, JSON/HTML/CSV, export, paths, and
Advanced diagnostics remain secondary read-only evidence. The first visual
pass found insufficient foreground contrast in this new summary; the final
build explicitly uses the normal text brush and the final recordings verify
the correction.

## Verification

Build:

```powershell
dotnet build "OpenVisionLab.ThreeDStudio.sln" -c Release -p:Platform="Any CPU"
```

Result: `0` warnings, `0` errors.

Focused verification:

- Workbench docking and stage composition: `58/58` Pass;
- Validation Set: `84/84` Pass;
- stable sample-set action: accessible and runnable;
- Failure Analysis operator summary: selected sample, rule, and reason
  populated;
- Results operator summary and correction route: populated and
  keyboard-focusable.

The Validation Set verifier also had one stale fixture boundary: it copied a
failed step into a new Shell without its owning failed sample. The fixture now
copies both identities, matching the product's existing correction-context
contract. No product execution policy changed.

## Current Release evidence

Before:

- `artifacts/current/20260729-novice-hierarchy-accessibility/before/`;
- both layouts required `execute-validation-sample-set-layout-fallback`;
- Failure Analysis and Results were technical-first.

Final:

- `artifacts/current/20260729-novice-hierarchy-accessibility/final/`;
- Wide `1920 x 1040`, 15 fps, 110 s;
- Compact `1280 x 760`, 15 fps, 110 s;
- both layouts find
  `id=ValidationSetRunAllButton; name=샘플 세트 실행`;
- neither timeline contains missing, fallback, negative-coordinate, or failed
  postcondition events;
- both pass Advanced Viewer and final Failure Analysis assertions;
- both preserve `3 Pass / 2 Fail / 0 Error`.

Before keyframes:

- `before/analysis-keyframes/wide-failure-before.png`;
- `before/analysis-keyframes/compact-failure-before.png`;
- `before/analysis-keyframes/wide-results-before.png`;
- `before/analysis-keyframes/compact-results-before.png`.

Final keyframes:

- `final/analysis-keyframes/wide-failure-final.png`;
- `final/analysis-keyframes/compact-failure-final.png`;
- `final/analysis-keyframes/wide-results-final.png`;
- `final/analysis-keyframes/compact-results-final.png`.

The intermediate `after/` capture is development evidence only. It exposed
and led to the Results foreground-contrast correction; use `final/` as the
accepted current UI evidence.

## Direction decision

The current simulated-novice software route now has reusable current-Release
evidence for:

- stage discovery;
- direct sample-set execution;
- visible totals and failure selection;
- operator-first failure explanation;
- correction in Teach with source and ROI;
- operator-first Results summary;
- Advanced geometry;
- final validation-state preservation.

Do not spend additional model tokens repeating this automated route while the
source, requirements, or evidence validity remain unchanged. The immediate
prerequisite is now the human owner's unaided Wide/Compact R0. After that
passes, begin `J-01/J-03/J-04 SurfaceModel`.

## Completion record

Status: Complete

Scope: Validate/Results novice information hierarchy and live sample-set
accessibility for the current Release Wide/Compact owner path.

Acceptance criteria:

- operator-first Failure Analysis -> Pass;
- operator-first Results and correction route -> Pass;
- live UI Automation identity without fallback -> Pass in both layouts;
- keyboard-focusable explicit action -> Pass;
- Release build -> Pass (`0/0`);
- Workbench and Validation Set verification -> Pass (`58/58`, `84/84`);
- current final Wide/Compact videos -> Pass;
- state and explicit-execution contracts -> Pass.

Verification: Release build, focused Window-hosted checks, Validation Set
regression suite, actual-pointer UI Automation replay, FFprobe, timeline
negative scan, current-EXE timestamp gate, and before/final frame comparison.

Evidence:

- this document;
- `artifacts/current/20260729-novice-hierarchy-accessibility/before/`;
- `artifacts/current/20260729-novice-hierarchy-accessibility/final/`.

Boundary / next dependency: Human-owner unaided R0 is external and has not
been performed. Physical calibration and certified metrology remain
unverified.
