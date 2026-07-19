# OpenVisionLab 3D Height Difference Edge Typed Adapter v1 Design

Updated: 2026-07-19

Status: **Complete - approved and implemented locally**

Implementation approval: **Owner approved all seven decisions on 2026-07-19**

## Work Contract

### User goal

Define the first feature-extraction adapter after Filter so the taught workflow
can progress from a filtered C3D height field to deterministic edge points, then
later to 3D line fit, line intersection, correspondence, and a full XYZ affine
map.

### Non-negotiable requirements

- The tool is one reusable 3D inspection step, not a Thickness/Warpage-only
  shortcut.
- Teaching, selection capture, parameter editing, entity visibility, and Viewer
  display changes never execute the edge calculation.
- `Preview`, `Run`, and `Publish` remain explicit actions.
- The result is a typed `EdgePointSet` with source, selection, parameter, and
  per-point provenance. It is not a bitmap edge layer.
- Numerical calculation uses the complete row-major C3D height field, not
  render-density points or Viewer-scaled display coordinates.
- Missing samples are never bridged, filled, or treated as zero height.
- This slice cannot claim physical scale, sub-pixel accuracy, edge-location
  uncertainty, calibration, affine validity, or metrology.

### Included scope

- one C3D adjacent-height-difference rule;
- one recipe-owned `GridRectangle` edge search band;
- explicit comparison axis, edge polarity, and minimum height difference;
- one deterministic candidate per scanline;
- typed input/output, diagnostics, provenance, and failure contracts;
- selected-step Properties and Viewer overlay behavior; and
- synthetic Golden, fixed-source identity, UI, Runner, and regression gates.

### Excluded scope

- Sobel, Scharr, Laplacian, Canny, convolution kernels, morphology, or contour
  tracing;
- automatic ROI discovery, multiple disconnected bands, freehand/lasso, and
  point-cloud or mesh neighborhood edges;
- sub-cell interpolation, smoothing inside the Edge tool, and automatic
  threshold selection;
- 3D line fitting, outlier rejection, line intersection, correspondence,
  affine solving, and re-grid execution; and
- physical units, sensor calibration, uncertainty, or metrology claims.

## Evidence and Product Fit

The current catalog and feature-first template already declare:

```text
Filter / FilteredHeightField
  -> Height Difference Edge + recipe-owned GridRectangle
  -> EdgePointSet
  -> 3D Line Fit
```

The generic teaching implementation already persists the required
source-SHA-bound `GridRectangle`. Filter v1 already produces a same-dimension,
same-frame C3D `FilteredHeightField` with a canonical output hash. No new
selection schema or generic executor is needed for this adapter.

The 2D OpenVisionLab reference confirms the workflow rules worth keeping:

1. input and output entities are explicit;
2. tool parameters are visible and recipe-owned;
3. parameter changes wait for an explicit Preview; and
4. a feature-preparation step does not make the final measurement OK/NG
   decision.

Its Canny/Sobel/Scharr/Laplacian implementations operate on 8-bit image
intensity and therefore are not reused as the numerical definition for C3D
raw height.

The historical manual review confirms the user's intended functional chain of
four bounded line searches, fitted lines, and two corner intersections. Only
that abstract workflow is retained. Historical UI, labels, defaults,
proprietary correction behavior, and equipment scope are not copied.

## Fixed-Source Read-Only Observation

The user-designated Warpage source was inspected without changing it:

```text
Path:   3D/Warpage/Ori_20240116_094430.C3D
SHA-256: 79C02761F9B711C0F8980D4376B9FCE25E00D425E6CA85DA4D4349ECF5F0299C
Grid:   1301 columns x 1967 rows
Valid:  1,653,562
Missing: 905,505
```

Whole-source adjacent absolute raw-height differences are strongly
axis-dependent:

| Compare axis | Median | p90 | p95 | p99 | Maximum |
| --- | ---: | ---: | ---: | ---: | ---: |
| Across columns | 1.343994 | 7.552002 | 30.976074 | 235.583984 | 1140.608154 |
| Across rows | 7.359985 | 48.512009 | 131.760010 | 334.256042 | 844.224121 |

These values mix surface variation, real transitions, noise, and outliers.
They are evidence that a global guessed default would be misleading; they are
not an approved threshold and are not algorithm-validation evidence. Each
edge band must store its own explicit `MinimumDelta` after the user reviews the
relevant area/Profile.

## Proposed Owner Decisions for v1

