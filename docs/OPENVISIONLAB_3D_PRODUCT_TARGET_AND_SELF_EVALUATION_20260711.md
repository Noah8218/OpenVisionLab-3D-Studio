# OpenVisionLab 3D Product Target And Self Evaluation

Updated: 2026-07-20

Status: current product-direction source of truth. Older market reviews remain useful as history, but this document controls current priorities when they conflict.

Viewer reliability phases and claim limits are governed by `docs/OPENVISIONLAB_3D_VIEWER_RELIABILITY_PHASES_20260714.md`.

## Executive Decision

OpenVisionLab 3D Studio should target an explainable, local, rule-based 3D inspection recipe workbench for height maps, point clouds, and meshes.

The product workflow is:

```text
Load measured 3D data and optional nominal data
  -> define units, coordinate frame, references, and ROIs
  -> add ordered inspection steps
  -> Preview explicitly
  -> review metrics, tolerance state, and 2D/3D overlays
  -> Publish an explicit result entity/layer
  -> save the recipe
  -> replay the same recipe in the headless Runner
  -> review a durable run record and report
```

## UI/UX Delivery Priority - 2026-07-20

The owner has paused new algorithm development until the UI/UX completion gate
reaches at least `80/100` accepted points. UI work must preserve existing
explicit Preview/Publish/Run behavior and may use current algorithms only as
display and smoke evidence. The gate, scorecard, delivery order, and evidence
requirements are fixed in
`docs/OPENVISIONLAB_3D_UI_UX_80_COMPLETION_GATE_20260720.md`.

## Full-XYZ affine design authorization - 2026-07-20

The owner explicitly authorized the next algorithm phase to begin with design.
This overrides the UI pause only for the documented full-XYZ affine design;
it does not accept the UI gate, start solver code, or weaken any explicit
Preview/Publish/source-immutability boundary. The authoritative design is
`docs/OPENVISIONLAB_3D_XYZ_AFFINE_TOOL_DESIGN_20260720.md`: it separates
Affine Solve from Apply and later Re-grid, requires the existing exactly-four
non-coplanar correspondence evidence, and forbids silent planar fallback,
least squares, automatic matching, calibration, and metrology claims in v1.
Implementation requires owner approval of that design plus formal UI-gate
acceptance or a later explicit owner authorization to start code.

Current P3 Tool-Lab review checkpoint on 2026-07-20: **the existing Filter,
Height Difference Edge, Line Intersection, and Landmark Correspondence Tool
Labs share an OpenVisionLab-owned input -> parameters -> output/evidence
presentation rhythm.** The shared lower review header retains the one staged
parameter editor and current state while the upper output/evidence headers
make execution/hash information visible without scrolling. Korean/English
command labels are localized; technical IDs and contracts stay stable. This
is UI-only: Preview/Publish remain explicit, source geometry stays immutable,
and no Tool Lab, graph editor, solver, calibration, or metrology capability is
added. Current-source evidence passes build `0/0`, Tool Recipe Teaching
`18/18`, Workbench docking `20/20`, and four Korean plus one English Tool Lab
screenshot-quality captures. P4 owner scoring remains required before the
`80/100` UI gate or algorithm work can be considered open.

Current GoPxL chain-readability checkpoint on 2026-07-20: the owner's review
reopened the provisional `89/100` UI score because it was too narrow: it did
not adequately evaluate whether an operator can see the `INPUT -> TOOL ->
OUTPUT` route for a multi-tool inspection recipe. The score is historical,
not accepted, and the UI gate remains closed. G1 provides a bilingual,
read-only Flow Map inside Pipeline / Validation. G2 provides a floatable/
hideable `Output Compare` dock whose explicit A/B/C pins render only a
verified source or the current Filter Preview C3D; non-surface feature
artifacts remain evidence, not fabricated surfaces. G3 provides a compact,
read-only selected-tool `Inputs -> Parameters -> Output` summary above the
existing single WPG parameter editor. All three surfaces are read-only with
respect to the recipe except for the pre-existing authored editors and
Apply/Discard boundary. Current source passes build `0/0`, Artifact Navigator
`11/11`, Workbench docking `22/22`, Tool Recipe Teaching `18/18`, and Korean
`1920 x 1080` / `1280 x 760` plus English `1920 x 1080`
screenshot-quality capture. The following G4 checkpoint supersedes that
historical next-decision statement; a writable graph, generic executor,
sensor/PLC/HMI, affine solver, calibration, and metrology scope remain out of
bounds. See
`docs/OPENVISIONLAB_3D_UI_UX_80_COMPLETION_GATE_20260720.md` and
`docs/OPENVISIONLAB_3D_GOPXL_TOOL_LAB_DIRECTION_20260719.md`.

Current GoPxL chain-readability G4 checkpoint on 2026-07-20: the lower
Workbench now adds a floatable/hideable `Displayed Outputs / Overlay Manager`
beside the existing Flow Map, Output Compare, and selected-tool I/P/O summary.
It reads the existing Artifact Registry and permits only a verified source or
current non-stale Filter C3D to be shown in the main Viewer or pinned to the
first empty A/B/C compare slot. `EdgePointSet`, lines, corners, stale, and
declared output remain evidence-only or unavailable; no synthetic C3D surface
is created. Show/pin/focus actions neither execute a tool nor alter routes,
parameters, or recipe persistence. Current-source evidence passes build `0/0`,
Tool Recipe Teaching `18/18`, Workbench docking `24/24`, Artifact Navigator
`14/14`, and screenshot-quality at Korean `1920 x 1080` / `1280 x 720` plus
English `1920 x 1080`. Evidence is
`artifacts/ui/20260720-displayed-outputs-g4/`. The UI gate remains unaccepted;
next UI scope is G5 port diagnostics / Problems followed by G6 compatible Tool
Catalog scanning. A writable graph, generic executor, camera/PLC/HMI, affine
solver, calibration, and metrology remain out of scope.

Current GoPxL chain-readability G5 checkpoint on 2026-07-20: the existing
read-only Flow Map now exposes actual input/output port state from the typed
Artifact Registry: ready/current, declared-upstream waiting, stale, and
unresolved. The new compact bilingual Problems tab remains in the same
Pipeline / Validation dock. It aggregates only route issues plus existing
recipe validation messages; Focus Step selects the authored step but cannot
edit a route, parameter, or persistence state and cannot Preview, Run, or
Publish. Current-source evidence passes build 0/0, Tool Recipe Teaching
18/18, Workbench docking 25/25, Artifact Navigator 18/18, and first-attempt
screenshot quality at Korean 1920 x 1080 / 1280 x 760 plus English 1920 x
1080. The compact capture retains the first route problem and Focus action
without scrolling. Evidence is artifacts/ui/20260720-flow-problems-g5/.
G5 is UI-only; its completed G6 compatible Tool Catalog follow-up is recorded
in the next checkpoint. The UI gate remains unaccepted, and a writable graph,
generic executor, camera/PLC/HMI, affine solver, calibration, and metrology
remain out of scope.

Current GoPxL chain-readability G6 checkpoint on 2026-07-20: Toolbox now has
a compact bilingual compatible-next-tool scan derived only from the existing
typed Artifact Registry. A ready C3D source proposes Filter, ROI / Crop,
2-Point Line, and 3-Point Plane; a current grid selection plus Published
FilteredHeightField proposes Height Difference Edge; and a Published
EdgePointSet proposes 3D Line Fit. Cards show the declared input contract and
the exact candidate IDs. Selecting a card only changes the existing Toolbox
selection: it cannot add/reorder a step, write a route, edit/save a recipe,
or Preview/Run/Publish. Current-source evidence passes build 0/0, Tool Recipe
Teaching 18/18, Workbench docking 25/25, Artifact Navigator 22/22, and
first-attempt screenshot quality at Korean 1920 x 1080 / 1280 x 760 plus
English 1920 x 1080. Evidence is
artifacts/ui/20260720-compatible-tool-catalog-g6/. The UI gate remains
owner-unaccepted; the next decision is an owner UI/UX acceptance re-review.
A writable graph, automatic routing, generic executor, camera/PLC/HMI,
affine solver, calibration, and metrology remain out of scope.

Current GoPxL chain-readability G7 checkpoint on 2026-07-20: the compact
compatible catalog now exposes a separate explicit `Add` action beside its
visible candidate and one read-only nearest-missing-input explanation. The
remaining candidates are internally scrollable so `1280 x 760` retains both
the next authoring action and why the next typed tool is unavailable. Selecting
the candidate still only changes Toolbox selection; the new `Add` command adds
one ordinary taught step with exactly the visible candidate IDs and never
Preview/Run/Publishes. Step title and state now use responsive separate rows.
Current-source evidence passes build `0/0`, Tool Recipe Teaching `18/18`,
Workbench docking `25/25`, Artifact Navigator `24/24`, and first-attempt
screenshot quality at Korean `1920 x 1080` / `1280 x 760` plus English
`1920 x 1080`. Evidence is
`artifacts/ui/20260720-responsive-catalog-g7/`. This closes the two concrete
compact-layout deductions from the self-evaluation, but does not accept the
UI gate; keyboard-only, high-DPI, and first-time-operator owner review remain
required. No algorithm, automatic routing, generic executor, camera/PLC/HMI,
affine solver, calibration, or metrology capability was added.

For the user's full-XYZ point-cloud workflow, the ordered inspection steps are intended to express `source -> filter -> height-difference edge candidates -> fitted 3D lines/planes -> intersections/correspondence evidence -> XYZ affine transform -> derived 3D map -> thickness/warpage/other measurements -> review`. This is a product direction, not a claim that the generic executor or affine solver exists today.

Current typed-tool chain checkpoint on 2026-07-19: **Filter, Height Difference
Edge, 3D Line Fit, and Line Intersection v1 are now bounded executable slices.**
Line Intersection consumes two authored-order current Published `LineFeature`
inputs, applies full-XYZ closest-approach/gap/acute-angle/support gates, and
emits a source-coordinate `CornerAnchor` midpoint only as feature evidence.
The Recipe Workbench carries typed WPG teaching, explicit Preview/Publish,
independent Published-output tracking for two line branches, an Artifact
Registry/Navigator node, Viewer overlay/HUD controls, a separate two-viewer
Tool Lab, and a floatable/hideable `Intersection Evidence` dock pane. Local
evidence passes build `0/0`, Golden plus synthetic full Runner chain `9/9`, full Workbench/Runner hash parity
`23/23`, Line Fit regression `14/14`, Edge regression `11/11`, docking `18/18`,
and a current-build Tool Lab screenshot-quality check. This is not a generic
graph executor, automatic corner discovery, XYZ affine solver, calibration,
or physical/metrology claim. See
`docs/OPENVISIONLAB_3D_LINE_INTERSECTION_TYPED_ADAPTER_IMPLEMENTATION_20260719.md`.
The earlier 2026-07-19 Filter, Edge, Line Fit, WPG, and Tool Lab checkpoint
bullets below remain historical evidence; where they say Line Intersection is
open or blocked, this current checkpoint supersedes only that statement.

