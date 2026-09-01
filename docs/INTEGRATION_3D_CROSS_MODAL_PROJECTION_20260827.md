# 3D cross-modal coordinate projection

Date: 2026-08-27
Status: **Implemented for the bounded raw-height adapter slice**

`ThreeDIntegrationHeightMapRunner` now optionally consumes a paired 2D/Image
transaction when the 3D Handoff contains a `coordinate-projection-profile`
artifact. It verifies the profile, paired Handoff, paired profile, completed
2D Result, and 2D Run Record before producing any projection points.

## Directional behavior

- `2D -> 3D`: every persisted 2D overlay point (or its center when no points
  are present) is mapped into the C3D grid. The immutable raw-height snapshot
  is sampled with nearest-neighbor indexing and the point records
  `Valid`, `Missing`, or `OutsideGrid`.
- `3D -> 2D`: the recipe-owned inspection ROI's four corners and center are
  mapped back to source-image coordinates. The result records the raw sample,
  image-bound status, and inspection status.
- Both lists are written to `artifacts/coordinate-projection-result.json` as
  Result evidence with paired transaction IDs, run IDs, dimensions, and the
  profile identity.

The sidecar is schema `1.0`: image `px`/top-left, grid `raw-height` with frame
`frame.c3d-grid-index`/top-left, and `normalized-linear` mapping. This is a
software coordinate convention; it is not calibration or metrology.

## Ownership boundary

The Reporting adapter owns the paired transaction and raw-height projection.
The 3D Viewer/Workbench remains responsible for native display state and does
not rerun inspection or mutate recipe/source/ROI state when evidence is read.
No cross-repository project reference was introduced; the 3D reader mirrors
the JSON contract locally.

## Verification

The final Release cross-process smoke passed:

`D:\OpenVisionLab-TestData\OpenVisionLab-CrossRepo\projection\cross-modal-projection-20260827-163714-c93911d218f94f7e9b4586a7bda7dcae\3d-consumer-evidence.txt`

- raw C3D buffer: `1280x840`, materialized successfully;
- recipe-owned ROI: `row:156,column:178,rows:144,columns:110`;
- Result: `Accepted` / `Completed` / `Pass`;
- `2D -> 3D` points: `4`;
- `3D -> 2D` points: `5`;
- projection evidence: `artifacts/coordinate-projection-result.json`;
- the separate external ImageCanvas screen consumer read the same Result and
  rendered five read-only reverse-projected markers; screen evidence is in
  `...\2d-screen-consumer\cross-modal-2d-screen-smoke.txt`;
- separate 3D Reporting tests: `10/10` via the repository's xUnit
  in-process `dotnet run` entrypoint;
- 3D Reporting build: `0` warnings, `0` errors.

The D-backed exchange and full summary are in the same run root. The 3D
adapter's existing no-profile HeightMap path remains compatible and publishes
the normal Run Record without projection evidence. The external ImageCanvas
consumer is the verified graphical presentation boundary; the main application
Pipeline Review remains separate.

## Boundary

No calibrated transform, distortion model, camera pose, point-cloud/mesh
registration, PLC/robot/MES/cloud integration, automatic execution, release,
or deployment behavior is implied. The current cross-modal slice produces
validated coordinate evidence, read-only Machine Studio status, and verified
read-only marker presentation in the external ImageCanvas consumer.
