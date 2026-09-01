# 3D HeightMap Buffer Integration Runtime Check — 2026-08-26

Status: Complete

Scope: explicit 3D consumer execution for a v2 Handoff with a `HeightMap`
artifact containing C3D bytes. The adapter materializes
`C3DHeightFieldSnapshot.Values` as the raw height buffer, creates the existing
`VisionSdkHeightMapInput`, runs `VisionSdkHeightMapInspection.EvaluateWarpage`,
and publishes a correlated Run Record plus v2 Result.

Acceptance criteria:

- C3D artifact byte length and SHA-256 are checked before materialization ->
  pass.
- Raw height values are passed directly to the Vision SDK boundary -> pass.
- Flat/low-variation C3D fixture -> `Completed/Pass` -> pass.
- High-variation C3D fixture -> `Completed/Ng` -> pass.
- Run Record correlation includes project, step, source/recipe identity,
  `ThreeD/HeightMap`, and consumer build -> pass.

Implementation:

- `src/OpenVisionLab.ThreeD.Reporting/Integration/ThreeDIntegrationHeightMapRunner.cs`
- `src/OpenVisionLab.ThreeD.Reporting/Integration/ThreeDIntegrationExchange.cs`
- `tests/OpenVisionLab.ThreeD.Reporting.Tests/ThreeDIntegrationHeightMapRunnerTests.cs`

Verification:

```powershell
dotnet build src\OpenVisionLab.ThreeD.Reporting\OpenVisionLab.ThreeD.Reporting.csproj -c Release --nologo
dotnet run --project tests\OpenVisionLab.ThreeD.Reporting.Tests\OpenVisionLab.ThreeD.Reporting.Tests.csproj -c Release --no-build -- `
  -noLogo -automated -reporter quiet
```

Observed result: the Reporting build completed with 0 warnings and 0 errors.
The xUnit v3 in-process runner discovered 5 tests and completed 5/5 with no
failures. Test data was created under
`D:\OpenVisionLab-TestData\OpenVisionLab-3D-Studio\integration-heightmap-runner-tests`.

Boundary / next dependency: this is a UI-agnostic local C3D/height-map slice.
It intentionally does not convert a 2D PNG into height, infer calibration, or
wire a WPF command. A shared 2D/3D source requires an explicit calibration and
scalar-semantics contract before implementation.