Current Landmark Correspondence v1 checkpoint on 2026-07-20: **implemented
as a strict structural-evidence gate, not an affine transform.** The Workbench
now authors one schema `1.2` descriptor plus exactly four source
`CornerAnchor` / reference-XYZ rows, accepts only current Published Line
Intersection artifacts, requires source and reference rank `4/4` plus an
explicit normalized tetrahedron-volume threshold, and carries immutable
Preview/Publish/stale evidence into the Viewer, a reusable Tool Lab, a
floatable/hideable Correspondence Evidence dock pane, and the headless Runner
adapter. Current-source evidence passes a zero-warning/error build,
synthetic correspondence golden `5/5`, selection-schema regression `17/17`,
and dock regression after adding the ninth pane. The UI screenshot shows the
intentionally blocked `0/4` teaching state. A real four-anchor source/reference
fixture, source/reference provenance, and full-chain Runner replay have not
been supplied, so XYZ Affine, Re-grid, Thickness, and Warpage execution remain
blocked. The operator handoff is now fixed in
`docs/OPENVISIONLAB_3D_FOUR_ANCHOR_TEACHING_INPUT_PACKAGE_20260720.md`; it
provides no fabricated coordinate or threshold. See
`docs/OPENVISIONLAB_3D_LANDMARK_CORRESPONDENCE_TYPED_ADAPTER_DESIGN_20260719.md`.

Current A1 full-XYZ Affine Solve authorization on 2026-07-21: **the owner
explicitly reopened implementation only for current Published
`CorrespondenceSet -> AffineTransform3D`.** The slice keeps exactly four
affine-independent pairs, a deterministic source-to-reference double solve,
taught condition and arithmetic-residual boundaries, and explicit
Preview/Discard/Publish evidence. Applying a transform, re-grid, Thickness,
Warpage, physical calibration, and metrology remain separate unimplemented
gates. See `docs/OPENVISIONLAB_3D_XYZ_AFFINE_TOOL_DESIGN_20260720.md`.

Current A1 full-XYZ Affine Solve implementation checkpoint on 2026-07-21:
**deterministic solve-only code is complete, not a physical result.** The typed
tool consumes exactly four current Published correspondence pairs and creates
an immutable `AffineTransform3D` with source/reference provenance,
condition/determinant/residual evidence, and a canonical hash. Its WPG and
Tool Lab retain explicit Preview/Discard/Publish and never fabricate a
transformed surface. Current evidence passes Debug build `0/0`, Runner golden
`4/4`, teaching `18/18`, Recipe Manager/WPG `18/18`, docking `25/25`, Artifact
Navigator `24/24`, and accepted 1920/1280 Workbench plus Tool Lab captures.
The real four-anchor fixture has not been supplied, so actual fixture
Preview/Publish/headless replay remains open; applying points, re-grid,
Thickness, Warpage, calibration, and metrology remain separate blocked
decisions. See
`docs/OPENVISIONLAB_3D_XYZ_AFFINE_SOLVE_IMPLEMENTATION_20260721.md`.

Current Library-Noah algorithm-ownership checkpoint on 2026-07-21:
**Studio is now an adapter, not a duplicate numerical owner, for A1 Full XYZ
Affine Solve and Line Intersection.** The exact vendored `Lib.ThreeD` 2.3.0
package at Library-Noah commit `630e37b9111f3223217c815e19c480546fde8ad7`
contains `FullXyzAffineSolveTool`, `TwoPointLineTool`, and
`LineIntersectionTool`. Current source passes Library-Noah build `0/0`, Smoke
`20/20`, Studio build `0/0`, package integrity, Noah bridge `7/7`, A1 Golden
`4/4`, Line Intersection Golden `9/9`, Line Intersection Workbench `23/23`,
teaching `18/18`, Recipe Manager/WPG `18/18`, docking `25/25`, and Artifact
Navigator `24/24`. Filter, Height Difference Edge, and 3D Line Fit numerical
algorithms still remain in Studio pending separate boundary-preservation gates;
the owner-approved 2-Point Line Studio adapter is the next typed tool slice.
No migration changes the missing real A1 fixture, affine application, re-grid,
calibration, or metrology boundaries. See
`docs/OPENVISIONLAB_3D_ALGORITHM_OWNERSHIP_AND_NOAH_MIGRATION_20260721.md`.

Current 2-Point Line implementation checkpoint on 2026-07-21: **the
owner-approved manual `raw C3D + PointSet(2) -> ordered full-XYZ LineFeature`
slice is complete for local software evidence.** The strict Studio adapter
resolves current source values for exactly two ordered grid cells, delegates
construction to packaged Library-Noah `TwoPointLineTool`, preserves its own
immutable source/selection/hash lineage, and provides typed WPG, explicit
Preview/Publish/stale state, source-change clearing, Artifact Registry output,
Viewer segment overlay, a single-instance dual-viewer Tool Lab, and headless
Runner replay. `IC3DLineGeometry` is deliberately narrow and lets the existing
Line Intersection tool consume either a fitted or a picked published line
without a generic feature graph. Current-source evidence passes Debug build
`0/0`, 2-Point Line Golden/Runner `7/7`, Line Intersection Golden including a
two-picked-line chain `10/10`, 2-Point Line Workbench `16/16`, Line
Intersection Workbench regression `23/23`, Tool Recipe Teaching `18/18`,
Recipe Manager/WPG `18/18`, Artifact Navigator `24/24`, Docking `25/25`, and
a first-attempt actual-Thickness C3D Tool Lab capture. This does not detect a
physical edge, create an acceptance result, prove calibrated length, or
authorize affine application, re-grid, Thickness, Warpage, calibration, or
metrology. See
`docs/OPENVISIONLAB_3D_TWO_POINT_LINE_TYPED_ADAPTER_DESIGN_20260721.md`.

Next typed-tool design checkpoint on 2026-07-21: **3-Point Plane v1 is
documented, not implemented.** It proposes a raw-C3D plus recipe-owned
`PointSet(3)` manual oriented datum plane, with ordered normal, support
triangle, strict source binding, a pure future Noah calculation, and a typed
future Studio adapter. It intentionally excludes best-fit ROI planes,
measurement/OK-NG, affine application, re-grid, Thickness, Warpage,
calibration, and metrology. Implementation waits for the owner to confirm
normal orientation and datum/UI semantics. See
`docs/OPENVISIONLAB_3D_THREE_POINT_PLANE_TYPED_ADAPTER_DESIGN_20260721.md`.

Current maturity is **early inspection workbench MVP**. No repository-backed percentage is used.

