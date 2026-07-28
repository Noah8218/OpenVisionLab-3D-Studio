# First Recipe UX and Recipe Center

Date: 2026-07-22
Status: Implemented and current-build verified; owner first-use replay remains external evidence

## Decision

The first operator task is now the active UI gate. A user must be able to
create or open an inspection recipe, identify its 3D input, add the first
tool, teach and preview the selected step, and save the recipe without a
developer explaining the screen.

The accepted product identity does not change: OpenVisionLab 3D Studio is a
local, sensor-neutral, rule-based 3D inspection recipe workbench. This UI
slice does not add an algorithm, change a recipe or Run Record schema, or
expand into sensor, PLC, robot, cloud, account, or production-line scope.

The 2026-07-21 `85/100` owner UI score remains historical evidence for the
visual system, docking, and the screens reviewed at that time. It is not a
passing first-task-usability claim. The owner's real first-use attempt on
2026-07-22 could not identify how to start a recipe, so the deferred
first-time-operator gate is reopened until the acceptance protocol below
passes.

## Commercial pattern used

The workbench adopts the useful responsibility split demonstrated by GoPxL:

```text
Tool Library -> ordered Tool Flow -> Inputs / Parameters / Outputs -> 3D Viewer
```

The separate recipe window adopts the useful start-center responsibility
demonstrated by MVTec MERLIC:

```text
New -> Open -> Current work -> Recent work and details
```

These are workflow references, not a visual copy. OpenVisionLab keeps its own
navy/light/teal theme, WPF-UI icon language, explicit Preview/Publish/Run
contract, typed entity IDs, PropertyGrid editing, and dockable views.

## UI ownership

### Recipe Center

The existing single-instance, separate Recipe Manager window remains
separate from the main Workbench and becomes `Recipe Center` / `레시피 센터`.
It owns only recipe lifecycle entry and session summary:

- create a new inspection recipe;
- open an existing recipe;
- show the current recipe name, path, source, step count, validation, and
  modified state;
- save or save as;
- show recent recipes with enough path information to distinguish equal file
  names;
- remove an item from the recent list without implying file deletion.

It does not own tool teaching, Preview, Publish, Run, or algorithm execution.

### Main Workbench

The default 1920 x 1080 authoring layout separates four responsibilities:

1. `Tool Library`: compatible suggestions plus the searchable complete tool
   catalog and explicit Add.
2. `Recipe Flow`: source identity, ordered typed entity/step navigation,
   selected INPUT -> OUTPUT route, and advanced source/reference metadata.
3. `3D View`: source, ROI, overlays, display controls, and explicit
   Preview/Run/Publish commands.
4. `Step Setup`: selected step Inputs -> Parameters -> Outputs and teaching
   selections.

The bottom validation/evidence pane is hidden while the recipe has zero
steps. It becomes available when a step exists or when an explicit command
opens evidence. Empty space must not compete with the first actionable task.

## First recipe journey

The top of the Workbench shows a non-blocking progress guide:

```text
1 Recipe -> 2 Input -> 3 Add tools -> 4 Teach & Preview -> 5 Validate & Run
```

This guide does not enforce a wizard and does not execute anything. Its
current state is derived from the existing recipe session. The contextual
next action is exactly one of:

1. select or load 3D input;
2. add the first compatible tool;
3. select an existing step;
4. teach Inputs/Parameters/Outputs and Preview explicitly;
5. validate, Run, and save explicitly.

Filter must be presented as an optional preparation tool, not as a mandatory
product mode. Operators may add a compatible measurement tool directly when
the current typed input contract allows it.

## Non-negotiable behavior

- Parameter edits, ROI/point teaching, visibility changes, and tool selection
  never run inspection automatically.
- Preview, Publish, and whole-recipe Run remain explicit commands.
- Output creation does not switch the input layer automatically.
- Viewer pan/zoom/right-drag, ROI overlays, profile, comparison, docking, and
  custom title-bar behavior remain available.
- The first implementation is an ordered typed flow. A decorative arbitrary
  node editor and writable free-form connections remain out of scope until
  general execution semantics are proven.
