# OpenVisionLab 3D Line Fit Typed Adapter v1 Design

Updated: 2026-07-19

Status: **Complete - implemented and locally verified typed adapter**

Implementation approval: **Approved by owner on 2026-07-19 (all nine decisions)**

## Work Contract

### User goal

Implement the approved typed inspection step that consumes the published
`EdgePointSet`, fits a repeatable full-XYZ line feature, and gives the operator
clear Workbench and Viewer evidence without adding an implicit upstream run or
a physical-metrology claim.

### Non-negotiable requirements

- Keep the user's intended chain explicit:
  `Filter -> Height Difference Edge -> 3D Line Fit -> Line Intersection`.
- Fit the complete numeric XYZ point, not a height-map XY projection.
- Preserve explicit Preview, Cancel, stale invalidation, and Publish without
  silently running the upstream Edge step.
- Use the existing docked Workbench and Viewer instead of creating a separate
  modal tool window.
- Keep the result local, sensor-neutral, deterministic, and replayable by the
  headless Runner through the same Tools rule.
- Do not claim calibrated distance, physical line accuracy, GD&T, or metrology.
- Do not modify the four real Line Fit template rows until the owner approves
  this contract and later teaches their numeric limits.

### Included scope

- one current Published `C3DHeightDifferenceEdgePointSet` input;
- one full-XYZ robust line feature output;
- deterministic candidate selection and final orthogonal refit;
- explicit inlier quality gates and diagnostics;
- compact Properties UI, Viewer overlays, and a dockable residual chart;
- strict recipe shape, ownership, failure rules, and Golden/UI gates.

### Excluded scope

- Line Intersection and closest-approach policy;
- automatic residual selection or inferred production defaults;
- recalculating Filter or Edge from Line Fit;
- physical axis pitch, height scale/offset, calibration, or uncertainty;
- generic point-cloud/mesh line fitting;
- correspondence, affine solving, re-grid, Thickness, or Warpage execution.

### Completion checkpoint

The approved Line Fit v1 implementation is complete locally. Its 9/9 numerical
Golden suite, 14/14 Workbench state/parity suite, 17/17 docking suite, and a
current-build Warpage C3D Preview capture all passed. The actual-source smoke
uses an in-memory maximum residual of `100` only to make the review chart
legible; it is never written to the teaching template. See
`docs/OPENVISIONLAB_3D_LINE_FIT_TYPED_ADAPTER_20260719.md` for commands and
claim boundaries. Design Line Intersection separately before implementation.

### Verification plan after approval

- analytic and controlled-outlier Golden cases;
- Workbench state and Viewer/Runner hash parity;
- current-source before/after UI captures at `1280 x 760`;
- pointer, docking, teaching, Filter, Edge, Profile, BinaryHost, and Shell
  regressions.

### Known risks and blockers

- Current coordinates are `X=column`, `Y=raw-height`, `Z=row`. They are a
  useful source numeric frame but not one calibrated physical unit.
- Four real search bands and their Line Fit limits have not been taught by the
  owner. Synthetic values cannot be promoted into the recipe.
- A future 3D Line Intersection must define closest approach because two 3D
  lines can be skew rather than mathematically intersecting.

## Why Line Fit Is the Next Typed Slice

The completed upstream contract is:

```text
Published FilteredHeightField
  + recipe-owned GridRectangle
  -> explicit Height Difference Edge Preview
  -> ordered EdgePointSet
  -> explicit Publish
```

Line Fit must consume that exact immutable `EdgePointSet`. It does not capture
another ROI and it does not re-read the C3D file. The output is a named
`LineFeature` for the later Line Intersection step.

The 2D OpenVisionLab reference reinforces three useful UI rules without
supplying the 3D numeric algorithm:

- line parameters remain grouped and visible in the PropertyGrid/tool panel;
- the detected points, fitted line, and result summary are reviewed together;
- changing parameters requires another explicit Preview or Run Review.

Commercial 3D workbench research already recorded in this repository adds the
same product lesson: a feature tool is useful only when its input, overlay,
quality evidence, and downstream identity are visible in one repeatable step.

## UI-First Design

### Default Workbench layout

