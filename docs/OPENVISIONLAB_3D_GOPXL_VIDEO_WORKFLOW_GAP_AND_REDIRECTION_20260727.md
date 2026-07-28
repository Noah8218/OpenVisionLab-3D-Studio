# GoPxL Video Workflow Gap and OpenVisionLab 3D Redirection

Date: 2026-07-27
Status: Complete for the supplied-video review and current-source architecture comparison

## Executive decision

The owner's concern is valid: continuing to add workflow hints, cards, ribbons,
and dock tabs to the current Workbench will increase later UI rework.

This does **not** justify rewriting the product. The current C3D loader,
typed recipe and artifact contracts, numerical tool adapters, Runner, 3D
renderer, ROI geometry, persistence, and verification suites remain useful.
The highest rework risk is the Shell presentation and the way selected-tool,
ROI, output, and Viewer state are exposed to the operator.

The immediate direction is:

1. pause broad UI feature expansion and new algorithm-tool UI;
2. define one stable Inspection Workspace interaction contract;
3. reorganize the existing capabilities around one selected tool and one
   dominant Viewer;
4. prove the redesigned workflow with the exact eight-Tab Thickness recipe;
5. resume tool breadth only after an unaided owner replay passes.

This is a bounded Workbench redirection, not a full product rewrite and not a
request to copy GoPxL's visual identity.

## Evidence reviewed

Supplied local videos:

| Source | Media | Duration | Resolution | Supporting text |
| --- | --- | ---: | ---: | --- |
| `C:\Git\GoPxL_Video\GoPxL GUI - Walk Through.mp4` | VP9/Opus | 04:04.874 | 3840 x 2160 | English VTT |
| `C:\Git\GoPxL_Video\GoPxL Training Part 3 - Introduction to Three Measurement Tools.mp4` | VP9/Opus | 11:44.494 | 1920 x 1080 | English SRT |

Derived local review evidence:

- `artifacts/current/20260727-gopxl-gap-analysis/walkthrough-contact-sheet.jpg`
- `artifacts/current/20260727-gopxl-gap-analysis/walkthrough-key-scenes.jpg`
- `artifacts/current/20260727-gopxl-gap-analysis/training-contact-sheet.jpg`
- `artifacts/current/20260727-gopxl-gap-analysis/training-key-scenes.jpg`

Current OpenVisionLab evidence compared:

- current eight-Tab authoring capture:
  `artifacts/current/20260726-tab-thickness-model/after-tab-01-authoring.png`;
- `ToolRecipeWorkbenchView`, `ToolLibraryView`, `RecipePipelineReviewView`,
  `ToolInspectorView`, `DisplayedOutputsView`, and `OutputCompareView`;
- current `ToolWorkbenchViewModel` owners and sessions;
- current Viewer teaching, display, camera, and output-overlay owners;
- the current GoPxL-informed direction, ROI workflow, MVVM boundary, code
  rules, and eight-Tab completion documents.

The videos demonstrate one product version and one training scenario. They do
not prove every current GoPxL capability. Conclusions below are limited to
what is visible or stated in these supplied files.

## What the videos actually teach

### GUI walk-through

| Time | Observed behavior | Relevant lesson |
| ---: | --- | --- |
| 00:29-00:42 | A narrow category rail expands on hover and can stay open. | Global navigation stays separate from the inspection work surface. |
| 00:44-01:11 | Settings, jobs, maintenance, and support are distinct pages. | Recipe/job lifecycle is not mixed into tool parameters. |
| 01:31-02:28 | Design, discovery, alignment, and sensor motion are distinct system responsibilities. | Hardware scope is broad in GoPxL but is not required for the local OpenVisionLab target. |
| 02:30-02:59 | Inspect contains Scan and Tools; Tools combines measurement tools. | Inspection authoring has one obvious home. |
| 03:31-03:49 | Health, Measurements, and Performance expose runtime information. | Results and runtime state are visible surfaces, not only log text. |

### Measurement-tool training