- New UI labels are supplied in Korean and English. Typed IDs, file names,
  schema identifiers, and established algorithm names may remain technical
  values.

## Acceptance protocol

The UI slice is complete only when all of the following pass from a current
Debug build:

1. Fresh before and after captures exist for the Korean 1920 x 1080
   Workbench and the separate Recipe Center.
2. At zero steps, the Workbench displays one clear next action and does not
   consume the bottom third of the screen with an empty pipeline.
3. Tool Library, Recipe Flow, 3D View, and Step Setup are distinct dockable
   panes at 1920 x 1080.
4. Recipe Center visually separates New/Open, Current Recipe, and Recent
   Recipes and displays paths for same-name disambiguation.
5. Korean/English switching updates every new user-facing label without
   changing recipe state.
6. At zero steps, Save and Save As remain available so the operator can create
   the recipe first. Preview and Run remain unavailable until a valid input and
   inspection step exist. No placeholder inspection step is created.
7. Stock WPF message boxes are replaced by the shared bilingual company
   dialog and keep technical details secondary to the operator instruction.
8. Existing docking, recipe teaching, PropertyGrid, Preview/Publish, and
   save/reopen verification remains green.
9. Build completes with zero warnings and zero errors.

## Proof boundary

Passing this protocol proves a clearer first-recipe UI and preserved software
contracts. It does not prove that an unaided external operator completed the
workflow, physical calibration, Gauge R&R, metrology trust, real four-corner
alignment, broad arbitrary graph execution, or production integration.

## Completion record

```text
Status: Complete
Scope: Separate Recipe Center and the first-recipe Workbench hierarchy only; no algorithm, recipe schema, or Run Record behavior changed.
Acceptance criteria: Current Debug build 0 warnings/errors; docking 25/25; Recipe Center/WPG/localized computed state and shared-dialog checks 24/24; recipe teaching 18/18; Korean and English Workbench/Recipe Center/dialog captures accepted on attempt 1.
Verification: dotnet build OpenVisionLab.ThreeDStudio.sln -c Debug -p:Platform="Any CPU"; --verify-workbench-docking; --verify-recipe-manager-wpg; --verify-tool-recipe-teaching; current-build screenshot smokes at 1920 x 1080.
Evidence: artifacts/current/20260722-first-recipe-ux/ and this document.
Boundary / next dependency: The owner must replay New/Open -> add tool -> teach/Preview -> save without guidance before an unaided-operator usability claim. Real alignment/metrology still requires a trusted four-landmark acquisition.
```

The final Workbench separates `Tool Library`, `Recipe Flow`, `3D View`, and
`Step Parameters` as four dockable roles. With zero steps it presents one
contextual next action and leaves the bottom pipeline closed. The Recipe
Center presents full-width New/Open actions, current recipe/source/save state,
and recent files with paths. All newly introduced labels and computed status
summaries switch between Korean and English without mutating the recipe.

Current-build evidence:

- before: `workbench-before-1920x1080-ko.png`, `recipe-center-before.png`;
- after Korean: `workbench-after-1920x1080-ko.png`, `recipe-center-after-ko.png`;
- after English: `workbench-after-1920x1080-en.png`, `recipe-center-after-en.png`;
- reports: `workbench-docking-verification.txt`,
  `recipe-center-wpg-verification.txt`,
  `tool-recipe-teaching-verification.txt`, and the four screenshot-quality
  reports.

All files are under
`artifacts/current/20260722-first-recipe-ux/`.

The follow-up invalid-save and bilingual message-dialog evidence is recorded
in `docs/OPENVISIONLAB_3D_MESSAGE_DIALOG_AND_RECIPE_SAVE_UX_20260722.md` and
`artifacts/current/20260722-message-dialog-localization/`.

The original zero-step Save restriction was corrected after the owner
clarified that recipe creation must precede inspection-step authoring. The
current authoritative contract and EXE evidence are in
`docs/OPENVISIONLAB_3D_EMPTY_RECIPE_LIFECYCLE_20260722.md`.