- Viewer Foundation v1: **passed for the current fixed sample matrix**.
- C3D map fidelity: **display frame passed for the fixed Thickness sample; physical scale unverified**.
- Inspection Recipe v1: **baseline passed for five independent typed C3D slices: numeric-reference-ROI plane flatness, explicit-cell point-pair dimensions, explicit two-region Gap/Flush, explicit reference/measurement-ROI Volume, and exact-row Cross-section Dimensions**.
- Functional inspection direction: **feature-first C3D Thickness Teaching v1 and bounded local raw-height Warpage now cover load -> one taught grid ROI -> explicit Preview/Publish -> recipe save/reopen -> Runner replay -> Viewer and Shell result surfaces in the current local working tree. Neither `raw-height` result is a calibrated physical measurement claim. See `docs/OPENVISIONLAB_3D_C3D_THICKNESS_TEACHING_20260717.md` and `docs/OPENVISIONLAB_3D_WARPAGE_INPUT_PREFLIGHT_20260717.md`.**
- Tool Recipe Workbench and generic teaching: **the Shell now defaults to `Workbench`, a composable 3D-tool layout with Toolbox / Entities, Viewer, Tool Inspector, Recipe Pipeline / Review, Run Log, Height Profile, and Fit Diagnostics. Teaching Recipe v1 supports C3D source selection, reference declaration, ordered tool authoring, entity routing, parameter editing, structural validation, JSON save/reopen, and a portable `3D/Warpage` feature-first template. Filter, Height Difference Edge, and 3D Line Fit are bounded typed adapters; later intersection, XYZ Affine, re-grid, Thickness, Warpage, and review rows remain teachable but execution-blocked. Existing Task (`Teach -> Inspect -> Review`), Calibrate, and Expert surfaces remain available as specialized workspaces. This does not create a generic executor, automatic feature finder, physical warpage claim, or XYZ affine solver. See `docs/OPENVISIONLAB_3D_TOOL_RECIPE_TEACHING_CONTRACT_20260718.md`, `docs/OPENVISIONLAB_3D_HEIGHT_DIFFERENCE_EDGE_TYPED_ADAPTER_20260719.md`, and `docs/OPENVISIONLAB_3D_LINE_FIT_TYPED_ADAPTER_20260719.md`.**
- Workbench V2, docking, and interactive Profile UI gate: **passed locally on 2026-07-18. The default teaching surface presents recipe/source/frame/alignment/saved state, a category-grouped tool catalog, workflow-grouped properties, an explicit non-running `Teach -> Preview -> Run -> Publish` lifecycle, and six real AvalonDock panes: Project Explorer, 3D View, Properties, Pipeline / Validation, Session Log, and Height Profile. Advanced also has six dock panes and Calibration has four. C3D defaults to Wireframe; right-drag pans, short right-click opens the menu, and the canvas/top `View` menus both expose Fit all, Fit selection, Reset view, Screenshot, and Profile. A camera-aware Viewer-display XYZ triad and draggable P1/P2 height profile are visible; Profile is source-SHA-bound display state and does not alter recipe point-pair evidence or run Preview/Run/Publish. Current evidence passes docking `15/15`, focused contracts `78/78`, Profile pointer `6/6`, both menu bindings `5/5`, current UI quality `4/4`, and BinaryHost manifest/outputs/Host API `14/14`, `12/12`, `3/3`. This does not prove filter/edge/fit/intersection execution, XYZ affine solving, physical calibration, or metrology. See `docs/OPENVISIONLAB_3D_DOCKABLE_WORKBENCH_REFACTOR_20260718.md`.**
- C3D height color/distribution legend gate: **passed locally on 2026-07-19. The Viewer right side now combines a palette-matched vertical raw-height scale with a full-source 32-bin histogram, valid/missing counts, mean, and the most-populated interval. Distribution values come from every finite, non-zero C3D cell and remain independent of render density. Height, Grayscale, and Thermal update the scale from their rendering palettes; Solid, Deviation, and non-C3D sources hide it. The overlay is hit-test-free and display-only, and it remains visible in the docked Shell and zero-`ProjectReference` external host. Current evidence passes the focused contract `20/20`, display settings `82`, docking `15/15`, established Profile pointer `6/6`, accepted current Shell/Viewer captures, and BinaryHost `14/14`, `12/12`, `3/3`. Manual/ROI ranges, physical scale, calibration, uncertainty, and metrology remain open. See `docs/OPENVISIONLAB_3D_C3D_HEIGHT_DISTRIBUTION_LEGEND_20260719.md`.**
- Historical TLB manual functional reference: **all 87 slides of the user-provided former 3D program manual were inspected locally on 2026-07-19 as confidential reference material. OpenVisionLab may reuse abstract workflows such as docked teaching, grouped ROI/entity organization, view presets, profile, palette/contour, source/processed/aligned comparison, and step-result drilldown, but must use its own typed contracts, fixtures, icons, wording, styling, and layout. Sensor/MMI/database/equipment control, alarm/retry, physical correction, and the historical UI identity remain out of scope. See `docs/OPENVISIONLAB_3D_TLB_MANUAL_FUNCTIONAL_REFERENCE_20260719.md`.**
- First generic execution adapter: **Filter v1 passed locally after owner approval. The adapter applies Median Kernel `3/5/7` to C3D `raw-height`, preserves missing cells and grid/frame identity, uses only available valid boundary neighbors, leaves ROI to `ROI / Crop`, and reports preprocessing completion without a measurement OK/NG decision. Data hashes the same source bytes it parses; Tools owns the strict closed-schema rule; Runner and Workbench call the same adapter; Viewer only displays the saved derived C3D. A finite-zero Median output is rejected because C3D reserves zero for missing data and silently saving it would corrupt the approved mask contract. The fixed Warpage output SHA-256 is `569436F1ED6DCB656862935A738FAB691D156BD7FBE1071962FB8DA290E400C6`. Local evidence passes build `0/0`, Golden `13/13`, teaching `16/16`, selections `17/17`, docking `15/15`, height distribution `20/20`, profile ViewModel `8/8`, actual Runner replay, current-build Preview/Publish capture quality, BinaryHost manifest/outputs/API `14/14`, `12/12`, `3/3`, and Viewer/Shell pointer `5/5` each. Gaussian/bilateral, hole filling, ROI fusion, point-cloud/mesh filtering, edge extraction, affine, calibration, and physical claims remain outside this gate. See `docs/OPENVISIONLAB_3D_FILTER_TYPED_ADAPTER_20260719.md`.**
- Height Difference Edge v1 typed-adapter checkpoint: **the owner approved all seven decisions and the slice now passes locally. It consumes a current Published filtered height field plus one source-bound `GridRectangle`, applies explicit axis/polarity/inclusive `MinimumDelta`, selects the strongest adjacent pair per scanline with the lowest-index tie, and emits ordered XYZ midpoints. Missing pairs are skipped and comparisons stay inside the taught band. Core/Tools/Workbench/Viewer/Runner share one contract and rule; Preview/Cancel/stale/Publish are explicit and the output has no measurement OK/NG. Evidence passes build `0/0`, Golden/Runner `13/13`, Workbench/parity `10/10`, teaching `16/16`, selections `17/17`, and current-source UI quality. The smoke-only actual-source band yields `135` points/hash `94F44FC244DCED2409DEEE5AF07C0DF9E2AC108C7C3DBB985647ED0A6B8CFB2B` but is not saved or production teaching. Four real owner-taught bands, remote CI, 3D Line Fit, intersection, correspondence, affine, calibration, and physical claims remain open. See `docs/OPENVISIONLAB_3D_HEIGHT_DIFFERENCE_EDGE_TYPED_ADAPTER_20260719.md`.**
- 3D Line Fit v1 typed-adapter checkpoint: **the owner approved all nine decisions and the slice now passes locally. It consumes one current Published EdgePointSet and fits full numeric `(column, raw-height, row)` data with deterministic consensus plus orthogonal TLS; candidate priority is count/RMS/span/pair-index, pair scheduling is SHA-256 bounded at 256, canonical direction follows the positive scanline axis, and the support segment spans inlier projections only. Workbench/Runner share the same Tools rule, with explicit Preview/Cancel/stale/Publish and no implicit Edge execution. Viewer evidence is teal inliers, amber outliers, a finite line/arrow, a selected residual, and identical display-only controls in the top View and short-right-click menus; Fit Diagnostics is a linked, hideable/floatable dock pane. Evidence passes build `0/0`, Golden `9/9`, Workbench/hash parity `14/14`, docking `17/17`, Edge regressions `13/13` and `11/11`, and an accepted current-source actual-Warpage C3D capture with `135` inputs/inliers/plot points. The C3D smoke uses an in-memory residual limit of `100`, not a saved teaching value. The result is uncalibrated feature extraction, not inspection OK/NG, physical accuracy, or metrology. Line Intersection design/approval, correspondence, affine, real taught limits, provenance, calibration, and physical claims remain open. See `docs/OPENVISIONLAB_3D_LINE_FIT_TYPED_ADAPTER_20260719.md`.**
- Recipe Manager + WPG teaching v1: **passed locally on 2026-07-19. The docked Recipe Manager owns explicit New/Open/Save/Save As and a bounded Recent list; validates recorded C3D path/length/SHA-256/grid identity; opens missing or mismatched sources in repair state without execution; and clears stale Viewer geometry. Filter and Height Difference Edge use detached typed WPG drafts with explicit Apply/Discard, invariant serialization, invalid-edit rejection, missing-known-property restoration, and unknown-property preservation. Unsupported steps remain visible/read-only and round-trip unchanged. WPG is a pinned Shell-only .NET 10 package with view-local theme resources. Evidence passes build `0/0`, package integrity, Recipe Manager/WPG `17/17`, teaching `16/16`, selections `17/17`, docking `15/15`, Filter `13/13`, Edge `13/13`, current-build UI quality, and sequential verifier cleanup with zero remaining processes. This does not create a generic executor or implement Line Fit, intersection, XYZ affine, calibration, or metrology. See `docs/OPENVISIONLAB_3D_RECIPE_MANAGER_WPG_IMPLEMENTATION_20260719.md`.**
- GoPxL-informed Tool Lab direction: **approved by the owner on 2026-07-19. Recipe Manager is a separate single-instance lifecycle window; the primary Workbench is Toolbox/Entities plus Viewer, typed Step Parameters, Pipeline/Validation, Log, Profile, and Fit Diagnostics; each executable algorithm is developed as a dedicated Tool Lab with explicit Preview/Publish and separate input/output 3D result inspection; the Recipe Navigator tree is primary and a typed `INPUT -> OUTPUT` Flow Map is advanced/read-first. P1 Filter Tool Lab uses separate source and preview Viewer instances; P2 derives a read-first typed Artifact Registry and Recipe Navigator Tree from the shared Recipe Session; P3 Edge Tool Lab shows the exact Published FilteredHeightField beside the same height field with the `EdgePointSet` overlay, result diagnostics, and SHA-256 identity. The shared P4 visual baseline defines an OpenVisionLab-owned light/navy/teal Workbench at 1920x1080, with dark readable text on light information surfaces and a viewer-dominant docked layout. P5 makes the composed `StudioTitleBarView` the title UserControl for every Shell WPF Window, removing native Windows title bars while retaining named minimize, maximize/restore, and close commands. Current source/selection/declared-output/actual Filter, Edge, and Line Fit artifacts distinguish `Declared`, `Preview`, `Published`, and `Stale`, and Step-node selection changes focus only. `StudioHeaderView` owns the composed title/context/navigation/command presentation and WPF UI icons accompany, but never replace, text/tooltips/accessibility names. This is a functional UI/UX direction informed by GoPxL and other commercial tools, not a copied visual design or an implementation claim for a generic graph editor, intersection, affine, calibration, or metrology. See `docs/OPENVISIONLAB_3D_GOPXL_TOOL_LAB_DIRECTION_20260719.md`, `docs/OPENVISIONLAB_3D_WORKBENCH_THEME_1920_BASELINE_20260719.md`, and `docs/OPENVISIONLAB_3D_LINE_FIT_TYPED_ADAPTER_20260719.md`.**
- Thickness external-review interchange gate: **revalidated locally on 2026-07-18. The exact `1301 x 1967` C3D/PNG pair remains unchanged, and a fresh 66,212-vertex/128,516-visualization-face PLY export passes zero-error .NET XYZ/RGB readback plus independent Python XYZ error `2.34313965e-7` and RGB error `0`. The PNG is a human-readable height-color reference and the PLY is a Viewer-display interchange asset; neither establishes physical units or calibration. See `docs/OPENVISIONLAB_3D_MAP_FIDELITY_VALIDATION_20260711.md`.**
- Warpage input preflight: **the local Thickness and Warpage C3D and PNG candidates remain byte-identical and carry no independent source/acquisition metadata. The user designated `3D/Warpage` as the local Warpage input, so the current `c3d-warpage` recipe, Shell task, Viewer overlay, and Runner replay are permitted as an explicitly local raw-height workflow. The Library-Noah bridge and current-source golden prove calculation/contract behavior only; distinct source meaning, unit/frame, datum/reference, acquisition provenance, and calibration evidence remain required for any physical claim. See `docs/OPENVISIONLAB_3D_WARPAGE_INPUT_PREFLIGHT_20260717.md`.**
- Release candidate: **Viewer bundle prerelease `v0.1.0-rc.1` is published at commit `ac57687`; local, Windows CI, public archive/manifest hash, downloaded-bundle BinaryHost, and `Matched` Viewer/Runner gates pass**.
- Current development identity: **post-RC source builds, manifests, and Run Records identify as `0.1.1-dev`; no corresponding tag or package is published**.
- Calibration workbench: **Phase A-E View/ViewModel/Model baseline, explicit offline Study binding, and LiveCharts2 Run Chart pass locally; representative Model `34/34`, source-identity loader `13/13`, ViewModel workflow `55/55`, and current native WPF capture are recorded. Phase F adds a typed aligned-point repeatability Model/Tool contract `33/33`; Phase G adds a closed-schema aligned-source Study/Mapping loader and headless Runner report `20/20`. The loader independently verifies synthetic Study/source/Mapping byte length/SHA-256 from the bytes it parses, including UTF-8 BOM JSON, rejects duplicate acquisition evidence, and rejects source/unit/frame/alignment/correspondence mismatches before per-point evaluation. The Runner preserves Study/source/Mapping provenance and point-level statistics, but does not prove mapping derivation. Real repeated acquisition data, trusted raw-source-to-mapping derivation, linked 3D selection, active profiles, physical calibration, uncertainty, and Gauge R&R remain incomplete**.
- Calibration Windows CI: **Studio commit `c45ce78` passed Actions run `29569056102` with the Library-Noah package/bridge, aligned Model `33/33`, aligned Study/Runner `20/20`, Thickness, Calibration Center ViewModel, and existing Viewer/Runner gates all green. Artifact metadata is ID `8402387241`, `3,727,932` bytes, digest `sha256:24080e4ef536a56a5c56a5178822ecfb885c4ae71d96c145e339ded4e0045787`; archive contents were not independently downloaded because GitHub requires authentication. Library-Noah warning-cleanup commit `c2b5860` also passed Build run `29569055985`**.
- Nominal/actual inspection: **two fixed same-design NIST physical instances pass independent CloudCompare comparison, full-query Viewer/Shell Preview and Publish, typed recipe save/reopen, headless Runner replay, schema `1.2`, and Viewer/Runner `Matched`; Stanford supplied-transform application and the local difficult-geometry synthetic matrix also pass, while registration recovery, arbitrary datasets, uncertainty, and metrology certification remain incomplete**.
- Production integration: **intentionally out of scope**.

- Pointer-input smoke target readiness: **passed locally for the current source**. A visible/enabled Viewer point can still resolve to another desktop window, so the smoke normalizes HWND roots, checks target/focus before every gesture, temporarily attaches the foreground input queue only in the smoke path, and detaches in `finally`. Final current-source evidence passes Viewer `5/5`, Shell `5/5`, fixed matrix `128/128`, and DLL-only BinaryHost manifest/outputs/Host API `13/13`, `12/12`, `3/3`. See `docs/OPENVISIONLAB_3D_VIEWER_POINTER_TARGET_RELIABILITY_20260716.md`.

Passing Viewer Foundation v1 does not mean the viewer is production-complete. It means rendering, camera, visibility, picking, selection, measurement/result overlays, color modes, Shell hosting, and screenshot evidence are stable enough to protect as a regression baseline while inspection workflow development begins.

The reliability decision is phase-specific: Phase 1 has passed for the fixed supported scope locally and in the current Windows CI workflow, including Foundation, selected-point provenance, current-versus-next-Preview density-state clarity, hosted dual-capture, and mandatory real WPF pointer input in standalone Viewer and hosted Shell. This does not generalize to every Windows session or arbitrary geometry. Phase 2 geometric generalization is not passed; Phase 3 physical/metrology reliability is blocked and unverified. Do not convert those decisions into one product-reliability percentage.

