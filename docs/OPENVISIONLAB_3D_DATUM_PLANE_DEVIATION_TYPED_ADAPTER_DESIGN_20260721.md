# OpenVisionLab 3D C3D Datum Plane Raw-Height Deviation v1 Design

Updated: 2026-07-21

Status: **Complete for the local raw-height software slice.** The typed
Library-Noah algorithm, Studio adapter, recipe route, Workbench lifecycle,
Viewer overlay, Tool Lab, and Runner replay are implemented and verified.
This remains neither a physical datum nor a calibrated metrology claim.

## Decision

The first safe consumer of the published manual `PlaneFeature` is a narrow
typed local inspection tool:

```text
raw C3D HeightField + published oriented PlaneFeature + GridRectangle
  -> DatumPlaneDeviationResult
```

Its name is **C3D Datum Plane Raw-Height Deviation**. It makes the
datum construction and the inspected surface explicit, while keeping the
source C3D unchanged. It is deliberately not called physical flatness,
warpage, thickness, affine alignment, or re-grid.

This is preferable to connecting `PlaneFeature` directly to an existing
Warpage or Thickness step: those tools have different reference semantics and
would otherwise conceal the required datum, source-lineage, and raw-height
boundaries. It is also a more useful first consumer than Plane-Plane
Intersection, which creates geometry but has no current inspected result or
fixture-backed acceptance use.

## Why the existing Plane Flatness rule is not reused

`PlaneFlatnessRule` is a completed numeric-reference-ROI tool. It first fits a
plane from its own reference samples and then evaluates signed Euclidean point
distance. The manual `C3DThreePointPlaneFeature` instead carries an authored
full-XYZ plane whose current coordinate convention is:

```text
X = grid column
Y = raw-height
Z = grid row
```

`X`, `Y`, and `Z` therefore do not yet have a demonstrated common physical
unit. Reusing Euclidean signed distance would imply a geometric unit that the
input does not establish. The new consumer must instead report a deliberately
named **raw-height residual** and retain the existing Plane Flatness behavior
unchanged.

## Exact v1 contract

### Required inputs

| Input | Contract | Required current-state rule |
| --- | --- | --- |
| `source.c3d.*` | Raw C3D height field | Current, finite, loadable C3D source. |
| `derived.*-plane.*` | Published `C3DThreePointPlaneFeature` | Must resolve from the exact current recipe source and be published, not Preview, stale, declared, or manually reconstructed. |
| `selection.*-measurement.*` | Recipe-owned `GridRectangle` | Must bind to the same current source bytes, grid width/height, and `frame.c3d-grid-index`; it defines the inspected cells only. |

The source entity ID, source SHA-256, unit, frame ID, coordinate convention,
and grid binding must match across all three inputs. A plane from a different
source, a replacement source, a stale plane, a changed three-point selection,
or a stale measurement rectangle is an **Error**, never an implicitly repaired
or best-fit result.

The direct source flow is intentionally small:

```text
Raw C3D ──> PointSet(3) ──> 3-Point Plane ──> PlaneFeature ──┐
     │                                                        ├─> Datum Plane Raw-Height Deviation
     └──────────────────────────────> GridRectangle ─────────┘
```

The Runner reconstructs the direct 3-Point Plane prerequisite from the same
recipe and source bytes before it evaluates the consumer. This is a narrow
typed dependency, not a generic graph executor, auto-route, or cross-session
artifact cache.

### Raw-height residual equation

The published plane is:

```text
n.x * X + n.y * Y + n.z * Z + d = 0
```

For each finite C3D cell `(column, rawHeight, row)` in the measurement
rectangle, the consumer solves the expected raw-height on that plane:

```text
expectedRawHeight = -(n.x * column + n.z * row + d) / n.y
rawHeightResidual = rawHeight - expectedRawHeight
```