No new modal dialog or fixed pane is introduced. The existing dockable layout
remains:

```text
+----------------------+--------------------------------+----------------------+
| Project Explorer     | 3D View                        | Properties           |
|                      |                                | Step: 3D Line Fit    |
| ordered recipe rows  | points + line + selected      | input / fit / output |
|                      | residual evidence              |                      |
+----------------------+--------------------------------+----------------------+
| Fit Diagnostics (dockable, context-visible after Line Fit Preview)          |
| residual by scanline | threshold | linked 3D point selection                |
+-----------------------------------------------------------------------------+
```

`Fit Diagnostics` joins the existing lower AvalonDock document group. It may
be hidden, floated, resized, or docked by the user. It is not a permanent
seventh panel forced into every workspace and it does not replace the existing
Height Profile view.

### Properties card

Selecting one authored `3D Line Fit` row shows a compact typed card. The
generic free-text parameter expander is hidden for this typed step.

```text
Step 03: 3D Line Fit
----------------------------------------------------------------
Input
  Edge points          derived.edgepoints-ul-horizontal.01
  Upstream state       Published | current
  Available points     135
  Coordinate frame     column / raw-height / row (uncalibrated)

Fit rule
  Method               Deterministic consensus + orthogonal TLS
  Maximum residual     [ explicit source-coordinate value ]
  Minimum inliers      [ explicit integer >= 3 ]
  Minimum ratio        [ explicit 0 < value <= 1 ]
  Minimum support span [ explicit grid-index span >= 2 ]

Fixed v1 policy
  Hypotheses           SHA-256 pair schedule, maximum 256
  Refit                Orthogonal TLS until stable, maximum 10
  Direction            Positive source scanline axis
  Segment              Inlier projection extents

Preview evidence
  State                Ready | Preview running | Preview ready | ...
  Inliers              128 / 135 (94.8%)
  Residual RMS / max   <source numeric values>
  Support span         <scanline count>
  Output               derived.line-ul-horizontal.01
  Content SHA-256      <canonical hash after Preview>
```

The numbers above illustrate layout only. They are not expected values for the
Warpage source and are never written to the current teaching template.

### Viewer overlay

Before Preview, the Viewer shows the already Published Edge points only. It
does not guess a fitted line.

After explicit Preview:

- inliers use compact teal markers;
- outliers use amber markers and remain visible as review evidence;
- the fitted inlier segment is a solid teal line;
- a small arrow shows the canonical positive direction;
- no extrapolated infinite line is drawn in v1;
- selecting a point shows its scanline, XYZ, orthogonal residual, projected
  point, and inlier/outlier state;
- only the selected point receives a residual connector, avoiding a dense
  forest of residual lines; and
- the HUD says `Line Fit Preview - not published` until Publish.

The existing compact top `View` menu and short-right-click canvas menu receive
one identical `Line fit overlays` submenu:

```text
Line fit overlays
  [x] Inlier points
  [x] Outlier points
  [x] Fitted segment
  [x] Selected residual
  [ ] Fit Diagnostics
```

These are display-only toggles. They never mutate recipe parameters, mark a
Preview stale, or execute the tool. Right-button drag continues to pan and a
short right click continues to open the menu.

### Dockable Fit Diagnostics

The bottom chart is a review surface, not another algorithm:

```text
Orthogonal residual
  ^              amber outlier
  |  ---------------- Maximum residual ----------------
  |      teal inliers       x
  +----------------------------------------------------> scanline index

Selected: scanline 332 | residual 0.42 | inlier | XYZ (...)
```

- horizontal axis: original ordered `ScanlineIndex`;
- vertical axis: non-negative full-XYZ orthogonal residual in source numeric
  coordinates;
- horizontal limit: the explicitly taught `MaximumOrthogonalResidual`;
- teal/amber colors match the Viewer;
- chart selection and Viewer selection use the same point index;
- hiding, docking, floating, or selecting chart points changes presentation
  state only; and
- stale or cleared Preview retains no apparently current chart result.

This chart reuses the existing application chart capability; no new charting
dependency is justified.

## Coordinate and Meaning Boundary

The upstream `EdgePointSet` explicitly stores:

