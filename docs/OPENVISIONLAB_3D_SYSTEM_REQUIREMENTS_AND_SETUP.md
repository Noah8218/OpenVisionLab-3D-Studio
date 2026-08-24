# OpenVisionLab 3D Studio System Requirements and Setup

## Operator runtime

The supported distribution is a self-contained Windows x64 folder package.

| Requirement | Status |
| --- | --- |
| Windows 10 build 19041 or later, or Windows 11, x64 | Required |
| OpenGL-compatible GPU with a current vendor driver | Required |
| Separate .NET installation | Not required by the self-contained package |
| Git, Python, Windows SDK, or Visual Studio | Not required to run the package |

Extract the package, keep its files together, and run
`OpenVisionLab.ThreeD.Shell.exe`. See the
[Windows package quick start](OPENVISIONLAB_3D_WINDOWS_PACKAGE_QUICK_START.md)
for the first startup and sample workflow.

## Source build

Required tools:

- Windows 10 build 19041 or later, or Windows 11, x64
- Git
- Windows PowerShell 5.1 or later
- .NET 10 SDK `10.0.300` or a later compatible .NET 10 feature band
- OpenGL-compatible GPU and current driver for running the Viewer or Workbench

Check the source-build prerequisites without changing the machine:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\setup-development-environment.ps1 -CheckOnly -Scope Build
```

Restore and build:

```powershell
dotnet restore OpenVisionLab.ThreeDStudio.slnx
dotnet build OpenVisionLab.ThreeDStudio.slnx -c Release --no-restore
```

Run the Workbench:

```powershell
dotnet run --no-build --project src\OpenVisionLab.ThreeD.Shell\OpenVisionLab.ThreeD.Shell.csproj -c Release
```

Build the self-contained package:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\publish-windows-app.ps1
```

The default package output is
`artifacts\release\openvisionlab-3d-studio-win-x64`.

## Path guidance

Use a short checkout path such as `C:\src\OpenVisionLab-3D-Studio`. If a deep
NuGet cache causes restore path errors, retry in the same terminal with a short
process-local cache:

```powershell
$env:NUGET_PACKAGES = 'C:\nuget'
dotnet restore OpenVisionLab.ThreeDStudio.slnx
```