NIST AMMT `Overhang Part X4` is the first corresponding local external measured/nominal baseline. Its `9 x 5 x 5 mm` nominal STL loads correctly with 2,904 triangles, while the Part 1 XCT surface is a distinct 8,560,096-triangle binary STL above the current Viewer limit. The independent CloudCompare 2.13.2 deviation prerequisite passes in the NIST-provided 3-2-1 part frame over all 4,223,524 CloudCompare-unique measured vertices: unsigned mean/std are `0.192040211` / `0.208181684 mm`, signed mean/std are `0.0124131265` / `0.282957542 mm`, XYZ is preserved exactly, and independent binary-PLY verification agrees within `1e-6 mm`. OpenVisionLab streams the original source and matches the fixed query's unsigned/robust-signed output within `1e-6 mm`. Viewer and Shell run all 4,223,524 query points, classify `548,207` below, `2,990,143` within, and `685,174` above `[-0.3, 0.3] mm`, and publish a separate result entity/layer. Fast/Balanced/Detailed Viewer runs render `24,992` / `59,487` / `145,639` signed display samples while preserving normalized measurement/published-evidence SHA-256 `2FD93EF942D12C621A76964EF681816EE831CD8DEA214EF0A201F602BA30D1C9`. A completed Balanced result now remains explicitly labelled `Current display: Balanced | 59,487 | stride 71` after Detailed is selected, while `Next Preview: Detailed` states that explicit Preview is required; the subsequent Detailed Preview becomes current with `145,639` samples and stride `29` without changing that evidence hash. The typed recipe reopens with stable actual/nominal/query identities and hashes, and Runner produces the same expected `Fail` status and statistics with `ViewerRunnerComparison|Matched`; schema `1.2` JSON plus HTML/CSV preserve step and execution identity. The current executor/result verification passes `27/27`, ViewModel verification passes `71` checks, and a real Balanced pointer-ray smoke exposes ordered query index, actual/query source IDs, signed/unsigned deviation, nearest nominal triangle, and tolerance state in Viewer and Shell. Separate real OS pointer smokes route deterministic click/orbit/pan/zoom events through both hosts and produce byte-identical repeat reports per host. The maximum UI contract-statistic delta from independently parsed CloudCompare output remains `1.3381639552001445e-7 mm`. This closes one fixed identity-frame product slice and the locally/current-Windows-CI-validated fixed-scope Phase 1 Viewer gate, not other sampling/topology cases, semantic mismatch between non-empty declared unit/frame and source truth, non-identity alignment, physical uncertainty, metrology certification, or redistribution approval. See `docs/OPENVISIONLAB_3D_MEASURED_NOMINAL_SAMPLE_REVIEW_20260714.md`, `docs/OPENVISIONLAB_3D_NIST_CLOUDCOMPARE_DEVIATION_BASELINE_20260714.md`, `docs/OPENVISIONLAB_3D_NIST_NOMINAL_ACTUAL_END_TO_END_20260714.md`, and `docs/OPENVISIONLAB_3D_VIEWER_RELIABILITY_PHASES_20260714.md`.

Phase 2 evidence progressed on 2026-07-15 without changing the phase decision. NIST Overhang X4 Part 2 is a separately manufactured and measured source with `8,040,658` triangles, SHA-256 `0F74D3A949488C161DAC71681420A171B1EDA3E478ED24D492D33AA6C9F7F032`, and bounds distinct from Part 1. The exact Part 1 CloudCompare 2.13.2 executable extracted `3,965,430` ordered validation vertices; independent PLY verification preserves exact XYZ, and OpenVisionLab unsigned/robust-signed full-query parity passes with maximum difference `7.1853447186631669e-7 mm`, zero material sign mismatches, one explicitly equivalent float-epsilon zero, and 100% signed coverage. The visible Part 2 workflow preserves distinct actual/query IDs, explicit Preview/Publish, `59,186` Balanced display samples independent of all `3,965,430` measured points, selected-point provenance, typed recipe save/reopen, schema `1.2`, and `ViewerRunnerComparison|Matched`. Stanford Drill separately supplies 12 real range scans and 11 published non-identity translation/quaternion transforms; its local research-only gate matches independent Python/Runner and CloudCompare transform output and rejects a controlled tamper. The synthetic difficult-geometry audit passes mesh deviation `23/23`, nominal/actual execution `29/29`, build `0/0`, and fixed Viewer/Shell matrix `128/128` locally and in Windows CI. Phase 2 remains not passed because only the runtime-neutral registration acceptance policy has passed; no approved engine maps actual results through it, and Viewer/Runner registration parity remains open. See `docs/OPENVISIONLAB_3D_NIST_PART2_CLOUDCOMPARE_DEVIATION_BASELINE_20260715.md`, `docs/OPENVISIONLAB_3D_NIST_PART2_VISIBLE_WORKFLOW_20260715.md`, `docs/OPENVISIONLAB_3D_STANFORD_TRANSFORM_BASELINE_20260715.md`, and `docs/OPENVISIONLAB_3D_PHASE2_DIFFICULT_GEOMETRY_GOLDENS_20260715.md`.

The measured/nominal workbench View, ViewModel, shared Model, Preview, Publish, recipe roundtrip, and Runner checkpoints now pass in that order. Core owns typed comparison and result contracts, Data owns ordered PLY parsing, Tools owns the numerical executor and typed recipe, the ViewModels own durable workflow/input/published state, and the View/code-behind boundary is limited to binding, rendering, file/CLI smoke setup, and event bridging. Current-source end-to-end evidence is under `artifacts/nominal_actual_publish_20260714`.

The source-aware Viewer display-settings slice has separately completed its View, ViewModel, Viewer-local Model, C3D Geometry Style rendering, local deterministic performance, and C3D Grayscale/Thermal Color Map checkpoints. Typed source, Geometry Style, and Color Map identifiers plus immutable `ViewerDisplaySettingsSnapshot` define the effective display-state contract; SharpGL consumes that snapshot for C3D Points, Wireframe, Surface, and Surface + Edges. The display proxy triangulates only complete stride-adjacent source cells, leaves holes open, uses reduced display-only grid overlays, and remains separate from source-cell picking and measurement. Static C3D geometry uses an OpenGL display list; result-owned Plane Flatness Deviation colors bypass it. Two final 31-frame Fast/Balanced/Detailed runs pass `24/24` cases on the recorded local GTX 1060 3GB machine, with minimum observed FPS of `46.786`, `32.574`, and `18.352` respectively. The fixed Balanced 33,761-point C3D sample also completes 90-frame Grayscale and Thermal smokes at `75.303 FPS / 5.272 ms` and `37.049 FPS / 10.438 ms`. Commit `3136ebe` adds a mandatory direct LUT gate, and Windows Actions run `29409271743` passes both color captures/contracts plus all existing gates. These are fixed-sample behavior claims; the performance figures remain local, not cross-machine guarantees. Display changes remain outside inspection Preview, Publish, recipe fingerprints, and Host API v1.0. Preserve `docs/OPENVISIONLAB_3D_C3D_GEOMETRY_STYLE_PERFORMANCE_20260715.md`, `docs/OPENVISIONLAB_3D_C3D_COLOR_MAPS_20260715.md`, and their artifact folders as evidence sources. The later full-source C3D height/distribution legend closes the automatic source-range legend gap; manual/ROI ranges, inversion, and physical color calibration remain open.

The GLB/STL Geometry Style checkpoint passed locally and in Windows CI on 2026-07-15 in View -> ViewModel -> Viewer-local Model/render order. The existing standalone and Shell Views continue to bind to one Viewer-owned display surface; imported triangle meshes now expose typed Points, Wireframe, Surface, and Surface + Edges choices, and SharpGL consumes the existing immutable display snapshot without adding recipe, result, or Host API state. BoxTextured preserves its uploaded source texture, BoxVertexColors preserves vertex colors, and Tetrahedron preserves the Solid fallback across all four styles. The focused verifier passes `15/15`: all 12 sample/style cases render distinct screenshot hashes, while each sample retains one pick contract and one two-point measurement contract. Display ViewModel verification passes `79` checks, the fixed matrix passes `128/128`, BinaryHost preserves manifest `13/13`, outputs `12/12`, and Host API commands `3/3`, and established Viewer/Shell pointer report hashes remain unchanged. Commit `c1ea4cb` passed every Windows Actions step in run `29413823276`; authenticated artifact `8342304881` is `3,721,333` bytes with digest `sha256:baa41a597d4cd55894aff2d9cc8bcbe811c853e52402f51d18c084407f95866e`, and its downloaded 37-file imported-mesh evidence set passed direct inspection. This is a fixed-sample rendering claim, not a large-mesh performance, arbitrary-material, or physical-accuracy claim. Preserve `docs/OPENVISIONLAB_3D_IMPORTED_MESH_GEOMETRY_STYLES_20260715.md` as the evidence source.

Current-source revalidation on 2026-07-12 confirmed the gate with `artifacts/viewer_validation_20260712/matrix_smoke_summary_after.txt`: 129 loader, display, pick, measurement, color, density, Shell-hosting, contract, and controlled-failure checks passed with no failures. C3D-specific detailed display, point picking, two-point distance/height evidence, the 10/10 mapping golden suite, a 66,212-point zero-error .NET PLY roundtrip, independent Python recalculation, and Open3D 0.19.0 re-save comparison also passed in the Viewer display frame. This closes the current fixed-scope Viewer validation; physical calibration and licensed metrology comparison remain separate blocked trust gates.

Full-resolution external-viewer validation on 2026-07-13 strengthened trust gate T3. Stable portable CloudCompare 2.13.2 independently loaded and re-saved all `1,653,562` current-source C3D vertices with unchanged order/RGB and maximum coordinate-component drift `5.00000001e-7` Viewer units. Its C2C mean/std are `4.91657e-7` / `1.49337e-7`, and independently reconstructed selected-cell distance, width, model-height delta, raw-height delta, and signed elevation angle pass the display-frame tolerances. This is interchange and derived-value evidence, not physical calibration, uncertainty, certified metrology, or ZEISS/PolyWorks equivalence. See `docs/OPENVISIONLAB_3D_CLOUDCOMPARE_PARITY_20260713.md`.

Release-candidate revalidation on 2026-07-13 confirmed `0.1.0-rc.1` at commit `ac57687` in Windows Actions run `29198517611`. Build, binary-only Viewer Host, Viewer/Shell screenshot quality, six algorithm/map golden suites, actual C3D PLY roundtrip, independent Python mapping, and artifact upload passed. The uploaded Viewer manifest and schema `1.1` Cross-section Run Record carry the same clean commit and product/Host API identity, and the Viewer/Runner state is `Matched`. GitHub prerelease `v0.1.0-rc.1` publishes the complete Viewer dependency ZIP with SHA-256 `b9a9b6d002f507da63da32934d93bf6e8deaff2d7c1b00ff70a6f36d6b784a83`; this is not a stable, calibrated, or full-application release.

Post-RC development identity changed to `0.1.1-dev` on 2026-07-14 so current-source Viewer manifests and Run Records cannot be confused with the published RC evidence. This is a source/build identity only; no tag, release asset, stable promotion, or Host API change was made.