```text
numeric X = grid column
numeric Y = raw height
numeric Z = grid row
```

Line Fit v1 uses all three components and full-XYZ point-to-line distance. It
does not drop Y and does not fit only the XZ footprint.

However, column, raw height, and row are not proven to share one physical
scale. Therefore:

- the UI calls the residual a **source-coordinate residual**;
- the output preserves `frameId`, `rootSourceSha256`, and the explicit
  coordinate convention;
- no `mm`, `degree`, calibrated length, or uncertainty label is shown;
- the later affine chain may map this source frame only after separately
  approved correspondence evidence exists; and
- physical accuracy remains blocked until pitch, height scale/offset, axis
  orientation, calibration identity, and validation evidence exist.

## Recommended Numerical Contract

### Alternatives considered

| Alternative | Decision | Reason |
| --- | --- | --- |
| Plain ordinary or orthogonal least squares over all points | Reject for v1 | One wrong strongest-edge point can pull the complete line. |
| Non-deterministic RANSAC | Reject | Random trial order conflicts with canonical output/hash evidence. |
| Huber/Tukey IRLS only | Defer | It is deterministic but its tuning and binary inlier meaning are less obvious to an operator. |
| Deterministic consensus followed by orthogonal TLS | **Recommend** | Keeps a familiar explicit residual gate, rejects isolated points, and produces one full-XYZ line deterministically. |

### Parameters the operator teaches

| Parameter | Rule | Purpose |
| --- | --- | --- |
| `MaximumOrthogonalResidual` | finite `> 0`, inclusive | Defines which full-XYZ points support one candidate line. |
| `MinimumInlierCount` | integer `>= 3` and `<= input count` | Prevents a two-point hypothesis from becoming accepted evidence. |
| `MinimumInlierRatio` | finite `0 < value <= 1` | Prevents a small accidental subset from winning in a large point set. |
| `MinimumInlierScanlineSpan` | integer `>= 2` | Requires support across a meaningful ordered edge span without inventing a physical length. Span is exactly `max(inlier ScanlineIndex) - min(inlier ScanlineIndex)` in grid-index intervals. |

All four values are explicit per Line Fit row. v1 has no auto threshold,
learned preset, global default, or silent copying between the four real lines.

### Fixed deterministic policies

| Policy | v1 value |
| --- | --- |
| Hypothesis source | two distinct ordered Edge points |
| Small input schedule | enumerate all pairs when pair count is `<= 256` |
| Large input schedule | deterministic SHA-256-derived unique pairs |
| Maximum hypotheses | `256` |
| Candidate residual | Euclidean orthogonal distance in complete numeric XYZ |
| Threshold comparison | `residual <= MaximumOrthogonalResidual` |
| Candidate score | inlier count desc, inlier RMS asc, scanline span desc, pair indices asc |
| Final refit | centroid/covariance orthogonal total least squares on inliers |
| Reclassification | repeat refit and threshold classification until membership is stable; maximum `10` iterations, otherwise fail closed |
| Direction | positive scanline axis: `+Z` for an AcrossColumns Edge input, `+X` for AcrossRows; a near-zero component on that required axis fails closed |
| Segment endpoints | minimum/maximum inlier projection on the fitted line |

For large input, pair generation hashes the input content identity plus an
increasing attempt number and derives two point indices from the digest. Equal,
duplicate, or zero-length pairs are skipped. The exact digest-to-index byte
order becomes part of the implementation Golden contract. `System.Random` is
not used.

### Numerical sequence

1. Require one current Published `C3DHeightDifferenceEdgePointSet` and verify
   its output entity, content hash, root source, frame, coordinate convention,
   and finite ordered points.
2. Require at least three points and validate the four explicit parameters.
3. Generate at most 256 deterministic two-point hypotheses.
4. For each non-degenerate hypothesis, calculate the full-XYZ orthogonal
   residual of every input point.
5. Form the inclusive inlier set and reject candidates that fail minimum
   count, ratio, or scanline span.
6. Choose one candidate with the fixed score/tie order.
7. Refit its inliers by double-precision orthogonal TLS using the dominant
   covariance direction; reclassify all points and repeat until the exact
   inlier membership is stable.