| Decision | Proposed v1 value | Reason |
| --- | --- | --- |
| Search region | One recipe-owned `GridRectangle` consumed directly by Edge | It is a bounded search constraint, not a cropped output field. `ROI / Crop` remains a separate transform tool. |
| Comparison axis | `AcrossColumns` or `AcrossRows` | Names describe the compared neighbor pair and remove the current ambiguous `Horizontal/Vertical` wording. |
| Polarity | `Rising`, `Falling`, or `Absolute` | Signed transitions distinguish entering/leaving a raised surface; `Absolute` supports direction-independent teaching. |
| Threshold | finite `MinimumDelta > 0`, comparison is inclusive | Candidate condition uses `>=`; no hidden default or auto-threshold. |
| Candidate policy | `StrongestPerScanline` | Produces a bounded, deterministic set suitable for later line fitting instead of every noisy threshold crossing. |
| Tie policy | lowest comparison-start row/column index | Makes repeated execution and hashes deterministic. |
| Point location | midpoint of the winning adjacent 3D sample pair | Symmetric definition avoids silently choosing the high or low side of a height step. |
| Missing/boundary | `SkipPair` and `WithinSelection` | No interpolation, padding, comparison outside the taught band, or bridge across a hole. |
| Minimum output | at least two points | Prevents publishing an empty/single-point line candidate; later 3D Line Fit owns stricter inlier rules. |
| Upstream state | input entity must be current and Published | Edge Preview never runs Filter implicitly or consumes an unpublished/stale upstream artifact. |

The fixed policy values remain visible in Evidence/Advanced and are persisted
in the recipe so a future version cannot silently change their meaning.

## Direction and Coordinate Contract

The source numeric frame is not the Viewer display frame. The adapter uses the
uncalibrated grid/raw-height tuple:

```text
numeric X = grid column
numeric Y = raw height
numeric Z = grid row
```

The Viewer may center and scale these values for rendering, but that display
transform is never used by the edge calculation or the later fit input.

### `AcrossColumns`

For every row in the selected rectangle, compare adjacent columns in increasing
column order:

```text
h0 = H[row, column]
h1 = H[row, column + 1]
delta = h1 - h0
```

This searches in the numeric +X direction. The resulting point set normally
forms an edge whose long direction is approximately along rows / numeric Z.

### `AcrossRows`

For every column in the selected rectangle, compare adjacent rows in increasing
row order:

```text
h0 = H[row, column]
h1 = H[row + 1, column]
delta = h1 - h0
```

This searches in the numeric +Z direction. The resulting point set normally
forms an edge whose long direction is approximately along columns / numeric X.

The UI always shows both labels, for example:
`Compare across columns (+X) | expected edge along rows (Z)`. It never shows
only `Horizontal` or `Vertical`.

## Numerical Definition

For every scanline inside the selected rectangle:

1. Enumerate only adjacent pairs fully contained in the rectangle.
2. Skip a pair when either raw-height value is missing or non-finite.
3. Calculate `delta = next - current` in the selected increasing-axis
   direction.
4. Apply the selected polarity:

   ```text
   Rising:   delta >= MinimumDelta
   Falling:  delta <= -MinimumDelta
   Absolute: abs(delta) >= MinimumDelta
   ```

5. From the passing candidates, select the greatest `abs(delta)`.
6. If magnitudes are exactly equal as `double`, select the candidate with the
   lowest comparison-start index.
7. Create one point at the arithmetic midpoint of the two numeric samples:

   ```text
   AcrossColumns: X = column + 0.5
                  Y = (h0 + h1) / 2
                  Z = row

   AcrossRows:    X = column
                  Y = (h0 + h1) / 2
                  Z = row + 0.5
   ```

8. Order output points by ascending scanline index.

No accepted candidate on a scanline is a normal diagnostic outcome. The full
step fails only when the final output contains fewer than two points or a
typed preflight rule fails.

## Teaching UI

The existing docked `Project Explorer | 3D View | Properties` layout is
retained. No new permanent pane or Edge-local execution button is added.

Selecting an authored Edge row shows this compact Properties content:

```text
Step 02: Height Difference Edge
------------------------------------------------------
Input
  Height field       derived.filtered-height.01
  Upstream state     Published | current

Search band
  Grid rectangle     selection.edge-band.ul.x
  Row / column       40..129 / 30..449
  [Capture] [Replace] [Use existing]

Edge rule
  Compare             [Across columns (+X)       v]
  Expected edge       Along rows (Z)
  Polarity            [Rising | Falling | Absolute]
  Minimum delta       [ explicit raw-height value ]

Fixed v1 policy
  Candidate           Strongest per scanline
  Point               Adjacent-pair midpoint
  Missing             Skip pair; stay inside band

Output
  EdgePointSet        derived.edgepoints-ul-x.01
  State               Taught - ready for Preview
```