The same current working tree passed a local pre-push regression on 2026-07-14. `artifacts/local_ci_pre_push_20260714/local_ci_summary.txt` records a zero-warning/error build; an eight-project NuGet audit with zero vulnerable or deprecated packages; Plane `9/9`, Point Pair `9/9`, Gap/Flush `8/8`, Volume `9/9`, Cross-section `9/9`, and C3D Map `10/10` golden cases; five typed Run Record identities and two legacy `Step=null` cases; `Matched` Cross-section Viewer/Runner output; independent .NET/Python map checks; a `0.1.1-dev` BinaryHost with Host API `1.0`; and accepted Viewer, BinaryHost, and Shell schema `1.0`/`1.1`/`1.2` screenshots.

Windows Actions run `29302323300` then passed at commit `e704f6f` with every job step green, including the new LF recipe-byte and recorded-SHA gate. Authenticated artifact `8298975554` is `1,323,767` bytes with digest `sha256:70935ecfb48978cc20abeda446b62fd0ba8d67fb29809a932b122b7a77fa5d00`. Its clean Windows Viewer manifest and Run Record identify product `0.1.1-dev`, Host API `1.0`, manifest schema `1.0`, Run Record schema `1.2`, and commit `e704f6f`; typed Cross-section step identity, Pass status, five metrics, three overlays, and Viewer/Runner `Matched` state pass. The executed recipe's raw SHA-256 is the same LF-stable `f9355976ebd179f20719e20d24736a6f61d8b6711e98bad4b543ced1ae279666` locally and remotely, and the selected local/remote business/evidence payload matches exactly. This is post-RC development evidence, not a packaged release, physical calibration, or metrology certification.

A fresh public-asset acceptance run on 2026-07-13 independently downloaded that ZIP and `SHA256SUMS.txt`, matched the archive hash, and used the BinaryHost verifier to enforce all 13 manifest file paths, sizes, and SHA-256 values before build. The zero-`ProjectReference` Host passed with 12/12 required outputs, C3D render/pick, and an accepted first-attempt screenshot (`blackRatio=0.0045`, `whiteRatio=0.3578`, luminance `0..255`); a 4/4 rejection matrix blocked outside-bundle, missing, wrong-size, and same-size hash-mismatched entries before Host build. This proves package integrity and hostability for the tested Windows/.NET 10 environment, not physical measurement accuracy or broad host compatibility.

Host API v1.0 consumer acceptance on 2026-07-13 passed against both that public RC bundle and a current-source bundle. The zero-`ProjectReference` BinaryHost records a C3D `HostState` snapshot, nonzero `HostStateChanged` events, all three view-command invocations, and a successfully parsed `c3d-height-deviation` recipe saved through `IOpenVisionThreeDViewerHost.SaveRecipe`. A controlled missing-recipe run records `smokeExitCode=1` and now returns process exit code `1`, closing an external-host failure-propagation gap.

Registration-engine research on 2026-07-13 accepted Open3D `DemoICPPointClouds` as a probe-only alignment golden candidate, not calibrated or nominal/actual evidence. An 11-case x 3-run robustness characterization is deterministic but matches only 5 predeclared outcomes: high noise misses the translation limit, partial/combined cases miss strict RMSE limits despite small known-transform errors, a medium initial error converges unexpectedly, and a distant initial error proves that zero correspondences can be reported with RMSE `0`. The recorded non-GUI Open3D `0.19.0` source commit now passes both the recovered build and an independent clean single-shot Release build/install. The clean 873-file, 88,977,375-byte install has the same paths and sizes as the recovered install, 871 identical file hashes, and two rebuilt DLLs with matching export/dependency contracts but different PE timestamps and hashes. Its 58,520,064-byte three-file probe runtime matches the official Windows binary in all 33 robustness runs and all three current `0 -> 1` DemoICP runs. Current hardened evidence remains narrower than the earlier report: both runtimes reject pair `1 -> 2` because `cloud_bin_2.pcd` contains 771 non-finite normals, so its older successful metrics are historical pre-hardening evidence. This proves a reproducible separate-process technical boundary and an explicit correspondence/fitness guard requirement, not product adoption. A schema-valid 52-component CycloneDX candidate records 27 direct and 25 support/transitive components plus exact available archive/license hashes and three Open3D-side modifications. Assimp's fixed-build compiled closure passes at 232/232 source/object mappings, 15/15 bundled-zlib mappings, and a deterministic 48-importer/22-exporter registry. Fixed oneMKL provenance passes exact three-wheel identity and RECORD integrity, 179/179 archive/install payload hashes, 4/4 Release link inputs, and byte-identical two-run semantic reassembly. VTK `9.1.0` source/recipe/payload/transitive closure passes with 1,156/1,156 archive/install files, 14 explicit Release link inputs, 20 reachable targets, 16 static libraries, seven exact child components, and 8/8 source-matched licenses; the documented VS2019 workflow conflicts with `_MSC_VER=1900` in all 30 packaged C++ libraries, so exact binary/toolchain reproducibility remains open. Distribution remains blocked by Assimp vendored snapshot/modification provenance, BoringSSL binary/toolchain reproducibility, final notices, Microsoft VC/OpenMP and clean-host evidence, product integration impact, and owner/legal approval; Viewer/Runner parity remains open. The .NET `PclNET 0.8.3` path remains rejected, and no product dependency, PCD loader, or fixed sample was added. `docs/OPENVISIONLAB_3D_REGISTRATION_ENGINE_PROTOTYPE_20260713.md`, `docs/OPENVISIONLAB_3D_OPEN3D_DISTRIBUTION_AUDIT_20260713.md`, and `docs/OPENVISIONLAB_3D_OPEN3D_SBOM_CANDIDATE_20260713.md` record the decision.

**2026-07-16 VTK correction:** `_MSC_VER=1900` is a VS2015-and-later STL ABI-family marker, not a measurement of the actual compiler. The legacy VTK configuration header records VS2019 MSVC `14.29.30133`, and a current no-patch Release rebuild has now matched the legacy Release package contract and direct link/run smoke. This removes the claimed marker conflict, but it does not establish historical byte identity, Debug reconstruction, Open3D product adoption, or distribution readiness. See `docs/OPENVISIONLAB_3D_VTK_CONTROLLED_REBUILD_20260716.md`.

**2026-07-16 distribution-evidence correction:** the preceding registration summary is historical and is superseded on five points. The CMake-selected official Assimp `v5.4.2` ZIP exactly matches `2,940/2,940` clean-build source files by ordinal path, length, and SHA-256 with no generated update or patch command; fixed original Poly2Tri source content matches the official Mercurial revision to the candidate Git archive in `35/35` raw-byte-identical files, and its current Assimp delta has complete path-level history coverage (`28` official entries, `14/14` direct paths, `1,407/1,328` net line delta); fixed Clipper `6.4.2` release/import/current-tag provenance also passes with archive-to-import `7/6`, import-to-current `4/4`, and current build blob `c0a8565bb98568dcca4a5350ca52fa08152bea51`; fixed stb_image `v2.29` upstream commit `0bc88af...` to current build source passes with shared blob `a632d543510ebf4410f124369b07a303e1d096d6`, while no upstream release/tag is claimed. Other vendored-component upstream revision/modification provenance remains open. The fixed Open3D candidate also has a Windows Sandbox technical clean-host prerequisite pass, so only REDIST/legal treatment rather than clean-host execution evidence remains open. The VTK marker conflict is withdrawn. This does not authorize an Open3D dependency, registration UI, or product workflow: final notices, owner/legal approval, product integration impact, real result mapping, and Viewer/Runner parity remain blocked. See `docs/OPENVISIONLAB_3D_ASSIMP_SOURCE_SNAPSHOT_IDENTITY_20260716.md`, `docs/OPENVISIONLAB_3D_ASSIMP_POLY2TRI_LINEAGE_20260716.md`, `docs/OPENVISIONLAB_3D_ASSIMP_POLY2TRI_DELTA_ATTRIBUTION_20260716.md`, `docs/OPENVISIONLAB_3D_ASSIMP_CLIPPER_PROVENANCE_20260716.md`, `docs/OPENVISIONLAB_3D_ASSIMP_STB_PROVENANCE_20260716.md`, `docs/OPENVISIONLAB_3D_OPEN3D_CLEAN_HOST_EXECUTION_PROTOCOL_20260716.md`, and `docs/OPENVISIONLAB_3D_OPEN3D_DISTRIBUTION_AUDIT_20260713.md`.

The runtime-neutral registration acceptance policy passed locally and in Windows CI on 2026-07-16 without adopting a registration runtime. `RegistrationAcceptanceRule` evaluates correspondence count, fitness, inlier RMSE, rigid homogeneous transform validity, translation, and rotation in fail-closed order; zero-correspondence/RMSE-zero output, malformed or non-finite transforms, scale, and reflection are rejected, and later criteria remain `NotRun` after an earlier failure. The golden verifier passes `20/20`. Commit `13f143a` passed Windows Actions run `29454088343`, job `87483200712`, including mandatory step 15; authenticated artifact `8358732707` is `3,726,847` bytes with digest `sha256:fced1dde391124d89b761336c907957d597b73dfbecbdc9d2dff62f4bf18b9f7`. Follow-up documentation commit `b4ce585` also passed run `29454516121`, job `87484554739`; artifact `8358888081` is `3,727,356` bytes with digest `sha256:9aa87d7503d4ca9f2ec5f2c84af68be926d47c5639ec1b67d44b4c9289089793`. This closes the engine-independent policy gate only. Approved runtime distribution, actual result mapping, and Viewer/Runner registration parity remain open; no product dependency or UI was added.

Windows Actions run `29216983045` passed this Host API consumer gate at commit `95dd8da` together with all Shell/Runner/golden/map checks. Evidence artifact `8266920376` is `1,167,342` bytes with digest `sha256:254145a80071df39f88d4c199372d1c30c64057f6b931062de4c8dfbdc476c16`.

Windows Actions run `29215566528` revalidated the hardened gate at commit `c50d196`. BinaryHost, Shell screenshot quality, Runner/golden/map checks, actual C3D roundtrip, independent Python mapping, and artifact upload all passed. Evidence artifact `8266449434` has digest `sha256:230b5607524e668ed47f59d85e08514bace873e631f676bb44a32282d2eb4c65`.

Windows Actions run `29288595132` revalidated the current Viewer trust baseline at commit `cebdc8f` on 2026-07-14. The existing BinaryHost, Shell screenshot-quality, Runner/golden/map, actual-map, PLY-signature, and artifact gates passed together with the new stride-aligned independent Python point-pair calculation. Evidence artifact `8294167228` is `1,167,597` bytes with digest `sha256:485c6bbcfb0389ed2af2584eb9dfb359365fd95927bcfb3e3b2ccd4342d9b7bc`.

Windows Actions run `29297655730` passed the NuGet package-health gate at commit `6779881` on 2026-07-14. The separate four-case verifier self-test and live audit passed for all eight solution projects with zero vulnerable or deprecated direct/transitive packages, and every existing BinaryHost, Shell screenshot-quality, Runner, golden, map, and upload step remained green. Evidence artifact `8297372590` is `1,168,807` bytes with digest `sha256:66a3a2650a720aa8810ca4a433f73f08d97053122f77750f740455e6b9385fde`; a fresh authenticated download matched the digest and contained both parseable raw JSON responses plus the zero-finding summary.

## Evidence Checked

Local checks performed on 2026-07-11:

