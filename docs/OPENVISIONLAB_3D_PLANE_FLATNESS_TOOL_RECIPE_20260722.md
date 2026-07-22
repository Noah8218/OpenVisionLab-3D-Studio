# Plane Flatness Generic Tool Recipe Slice

Updated: 2026-07-22  
Status: **Software slice complete; physical/metrology validation unverified**

## Contract

`Plane Flatness` is a composable `Measure` tool, not a product mode:

```text
Published TransformedHeightField
  + artifact-owned reference GridRectangle
  + artifact-owned measurement GridRectangle
  -> MeasurementResult
```

The input order is part of the recipe contract. Both ROIs must match the exact
A3 owner entity, content SHA-256, root-source SHA-256, grid dimensions, unit,
and reference frame. Raw C3D is rejected in v1 because Plane Flatness requires
an explicit A3 reference frame/unit.

WPG parameters are `MaximumFlatness`, `MinimumReferenceSampleCount`, and
`MinimumMeasurementSampleCount`. Preview reconstructs each finite grid cell as
full XYZ from the A3 origin, U/V/H axes, pitch, cell center, and height.
`PlaneFlatnessRule` maps those samples to the vendored Library-Noah
`PlaneFlatnessInspectionTool`, which fits only the reference ROI and reports
signed-distance peak-to-valley and RMS for the measurement ROI. Publish reuses
the exact Preview output; it does not recalculate. See
`OPENVISIONLAB_3D_PLANE_FLATNESS_NOAH_MIGRATION_20260722.md` for the numerical
ownership and package-provenance gate.

## Verification

- Solution build: `0` warnings, `0` errors.
- Artifact-owned ordered Runner: `14/14`.
- Generic measurement Workbench: `10/10`.
- Library-Noah independent Smoke: `42/42`; package/bridge: pass and `7/7`.
- Direct Plane Flatness and ordered Runner output SHA-256 are identical.
- Save/reopen preserves both distinct ROI bindings and schema `1.3`.
- Wrong owner, content hash, grid, order, duplicate output, and unsupported
  downstream tool cases fail closed.

Evidence:

- `artifacts/verification/20260722-plane-flatness-tool/artifact-owned-roi.txt`
- `artifacts/verification/20260722-plane-flatness-tool/workbench.txt`
- `artifacts/verification/20260722-plane-flatness-noah/`
- `artifacts/ui/20260722-plane-flatness-tool/before.png` (pre-change
  Workbench baseline; the supplied legacy smoke recipe path was unavailable)
- `artifacts/ui/20260722-plane-flatness-tool/after.png` and
  `after-quality.txt` (current 1920 x 1080 Plane Flatness selected state,
  accepted on attempt 1)

## Boundary and next UI gate

This proves deterministic software routing and numerical reuse on a synthetic
A3 fixture. It does not prove real A1/A2 acquisition, calibrated units,
physical flatness, Gauge R&R, or metrology accuracy.

The current Workbench can route the two selections explicitly and can reuse
existing selections, but its Viewer capture panel still presents one generic
selection slot. The next UI gate is a compact role selector/progress control:
`1 Reference ROI -> 2 Measurement ROI`, with separate replace/reuse actions
and no implicit Preview.
