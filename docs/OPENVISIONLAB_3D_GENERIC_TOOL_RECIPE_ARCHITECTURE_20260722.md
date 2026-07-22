# Generic Tool Recipe Architecture

Date: 2026-07-22  
Status: Accepted implementation direction

## Decision

OpenVisionLab 3D Studio is not a Thickness/Warpage teaching application. It is a local, rule-based 3D inspection recipe workbench. Thickness, Warpage, Plane Flatness, Point Pair, Gap/Flush, Volume, and Cross-section Dimensions are reusable measurement tools in a larger catalog; they must never own the workspace, recipe lifecycle, or product title.

The canonical product path is:

```text
Recipe Manager
  -> ToolRecipeDocument
  -> ordered ToolRecipeStep graph
  -> typed INPUT entity IDs
  -> PropertyGrid parameters and recipe-owned selections
  -> explicit Preview
  -> explicit Publish
  -> typed OUTPUT entity IDs
  -> save/reopen
  -> headless Runner replay
```

This follows the useful GoPxL pattern—catalog, ordered tool chain, selected-tool configuration, 3D output display, and explicit outputs—without copying its visual design or expanding into sensor/controller platform scope.

## Canonical ownership

- `ToolRecipeDocument` is the canonical multi-tool inspection recipe contract.
- `ToolWorkbenchViewModel` owns the editable recipe session, dirty state, selected step, PropertyGrid draft, Preview/Publish state, and entity navigation.
- `Core` owns recipe and typed entity contracts.
- `Data` owns C3D/file identity and loading.
- `Tools` owns strict ToolRecipe-to-algorithm adapters and calls Library-Noah for numerical algorithms where applicable.
- `Shell` presents tools and evidence; it does not own inspection math.
- `Runner` must replay the same ordered recipe adapters without UI shortcuts.

## UI rules

- The main workspace is named `Inspection Recipe` / `검사 레시피`.
- The former Thickness/Warpage task page is not a product navigation destination.
- Toolbox categories are Prepare, Feature & Datum, Transform, Measure, and Review.
- Every algorithm appears as a step with visible `INPUT -> OUTPUT` routing.
- Parameters use the common WPG PropertyGrid adapter.
- ROI and point selections belong to the recipe and are routed as typed entity IDs.
- Editing never runs an algorithm. Preview and Publish remain explicit.

## Recipe file rules

- New generic files use `*.ov3d-recipe.json`.
- Existing `*.ov3d-teach.json` ToolRecipeDocument files remain readable as a legacy filename convention.
- Old single-purpose `c3d-thickness` and `c3d-warpage` recipe types remain compatibility inputs until an explicit importer is completed; they are not the future authoring model.
- A recipe may contain multiple Thickness, Warpage, filtering, feature, datum, transform, and review steps.

## Height measurement inputs

Thickness and Warpage now share the generic lifecycle:

```text
verified raw C3D HeightField + source-owned GridRectangle
  OR
Published TransformedHeightField + artifact-owned GridRectangle
  -> Thickness or Warpage PropertyGrid parameters
  -> Preview result and evidence
  -> Publish MeasurementResult
```

Thickness parameters: minimum, maximum, minimum valid samples.  
Warpage parameters: maximum peak-to-valley, maximum RMS, minimum valid samples.

Both adapters call the existing Library-Noah-backed rules. They do not create a separate recipe/session/window.

Plane Flatness adds a separate three-input contract:

```text
Published TransformedHeightField
  + reference GridRectangle
  + measurement GridRectangle
  -> Plane Flatness PropertyGrid parameters
  -> Preview result and evidence
  -> Publish MeasurementResult
```

Its v1 input order is fixed and both ROIs must be owned by the exact same A3
artifact. Raw C3D is intentionally rejected because the tool needs an explicit
reference frame and unit.

Gap/Flush adds an ordered three-input contract:

```text
Published TransformedHeightField
  + first GridRectangle
  + second GridRectangle
  -> Gap/Flush PropertyGrid parameters
  -> Preview result and evidence
  -> Publish MeasurementResult
```

The first/second ROI order determines the signs of gap and flush. Both ROIs
must be owned by the same exact A3 artifact. Raw C3D is rejected by this
generic adapter; the legacy raw-C3D recipe remains a compatibility path.

Artifact-owned selections use recipe schema `1.3` and record the exact owner entity ID, artifact SHA-256, root-source SHA-256, grid dimensions, unit, and frame. Save/reopen preserves those fields. A reopened ROI becomes executable only after the same A3 output is Published again; mismatched owner, bytes, grid, unit, frame, or root source fails closed.

Cross-section Dimensions consumes one Published A3 plus one `GridRectangle`
spanning exactly one row and at least two columns. Library-Noah owns the
source-neutral U-width/H-range arithmetic and independent acceptance. Studio
owns exact A3 binding, row policy, U/H adaptation, WPG, result evidence, and
Runner routing. The generic UI does not reuse the legacy raw-height wording.

## Current boundary

The Generic Ordered Recipe Executor v1 accepts one explicit Published A2 `TransformedPointCloud`, executes the authored A3 Re-grid step, then executes every following Thickness, Warpage, Plane Flatness, Point Pair Dimensions, Gap/Flush, Volume, or Cross-section Dimensions step in authored recipe order using the resulting artifact-owned selection inputs. A tolerance `Fail` remains evidence and does not suppress later measurements. V1 rejects any other downstream tool rather than pretending to support an arbitrary graph. It does not invent, auto-publish, or reconstruct A1/A2.

Real four-landmark A1/A2 source evidence, arbitrary whole-graph execution, physical scale, calibration, metrology trust, camera/PLC/robot/cloud integration remain outside this gate.

## Acceptance evidence

- Full solution build: zero warnings, zero errors.
- Generic measurement Workbench verification: supported typed PropertyGrid adapters, explicit Preview/Publish, `*.ov3d-recipe.json` save, and reopen pass `27/27`.
- Artifact-owned selection and ordered Runner verification: schema/route, legacy single-sequence compatibility, seven-measurement authored order, direct-adapter hash parity, continued evidence after tolerance Fail, ROI/PointSet save/reopen, and wrong owner/hash/grid/order/tool/output rejection pass `18/18`.
- UI smoke: current-source 1920 x 1080 Workbench capture must show the generic Inspection Recipe navigation and no Thickness/Warpage product-mode selector.
