# OpenVisionLab 3D Full-XYZ Affine Tool Design

Updated: 2026-07-20

Status: Owner-authorized design. This document adds no affine solver,
transformed point cloud, re-grid implementation, or physical claim.

## Product decision

The product will implement the owner's chosen full XYZ point-cloud affine
mapping, not a height-map XY correction.

    q = A p + t

    p = (source X, source Y, source Z)
    q = (reference X, reference Y, reference Z)
    A = 3 x 3 linear affine component
    t = 3 x 1 translation

The first mapping direction is always current source frame -> authored
reference frame. The current source convention is column-rawHeight-row
(Viewer: X/right, Y/up-height, Z/depth). Reverse mapping, registration
recovery, calibration, and metrology are separate product decisions.

The product stays a local deterministic typed recipe workbench. This design
does not introduce a free-form graph, automatic feature matcher, camera/PLC,
robot, cloud, or production-line control.

## Existing two-corner teaching is an acquisition scaffold

The established workflow remains the way landmarks are obtained:

    height difference -> EdgePointSet -> 3D Line Fit -> Line Intersection
    -> named CornerAnchor

Upper-left and lower-right CornerAnchor evidence is useful but two
source/reference pairs cannot determine a general 3D affine transform. A v1
solve needs four source/reference pairs. The four source positions and the
four reference positions must each form a non-degenerate tetrahedron. Four
points on a single top plane remain insufficient, even with small C3D noise.

The existing C3DLandmarkCorrespondenceSet already enforces exactly four
current Published CornerAnchor values, rank 4/4, and normalized
tetrahedron-volume gates. At least one qualified off-plane feature is required
on both sides. A planar fixture needs a separately designed constrained planar
transform; v1 fails closed and never silently downgrades to XY alignment.

The operator input package remains
docs/OPENVISIONLAB_3D_FOUR_ANCHOR_TEACHING_INPUT_PACKAGE_20260720.md.
This design does not invent fixture XYZ values, frames, units, provenance,
revisions, or thresholds.

## Typed end-to-end chain

    RawHeightField / SourceC3D
      -> FilteredHeightField                 (feature extraction input)
      -> EdgePointSet
      -> FittedLine3D x2 per landmark
      -> CornerAnchor x4
      -> CorrespondenceSet                   (existing structural gate)
      -> AffineTransform3D                   (A1: solve only)
      -> TransformedPointCloud               (A2: explicit apply)
      -> RegriddedHeightField                (A3: later separate design)
      -> Thickness / Warpage / MeasurementResult

The Filter output establishes feature anchors. The raw C3D is the default
surface for affine application, so preprocessing does not silently alter
inspection data. A filtered surface is allowed only when explicitly selected
as the application input and its output is visibly labelled filtered.

Every Preview or Published artifact retains root source entity/SHA-256, input
artifact identity, frame, unit, coordinate convention, parameter identity,
state, and canonical content SHA-256.

## Delivery slices

| Slice | Input -> output | Purpose | Excluded behavior |
| --- | --- | --- | --- |
| A1: XYZ Affine Solve | Published CorrespondenceSet -> AffineTransform3D | Solve and validate one matrix. | No C3D point is moved. |
| A2: Apply XYZ Affine | selected HeightField + Published AffineTransform3D -> TransformedPointCloud | Move every finite source point once. | No re-grid, fill, smoothing, or measurement. |
| A3: Re-grid | Published TransformedPointCloud -> RegriddedHeightField | Create a reference-frame inspection grid. | Requires its own interpolation/collision design. |
| A4: Measurement | Published RegriddedHeightField -> result | Teach Thickness, Warpage, and later rules. | No physical/metrology claim by default. |

This is deliberately split: matrix fitting, applying a transform, resampling,
and measuring are different failure domains and must each have their own
Preview/Publish and Runner evidence.

## A1: XYZ Affine Solve v1

### Input and WPG parameter contract

The only v1 input is one current Published C3DLandmarkCorrespondenceSet.
It must be exactly the existing four ordered pairs, with shared root source,
source frame/unit/convention, explicit reference frame/unit/provenance/revision,
rank 4/4, valid normalized volumes, and a current content SHA-256.

