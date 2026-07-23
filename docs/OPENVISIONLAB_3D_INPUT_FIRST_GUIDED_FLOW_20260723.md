# OpenVisionLab 3D Input-First Guided Flow

Date: 2026-07-23

## Status

Status: Complete

Scope: make the first inspection workflow start without an implicit sample and guide the operator through recipe -> input -> tools -> teaching/Preview -> validation/Run.

## Product decision

OpenVisionLab 3D Studio is an explainable local rule-based 3D inspection recipe workbench. It follows the useful GoPxL Tools/Chaining pattern of typed tools, visible INPUT -> OUTPUT flow, contextual configuration, and explicit execution. It does not adopt camera, PLC, robot, cloud, account, or production-controller scope.

The first inspection contract is:

1. Create or open a recipe.
2. Explicitly select the 3D input used by that recipe.
3. Add only tools compatible with the current typed input.
4. Select a recipe step and teach its input, ROI, and parameters.
5. Run Preview explicitly, review evidence, then Validate/Run and Publish explicitly.

Saving a named source-less zero-step recipe remains valid. It is an editable draft, not an execution-ready inspection.

## Responsibility change

Before this change, `OpenVisionThreeDViewerControl` synchronously loaded the fixed Thickness C3D in its constructor and `MainWindow` copied that source into a new Workbench. The first screen therefore looked ready even though the operator had never selected the inspection input.

After this change:

- Normal Shell startup constructs the Viewer without default samples.
- Existing automated compatibility runs retain the default-sample constructor unless `--smoke-input-first-start` explicitly selects the empty path.
- `CreateNewTeachingRecipe` clears source identity, references, selections, steps, selected step, and recipe path.
- Recipe Center New immediately saves a named source-less zero-step recipe, clears the Viewer, and activates the Workbench.
- Only explicit `Open 3D Map` or opening a recipe with a valid source moves the Workbench to source-ready state.
- `AddSelectedToolCommand` requires the full `IsSourceReadyForRecipe` contract, not merely a non-empty path.

## UI behavior

- `Tool Library` is named `Inspection Tools` / `검사 도구`.
- `Recipe Flow` is named `Inspection Flow` / `검사 구성`.
- Before input, compatible suggestions, search, the full catalog, Add, and palette details are hidden.
- The left panel and central Viewer present the same localized `Select 3D input data` / `3D 입력 데이터를 선택하세요` action.
- Once input is ready, compatible suggestions and the catalog appear and stage 3 becomes the active journey step.
- Once a step is selected, the contextual next action directs the operator to Step Parameters and explicit Preview.
- The empty Viewer HUD reports `Transform: not available`, `Alignment: no source`, and `Mapping: no source`; it no longer implies an aligned source.
- Preview, Run, and Publish remain explicit commands.

## Acceptance evidence

- Full Debug solution build: 0 warnings, 0 errors.
- Tool Recipe teaching verification: 25/25.
- Recipe Center/WPG verification: 27/27.
- Docking workspace verification: 26/26.
- Actual EXE New through the real Don't Save action: Pass.
  - persisted schema: 1.3
  - source path: empty
  - step count: 0
  - source ready: false
  - Viewer source: empty
  - saved and clean: true
- Actual EXE explicit input transition: Pass.
  - previous source: empty
  - selected source: fixed Thickness C3D
  - current Viewer source equals selected source
  - load state cleared at 100%
  - compatible inspection tools visible only after the transition
- Korean and English 1920 x 1040 screenshots pass quality on attempt 1.

Evidence folder: `artifacts/current/20260723-input-first-guided-flow/`

Key files:

- `before-normal-start-1920x1040-ko.png`
- `after-new-empty-ko.png`
- `after-input-first-en.png`
- `after-explicit-input-ko.png`
- `actual-new-empty.ov3d-recipe.json`
- `actual-new-empty-report.txt`
- `actual-explicit-input-load-report.txt`
- `tool-recipe-teaching.txt`
- `recipe-manager-wpg.txt`
- `workbench-docking.txt`

## Claim boundary and next gate

This closes the implementation defect where a new operator encountered an unexplained preloaded sample and premature tool catalog. It does not prove that an unaided owner can complete the full first recipe, nor does it provide multi-sample repeat validation, physical calibration, or metrology evidence.

The next internal UX priority is a bounded multi-sample validation workspace: select an input set, repeat the taught recipe, compare per-sample status/metrics/overlays, and open a failed sample without weakening explicit Run. Viewer UI-apply/first-render performance remains the following bounded performance priority.

## Completion record

Status: Complete

Scope: source-less normal/new recipe, explicit input transition, gated tool discovery, bilingual guided first-inspection UI.

Acceptance criteria: empty startup/new recipe -> pass; explicit input owns ready transition -> pass; tools hidden before and visible after input -> pass; explicit Preview/Run/Publish preserved -> pass; bilingual current-build captures -> pass.

Verification: full Debug build 0/0; teaching 25/25; Recipe Center/WPG 27/27; docking 26/26; actual EXE New and explicit C3D load passes; screenshot-quality passes.

Evidence: `artifacts/current/20260723-input-first-guided-flow/`.

Boundary / next dependency: unaided owner replay remains external; trusted physical-frame and metrology evidence remains unavailable.
