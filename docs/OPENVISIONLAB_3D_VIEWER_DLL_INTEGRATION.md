# OpenVisionLab 3D Viewer DLL Integration

## Boundary

`OpenVisionLab.ThreeD.Viewer` is a separate .NET 10 WPF class-library project. The main Studio host and the docked Shell consume its public `OpenVisionThreeDViewerControl`; they do not own SharpGL rendering code.

The Viewer is distributed as a dependency bundle rather than one isolated file. `OpenVisionLab.ThreeD.Viewer.dll` requires the adjacent Core, Data, Tools, SharpGL, and LASzip assemblies listed in `viewer-dll-manifest.json`.

## Build The Bundle

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\build-viewer-dll.ps1
```

Default output:

```text
artifacts\viewer-dll\net10.0-windows\
```

The script uses `dotnet publish` because a plain class-library build does not copy all SharpGL runtime dependencies. It fails when a required runtime DLL is missing and records the size and SHA-256 of every delivered file. The manifest also records the central product version, Host API version, Viewer assembly version, Git commit/tree state, target framework, configuration, and .NET SDK version.

`Directory.Build.props` is the single source for the development product version and Viewer Host API version. Update those values deliberately when either public contract changes.

## Reference From Another WPF Project

The host must target `net10.0-windows` and enable WPF. Keep the complete Viewer bundle together in the host output directory.

```xml
<PropertyGroup>
  <TargetFramework>net10.0-windows</TargetFramework>
  <UseWPF>true</UseWPF>
</PropertyGroup>

<ItemGroup>
  <Reference Include="OpenVisionLab.ThreeD.Viewer">
    <HintPath>..\ViewerDll\OpenVisionLab.ThreeD.Viewer.dll</HintPath>
    <Private>true</Private>
  </Reference>
</ItemGroup>
```

Host the control in XAML:

```xml
<Window
    x:Class="ViewerHost.MainWindow"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:viewer="clr-namespace:OpenVisionLab.ThreeD.Viewer;assembly=OpenVisionLab.ThreeD.Viewer">
    <viewer:OpenVisionThreeDViewerControl SidePanelsVisible="False" />
</Window>
```

Use `SidePanelsVisible="True"` when the Viewer runs without the OpenVisionLab Shell panes.

## Host API

`OpenVisionThreeDViewerControl` implements `IOpenVisionThreeDViewerHost`. External host code should use `HostState`, `HostStateChanged`, and the host commands instead of depending on the concrete `MainWindowViewModel`.

```csharp
using OpenVisionLab.ThreeD.Viewer.Hosting;

IOpenVisionThreeDViewerHost viewer = ViewerControl;
viewer.HostStateChanged += (_, args) =>
{
    StatusText.Text = args.State.ViewerStatus;
    MeasurementText.Text = args.State.MeasurementSummary;
};

viewer.FitAll();
```

Host API version `1.0` provides immutable state snapshots and `FitAll`, `FitSelection`, `ResetView`, and `SaveRecipe` commands. Adding members is compatible; removing or changing existing member semantics requires a host API version change.

## Ownership Rules

- Viewer project: Viewer View, Viewer ViewModel, SharpGL render loop, camera, picking, measurement/result overlays.
- Core/Data/Tools projects: non-UI contracts, parsers, recipes, and inspection calculations consumed by Viewer and Runner.
- Host project: window lifetime, docking, WPF-UI theme, and application-level navigation.
- Host code must not copy SharpGL rendering or Viewer ViewModel logic.
- External host code should not subscribe directly to `MainWindowViewModel.PropertyChanged`; use the versioned host API.
- New visible Viewer workflows continue in View -> ViewModel -> Model order.

## Release Check

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\build-viewer-dll.ps1 -Configuration Release
dotnet build OpenVisionLab.ThreeDStudio.slnx -c Debug --no-restore
```

After a Viewer change, retain the existing current-build Viewer/Shell screenshot and data-loading matrix checks. A successful DLL bundle proves dependency completeness; it does not replace rendering or interaction evidence.

## Binary Host Proof

The minimal sample under `samples\OpenVisionLab.ThreeD.Viewer.BinaryHost` has no `ProjectReference`. It references DLLs from the published bundle, copies the manifest, and hosts `OpenVisionThreeDViewerControl` through Host API v1.0.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\verify-viewer-dll-host.ps1
```

The verification builds the bundle and sample, launches the generated Host EXE directly, then requires current C3D render/pick screenshot and contract evidence.
