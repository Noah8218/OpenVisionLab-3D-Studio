using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Data;

/// <summary>
/// Projects one existing ConnectedRegionArtifact region into the typed
/// downstream artifact contract. It performs no connected-region arithmetic.
/// </summary>
public static class C3DEditableRegionArtifactFactory
{
    public static C3DEditableRegionArtifact Create(
        string artifactId,
        string name,
        C3DConnectedRegionArtifact source,
        int regionIndex) =>
        C3DEditableRegionArtifact.Create(artifactId, name, source, regionIndex);
}