| Time | Observed behavior | Relevant lesson |
| ---: | --- | --- |
| 00:36-01:18 | A replay file is loaded from the Viewer toolbar and replay state becomes visible. | Input/replay identity and current data state stay close to the Viewer. |
| 01:28-01:56 | Surface is the default; the operator changes to perspective, orbits/pans, then selects profile view. | Named view modes are first-class workflow tools. |
| 02:14-02:59 | The operator opens Tools, searches, then drag-drops or double-clicks a tool into the chain. | Catalog discovery and insertion are direct and spatially understandable. |
| 03:01-03:16 | Inputs and outputs connect automatically; the selected tool opens Inputs, Parameters, and Outputs together. | One selection owns the chain row, configuration, and Viewer evidence. |
| 03:16-04:08 | Bounding settings change the visible region while the operator remains on the tool. | Parameter meaning is visually connected to geometry. |
| 04:21-04:28 | Individual outputs such as width, length, height, and center point are enabled in the selected tool. | Output selection belongs with tool configuration. |
| 04:42-05:01 | Contextual Tool Help explains the selected tool and remains collapsible. | Help is local to the current task. |
| 05:05-05:36 | A tool owns multiple regions; ROIs are first positioned in surface/top view and refined in perspective. | ROI authoring deliberately uses both 2D-like and 3D views. |
| 06:20-06:40 | Region type changes to a circle and numeric geometry is available. | Region role, type, handle editing, and numeric editing form one contract. |
| 07:03-07:25 | Surface Dimension is inserted after Surface Mask and consumes its output. | Tool chaining is visible and useful without requiring a general node canvas. |
| 07:30-08:20 | Two feature ROIs are positioned; output dimensions and arrows appear in the Viewer. | Numeric output and graphical evidence are synchronized. |
| 08:20-08:33 | Measurements are pinned from the selected tool/output surface. | Important results can remain visible independently of selection. |
| 08:44-10:41 | Viewers split or pop out; each Viewer chooses its own view/mode and displayed outputs. | Comparison is a normal Viewer capability, not a hidden review-only task. |
| 11:04 | The configured measurement plan is saved as a job. | Save closes the same authoring flow; it is not a separate mental model. |

## GoPxL interaction contract inferred from the videos

The strongest commercial lesson is not color, density, or a left navigation
rail. It is a synchronized interaction contract:

```text
tool catalog
  -> ordered tool chain
  -> one selected tool
       -> inputs
       -> parameters and regions
       -> enabled outputs
  -> one or more data viewers
       -> view mode
       -> ROI and feature handles
       -> pinned numeric/graphical evidence
```

Selection is the center of the workflow. The operator does not need to infer
which of several panels owns the next action.

## Current OpenVisionLab strengths to preserve

The following are not reasons for a rewrite:

- validated C3D loading, sampled-grid identity, source SHA-256 checks, and
  current large-source Viewer performance;
- Surface/Points/Wireframe/Surface+Edges presentation and height coloring;
- Viewer orbit, pan, zoom, Fit, picking, and existing result overlays;
- typed `ToolRecipeDocument`, ordered steps, artifact-owned selections,
  schema `1.3` persistence, and last-recipe restore;
- strict tool adapters shared by Workbench and headless Runner;
- explicit Preview, Publish, Run, save/reopen, Validation Set, and durable run
  evidence;
- `ArtifactRegistry`, compatible-tool suggestions, Problems, Displayed
  Outputs, and A/B/C Output Compare session;
- selected GridRectangle move, resize, numeric editing, Reference and
  Measurement roles, review mode, Apply, Cancel, and Delete;
- existing View/ViewModel adapters for WPF dialogs, AvalonDock, PropertyGrid,
  OpenGL, and pointer interaction.

These capabilities should be recomposed, not reimplemented.

## Gap assessment

The table uses scoped states rather than one misleading completeness score.

| Workflow capability | GoPxL video | Current OpenVisionLab | Assessment |
| --- | --- | --- | --- |
| Search and add a tool | Search, drag/drop, double-click | Search, explicit `+`, double-click, compatible suggestions | Good foundation |
| Read the ordered chain | Tool diagram and selected row remain beside configuration | Navigator/Flow Map exist, but selected-step actions and explanations are distributed across panes | Partial |
| Configure one selected tool | Inputs, Parameters, Outputs in one panel | Compact I/P/O summary exists, but route details, ROI cards, WPG, actions, output display, and evidence are separated vertically and across docks | Weak interaction continuity |
| Connect inputs | Automatic default chaining with visible inputs | Compatible candidates and explicit IDs exist; generic insertion is less direct and ambiguity is not resolved in one input editor | Partial |
| Position ROI | Top/surface first, perspective refinement, visible handles and numeric geometry | Perspective Viewer with handles and numeric GridRectangle editor; no named orthographic Top preset and the operator must understand several capture/apply states | Partial and high priority |
| Multiple ROI roles/regions | Multiple regions owned and configured by the selected tool | Thickness owns Reference/Measurement selections, but repeated Tab instances are eight separate steps without a compact group surface | Partial |
| Finish ROI drawing | Manipulation continues as ordinary selected-region editing | Review mode now ends capture, but prior owner attempts prove the transition and next action are still not self-evident | Incomplete owner acceptance |
| Select outputs | Per-output enable controls in the tool panel | Output identity exists, but selection/display/pinning is mainly in separate Displayed Outputs/Compare surfaces | Major discoverability gap |
| See result evidence | Values and arrows can be pinned in the Viewer | Preview/Run evidence exists, but selected-tool values and overlay controls are not one obvious surface | Major workflow gap |
| Change view mode | Surface/top, perspective, profile, height map, intensity, mesh, points | Geometry/color choices, Profile, and Height Map exist; named Top/Perspective workflow and compact per-Viewer mode control are incomplete | Partial |
| Split/pop-out Viewer | Normal Viewer toolbar capability with independent outputs | Output Compare A/B/C and Tool Labs exist, but are separate docks/windows and are not discoverable as the main Viewer workflow | Partial |
| Contextual tool help | Collapsible help for selected tool | Hints, tooltips, and documentation exist; no consistent selected-tool help surface | Missing |
| Save/replay plan | Replay file and saved job | C3D load, recent/last recipe, save/reopen, Validation Set, Runner | Strong local foundation |
| Hardware/runtime platform | Sensor, alignment, industrial output, health | Intentionally absent | Out of scope, not a product defect |

