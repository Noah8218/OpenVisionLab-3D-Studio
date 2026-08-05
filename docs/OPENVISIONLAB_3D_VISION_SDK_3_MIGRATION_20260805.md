# OpenVisionLab Vision SDK 3 Migration

Date: 2026-08-05

Status: Complete

## Scope

OpenVisionLab 3D Studio now consumes the renamed OpenVisionLab Vision SDK
instead of Library-Noah. The active dependency is the repository-vendored
`OpenVisionLab.Vision3D 3.0.0` package. An adjacent SDK checkout is not needed
to restore, build, run the bundled sample, or publish the Windows application.

The migration covered package feeds and references, namespaces, Studio bridge
names, Runner commands, structure guards, CI commands, package publication,
R0 fixed binary hashes, and current documentation. Historical Library-Noah
records remain evidence for their recorded versions and are marked superseded
where they could otherwise look current.

## Fixed SDK input

| Item | Value |
| --- | --- |
| SDK repository | `C:\Git\OpenVisionLab-Vision-SDK` |
| Source commit | `f34fdf912ff38fe20f36dbb063837e14b4f922b3` |
| Package | `OpenVisionLab.Vision3D 3.0.0` |
| Target | `netstandard2.0` |
| Vendored path | `third_party/OpenVisionLabVisionSdk/OpenVisionLab.Vision3D.3.0.0.nupkg` |
| Package SHA-256 | `F7324DC43ABF8E130D6F88C034287C192CFEA89E16A8A906A60F52DE341045B4` |

The package contains its DLL and XML documentation plus `LICENSE`, `NOTICE`,
`README.md`, and `docs/three-d-inspection.md`. `NuGet.Config` resolves it from
the repository-relative feed; Studio has no SDK `ProjectReference`.

## Compatibility result

The SDK migration guide maps the former Geometry, FeatureExtraction, and
Inspection namespaces directly to their `OpenVisionLab.Vision3D` equivalents.
The SDK's overflow/underflow-safe distance and scaled-RMSE hardening preserves
the formula and decisions but changed two controlled fixtures by one
representable `double` value:

- false-positive RMSE: `0.8062257706404623` to
  `0.80622577064046241`;
- fixed performance-fixture RMSE: `1.2291427472082641E-14` to
  `1.2291427472082631E-14`.

Their deterministic execution and assessment hashes were rebaselined. Pose,
coverage, candidate count, acceptance decision, controlled-failure reason,
units, frames, and policy are unchanged. After normalizing package identity,
paths, timestamps, timings, generated identifiers, package-name-dependent
rendered length, temporary-path-dependent fixture length, and these two
documented numerical changes, the 46 Runner and 27 Shell primary reports are
equivalent `73/73`.

The observed Surface Match performance fixture remained within its existing
local ceilings. These timings are observational and do not establish
production performance.

## Verification

- SDK Release build: `0` warnings, `0` errors.
- SDK smoke: `154/154`.
- isolated local-package-only SDK consumer: pass for 2D properties/tools,
  Blob, 3D inspection, Surface Match, and mesh APIs.
- Studio isolated restore and Release build: `0` warnings, `0` errors across
  12 projects.
- active source, script, CI, and NuGet configuration legacy Noah identifiers:
  `0`.
- Studio CI-equivalent Debug restore/build: `0` warnings, `0` errors.
- vendored package integrity: pass.
- direct Vision SDK 3D bridge: `26/26`.
- structure and zero-debt guard: `29/29`.
- Runner matrix: `46/46`.
- Shell/Workbench matrix: `27/27`.
- bundled Thickness Coupon Tool Recipe: `8/8` steps, expected status Pass.
- self-contained Windows package: `502/502` manifest entries verified by size
  and SHA-256; `OpenVisionLab.Vision3D.dll` and all required operator files are
  present; separate .NET installation is not required.
- refreshed Wide and Compact R0 `-ValidateOnly`: both pass, select the current
  leftmost `\\.\DISPLAY2`, and launch no application.

The CI workflow now calls `verify-vision-sdk-package.ps1` and
`--verify-vision-sdk-3d`. Hosted GitHub Actions remains unverified until these
uncommitted changes are pushed and a workflow run completes.

The first package-consumer attempt used a malformed combined NuGet source and
the first sample attempt used the legacy `--recipe` route. Corrected isolated
local sources and the supported `--tool-recipe` route both passed; neither was
a product failure. A later `--no-restore` build against a fresh artifacts path
also failed only because that path had no assets file; restore followed by the
same Release build passed `0/0`.

## Evidence

Primary local evidence is stored at:

`D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260805-vision-sdk-3-migration\`

The self-contained package is under the repository `artifacts` junction at:

`artifacts\current\20260805-vision-sdk-3-migration\self-contained-package\`

On this restored workstation that junction resolves to the recovered `E:`
artifact volume; build intermediates and all other migration evidence were
kept on `D:`.

## Completion record

```text
Status: Complete
Scope: Studio dependency, source adapters, guards, CI commands, documentation, sample replay, and Windows packaging migrated from Library-Noah to OpenVisionLab Vision SDK 3
Acceptance criteria: fixed committed SDK package consumed without adjacent checkout -> Pass; relevant behavior and reports normalized -> Pass 73/73; Runner and Shell matrices -> Pass 46/46 and 27/27; bundled sample -> Pass 8/8; self-contained package -> Pass 502/502
Verification: SDK Release 0/0 and smoke 154/154; isolated package consumer pass; Studio Release and Debug 0/0; package pass; bridge 26/26; structure 29/29; both R0 ValidateOnly modes pass
Evidence: this document and D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\artifacts\current\20260805-vision-sdk-3-migration\
Boundary / next dependency: no commit or push was authorized; hosted CI is unverified; product-owner unaided Wide and Compact R0 remains required for A-01 and Workspace v3 acceptance
```
