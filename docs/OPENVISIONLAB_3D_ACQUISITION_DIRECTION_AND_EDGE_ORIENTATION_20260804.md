# Acquisition Direction and Edge Orientation

Date: 2026-08-04

Backlog item: `K-04`

Status: Complete for the documented software scope

## Outcome

OpenVisionLab 3D Studio now accepts an explicit acquisition direction in the
source coordinate frame and uses it to classify existing model-edge declared
normals as `SensorFacing`, `AwayFromSensor`, or `Grazing`.

This is an evidence and display feature. It does not change surface matching,
the separate edge score, acceptance limits, or Pass/Fail. It does not infer a
camera position or direction from model geometry.

## Short operator workflow

1. Open the measured 3D source.
2. Open **Source Quality**.
3. Set acquisition evidence to **Evidence available** and enter the evidence
   and limitations.
4. Set structured acquisition direction to **Direction available**.
5. Enter the XYZ vector using the visible `Sensor → scene` convention and the
   displayed source frame.
6. Choose **Apply source contract**. The vector is normalized before it is
   stored. Apply changes the recipe but does not Preview, Publish, Run, or
   Validate.
7. When identified edge diagnostic evidence is displayed, review the
   direction marker and the facing/away/grazing normal endpoints.

Example: entering `(0, 0, -2)` stores `(0, 0, -1)`. The magnitude has no
distance meaning; only direction is retained.

## Persisted recipe contract

`ToolRecipeAcquisitionProvenance.AcquisitionDirection` is optional so recipes
saved before K-04 remain readable. A missing property is displayed as the
legacy-compatible **Direction unavailable** fallback and does not dirty the
recipe.

```json
{
  "state": "Available",
  "evidence": "Station S-04 acquisition record ACQ-20260804-17.",
  "limitationNotes": "Direction supplied; camera pose and calibration unavailable.",
  "acquisitionDirection": {
    "state": "Available",
    "convention": "SensorToScene",
    "frameId": "frame.c3d-grid-index",
    "vector": { "x": 0, "y": 0, "z": -1 }
  }
}
```

Contract rules:

| Rule | Result |
| --- | --- |
| Direction state is `Available` | Provenance must also be `Available` |
| Available vector | finite, non-zero, normalized on Apply |
| Direction frame | exact ordinal match with the source frame |
| Convention | fixed to `SensorToScene` |
| Direction state is `Unavailable` | vector must be absent |
| Direction property is absent | clean legacy fallback; no inference |
| Source changes | source-scoped provenance and direction reset |

Camera position, extrinsics, intrinsics, lens model, calibration, and capture
timing are not part of this contract.

## Numerical ownership

Reusable orientation mathematics is owned by the public sealed
`Lib.ThreeD.FeatureExtraction.AcquisitionDirectionOrientationTool` in
Library-Noah. The Studio adapter supplies the explicit direction and the
already identified model-edge declared normals, then maps the controlled
result into Studio evidence.

Exact consumed source and package:

| Item | Identity |
| --- | --- |
| Library-Noah commit | `9dd95690d3e439b459c39aea99878880cdcc5808` |
| NuGet package | `Lib.ThreeD 2.9.1` |
| Package SHA-256 | `BDE8D2C01B6DC380EF4579C89DE495F06F79BA4864D4229CD5CE87713BD1CA4E` |
| Direction semantics | `sensor-to-scene-normal-orientation-v1` |

The Tool normalizes the supplied vectors, preserves source order, calculates
the alignment cosine, and uses the authored grazing threshold. Studio does not
duplicate this arithmetic.

## Evidence chain and Viewer behavior

`SurfaceEdgeAcquisitionDirectionArtifact` is a separate, content-addressed
display artifact. It links:

- the exact `SurfaceEdgeDiagnosticOverlayArtifact` SHA-256;
- the identified source SHA-256;
- the exact source/overlay frame;
- the normalized sensor-to-scene direction;
- the grazing threshold;
- one ordered cosine and orientation for every model edge.

