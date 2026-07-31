# OpenVisionLab 3D Studio — Workbench v4 Evidence And Safe Layout

Date: 2026-07-30
Status: Complete — v4-2 Validate/Results and v4-3 visual/layout slices

## 1. Outcome

Workbench v4 is complete as three bounded presentation slices:

1. v4-1: one Job Bar, responsive responsibility rail, and unified Authoring;
2. v4-2: Validate and Results evidence composed beside the same Viewer;
3. v4-3: one graphite visual system and a versioned, presentation-only layout
   profile.

The product remains a local, file-first, deterministic 2.5D/3D rule-based
inspection workbench. This work changes presentation and evidence navigation;
it does not add sensor acquisition, PLC/robot/cloud integration, physical
calibration, certified metrology, or a new inspection algorithm.

## 2. v4-2 Evidence-Centered Validate And Results

### Validate

Validate now uses one full-height evidence pane beside a dominant Viewer.
The evidence pane keeps the existing five operator sections:

- Samples;
- Run Results;
- Failure Analysis;
- Threshold Review;
- Held-out.

Selecting a staged sample presents that sample's C3D source in the Viewer and
synchronizes the authored ROI for visual comparison. This is a display-only
route: selection does not change recipe source routing, alter the recipe,
start Preview, publish an output, or run the sample set. Running the five
samples remains the explicit `Run sample set` action.

The normal evidence workflow is:

```text
select sample
  -> inspect the same sample in Viewer
  -> inspect Pass/Fail/Error and failed-cell evidence
  -> choose Fix in Teach when correction is needed
  -> explicitly edit/apply in Teach
  -> explicitly rerun the development or Held-out set
```

Good, Bad, and Held-out roles and their development/held-out exclusion rules
are unchanged.

### Results

Results is a read-only evidence workspace beside the Viewer. It leads with:

- final decision;
- affected/executed step summary;
- the next operator action;
- Run Record, Output Compare, and Reports/export sections.

The supplied schema `1.5` Fail Run Record is restored after command-line recipe
load and remains visible in the Results workspace. Advanced analysis is an
explicit route. Results does not expose parameter editing or Save and does not
execute inspection.

### Layout contract

Wide Validate and Results default to evidence `1.60*` and Viewer `2.70*`.
Compact defaults to evidence `1.05*` and Viewer `2.45*`. User-adjusted safe
ratios are remembered independently for Wide and Compact layouts.

## 3. v4-3 Graphite Visual System

The application now uses one high-contrast graphite role system:

- application background: `#090E15`;
- panel surfaces: `#141B24` and `#1B2531`;
- command surface: `#0E151E`;
- controls: `#202B38`;
- dividers: `#334155`;
- primary accent/focus: `#39C6C1`;
- semantic Pass, Warning, Fail, and Information colors remain distinct.

The Viewer keeps its scientific height-color scale independent from Shell
chrome. Familiar icon-only actions are allowed only when they have a workflow
purpose, localized accessible name, stable AutomationId, and tooltip.
Ambiguous actions retain text.

## 4. Safe Layout Profile Contract

The normal application stores schema `1` at:

```text
%LocalAppData%\OpenVisionLab\ThreeDStudio\studio-layout-v1.json
```

Automated Shell runs ignore that user profile unless an explicit smoke profile
path is supplied.

### Allowlisted persisted fields

- normal/maximized window placement;
- Wide and Compact pane ratios for Authoring, Validate, Results, and legacy
  workspaces;
- Workbench and Advanced presentation profiles;
- selected primary content ID:
  `three-d-viewer` or `displayed-outputs`;
- selected support content ID:
  `data-layers`, `tool-library`, or `tool-inspector`.

### Explicitly excluded fields

- recipe content or dirty state;
- source identity or source routing;
- selected validation sample and role;
- parameter drafts;
- ROI draft/capture state;
- Preview, Publish, Run, or Validation command state;
- inspection results, Run Records, or thresholds;
- arbitrary AvalonDock type names or serialized layout XML.

