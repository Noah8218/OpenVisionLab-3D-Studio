using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Tools;

/// <summary>
/// Shared deterministic execution boundary for Runner and Workbench. The
/// caller supplies the bounded search domain explicitly; this service returns
/// pose, raw coverage, and an identified overlay without acceptance policy.
/// </summary>
public static class SurfaceMatchExecutor
{
    public static SurfaceMatchExecutionArtifact Execute(
        SurfaceModelArtifact model,
        PreparedSceneArtifact scene,
        RigidSurfacePoseSearchParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(parameters);

        var poseResult =
            RigidSurfacePoseSearch.Execute(
                model,
                scene,
                parameters);
        return SurfaceMatchExecutionArtifact.Create(
            model,
            scene,
            poseResult);
    }
}