- `dotnet build OpenVisionLab.ThreeDStudio.slnx -c Debug`: passed with zero warnings and zero errors.
- Viewer fitted-plane smoke: `artifacts/self_eval_viewer_plane_20260711.png` and `artifacts/self_eval_viewer_plane_20260711.txt`.
- Shell hosted-viewer smoke: `artifacts/self_eval_shell_plane_20260711.png`.
- Runner replay: `artifacts/self_eval_runner_c3d_20260711.txt`; the configured sample intentionally reports `Fail` because peak deviation exceeds tolerance.
- Analytic plane/flatness verification: `artifacts/plane_flatness_golden_after.txt`; exact plane coefficients, signed extrema, flatness, RMS, Pass/Fail thresholds, and six controlled error paths pass.
- Point-pair dimensions evidence: `artifacts/viewer_dimensions_after.*`, `artifacts/viewer_dimensions_reopen_after.*`, `artifacts/runner_point_pair_dimensions_after.txt`, and `artifacts/shell_dimensions_after.png`; the saved source cells replay the same distance, XZ width, signed elevation angle, and status.
- Analytic point-pair verification: `artifacts/point_pair_dimensions_golden_after.txt`; a known `(3,4,4)` vector, signed descending angle, tolerance failure, and six controlled invalid-input paths pass (`9/9`).
- Gap/Flush evidence: `artifacts/viewer_gap_flush_after.*`, `artifacts/viewer_gap_flush_reopen_after.*`, `artifacts/runner_gap_flush_after.txt`, and `artifacts/shell_gap_flush_after.png`; Viewer and Runner match signed gap `1.322` model, signed flush `243.544` raw-height, sample counts, and Pass status.
- Analytic Gap/Flush verification: `artifacts/gap_flush_golden_after.txt`; signed separation/overlap, independent tolerance failures, empty region, non-finite statistics, invalid tolerance, and missing-unit cases pass (`8/8`).
- Volume evidence: `artifacts/viewer_volume_after.*`, `artifacts/viewer_volume_reopen_after.*`, `artifacts/runner_volume_after.txt`, and `artifacts/shell_volume_steps_after.png`; Viewer and Runner match above `0.874`, below `0.972`, signed net `-0.098 model^3`, sample counts, and Pass status.
- Analytic Volume verification: `artifacts/volume_golden_after.txt`; exact above/below/net integration, signed acceptance, insufficient/empty samples, invalid area/tolerance, non-finite measurement, and missing-unit cases pass (`9/9`).
- Cross-section evidence: `artifacts/viewer_cross_section_after.*`, `artifacts/viewer_cross_section_reopen_after.*`, `artifacts/runner_cross_section_after.txt`, and `artifacts/shell_cross_section_steps_after.png`; Viewer and Runner match row `983`, columns `200..1100`, `836` valid samples, width `4.247 model`, raw-height range `1708.232`, and Pass status.
- Analytic Cross-section verification: `artifacts/cross_section_golden_after.txt`; exact width/range, independent tolerance failures, invalid selectors, insufficient/non-finite/out-of-range samples, invalid tolerance, and missing-unit cases pass (`9/9`).
- C3D map fidelity: `artifacts/map_fidelity/c3d_map_fidelity_golden.txt` passes `10/10`; the full-resolution point-only audit roundtrips all 1,653,562 valid points with zero .NET XYZ/RGB error; an independent Python implementation reports maximum coordinate error `2.37e-7` and RGB error `0`; the local PNG identifies the unflipped source orientation; Microsoft 3D Viewer independently renders the same major shape; Open3D 0.19.0 preserves the sampled `66,212` points/RGB within `5e-6`; and CloudCompare 2.13.2 preserves all full-resolution points/RGB within `5.00000001e-7` Viewer units while also passing C2C and selected point-pair metric checks.
- Current data matrix: C3D, GLB, STL, LAS, and LAZ with positive and controlled-failure paths.
- Current architecture: separate Core, Data, Tools, Viewer, Docking.Controls, Shell, Runner, and app-host projects.
- Fixed NIST nominal/actual end-to-end evidence: `artifacts/nominal_actual_publish_20260714` records the Part 1 accepted Viewer/Shell, recipe reopen, `ViewerRunnerComparison|Matched`, and schema `1.2` baseline. `artifacts/nist_part2_visible_20260715` records the Part 2 before/after identity correction, accepted Viewer/Shell, selected point, recipe reopen, Runner `Matched`, schema `1.2` JSON/HTML/CSV, `27/27` executor/result, `71` ViewModel checks, `128/128` fixed matrix, BinaryHost, and current pointer input. `artifacts/pointer_input_regression_20260715` records byte-identical repeated local pointer reports. Windows Actions runs `29378562022` and `29378878976` prove hosted dual-capture and Viewer/Shell pointer input in the current runner image; the second run enforces pointer input as a mandatory gate and preserves authenticated artifact digest `sha256:3179673b1d98406daaebc29bb1c4902e977bc9c49bf23a5d233e6dba5a5d8247`.

Current-source Viewer revalidation performed on 2026-07-12:

- Build: passed with zero warnings and zero errors.
- Fixed matrix: `artifacts/viewer_validation_20260712/matrix_smoke_summary_after.txt` records 129 passes and zero failures.
- C3D interaction evidence: `artifacts/viewer_validation_20260712/c3d_detailed_pick.png`, `c3d_detailed_pick.txt`, `c3d_two_point.png`, and `c3d_two_point.txt`.
- C3D numerical evidence: `c3d_map_golden.txt`, `c3d_map_dotnet.txt`, and `c3d_map_python.txt` in the same artifact folder.
- External runtime evidence: Open3D 0.19.0 preserved all 66,212 sampled vertices and RGB; ASCII re-save maximum coordinate drift was `5e-6`, passing the documented `1e-5` external-writer tolerance.
- Full-resolution external runtime evidence: CloudCompare 2.13.2 preserved all 1,653,562 ordered vertices and RGB at `1e-6`, reported sub-micro-unit C2C statistics, and preserved the fixed recipe point-pair display-frame metrics. Evidence is recorded under `artifacts/map_fidelity_cloudcompare_20260713` and summarized in `docs/OPENVISIONLAB_3D_CLOUDCOMPARE_PARITY_20260713.md`.

The plane/flatness, point-pair-dimensions, Viewer, and Evidence Workbench baseline is published in commit `718792e`. The C3D map-fidelity update is evaluated by the current-build evidence listed above.

**2026-07-16 kuba--zip correction:** Assimp's three compiler inputs now pass a fixed `kuba--/zip v0.3.1` tag through bounded, CRLF-normalized Assimp deltas to the current clean-build source. CMake `0.3.0` metadata is not used as the upstream source claim. This reduces one vendored-component provenance gap only; independent miniz provenance, remaining components, notices, legal approval, distribution, product integration, real result mapping, and Viewer/Runner parity remain open. See `docs/OPENVISIONLAB_3D_ASSIMP_KUBAZIP_PROVENANCE_20260716.md`.

**2026-07-16 pugixml correction:** Assimp's three effective header-only inputs now pass from official `zeux/pugixml v1.13` commit `a0e064336317c9347a91224112af9933598714e9`, through Assimp import `62cefd5b275628ff97a77d0cd9220e1c35794a3f`, to the fixed `v5.4.2` clean-build source. The retained header-only configuration is the only `1/1` source import delta; `pugixml.cpp` and `pugixml.hpp` remain source-identical at import, and the sole post-import change is a `2/2` copyright update per file. The vendored CMake `VERSION 1.9` value is not the source version. This reduces one bounded vendored provenance gap only; remaining components, notices, legal approval, distribution, product integration, real result mapping, and Viewer/Runner parity remain open. See `docs/OPENVISIONLAB_3D_ASSIMP_PUGIXML_PROVENANCE_20260716.md`.

**2026-07-16 UTF8-CPP correction:** Assimp's four compiler-read headers now pass from official `nemtrif/utfcpp v3.2.3` commit `79835a5fa57271f07a90ed36123e30ae9741178e`, through update `ce59d49dd9ce93ccf8585f78c70e58cb0e5d4961`, to the fixed `v5.4.2` clean-build source. The official tag, update, current tag, and build all have identical header blobs, and fixed-range post-update path history is empty. CMake, the `utf8.h` include chain, and the four-file compiler-read closure are checked. This reduces one four-header provenance gap only; optional headers, remaining components, notices, legal approval, distribution, product integration, real result mapping, and Viewer/Runner parity remain open. See `docs/OPENVISIONLAB_3D_ASSIMP_UTF8CPP_PROVENANCE_20260716.md`.

**2026-07-16 MiniZip correction:** Assimp's four compiler-read MiniZip files now pass from official `madler/zlib v1.3.1` commit `51b7f2abdade71cd9bb0e7a373ef2610ec6f9daf`, through update `64d88276ef7117c09165e468dbb9acd999e324ac`, to the fixed `v5.4.2` clean-build source. The official tag, update, current tag, and build all have identical blobs for `ioapi.c`, `ioapi.h`, `unzip.c`, and `unzip.h`, and fixed-range post-update history is empty. CMake lists `crypt.h`, but source-defined `NOUNCRYPT` prevents its conditional inclusion in this build, so it remains outside the four-file compiler-read closure. This reduces one bounded zlib-contrib provenance gap only; complete MiniZip/Info-ZIP/`crypt.h` provenance, remaining components, notices, legal approval, distribution, product integration, real result mapping, and Viewer/Runner parity remain open. See `docs/OPENVISIONLAB_3D_ASSIMP_MINIZIP_PROVENANCE_20260716.md`.

**2026-07-16 miniz correction:** Assimp's one compiler-read miniz header now passes a bounded public `kuba--/zip v0.3.1` to Assimp/current-build chain. The fixed tag, PR baseline, PR `2/0` and `4/1` changes, merge, post-merge `1/1` change, `v5.4.2`, and build input are tied by exact blobs and closure evidence. This reduces one bounded vendored provenance gap only; independent original `richgel999/miniz` source identity/history, other components, notices, legal approval, distribution, product integration, real result mapping, and Viewer/Runner parity remain open. See `docs/OPENVISIONLAB_3D_ASSIMP_MINIZ_PROVENANCE_20260716.md`.

**2026-07-16 OpenDDL Parser correction:** Assimp's `13` compiler-read OpenDDL Parser inputs now pass from public `kimkulling/openddl-parser v0.5.1` through the exact Assimp baseline, two bounded Assimp changes, current `v5.4.2`, and the clean build. The static `0.4.0` source string is shared by the upstream tag and is not used as source identity. This reduces one bounded vendored provenance gap only; remaining components, notices, legal approval, distribution, product integration, real result mapping, and Viewer/Runner parity remain open. See `docs/OPENVISIONLAB_3D_ASSIMP_OPENDDL_PROVENANCE_20260716.md`.

**2026-07-16 zlib correction:** Assimp's `25` compiler-read zlib core inputs now pass from public `madler/zlib v1.2.13`, through the exact Assimp update, current `v5.4.2`, and the clean build. All source blobs match and every checked source path has no post-import change. The build-generated `zconf.h` is recorded but remains outside this upstream source-identity subset. This reduces one bounded vendored provenance gap only; remaining components, notices, legal approval, distribution, product integration, real result mapping, and Viewer/Runner parity remain open. See `docs/OPENVISIONLAB_3D_ASSIMP_ZLIB_PROVENANCE_20260716.md`.

