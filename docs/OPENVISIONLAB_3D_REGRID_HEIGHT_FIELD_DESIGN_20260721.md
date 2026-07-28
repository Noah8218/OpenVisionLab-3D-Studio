# OpenVisionLab 3D A3 Re-grid Height Field Design

Updated: 2026-07-21

Status: Implemented for deterministic synthetic software evidence after owner
approval on 2026-07-21. Real aligned A1/A2 data, trusted Mapping Profile
provenance, physical calibration, and metrology validation remain unverified.

## Purpose

A2 produces an immutable point cloud in the A1 reference frame. It is not a
height map. A3 may create one inspection-grid artifact only through an explicit
recipe-owned profile:

```text
current Published TransformedPointCloud + explicit ReferenceGridProfile
    -> TransformedHeightField
```

The profile makes the otherwise ambiguous choices visible: reference origin,
planar U/V axes, height H axis, pitch, dimensions, extent, cell assignment,
collision handling, and missing-value behavior. A3 creates no physical unit,
calibration, Thickness, Warpage, C3D file, mesh, triangle, fill, or smoothing
claim.

## Implemented slice and evidence

- Library-Noah commit `8811ca260caf3a6640933624106df23146427d53` owns the
  source-neutral `ReferenceGridRegridTool`: orthonormal/right-handed profile
  checks, a bounded `4,194,304`-cell output, half-open projection, deterministic
  collision winner, preserved holes, coverage, and winner planar-distance
  evidence. Its Release smoke passes `33/33`.
- Studio vendors committed `Lib.ThreeD 2.7.1` with SHA-256
  `3A873D926764CCC6781413DA62DD7D2F6FDF050058BCEE231279C1C77CEC69DA`.
  The package verifier checks package ID/version/commit/hash/target.
- Studio Core owns immutable `C3DReferenceGridProfile` and
  `C3DTransformedHeightField`; Tools adapt one A2 cloud; the Runner golden
  passes `4/4` at `artifacts/current/a3-regrid-height-field-golden-final.txt`.
- The Workbench has explicit Preview/Publish/stale state, a typed WPF
  PropertyGrid, Artifact Registry entry, Tool Catalog/compatible-input route,
  dedicated custom-title A3 Tool Lab, and a line-based A3 Viewer renderer.
  Below-threshold coverage yields a Warning Preview with an output but leaves
  Publish disabled.

The Tool Lab screenshot automation was invoked from the current Shell build,
but this session's graphical host returned normally without producing the
requested capture file. Therefore current visual capture/quality evidence is
**not** claimed; it must be rerun in an interactive WPF desktop session.

## Included and excluded scope

| Included in A3 | Explicitly excluded |
| --- | --- |
| One current Published A2 cloud and one fully authored profile | New affine solve/application, registration, automatic surface finding, or reference-frame inference |
| Deterministic projection into a declared U/V/H grid | Interpolation, nearest-neighbour hole fill, smoothing, mesh/triangulation, C3D mutation or C3D output |
| Cell ownership, collision, out-of-bounds, hole, coverage, and canonical hash evidence | Physical calibration, uncertainty, Gauge R&R, engineering-unit or metrology claim |
| Explicit Preview/Discard/Publish/stale lifecycle, Runner parity, and a dedicated Tool Lab | Thickness, Warpage, generic graph execution, PLC/camera/robot/cloud integration |

`TransformedHeightField` is a new in-memory inspection artifact. It is never
silently substituted for raw C3D or for A2's ordered point cloud.

## ReferenceGridProfile v1

The A3 step owns one typed profile in its WPF PropertyGrid. There is no hidden
global calibration profile and no inferred default. The profile is persisted in
the teaching recipe and has its own canonical hash.

```text
ReferenceGridProfile
  ProfileVersion
  ReferenceFrameId / ReferenceUnit / Provenance / Revision
  OriginX / OriginY / OriginZ             // outer lower-left grid corner
  UAxisX / UAxisY / UAxisZ                // unit planar axis; output columns
  VAxisX / VAxisY / VAxisZ                // unit planar axis; output rows
  HAxisX / HAxisY / HAxisZ                // unit height axis
  ColumnPitch / RowPitch                  // positive reference-frame units
  ColumnCount / RowCount                  // finite bounded positive integers
  CellAssignmentPolicy = PlanarNearestCellCenter
  CollisionPolicy = NearestPlanarCenterThenSourceRowColumn
  OutOfBoundsPolicy = RejectPreview
  HolePolicy = PreserveMissing
  MinimumCoverageRatio                   // explicit 0..1 Publish gate
```

Validation is fail-closed before point iteration:

