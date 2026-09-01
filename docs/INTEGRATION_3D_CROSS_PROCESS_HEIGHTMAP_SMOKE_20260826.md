# 3D Cross-Process HeightMap Integration Smoke — 2026-08-26

Status: Complete

> Historical note: this document records the earlier 17:43 console smoke,
> where the consumer supplied the known recipe values explicitly. The current
> recipe-owned execution boundary and later evidence are recorded in
> `INTEGRATION_3D_RECIPE_OWNED_HEIGHTMAP_EXECUTION_20260826.md`.

## Scope

This closure proves the local producer/consumer path for one real C3D source:

1. Machine Studio publishes a `ThreeD/HeightMap` v2 Handoff in a D-drive
   exchange directory.
2. A separate 3D consumer process reads and accepts that Handoff.
3. The consumer verifies the copied C3D byte identity, materializes the raw
   height buffer, runs the existing deterministic HeightMap/warpage adapter,
   and publishes a correlated Run Record and Result.

The smoke uses the bundled Thickness Coupon source and the explicit ROI and
acceptance from `recipes/c3d-warpage.recipe.json`. It does not add a WPF
button, a camera SDK, calibration, or a physical metrology claim.

## Implementation

- `tools/ThreeDIntegrationConsumerSmoke/ThreeDIntegrationConsumerSmoke.csproj`
- `tools/ThreeDIntegrationConsumerSmoke/Program.cs`
- `scripts/run-three-d-integration-cross-process-smoke.ps1`
- `src/OpenVisionLab.ThreeD.Reporting/Integration/ThreeDIntegrationHeightMapRunner.cs`
- Producer extension in the Machine Studio repository:
  `tools/MachineIntegrationProducerSmoke/Program.cs`

The runner keeps the existing boundary: it loads `C3DHeightFieldSnapshot`,
passes its raw values to `VisionSdkHeightMapInspection`, and publishes through
`ThreeDIntegrationExchange`. The smoke launches the built producer and
consumer assemblies directly with `dotnet`, so the recorded PIDs identify the
two application processes rather than a shared in-process test host.

## Acceptance evidence

Evidence root:

`D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\integration-3d-cross-process\run-20260826-174356-5e0981dd77d049f5875bf71569811647\`

Observed results:

- Producer PID `30176`; consumer PID `1196`; the PIDs are different.
- Transaction `b29309f1-a1d9-4c92-9575-f64522b49c0d` and message
  `cb6c29d8-c8bb-446e-9f31-4bf2af9a9fb9` remained correlated.
- Acknowledgement: `Accepted`; Result: `Completed`; outcome: `Pass`.
- Source: `4,300,808` bytes, SHA-256
  `D879FC9E40678762214E8C3FBEA01F5C9A309701DAAEAD448067E563C5B502F8`, grid
  `1280x840`, valid `908,436`, missing `166,764`.
- Consumer raw-buffer evidence: `rawHeightBufferMaterialized=True`.
- ROI decision: `PeakToValley=7.963941253882695 mm`, limit `10 mm`, valid
  coverage `1.0` over `15,840` samples.
- Result artifact: `artifacts/3d-run-record.json`.

## Verification

```powershell
.\scripts\run-three-d-integration-cross-process-smoke.ps1 -Configuration Release
```

Observed: Release build of both producer and consumer succeeded; the smoke
returned `0` and wrote `cross-process-summary.txt` and `consumer-evidence.txt`.

Focused regression checks also passed:

```powershell
dotnet run --project tests\OpenVisionLab.ThreeD.Reporting.Tests\OpenVisionLab.ThreeD.Reporting.Tests.csproj -c Release -- -noLogo -automated -reporter quiet
dotnet test tests\OpenVisionLab.Machine.Infrastructure.Tests\OpenVisionLab.Machine.Infrastructure.Tests.csproj -c Release --no-restore --logger "console;verbosity=minimal"
```

Observed: 3D Reporting `5/5`; Machine Infrastructure `32/32`; both repository
`git diff --check` checks passed. No PC restart, commit, push, or performance
run was performed.

## Boundary

- This is a console producer/consumer smoke, not actual Machine Studio or 3D
  Studio WPF button interaction.
- The transport carries recipe identity/hash; this smoke supplies the explicit
  warpage ROI and threshold from the known recipe, while recipe-content parsing
  remains outside the transport path.
- Both development worktrees were dirty. The protocol identities declare
  `Clean` to satisfy the v2 contract, while the evidence records the actual
  worktree state as `Dirty`; this is not clean-release provenance.
- Raw-height values are software-frame data and are not calibrated physical
  metrology.