The output is valid only where `abs(n.y) >= MinimumAbsoluteNormalY` (default
`0.1`). This explicit height-field-orientation validity policy is not a
physical product tolerance: a plane near vertical cannot be represented
stably as one raw height for each grid cell. The boundary prevents an
unbounded division from becoming a plausible-looking result.

Reversing the authored plane normal reverses both `n` and `d`, so it yields the
same predicted raw height and the same residual values. The ordered normal is
still retained in `PlaneFeature` for later orientation-aware consumers.

### Parameters and result

| Item | Proposed v1 behavior |
| --- | --- |
| `OutputRole` | Required text, for example `DatumPlaneDeviation`. It is traceability only. |
| `MaximumPeakToValleyRawHeight` | Required positive finite local recipe limit. Its name and result unit remain `raw-height`; it is not a physical tolerance. |
| Residual mode | Fixed/read-only: `RawHeightMinusDatumPlanePredictedRawHeight`. |
| `MinimumValidSampleCount` | Required typed recipe policy, integer `>= 3`. Fewer valid cells are an Error. |
| `MinimumAbsNormalY` | Required typed recipe policy in `(0, 1]`; the default is `0.1`. It remains visible in the result provenance rather than being hidden as a display setting. |

`DatumPlaneDeviationResult` contains, at minimum:

```text
minimumRawHeightResidual
maximumRawHeightResidual
peakToValleyRawHeight
rootMeanSquareRawHeightResidual
validSampleCount
missingSampleCount
planeFeatureSha256
measurementSelectionSha256
sourceSha256
status (Pass / Fail / Error)
```

`Pass` means only that the calculated local raw-height P2V is within the
explicit local raw-height limit. `Fail` means it exceeds that limit. `Error`
means the typed input/lineage/geometry contract could not be satisfied. None
of the states establish an engineering unit, physical datum, GD&T flatness,
or sensor measurement capability.

## Ownership and lifecycle

| Owner | Proposed responsibility | Explicitly excluded |
| --- | --- | --- |
| Library-Noah | Source-neutral `DatumPlaneRawHeightDeviationInspectionTool`: finite point validation, `n.y` gate, predicted-height residual statistics, P2V/RMS, and status. | C3D, recipe, ROI coordinates, WPF, Viewer, hashes, or source provenance. |
| Studio Core | Immutable result/provenance contract. | General-purpose graph or mutable source transform. |
| Studio Data | Current C3D loading and source/grid binding validation. | Numerical plane/deviation implementation. |
| Studio Tools adapter | Rectangle sampling, direct PlaneFeature validation, Noah adaptation, typed ToolResult/overlays. | Re-fitting a plane or duplicating Noah numerical math. |
| Shell/ViewModel | Typed WPG draft, explicit Preview/Publish, stale propagation, Tool Lab command/state, artifact projection. | Hidden execution or source mutation. |
| Viewer | Read-only measurement rectangle, datum triangle/normal from input plane, extrema, and residual color overlay after Preview. | Affine application, re-grid, camera-state ownership, or selection mutation. |

The state transition must remain explicit:

```text
Plane taught -> Plane Preview -> Plane Published
Measurement rectangle taught -> Datum Deviation Ready
  -> explicit Preview -> Preview ready -> explicit Publish
source / plane / rectangle / parameter change -> Datum result stale
```

Opening a Tool Lab, selecting a node, changing display options, opening a
recipe, or merely making an artifact visible must not Preview, Publish, or
modify the C3D source. A PlaneFeature change stales this consumer only; it
does not trigger any Thickness, Warpage, affine, or re-grid work.

## Workbench and Tool Lab design

The step is shown as one typed GoPxL-style route with visible ports:

```text
Inputs: Raw C3D | Published PlaneFeature | Measurement GridRectangle
Output: DatumPlaneDeviationResult (raw-height)
```

The WPG shows input IDs and current lineage as read-only fields above the
small editable limit/role group. It must show a concise blocking message when
the plane is not published/current or `abs(n.y)` is below policy; it must not
offer a fallback fit button.

