using OpenVisionLab.Vision3D.FeatureExtraction;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Tools;

/// <summary>
/// Adapts explicit recipe direction and existing declared normals to the
/// OpenVisionLab Vision SDK orientation tool. No acquisition direction is inferred here.
/// </summary>
public static class SurfaceEdgeAcquisitionDirectionBuilder
{
    public static SurfaceEdgeAcquisitionDirectionArtifact Build(
        SurfaceEdgeDiagnosticOverlayArtifact overlay,
        string sourceContentSha256,
        ToolRecipeAcquisitionDirection direction,
        double grazingAbsoluteCosineMaximum = 0.05)
    {
        ArgumentNullException.ThrowIfNull(overlay);
        ArgumentNullException.ThrowIfNull(direction);
        if (!SurfaceEdgeDiagnosticOverlayArtifactValidator.Inspect(overlay).IsValid
            || direction.State != ToolRecipeAcquisitionDirectionState.Available
            || direction.Convention != ToolRecipeAcquisitionDirectionConvention.SensorToScene
            || direction.Vector is not { } vector
            || direction.FrameId != overlay.TargetFrameId)
        {
            throw new InvalidDataException(
                "Surface-edge orientation requires explicit available SensorToScene direction in the exact overlay frame.");
        }

        var result = new AcquisitionDirectionOrientationTool().Execute(
            new ThreeDPoint(vector.X, vector.Y, vector.Z),
            overlay.ModelSegments.Select(segment =>
                new AcquisitionDirectionNormalInput(
                    segment.ModelEdgeOrder,
                    new ThreeDPoint(
                        segment.DeclaredNormal.X,
                        segment.DeclaredNormal.Y,
                        segment.DeclaredNormal.Z))).ToArray(),
            new AcquisitionDirectionOrientationOptions
            {
                GrazingAbsoluteCosineMaximum = grazingAbsoluteCosineMaximum
            });
        if (!result.Success || result.NormalizedSensorToSceneDirection is null)
        {
            throw new InvalidDataException(
                "OpenVisionLab Vision SDK rejected the acquisition direction or declared-normal evidence: "
                + result.Message);
        }

        var artifact = new SurfaceEdgeAcquisitionDirectionArtifact(
            SurfaceEdgeAcquisitionDirectionArtifact.CurrentSchemaVersion,
            SurfaceEdgeAcquisitionDirectionArtifact.CurrentSemantics,
            overlay.ContentSha256,
            sourceContentSha256,
            direction.FrameId,
            direction.Convention,
            new SurfaceModelPoint3(
                result.NormalizedSensorToSceneDirection.X,
                result.NormalizedSensorToSceneDirection.Y,
                result.NormalizedSensorToSceneDirection.Z),
            grazingAbsoluteCosineMaximum,
            result.Items.Select(item => new SurfaceEdgeAcquisitionOrientationItem(
                item.SourceOrder,
                item.AlignmentCosine,
                item.Orientation switch
                {
                    AcquisitionDirectionOrientation.SensorFacing =>
                        SurfaceEdgeAcquisitionOrientation.SensorFacing,
                    AcquisitionDirectionOrientation.AwayFromSensor =>
                        SurfaceEdgeAcquisitionOrientation.AwayFromSensor,
                    AcquisitionDirectionOrientation.Grazing =>
                        SurfaceEdgeAcquisitionOrientation.Grazing,
                    _ => throw new InvalidDataException(
                        "OpenVisionLab Vision SDK returned an unsupported acquisition orientation.")
                })).ToArray(),
            string.Empty);
        artifact = artifact with
        {
            ContentSha256 = SurfaceEdgeAcquisitionDirectionArtifact
                .CalculateContentSha256(artifact)
        };
        var validity = SurfaceEdgeAcquisitionDirectionArtifactValidator.Inspect(artifact, overlay);
        if (!validity.IsValid)
        {
            throw new InvalidDataException(
                "Surface-edge acquisition-direction artifact is invalid: "
                + string.Join(" ", validity.Errors));
        }
        return artifact;
    }
}
