# OpenVisionLab 3D Phase 2 Difficult-Geometry Goldens

Date: 2026-07-15
Status: Local synthetic controlled-outcome gate and mandatory workflow definition passed; GitHub Windows execution pending

## Decision

The render-independent point-to-triangle and nominal/actual execution paths now have explicit controlled outcomes for every difficult-geometry item in the Phase 2 checklist. This closes the local synthetic checklist only. Phase 2 remains open because the accepted registration path must still record correspondence count and fitness before RMSE and reject zero-correspondence false success.

No View, ViewModel, shared Model, Viewer, recipe, or Host API behavior changed. The implementation adds cases only to the existing Runner verifiers.

The current `.github/workflows/ci.yml` contains one mandatory fail-closed step that runs both verifiers, checks the exact passing headers and required case names, writes a compact summary, and leaves all reports under `artifacts/ci/phase2-difficult-geometry` for the existing `if: always()` artifact upload. Its exact PowerShell body passes locally. This is workflow-readiness evidence, not a completed GitHub Windows run.

## Audit Matrix

| Checklist item | Before this checkpoint | Added controlled outcome |
| --- | --- | --- |
| Duplicate vertices | No named deterministic case | Two coincident triangles with duplicated vertices select the lowest source-triangle ID on an exact distance tie. |
| Non-finite normals | Reader guard existed; only a non-finite vertex was tested | A finite triangle with a non-finite stored STL normal is rejected. |
| Open surfaces | Existing tests used isolated triangles but did not state the sign meaning | Reversing one open triangle's winding reverses the signed distance. The sign is an oriented local-normal result, not a closed-solid inside/outside claim. |
| Edge/vertex nearest hits | Edge direct/robust and vertex direct outcomes existed | Vertex robust recovery now has an exact synthetic result in addition to the direct unresolved state. |
| Sparse/dense query sampling | Display budgets were tested against one five-point query | Separate one-point and six-point full-query files produce their exact counts and uniform one-unit offset statistics while display budgets remain separate. |
| No correspondence/data | Empty input guards existed but were not part of the gate | Empty nominal mesh and zero-vertex validation query are rejected before a result can be reported. |

CloudCompare documents signed cloud-to-mesh distance as triangle-normal based and warns that sparse compared-mesh vertices may require explicit surface sampling. Therefore OpenVisionLab does not claim that aggregate statistics are invariant under arbitrary query density; the sparse/dense case uses a uniform known offset by construction. Reference: <https://cloudcompare.org/doc/wiki/index.php/Cloud-to-Mesh_Distance>.

Open3D exposes registration correspondence data, fitness, and inlier RMSE as separate result fields. The future registration gate must evaluate them in that order instead of treating a low RMSE alone as success. Reference: <https://www.open3d.org/docs/release/python_api/open3d.t.pipelines.registration.RegistrationResult.html>.

## Verification

Before changes:

- Mesh deviation: `18/18`.
- Nominal/actual execution: `27/27`.

After changes:

- Build: 0 warnings, 0 errors.
- Mesh deviation: `23/23`.
- Nominal/actual execution: `29/29`.
- Fixed Viewer/Shell data-loading matrix: `128/128`, failures `0`.
- Workflow YAML parse: pass; the mandatory gate exists once and precedes artifact upload.
- Exact mandatory step body: pass; both reports and `summary.txt` were produced under `artifacts/ci/phase2-difficult-geometry`.

Commands:

```powershell
dotnet build OpenVisionLab.ThreeDStudio.slnx -c Debug --no-restore
dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --verify-mesh-deviation --report artifacts\phase2_difficult_geometry_20260715\after\mesh_deviation_golden.txt
dotnet run --project src\OpenVisionLab.ThreeD.Runner\OpenVisionLab.ThreeD.Runner.csproj -c Debug --no-build -- --verify-nominal-actual-comparison --report artifacts\phase2_difficult_geometry_20260715\after\nominal_actual_golden.txt
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\run-data-loading-matrix-smoke.ps1 -ArtifactDir artifacts\phase2_difficult_geometry_20260715\matrix -SkipBuild
```

Evidence root: `artifacts/phase2_difficult_geometry_20260715`.

Expanded local pre-push regression on 2026-07-15:

- Restore: current.
- NuGet verifier self-test: `4/4`; live audit: projects `8`, vulnerable `0`, deprecated `0`.
- Build: 0 warnings, 0 errors.
- Difficult-geometry gate: mesh deviation `23/23`, nominal/actual `29/29`, selected required evidence `10/10`.
- Fixed Viewer/Shell matrix: `128/128`, failures `0`.
- BinaryHost: zero `ProjectReference`, manifest `13/13`, required outputs `12/12`, Host API commands `3/3`, screenshot quality accepted on attempt 1.

Expanded evidence root: `artifacts/phase2_ci_prepush_20260715` (`92` files, `5,863,699` bytes at capture time). This remains local pre-push evidence and does not replace the pending GitHub Windows run.

## Limits

- These are synthetic controlled outcomes, not arbitrary-mesh generalization or a commercial metrology comparison.
- Open-surface signed distance follows local triangle orientation. It does not classify global solid interior/exterior.
- Query distributions weight aggregate statistics. The sparse/dense case proves exact execution for known inputs, not sampling invariance on varying geometry.
- Empty mesh/query rejection is not the registration zero-correspondence gate.
- The two verifiers are mandatory in the current workflow definition, but that definition has not yet passed a GitHub-hosted Windows run.
- Calibration, uncertainty, physical C3D mapping, and licensed metrology evidence remain unavailable.

## Next Gate

1. After an explicit `PUSH` request, inspect the first GitHub Windows run and authenticate the uploaded reports before closing Windows portability.
2. Keep registration product integration blocked until runtime/distribution prerequisites are resolved. Then require correspondence count, fitness, transform plausibility, and RMSE with explicit zero-correspondence rejection.