- origin, all axes, and pitches are finite;
- U/V/H are unit length within a documented numerical tolerance;
- U and V are orthogonal, and `cross(U,V)` agrees with H (right-handed);
- pitches are positive; dimensions are bounded to the implementation memory
  budget; `ColumnCount * RowCount` cannot overflow;
- profile frame/unit/provenance/revision are non-empty and agree with the
  current Published A2 reference frame/unit;
- MinimumCoverageRatio is explicit and in `[0, 1]`.

The profile describes a reference coordinate system. It does not state that
the reference unit is millimetres or that the transform is physically correct.

## Deterministic projection and cell assignment

For transformed point `q` and profile origin `o`:

```text
u = dot(q - o, U)
v = dot(q - o, V)
h = dot(q - o, H)

column = floor(u / ColumnPitch)
row    = floor(v / RowPitch)
cell center = ((column + 0.5) * ColumnPitch,
               (row + 0.5) * RowPitch)
planarDistanceSquared = (u - centerU)^2 + (v - centerV)^2
```

The half-open planar range is `[0, ColumnCount * ColumnPitch)` and
`[0, RowCount * RowPitch)`. A finite A2 point outside either range rejects the
Preview under v1 `OutOfBoundsPolicy = RejectPreview`; it is not clipped or
silently dropped.

Multiple points in one output cell are resolved deterministically by the
smallest planar distance to that cell centre. Exact distance ties select the
lowest original A2 source `Row`, then `Column`. The output records collision
count and winner source locator for every populated cell. This is a declared
sampling decision, not interpolation.

An empty output cell remains missing. A3 never invents an H value, carries a
neighbour across a hole, averages a collision, smooths a peak, or constructs a
face. `coverage = populatedCellCount / totalCellCount` is evidence. Preview
may show the incomplete field, but Publish is disabled until coverage is at
least the authored MinimumCoverageRatio.

## Output contract

```text
TransformedHeightField
  ContractVersion / OutputEntityId
  InputTransformedPointCloudEntityId / InputContentSha256
  RootSourceEntityId / RootSourceSha256
  ReferenceFrameId / ReferenceUnit / ReferenceProvenance / ReferenceRevision
  ReferenceGridProfileContentSha256
  Origin / UAxis / VAxis / HAxis
  ColumnPitch / RowPitch / ColumnCount / RowCount
  PopulatedCellCount / MissingCellCount / CollisionCount / CoverageRatio
  Ordered TransformedHeightCell[]             // row-major grid order
  Provenance / ContentSha256

TransformedHeightCell
  Row / Column / IsMissing
  HeightAlongH                                // only for populated cells
  WinnerSourceRow / WinnerSourceColumn        // only for populated cells
  WinnerPlanarDistanceSquared
```

The canonical SHA-256 includes profile identity, input cloud identity, all
grid metadata, quality counts, and every cell in row-major order. Recipes save
only the authored A3 step/profile; they never cache the generated field.

## Lifecycle and UI contract

```text
Ready (current Published A2 + valid profile)
  -> Preview (temporary TransformedHeightField)
  -> Discard
  -> Publish (the same Preview reference; no recomputation)
  -> Stale (A2 identity, profile, source lineage, or step route changes)
```

- Editing a profile marks the A3 output stale; it never recalculates.
- Tool Lab open, docking, camera movement, Viewer menu, selection, and display
  style changes never execute A3.
- The A3 Tool Lab has two reusable viewers: left is the Published A2 line
  cloud; right is the re-gridded field using line/grid display plus a missing
  cell overlay. It presents profile axes/origin, coverage, collisions,
  out-of-bounds state, input/profile/output hashes, and Preview/Publish state.
- WPF PropertyGrid owns only the authored profile. The output panel is
  read-only evidence. Icons retain text/tooltip/automation names.
- The main Workbench remains source/inspection context until the operator
  explicitly displays the Published A3 artifact.

## Responsibility boundaries

| Layer | Responsibility | Must not do |
| --- | --- | --- |
| Library-Noah `Lib.ThreeD` | Pure profile validation, projection/binning, deterministic collision selection, coverage, and canonical construction | Read C3D, persist recipes, own WPF state, infer a frame, or measure |
| Studio Core | Immutable profile/field contracts | Paths, mutable caches, or rendering |
| Studio Data | Persist/read profile and verify lineage | Projection math, Viewer scaling, or physical-unit inference |
| Studio Tools | Adapt A2 contract into Library-Noah and enforce exact route/current-Published state | Duplicate numerical re-grid math or create UI state |
| Runner | Replay the same A3 adapter and output hash | Alternate implementation or a hidden fill policy |
| Shell / Viewer | Explicit lifecycle and evidence presentation | Execute on opening, write source files, or imply calibration |