## Why continued patching is risky

The current screen already contains:

- a five-stage journey strip;
- Tool Library guidance and compatible-tool cards;
- Inspection Flow and Recipe Navigator information;
- selected-step actions;
- a selected-tool I/P/O summary;
- ROI-specific cards and numeric editing;
- WPG parameters and Apply/Discard;
- a Viewer workflow ribbon;
- Pipeline/Validation, Problems, Displayed Outputs, Output Compare, logs, and
  tool-specific evidence docks.

Most of these capabilities are individually valid. The problem is that the
same selected step and next action are explained in several places.

The current `ToolWorkbenchViewModel*.cs` files total about 9,019 lines, with
the main composition file about 2,853 lines. File size alone is not an
architectural defect, and the current code already has useful independent
sessions. It is nevertheless a migration risk because the root ViewModel
still exposes selection, teaching, PropertyGrid, execution, displayed-output,
comparison, and validation state to many views. Adding more special-case UI
directly to this surface will make a later workspace change more expensive.

## Target: Inspection Workspace v3

### Wide layout

```text
+--------------------------------------------------------------------------+
| Recipe | Input | saved/dirty | selected step state | Preview | Run | Save |
+-------------+---------------+------------------+-------------------------+
| Tool        | Recipe chain  | Selected tool    | Data Viewer             |
| catalog     |               |                  |                         |
|             | Source        | Inputs           | Top / Perspective /     |
| search      |  +- Tool 1    | Parameters       | Profile                 |
| compatible  |  +- Tool 2    | Regions          |                         |
| all tools   |  +- Tool 3    | Outputs          | Surface / Height /      |
|             |               | Help             | Intensity / Points      |
|             |               |                  |                         |
|             |               |                  | ROI + result overlays   |
+-------------+---------------+------------------+-------------------------+
| Problems | Messages | Performance | Validation (collapsed unless needed) |
+--------------------------------------------------------------------------+
```

The Viewer remains dominant. At compact width, Tool Catalog and Recipe Chain
become two tabs in one left region; the selected-tool panel remains directly
beside the Viewer.

### Remove or demote from the default path

- remove the five-stage journey strip from the permanent work surface;
- remove duplicate next-action prose after the selected tool is ready;
- do not show the same step title and route summary in three separate cards;
- move Logs, Problems, Performance, Validation, and advanced Flow Map to a
  collapsed lower area that opens automatically only for a real issue or an
  explicit command;
- keep Recipe Manager separate for lifecycle operations, but make Save and
  current recipe state available in the global command bar.

### Selected-tool panel contract

Every selected tool uses the same stable order:

1. **Inputs**
   - typed contract, selected entity, state, frame/unit, and route selector;
   - a deterministic suggested input is preselected;
   - an ambiguous route requires an explicit operator choice;
   - adding or selecting never invokes Preview or Run.
2. **Parameters**
   - existing typed PropertyGrid or a tool-specific adapter;
   - invalid and unapplied values are visible without a modal detour.
3. **Regions**
   - visible role rows such as `Reference` and `Measurement`;
   - `Draw`, `Edit`, `Delete`, `Fit ROI`, status, and numeric geometry;
   - only the active region is yellow; other authored regions use stable role
     colors and labels.