8. Fail closed if covariance is degenerate, arithmetic is non-finite, or
   membership does not stabilize within ten iterations.
9. Require a finite non-negligible direction component on the expected
   scanline axis, canonicalize it positive, and calculate the inlier-projection
   segment endpoints.
10. Create one immutable output and diagnostics snapshot. Preview never emits
    or publishes a partial line.

The Tools implementation should use a small deterministic 3x3 symmetric
eigensolve in double precision. A new general math package is not justified
for one dominant covariance vector; the solver receives focused analytic and
degeneracy Goldens.

## Typed Output Contract

```text
C3DLineFeature
  contractVersion=1.0
  outputEntityId
  inputEdgePointSetEntityId / inputContentSha256
  rootSourceEntityId / rootSourceSha256
  sourceScalarUnit / frameId / coordinateConvention=column-rawHeight-row
  residualUnit=source-coordinate             # label, not a physical unit
  FitMethod=DeterministicConsensusOrthogonalTls
  MaximumOrthogonalResidual
  MinimumInlierCount / MinimumInlierRatio
  MinimumInlierScanlineSpan
  fixed hypothesis/refit/direction/endpoint policies

  anchorX / anchorY / anchorZ          # final inlier centroid
  directionX / directionY / directionZ # canonical unit direction
  segmentStartX / Y / Z
  segmentEndX / Y / Z

  inputPointCount / inlierCount / outlierCount / inlierRatio
  inlierScanlineMinimum / Maximum / Span
  residualRms / residualMaximum / residualMedian
  projectedSegmentLength               # source numeric, not physical

  ordered point diagnostics
    inputPointIndex / scanlineIndex
    x / y / z
    projectedX / Y / Z
    orthogonalResidual
    isInlier

  provenance
  canonical contentSha256
```

Anchor plus unit direction represents the infinite mathematical line needed by
the future intersection step. Segment endpoints are display/support evidence
only. The future Line Intersection contract must never infer that two segment
endpoints are the line definition.

The canonical SHA-256 covers contract version, all identities, parameters,
fixed policies, anchor/direction/endpoints, diagnostics, and every ordered
point classification/projection as declared IEEE-754 values. It is replay
evidence, not accuracy certification.

Successful execution is presented as `Completed - feature extraction`. It
does not create an inspection OK/NG result. Outliers are normal diagnostics;
failure to meet the taught support gates produces no `LineFeature`.

## Proposed Strict Recipe Shape

The current placeholders remain unchanged until approval. The approved
implementation would replace each Line Fit row with this closed shape:

```json
{
  "id": "step.line.ul.horizontal.01",
  "toolId": "three-d-line-fit",
  "toolName": "3D Line Fit",
  "minimumInputCount": 1,
  "inputEntityIds": [ "derived.edgepoints-ul-horizontal.01" ],
  "outputEntityId": "derived.line-ul-horizontal.01",
  "parameters": [
    { "name": "FitMethod", "value": "DeterministicConsensusOrthogonalTls" },
    { "name": "MaximumOrthogonalResidual", "value": "Set explicitly" },
    { "name": "MinimumInlierCount", "value": "Set explicitly" },
    { "name": "MinimumInlierRatio", "value": "Set explicitly" },
    { "name": "MinimumInlierScanlineSpan", "value": "Set explicitly" },
    { "name": "HypothesisPolicy", "value": "Sha256PairSchedule" },
    { "name": "MaximumHypotheses", "value": "256" },
    { "name": "RefinementPolicy", "value": "OrthogonalTlsUntilStable10" },
    { "name": "DirectionPolicy", "value": "PositiveScanlineAxis" },
    { "name": "EndpointPolicy", "value": "InlierProjectionExtents" }
  ]
}
```

The strict parser rejects unknown/duplicate names, unknown policy values,
numeric enum shortcuts, localized thousands separators, unit suffixes,
non-finite values, non-integer count/span values, and incomplete placeholders.

## State and Explicit Execution Contract