UI rules:

- `MinimumDelta` is required invariant numeric input. Blank, zero, negative,
  `NaN`, infinity, localized thousands separators, and unit suffixes are
  rejected rather than normalized.
- Capture/Replace continues to use the established two-grid-corner teaching
  session and never finds an edge.
- Changing the search band, input, axis, polarity, threshold, or output ID
  marks an existing Preview stale but does not recalculate it.
- The existing Profile tool is the supporting display aid for inspecting local
  raw-height transitions. Profile state remains separate from recipe evidence.
- The global top `Preview`, `Run`, and `Publish` commands remain the only
  execution actions.

## Viewer and Evidence UX

Before Preview, the Viewer shows only the applied recipe-owned search rectangle
and a compact compare-direction arrow. It does not show guessed edge points.

After explicit Preview, the temporary result overlay shows:

- the search rectangle in the normal teaching-selection style;
- a small +X or +Z compare-direction arrow;
- one visible marker per accepted scanline, without drawing a fitted line;
- a selected-marker detail containing both source locators, `h0`, `h1`, signed
  `delta`, magnitude, and midpoint XYZ; and
- `Preview output - not published` until the user explicitly publishes.

Properties/Evidence shows candidate count, scanline count, eligible pair count,
missing-pair skips, no-candidate scanlines, and accepted magnitude min/max/mean.
Rejected crossings are not all rendered in v1; that would create a heavy debug
surface before a real need is proven.

Published Edge points appear as a separate entity/layer. Publishing never
changes a later step input automatically and never draws the later fitted line.

## State and Explicit Execution Contract

| State | Meaning | Allowed next action |
| --- | --- | --- |
| `Taught incomplete` | Search band, explicit threshold, route, or output is missing. | Correct teaching. |
| `Waiting for upstream` | Routed Filter output is absent, stale, or unpublished. | Preview/Publish Filter explicitly. |
| `Ready` | Typed route, source, selection, and parameters pass preflight. | Preview. |
| `Preview running` | Explicit Edge calculation is running off the UI thread. | Cancel. |
| `Preview ready` | Temporary points and diagnostics exist. | Inspect or Publish. |
| `Preview stale` | Input identity, selection, or parameter changed. | Preview again. |
| `Published` | Current non-stale point set is promoted to the declared entity. | Save recipe or teach the next step. |
| `Error` | Preflight or execution failed closed. | Correct the named cause and retry. |

```text
Teach / selection capture / parameter edit / visibility / Profile
  -> authored/display state only

Preview selected Edge
  -> require current Published upstream input
  -> verify root-source and derived-input identities
  -> verify the exact recipe-owned GridRectangle
  -> execute the typed rule
  -> create temporary points and diagnostics
  -> do not run Filter, Publish, save, or reroute any entity

Publish selected Edge
  -> require the current non-stale Preview
  -> promote that exact point snapshot without recalculation

Run recipe
  -> remain disabled for the current affine template until every requested
     downstream row has an approved adapter
  -> never partially run only Filter and Edge while reporting recipe success
```

## Typed Recipe Contract

The current ambiguous `Direction` and placeholder `Threshold` fields are not
silently migrated because `Horizontal/Vertical` does not reveal whether it
describes comparison direction or expected edge orientation.

The approved v1 shape will be:

```json
{
  "id": "step.edge.ul.x.01",
  "toolId": "height-difference-edge",
  "toolName": "Height Difference Edge",
  "minimumInputCount": 1,
  "inputEntityIds": [
    "derived.filtered-height.01",
    "selection.edge-band.ul.x"
  ],
  "outputEntityId": "derived.edgepoints-ul-x.01",
  "parameters": [
    { "name": "ComparisonAxis", "value": "AcrossColumns" },
    { "name": "Polarity", "value": "Rising" },
    { "name": "MinimumDelta", "value": "100" },
    { "name": "CandidatePolicy", "value": "StrongestPerScanline" },
    { "name": "PointPolicy", "value": "PairMidpoint" },
    { "name": "MissingValuePolicy", "value": "SkipPair" },
    { "name": "BoundaryPolicy", "value": "WithinSelection" }
  ]
}
```

The existing structural field counts required data inputs, so it remains `1`
to keep an incomplete teaching template saveable. Typed readiness still
requires exactly the two routed entities shown above: one Published height
field and one `GridRectangle`. This representation does not change the
approved search-band decision.

`100` is a schema example for a synthetic fixture, not a recommended Warpage
threshold. The actual four edge rows remain incomplete until their bands,
polarity, and `MinimumDelta` values are explicitly taught.

