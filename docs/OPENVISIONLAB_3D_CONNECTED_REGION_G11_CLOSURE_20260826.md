# OpenVisionLab 3D G-11 Connected Region Closure

Date: 2026-08-26

Status: Complete for the bounded software slice

## Scope

G-11 now executes deterministic connected-region detection over an explicit,
source-bound boolean mask and an immutable C3D height-field snapshot. The
Studio adapter validates source identity, mask identity, dimensions, finite
foreground heights, and connectivity before delegating labeling and region
metrics to the vendored Vision SDK.

This slice does not add polygon/circle rasterization, threshold-to-mask policy,
recipe persistence, a persisted region artifact, Presence/Fill/Completeness
consumers, WPF UI, product-version changes, package publication, commit, or
push. Reported area is `grid-index²`; it is not calibrated physical area.

## Ownership and contract

- `OpenVisionLab.Vision3D.ConnectedRegionTool` owns four/eight-neighbor
  labeling, row-major seed order, row-major cell order, and cancellation-aware
  traversal.
- `OpenVisionLab.Vision3D.ConnectedRegionMetricsTool` owns cell count, area,
  centroid, principal orientation, and cell-footprint bounds.
- `C3DConnectedRegionMask` owns an immutable copy of the explicit row-major
  mask and its content identity.
- `C3DConnectedRegionRule` owns the Studio source/mask contract and maps SDK
  output to `C3DConnectedRegionOutput` without reimplementing region arithmetic.
- `C3DConnectedRegionGoldenVerification` owns the deterministic Runner fixture
  and report.

## Exact SDK package

| Item | Value |
| --- | --- |
| Package | `OpenVisionLab.Vision3D 3.0.1-dev.20260826.domain-mask.1` |
| Source commit | `db8b8a281dd028c62fabfc49febcde9b4d345d37` |
| Target | `netstandard2.0` |
| SHA-256 | `D87570212D4C8913360CB01D20D9669720EDB6424B42C7FB790909EC8766D1CB` |
| Vendored path | `third_party/OpenVisionLabVisionSdk/OpenVisionLab.Vision3D.3.0.1-dev.20260826.domain-mask.1.nupkg` |

## Verification evidence

All generated reports were written below
`D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\g11-connected-region\`.

- Package hash/metadata verification: `pass=True`, exact required entries,
  package ID/version, source commit, and SHA-256.
- Runner G-11 verification: `10/10 PASS` in `connected-region.txt`.
- Existing Vision SDK Runner verification after the package update: `26/26`
  in `vision-sdk-runner.txt`.
- Full solution Release build: `0` warnings, `0` errors.
- Release test suite: `10` passed, `0` failed, `0` skipped.
- Structure/ownership guard: all G-11 ownership checks pass. The canonical
  `.sln` comparison reports `67/68` only because the pre-existing dirty
  worktree has `Reporting.Tests` in `.slnx` but not in `.sln`; the same guard
  against the current `.slnx` project set reports `68/68` in
  `code-structure-slnx.txt`.
- `git diff --check`: no whitespace errors in the tracked diff.

## Durable boundary

The bounded G-11 adapter is complete. The next software dependency is G-12
for a user-facing detected-region output/overlay workflow. Product-owner R0,
representative maximum-C3D qualification, physical metrology, and release
approval remain separate prerequisites.
