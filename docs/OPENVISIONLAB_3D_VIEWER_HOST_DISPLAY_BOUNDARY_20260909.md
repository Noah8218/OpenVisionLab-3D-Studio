# Viewer Host display boundary

`ViewerHostState.NominalActualDisplay` exposes immutable, read-only comparison
display state. The host can inspect readiness, evidence, progress, deviation,
and display-sampling summaries without taking ownership of comparison state.

## Ownership and reading route

Start with [ViewerHostContract](../src/OpenVisionLab.ThreeD.Viewer/Hosting/ViewerHostContract.cs),
then follow
[ViewerHostStateProjection](../src/OpenVisionLab.ThreeD.Viewer/Hosting/ViewerHostStateProjection.cs)
and the control's `HostStateChanged` composition. The comparison ViewModel retains mutable state and
the Preview, Cancel, and Publish commands. Shell bindings consume the snapshot.

Snapshot refresh is a display operation. It must not rerun comparison or publish
results. Keep command ownership separate from the immutable host projection.

The host API version is owned by [Directory.Build.props](../Directory.Build.props).
Existing positional construction remains valid. Matching tests and focused
Verification routes own executable assertions; this document defines the
responsibility and compatibility boundary rather than a historical pass count.

GPU, driver, native input, scaling, and calibration qualification remain separate.

## C3D appearance images

The Host exposes optional texture load, visibility, removal, and preference
capture/restore operations. `ViewerHostState.Presentation` reports availability,
the selected image name, visibility, and a controlled error. Default interface
implementations leave the capability unavailable on an older custom provider.

`C3DTextureViewModel` owns source-bound image associations and visibility;
the loading adapter decodes the image and closes its file stream before use.
The existing C3D renderer owns per-context texture upload and release. Source
sample-centre UV coordinates are independent of display sampling. Texture
selection does not mutate the height grid or execute inspection.

Studio stores the association in the pane's recipe-scoped display profile.
Restoration validates source/image identity and grid dimensions. Missing,
changed, or incompatible image state cannot silently attach to a different map.
