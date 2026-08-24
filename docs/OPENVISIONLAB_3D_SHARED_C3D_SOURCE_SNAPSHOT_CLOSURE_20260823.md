# OpenVisionLab 3D Shared C3D Source Snapshot Closure

Updated: 2026-08-23
Status: Complete
Owner issue: `PL-0036`

## 1. Operator Problem

Opening one C3D source could decode the same full grid independently for Source
Quality and Height Image. Height Image then copied every decoded `double` value
again, while `C3DHeightFieldSnapshot` temporarily retained the complete source
file byte array beside the decoded values. On large sources these avoidable
copies increase memory pressure and can make later source-review actions feel
slower even though they refer to the same exact active source.

## 2. Completed Scope

- `ToolWorkbenchSourceSession` now owns one asynchronous immutable decoded C3D
  snapshot per exact path, file stamp, source binding, entity, unit, and frame
  key.
- Concurrent Source Quality and Height Image requests await the same task and
  receive the same snapshot instance.
- A current C3D source binding verifies SHA-256 and grid dimensions before the
  snapshot becomes shareable. A failed task is removed so a later valid retry
  is possible.
- Source path or binding replacement clears the shared task and cancels/removes
  a previously displayed Height Image. A new empty recipe clears both owners.
- Standalone Source Quality and Height Image entry points retain their existing
  path-based load behavior.
- `C3DHeightImageFrame` retains the snapshot's `ReadOnlyMemory<double>` rather
  than allocating `source.Values.ToArray()`.
- `C3DHeightFieldSnapshot` now parses and hashes C3D bytes in one sequential
  pass with a fixed 64 KiB buffer instead of `File.ReadAllBytes`.

This is a data-lifetime and responsiveness correction. It does not change the
visible layout or invoke Preview, Publish, Run, Validation, recipe mutation, or
result creation.

## 3. Refactor Proof

| Concern | Before | Current owner and flow | Proof |
| --- | --- | --- | --- |
| Active decoded source lifetime | Source Quality and Height Image each called `C3DHeightFieldSnapshot.LoadIdentified` | `ToolWorkbenchSourceSession.GetOrLoadDecodedSourceAsync` owns one task; both Workbench routes use it | focused verification receives one reference from concurrent and repeated requests |
| Height Image raw values | `C3DHeightImageFrame.Create` copied all values with `ToArray()` | frame retains the immutable snapshot memory and uses spans only while rendering/reading | structure guard rejects restoration of `source.Values.ToArray()` |
| Source file decode | `LoadIdentified`/`LoadVerified` retained `File.ReadAllBytes` beside `double[]` | `C3DHeightFieldSnapshot.ParseAndHash` owns sequential decode and incremental SHA-256 | structure guard rejects `File.ReadAllBytes(fullPath)`; exact hash/grid/value checks pass |
| Stale source handling | consumer-local file stamps caused independent reloads and an old Height Image could remain | source binding replacement clears the task and Height Image; verified binding rejects stale SHA/grid | focused stale-binding rejection passes |

The former consumers still own report creation, pixel rendering, cancellation,
and presentation state. Only the decoded-source lifetime moved to the existing
source-session owner; no partial type, cache service, storage layer, or new
framework was introduced.

## 4. Acceptance Results

1. One task/snapshot per active source key: pass; concurrent calls return the
   same reference, explicit clear returns a new reference, and stale binding is
   rejected.
2. Source Quality and Height Image share the session snapshot: pass; both
   output identities equal the shared snapshot SHA-256.
3. No Height Image value-array copy: pass; full Height Image/ROI regression is
   `64/64` and the structure guard enforces the zero-copy owner.
4. No whole-file byte retention during source decode: pass; exact generated
   file length, SHA-256, grid, raw values, valid count, and missing count are
   preserved.
5. Proportional build/regression/structure gates: pass.

## 5. Verification

Physical evidence root:

`D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260823-pl0036-shared-c3d-snapshot`

Commands actually run against the final source:

```powershell
dotnet build OpenVisionLab.ThreeDStudio.sln -c Release --nologo

dotnet run --no-build -c Release `
  --project src/OpenVisionLab.ThreeD.Shell/OpenVisionLab.ThreeD.Shell.csproj -- `
  --verify-source-quality-workspace <D-backed-report>

dotnet run --no-build -c Release `
  --project src/OpenVisionLab.ThreeD.Shell/OpenVisionLab.ThreeD.Shell.csproj -- `
  --verify-inspection-workspace-selection <D-backed-report>

dotnet run --no-build -c Release `
  --project src/OpenVisionLab.ThreeD.Shell/OpenVisionLab.ThreeD.Shell.csproj -- `
  --verify-c3d-height-profile <D-backed-report>

dotnet run --no-build -c Release `
  --project src/OpenVisionLab.ThreeD.Shell/OpenVisionLab.ThreeD.Shell.csproj -- `
  --verify-c3d-height-distribution <D-backed-report>

powershell -NoProfile -ExecutionPolicy Bypass `
  -File scripts/verify-code-structure.ps1 `
  -ReportPath <D-backed-report>
```

Results:

- full solution Release build: `0` warnings, `0` errors;
- shared snapshot and Source Quality: `24/24`;
- Inspection Workspace and Height Image/ROI: `64/64`;
- C3D height profile: `14/14`;
- C3D height distribution: `26/26`;
- code structure: `67/67`.

The Shell verification route exits before creating the main window. Monitor
topology was still recorded dynamically: two independent monitors were found,
and the smaller left monitor `\\.\DISPLAY2`, bounds
`{-1920,365,1920,1080}`, was selected. No visible window existed to place or
intersect.

## 6. Boundaries And Remaining Risk

- This closure proves removal of the audited whole-file byte allocation and
  duplicate decoded Height Image value array. It does not claim a measured
  process-memory reduction for a representative maximum production source.
- The active session deliberately retains one decoded `double[]` while the
  source is in use. Out-of-core storage, tiling, memory mapping, and a general
  cache remain unapproved and were not added.
- Representative maximum-C3D qualification remains blocked until the owner
  supplies a representative input and accepts peak process-memory and load-time
  limits.
- Product-owner unaided Wide/Compact R0 remains deferred. This work was not
  applied to or used to rebuild the frozen `c1b49ec` R0 package.
- No commit, push, version, package, or release action was performed.

## 7. Durable Completion Record

```text
Status: Complete
Scope: PL-0036 active Workbench decoded C3D snapshot sharing, binding verification, streaming source decode, stale Height Image clearing, and zero-copy Height Image raw values
Acceptance criteria: one source-session task/reference -> pass; Source Quality/Height Image shared identity -> pass; stale binding/clear behavior -> pass; exact streaming decode identity/values -> pass; focused/build/structure gates -> pass
Verification: full solution Release 0/0; shared snapshot/Source Quality 24/24; Inspection Workspace/Height Image 64/64; C3D profile 14/14; distribution 26/26; structure 67/67
Evidence: docs/OPENVISIONLAB_3D_SHARED_C3D_SOURCE_SNAPSHOT_CLOSURE_20260823.md; .proofline/issues/PL-0036.json; D:/OpenVisionLab-TestData/OpenVisionLab-3D-Studio/artifacts/current/20260823-pl0036-shared-c3d-snapshot/
Boundary / next dependency: no maximum-input memory/load-time qualification; R0 remains deferred; frozen R0 package unchanged; no commit, push, version, package, or release action
```
