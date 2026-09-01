# Recipe-Owned 3D HeightMap Execution — 2026-08-26

Status: Complete

## Scope

The cross-process `ThreeD/HeightMap` smoke now executes from the copied,
hash-bound C3D Warpage recipe instead of embedding ROI and acceptance values in
the consumer program.

The bounded path is:

1. Machine Studio publishes a v2 Handoff with the C3D source and recipe.
2. The 3D consumer verifies the declared recipe bytes and loads the existing
   `C3DWarpageRecipe` model.
3. The runner rejects disabled recipes and source/step/Handoff unit or frame
   mismatches before materializing the C3D raw-height buffer.
4. The existing deterministic HeightMap inspection boundary evaluates the
   recipe ROI and acceptance and publishes the correlated Run Record and
   Result.

This is still a console producer/consumer boundary. It does not add a WPF
execution button, camera SDK, calibration, or physical metrology claim.

## Implementation

- `src/OpenVisionLab.ThreeD.Reporting/Integration/ThreeDIntegrationHeightMapRunner.cs`
  adds `RunAcceptedHandoffFromRecipe`, recipe byte identity verification, and
  fail-closed unit/frame checks while preserving the explicit request overload.
- `tools/ThreeDIntegrationConsumerSmoke/Program.cs` loads the copied recipe
  and calls the recipe-owned runner path; no ROI or threshold constants remain.
- `tools/MachineIntegrationProducerSmoke/Program.cs` aligns the 3D smoke
  context with the bundled recipe: unit `raw-height` and frame
  `frame.c3d-grid-index`. The 2D command remains unchanged.
- `tests/OpenVisionLab.ThreeD.Reporting.Tests/ThreeDIntegrationHeightMapRunnerTests.cs`
  covers recipe-driven Pass execution and context-mismatch rejection.

## Acceptance evidence

Evidence root:

`D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\integration-3d-cross-process\run-20260826-180058-690a69ee1118427289e4a7e3ecaaf320\`

Observed results:

- Producer PID `400`; consumer PID `16900`; the processes were separate.
- Transaction `bdb2d2b8-dfa3-44fc-88ee-b32d02d5fff9` and message
  `72f7d5ba-2045-491d-930a-c595a583994a` remained correlated.
- Acknowledgement `Accepted`; Result `Completed`; outcome `Pass`.
- Recipe-derived context: `raw-height`, `frame.c3d-grid-index`.
- Recipe-derived acceptance: maximum peak-to-valley `10`; ROI
  `row:156,column:178,rows:144,columns:110`; minimum valid samples `3`.
- Source: `4,300,808` bytes, SHA-256
  `D879FC9E40678762214E8C3FBEA01F5C9A309701DAAEAD448067E563C5B502F8`, grid
  `1280x840`, valid `908,436`, missing `166,764`.
- Consumer evidence contains `rawHeightBufferMaterialized=True` and
  `runRecordRelativePath=artifacts/3d-run-record.json`.
- Result metrics report `PeakToValley=7.963941253882695 raw-height` and
  `ValidCoverageRatio=1` over `15,840` samples.

## Verification

```powershell
dotnet run --project tests\OpenVisionLab.ThreeD.Reporting.Tests\OpenVisionLab.ThreeD.Reporting.Tests.csproj -c Release -- -noLogo -automated -reporter quiet
dotnet build tools\ThreeDIntegrationConsumerSmoke\ThreeDIntegrationConsumerSmoke.csproj -c Release --no-restore
.\scripts\run-three-d-integration-cross-process-smoke.ps1 -Configuration Release -SkipBuild
```

Observed: Reporting tests `7/7`; consumer Release build `0` warnings and `0`
errors; the separate producer/consumer smoke returned `0` and wrote the
summary/evidence files above. The producer Release build also completed with
`0` warnings and `0` errors before the process-only smoke. The script's wrapper
build phase was not used as the final gate because Windows `Start-Process
-Wait` remained attached to the compiler-server child; the two builds were
run directly and the process-only wrapper then passed.

## Boundary

- This proves recipe-owned execution for the console integration adapter, not
  the current 3D WPF integration panel. The panel still publishes an existing
  Run Record and requires a separate UI-owned execution workflow if one is
  approved.
- `raw-height` is software-frame data and is not calibrated physical
  metrology.
- Protocol identities declare `Clean` for the v2 contract while the evidence
  records both development worktrees as actually `Dirty`; this is not clean
  release provenance.
- No PC restart, performance run, commit, push, version, package, or release
  action occurred.
