using System.Text;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Tools;

internal static class SurfaceEdgeAcquisitionDirectionVerification
{
    private const string FrameId = "frame.synthetic-acquisition";
    private static readonly string SourceSha256 = new('B', 64);

    public static int Run(string reportPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        var overlay = CreateOverlay();
        var rawOverlaySha256 = overlay.ContentSha256;
        var direction = new ToolRecipeAcquisitionDirection(
            ToolRecipeAcquisitionDirectionState.Available,
            ToolRecipeAcquisitionDirectionConvention.SensorToScene,
            FrameId,
            new ToolRecipeXyz(0.0, 0.0, -2.0));
        var artifact = SurfaceEdgeAcquisitionDirectionBuilder.Build(
            overlay,
            SourceSha256,
            direction,
            0.05);

        var cases = new List<(string Name, bool Passed, string Evidence)>
        {
            (
                "explicit-direction-classifies-in-source-order",
                SurfaceEdgeAcquisitionDirectionArtifactValidator.Inspect(artifact, overlay).IsValid
                && artifact.NormalizedSensorToSceneDirection == new SurfaceModelPoint3(0.0, 0.0, -1.0)
                && artifact.Items.Select(item => item.Orientation).SequenceEqual(
                    [
                        SurfaceEdgeAcquisitionOrientation.SensorFacing,
                        SurfaceEdgeAcquisitionOrientation.AwayFromSensor,
                        SurfaceEdgeAcquisitionOrientation.Grazing
                    ]),
                $"direction={artifact.NormalizedSensorToSceneDirection};orientations={string.Join(',', artifact.Items.Select(item => item.Orientation))}"),
            (
                "unavailable-direction-is-not-inferred",
                Throws<InvalidDataException>(() =>
                    SurfaceEdgeAcquisitionDirectionBuilder.Build(
                        overlay,
                        SourceSha256,
                        ToolRecipeAcquisitionDirection.CreateUnavailable(FrameId))),
                "Unavailable evidence is rejected instead of inferred from geometry."),
            (
                "frame-mismatch-is-rejected",
                Throws<InvalidDataException>(() =>
                    SurfaceEdgeAcquisitionDirectionBuilder.Build(
                        overlay,
                        SourceSha256,
                        direction with { FrameId = "frame.other" })),
                $"overlayFrame={FrameId};directionFrame=frame.other"),
            (
                "content-tamper-is-rejected",
                !SurfaceEdgeAcquisitionDirectionArtifactValidator.Inspect(
                    artifact with
                    {
                        Items = artifact.Items.Select((item, index) =>
                            index == 0
                                ? item with { AlignmentCosine = 0.25 }
                                : item).ToArray()
                    },
                    overlay).IsValid,
                $"originalSha256={artifact.ContentSha256}"),
            (
                "raw-edge-overlay-identity-is-unchanged",
                rawOverlaySha256 == overlay.ContentSha256
                && artifact.EdgeDiagnosticOverlayContentSha256 == rawOverlaySha256,
                $"rawOverlaySha256={rawOverlaySha256};orientationSha256={artifact.ContentSha256}")
        };

        var passed = cases.Count(item => item.Passed);
        var lines = new List<string>
        {
            $"SurfaceEdgeAcquisitionDirectionVerification|{(passed == cases.Count ? "PASS" : "FAIL")}|cases={cases.Count}|passed={passed}|failed={cases.Count - passed}",
            $"Contract|convention=SensorToScene|frame={FrameId}|grazingAbsoluteCosineMaximum=0.05|sourceSha256={SourceSha256}|scoreChanged=false|acceptanceChanged=false|inference=false"
        };
        lines.AddRange(cases.Select(item =>
            $"{(item.Passed ? "PASS" : "FAIL")} | {item.Name} | {item.Evidence}"));
        var fullPath = Path.GetFullPath(reportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath) ?? Environment.CurrentDirectory);
        File.WriteAllLines(fullPath, lines, new UTF8Encoding(false));
        Console.WriteLine($"Surface-edge acquisition direction verification: {(passed == cases.Count ? "Pass" : "Fail")} ({passed}/{cases.Count})");
        return passed == cases.Count ? 0 : 1;
    }

    private static SurfaceEdgeDiagnosticOverlayArtifact CreateOverlay()
    {
        var unitX = new SurfaceModelPoint3(1.0, 0.0, 0.0);
        var segments = new[]
        {
            CreateSegment(0, 0.0, new SurfaceModelPoint3(0.0, 0.0, 1.0)),
            CreateSegment(1, 1.0, new SurfaceModelPoint3(0.0, 0.0, -1.0)),
            CreateSegment(2, 2.0, unitX)
        };
        var artifact = new SurfaceEdgeDiagnosticOverlayArtifact(
            SurfaceEdgeDiagnosticOverlayArtifact.CurrentSchemaVersion,
            SurfaceEdgeDiagnosticOverlayArtifact.CurrentSemantics,
            new string('1', 64),
            new string('2', 64),
            new string('3', 64),
            new string('4', 64),
            new string('5', 64),
            new string('6', 64),
            "mm",
            FrameId,
            segments,
            [],
            string.Empty);
        return artifact with
        {
            ContentSha256 = SurfaceEdgeDiagnosticOverlayArtifact.CalculateContentSha256(artifact)
        };
    }

    private static SurfaceEdgeModelDiagnosticSegment CreateSegment(
        int order,
        double x,
        SurfaceModelPoint3 normal) => new(
        order,
        new SurfaceModelPoint3(x, 0.0, 0.0),
        new SurfaceModelPoint3(x + 0.5, 0.0, 0.0),
        new SurfaceModelPoint3(x + 0.25, 0.0, 0.0),
        new SurfaceModelPoint3(1.0, 0.0, 0.0),
        normal,
        ModelSurfaceEdgeKind.Boundary,
        false,
        null);

    private static bool Throws<TException>(Action action)
        where TException : Exception
    {
        try
        {
            action();
            return false;
        }
        catch (TException)
        {
            return true;
        }
    }
}
