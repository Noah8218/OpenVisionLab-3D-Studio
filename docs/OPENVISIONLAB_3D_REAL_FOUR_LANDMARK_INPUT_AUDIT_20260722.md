# Real Four-Landmark Input Audit

Updated: 2026-07-22  
Status: **Blocked by missing operator/reference evidence**

## Audited local input

- `3D/Warpage/Ori_20240116_094430.C3D` exists (`10,236,276` bytes).
- The matching PNG is display evidence only.
- No sidecar JSON, CSV, TXT, or YAML supplies four trusted reference XYZ
  coordinates, reference frame, unit, provenance, revision, or the owner-chosen
  normalized tetrahedron-volume threshold.
- `OPENVISIONLAB_3D_FOUR_ANCHOR_TEACHING_INPUT_PACKAGE_20260720.md` still
  contains placeholders for every one of those required fields.

## Decision

The real four-landmark A1 -> A3 gate remains **unverified**. Coordinates must
not be inferred from the C3D or PNG. Synthetic evidence may prove software
behavior, but cannot replace fixture/CAD/controlled-measurement authority.

## Exact unblock condition

Provide one saved recipe/source package containing four current Published
`CornerAnchor` IDs, four distinct trusted reference XYZ values, exact
frame/unit/provenance/revision, and an explicit
`MinimumNormalizedTetrahedronVolume` in `(0, 1)`. Then run Preview, Publish,
save/reopen, and headless Runner hash parity on that same package.