4. **Outputs**
   - each output shows enabled/disabled, stale/current, value, unit, and
     tolerance state;
   - each displayable output has `Show`, `Pin`, and `Compare`;
   - geometric evidence can be enabled without fabricating a surface.
5. **Help**
   - a collapsed, selected-tool description with one short workflow example.

### Viewer contract

Add a compact first-class Viewer toolbar:

- view: `Top`, `Perspective`, and `Profile`;
- display: `Surface`, `Height`, `Intensity` when available, `Points`, and
  `Mesh/Edges`;
- camera: `Fit all`, `Fit ROI`, and reset;
- layout: single, split vertical, split horizontal, and pop out;
- evidence: visible overlay/output pins for that Viewer.

`Top` must be a true named camera/projection preset for ROI positioning, not
merely a near-top perspective camera. Perspective remains available for
height and occlusion review.

Viewer display changes remain display-only and never change recipe inputs or
invoke inspection.

### ROI lifecycle

```text
Select role
  -> Draw
  -> mouse-up or second corner creates a ready candidate
  -> automatic Review mode and ordinary empty-space orbit
  -> handle or numeric correction
  -> Apply selection or Cancel
  -> explicit Preview for measurement results
```

The candidate must never accept an accidental third point. `Apply selection`
changes recipe geometry only. It does not Preview, Publish, or Run.

### Explicit execution remains

GoPxL's immediate replay feedback must not be copied by violating the current
OpenVisionLab lifecycle:

- geometry handles and view-only overlays update immediately;
- parameter/ROI changes mark the selected tool `Dirty / Preview stale`;
- only explicit Preview calculates selected-tool results;
- only explicit Publish commits a publishable derived artifact;
- only explicit Run executes the complete recipe;
- save may preserve an incomplete draft, while Preview/Run fail closed.

This keeps deterministic evidence and prevents hidden execution.

### Repeated eight-Tab workflow

Do not replace eight independent measurement records with one opaque
multi-result algorithm.

Add a presentation-level `Thickness group (8)`:

- keep eight ordinary typed Thickness steps and eight output identities;
- show Tab 1 through Tab 8 as compact child rows;
- allow shared parameter templates with explicit per-Tab override;
- offer `Duplicate instance` and a bounded `Create N instances` command;
- keep Reference/Measurement ROI status and current result on each Tab row;
- selecting a Tab synchronizes the chain, selected-tool panel, and Viewer.

This improves repeated teaching without rewriting Runner or recipe semantics.

## Architecture direction

### Preserve

- `OpenVisionLab.ThreeD.Core`, `.Data`, `.Tools`, and `.Runner`;
- `ToolRecipeDocument`, typed adapters, `ArtifactRegistry`, and current
  verification fixtures;
- `OpenVisionThreeDViewerControl` rendering/pointer adapters;
- `ToolWorkbenchStepPropertySession` and
  `ToolWorkbenchOutputCompareSession`;
- AvalonDock as a host capability, not as the operator's workflow model.

### Introduce only real responsibility owners

1. `SelectedToolWorkspaceViewModel`
   - owns the presentation of one selected step's Inputs, Parameters,
     Regions, Outputs, Help, dirty/stale state, and commands;
   - delegates numerical execution, persistence, PropertyGrid drafts, and
     teaching capture to existing owners.
2. `InspectionWorkspaceSelectionSession`
   - owns one synchronized selection identity across recipe chain, selected
     tool, active ROI role, Viewer focus, and current output;
   - contains no WPF or OpenGL code.
3. `ViewerWorkspaceSession`
   - owns single/split/pop-out presentation, per-Viewer display mode, and
     output pins;
   - reuses current Viewer hosts and Output Compare session.

`ToolWorkbenchViewModel` remains the composition/session root while behavior
moves only when an independent owner has a focused test seam. Do not create
new partial files as a substitute for these owners.

### Do not build yet

- a decorative free-form node graph;
- automatic execution on selection, route, ROI, visibility, or output-pin
  changes;
- a generic XYZ volume ROI by reinterpreting `GridRectangle`;
- camera, PLC, robot, HMI, cloud, account, or plant management;
- new algorithms before the redesigned Thickness workflow is accepted.

## Implementation sequence and gates

1. **Freeze and interaction specification**
   - approve the layout, selected-tool contract, ROI lifecycle, and output
     behavior before WPF restructuring;
   - produce a current-source wireframe and an exact eight-Tab click-path.
   - Recommended model: `gpt-5.6-terra`
   - Reasoning effort: `medium`