| State | Meaning | Allowed next action |
| --- | --- | --- |
| `Taught incomplete` | One or more explicit fit limits or route/output fields are missing. | Correct teaching. |
| `Waiting for upstream` | Edge output is absent, stale, Preview-only, or unpublished. | Preview and Publish Edge explicitly. |
| `Ready` | The exact Published Edge snapshot and parameters pass preflight. | Preview Line Fit. |
| `Preview running` | Line Fit is calculating away from the UI thread. | Cancel. |
| `Preview ready` | Temporary line and diagnostics exist. | Review overlays/chart or Publish. |
| `Preview stale` | Edge identity/hash or a Line Fit parameter/output changed. | Preview again. |
| `Published` | The exact current Preview snapshot was promoted. | Save or teach Line Intersection. |
| `Error` | Preflight, consensus, TLS, or support gate failed closed. | Correct the named cause. |

```text
Select / show / hide / dock / float / choose chart point
  -> presentation state only

Preview selected Line Fit
  -> require exact current Published EdgePointSet
  -> execute the one typed Tools rule
  -> create temporary LineFeature + diagnostics
  -> do not run Edge, mutate routing, save, or publish

Publish selected Line Fit
  -> require current non-stale Preview
  -> promote the exact same object without recalculation

Run recipe
  -> remain disabled while Line Intersection and later rows are unsupported
```

## Preflight and Failure Rules

Line Fit Preview and Runner fail closed when:

- input is not exactly one current Published `EdgePointSet`;
- input entity/hash/root source/frame/coordinate convention differs from its
  published evidence;
- fewer than three finite ordered points exist;
- output ID is empty, collides, or equals the input ID;
- any parameter or fixed policy is absent, duplicated, malformed, or unknown;
- maximum residual is not finite and greater than zero;
- minimum inlier count is outside `[3, input count]`;
- minimum ratio is outside `(0, 1]`;
- minimum scanline span is below two or cannot be reached by the input;
- no non-degenerate hypothesis satisfies all support gates;
- TLS covariance is degenerate, its dominant direction is non-finite, or it
  does not advance along the expected source scanline axis;
- final membership fails count, ratio, or span after refit;
- refinement does not stabilize within ten iterations;
- any output projection, residual, summary, or hash input is non-finite; or
- cancellation occurs before the immutable output is complete.

No partial output is retained or published after failure/cancellation.

## Ownership Boundary

| Layer | Owns | Does not own |
| --- | --- | --- |
| `Core` | Immutable `C3DLineFeature`, ordered point diagnostics, identities, canonical content evidence. | Calculation, WPF, or C3D loading. |
| `Data` | Existing upstream snapshot/source identity access only. | Fitting or threshold choice. |
| `Tools` | Strict parsing, deterministic hypotheses, residuals, TLS, support gates, output creation. | Viewer transforms, recipe mutation, or file dialogs. |
| Shell ViewModel | Selected row, explicit Preview/Cancel/Publish, stale propagation, Properties and diagnostics state. | A second fitting implementation. |
| Viewer | Inlier/outlier/segment/residual overlays and linked selection in the current display transform. | Fit calculation, limits, or persistence. |
| Docking View | Residual chart presentation and linked-selection command binding. | Reclassification or recipe edits. |
| `Runner` | Calls the same Tools rule and serializes the same output evidence. | Separate headless fitting logic. |

Do not introduce a generic adapter factory in this slice. Filter, Edge, and
Line Fit can continue using direct typed dispatch until duplicated behavior
demonstrates one stable abstraction.

## Verification and Acceptance Gates After Approval

### Numerical Golden

1. Exact axis-aligned X, Y, and Z full-XYZ lines recover the expected canonical
   anchor/direction/endpoints and zero residual.
2. A known oblique 3D line with symmetric small noise returns the analytic TLS
   direction and declared residual summaries within fixed tolerance.
3. One and several controlled far outliers are excluded while inlier support
   remains stable.
4. Equality at `MaximumOrthogonalResidual` is included.
5. Minimum count, ratio, and scanline span pass exactly at their boundaries
   and fail immediately below them.
6. Candidate score ties follow count, RMS, span, and pair-index order.
7. AcrossColumns canonicalizes direction to `+Z`; AcrossRows to `+X`.
8. Segment endpoints equal final inlier projection extents and never extend
   beyond evidence.
