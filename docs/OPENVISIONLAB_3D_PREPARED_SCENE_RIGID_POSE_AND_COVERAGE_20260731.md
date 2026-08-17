# OpenVisionLab 3D Prepared Scene, Rigid Pose, and Coverage

Date: 2026-07-31

Status: Complete

Backlog scope: `J-06`, `J-08`, `J-09`

Source state: current working tree based on `8672e4b`, including the
uncommitted SurfaceModel foundation and this matching slice.

## Product Decision

This slice applies an OpenVisionLab product principle.

- OpenVisionLab operator problem: measured scene evidence previously had no
  identified preparation artifact, and SurfaceModel matching had no bounded
  deterministic pose or explicit coverage contract.
- Product principle: keep input identity, configuration, transformed evidence,
  and result evidence linked.
- Independent OpenVisionLab design: WPF-neutral Core contracts, atomic Data
  persistence, pure Tools preparation/search/scoring, and headless Runner
  fixtures.
- Evidence: canonical hashes, known-pose recovery, occlusion coverage,
  fail-closed cases, regressions, and fixed R0 package validation.

No full layout redesign is approved or implemented.

## Implemented Contract

### Prepared Scene

`PreparedSceneArtifact` schema `1.0` records:

- artifact ID and name;
- explicit unit, source frame, and
  `source-cartesian-xyz` coordinate convention;
- the complete `SourceQualityReport` and its canonical semantic SHA-256;
- all finite scene points;
- deterministic even-index scene samples and preparation parameters;
- a canonical artifact content SHA-256.

The validator rejects unsupported schema, invalid text identity, inconsistent
unit/frame, malformed Source Quality evidence, point-count mismatch,
non-finite points, invalid or non-deterministic samples, duplicate sample
locators, hash mismatch, excessive distributions, and unsupported sampling
policy. Validation reports errors and never repairs or resamples evidence.

`PreparedSceneArtifactStore` validates before save and after load, writes
UTF-8 JSON atomically, preserves the prior artifact when a replacement is
invalid, and rejects malformed JSON.

### Bounded rigid pose

`RigidSurfacePoseSearch` version
`bounded-euler-centroid-nearest-v1`:

- enumerates explicit X, Y, and Z Euler ranges in deterministic order;
- derives model-to-scene translation from rotated-model and scene-sample
  centroids;
- rejects translations outside explicit per-axis bounds;
- rejects domains beyond the caller budget or the version-1 absolute
  `1,000,000` candidate safety limit before candidate-array allocation;
- ranks by matched sample count, then RMSE, then enumeration order;
- returns either an identified rigid model-to-scene pose or an explicit
  no-match reason.

The result is decision-free. It does not contain a product Pass/Fail limit.

### Surface coverage

Coverage semantics are fixed as:

```text
one-way-model-sample-greedy-unique-nearest-position-v1
```

- direction: nominal model samples to prepared scene samples;
- traversal: stable model sample order;
- correspondence: nearest unclaimed scene sample;
- reuse: each scene sample can be claimed once;
- distance rule: inclusive explicit maximum Euclidean distance;
- numerator: matched model sample count;
- denominator: total model sample count;
- secondary metric: RMSE over matched correspondences only.

This is raw evidence. Acceptance thresholds remain a later policy layer.

## Controlled Evidence

The known-pose fixture applies a documented `30 degree` Z rotation and
translation `(10, -4, 2) mm` to an asymmetric five-sample SurfaceModel.

The bounded seven-candidate search recovers:

- rigid rotation angle: `29.999999999999979 degrees`;
- translation: `(10, -4, 2) mm`;
- full-scene coverage: `5/5 = 1.0`;
- full-scene RMSE: `8.8817841970012523E-16 mm`;
- repeated result SHA-256:
  `BD0B428B72CAEAD91F3A993A6C6CDC2E91B5EE4BAF8C5D7FA250D93E104CEE0A`.

Removing one controlled scene sample produces:

- matched model samples: `4/5`;
- coverage: `0.8`;
- RMSE: `0`.

The focused verifier also proves invalid scene data, source-quality mismatch,
noncanonical hashes, content tampering, malformed JSON, out-of-bounds
translation, excessive candidate budget/resolution, overflowing distribution,
and invalid correspondence distance fail closed.

## Ownership

| Responsibility | Owner |
| --- | --- |
| Prepared Scene, source-quality identity, rigid pose/result, coverage contracts | `OpenVisionLab.ThreeD.Core` |
| Validated atomic Prepared Scene JSON persistence | `OpenVisionLab.ThreeD.Data` |
| Pure scene preparation, bounded pose search, raw coverage scoring | `OpenVisionLab.ThreeD.Tools` |
| Controlled fixtures, persistence checks, repeatability, and reports | `OpenVisionLab.ThreeD.Runner` |

No WPF, Shell, Viewer, recipe, ROI, Preview, Publish, Run, or Validation state
is owned or mutated by this slice.

## Verification

Commands:

```powershell
dotnet build "OpenVisionLab.ThreeDStudio.sln" `
  -c Release -p:Platform="Any CPU" -m:1