**2026-07-16 RapidJSON correction:** Assimp's `29` compiler-read headers now pass from public post-`v1.1.0` RapidJSON snapshot `676d99...`, through the exact Assimp update, current `v5.4.2`, and the clean build. The public `v1.1.0` tag is an ancestor rather than the source identity. All header blobs match and every checked path has no post-import change. This reduces one bounded vendored provenance gap only; remaining components, notices, legal approval, distribution, product integration, real result mapping, and Viewer/Runner parity remain open. See `docs/OPENVISIONLAB_3D_ASSIMP_RAPIDJSON_PROVENANCE_20260716.md`.

**2026-07-16 Open3DGC correction:** Assimp's `29` compiler-read Open3DGC files now pass from public `KhronosGroup/glTF` `mesh-compression-open3dgc` snapshot `7b61d5e...`, through import `054820e6...`, the exact `v5.4.2` tag, and the clean build. The fixed current Assimp delta has exactly `16` paths. The core's MIT notice and the arithmetic-codec BSD-2-Clause notices are both recorded. This reduces one bounded carrier-snapshot/import/delta provenance gap only; historical AMD remote availability, final notices, legal approval, distribution, product integration, real result mapping, and Viewer/Runner parity remain open. See `docs/OPENVISIONLAB_3D_ASSIMP_OPEN3DGC_PROVENANCE_20260716.md`.

**2026-07-16 Assimp closure notice-manifest checkpoint:** a fixed source-backed candidate now cross-checks Assimp core and all `12` compiler-read closure components against the archive-to-build snapshot, CycloneDX evidence, and current source hashes. It records `13` entries, `125` compiler-read paths, and `15` separate source notice records, with a repeatable contract SHA-256 `ce51a50d...`; a wrong archive identity fails closed. This is explicitly a candidate manifest, not a final notice bundle, legal approval, distribution approval, product integration, or a Viewer/Runner behavior change. See `docs/OPENVISIONLAB_3D_ASSIMP_CLOSURE_NOTICE_MANIFEST_20260716.md`.

**2026-07-16 miniz original-origin boundary:** the remaining original-source question is now bounded rather than silently inferred. A full current official `richgel999/miniz` reachable-object audit finds neither the fixed Kuba nor clean-build raw blob and records `OriginIdentityStatus=Unresolved`; it does not assert that no historical derivation exists. The current build header has both observed Unlicense and MIT text, supporting the existing candidate source-text expression only. This reduces ambiguity in the notice input but does not complete notices, legal review, distribution, product integration, real result mapping, or Viewer/Runner parity. See `docs/OPENVISIONLAB_3D_ASSIMP_MINIZ_ORIGIN_BOUNDARY_20260716.md`.

## Commercial Product Findings

Official product material was checked on 2026-07-11.

| Product | Commercial pattern | Direction for OpenVisionLab 3D |
| --- | --- | --- |
| ZEISS INSPECT | Parametric inspection steps are traceable, repeatable, editable, dependency-aware, and reusable as templates. It also connects nominal/actual color maps, GD&T, reporting, and trend analysis. | Make the inspection plan and its dependencies first-class. A result must explain which source, reference, ROI, parameters, and earlier step produced it. |
| PolyWorks Inspector | Uses reusable inspection projects, explicit sequences, feature/datum/best-fit alignment, measured-to-nominal color maps, dimensional controls, multipiece review, and certified math. | Treat coordinate/reference definition as part of the recipe, show real ordered steps, and keep Viewer/Runner results deterministic. Do not claim metrology-grade accuracy without validation. |
| Geomagic Control X | Focuses on scan-to-CAD or scan-to-scan comparison, visual scripting, repeated automated inspection, annotations, dimensions, and understandable reports. | Add nominal/actual comparison only after basic references and recipe steps are stable. Preserve learned inspection intent in recipes rather than UI-only state. |
| LMI Gocator | Chains masks/ROIs into surface tools such as plane, flatness, dimensions, holes, volume, gap, and flush, then applies thresholds for decisions. | Build small, inspectable ROI-based tools with explicit inputs, metrics, overlays, and tolerances. `Reference Plane + Flatness` is the correct first complete surface tool. |
| Cognex VisionPro 3D | Provides reference-plane height, volume, cross-section, alignment, and graphical application flow for industrial 3D data. | Prioritize reference-relative height, volume, cross-section, and ordered tool flow before broad CAD or AI features. |

The common commercial lesson is not the number of tools. It is the complete chain:

```text
reference -> alignment -> tool input -> measurement -> tolerance -> visual evidence -> replay -> report
```

## Target Position

### Target Users

- Vision and automation engineers developing offline 3D inspection recipes.
- Quality engineers reviewing 3D measurements and evidence without needing a full CAD metrology suite.
- Developers extending transparent rule-based tools and validating Viewer/Runner parity.

### Product Differentiators

- Local-first and sensor-neutral for imported height maps, point clouds, and meshes.
- A separately reusable SharpGL Viewer with inspection facts visible inside the Viewer itself.
- Explicit source/result separation and explicit Preview/Publish behavior.
- Human-readable evidence contracts plus a headless Runner for deterministic replay.
- Inspectable rule algorithms and small end-to-end tool slices instead of hidden automatic tuning.
- Future LLM assistance may draft recipe steps only after the recipe schema and validators are stable; it must never bypass validation or explicit Preview/Run/Publish.

### Explicit Non-Goals For The Current Product Phase

- Full CAD kernel, broad STEP/IGES/PMI import, or standards-complete GD&T.
- Scanner/camera acquisition, robot programming, PLC/I/O, production HMI, or line control.
- Enterprise data lake, cloud collaboration, account management, or plant-wide SPC platform.
- AI defect training or automatic recipe tuning before rule-based evidence is trustworthy.
- Claims of calibrated or certified metrology accuracy without units, calibration, uncertainty, and algorithm-validation evidence.

## Capability Scorecard

Scale: `0` absent, `1` prototype, `2` working MVP, `3` operational baseline, `4` commercial-mature. Scores are directional and are not combined into a marketing percentage.

| Capability | Current | Evidence | Main gap |
| --- | ---: | --- | --- |
| Data loading and 3D display | 3 | C3D, GLB, STL, LAS/LAZ fixed matrix; render density and controlled loader failures. | Clip/crop workflow, broader formats, and out-of-core scale are not yet operational. |
| Camera, picking, selection, overlays | 3 | Orbit/pan/zoom/fit, point/mesh picks, ROI/section, measurement and result overlays, Viewer HUD, and mandatory Windows CI click/orbit/pan/zoom regression in Viewer and Shell. | Arbitrary Windows desktop/session configurations and broader interaction scenarios remain unverified. |
| Reference and alignment | 2 | Transform state, translation-only Align From ROI, fitted C3D height-field plane, and numeric recipe-owned reference ROI. | No interactive plane ROI, 3-point frame, plane-derived rotation, 3-2-1, or best-fit. |
| Measurement toolbox | 2 | Two-point, height delta, ROI step, section/profile, height map, fitted-plane distance, ROI-reference flatness, explicit-cell dimensions, Gap/Flush, Volume, Cross-section, and two fixed full-query actual-to-nominal surface-deviation instances. | Automatic feature-based dimensions, area, physical/calibrated volume, broader nominal comparison, and edge-detected gap remain incomplete. |
| Recipe and inspection-step model | 2 | Typed flatness, point-pair-dimensions, Gap/Flush, Volume, Cross-section, and nominal/actual slices with stable step/source/reference IDs, save/reopen, explicit Preview/Publish, Runner replay, and Shell evidence. | The slices use tool-specific recipe families; there is no proven multi-step dependency executor. |
| Runner and evidence parity | 2 | Headless replay, contract comparison, screenshots, result layers, Shell history/snapshot views, schema `1.2` JSON/HTML/CSV, two fixed NIST actual/nominal/query identities, and `Matched` output. | Both pairs share one nominal design and XCT family; batch and general ordered multi-step replay remain unproven. |
| Nominal/actual comparison | 2 | Two fixed NIST identities, source-provided frame/alignment, exact point-to-triangle deviation, robust signs, zero-centred color map, tolerances, Preview/Publish, recipe roundtrip, Runner parity, supplied-transform evidence, and locally/Windows-CI-passed difficult-geometry goldens. | Registration recovery, arbitrary sensors/geometry, uncertainty, and metrology remain open. |
| Reporting and multipiece review | 2 | Runner TXT, per-run JSON, human-readable HTML metric table, CSV metric export, and Shell artifact commands. | No PDF, database, retention/signing, multi-piece table, trends, statistics, or SPC. |
| Calibration and repeatability | 1 | Accepted Calibration Center View/ViewModel baseline, LiveCharts2 Run Chart linked to the values grid, offline typed representative Model `34/34`, source-identity Study loader `13/13`, ViewModel workflow `55/55`, typed aligned-point Model/Tool `33/33`, and closed-schema aligned-source Study/Mapping loader `16/16`. | Only synthetic evidence is available. No real repeated 3D acquisitions, trusted raw-source-to-mapping derivation, linked 3D point selection, active profile, physical calibration, uncertainty, or Gauge R&R. |
| Metrology assurance | 1 | Deterministic smoke values, explicit raw/model units in selected paths, analytic plane/flatness, point-pair, Gap/Flush, Volume, Cross-section, and repeatability golden suites, plus a C3D display-frame golden/neutral-PLY roundtrip baseline. | Formal physical mapping contract, calibration provenance, uncertainty, calibrated external datasets, licensed metrology comparison, feature-fitting validation, and broader independent algorithm validation. |
| Architecture and maintainability | 2 | Separate Viewer/Shell/Core/Data/Tools/Runner boundaries; MVVM direction; CI build. | Viewer code-behind remains large, recipe logic is tool-specific, and automated unit/integration tests are limited. |

## Gate Decision

### Viewer Foundation v1: Passed

The current fixed matrix demonstrates the contracts originally required for the viewer gate:

- reliable display of representative height-grid, mesh, and point-cloud samples;
- camera control and fit behavior;
- object/layer visibility;
- picking and selection;
- measurement and result overlays;
- color modes and legends;
- standalone Viewer and docked Shell hosting;
- screenshot and contract smoke evidence;
- controlled loader failures.

Future viewer changes must preserve this baseline, but routine development should no longer add viewer-only features without an inspection workflow need.

The 2026-07-12 current-source revalidation closes this fixed-scope Viewer gate. It does not close physical calibration, out-of-core scale, gesture automation, or independent commercial metrology validation.

### Inspection Recipe v1: Current Gate

The next release target is one complete reusable inspection plan, not another isolated smoke-only measurement.

Required acceptance scenario:

1. Load the C3D sample.
2. Define a reference plane from an operator-selected ROI or three valid points.
3. Add a flatness/deviation step with explicit units and tolerance.
4. Preview without mutating the source entity.
5. Show plane normal, sample count, RMS, min/max signed deviation, flatness, status, and deviation overlay.
6. Publish a separate result entity/layer explicitly.
7. Save the reference and tool as recipe steps with stable IDs and input references.
8. Reopen the recipe and reproduce the same Viewer result.
9. Run the same recipe headlessly and match metrics/status against the Viewer contract.
10. Show the actual ordered steps and the resulting run record in Shell.

Inspection Recipe v1 passes only when all ten items have current build and smoke evidence.

Status on 2026-07-11: the baseline passes for `recipes/c3d-plane-flatness.recipe.json` using a numeric operator-configured reference ROI. Current evidence is `artifacts/viewer_flatness_after.*`, `artifacts/viewer_flatness_reopen_after.*`, `artifacts/runner_flatness_after.txt`, and `artifacts/shell_flatness_after.png`. This does not validate calibrated accuracy or a general multi-step graph.

