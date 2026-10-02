# Source setup and project navigation

## Prerequisites

Use Windows 10 build 19041 or later, or Windows 11, on x64. Install Git,
Windows PowerShell 5.1 or later, and the .NET SDK selected by
[global.json](../global.json). Running the viewer requires an OpenGL-compatible
GPU with a current driver.

Check build prerequisites without installing software:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/setup-development-environment.ps1 -CheckOnly -Scope Build
```

## Start here

1. Open [the solution](../OpenVisionLab.ThreeDStudio.slnx), or
   [the equivalent .sln](../OpenVisionLab.ThreeDStudio.sln).
2. Select [Shell](../src/OpenVisionLab.ThreeD.Shell/OpenVisionLab.ThreeD.Shell.csproj)
   as the startup project for recipe teaching and inspection.
3. Read [Shell App.xaml](../src/OpenVisionLab.ThreeD.Shell/App.xaml), then follow
   [MainWindow](../src/OpenVisionLab.ThreeD.Shell/MainWindow.xaml.cs)'s DataContext
   injection into the changed view's binding and command owner.
4. Follow matching project references and tests. Do not infer ownership or
   startup configuration from solution ordering.

The `.sln` and `.slnx` list the same product projects; no shared launch profile
selects a startup project automatically.

| Purpose | Source |
| --- | --- |
| Recipe workbench | [Shell](../src/OpenVisionLab.ThreeD.Shell/OpenVisionLab.ThreeD.Shell.csproj) |
| Viewer demonstration host | [ThreeDStudio](../src/OpenVisionLab.ThreeDStudio/OpenVisionLab.ThreeDStudio.csproj) |
| Headless recipe execution | [Runner](../src/OpenVisionLab.ThreeD.Runner/OpenVisionLab.ThreeD.Runner.csproj) |
| Focused product checks | [Verification](../src/OpenVisionLab.ThreeD.Verification/VerificationCommandRouter.cs) |
| Data tests | [Data.Tests](../tests/OpenVisionLab.ThreeD.Data.Tests/OpenVisionLab.ThreeD.Data.Tests.csproj) |
| Reporting tests | [Reporting.Tests](../tests/OpenVisionLab.ThreeD.Reporting.Tests/OpenVisionLab.ThreeD.Reporting.Tests.csproj) |
| Library dependency graph | Each solution project's `ProjectReference` and `PackageReference` |

## Build and run

```powershell
dotnet restore OpenVisionLab.ThreeDStudio.slnx
dotnet build OpenVisionLab.ThreeDStudio.slnx -c Release --no-restore
dotnet run --project src/OpenVisionLab.ThreeD.Shell/OpenVisionLab.ThreeD.Shell.csproj -c Release --no-build
```

The repository includes pinned local packages through
[NuGet.Config](../NuGet.Config). No adjacent Vision SDK or integration repository
is required. Do not replace fixed packages merely to make a local restore pass.

The solution includes libraries for domain/data/tools, viewer/presentation/
docking, reporting, and shared logging/localization/dialogs. Application hosts
compose those libraries; domain policy must not depend on the UI host.

## Local paths and tests

Keep a short checkout path. If MSBuild's child process cannot find Git or
PowerShell, check the terminal PATH and remove duplicate entries for that
process; avoid changing application logic for a tool-discovery problem.

Put local test data and generated output on a separate test-data volume.
Set `TEMP`, `TMP`, and `OPENVISIONLAB_3D_TEST_ARTIFACT_ROOT` in the test process
when needed. Build output can be routed with `--artifacts-path`.

The test runner is selected in `global.json`. Examples:

```powershell
dotnet test --project tests/OpenVisionLab.ThreeD.Data.Tests/OpenVisionLab.ThreeD.Data.Tests.csproj -c Release
dotnet test --project tests/OpenVisionLab.ThreeD.Reporting.Tests/OpenVisionLab.ThreeD.Reporting.Tests.csproj -c Release
```

A source build and these tests do not qualify a machine's GPU, native scaling,
camera input, calibration, or production operation.