The strict parser rejects unknown names, duplicate names, unknown enum values,
missing fixed policies, invalid numbers, and silent numeric normalization.

## Typed Input and Output

```text
C3DHeightDifferenceEdgeInput
  stepId / inputEntityId / selectionId / outputEntityId
  rootSourceEntityId / rootSourceSha256
  inputByteLength / inputContentSha256
  width / height / unit / frameId / scalarMeaning=raw-height
  complete row-major values
  GridRectangle
  ComparisonAxis / Polarity / MinimumDelta
  CandidatePolicy=StrongestPerScanline
  PointPolicy=PairMidpoint
  MissingValuePolicy=SkipPair
  BoundaryPolicy=WithinSelection

C3DHeightDifferenceEdgePoint
  scanlineIndex
  firstRow / firstColumn / firstHeight
  secondRow / secondColumn / secondHeight
  signedDelta / magnitude
  x=grid-column / y=raw-height / z=grid-row

C3DHeightDifferenceEdgeOutput
  outputEntityId / rootSourceEntityId / inputEntityId / selectionId
  unit / frameId / scalarMeaning=raw-height
  ordered immutable EdgePointSet
  scanlineCount / eligiblePairCount / skippedMissingPairCount
  acceptedScanlineCount / noCandidateScanlineCount
  acceptedMagnitudeMinimum / Maximum / Mean
  canonical outputContentSha256
  provenance: toolId, contractVersion, source/input hashes, selection, parameters
```

The canonical output hash covers the fixed contract version, identities,
selection bounds, parameters, diagnostics, and ordered IEEE-754 point fields in
one declared byte order. It is Viewer/Runner repeatability evidence, not a
physical accuracy claim.

## Preflight and Failure Rules

Preview and Runner fail closed when any of these conditions is present:

- the first input does not resolve to one current Published C3D height field;
- the second input is not one recipe-owned `GridRectangle`;
- the selection root source, SHA-256, grid dimensions, or frame differs from
  the height field's root-source provenance;
- the derived input path, byte length, SHA-256, dimensions, unit, frame, or
  scalar meaning differs from its published evidence;
- the rectangle is outside the grid;
- `AcrossColumns` has fewer than two columns, or `AcrossRows` has fewer than
  two rows;
- the rectangle cannot provide at least two scanlines;
- `MinimumDelta` is not finite and greater than zero;
- any enum/fixed policy is absent or unsupported;
- output ID is empty, collides with another identity, or equals an input ID;
- fewer than two points remain after missing/polarity/threshold evaluation;
- midpoint or diagnostic arithmetic becomes non-finite; or
- cancellation occurs before a complete immutable output exists.

A failed or cancelled Preview publishes no partial point set. Edge success is
shown as `Completed - feature extraction`; it does not assign inspection
Pass/Fail.

## Ownership Boundary

| Layer | Owns | Does not own |
| --- | --- | --- |
| `Core` | Immutable edge-point/result/provenance contracts and existing recipe identities. | C3D bytes, WPF, or calculation. |
| `Data` | Same-byte C3D/derived-height identity verification and immutable complete height snapshots. | Thresholds, polarity, or point selection. |
| `Tools` | Strict parameter parsing, adjacent-pair rule, point/diagnostic/hash creation. | Viewer scaling, recipe mutation, or file dialogs. |
| Shell ViewModel | Selected step, explicit Preview/Cancel/Publish, stale propagation, readiness and evidence text. | A second numerical implementation. |
| Viewer | Search-band/direction/temporary-point overlays and hit display in the current display transform. | Recipe persistence, threshold choice, or fitting. |
| `Runner` | Headless use of the same Tools rule and evidence serialization. | A separate edge algorithm. |

As with Filter, no generic adapter interface/factory is introduced merely
because a second tool exists. Direct typed dispatch remains sufficient until a
third adapter proves a stable common abstraction.

## Verification and Acceptance Gates

### Numerical Golden

1. `AcrossColumns + Rising` finds the expected midpoint points on an analytic
   positive height step.
2. `AcrossRows + Falling` finds the expected midpoint points on an analytic
   negative height step.
3. `Absolute` accepts both signs while preserving signed delta evidence.
4. threshold equality passes because the contract is inclusive.
5. multiple crossings select the strongest magnitude on each scanline.
6. equal-magnitude ties select the lowest comparison-start index.
7. missing cells skip only affected adjacent pairs and are never bridged.
8. comparisons never read outside the selected rectangle.
9. narrow rectangles, insufficient scanlines, no candidates, and a single
   output point fail with the declared messages.
