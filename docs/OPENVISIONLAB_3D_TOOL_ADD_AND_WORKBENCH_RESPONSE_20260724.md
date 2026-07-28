# Tool add discoverability and Workbench response - 2026-07-24

## Owner finding

The owner could load a C3D and reach the inspection tool catalog, but the
authoring interaction still had three product-level problems:

- the only `Add selected step` action was below a long catalog and therefore
  disappeared from the normal viewport;
- a selected GridRectangle could be drawn, but its selected/edit state and
  coordinate meaning were not sufficiently clear;
- tool selection, step addition, and parameter focus felt globally slow.

The tool-add and bounded Workbench-response problems are closed by this
checkpoint. ROI selection/editing is deliberately recorded as the next typed
UI slice rather than being hidden inside this change.

## Product decision

The authoring sequence remains:

`Input -> choose tool -> add step -> teach parameters/ROI -> Preview -> Publish`

Tool selection and tool addition are distinct. Merely selecting a catalog row
does not mutate the recipe or execute inspection. Adding a row creates one
typed recipe step, selects it in Inspection Flow, and exposes its Step
Parameters. Preview, Publish, and Run remain explicit.

The catalog now follows the same local-action convention as the compatible
tool suggestions:

- every full-catalog row has a visible inline `+` action;
- double-clicking a full-catalog row performs the same add command;
- the old global add button below the scrollable catalog is removed;
- the inline icon has a bilingual tooltip and accessible name;
- the long catalog uses recycling virtualization.

This avoids a second free-form graph editor and preserves the typed
INPUT/OUTPUT recipe contract.

## Response-time correction

The Workbench update path was reduced without changing recipe semantics:

- pure tool selection no longer rebuilds the compatible catalog or writes
  session/external logs;
- selected-step focus refreshes only the selected tool family's execution
  state instead of every registered adapter;
- recipe step reordering suppresses recursive whole-recipe refresh;
- compatible tools, artifact registry, navigator, displayed outputs, compare
  candidates, and flow diagnostics publish one collection Reset rather than
  many per-row notifications;
- the WPG host coalesces repeated loaded-view refreshes at Dispatcher
  background priority and flushes pending state before commit.

Authored recipe collections, typed IDs, source identity, parameters, explicit
Preview/Publish/Run, and output-layer behavior are unchanged.

## ROI direction fixed by this decision

The existing `GridRectangle` is a height-field surface footprint:
row, column, row count, and column count. It is not a freely rotatable XYZ
volume. Overloading it with six degrees of freedom would make recipe meaning
and Runner replay ambiguous.

The next Surface ROI UX slice must add:

1. an unmistakable selected state in the Viewer;
2. corner/edge handles for move and resize;
3. a bilingual ROI label and compact numeric row/column/width/height editor;
4. a source-frame X/Z footprint summary and optional height min/max filtering;
5. synchronized Viewer and Step Parameters selection;
6. explicit Apply, with no automatic Preview or execution.

A true point-cloud or mesh volume selection must be a separate typed
`OrientedBox3D` contract with center XYZ, size XYZ, and rotation XYZ. It must
not silently replace or reinterpret existing `GridRectangle` recipes.

The current Thickness adapter still reports scalar height statistics inside
one GridRectangle. It is not a calibrated two-surface physical-thickness
algorithm.

## Verification

Current Release verification:

- solution build: pass, `0` warnings / `0` errors;
- Tool Recipe teaching: pass, `27/27`;
- docking workspace: pass, `28/28`;
- Recipe Center/WPG: pass, `28/28`;
- generic height measurement Workbench: pass, `28/28`;
- typed artifact/navigator: pass, `24/24` plus the explicit compatible-add
  command path;
- Korean actual-EXE screenshot: accepted on attempt 1, with no clipped catalog
  add actions.

Three independent local Release EXE runs used the same source and one
Thickness add:

| Interaction | Target | Run 1 | Run 2 | Run 3 | Median | Result |
|---|---:|---:|---:|---:|---:|---|
| Tool selection | `<= 50 ms` | `2.803` | `5.404` | `3.533` | `3.533` | Pass |
| Tool add | `<= 150 ms` | `90.421` | `94.665` | `79.598` | `90.421` | Pass |
| Step selection/focus | `<= 150 ms` | `27.361` | `33.225` | `31.142` | `31.142` | Pass |
| UI apply/layout | `<= 150 ms` | `134.805` | `110.427` | `122.333` | `122.333` | Pass |

The recipe-refresh median was `56.870 ms`. All runs ended with one authored
Thickness step, `Taught incomplete`, and `publishAvailable=False`, proving
that adding did not invoke Preview or Publish.

Evidence:

- `artifacts/current/20260724-tool-add-workbench-response/before-tool-add.png`
- `artifacts/current/20260724-tool-add-workbench-response/after-inline-add.png`
- `artifacts/current/20260724-tool-add-workbench-response/batched-interaction-1.txt`
- `artifacts/current/20260724-tool-add-workbench-response/batched-interaction-2.txt`
- `artifacts/current/20260724-tool-add-workbench-response/batched-interaction-3.txt`
- `artifacts/current/20260724-tool-add-workbench-response/tool-recipe-teaching-final.txt`
- `artifacts/current/20260724-tool-add-workbench-response/workbench-docking-final.txt`
- `artifacts/current/20260724-tool-add-workbench-response/recipe-manager-wpg-final.txt`
- `artifacts/current/20260724-tool-add-workbench-response/height-measurement-final.txt`
- `artifacts/current/20260724-tool-add-workbench-response/artifact-navigator-final.txt`
- `artifacts/current/20260724-tool-add-workbench-response/after-inline-add-quality.txt`

## Boundary

The timing is a fixed local Release EXE smoke at the current workstation and
is not a broad hardware, every-view, or arbitrary-data benchmark. The inline
button command path is covered by the typed verifiers and actual EXE capture.
The row double-click handler is implemented, but native-pointer double-click
addition was not separately automated in this checkpoint.

This work does not prove Surface ROI handle editing, an XYZ oriented volume,
calibrated physical thickness, physical calibration, or metrology.

## Completion record

Status: Complete

Scope: inline and double-click catalog add affordances, removal of the
off-screen global add action, virtualized catalog presentation, bounded
Workbench/WPG response correction, performance instrumentation, and durable
ROI contract direction.

Acceptance criteria:

- every visible full-catalog tool can be added without scrolling to a footer:
  pass;
- selection alone does not mutate or execute the recipe: pass;
- add creates one selected typed step without Preview/Publish: pass;
- tool selection, add, step focus, and UI apply meet their fixed local Release
  budgets in three runs: pass;
- current UI capture has no clipped add control: pass;
- GridRectangle and future XYZ volume semantics are documented separately:
  pass.

Verification: Release build `0/0`; focused checks `27/27`, `28/28`, `28/28`,
`28/28`, and artifact/navigator `24/24` plus explicit add; three current
actual-EXE timing runs; current screenshot quality accepted on attempt 1.

Evidence: this document and
`artifacts/current/20260724-tool-add-workbench-response/`.

Boundary / next dependency: implement and verify Surface ROI selected/edit
state first. Introduce `OrientedBox3D` only as a separate typed contract when a
tool genuinely requires an XYZ volume.
