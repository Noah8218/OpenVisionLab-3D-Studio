using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Data;

/// <summary>
/// Reports only channels actually owned by a decoded source. Derived geometry
/// or display colors never become source-channel evidence.
/// </summary>
public static class SourceChannelCatalogAnalyzer
{
    public static IReadOnlyList<SourceQualityChannelAvailability>
        CreateForC3DHeightGrid() =>
        Array.AsReadOnly<SourceQualityChannelAvailability>(
        [
            Available(
                SourceQualityChannel.Height,
                "Finite non-zero float32 grid samples are available as raw height."),
            Unavailable(
                SourceQualityChannel.Intensity,
                "The supported C3D height-grid layout contains no intensity channel."),
            Unavailable(
                SourceQualityChannel.Color,
                "The supported C3D height-grid layout contains no color channel."),
            Unavailable(
                SourceQualityChannel.Depth,
                "The source declares raw height, not a separate calibrated depth channel."),
            Unavailable(
                SourceQualityChannel.Normal,
                "The supported C3D height-grid layout stores no measured normal channel."),
            Unavailable(
                SourceQualityChannel.Confidence,
                "The supported C3D height-grid layout stores no confidence channel."),
            Unavailable(
                SourceQualityChannel.SignalToNoiseRatio,
                "The supported C3D height-grid layout stores no SNR channel.")
        ]);

    public static IReadOnlyList<SourceQualityChannelAvailability>
        CreateForImportedMesh(ImportedMesh mesh)
    {
        ArgumentNullException.ThrowIfNull(mesh);

        var colorEvidence = mesh.HasVertexColors
            ? $"The decoded {mesh.Format} mesh owns {mesh.VertexColors.Length:N0} vertex colors."
            : mesh.HasBaseColorTexture
                ? $"The decoded {mesh.Format} mesh owns a base-color texture and {mesh.TextureCoordinates.Length:N0} texture coordinates."
                : $"The decoded {mesh.Format} mesh contains no supported vertex-color or base-color texture channel.";
        var normalEvidence = mesh.HasDeclaredNormals
            ? $"The decoded {mesh.Format} mesh declares {mesh.DeclaredNormalCount:N0} normals for {mesh.Positions.Length:N0} positions; normal-quality evidence decides whether they are dense and consistent."
            : $"The decoded {mesh.Format} mesh contains no declared normal channel. Calculated face normals are not promoted to source data.";

        return Array.AsReadOnly<SourceQualityChannelAvailability>(
        [
            Unavailable(
                SourceQualityChannel.Height,
                $"The decoded {mesh.Format} triangle mesh does not declare a height-grid channel."),
            Unavailable(
                SourceQualityChannel.Intensity,
                $"The decoded {mesh.Format} triangle mesh contains no supported intensity channel."),
            mesh.HasVertexColors || mesh.HasBaseColorTexture
                ? Available(SourceQualityChannel.Color, colorEvidence)
                : Unavailable(SourceQualityChannel.Color, colorEvidence),
            Unavailable(
                SourceQualityChannel.Depth,
                $"The decoded {mesh.Format} triangle mesh contains positions, not a calibrated depth channel."),
            mesh.HasDeclaredNormals
                ? Available(SourceQualityChannel.Normal, normalEvidence)
                : Unavailable(SourceQualityChannel.Normal, normalEvidence),
            Unavailable(
                SourceQualityChannel.Confidence,
                $"The decoded {mesh.Format} triangle mesh contains no supported confidence channel."),
            Unavailable(
                SourceQualityChannel.SignalToNoiseRatio,
                $"The decoded {mesh.Format} triangle mesh contains no supported SNR channel.")
        ]);
    }

    public static IReadOnlyList<SourceQualityChannelAvailability>
        CreateForLazPointCloud(LazPointCloud pointCloud)
    {
        ArgumentNullException.ThrowIfNull(pointCloud);

        var format = pointCloud.Metadata.PointDataFormat;
        var colorEvidence = pointCloud.HasRgb
            ? $"LAS point format {format} declares RGB and the decoder preserves RGB for {pointCloud.SampledPoints.Length:N0} sampled points."
            : $"LAS point format {format} does not declare an RGB channel.";

        return Array.AsReadOnly<SourceQualityChannelAvailability>(
        [
            Unavailable(
                SourceQualityChannel.Height,
                "The decoded point cloud owns XYZ coordinates, not a native height-grid channel."),
            pointCloud.HasIntensity
                ? Available(
                    SourceQualityChannel.Intensity,
                    $"LAS point format {format} declares intensity and the decoder preserves intensity for {pointCloud.SampledPoints.Length:N0} sampled points.")
                : Unavailable(
                    SourceQualityChannel.Intensity,
                    $"LAS point format {format} does not declare a supported intensity channel."),
            pointCloud.HasRgb
                ? Available(SourceQualityChannel.Color, colorEvidence)
                : Unavailable(SourceQualityChannel.Color, colorEvidence),
            Unavailable(
                SourceQualityChannel.Depth,
                "The decoded point cloud owns XYZ coordinates, not a separate calibrated depth channel."),
            Unavailable(
                SourceQualityChannel.Normal,
                "The supported LAS/LAZ decoder does not expose a measured normal channel. Calculated normals are not promoted to source data."),
            Unavailable(
                SourceQualityChannel.Confidence,
                "The supported LAS/LAZ decoder does not expose a confidence channel."),
            Unavailable(
                SourceQualityChannel.SignalToNoiseRatio,
                "The supported LAS/LAZ decoder does not expose an SNR channel.")
        ]);
    }

    private static SourceQualityChannelAvailability Available(
        SourceQualityChannel channel,
        string evidence) =>
        new(channel, SourceQualityChannelState.Available, evidence);

    private static SourceQualityChannelAvailability Unavailable(
        SourceQualityChannel channel,
        string evidence) =>
        new(channel, SourceQualityChannelState.Unavailable, evidence);
}