2. **Selection and selected-tool boundary**
   - add the two presentation/session owners above;
   - prove chain/config/Viewer selection synchronization without execution.
   - Complete on 2026-07-27; focused verification passes `12/12`.
   - Recommended model: `gpt-5.6-sol`
   - Reasoning effort: `high`
3. **Workspace shell recomposition**
   - replace the journey-heavy default with Catalog, Chain, Selected Tool,
     dominant Viewer, and collapsed issue area;
   - reuse existing views and commands where their responsibility still fits.
   - Recommended model: `gpt-5.6-sol`
   - Reasoning effort: `high`
4. **Thickness ROI first slice**
   - implement Top/Perspective/fit-ROI workflow and the compact
     Reference/Measurement rows;
   - prove candidate -> Review -> Apply/Cancel with the exact supplied C3D.
   - Recommended model: `gpt-5.6-sol`
   - Reasoning effort: `high`
5. **Selected outputs and Viewer layouts**
   - surface value/state/Show/Pin/Compare in the selected tool;
   - expose split/pop-out as normal Viewer commands.
   - Recommended model: `gpt-5.6-terra`
   - Reasoning effort: `high`
6. **Eight-Tab group**
   - group the existing eight steps, add bounded duplication/templates, and
     retain eight independent outputs and Runner records.
   - Recommended model: `gpt-5.6-terra`
   - Reasoning effort: `medium`
7. **Owner acceptance gate**
   - owner completes open -> add -> route -> two ROIs -> limits -> Preview ->
     visible result -> eight Tabs -> Run -> Save -> reopen without guidance;
   - only after this passes may broad tool development resume.
   - Prerequisite: owner access to the rebuilt Windows application and a
     decision on physical datum, units, and production tolerances for
     certified measurement claims.

## Acceptance criteria for the redesigned first slice

- selected step, selected ROI role, selected output, and Viewer highlight
  always refer to the same identity;
- the selected-tool title appears once in the main authoring path;
- Reference and Measurement ROI actions are visible without opening an
  advanced section;
- Top and Perspective are named one-click view modes;
- finishing the second corner enters Review mode and restores empty-space
  orbit;
- output values, stale/current state, and overlay controls are visible in the
  selected-tool panel;
- Preview and Run remain explicit and are never triggered by selection,
  routing, ROI edits, visibility, or Viewer changes;
- the exact eight-Tab recipe saves/reopens with names, 16 ROIs, routes,
  parameters, and eight output identities unchanged;
- current Release build, structure guard, recipe teaching, ROI pointer,
  height measurement, Validation Set, Runner, and current-window screenshot
  checks pass;
- an unaided owner replay passes before the workflow is called complete.

## Risk conclusion

| Area | Rewrite risk if direction changes now | Decision |
| --- | ---: | --- |
| Core/Data numerical and identity contracts | Low | Preserve |
| Tools adapters and Runner | Low | Preserve and extend only after UX gate |
| Viewer rendering/camera/picking base | Low to medium | Preserve; add named view/session controls |
| ROI interaction adapter | Medium | Refine around the new workspace contract |
| Shell default layout and navigation hierarchy | High | Recompose before adding more workflow UI |
| Root Workbench selection/presentation surface | High | Extract real session owners incrementally |
| Physical calibration/metrology | Blocked by evidence | Do not infer or redesign around guesses |

The costly mistake would be either extreme: continuing to patch the current
default path indefinitely, or discarding the validated engine and starting
again. The recommended path is to preserve the deterministic inspection core
and deliberately replace the operator-facing composition now.

The detailed interaction, exact eight-Tab click path, repeat-grid authoring
contract, and MVVM implementation mapping are fixed in
`OPENVISIONLAB_3D_INSPECTION_WORKSPACE_V3_INTERACTION_SPEC_20260727.md`.

## Completion record

Status: Complete

Scope: supplied GoPxL video review, current-source workflow/architecture gap
assessment, and a bounded Workbench redirection plan.

Acceptance criteria: both videos and subtitles reviewed -> pass; key scenes
extracted -> pass; current Workbench/Viewer/recipe owners compared -> pass;
preserve/recompose/reject boundaries documented -> pass; phased acceptance
gates documented -> pass.

Verification: `ffprobe` metadata for both videos; full-duration contact
sheets; 24 key scene captures; subtitle timeline review; current XAML,
ViewModel ownership, Viewer capabilities, and product-direction documents
checked.

Evidence: this document and
`artifacts/current/20260727-gopxl-gap-analysis/`.

Boundary / next dependency: no production code or layout was changed. The
owner must approve the Inspection Workspace v3 interaction contract before
implementation; physical datum, calibration, units, and tolerances remain
external prerequisites for certified thickness claims.
