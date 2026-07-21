# OpenVisionLab 3D Line Fit Library-Noah Migration

Updated: 2026-07-21

Status: Complete for deterministic software structure and regression evidence.
The owner approved migration of the existing deterministic
3D Line Fit numerical implementation only. This is not a new fitting method,
new Tool contract, calibration, or metrology work.

## Work contract

### User goal

Move the reusable full-XYZ deterministic consensus/TLS line-fitting math from
Studio into `Library-Noah` (`Lib.ThreeD`), while preserving the existing
OpenVisionLab 3D typed Tool workflow and observable results.

### Non-negotiable requirements

- Keep the approved `EdgePointSet -> C3DLineFeature` Studio contract,
  parameters, policies, canonical hash, Viewer evidence, and explicit
  Preview/Publish lifecycle unchanged.
- Preserve the fixed SHA-256 pair schedule, maximum `256` hypotheses, at most
  `10` TLS refinement iterations, inclusive residual threshold, deterministic
  candidate tie order, positive source-scanline direction, and inlier-projection
  endpoints.
- Do not add a generic algorithm framework, graph executor, calibration,
  physical units, or a second Studio numerical implementation.
- Library-Noah must stay source-neutral: it receives ordered finite XYZ points,
  a caller-supplied input hash, an explicit positive-axis choice, and numerical
  limits only. It receives no C3D, recipe, WPF, source identity, or UI types.

### Checkpoints

1. Add one pure `Lib.ThreeD` deterministic line-fit tool with analytical smoke
   coverage for a full-XYZ outlier case and a controlled support failure.
2. Replace Studio's private pair/TLS/eigen/residual implementation with a
   strict adapter that maps its immutable edge points to/from the Noah result.
3. Build a committed Library-Noah package, pin the Studio vendored package to
   that exact version/commit/hash, then prove the current Studio call path.
4. Run Noah smoke, Studio Line Fit golden, dependent Edge regression, full
   Studio build, package verifier, and structural search checks.

### Known blockers and boundaries

Real four-anchor data and Mapping Profile provenance remain absent. This
migration improves reusable deterministic software structure only; it does not
authorize a real affine, re-grid, Thickness, Warpage, calibration, or
metrology claim.

## Refactor proof plan

### Current structure

- Current responsibility owner: `C3DLineFitRule` in Studio owns pair scheduling,
  consensus selection, TLS covariance/eigen decomposition, residual
  classification, support gates, and diagnostics.
- Current call path: `ToolRecipeLineFitExecution -> C3DLineFitRule.Evaluate ->
  C3DLineFeature`.
- Current dependency direction: Studio Tools contains both C3D adapter logic and
  reusable numerical calculations.
- Current state/data owner: Studio owns all inputs, intermediate memberships,
  numerical result, output artifact, and UI lifecycle.

### Intended structure

- New responsibility owner: `Lib.ThreeD.DeterministicLineFitTool` owns pure
  pair scheduling, consensus/TLS fit, residual membership, support checks, and
  numerical diagnostics.
- New call path: `ToolRecipeLineFitExecution -> C3DLineFitRule adapter ->
  Lib.ThreeD.DeterministicLineFitTool -> C3DLineFeature`.
- New dependency direction: Studio Tools references the vendored `Lib.ThreeD`
  package; Library-Noah has no Studio dependency.
- New state/data owner: Library-Noah owns transient numerical state; Studio
  owns C3D lineage, artifact hash, result/overlay, lifecycle, and UI state.

### Structural conditions

1. Studio has no private pair scheduling, covariance/TLS/eigen, or point-to-line
   residual implementation after migration.
2. The Studio adapter calls the Noah tool for every executable Line Fit route.
3. Existing Studio golden outputs and controlled failures remain unchanged.

### Proof checks

- Search for removed private helper names and confirm the new Noah call path.
- Confirm `Lib.ThreeD` has no Studio references and Studio pins the committed
  package ID/version/commit/SHA-256.
- Run Library-Noah smoke, Studio golden/regression, package verification, and a
  full Studio build.

## Completion record

```text
Status: Complete
Scope: Pure deterministic full-XYZ Line Fit ownership moved to Library-Noah;
  Studio is now the typed C3D/result adapter.
Acceptance criteria: Noah source-neutral tool and smoke -> pass; Studio calls
  the vendored package -> pass; old Studio numerical helpers removed -> pass;
  Line Fit and dependent Edge regression -> pass.
Verification: Library-Noah Release build `0 warning / 0 error`; Smoke `35/35`;
  Studio Debug build `0 warning / 0 error`; Line Fit Golden `9/9`; Edge Golden
  `13/13`; package verifier and Noah bridge `7/7` -> pass.
Evidence: Library-Noah commit `f47c4b2fd854758bf5f56c266299a8a0401fe3a0`;
  vendored `Lib.ThreeD 2.7.2` SHA-256
  `CE01E934BA0582D797A6A0AB5E2CAAFD09EDBEECECA8DEC7331686511CB8653B`;
  `artifacts/current/line-fit-noah-migration-golden.txt`;
  `artifacts/current/line-fit-noah-migration-edge-regression.txt`;
  `artifacts/current/line-fit-noah-migration-bridge.txt`; and
  `artifacts/current/line-fit-noah-migration-package.txt`.
Boundary / next dependency: this preserves deterministic feature-extraction
  behavior only. A real four-anchor package and Mapping Profile are still
  required before actual A1/A2/A3 or physical claims. Filter and Height
  Difference Edge need separate owner-approved migration slices.
```