9. Duplicate/identical points, non-finite points, degenerate covariance,
   unstable refinement, malformed parameters, and cancellation fail closed.
10. Identity/hash/frame/coordinate mutations fail before calculation.
11. Repeated execution produces byte-identical classifications and output
    SHA-256.
12. Workbench and Runner use the exact same fixed output SHA-256.

### Workbench and Viewer

- all eight states are ViewModel verified;
- Line Fit refuses unpublished/stale Edge and never invokes Edge;
- chart/menu/docking/selection actions invoke zero algorithms;
- parameter or upstream changes clear current overlays/chart and mark Preview
  stale without calculating;
- Publish reuses the exact current Preview object;
- current-source before/after captures show the typed Properties card,
  inlier/outlier line evidence, HUD state, and docked residual chart;
- chart-to-Viewer and Viewer-to-chart selection identifies the same point;
- right-drag pan, short-right-click menu, top View menu, orbit, zoom, pick, and
  Profile remain green; and
- whole Run remains blocked before unsupported downstream rows.

### Regression and host boundary

- build: zero warnings/errors;
- existing Filter and Edge Goldens/Workbench parity;
- teaching and source-bound selection verification;
- docking/Profile/height-distribution and pointer matrices;
- DLL-only BinaryHost manifest/outputs/Host API and direct C3D render/pick;
- CI invocation/report assertions for Line Fit Golden, Workbench parity, and
  current-source screenshot quality.

### Actual Warpage adoption gate

Synthetic Goldens prove the algorithm contract first. The actual affine chain
still requires the owner to teach and review all four Line Fit rows against
the four real Published Edge outputs:

- maximum source-coordinate residual;
- minimum inlier count;
- minimum inlier ratio; and
- minimum inlier scanline span.

No value is inferred from the smoke-only Edge band or copied from another row.

## Approval Checkpoint

Implementation may begin only after the owner approves or changes these nine
decisions:

1. Line Fit consumes exactly one current Published `EdgePointSet` and never
   runs Edge implicitly.
2. v1 fits all three numeric components `(column, raw-height, row)` and labels
   residuals as uncalibrated source-coordinate values.
3. Use deterministic consensus followed by orthogonal TLS; do not use plain
   all-point least squares, random RANSAC, or IRLS-only fitting.
4. Require four explicit operator limits: maximum residual, minimum inlier
   count, minimum inlier ratio, and minimum inlier scanline span.
5. Fix the hypothesis schedule to all pairs up to 256 and SHA-256-derived pairs
   above that, with at most 256 hypotheses.
6. Refit/reclassify until stable with a maximum of ten iterations; failure to
   stabilize produces no line.
7. Canonicalize direction to `+Z` for AcrossColumns input and `+X` for
   AcrossRows input; display only the inlier projection segment.
8. Add the compact Properties card, inlier/outlier Viewer overlay, identical
   right-click/top View toggles, and dockable linked residual chart.
9. Keep success as feature-extraction completion without inspection OK/NG;
   leave the four production rows explicitly incomplete until real teaching.

## Completion Record

```text
Status: Complete
Scope: 3D Line Fit v1 approval design only; no production algorithm, recipe,
       Workbench, Viewer, Runner, or CI implementation was changed.
Acceptance criteria:
  - UI-first Workbench/Viewer/docking flow recorded: Pass.
  - Full-XYZ deterministic robust-fit contract and unit boundary recorded: Pass.
  - Typed input/output, strict recipe, lifecycle, ownership, failures, and
    Golden/regression gates recorded: Pass.
  - Nine implementation decisions are explicit and await owner approval: Pass.
Verification:
  - Current EdgePointSet contract, Edge Tools rule, teaching template,
    Workbench Properties/state, Viewer interaction contracts, and handoff read.
  - Current 2D Line PropertyGrid/result-review behavior reviewed read-only.
  - Existing commercial review and current product target checked.
Evidence: docs/OPENVISIONLAB_3D_LINE_FIT_TYPED_ADAPTER_DESIGN_20260719.md
Boundary / next dependency: owner approval or requested changes to the nine
decisions. Do not implement Line Fit until that decision is recorded.
```