The WPG teaches only these new numerical values:

| Parameter | Closed v1 policy |
| --- | --- |
| SolvePolicy | ExactFourPartialPivot only. |
| MaximumConditionEstimate | Explicit finite positive rejection limit for the source augmented matrix. |
| ArithmeticResidualWarning | Explicit finite non-negative review threshold. |

There is no least squares, outlier removal, automatic correspondence matching,
or selection of four rows from a larger set. A five-pair recipe is rejected by
v1. With exactly four pairs a matrix interpolates those pairs, so residual is
only an arithmetic-consistency review signal, not fit-quality or metrology
evidence.

### Deterministic mathematics

Use double arithmetic and a row formulation:

    P = [ sourceX sourceY sourceZ 1 ]        // 4 x 4
    Q = [ referenceX referenceY referenceZ ] // 4 x 3
    B = inverse(P) * Q                        // 4 x 3

    reference = [sourceX sourceY sourceZ 1] * B

B is the named source-to-reference 3x4 matrix. The implementation uses
scaled partial-pivot Gauss-Jordan or LU decomposition. It must not use normal
equations. Each pivot has a documented relative tolerance; the tool calculates
the infinity-norm condition estimate normInf(P) * normInf(inverse(P)) and rejects a value
above the taught maximum.

The tool records source/reference ranks and normalized volumes, signed source
augmented determinant, abs(det(A)), condition estimate, every transformed
anchor, every residual vector/norm, RMS and maximum arithmetic residual, and
all input/output hashes. It fails closed for stale input, non-finite values,
pivot failure, invalid matrix, or condition-limit breach. A residual warning
is a review state only; it is not an inspection OK/NG decision.

### New immutable Core contracts

    C3DAffineTransform3D
      outputEntityId
      correspondenceEntityId / correspondenceContentSha256
      rootSourceEntityId / rootSourceSha256
      sourceFrameId / sourceUnit / sourceCoordinateConvention
      referenceFrameId / referenceUnit / provenance / revision
      matrix M11..M14, M21..M24, M31..M34
      sourceAugmentedDeterminant / linearDeterminantAbsolute
      conditionEstimate / taughtMaximumConditionEstimate
      arithmeticRmsResidual / arithmeticMaximumResidual / warningThreshold
      ordered C3DAffineLandmarkResidual x4
      provenance / contentSha256

    C3DAffineLandmarkResidual
      source CornerAnchor identity/hash/XYZ
      reference landmark identity/XYZ
      transformed XYZ / residual XYZ / residual norm

The contracts contain no mutable viewer state, source path, Matrix4x4 float,
or physical-calibration flag. Viewer code may convert the canonical double
matrix only for drawing.

### UI and lifecycle

The reusable XYZ Affine Solve Tool Lab follows the approved typed rhythm:

    Published correspondences | typed WPG parameters | matrix and residual evidence
    source/reference provenance | Preview / Discard / Publish | no surface output

- Opening, docking, row selection, camera motion, and overlay visibility do
  not solve, save, or change a route.
- Preview resolves one current Published CorrespondenceSet and creates a
  temporary immutable AffineTransform3D.
- Discard removes only that Preview; Publish promotes exactly that Preview and
  never recalculates.
- A correspondence identity or WPG draft change makes Preview stale.
- The Viewer shows four labelled anchor pairs and residual connectors; it does
  not fabricate a transformed height map in A1.

The Flow Map displays CorrespondenceSet -> AffineTransform3D. Candidate
selection and explicit Add remain separate from Preview and Publish.

## A2: Apply XYZ Affine v1

    Current SourceC3D or current Published FilteredHeightField
      + current Published AffineTransform3D with matching root source identity
      -> TransformedPointCloud

For every finite (x, y, z), apply the published double matrix exactly once.
Preserve grid-cell locator, RGB/display attributes, raw value, source binding,
and explicit surfaceFlavor (raw or filtered). Missing C3D cells stay missing.
No points, triangles, or values are invented.