dotnet run --no-build -c Release `
  --project src/OpenVisionLab.ThreeD.Runner/OpenVisionLab.ThreeD.Runner.csproj -- `
  --verify-surface-matching-foundation `
  --report artifacts/current/20260731-surface-matching-foundation/surface-matching-foundation-verification.txt

dotnet run --no-build -c Release `
  --project src/OpenVisionLab.ThreeD.Runner/OpenVisionLab.ThreeD.Runner.csproj -- `
  --verify-surface-model-foundation `
  --report artifacts/current/20260731-surface-matching-foundation/surface-model-foundation-regression.txt

dotnet run --no-build -c Release `
  --project src/OpenVisionLab.ThreeD.Shell/OpenVisionLab.ThreeD.Shell.csproj -- `
  --verify-source-channel-normal-quality `
  artifacts/current/20260731-surface-matching-foundation/source-channel-normal-quality-regression.txt

dotnet run --no-build -c Release `
  --project src/OpenVisionLab.ThreeD.Shell/OpenVisionLab.ThreeD.Shell.csproj -- `
  --verify-source-quality-workspace `
  artifacts/current/20260731-surface-matching-foundation/source-quality-workspace-regression.txt

powershell -NoProfile -ExecutionPolicy Bypass `
  -File scripts/verify-code-structure.ps1 `
  -ReportPath artifacts/current/20260731-surface-matching-foundation/code-structure-report.txt

powershell -NoProfile -ExecutionPolicy Bypass `
  -File scripts/start-human-owner-r0.ps1 -Layout Wide -ValidateOnly

powershell -NoProfile -ExecutionPolicy Bypass `
  -File scripts/start-human-owner-r0.ps1 -Layout Compact -ValidateOnly
```

Results:

| Check | Result |
| --- | --- |
| Release solution build | Pass, `0` warnings / `0` errors |
| Prepared Scene / rigid pose / coverage | Pass, `28/28` |
| SurfaceModel regression | Pass, `22/22` |
| Source channel + dense normal regression | Pass, `26/26` |
| Source Quality regression | Pass, `18/18` |
| Code structure | Pass, `17/17` |
| Human R0 fixed package | Wide and Compact `-ValidateOnly` pass; no application launched |

The slice changes no UI, UX, layout, navigation, or visible text. New
Wide/Compact screenshots are not applicable. The product owner's unaided
Wide/Compact R0 remains external and is not replaced by `ValidateOnly`.

## Evidence

- `artifacts/current/20260731-surface-matching-foundation/surface-matching-foundation-verification.txt`
- `artifacts/current/20260731-surface-matching-foundation/known-pose-full.prepared-scene.json`
- `artifacts/current/20260731-surface-matching-foundation/known-pose-occluded.prepared-scene.json`
- `artifacts/current/20260731-surface-matching-foundation/known-pose-result.json`
- `artifacts/current/20260731-surface-matching-foundation/surface-model-foundation-regression.txt`
- `artifacts/current/20260731-surface-matching-foundation/source-channel-normal-quality-regression.txt`
- `artifacts/current/20260731-surface-matching-foundation/source-quality-workspace-regression.txt`
- `artifacts/current/20260731-surface-matching-foundation/code-structure-report.txt`
- `artifacts/current/20260731-surface-matching-foundation/r0-wide-validate-only.txt`
- `artifacts/current/20260731-surface-matching-foundation/r0-compact-validate-only.txt`

## Boundaries

This completion does not prove:

- general registration convergence on arbitrary industrial scenes;
- symmetry handling, multiple matches, key points, or surface removal;
- acceptance policy, product Pass/Fail, timing budgets, or false-positive
  performance;
- transformed-model Viewer overlay or Workbench/Runner parity;
- physical calibration, metrology, uncertainty, traceability, GR&R, or
  production tolerance;
- sensor, PLC, robot, cloud, or production-line integration.

The controlled fixture is numerical contract evidence, not a physical-metrology
or product-equivalence claim.

## Completion Record

Status: Complete

Scope: `J-06/J-08/J-09` identified Prepared Scene, bounded deterministic rigid
pose, and explicit decision-free one-way surface coverage.

Acceptance criteria:

- identified Prepared Scene and Source Quality identity -> Pass, canonical
  scene and source-quality hashes;
- fail-closed malformed/inconsistent evidence -> Pass, focused invalid cases;
- deterministic bounded rigid pose -> Pass, seven-candidate known-pose fixture;
- explicit occlusion coverage -> Pass, `4/5 = 0.8`;
- no inspection or presentation-state mutation -> Pass, runtime-neutral pure
  owners and focused evidence;
- Release/regression/structure gates -> Pass, results above.

Verification: Release `0/0`; focused `28/28`; SurfaceModel `22/22`; source
channel/normal `26/26`; Source Quality `18/18`; structure `17/17`; both R0
`-ValidateOnly` modes pass.

Evidence: `artifacts/current/20260731-surface-matching-foundation/`.

Boundary / next dependency: `J-10/J-16` must add transformed-model Viewer
evidence and Workbench/Runner pose, coverage, overlay, and hash parity without
introducing implicit execution or decision policy.

Next priority:

1. `J-10/J-16 transformed overlay and Workbench/Runner parity` |
   Recommended model: `gpt-5.6-sol` | Reasoning effort: high