Its dedicated modeless Tool Lab follows the existing single-instance,
custom-titlebar, dockable-view style:

```text
Left viewer:  raw C3D + P1/P2/P3 + datum triangle/normal + measurement rectangle
Right viewer: same raw C3D + read-only residual color overlay + min/max markers
Bottom review: input hashes, normal-Y policy, P2V/RMS/counts, explicit status
```

The right viewer is a visualization of the derived result; it is not a new
source map. Height/distribution legend and profile tools may consume the
result only through a later separately designed display contract.

## Verification plan

1. **Library-Noah golden cases**: exact zero residual; positive/negative
   residual extrema; normal reversal invariance; invalid/non-finite values;
   `n.y` orientation rejection; insufficient samples; invalid limit.
2. **Studio adapter cases**: exact source SHA/frame/grid equality; stale or
   Preview-only plane rejection; changed three-point selection rejection;
   rectangle binding/edge/missing-cell cases; source immutability; no
   re-fit/fallback path.
3. **Lifecycle and Runner parity**: Preview and Publish reuse one immutable
   result; a changed input stales it; headless Runner reproduces input/result
   hashes and status from the same recipe/source; JSON/HTML/CSV record the
   local-unit boundary.
4. **Current UI evidence**: fresh before/after captures from a current build;
   custom-titlebar/single-instance Tool Lab smoke; visible typed ports, blocked
   states, accessibility names, and explicit Preview/Publish behavior.
5. **Actual-input exercise**: if approved, execute the named local C3D only
   as a deterministic raw-height software exercise. Record its hash and
   result, but do not treat it as external ground truth or a physical result.

## Implementation evidence - 2026-07-21

The approved slice is implemented with the following ownership boundary:

- Library-Noah commit `986f04346af6fea1d627e7a8fa5a56f6f9c0117a` supplies
  `DatumPlaneRawHeightDeviationInspectionTool` in vendored `Lib.ThreeD`
  `2.5.1` (package SHA-256
  `849E8C1B264DBF2E6D721E6F9865B1EFE9F0C935DCFF14D72E5A3E85383BBC75`).
- Studio Core owns immutable `DatumPlaneDeviationResult` provenance; Tools
  owns only raw-C3D/PlaneFeature/GridRectangle lineage and deterministic
  display sampling; Shell owns typed WPG plus explicit Preview/Publish/stale
  state; Viewer draws a read-only residual overlay without creating a map.
- Runner reconstructs the exact 3-Point Plane prerequisite from the same
  recipe/source before it evaluates Datum Deviation. `Pass` and `Fail` are valid
  calculation results; only invalid lineage/geometry/input is `Error`.

Current-source verification passed:

```text
dotnet build OpenVisionLab.ThreeDStudio.slnx -c Debug -p:Platform="Any CPU" --no-restore  -> 0 warnings, 0 errors
Runner --verify-c3d-datum-plane-deviation --report ...                                  -> 5/5
Shell --verify-tool-datum-plane-deviation-workbench ...                                 -> 12/12
Runner --verify-library-noah-3d --report ...                                            -> 7/7
```

The current Thickness C3D UI fixture is stored under
`artifacts/ui/20260721-datum-plane-deviation/`. It uses source SHA-256
`79C027...F0299C`, publishes the manual plane, then publishes one Datum result:
`P2V=3388.17573928833 raw-height`, `RMS=1004.1184400042351`, `9,245` valid
cells, and `755` missing cells. This is deterministic software/UI evidence
only; its deliberately broad local limit is not an inspection tolerance.

Fresh current-build UI evidence includes the closest reproducible pre-change
structural baseline
`closest-baseline-three-point-plane-tool-lab.png` and the implemented Datum
Tool Lab capture `after-datum-plane-deviation-tool-lab.png`. Both screenshot
quality reports passed on their first capture attempt. A true pre-change Datum
Tool Lab image cannot exist because that window was introduced by this slice.

