# OpenVisionLab 3D Filter Typed Adapter v1

Updated: 2026-07-19

Status: Complete for the approved local v1 gate. Remote Windows CI is not
claimed for this working tree.

## Approved Scope

The owner approved all four decisions from the design package:

1. C3D `raw-height` only;
2. Median only with Kernel `3`, `5`, or `7`;
3. preserve missing cells and use available valid neighbors at source borders;
4. keep ROI as a separate `ROI / Crop` step.

Gaussian/bilateral filtering, hole filling, point-cloud or mesh filtering,
edge extraction, fit/intersection, XYZ affine, re-grid, calibration, physical
units, and metrology remain excluded.

## Implemented Runtime Boundary

```text
Teaching recipe source identity
  -> Data reads and hashes the same C3D bytes
  -> Tools validates the closed Filter schema and calculates Median
  -> Runner or Workbench calls the same direct adapter
  -> derived C3D carries output/root-source hashes and provenance
  -> Viewer displays the derived file without changing the taught source
```

- Core records source byte length, SHA-256, width, and height.
- Data maps C3D zero/non-finite cells to missing and preserves the full grid.
- Tools rejects unknown/duplicate parameters and unsupported values instead of
  silently normalizing them.
- A finite-zero Median output is rejected explicitly because the C3D format
  reserves zero for missing data; saving it would violate mask preservation.
- The Workbench owns Preview/Cancel/stale/Publish state. Parameter, input,
  output, or source changes make prior Preview evidence stale.
- Publish accepts only the current non-stale Preview and does not recalculate.
- Whole Run is enabled only for a one-step Filter recipe. The supplied 17-row
  affine teaching template remains blocked before partial execution.
- No interface/factory was introduced for a single adapter.

## User Interface Evidence

- Before current-source baseline:
  `artifacts/ui/20260719-filter-typed-adapter/before.png`
- After current-build explicit Preview/Publish:
  `artifacts/ui/20260719-filter-typed-adapter/published.png`
- Accepted quality report:
  `artifacts/ui/20260719-filter-typed-adapter/published-quality.txt`

The after view shows the Median-only Properties card, Kernel presets, explicit
Preview/Run/Publish command availability, output SHA-256, Viewer raw-height
distribution, and `Published` pipeline state. Run remains disabled because the
following rows have no typed adapters.

## Verification

| Gate | Result | Evidence |
| --- | --- | --- |
| Exact solution build | Pass, 0 warnings / 0 errors | `dotnet build "OpenVisionLab.ThreeDStudio.sln" -c Debug -p:Platform="Any CPU"` |
| Median numerical and contract Golden | Pass, 13/13 | `artifacts/verification/20260719-filter-golden.txt` |
| Tool Recipe teaching/state | Pass, 16/16 | `artifacts/verification/20260719-filter-tool-recipe-teaching.txt` |
| Structured teaching selections | Pass, 17/17 | `artifacts/verification/20260719-filter-selections.txt` |
| Docking | Pass, 15/15 | `artifacts/verification/20260719-filter-docking.txt` |
| Height distribution | Pass, 20/20 | `artifacts/verification/20260719-filter-height-distribution.txt` |
| Profile ViewModel | Pass, 8/8 | `artifacts/verification/20260719-filter-profile-viewmodel.txt` |
| Fixed Warpage Runner replay | Pass | `artifacts/verification/20260719-filter-runner.json` |
| Workbench Preview/Publish visual | Pass | `artifacts/ui/20260719-filter-typed-adapter/published.png` |
| Binary-only Viewer host | Pass, manifest 14/14, outputs 12/12, API 3/3 | `artifacts/verification/20260719-filter-binary-host/` |
| Viewer/Shell real WPF pointer input | Pass, 5/5 each | `artifacts/verification/20260719-filter-pointer/` |

The fixed output is `10,236,276` bytes, `1301 x 1967`, and has SHA-256
`569436F1ED6DCB656862935A738FAB691D156BD7FBE1071962FB8DA290E400C6`.
This is deterministic preprocessing in the uncalibrated raw-height/display
frame, not a physical inspection result.

## Durable Closure

```text
Status: Complete
Scope: Approved C3D raw-height Median Filter v1 adapter, Runner replay, explicit Workbench Preview/Cancel/Publish, Viewer display, and local evidence.
Acceptance criteria:
  - Approved numerical choices implemented exactly: Pass, Golden 13/13.
  - Same bytes parsed and hashed; mutation rejected: Pass.
  - Runner and Workbench share one Tools adapter: Pass.
  - Preview does not rewrite source; Publish does not rerun: Pass.
  - Unsupported downstream Run is blocked: Pass.
  - Current-build before/after UI evidence accepted: Pass.
Verification: exact solution build 0/0 plus the focused gates listed above.
Evidence: artifacts/verification/20260719-filter-* and artifacts/ui/20260719-filter-typed-adapter/.
Boundary / next dependency: Height Difference Edge requires its own owner-approved numerical and teaching contract; physical calibration and metrology evidence remain unavailable.
```