Algorithm hardening status: `artifacts/plane_flatness_golden_after.txt` passes an analytic plane with known signed offsets and controlled invalid-reference/input cases. This validates the current plane/flatness mathematics against known answers, but not calibration, uncertainty, or external metrology software.

Second typed-slice status on 2026-07-11: `recipes/c3d-point-pair-dimensions.recipe.json` passes explicit Preview/Publish, source-cell recipe save/reopen, Viewer/Runner parity, Shell step evidence, and render-density-independent source-cell resolution. `artifacts/point_pair_dimensions_golden_after.txt` passes `9/9` known-answer and controlled-error cases. This measures two selected C3D cells; it does not perform edge detection, line/circle fitting, CAD dimensions, or GD&T.

Third typed-slice status on 2026-07-12: `recipes/c3d-gap-flush.recipe.json` passes explicit Preview/Publish, two-region recipe save/reopen, Viewer/Runner parity, Shell step evidence, and a fixed 140,000-point measurement budget independent from display density. `artifacts/gap_flush_golden_after.txt` passes `8/8` signed known-answer and controlled-error cases. Gap is the signed aligned-X distance between facing ROI edges; Flush is right-minus-left mean raw height. These remain unitless/raw-height results, not calibrated physical seam measurements.

Fourth typed-slice status on 2026-07-12: `recipes/c3d-volume.recipe.json` passes explicit Preview/Publish, reference-plane and measurement-ROI recipe save/reopen, Viewer/Runner parity, Shell step evidence, and `9/9` analytic/error golden cases. Its above/below/net values remain uncalibrated display-frame `model^3`, not physical volume.

Fifth typed-slice status on 2026-07-12: `recipes/c3d-cross-section-dimensions.recipe.json` passes explicit Preview/Publish, exact source-row/column-range recipe save/reopen, Viewer/Runner parity, Shell step evidence, and `9/9` analytic/error golden cases. It does not perform automatic feature finding or calibrated physical dimensioning.

## Development Priorities

### P0: C3D Map Fidelity - Display Baseline Done, Physical Profile Next

- Preserve the passed source-grid orientation, mapping golden cases, and neutral PLY coordinate/color roundtrip.
- Obtain X/Z pitch, height scale/offset, source/display units, axis directions, and calibration identity from the C3D producer or official format contract.
- Store those values in an explicit mapping profile. Keep the current normalization as a named uncalibrated display profile and never silently relabel it as physical units.
- Repeat the same neutral-file bounds/coordinate comparison in an independent metrology tool before making accuracy claims.

### P0: Reference Plane + Flatness End-To-End Slice - Baseline Done

- View: reference mode/ROI or three-point selection, flatness parameters, and visible step placement.
- ViewModel: commands, selection validation, tolerance state, metric/result state, and step summary.
- Model/Tools: fitted reference result and flatness evaluation using the smallest shared step shape required by this tool.
- Evidence: overlay, result layer, recipe save/reopen, Runner parity, Viewer/Shell screenshots, and contract checks.
- Do not build a speculative workflow engine first; let this first complete step define the minimum reusable contract.

### P1: Real Inspection Plan In Shell - Single-Step Evidence Baseline Done

- Actual flatness and point-pair recipe step rows show enabled state, source/reference inputs, status, and Viewer/Runner evidence.
- Multi-step order, dependencies, blocked-step state, and one combined recipe remain unproven.
- Keep Preview, Publish, Save, and Run explicit commands.

### P1: Basic Surface Measurement Set

Add one complete tool at a time in this order:

1. Flatness and signed deviation to selected plane. Baseline done for a numeric reference ROI.
2. Explicit-cell width/distance/signed elevation angle. Baseline done; automatic feature extraction remains out of scope.
3. Gap/flush or two-region step height. Explicit-region baseline done.
4. Volume above/below a reference plane. Explicit height-field ROI baseline done; physical calibration remains blocked.
5. Cross-section dimensions. Exact source-row/range baseline done; automatic feature finding remains out of scope.

Each tool requires Viewer/Shell UI, metrics, overlay, tolerance, recipe persistence, Runner replay, and evidence before the next tool starts.

### P2: Nominal/Actual Inspection v1 - Fixed Baseline Done

- Distinguish measured and nominal entities.
- Add explicit alignment strategy and transform evidence.
- Implement measured-to-nominal point/mesh deviation and a signed color map.
- Start with one local mesh/point-cloud pair; do not add a CAD kernel first.

End-to-end status on 2026-07-16: both NIST Overhang X4 Part 1 and Part 2 have verified CloudCompare signed/unsigned C2M references in the documented 3-2-1 part frame. For both physical instances, source identity, fixed-frame algorithm parity, generic View/ViewModel/shared execution, real signed-color Preview, separate Publish result, typed recipe save/reopen, Runner replay, schema `1.2` Run Record, and current Viewer/Shell evidence pass. Stanford Drill separately passes application of its published non-identity transform at point and aggregate level. The difficult-geometry controlled-outcome matrix and mandatory workflow gate pass locally and in Windows CI. The runtime-neutral registration acceptance policy passes `20/20` locally and in Windows CI; the remaining Phase 2 checklist item is an approved runtime/result adapter with Viewer/Runner registration parity. Do not generalize these fixed-source results to arbitrary registration or metrology.

Supply-chain checkpoint on 2026-07-16: the controlled VTK candidate now supports a same-source Open3D `0.19.0` `USE_SYSTEM_VTK=ON` Release build. Its candidate-to-clean install, DLL dependency/export, DemoICP, controlled-failure, and 33-run robustness checks pass locally. This reduces runtime compatibility uncertainty only; distribution approval, clean-host prerequisites, real result mapping, Viewer/Runner registration parity, and physical/metrology claims remain open.

### P2: Durable Run Record And Report

- Define a serializable run record containing recipe identity, source identity/hash, time, status, metrics, artifact paths, and Viewer/Runner match state.
- Generate simple JSON plus HTML or CSV before considering PDF or enterprise reporting.
- Add batch/trend views only after multiple real runs use the same stable record.

Status on 2026-07-12: schema `1.1` baseline done for one real Cross-section run. `artifacts/run_record_identity_20260712` preserves the matched Pass result plus product/Host API versions, Git commit/tree state, .NET runtime, OS, and architecture in JSON/HTML/CSV; current Shell remains compatible with schema `1.0`. Broader multi-run reporting remains deferred.

Repeatability status on 2026-07-14: authenticated artifacts from Windows Actions runs `29297655730` and `29297867087` contain independent Cross-section records for the same recipe/source. After excluding only Run ID, UTC time, elapsed milliseconds, and Git commit, the complete remaining JSON payload is identical with SHA-256 `59ab1baf854ef23da98bdf7e977a3fd69d9675e81f0efedb99dbc7f5be1cd2d8`; both records have Pass status, five identical metrics, three identical overlays, and `Matched` Viewer/Runner state. This strengthens deterministic same-source replay but is not multi-piece or batch evidence. Those schema `1.1` records identify the legacy `c3d-height-deviation` document family through `RecipeType` and the executed Cross-section tool through `ToolName`; schema `1.2` closes that historical ambiguity with the first-class `Step` object described below.

Step-identity status on 2026-07-14: schema `1.2` adds an optional first-class `Step` object while preserving older-minor deserialization. The fixed Cross-section run records stable step `step.c3d-cross-section-dimensions`, source `source.c3d-thickness`, and reference `reference.c3d-row-range` consistently in JSON, HTML, and CSV under `artifacts/run_record_step_identity_20260714`; status remains Pass and Viewer/Runner remains Matched. Plane Flatness, Volume, Point Pair, and Gap/Flush use their existing validated recipe IDs. Legacy Height Deviation and LAZ recipe `1.0` documents have no stable step ID, so their records leave `Step` empty rather than inventing identity. This closes the single-step traceability gap but does not prove a general ordered multi-step executor.

Viewer deployment status on 2026-07-12: binary boundary proven locally and in Windows Actions run `29195744796`. `samples/OpenVisionLab.ThreeD.Viewer.BinaryHost` has no project reference, builds from the published Viewer bundle, and its generated EXE directly passes C3D render/pick smoke with all required runtime dependencies; the CI evidence artifact was uploaded successfully.

Shell evidence status on 2026-07-12: local full Shell C3D capture was accepted on attempt 1 with black ratio `0.0609`, white ratio `0.6215`, luminance `0..255`, and `1,024,000` sampled pixels. Windows Actions run `29196380343` passed the Shell quality and release identity steps and uploaded the expanded CI evidence artifact.

### P3: Metrology Credibility

- Make model/display/source units and conversions explicit in every measurement path.
- Preserve the passed synthetic golden baselines for plane/flatness (`9/9`), point-pair distance/angle (`9/9`), Gap/Flush (`8/8`), Volume (`9/9`), Cross-section (`9/9`), and C3D display mapping (`10/10`). The next credibility gap is physical mapping/calibration provenance, uncertainty, calibrated external datasets, and independent feature-fitting/metrology validation.
- Record algorithm version, sample policy, calibration provenance, and uncertainty assumptions.
- Do not use terms such as certified, calibrated, or metrology-grade until independently justified.

## Engineering Direction

- Preserve the Viewer as a separate SharpGL library and preserve the docking/WPF-UI ownership boundaries.
- For visible work, follow View -> ViewModel -> Model. View contains bindings, commands, behaviors, and converters; code-behind remains only the UI/OpenGL/OS bridge.
- Build vertical inspection slices. A new tool is incomplete without parameters, validation, metrics, overlay, tolerance status, recipe persistence, Runner parity, and evidence.
- Keep source geometry immutable. Preview and published result geometry remain separate.
- Use stable step IDs and explicit entity/reference inputs. Never depend on display names or implicit active-selection state during replay.
- Keep measurement sampling independent from render density.
- Keep source-grid fidelity, Viewer display-frame fidelity, and calibrated physical fidelity as separate gates. Screenshots support numerical evidence but do not replace it.
- Make invalid references, insufficient points, unit mismatch, degenerate fits, and missing inputs controlled result states rather than unhandled exceptions.
- Prefer known synthetic truth before adding another public sample or external geometry dependency.
- Update this document and `AGENTS.md` when a gate passes or the product target changes.

## Official Sources Checked

- ZEISS INSPECT parametric concept: https://www.zeiss.com/metrology/en/software/zeiss-inspect/features/parametrics.html
- ZEISS INSPECT Optical 3D: https://www.zeiss.com/metrology/en/software/zeiss-inspect/zeiss-inspect-optical-3d.html
- PolyWorks Inspector: https://www.polyworks.com/en-us/products/polyworks-inspector
- Geomagic Control X: https://hexagon.com/products/geomagic-control-x
- Geomagic Control X automated inspection: https://hexagon.com/products/geomagic-control-x/automated-inspection
- LMI Gocator emulator scenarios and built-in tools: https://lmi3d.com/testing-purpose/
- LMI Surface Mask and Flatness workflow: https://lmi3d.com/blog/introducing-surface-masking/
- Cognex VisionPro 3D-A5000 tools: https://www.cognex.com/products/machine-vision/3d-machine-vision-systems/3d-a5000-series-area-scan/software
- CloudCompare cloud-to-cloud distance: https://www.cloudcompare.org/doc/wiki/index.php?title=Cloud-to-Cloud_Distance
- Open3D geometry file I/O: https://www.open3d.org/docs/latest/tutorial/geometry/file_io.html
