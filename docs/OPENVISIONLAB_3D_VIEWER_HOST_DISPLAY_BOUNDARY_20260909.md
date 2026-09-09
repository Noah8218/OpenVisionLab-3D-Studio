# Viewer Host Display Boundary

Date: 2026-09-09

## Scope

The Viewer exposes read-only Nominal/Actual comparison display values through
`ViewerHostState.NominalActualDisplay`. The boundary covers readiness, evidence,
progress, deviation distribution, and display-sampling summaries. Existing
`Preview`, `Cancel`, and `Publish` commands remain owned by the comparison
ViewModel.

This is an additive development-line change. Existing positional construction of
`ViewerHostState` remains valid, and the Viewer Host API is `1.1`.

## Ownership and call path

- `NominalActualComparisonViewModel` owns mutable comparison state and writes
  the source properties and derived display values.
- `OpenVisionThreeDViewerControl` reads that state in `CreateHostState()` and
  owns the `HostState` dependency property. The nested property-change handler
  replaces the immutable snapshot and raises `HostStateChanged` with
  `NominalActualDisplay`.
- `MainWindow.xaml` reads the snapshot in the Tool Inspector, Result Summary,
  and Linked View. The command buttons keep the concrete comparison ViewModel
  as their command owner.

For a first code reading, search for `NominalActualDisplay` and read these files
in order:

1. `src/OpenVisionLab.ThreeD.Viewer/Hosting/ViewerHostContract.cs`
2. `src/OpenVisionLab.ThreeD.Viewer/Views/OpenVisionThreeDViewerControl.xaml.cs`
3. `src/OpenVisionLab.ThreeD.Viewer/Views/OpenVisionThreeDViewerControl.Host.cs`
4. `src/OpenVisionLab.ThreeD.Shell/MainWindow.xaml`
5. `src/OpenVisionLab.ThreeD.Viewer/ViewModels/NominalActualComparisonViewModel.cs`

## Binding contract

The snapshot exposes these read-only values:

- `InputsReady` and `EvidenceSummary`
- `StateSummary` and `DirectionSummary`
- `ProgressPercent`
- `DistributionVisible` and `DistributionSummary`
- `CurrentDisplaySamplingSummary`, `NextPreviewSamplingSummary`, and
  `DisplaySamplingChangePending`

The control publishes a new snapshot when any of those values changes. The
dependency property makes the update observable to WPF bindings without
requiring Shell code to reach into the comparison ViewModel.

## Verification boundary

- The full solution Release build passes with zero warnings and zero errors.
- The existing Nominal/Actual ViewModel verification passes all 74 checks.
- Static source checks confirm that the three read-only Shell surfaces use the
  Host snapshot and that the Preview/Cancel/Publish command owner remains the
  comparison ViewModel.

Runtime GPU, driver, DPI, theme, and large-input qualification remain separate
from this source boundary. The product continues to report software-only
`raw-height` evidence and does not infer calibrated physical measurement.