The existing edge overlay schema remains unchanged. The Viewer keeps the
existing model/scene edge and purple declared-normal display, then adds:

- cyan endpoints for sensor-facing normals;
- magenta endpoints for normals pointing away from the sensor;
- amber endpoints for grazing normals;
- a blue sensor-to-scene direction marker;
- facing/away/grazing counts and the frame ID.

Changing the saved acquisition direction invalidates only this orientation
artifact. The raw edge overlay, surface/edge score, and assessment remain
unchanged and no matching execution is triggered.

## Fail-closed behavior

The orientation artifact is not created when direction evidence is missing,
Unavailable, non-finite, zero length, in a different frame, linked to a
different edge overlay, or content-hash invalid. Geometry is never used as a
fallback source of acquisition direction.

## Verification evidence

Physical evidence root:

`D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\20260804-k04-acquisition-direction\`

| Gate | Result |
| --- | --- |
| Library-Noah Release / Smoke | `0/0`, `138/138` |
| Studio Library-Noah package bridge | `26/26` |
| Direction artifact, frame, tamper, unchanged raw identity | `5/5` |
| Source contract save/reopen/legacy/no-execution | `17/17` |
| Edge Workbench routing and direction stale behavior | `16/16` |
| Runner edge foundation / diagnostic review | `21/21`, `20/20` |
| Current Debug solution build checkpoints | `0` warnings, `0` errors |
| Wide 1920×1040 current EXE Source Quality smoke | Pass; capture accepted attempt 1 |
| Compact 1280×760 current EXE Source Quality smoke | Pass; capture accepted attempt 1 |

Both EXE captures were dynamically placed on the leftmost monitor,
`\\.\DISPLAY2`, bounds `(-1920,365)-(0,1445)`. The Wide and Compact window
rectangles intersected that monitor. The current dark theme was checked in the
direction-available state with keyboard focus on Z; disabled direction inputs
remain covered by the existing unavailable/default state.

Before evidence:

- `before/before-wide-source-quality.png`;
- `before/before-compact-source-quality-focused.png`.

After evidence:

- `after/after-wide-direction-available-focus.png`;
- `after/after-compact-direction-available-focus.png`.

## Boundaries

This completion does not establish camera integration, calibration,
reconstruction, visibility/occlusion analysis, physical metrology,
cross-hardware performance, or production readiness. It does not change the
explicit Preview/Publish/Run/Validation actions or combine surface and edge
scores into a weighted score.

## Durable completion record

Status: Complete

Scope: Explicit source-frame SensorToScene contract, normalized save/reopen,
legacy fallback, Library-Noah normal-orientation classification, linked
display artifact, Viewer legend/marker, and direction-only stale handling

Acceptance criteria: explicit/backward-compatible contract -> pass `17/17`;
Noah-owned deterministic classification -> pass `138/138` and package bridge
`26/26`; fail-closed linked artifact -> pass `5/5`; raw score/assessment
unchanged and Apply does not execute -> pass Workbench `16/16`; current EXE
Wide/Compact theme/layout -> pass and screenshot quality accepted

Verification: commands and reports are retained in the D-drive evidence root
above; solution and focused projects built with zero warnings and errors

Evidence: this document, exact vendored `Lib.ThreeD.2.9.1.nupkg`, focused text
reports, and current EXE before/after PNGs

Boundary / next dependency: no inferred viewpoint, camera/calibration,
reconstruction, metrology, or score change; next dependency-ready product item
is `L-13 Surface-match pose/score component export`

## Next priority

1. `L-13 Surface-match pose/score component export` | Recommended model: `gpt-5.6-terra` | Reasoning effort: medium

Export the already identified pose and separate surface/edge score components
with JSON/HTML/CSV parity. Do not recompute matching in the export path.
