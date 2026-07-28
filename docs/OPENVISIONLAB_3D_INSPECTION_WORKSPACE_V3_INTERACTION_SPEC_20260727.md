# Inspection Workspace v3 Interaction Specification

Date: 2026-07-27
Status: Complete for interaction design and implementation mapping

## Outcome

This specification converts the supplied GoPxL video findings into an
OpenVisionLab-owned interaction contract. It is the required design gate before
changing the current default Workbench.

The first implementation target is the exact
`synthetic-thickness-coupon-v1.C3D` eight-Tab Thickness workflow.
No other tool UI should expand the default path until this slice passes owner
acceptance.

## Fixed product boundaries

- OpenVisionLab remains a local, rule-based 3D inspection recipe workbench.
- The Viewer default remains `Surface`.
- `Top`, `Perspective`, and `Profile` are camera/view modes. They do not change
  the selected source, display geometry, recipe, or inspection result.
- ROI display and handle movement may update immediately.
- Preview, Publish, Run, save, and route changes remain explicit.
- Output visibility, pinning, split Viewer, layer visibility, and camera
  changes never execute inspection.
- `GridRectangle` remains an X=column/Z=row height-field footprint.
- An XYZ volume requires a separate typed selection and is not part of v3.
- Sensor, camera, PLC, robot, HMI, cloud, account, and production-line
  integration remain out of scope.

## Default wide layout

```text
+--------------------------------------------------------------------------+
| recipe | input | saved/dirty/stale | Preview | Run All | Save            |
+-------------+---------------+------------------+-------------------------+
| Tool        | Recipe chain  | Selected tool    | 3D Viewer               |
| catalog     |               |                  |                         |
|             | Source        | Inputs           | Top/Perspective/Profile |
| search      |  +- Group     | Parameters       | Surface/Height/Points   |
| compatible  |     +- Tab 1  | Regions          | Fit all/Fit ROI/Split   |
| all tools   |     +- Tab 2  | Outputs          |                         |
|             |     +- ...    | Help             | ROI + result overlays   |
+-------------+---------------+------------------+-------------------------+
| Problems | Messages | Performance | Validation                         |
+--------------------------------------------------------------------------+
```

Nominal 1920-width allocation:

| Region | Target width | Rule |
| --- | ---: | --- |
| Tool Catalog | 220-240 px | Search and add only |
| Recipe Chain | 280-310 px | Ordered structure and selection |
| Selected Tool | 340-380 px | The only default configuration surface |
| Viewer | Remaining, normally 50% or more | Dominant inspection surface |

At 1280 width, Tool Catalog and Recipe Chain become tabs inside one
approximately 280 px left region. Selected Tool remains visible beside the
Viewer. Problems and advanced evidence remain collapsed unless selected or a
real issue opens them.

## Global command bar

The permanent command bar contains only:

- recipe name;
- current input name and grid readiness;
- saved/dirty state;
- selected-step `Ready / Dirty / Preview stale / Preview current / Error`;
- explicit Preview;
- explicit Run All;
- explicit Save.

The five-stage journey strip is removed from the permanent work surface.
First-use help belongs to the empty state and selected-tool Help section, not
to a permanent header.

## One synchronized selection

The Workbench has one `WorkspaceSelection`:

```text
recipe step identity
  + selected input identity
  + active ROI role/selection identity
  + selected output identity
  + focused Viewer slot
```

Changing the selected Tab or step updates:

- the highlighted Recipe Chain row;
- Selected Tool title and state;
- Inputs, Parameters, Regions, Outputs, and Help;
- active ROI role and Viewer highlight;
- selected output evidence.

Selection alone does not edit the recipe or execute a tool.

## Selected Tool contract

### Inputs

Each input row contains:

- required typed contract;
- current entity label and ID;
- state: current, declared, stale, missing, or ambiguous;
- frame and unit when available;
- an explicit selector when more than one compatible candidate exists.

When a tool is added, the nearest preceding compatible artifact is proposed.
It is selected automatically only when the choice is unique and deterministic.
The chosen route is shown immediately. An ambiguous route remains unresolved
until the operator selects one.

Adding or routing does not Preview or Run.