## Required evidence and current state

1. **Profile validation:** malformed/non-orthonormal/left-handed axes, missing
   provenance, non-finite values, non-positive pitch, oversize grid, and
   invalid coverage all fail closed.
2. **Independent projection:** synthetic identity, translated, rotated, and
   sheared A2 clouds are independently projected into the same declared U/V/H
   profile; every output cell and height matches.
3. **Boundary and missing:** negative/boundary/out-of-bounds samples, missing
   cells, holes, and cells at the half-open upper bound are deterministic.
4. **Collision:** nearest-planar-centre selection and row/column tie break are
   fixed by a golden suite; no average or fill is permitted.
5. **Determinism:** repeated Library-Noah, Studio Tools, and Runner results
   have one canonical output SHA-256.
6. **Lifecycle:** Preview/Discard/Publish/stale/cancel and profile edits are
   verified without automatic A2 execution.
7. **UI:** fresh Korean/English current-build captures at `1920 x 1080` and
   `1280 x 760` show the profile, line-based A2 input, grid output, missing
   evidence, disabled states, and no meaningful clipping.
8. **Real-data boundary:** only a real aligned A1/A2 output plus trusted
   profile provenance may validate an actual field. It does not establish
   physical calibration or Thickness/Warpage acceptance by itself.

Current synthetic coverage is deliberately narrow: the `4/4` Runner golden
fixes row-major cells/missing values/coverage, deterministic collision/hash,
out-of-bounds/reference-identity rejection, and typed recipe plus blocked
Publish. Library smoke separately fixes profile rejection and half-open bounds.
Rotated/sheared independent-reference projection, cancellation/stale lifecycle
automation, and the two required visual size/language captures remain pending.

## Owner decisions required

1. **Separate A3 contract:** A3 remains separate from A1/A2 and later
   measurement tools; only a current Published A2 cloud may enter it.
2. **Recipe-owned profile:** origin, axes, pitch, dimensions, frame/unit, and
   provenance are explicit typed A3 properties; no inferred global profile.
3. **U/V/H convention:** v1 uses a right-handed orthonormal reference basis
   and the half-open, cell-centre grid convention specified here.
4. **No interpolation/fill:** v1 preserves holes and uses only the declared
   nearest-planar-centre collision winner with source row/column tie break.
5. **Out-of-bounds fail closed:** any transformed point outside profile bounds
   rejects Preview rather than being clipped or silently skipped.
6. **Coverage publish gate:** Preview can expose missing cells; Publish needs
   an explicit recipe-owned MinimumCoverageRatio.
7. **Library-Noah ownership:** numerical validation/binning/collision/hash
   code lives in `Lib.ThreeD`; Studio stays adapter/UI/Runner only.
8. **Measurement boundary:** A3 output does not automatically enable
   Thickness/Warpage. Those require separate typed contracts and evidence.

## Preconditions and durable closure

The project currently has no real four-anchor package, published A1 matrix,
trusted source-to-reference mapping profile, or physical calibration evidence.
This design uses no fabricated values and leaves all of them unverified.

```text
Status: Incomplete
Scope: Deterministic A3 ReferenceGridProfile -> TransformedHeightField core,
  package boundary, typed adapter, Runner golden, Workbench lifecycle, Tool
  Lab, and line-based Viewer surface.
Acceptance criteria: Library smoke `33/33` -> pass; package verifier -> pass;
  A3 Runner golden `4/4` -> pass; full Studio build `0 warning / 0 error` -> pass;
  current interactive Tool Lab capture -> not produced in this host; real A1/A2
  data/profile validation -> unavailable.
Verification: Library Release build/smoke `33/33`; full Studio Debug build
  `0 warning / 0 error`; vendored package verifier; A3 Runner golden `4/4`;
  and A2 regression `4/4` were run on 2026-07-21 from the current Debug
  Runner output (not the stale `Any CPU` output path).
Evidence: this document; `artifacts/current/a3-regrid-height-field-golden-final.txt`;
  `artifacts/current/a2-affine-apply-golden-regression-final.txt`;
  `artifacts/current/a3-library-noah-bridge-regression-final.txt`;
  `artifacts/current/a3-library-noah-package-final.txt`; Library-Noah commits
  `cc879e0` and `8811ca2`.
Boundary / next dependency: rerun current Shell A3 Tool Lab capture in an
  interactive WPF desktop session, then use a real Published A1/A2 chain plus
  trusted Mapping Profile provenance. Do not claim physical calibration,
  Thickness, or Warpage from this slice.
```
