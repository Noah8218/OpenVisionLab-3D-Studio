# Imported-Mesh Allocation Guardrails Closure

Date: 2026-08-22
Issue: `PL-0035`

## Outcome

GLB and STL import now rejects unsupported file sizes and unsafe declared
allocation ranges before the corresponding whole-file, decoded-array, or
embedded-texture allocation. Existing public GLB, binary STL, and ASCII STL
imports remain unchanged in the focused verification.

This is a bounded import-safety change. It does not add a new Import surface,
external `.gltf` resources, processing cancellation, C3D maximum-input
qualification, or a physical-metrology claim.

## Limits And Failure Contract

| Input | Controlled limit or check | Failure behavior |
| --- | --- | --- |
| GLB/STL file | `536,870,912` bytes (512 MiB) | `InvalidDataException` before whole-file allocation |
| GLB accessor/final expanded geometry | at most `3,000,000` accessor elements/vertices and `3,000,000` indices (`1,000,000` triangles) | named accessor or expanded-mesh limit error before decoded-array/list growth |
| GLB accessor layout | non-negative offset/count, stride at least element width, and required span inside both the declared bufferView and embedded BIN buffer | named accessor/range error before decode |
| GLB embedded texture | at most `268,435,456` bytes (256 MiB), fully inside buffer 0/BIN | texture length/range error before byte copying |
| STL triangles | existing `1,000,000` ceiling | binary exact-length declaration rejected before `ReadAllBytes`; ASCII rejected when the next vertex would exceed the ceiling |

The 512 MiB file ceiling remains above the previously reviewed
`428,004,884`-byte measured ASCII STL while preventing unbounded whole-file
allocation. The GLB geometry ceiling aligns its maximum triangle-corner count
with the established STL triangle ceiling. These are supported import bounds,
not maximum-input latency or peak-memory guarantees.

Malformed GLB JSON/index/overflow failures that previously could escape as
generic parser/runtime exceptions are translated at the Data boundary to an
actionable `InvalidDataException` with the source path. The existing Viewer
GLB/STL handlers already include that message in their unsupported/corrupt
source summary. This path was inspected in source; no visible UI or XAML was
changed.

## Changed Owners

- `src/OpenVisionLab.ThreeD.Data/Meshes/GlbMesh.cs`
  - bounded header preflight;
  - accessor, bufferView, BIN, texture, and expanded-geometry validation;
  - consistent malformed-structure error translation.
- `src/OpenVisionLab.ThreeD.Data/Meshes/StlMesh.cs`
  - bounded file preflight;
  - existing triangle ceiling enforced during ASCII parsing.
- `src/OpenVisionLab.ThreeD.Shell/Verification/Data/SourceChannelAndNormalQualityVerification.cs`
  - malformed accessor count/range and texture declaration fixtures;
  - sparse over-limit GLB/STL and exact-length binary STL fixtures;
  - retained valid public GLB, binary STL, and ASCII STL checks.

No new dependency, UI framework, abstraction layer, or algorithm owner was
introduced.

## Verification

Evidence root:
`D:/OpenVisionLab-TestData/OpenVisionLab-3D-Studio/artifacts/current/20260822-pl0035-imported-mesh-guardrails/`

- Shell Release build: `0` warnings, `0` errors.
- Focused source-channel/import verification: `35/35`.
- Full solution Release build: `0` warnings, `0` errors.
- Code-structure guard: `67/67`.
- Malformed GLB evidence confirms:
  - accessor `3,000,001` rejected against `3,000,000`;
  - bufferView `offset=32, length=36` rejected against a 44-byte BIN;
  - texture `268,435,457` bytes rejected against `268,435,456`.
- Sparse-file evidence confirms both formats reject `536,870,913` bytes before
  whole-file allocation.
- Binary STL evidence confirms `1,000,001` triangles are rejected against the
  existing `1,000,000` limit before whole-file loading.

`소스 코드 기준 검토 완료 / 실제 Runtime UI 검증 필요`: the existing Viewer
status propagation was inspected, but this Data-only slice did not launch a
malformed import through the visible Viewer UI. No visible control, layout,
theme, localization, focus, hover, or DPI behavior changed.

## Completion Record

```text
Status: Complete
Scope: PL-0035 bounded GLB/STL whole-file, declared geometry, buffer range, embedded texture, and STL triangle allocations with actionable Data-boundary failures
Acceptance criteria: GLB accessor/range checks before allocation -> pass; embedded texture checks before copy -> pass; STL file/binary/ASCII ceilings before unsafe growth -> pass; valid public GLB/binary STL/ASCII STL compatibility -> pass; focused/build/structure gates -> pass
Verification: Shell Release 0/0; source-channel/import 35/35; full solution Release 0/0; structure 67/67
Evidence: docs/OPENVISIONLAB_3D_IMPORTED_MESH_ALLOCATION_GUARDRAILS_20260822.md; .proofline/issues/PL-0035.json; D:/OpenVisionLab-TestData/OpenVisionLab-3D-Studio/artifacts/current/20260822-pl0035-imported-mesh-guardrails/
Boundary / next dependency: visible malformed-import Viewer UI was not runtime-tested; R0 remains deferred; maximum C3D qualification remains blocked on representative owner data and accepted budgets; no commit, push, version, package, or release action occurred
```