### Parameters

- Use the existing typed PropertyGrid draft owner.
- Keep Apply and Discard explicit.
- Show invalid values beside the field and in Problems.
- Applying a parameter changes the recipe draft and marks Preview stale.
- Do not duplicate the same parameter in a second editor.

### Regions

Thickness always displays two compact role rows:

| Role | Commands | State |
| --- | --- | --- |
| Reference ROI | Draw, Edit, Delete, Fit ROI | Missing, Drawing, Review, Applied |
| Measurement ROI | Draw, Edit, Delete, Fit ROI | Missing, Drawing, Review, Applied |

The selected row and Viewer use the same role identity. The active candidate
is yellow. Other authored regions use stable role colors and visible labels.

Numeric `column`, `row`, `column count`, and `row count` remain available in
the same Regions section. They are not placed below unrelated evidence.

### Outputs

Each output row contains:

- enabled state;
- current/stale state;
- value and unit;
- Pass/Fail/Error when the tool has a tolerance result;
- `Show`, `Pin`, and `Compare` commands when applicable.

Feature outputs remain overlays. A feature entity is never fabricated as a
full surface.

### Help

A collapsed Help section contains:

- one sentence describing the selected tool;
- required inputs;
- one short authoring sequence;
- the output meaning and unit boundary.

## Viewer contract

### Default state

- geometry: `Surface`;
- color: `Height`;
- view mode for a newly loaded height field: near-top inspection fit, as
  currently verified;
- no inspection execution on load or view change.

### First-class view controls

| Control | Behavior |
| --- | --- |
| Top | True orthographic X/Z view for ROI positioning |
| Perspective | Perspective camera with orbit and pan |
| Profile | Existing profile workflow |
| Surface | Existing surface geometry |
| Height | Existing height color map |
| Points | Existing point presentation |
| Fit all | Current full-source fit |
| Fit ROI | Fit the selected authored/candidate ROI |
| Split vertical/horizontal | Create another Viewer slot |
| Pop out | Move a Viewer slot to a reusable window |

Each Viewer slot owns its display mode and pinned outputs. These are
presentation-only states.

### ROI state machine

```text
Missing
  -> Draw
  -> Capturing
  -> mouse-up or second corner
  -> Review
       -> handle/numeric correction
       -> Apply -> Applied
       -> Cancel -> previous authored state
```

In `Review`:

- no third capture point is accepted;
- empty-space left drag returns to normal Viewer orbit;
- corner and center handles remain active;
- Enter applies and Esc cancels;
- Preview remains stale until explicitly invoked.

## Exact eight-Tab click path

The path below is the acceptance script and must be runnable without reading a
separate manual.

| Step | Operator action | Visible response | Recipe mutation | Inspection execution |
| ---: | --- | --- | --- | --- |
| 1 | `Ctrl+N` | Empty recipe, Tool Catalog ready | New draft | None |
| 2 | `Ctrl+Shift+O`, select the exact C3D | Surface Viewer, `1280 x 840`, Source row current | Source identity recorded | None |
| 3 | Search `Thickness`, click `Add` | `Tab 1 Thickness` selected | One typed step added | None |
| 4 | Confirm the uniquely suggested `source.c3d.height-map` input | Input row becomes Ready | Route recorded | None |
| 5 | Click `Top`, Reference ROI `Draw`, drag, review, Apply | Yellow Reference ROI, then applied role color | Reference GridRectangle saved | None |
| 6 | Measurement ROI `Draw`, drag, review, Apply | Yellow Measurement ROI, then applied role color | Measurement GridRectangle saved | None |
| 7 | Set minimum, maximum, and valid-sample count; Apply | Step becomes Ready / Preview stale | Parameters saved | None |
| 8 | Click Preview or press `F5` | Mean/min/max result and measurement overlay become current | No recipe route change | Selected step only |
| 9 | Click `Repeat as grid`; set `4 columns x 2 rows`, column pitch `228`, row pitch `690`, name `Tab {n}`; review, Apply | Eight candidate Tab pairs, then a `Thickness group (8)` | Eight steps and 16 unique ROI identities | None |
| 10 | Select Tab 1 through Tab 8 and correct any ROI requiring local refinement | Chain, Selected Tool, and Viewer stay synchronized | Explicit per-Tab edits only | None |
| 11 | Press `Ctrl+F5`, review eight records, then `Ctrl+S` | `8/8` current self-test records and saved state | Recipe saved | Explicit full recipe |
| 12 | Close/reopen the recipe | Names, routes, 16 ROIs, parameters, and eight outputs restored | None | None |

