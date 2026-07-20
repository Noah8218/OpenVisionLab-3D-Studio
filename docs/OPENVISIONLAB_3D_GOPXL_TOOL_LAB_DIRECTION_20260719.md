# OpenVisionLab 3D GoPxL-Informed Tool Lab Direction

Updated: 2026-07-20
Status: Approved product direction; G1 Flow Map, G2 Output Compare, G3 selected-tool I/P/O summary, and G4 Displayed Outputs / Overlay Manager complete and verified locally; global UI/UX acceptance remains open

## Why this document exists

This is the durable UI/UX direction for future 3D Studio work. Read it before changing Recipe Manager, Tool Views, the recipe graph, or 3D result presentation.

The owner approved a GoPxL-informed workflow because its separation of Tool Diagram, `Inputs`, `Parameters`, `Outputs`, and split/pop-out data viewers matches a rule-based 3D inspection workbench. It is a functional reference only. OpenVisionLab must use its own layout, terminology, visual design, and implementation.

Reference material checked on 2026-07-19:

- [LMI GoPxL Tools](https://am.lmi3d.com/manuals/gopxl/gopxl-1.2/LMILaserLineProfiler/Content/Inspect_toolRelated/Inspect_Tools.htm)
- [LMI GoPxL Tool Chaining](https://em.lmi3d.com/manuals/gopxl/gopxl-1.4/en-US/LMILaserLineProfiler/Content/Inspect_toolRelated/Tool_chaining.htm)
- [MVTec MERLIC Tool Flow](https://www.mvtec.com/doc/merlic/5.6/manual/en-us/Content/Creator/User_interface_creator/tool_flow.html)
- [Zebra Aurora Vision Studio](https://www.zebra.com/us/en/products/oem/software/aurora-vision-studio.html)
- [NI Vision Builder table/diagram view](https://knowledge.ni.com/KnowledgeArticleDetails?id=kA03q0000019l00CAA&l=en-US)
- [PolyWorks Inspector](https://www.innovmetric.com/products/polyworks-inspector)

## Product decision

OpenVisionLab 3D Studio uses four synchronized surfaces, not a canvas-only editor:

1. **Recipe Manager window** — a single modeless window for recipe lifecycle only: New, Open, Save, Save As, recent recipes, source identity, readiness, and compatibility status. It must not be a permanent pane in the main Tool Workbench, a second pipeline editor, or an algorithm-execution surface.
2. **Recipe Navigator tree** — the default human-readable representation of source/reference entities, ordered tool groups, typed outputs, state, and validation. This is the primary view for understanding a recipe.
3. **Tool Lab / Tool View** — a dedicated executable editor for exactly one algorithm. It owns compatible input selection, typed WPG parameters, explicit Preview, Preview discard, Publish, output provenance, and 3D result inspection. Preview never changes the recipe; applying a step changes the recipe only through an explicit command.
4. **Flow Map and Compare Workspace** — an advanced read-first `INPUT -> OUTPUT` graph plus a 2- or 3-pane 3D comparison workspace. The Flow Map follows the tree selection. Free drag-to-connect graph editing is deferred until typed-port validation, undo, grouping, routing, and generic execution contracts exist.

The default navigation is therefore **tree first, Tool Lab second, graph when needed**. A graph-only default creates unreadable tool chains; a tree-only default hides dependencies and branches.

## Required entity vocabulary

Every Tool input and output must be typed and visibly labelled. The initial vocabulary is:

```text
SourceC3D / RawHeightField
  -> FilteredHeightField
  -> EdgePointSet
  -> FittedLine3D
  -> IntersectionPoint3D
  -> CorrespondenceSet
  -> AffineTransform3D
  -> TransformedPointCloud or TransformedSurface
  -> RegriddedHeightField
  -> MeasurementResult
```

Each Preview or Published artifact must retain its root source identity, input artifact identity, frame, unit, tool/parameter identity, content SHA-256, state (`Preview`, `Published`, `Stale`, `Error`), and display label. Only compatible typed outputs may be offered as a subsequent Tool input. Suggestions may be shown, but routes must never silently change.

Geometric outputs such as points, lines, planes, and intersections are displayed as overlays on their input 3D data and as typed rows in the entity tree; they are not falsely presented as full 3D surfaces.

## Tool Lab interaction contract

Each Tool Lab contains:

- an input entity selector and visible input provenance;
- a WPG-backed typed parameter editor;
- explicit `Preview`, `Discard Preview`, `Publish Output`, and `Apply Step to Recipe` actions;
- a separate input and output 3D viewer for surface/point-cloud transformations;
- an overlay/result-table view for feature outputs;
- result state, hash, frame, unit, and compatibility feedback.

The first Tool Lab is **Filter** with a Source C3D viewer beside a Filter Preview C3D viewer. The next Tool Labs are Height Difference Edge, Line Fit, Intersection, XYZ Affine, and Re-grid in that order. A later pinned Compare Workspace may float/dock independently, but the application must not create unbounded Viewer windows automatically.

Difference display is allowed only where source identity, coordinate-frame contract, and grid/point correspondence make it meaningful. It is not a substitute for calibration evidence.

## XYZ affine boundary

The owner chose whole XYZ point-cloud affine, not merely height-map XY affine. A generic `X' = A X + t` transform cannot be solved from only upper-left and lower-right corner correspondences. It requires at least four affine-independent 3D source/reference correspondences, non-coplanar for a fully determined general 3D transform, plus rank, determinant, residual, and source-provenance checks.

If a workflow instead needs planar XY registration plus a height correction, it must be defined as a distinct constrained transform, not called full XYZ affine.

## P1 implementation scope

P1 changes the existing Tool Workbench without adding an unimplemented generic executor:

- remove Recipe Manager from the main Workbench left pane;
- open one reusable separate Recipe Manager window from the command bar;
- retain the existing shared `ToolWorkbenchViewModel` as the Recipe Session owner;
- add the Filter Tool Lab with separate Source and Filter Preview 3D viewers;
- reuse the existing explicit Filter Preview/Publish adapter; do not add automatic execution;
- provide current-build screenshot and behavior evidence.

P1 does not claim linked cameras, generic graph editing, Line Fit, intersections, affine solving, re-grid, physical units, calibration, or metrology.

## Responsibility boundaries

```text
Core/Data/Tools: immutable contracts, source/artifact identities, loaders, numerical tool execution
Shell ViewModels: recipe/session state, typed tool drafts, Preview/Publish state, routes
Recipe Manager Window: recipe lifecycle presentation only
Tool Lab Window: one-tool visual lifecycle and comparison presentation only
Viewer: rendering, camera, pick, overlays; it never executes a Tool
```

No View code-behind may implement algorithm calculation, persistence, validation rules, or recipe mutation. Window creation, viewer hosting, and current-artifact presentation remain view-lifecycle responsibilities.

## Required verification for every future Tool Lab

1. Build with zero warnings/errors.
2. Verify that opening a Tool Lab does not execute Preview, Run, Publish, or mutate the recipe.
3. Verify source and output artifact identities and stale-state handling.
4. Verify only one Recipe Manager window is reused.
5. Capture fresh current-build main, Recipe Manager, and Tool Lab UI evidence; inspect labels, PropertyGrid values, input/output viewers, clipping, and overlap.
6. Prove the old responsibility coupling is absent: Recipe Manager is not hosted in the main Workbench pane; a Tool Lab consumes the shared session and presents separate inputs/outputs.

## Explicit non-goals

- Do not reproduce GoPxL, MERLIC, Zebra, NI, or PolyWorks visual design or source code.
- Do not add camera, PLC, robot, MES, cloud, user-account, or production-line control scope.
- Do not treat a Preview, display transform, or an uncalibrated C3D frame as physical metrology evidence.
- Do not make the Flow Map the only editor or permit unchecked free-form connections.

## Next decision gates

1. G1/G2/G3/G4 are complete: use the tree, read-only Flow Map, selected-tool I/P/O summary, explicit Output Compare, and real-artifact Displayed Outputs manager as one bounded read-first workflow.
2. Next UI priority is G5: add port-level route diagnostics and a compact Problems surface to the existing Flow Map; do not create editable wires or automatic routing.
3. Then complete G6 compatible Tool Catalog scanning. Only after the UI gate is formally accepted may a new algorithm implementation gate be defined.

## 2026-07-20 GoPxL chain-readability reassessment and G1 closure

The owner reviewed the current Workbench against the GoPxL-informed direction
and found the earlier tree, Tool Lab, and dock evidence insufficient for a
human to read a multi-tool route quickly. This is correct: the provisional
`89/100` UI score assessed a narrow set of visible surfaces and is not an
accepted UI-gate score. The product keeps its OpenVisionLab light/navy/teal
visual system; it adopts GoPxL's functional separation of tool chain,
selected-tool configuration, and displayable outputs only.

G1 is now complete: `RecipePipelineReviewView` has a bilingual, read-only
`Flow Map` tab in the existing `Pipeline / Validation` dock. Every existing
recipe row shows `Input contract + authored entity IDs -> Tool -> Output
contract + authored entity ID + current state`; it shares
`SelectedPipelineStep` with the Recipe Navigator and Pipeline table. The
`flow-map` smoke selector opens the same docked pane. It never connects ports,
creates a recipe row, changes a parameter, or invokes Preview/Publish.

Verification on 2026-07-20: solution build `0 warnings / 0 errors`; Tool
Recipe Teaching `18/18`; Workbench docking `21/21`, including explicit Flow
Map activation; screenshot-quality accepted on attempt one for Korean `1920
x 1080`, Korean `1280 x 760`, and English `1920 x 1080`. Evidence is
`artifacts/ui/20260720-gopxl-flow-map-g1/`.

G1 is deliberately not a generic graph canvas: cross-row wires, editable
ports, undo, generic execution, and hardware control are not implemented.

## 2026-07-20 G2 Output Compare closure

G2 is complete: the dockable/floatable `Output Compare` pane owns three
explicit, session-only A/B/C pins. It reads the existing Artifact Registry;
choosing a slot never changes a recipe route, invokes Preview/Publish, or
creates a derived artifact. The available candidates are deliberately narrow:
a verified loaded C3D source and the current non-stale Filter Preview C3D.
The empty `—` option clears a pin. Feature artifacts such as `EdgePointSet`,
lines, and intersections remain overlays/evidence rather than being falsely
rendered as full C3D surfaces.

Each occupied slot carries a short source/output label plus its typed contract,
current state, and entity ID over an independent compact viewer. The pane has
a fixed three-card canvas with horizontal access at compact widths and can be
floated for detailed side-by-side inspection. Rebuilding the Artifact Registry
after Preview preserves already-selected pins; it does not silently clear the
source side of a comparison.

Verification on 2026-07-20: solution build `0 warnings / 0 errors`; Artifact
Navigator plus pin-preservation check `11/11`; Workbench docking `22/22`;
screenshot-quality accepted on attempt one for Korean `1920 x 1080`, Korean
`1280 x 760`, and English `1920 x 1080`. Evidence is
`artifacts/ui/20260720-output-compare-g2/` and
`artifacts/verification/20260720-output-compare-g2-*.txt`.

G2 is not a generic result browser, a linked-camera implementation, or an
algorithm executor. G3 closes the selected-tool configuration summary below;
the WPG remains the single parameter editor.

## 2026-07-20 G3 selected-tool Inputs / Parameters / Outputs closure

G3 is complete: the selected Step Parameters pane begins with a compact,
read-only `Inputs -> Parameters -> Output` card. It shows the existing input
contract and authored entity ID, typed-parameter adapter state, and output
contract plus authored output entity ID together before the WPG. The original
input/output editors remain below, and the WPG remains the only parameter
editor; the summary introduces no new save, Preview, Publish, or route action.

Korean and English labels are localized while contracts and entity IDs retain
their stable technical identifiers. The three familiar WPF UI symbols improve
scan recognition but are accompanied by visible text.

Verification on 2026-07-20: solution build `0 warnings / 0 errors`; Tool
Recipe Teaching `18/18`; Workbench docking `22/22`; Artifact Navigator `11/11`;
screenshot-quality accepted on attempt one for Korean `1920 x 1080`, Korean
`1280 x 760`, and English `1920 x 1080`. Evidence is
`artifacts/ui/20260720-input-parameter-output-g3/`.

G3 is UI structure only. It is not a second parameter editor, writable graph,
generic executor, camera/PLC/HMI integration, affine solver, calibration, or
metrology claim.

## 2026-07-20 G4 Displayed Outputs / Overlay Manager closure

Status: Complete

The new floatable/hideable `Displayed Outputs` lower Workbench dock projects
only the existing typed Artifact Registry. A verified source or current
non-stale Filter C3D can be explicitly shown in the main Viewer or pinned to
the first empty A/B/C Output Compare slot. The selection is session-only: it
does not execute a tool, change a parameter, save a recipe, or reroute an
input. The Viewer display request is handled at the View boundary and reports
success back to the ViewModel before the manager claims that it is displayed.

`EdgePointSet`, `LineFeature`, `CornerAnchor`, and other non-surface typed
artifacts never receive a fabricated C3D surface. Current feature output is
labelled evidence-only and offers only `Focus Step`; stale and declared output
is labelled as having no current displayable output. The dock uses the
OpenVisionLab light/navy/teal theme, text-plus-icon commands, accessibility
names, and Korean/English labels while stable technical contracts and entity
IDs remain unchanged.

Scope: UI-only real-artifact display and compare-entry manager.
Acceptance criteria: only existing C3D artifacts render; feature outputs stay
evidence-only; show/pin/focus never executes or rewires a recipe; the dock
can float/hide without closing; Korean/English 1920 and Korean 1280 layouts
remain legible.
Verification: current-source build `0 warnings / 0 errors`; Tool Recipe
Teaching `18/18`; Workbench docking `24/24`; Artifact Navigator `14/14`;
screenshot-quality accepted on first attempt.
Evidence: `artifacts/ui/20260720-displayed-outputs-g4/`.
Boundary / next dependency: G4 is not an overlay renderer for arbitrary
features, a linked-camera system, generic result browser, writable graph, or
algorithm executor. Next is G5 port diagnostics / Problems, then G6 compatible
Tool Catalog scanning; the UI `80/100` gate is still not accepted.

## P1 local completion evidence

- The main Workbench left pane is now `Toolbox & Entities`; it no longer hosts Recipe Manager controls.
- `Recipe Manager` opens one reusable modeless lifecycle window from the command bar. The window shares the existing Recipe Session ViewModel and leaves dirty-change confirmation with the owning main window.
- `Filter Tool Lab` opens a dedicated input/output comparison window. Its left Viewer keeps the source C3D and its right Viewer displays only the saved Filter Preview output; opening the window does not run Preview or Publish.
- Current-source verification on 2026-07-19: solution build `0 warnings / 0 errors`; docking `15/15`; Recipe Manager/WPG `17/17`; tool-teaching `16/16`; selection contract `17/17`; Height Difference Edge regression `10/10`; Shell, Recipe Manager, and Filter Tool Lab screenshot-quality capture each accepted on attempt 1.
- Evidence folder: `artifacts/ui/20260719-gopxl-tool-lab` and `artifacts/verification/20260719-gopxl-tool-lab`.

At the P1 closure, this covered only presentation/lifecycle separation for the existing Filter adapter; the typed registry/tree was intentionally deferred. Generic Flow Map, linked cameras, Line Fit, intersection, affine, calibration, and metrology remain outside P1 and P2.

## P2 local completion evidence — Typed Artifact Registry + Recipe Navigator

Status: Complete
Scope: Read-first typed artifact registry, recipe input/output tree, composed Shell header UserControl, and purposeful WPF UI icons.
Acceptance criteria: Registry/tree derives only from existing session state, step selection is non-executing, header presentation is isolated from Shell behavior, and visible command/entity affordances retain text/accessibility.
Verification: Current-source build `0/0`; Artifact Registry + Recipe Navigator `9/9`; docking `15/15`; Recipe Manager/WPG `17/17`; teaching `16/16`; selection contract `17/17`; Edge `10/10`; current Shell screenshot quality accepted.
Evidence: `artifacts/ui/20260719-artifact-navigator` and `artifacts/verification/20260719-artifact-navigator`.
Boundary / next dependency: Edge Tool Lab is the next view slice; no writable Flow Map, Line Fit, intersection, affine, calibration, or metrology claim is made.

- `ToolWorkbenchViewModel` now owns a read-first `ArtifactRegistry` and `NavigatorRoots` derived from the existing Recipe Session, source binding, pipeline routes, Filter output, and Height Difference Edge output. The Viewer still owns only rendering and overlays.
- Every current source, selection, and pipeline output exposes a visible typed contract, state, root source ID, input entity ID, unit/frame, output SHA-256 when available, and detail/provenance. Outputs without an actual Preview remain visibly `Declared`; they are never presented as calculated results.
- The Recipe Navigator shows `Source & references` and an ordered `Recipe pipeline`, with each Step carrying explicit Input and Output child nodes. The initial view prioritizes the first Filter `Input -> Output` chain; selecting a Step node only focuses its existing Step Parameters and never executes Preview, Run, or Publish.
- `StudioHeaderView` now composes the entire upper application header: the pre-existing title/context UserControl plus workspace navigation and Recipe Manager/Tool Lab command buttons. The main window owns window/session behavior; the header only raises visual command requests.
- WPF UI `SymbolIcon` affordances are used for workspace modes, lifecycle commands, Tool Lab commands, workflow actions, and typed entity categories. Text labels, tooltips, and automation names remain present; icons are not the only affordance.
- Current-source verification on 2026-07-19: build `0 warnings / 0 errors`; Artifact Registry + Recipe Navigator `9/9`; docking `15/15`; Recipe Manager/WPG `17/17`; tool-teaching `16/16`; selection contract `17/17`; Height Difference Edge regression `10/10`; updated Shell screenshot quality accepted on attempt 1.
- Evidence folder: `artifacts/ui/20260719-artifact-navigator` and `artifacts/verification/20260719-artifact-navigator`.

This closes only the read-first registry/tree and top-header composition. It is not a generic artifact persistence layer, a writable Flow Map, linked-camera implementation, Edge Tool Lab, Line Fit, intersection, affine, calibration, or metrology claim.

## P3 local completion evidence -- Height Difference Edge Tool Lab

Status: Complete
Scope: Dedicated Height Difference Edge Tool Lab, visible FilteredHeightField-to-EdgePointSet presentation, one reusable window route, and a fixed multi-Edge artifact-registry correction.
Acceptance criteria: The Tool Lab keeps an exact Published FilteredHeightField input separate from its EdgePointSet overlay, exposes Preview/Publish without automatic execution, preserves WPG as the typed parameter editor, and shows a current C3D output/result evidence surface.
Verification: Current-source build `0/0`; Height Difference Edge Workbench `11/11`; Artifact Registry + Recipe Navigator `9/9`; docking `15/15`; Recipe Manager/WPG `17/17`; teaching `16/16`; selection contract `17/17`; current Shell and Edge Tool Lab screenshot quality accepted on attempt 1.
Evidence: `artifacts/ui/20260719-edge-tool-lab` and `artifacts/verification/20260719-edge-tool-lab`.
Boundary / next dependency: Line Fit remains design-only until the owner approves its selection, residual, and acceptance decisions. No writable Flow Map, intersection, affine execution, calibration, or metrology claim is made.

- `Edge Tool Lab` is a separate reusable window from the composed `StudioHeaderView`. It selects an existing Height Difference Edge step but does not Preview or Publish on open.
- The left Viewer renders only the current Published `FilteredHeightField`; the right Viewer renders the same saved height field with the existing `EdgePointSet` marker, direction, and taught-band overlay. EdgePointSet is deliberately not represented as a fabricated second surface.
- The visible output header reports Preview/Published state, accepted point/scanline count, eligible/missing-pair diagnostics, and output SHA-256. The shared WPG inspector retains the typed Edge parameter draft boundary.
- The fixed C3D smoke used the explicit smoke-only band `rows 285..419`, `columns 290..305`, `AcrossColumns`, `Rising`, `MinimumDelta=100`, produced `135` edge points from Published Filter hash prefix `569436F1ED6D`, and emitted Edge hash prefix `94F44FC244DC`. These values are evidence for the visual adapter path only and were not saved to the recipe.
- The Artifact Registry now treats an actual `EdgePointSet` as content only for the Edge step that owns the matching output entity ID. Other declared Edge rows remain `Declared`, preventing a multi-Edge template Preview from failing while the navigator is rebuilt.

This closes only the Edge Tool Lab presentation and its actual multi-Edge recipe navigation path. It does not implement Line Fit, a free-form graph editor, intersections, affine execution, calibration, or metrology.

## P4 local completion evidence -- Workbench visual-system baseline

Status: Complete
Scope: A shared OpenVisionLab light/navy/teal visual system and a 1920x1080 docked Workbench baseline.
Acceptance criteria: Viewer remains the primary work surface; docked panels/readable information cards use one palette; active workflow state is teal rather than an OS-default blue; no Tool or recipe behavior changes.
Verification: Current-source build `0/0`; docking `15/15`; Recipe Manager/WPG `17/17`; teaching `16/16`; selection contract `17/17`; Artifact Navigator `9/9`; fresh 1920x1080 final screenshot quality accepted on attempt 1.
Evidence: `docs/OPENVISIONLAB_3D_WORKBENCH_THEME_1920_BASELINE_20260719.md`, `artifacts/ui/20260719-workbench-theme-1920`, and `artifacts/verification/20260719-workbench-theme-1920`.
Boundary / next dependency: P5 now closes separate visual approval for Recipe Manager, Filter Tool Lab, and Edge Tool Lab. Calibration and Expert remain Main Window workspaces. Line Fit remains design-only pending owner approval.

- The default main window is `1920 x 1080`, with a `1180 x 720` lower bound. The docked left/viewer/right ratios are `0.82 : 2.45 : 1.05`, and the top/bottom ratios are `2.35 : 1`.
- Navy application/dock headers, cool-light panels and command bars, teal selected workflow state, and dark text on light information cards are shared Shell resources. Panels must not inherit the dark docking-host foreground.
- This intentionally preserves OpenVisionLab's own design language; no commercial UI colors, pane positions, assets, wording, or source implementation are copied.

## P5 local completion evidence -- teaching-window title view

Status: Complete
Scope: Remove native Windows title bars from all four Shell WPF Windows and use the composed `StudioTitleBarView` for product/window context plus minimize, maximize/restore, and close commands.
Acceptance criteria: Every Window is custom-chrome; Tool Lab title/context is not duplicated; title controls retain accessible names; recipe/tool behavior remains unchanged.
Verification: Current-source build `0/0`; docking `15/15`; Recipe Manager/WPG `17/17`; teaching `16/16`; selection contract `17/17`; Artifact Navigator `9/9`; Edge Workbench `11/11`; UI Automation title commands (maximize/restore/close); current Main/Recipe/Filter/Edge captures accepted on attempt 1.
Evidence: `docs/OPENVISIONLAB_3D_WORKBENCH_THEME_1920_BASELINE_20260719.md`, `artifacts/ui/20260719-teaching-window-theme-1920`, and `artifacts/verification/20260719-teaching-window-theme-1920`.
Boundary / next dependency: Custom chrome changes window presentation and standard Window commands only. It does not change dirty-save behavior, Tool execution, Preview/Publish, Viewer gestures, Line Fit, or any metrology claim.

- `StudioTitleBarView` is now the shared title UserControl for Main Window, Recipe Manager, Filter Tool Lab, and Edge Tool Lab. Every Shell WPF Window has `WindowStyle=None`, `WindowChrome`, a 56-pixel title band, and named window commands.
- The title band states the product and current window; the Tool Lab header below it states the algorithm and its explicit commands. This prevents duplicate Tool names while preserving scanable context.
