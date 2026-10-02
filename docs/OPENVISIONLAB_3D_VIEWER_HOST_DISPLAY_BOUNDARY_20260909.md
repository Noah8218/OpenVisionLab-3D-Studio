# Viewer Host display boundary

`ViewerHostState.NominalActualDisplay` exposes immutable, read-only comparison
display state. The host can inspect readiness, evidence, progress, deviation,
and display-sampling summaries without taking ownership of comparison state.

## Ownership and reading route

Start with [ViewerHostContract](../src/OpenVisionLab.ThreeD.Viewer/Hosting/ViewerHostContract.cs),
then follow `CreateHostState` and `HostStateChanged` in
[the Viewer control](../src/OpenVisionLab.ThreeD.Viewer/Views/OpenVisionThreeDViewerControl.xaml.cs)
and its host composition. The comparison ViewModel retains mutable state and
the Preview, Cancel, and Publish commands. Shell bindings consume the snapshot.

Snapshot refresh is a display operation. It must not rerun comparison or publish
results. Keep command ownership separate from the immutable host projection.

The host API version is owned by [Directory.Build.props](../Directory.Build.props).
Existing positional construction remains valid. Matching tests and focused
Verification routes own executable assertions; this document defines the
responsibility and compatibility boundary rather than a historical pass count.

GPU, driver, native input, scaling, and calibration qualification remain separate.