The broad `[-100000, 100000] raw-height` values are software-connectivity
limits. A Pass in this flow is not a production disposition.

## Bounded 4 x 2 repeat authoring

The exact current model demonstrates this layout:

- first-row Reference columns: `515`, `744`, `972`, `1198`;
- first-row Measurement columns: `575`, `800`, `1028`, `1255`;
- second-row start: row `1120` versus first-row row `430`;
- nominal column pitch: `228` grid columns;
- nominal row pitch: `690` grid rows.

`Repeat as grid` is a recipe-authoring operation:

1. starts from one complete Thickness instance;
2. accepts row/column count, row/column pitch, and name pattern;
3. creates a display-only candidate for every translated ROI pair;
4. validates all rectangles against the exact source grid;
5. Apply creates ordinary independent steps, selections, and outputs;
6. Cancel leaves the recipe unchanged;
7. never invokes Preview, Publish, or Run.

Local geometry differences remain editable per Tab. The repeat command does
not claim physical pitch, calibration, or automatic part recognition.

## MVVM implementation mapping

### Existing owners to reuse

| Existing owner | Reuse |
| --- | --- |
| `ToolRecipeDocument` | Source, ordered steps, routes, parameters, selections |
| `ToolWorkbenchStepPropertySession` | PropertyGrid draft, Apply, Discard |
| `ArtifactRegistry` projection | Typed inputs/outputs and state |
| teaching capture ViewModel and Viewer coordinator | Candidate capture and Apply/Cancel |
| `ToolWorkbenchOutputCompareSession` | A/B/C candidate and pin state |
| `OpenVisionThreeDViewerControl` | Rendering, camera, picking, ROI handles, overlays |
| existing Tool adapters and Runner | Preview, Run, result records |
| AvalonDock host | Hide, float, and advanced pane hosting |

### New responsibility owners

`InspectionWorkspaceSelectionSession`

- one non-WPF selection identity;
- synchronizes step, input, ROI role, output, and Viewer slot;
- emits selection changes only;
- does not execute or persist.

`SelectedToolWorkspaceViewModel`

- adapts the selected step into Inputs, Parameters, Regions, Outputs, and Help;
- delegates to existing recipe, PropertyGrid, capture, artifact, and execution
  owners;
- owns no numerical algorithm or OpenGL behavior.

`ViewerWorkspaceSession`

- owns Viewer slots, focused-slot composition, and pins;
- delegates each slot's projection and display mode to its existing Viewer
  ViewModel rather than duplicating camera/render state;
- composes existing Viewer hosts and Output Compare state;
- never mutates recipe routes or runs inspection.

`ThicknessRepeatGridAuthoringService`

- produces and validates a candidate set of ordinary translated step/selection
  records;
- has no Viewer or WPF dependency;
- Apply returns a new recipe draft; Cancel returns the original unchanged.

### Root ViewModel rule

`ToolWorkbenchViewModel` remains the composition root. New default-workspace
bindings target the cohesive owners above rather than adding another set of
flat root properties. Behavior moves only with focused tests proving the new
owner and removal of the previous responsibility.

## Implementation slices

1. Selection session and Selected Tool facade, with no layout change.
   Complete on 2026-07-27; see
   `OPENVISIONLAB_3D_INSPECTION_WORKSPACE_SELECTION_BOUNDARY_20260727.md`.
2. New default workspace composition using current commands and collections.
   Complete on 2026-07-27; see
   `OPENVISIONLAB_3D_INSPECTION_WORKSPACE_DEFAULT_COMPOSITION_20260727.md`.
3. Top orthographic mode and Fit ROI.
   Complete on 2026-07-27; see
   `OPENVISIONLAB_3D_VIEWER_TOP_ORTHOGRAPHIC_AND_FIT_ROI_20260727.md`.