### Restore safety

- ratios must be finite and between `0.20` and `8.00`;
- a restored window must be at least `1180 x 720` and intersect the current
  virtual desktop;
- unknown content IDs, unsafe ratios, and off-screen placement fall back only
  to their defaults;
- incompatible schema and corrupt JSON load defaults;
- corrupt/incompatible profiles are not silently overwritten by auto-save;
- writes use a temporary file and atomic replace/move, with temporary cleanup;
- `Reset layout` deletes only the exact profile and reapplies defaults;
- restore and reset never invoke Preview, Publish, Run, Validation, or recipe
  mutation.

Example operator workflow:

```text
resize evidence and Viewer panes
  -> close normally
  -> reopen
  -> safe presentation ratios return
  -> review the visible restored state
  -> choose Reset layout when defaults are wanted
```

Restoration never performs the operator's inspection task.

## 5. Acceptance Evidence

| Gate | Current result |
|---|---|
| Release solution build | Pass — `0` warnings, `0` errors |
| Workbench/docking and layout safety | Pass — `71/71` |
| Validation Set | Pass — `84/84` |
| Inspection Workspace selection/non-execution | Pass — `63/63` |
| C3D Height distribution/range | Pass — `25` checks |
| Code structure | Pass — `17/17` |
| Wide Validate | Pass — `1920 x 1040`, screenshot quality accepted |
| Compact Validate | Pass — `1280 x 760`, screenshot quality accepted |
| Wide Results | Pass — `1920 x 1040`, screenshot quality accepted |
| Compact Results | Pass — `1280 x 760`, screenshot quality accepted |
| First close/reopen | `Missing -> Restored`; no draft, ROI capture, Preview, or Validation run |
| Corrupt profile | `Corrupt -> defaults`; corrupt source preserved; no execution |
| R0 package prerequisite | Wide and Compact `-ValidateOnly` pass with current fixed hashes |

Evidence:

- `artifacts/current/20260730-gopxl-workbench-v4-evidence-and-layout/before/`;
- `artifacts/current/20260730-gopxl-workbench-v4-evidence-and-layout/after/`;
- `artifacts/current/20260730-gopxl-workbench-v4-evidence-and-layout/layout-reopen-smoke/`;
- `artifacts/current/20260730-gopxl-workbench-v4-evidence-and-layout/final-workbench-docking.txt`;
- `artifacts/current/20260730-gopxl-workbench-v4-evidence-and-layout/final-validation-set.txt`;
- `artifacts/current/20260730-gopxl-workbench-v4-evidence-and-layout/final-inspection-workspace.txt`;
- `artifacts/current/20260730-gopxl-workbench-v4-evidence-and-layout/final-height-distribution.txt`;
- `artifacts/current/20260730-gopxl-workbench-v4-evidence-and-layout/final-code-structure.txt`.

## 6. Completion Record

```text
Status: Complete
Scope: Workbench v4-2 evidence-linked Validate/Results and v4-3 graphite visual system plus safe layout save/restore/reset
Acceptance criteria: Viewer-linked evidence, read-only Results, explicit execution boundaries, allowlisted persisted presentation, safe fallback/reset, current Wide/Compact captures, and focused regressions -> Pass
Verification: Release 0/0; Workbench 71/71; Validation Set 84/84; Inspection Workspace 63/63; Height distribution 25 checks; structure 17/17; layout Missing/Restored/Corrupt round trips; Wide/Compact R0 package ValidateOnly pass
Evidence: docs/OPENVISIONLAB_3D_GOPXL_WORKBENCH_V4_EVIDENCE_AND_SAFE_LAYOUT_20260730.md; artifacts/current/20260730-gopxl-workbench-v4-evidence-and-layout/
Boundary / next dependency: This does not prove the human owner's unaided Wide/Compact R0, physical calibration, certified metrology, or commercial-platform integration. R0 is prepared but remains externally blocked until the owner performs both runs.
```
