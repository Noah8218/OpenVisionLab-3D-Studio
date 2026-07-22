# Artifact-owned ROI and ordered Runner

Date: 2026-07-22  
Status: Complete for the bounded A3 -> supported measurement slice

## Scope

Bind recipe `GridRectangle` and `PointSet` selections to one exact Published `TransformedHeightField`, preserve them through save/reopen, use them in Viewer teaching and measurement Preview, and replay A3 followed by authored Thickness, Warpage, Plane Flatness, Point Pair, Gap/Flush, Volume, and Cross-section Dimensions steps through the same Tools adapters.

## Contract

```text
explicit Published A2 TransformedPointCloud
  -> authored A3 Re-grid Height Map
  -> Published TransformedHeightField
       owner entity ID
       artifact SHA-256
       root-source SHA-256
       grid width/height
       reference unit/frame
  -> artifact-owned GridRectangle(s) or PointSet(2)
  -> Thickness, Warpage, Plane Flatness, Point Pair, Gap/Flush, Volume, and/or Cross-section Dimensions in authored recipe order
  -> ordered MeasurementResults
```

- Recipe schema `1.3` adds optional artifact ownership fields to `ToolRecipeSelectionSourceBinding`.
- A raw C3D selection remains the schema-compatible four-field binding used by older recipes.
- An artifact-owned selection is valid only when its first measurement input equals the recorded owner entity ID.
- Reopen preserves the ROI but does not pretend its runtime artifact is current. A3 must be Published again with the exact recorded identity before Preview.
- Viewer teaching switches to the Published A3 wire grid, picks populated reference-grid cells, and draws the candidate/applied ROI in that display frame.
- Editing and picking do not execute inspection. Preview and Publish remain explicit.
- A tolerance `Fail` is a completed measurement result; later measurements still execute. Route or adapter `Error` fails closed.
- V1 rejects an unsupported downstream tool and does not claim arbitrary whole-graph execution.

## Acceptance record

```text
Status: Complete
Scope: TransformedHeightField-owned GridRectangles/PointSet(2), save/reopen, Viewer capture path, shared measurement adapters, and bounded A3 -> multiple-measurement Runner sequence.
Acceptance criteria:
- schema and typed route -> pass
- authored seven-measurement order and aggregate status -> pass
- direct adapters and ordered Runner measurement hashes match -> pass
- earlier tolerance Fail preserves later measurement evidence -> pass
- artifact-owned ROI save/reopen -> pass
- wrong owner, artifact hash, grid, sequence order, downstream tool, and duplicate output -> rejected
- legacy raw-C3D measurement workflow -> retained
Verification:
- dotnet build OpenVisionLab.ThreeDStudio.slnx -c Debug -p:Platform="Any CPU"
- Runner --verify-artifact-owned-roi-runner -> 18/18
Evidence: artifacts/verification/20260722-generic-cross-section/ordered-runner.txt
Boundary / next dependency: sequence begins with an explicit Published A2 and supports only A3 followed by Thickness/Warpage/Plane Flatness/Point Pair/Gap-Flush/Volume/Cross-section Dimensions. Real four-landmark A1/A2 data and arbitrary whole-graph replay are not proven. No automatic feature detection, calibrated physical volume/dimension, or metrology claim.
```

## Reusable checklist

When another transformed-grid measurement tool is added:

1. Route the Published `TransformedHeightField` as the first input.
2. Capture the ROI while that exact artifact is visible.
3. Store all artifact ownership fields; never bind only by row/column.
4. On reopen, republish upstream and require exact binding verification.
5. Compare direct adapter and ordered Runner output hashes.
6. Add wrong-owner, wrong-hash, wrong-grid, and wrong-order rejection cases.