10. zero, negative, non-finite, malformed, duplicate, and unknown parameters
    fail closed.
11. root-source, derived-input, selection SHA, frame, grid, byte-length, and
    content mutations fail closed.
12. repeated execution produces the same ordered points, diagnostics, and
    output hash.
13. Viewer and Runner consume the same fixed synthetic output hash.

### UI and workflow

- incomplete, waiting-upstream, Ready, Preview-running, Preview-ready, stale,
  Published, cancelled, and Error states are ViewModel verified;
- selection capture, parameter edit, Profile, palette, geometry style, and
  visibility changes invoke zero algorithms;
- Edge Preview refuses unpublished/stale Filter output and never runs Filter;
- Preview does not rewrite input routes or save the recipe;
- Publish reuses the exact current Preview without recalculation;
- current-source before/after `1280 x 760` captures show the bounded Properties
  card, search band/direction, and clearly labeled point preview;
- the template and UI replace ambiguous direction wording without inventing
  real Warpage thresholds or search rectangles;
- whole Run remains blocked before partial execution of unsupported downstream
  rows; and
- build, teaching, selection, docking, Profile, distribution legend,
  pointer/context menu, BinaryHost, Viewer, and Shell baselines remain green.

### Actual Warpage adoption gate

The implementation can be proven first with OpenVisionLab-owned analytic C3D
fixtures. It is not accepted as evidence for the user's real alignment chain
until the owner teaches and reviews four real search bands, comparison axes,
polarities, and thresholds on the fixed Warpage source. Those values must not
be inferred from the whole-source quantiles above.

## Risks and Deferred Decisions

- A strongest crossing can select a different physical boundary when one
  search band contains multiple large steps. v1 addresses this by requiring a
  deliberately narrow recipe-owned band, not by adding heuristic grouping.
- Pair-midpoint position is deterministic but not sub-cell edge estimation.
  Interpolation requires a separate model and Golden evidence.
- Two resulting corner anchors do not establish a general full-XYZ affine
  transform. Four affine-independent source/reference correspondences, or a
  separately approved fixture-constrained transform, remain required.
- Grid column/row and raw height are uncalibrated numeric components. Source
  X/Z pitch, height scale/offset, units, axis orientation, and calibration
  identity remain external prerequisites for physical claims.

## Approval Checkpoint

All seven decisions below were approved by the owner on 2026-07-19. Current
implementation evidence is recorded in
`docs/OPENVISIONLAB_3D_HEIGHT_DIFFERENCE_EDGE_TYPED_ADAPTER_20260719.md`.

Implementation may begin only after the owner approves or changes these seven
decisions:

1. Edge consumes one recipe-owned search-band `GridRectangle` directly; it
   does not require an executed `ROI / Crop` output.
2. Replace ambiguous `Horizontal/Vertical` with `AcrossColumns (+X)` and
   `AcrossRows (+Z)`, while separately showing expected edge orientation.
3. Support `Rising`, `Falling`, and `Absolute`; use explicit inclusive
   `MinimumDelta > 0` with no automatic threshold.
4. Select `StrongestPerScanline`, with the lowest start index as the tie rule.
5. Output the arithmetic midpoint of the adjacent XYZ samples rather than
   silently choosing the high or low surface.
6. Skip missing pairs, stay inside the selection, and require at least two
   output points; later 3D Line Fit owns robust inlier acceptance.
7. Edge Preview requires a current Published upstream height entity and never
   runs Filter implicitly.

## Completion Record

```text
Status: Complete
Scope: Height Difference Edge v1 design and owner approval. Production-code
       closure is recorded in the separate implementation evidence document.
Acceptance criteria:
  - Current teaching selection, C3D/Filter, Workbench, Viewer, and Runner
    boundaries reviewed: Pass.
  - Relevant 2D explicit-layer/parameter/Preview workflow reviewed without
    adopting image-intensity algorithms: Pass.
  - Actual fixed Warpage adjacent-difference distribution inspected read-only
    and recorded without choosing a threshold: Pass.
  - Minimal deterministic edge rule, overlay UX, typed contracts, failures,
    ownership, Goldens, and approval decisions recorded: Pass.
Verification:
  - Source/doc consistency review and read-only fixed-source NumPy analysis.
  - The seven owner decisions were approved without numerical changes: Pass.
Evidence: docs/OPENVISIONLAB_3D_HEIGHT_DIFFERENCE_EDGE_TYPED_ADAPTER_DESIGN_20260719.md
Boundary / next dependency: See the implementation evidence document for the
local code/UI/Runner result and the real-teaching dependency.
```