## Current input readiness audit

This turn rechecked the two user-designated C3D files:

| Path | Bytes | Current SHA-256 | Consequence |
| --- | ---: | --- | --- |
| `3D/Thickness/Ori_20240116_094414.C3D` | `10,236,276` | `79C02761F9B711C0F8980D4376B9FCE25E00D425E6CA85DA4D4349ECF5F0299C` | Eligible only as one named local raw-height source. |
| `3D/Warpage/Ori_20240116_094430.C3D` | `10,236,276` | `79C02761F9B711C0F8980D4376B9FCE25E00D425E6CA85DA4D4349ECF5F0299C` | Byte-identical alias; not a second acquisition or independent sample. |

The prior C3D input preflight records a `1301 x 1967` float grid and no
embedded sensor, unit, calibration, reference-plane, or acquisition identity.
The current files therefore support deterministic software and UI evidence
only. They do not supply the fixture/nominal/physical-unit/repeated-acquisition
evidence needed for a physical datum, flatness, Warpage, or measurement-system
claim. See `OPENVISIONLAB_3D_WARPAGE_INPUT_PREFLIGHT_20260717.md`.

## Alternatives deliberately not selected

| Candidate | Decision | Reason |
| --- | --- | --- |
| `PlaneFeature -> Datum Plane Raw-Height Deviation` | **Recommended after approval** | One clear local inspection output with source, datum, ROI, and result visible. |
| `PlaneFeature -> existing PlaneFlatnessRule` | Not connected | It fits its own plane and reports Euclidean distance; it is a different established contract. |
| `PlaneFeature -> Warpage/Thickness` | Blocked | No manual-datum consumer contract or physical evidence; current files are aliases. |
| `PlaneFeature -> affine apply/re-grid` | Blocked | Three points define a plane, not a full source-to-reference XYZ affine correspondence or a resampling policy. |
| `PlaneFeature -> Plane-Plane Intersection` | Deferred | It adds geometry but no currently evidenced inspection decision. |

## Owner decisions recorded for this implementation

1. The owner approved **C3D Datum Plane Raw-Height Deviation** as the first
   PlaneFeature consumer with explicit local raw-height P2V status semantics.
2. The owner confirmed that the current Thickness C3D may be used only for a
   deterministic software/UI exercise, not physical validation. It is the
   same bytes as the Warpage alias.
3. When physical validation is desired, provide a distinct acquired source
   with source unit/frame/alignment provenance plus an expected datum/ROI/result
   or an independently trusted comparison. Until then, physical results remain
   blocked.

## Completion record

Status: **Complete**

Scope: Implemented the narrow `raw C3D + Published PlaneFeature +
GridRectangle -> DatumPlaneDeviationResult` typed inspection slice, including
Library-Noah numerical ownership, Studio provenance, explicit lifecycle,
Runner replay, Viewer evidence, and one modeless Tool Lab.

Acceptance criteria: typed source/plane/ROI route -> Pass; no plane re-fit or
source mutation -> Pass; Preview/Publish/stale lifecycle -> Pass; Runner
replay -> Pass; current Shell Tool Lab capture -> Pass; physical/metrology
claim deliberately absent -> Pass.

Verification: `OpenVisionLab.ThreeDStudio.slnx` Debug build `0 warnings / 0
errors`; Library-Noah package verifier `7/7`; Datum Golden/Runner `5/5`;
Datum Workbench verifier `12/12`; first-attempt current-build Tool Lab
captures and quality reports in `artifacts/ui/20260721-datum-plane-deviation/`.

Evidence: this document; `artifacts/ui/20260721-datum-plane-deviation/`; and
the fixed local C3D SHA-256 stated above.

Boundary / next dependency: the named C3D is uncalibrated and byte-identical
to the Warpage alias. A distinct acquired source with unit/frame/alignment
provenance and an independent expected datum/ROI/result is required before
physical validation, Thickness/Warpage coupling, or metrology claims.