4. Compact dual-role ROI rows and exact Review lifecycle.
   Complete on 2026-07-27; see
   `OPENVISIONLAB_3D_ROI_REVIEW_LIFECYCLE_20260727.md`.
5. Inline Outputs with Show/Pin/Compare.
   Complete on 2026-07-27; see
   `OPENVISIONLAB_3D_SELECTED_OUTPUT_ACTIONS_20260727.md`.
6. Viewer split/pop-out composition.
   Complete on 2026-07-27; see
   `OPENVISIONLAB_3D_VIEWER_WORKSPACE_COMPOSITION_20260727.md`.
7. `ThicknessRepeatGridAuthoringService` and group presentation.
   Complete on 2026-07-27; see
   `OPENVISIONLAB_3D_THICKNESS_REPEAT_GRID_AUTHORING_20260727.md`.
8. Exact-source owner acceptance replay.

Each slice must preserve current build, structure, Viewer pointer, docking,
recipe teaching, height measurement, Validation Set, Recipe Manager, Runner,
logging, save/reopen, and keyboard shortcut checks.

Implementation status is now `7/8` bounded slices (`87.5%`). The remaining
gate is the owner's unaided replay of the exact 12-step path.

## Acceptance gate

The design is implemented only when:

- the exact 12-step operator path passes without hidden critical actions;
- no selected-step title or primary action is duplicated in the default path;
- Top and Perspective are distinct, named, one-click modes;
- a completed ROI immediately enters Review and restores empty-space orbit;
- output value, state, Show, Pin, and Compare are visible with the selected
  tool;
- `Repeat as grid` creates eight ordinary steps and 16 unique selections
  without running inspection;
- save/reopen and Runner reproduce all eight identities and results;
- current screenshot evidence passes at 1920 x 1080 and 1280 x 760;
- the owner completes the path without guidance.

### 2026-07-28 current-capture acceptance correction

A current Wide/Compact review found that the same ROI Apply/Cancel actions
were visible in three surfaces and that a repeated Viewer instruction covered
the model. The correction preserves the global Review ribbon as the only
primary action owner, removes duplicate Selected Tool and Height Image action
sets, removes repeated Viewer selected-step/technical context, hides
Thickness repeat during local ROI capture, and gives Height Image a reversible
`65%` editing share. Current evidence is recorded in
`OPENVISIONLAB_3D_WORKSPACE_V3_UX_MID_REVIEW_AND_ACCEPTANCE_CORRECTION_20260728.md`.
The implementation remains `7/8`; unaided owner replay is still the final
gate.

## Evidence

- `docs/OPENVISIONLAB_3D_GOPXL_VIDEO_WORKFLOW_GAP_AND_REDIRECTION_20260727.md`
- `artifacts/current/20260727-gopxl-gap-analysis/`
- `artifacts/current/20260727-inspection-workspace-v3/`

## Completion record

Status: Complete

Scope: Inspection Workspace v3 screen responsibility, synchronized selection,
Viewer/ROI/output behavior, exact eight-Tab click path, repeat-grid authoring,
and MVVM migration mapping.

Acceptance criteria: wide/compact layout -> specified; exact click path ->
specified; explicit execution invariants -> preserved; current-owner reuse ->
mapped; real new responsibility owners -> identified; implementation and
owner gates -> specified.

Verification: interactive step-through wireframe rendered at 1440 x 1200;
source recipe ROI coordinates checked; all referenced current owners and
commands checked against the current workspace; step 9 interaction verified
with `Thickness group (8)` visible; JavaScript syntax and element-reference
checks passed; Markdown whitespace checks passed.

Evidence: this document,
`artifacts/current/20260727-inspection-workspace-v3/wireframe-step-01-empty.png`,
and
`artifacts/current/20260727-inspection-workspace-v3/wireframe-step-09-repeat-grid.png`.

Boundary / next dependency: no production code is changed by this design.
Implementation starts with the selection/session boundary after owner review
of this interaction contract. Physical datum, unit, calibration, uncertainty,
and production tolerances remain external prerequisites for certified
thickness claims.
