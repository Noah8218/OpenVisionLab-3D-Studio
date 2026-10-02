using System.Security.Cryptography;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using Xunit;

namespace OpenVisionLab.ThreeD.Reporting.Tests;

public sealed class C3DSignedHeightDeltaViewerContractTests
{
    [Fact]
    public void SignedDeltaFrameKeepsFiniteZeroMissingAndOriginDistinct()
    {
        var artifact = CreateArtifact();
        var frame = C3DHeightImageFrame.Create(artifact, TestContext.Current.CancellationToken);

        Assert.Equal(artifact.OutputEntityId, frame.SourceEntityId);
        Assert.Equal(artifact.GridOriginColumn, frame.GridOriginColumn);
        Assert.Equal(artifact.GridOriginRow, frame.GridOriginRow);
        Assert.Equal(-2.0, frame.Minimum);
        Assert.Equal(2.0, frame.Maximum);
        Assert.Equal(-2.0, frame.DefaultDisplayMinimum);
        Assert.Equal(2.0, frame.DefaultDisplayMaximum);
        Assert.Equal(C3DSignedHeightDeltaViewerContract.CoordinateMapping, C3DHeightImageFrame.CoordinateMapping);

        Assert.True(frame.TryGetCell(1, 0, out var zeroCell));
        Assert.Equal(0.0, zeroCell.RawHeight);
        Assert.True(zeroCell.IsValid);

        Assert.True(frame.TryGetCell(1, 1, out var missingCell));
        Assert.False(missingCell.IsValid);
        Assert.Equal(1, frame.InvalidCellMap.MissingCellCount);
        Assert.Equal(
            artifact.OutputContentSha256,
            C3DSignedHeightDeltaArtifact.CalculateValuesSha256(artifact.Values));
    }

    [Fact]
    public void SignedDeltaSidecarAndPngProjectionPreserveTheZeroSafeBoundary()
    {
        var artifact = CreateArtifact();
        var frame = C3DHeightImageFrame.Create(artifact, TestContext.Current.CancellationToken);
        var root = ReportingTestRoot.Create("signed-delta-viewer");
        try
        {
            var sidecarPath = Path.Combine(root, "delta.sdelta.json");
            var pngPath = Path.Combine(root, "delta.png");
            C3DSignedHeightDeltaArtifactStore.Save(sidecarPath, artifact);
            C3DHeightImagePngWriter.Write(frame, pngPath, TestContext.Current.CancellationToken);

            var reopened = C3DSignedHeightDeltaArtifactStore.Load(sidecarPath);
            Assert.Equal(0.0, reopened.Values[1]);
            Assert.True(double.IsNaN(reopened.Values[3]));
            Assert.Equal(artifact.ContentSha256, reopened.ContentSha256);
            Assert.Equal([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A], File.ReadAllBytes(pngPath)[..8]);
            Assert.Equal(frame.PixelSha256, Convert.ToHexString(SHA256.HashData(frame.Bgra32Pixels.ToArray())));
        }
        finally
        {
            ReportingTestRoot.DeleteBestEffort(root);
        }
    }

    private static C3DSignedHeightDeltaArtifact CreateArtifact() =>
        C3DSignedHeightDeltaArtifact.Create(
            "step.signed-delta",
            "output.signed-delta",
            "current.source",
            new string('A', 64),
            new string('B', 64),
            64,
            "background.source",
            new string('C', 64),
            new string('D', 64),
            64,
            "mm",
            "frame-1",
            2,
            2,
            11,
            17,
            [-2.0, 0.0, 2.0, double.NaN],
            3,
            3,
            3,
            1,
            1,
            1,
            1,
            "signed-delta-test");
}