The result viewer keeps the existing line/grid-based default where source
topology exists. It can show transformed grid edges and point markers, but it
does not triangulate, fill holes, modify the source Viewer, or claim a height
map before A3. Preview and Publish use normal current/stale identity rules.

## A3: Re-grid is a later independent design

A transformed cloud is not automatically an inspection height map. Re-grid
must later define reference output frame/axis convention, origin, spacing,
bounds, dimensions, collision policy, interpolation/support radius, missing
and hole policy, output identity, and Viewer/Runner parity. No interpolation,
nearest-neighbour fill, smoothing, or height image is hidden in A1 or A2.
Thickness and Warpage consume only a Published RegriddedHeightField.

## Responsibility boundaries

| Layer | A1 owns | A2 owns | Never owns |
| --- | --- | --- | --- |
| Core | Immutable affine/residual contracts and closed schema. | Transformed-cloud identity contract. | UI state and dialogs. |
| Data | Recipe persistence and source-byte binding. | Declared-source reading. | Matrix math or silent conversion. |
| Tools | Double solve, validation, canonical hash. | Finite-point application. | WPF lifecycle or re-grid policy. |
| Runner | Same Tools solve/report. | Same Tools application/parity. | Independent math. |
| Shell ViewModel | WPG drafts, state, explicit commands. | Explicit surface selection/apply lifecycle. | Math in code-behind. |
| Viewer / Tool Lab | Evidence presentation. | Input/result viewers and overlays. | Execution or persistence. |

## Recipe compatibility

The current xyz-affine-transform row in
recipes/c3d-xyz-affine-teaching-template.ov3d-teach.json remains a readable
two-corner, taught-only scaffold. It is not auto-upgraded or executed.

After a real schema 1.2 four-anchor correspondence recipe exists, an explicit
Save As migration may replace it with:

    step.xyz-affine-solve.01
      input: derived.correspondences.01
      output: derived.affine-transform.01

    step.apply-xyz-affine.01
      input: source.c3d.height-map + derived.affine-transform.01
      output: derived.transformed-point-cloud.01

The migration must never infer missing anchors, reference values, units,
frames, provenance, revision, normalized-volume threshold, condition limit,
or residual threshold.

## Required implementation evidence

### A1 numerical gates

1. Identity map from four non-coplanar exact pairs.
2. Translation, anisotropic scale, shear, and rotation represented by one
   general affine matrix.
3. Independently calculated matrix, transformed anchors, determinant, and
   residual values.
4. Missing/stale input, non-finite coordinate, duplicate pair, rank/volume
   failure, pivot failure, malformed parameter, wrong frame, and condition
   rejection.
5. Repeated Tools/Runner execution produces the same canonical SHA-256.
6. A five-pair document fails closed rather than silently using least squares.

### A2 and UI gates

1. A small synthetic grid matches independent double transformation for every
   finite point; missing cells stay missing.
2. Raw/filtered output labels stay distinct and root identity matches the
   transform provenance.
3. Preview/Publish/stale/discard states match between Workbench and Runner;
   opening either Tool Lab does not execute a tool.
4. Existing Filter, Edge, Line Fit, Line Intersection, Landmark
   Correspondence, docking, keyboard, title-bar, and BinaryHost gates remain
   green.
5. Fresh Korean/English 1920 and compact 1280 UI captures show inputs,
   parameters, explicit action, evidence, and output without clipping.

Synthetic gates prove deterministic computation only. A real fixture run still
needs the completed four-anchor input package and Runner parity. It does not
by itself prove calibration, Gauge R&R, uncertainty, or metrology.

## Owner approval required before code

1. Keep A1 solve and A2 application as two explicit tools.
2. Keep ExactlyFour; no least squares, outlier rejection, or automatic
   correspondence matching in v1.
3. Require explicit MaximumConditionEstimate and ArithmeticResidualWarning
   WPG parameters.
4. Keep A3 Re-grid and all Thickness/Warpage algorithms as later designed
   slices.

After approval, A1 becomes the next implementation proposal. It starts only
after the UI gate is formally accepted or the owner explicitly reopens code
implementation. A2 begins only after A1 numerical, recipe, Runner, UI, and
lifecycle gates pass.
