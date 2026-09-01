using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Viewer.Models;

namespace OpenVisionLab.ThreeD.Viewer.Teaching;

internal static class TeachingCapturePreparationVerification
{
    public static bool Verify(out string summary)
    {
        var passed = 0;
        var failed = 0;
        var details = new List<string>();

        void Check(string name, bool condition, string detail)
        {
            details.Add($"{(condition ? "PASS" : "FAIL")} | {name} | {detail}");
            if (condition)
            {
                passed++;
            }
            else
            {
                failed++;
            }
        }

        try
        {
            const string sourceSha = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
            var source = new TeachingCaptureSourceSnapshot(64, 48, 0.25f, 42.0, sourceSha);
            var binding = new ToolRecipeSelectionSourceBinding("C3D", sourceSha, 64, 48);
            var rectangleRequest = Request(
                "selection.rectangle",
                ToolRecipeSelectionKinds.GridRectangle,
                binding);
            var rectangleSelection = new ToolRecipeSelection(
                rectangleRequest.SelectionId,
                "Rectangle",
                rectangleRequest.Kind,
                rectangleRequest.RootSourceId,
                rectangleRequest.FrameId,
                binding,
                new ToolRecipeGridRectangle(2, 3, 7, 10),
                null,
                null);
            var rectanglePreparation = TeachingCapturePreparation.Prepare(
                rectangleRequest,
                rectangleSelection,
                true,
                source,
                null);
            Check(
                "valid C3D rectangle preparation",
                rectanglePreparation.IsValid
                && rectanglePreparation.InitialPoints is
                    [
                        { Locator.Row: 2, Locator.Column: 3, RawHeight: 42.0 },
                        { Locator.Row: 8, Locator.Column: 12, RawHeight: 42.0 }
                    ],
                rectanglePreparation.Message);

            var circle = new ToolRecipeGridCircle(10, 12, 3.75);
            var circleRequest = Request(
                "selection.circle",
                ToolRecipeSelectionKinds.GridCircle,
                binding);
            var circlePreparation = TeachingCapturePreparation.Prepare(
                circleRequest,
                new ToolRecipeSelection(
                    circleRequest.SelectionId,
                    "Circle",
                    circleRequest.Kind,
                    circleRequest.RootSourceId,
                    circleRequest.FrameId,
                    binding,
                    null,
                    null,
                    null,
                    GridCircle: circle),
                true,
                source,
                null);
            Check(
                "valid C3D circle preparation",
                circlePreparation.IsValid
                && circlePreparation.InitialGridCircle == circle
                && circlePreparation.InitialPoints is
                    [
                        { Locator.Row: 10, Locator.Column: 12 },
                        { Locator.Row: 10, Locator.Column: 15 }
                    ],
                circlePreparation.Message);

            var polygon = new ToolRecipeGridPolygon(
            [
                new ToolRecipeGridPolygonVertex(4, 6),
                new ToolRecipeGridPolygonVertex(4, 14),
                new ToolRecipeGridPolygonVertex(12, 14),
                new ToolRecipeGridPolygonVertex(12, 6)
            ]);
            var polygonRequest = Request(
                "selection.polygon",
                ToolRecipeSelectionKinds.GridPolygon,
                binding);
            var polygonPreparation = TeachingCapturePreparation.Prepare(
                polygonRequest,
                new ToolRecipeSelection(
                    polygonRequest.SelectionId,
                    "Polygon",
                    polygonRequest.Kind,
                    polygonRequest.RootSourceId,
                    polygonRequest.FrameId,
                    binding,
                    null,
                    null,
                    null,
                    GridPolygon: polygon),
                true,
                source,
                null);
            Check(
                "valid C3D polygon preparation",
                polygonPreparation.IsValid
                && polygonPreparation.InitialGridPolygon == polygon
                && polygonPreparation.InitialPoints?.Count == 4
                && polygonPreparation.InitialPoints[0].Locator == new ToolRecipeGridCellLocator("grid-cell", 4, 6)
                && polygonPreparation.InitialPoints[3].Locator == new ToolRecipeGridCellLocator("grid-cell", 12, 6),
                polygonPreparation.Message);

            var invalidRectangle = TeachingCapturePreparation.TryCreateGridRectanglePoints(
                source,
                new ToolRecipeGridRectangle(47, 3, 2, 4));
            Check(
                "out-of-grid rectangle rejected",
                !invalidRectangle.IsValid && invalidRectangle.Points is null,
                invalidRectangle.Message);

            var invalidPolygon = TeachingCapturePreparation.TryCreateGridPolygonPoints(
                source,
                new ToolRecipeGridPolygon(
                [
                    new ToolRecipeGridPolygonVertex(4, 6),
                    new ToolRecipeGridPolygonVertex(4, 6),
                    new ToolRecipeGridPolygonVertex(12, 6)
                ]));
            Check(
                "degenerate polygon rejected",
                !invalidPolygon.IsValid && invalidPolygon.Points is null,
                invalidPolygon.Message);

            var hiddenSource = TeachingCapturePreparation.Prepare(
                rectangleRequest,
                null,
                false,
                source,
                null);
            Check(
                "hidden C3D source rejected",
                !hiddenSource.IsValid
                && hiddenSource.Message.Contains("visible C3D source", StringComparison.Ordinal),
                hiddenSource.Message);

            var staleBinding = TeachingCapturePreparation.Prepare(
                rectangleRequest with
                {
                    SourceBinding = binding with { ContentSha256 = new string('B', 64) }
                },
                null,
                true,
                source,
                null);
            Check(
                "stale C3D source identity rejected",
                !staleBinding.IsValid
                && staleBinding.Message.Contains("does not match", StringComparison.Ordinal),
                staleBinding.Message);

            var invalidSha = TeachingCapturePreparation.Prepare(
                rectangleRequest with
                {
                    SourceBinding = binding with { ContentSha256 = "not-a-sha" }
                },
                null,
                true,
                source,
                null);
            Check(
                "invalid source identity shape rejected",
                !invalidSha.IsValid
                && invalidSha.Message.Contains("valid C3D source SHA-256", StringComparison.Ordinal),
                invalidSha.Message);

            var transformedRequest = rectangleRequest with
            {
                SourceBinding = binding with { Format = "TransformedHeightField" }
            };
            var missingTransformed = TeachingCapturePreparation.Prepare(
                transformedRequest,
                null,
                false,
                null,
                null);
            Check(
                "missing transformed field rejected",
                !missingTransformed.IsValid
                && missingTransformed.Message.Contains("TransformedHeightField", StringComparison.Ordinal),
                missingTransformed.Message);
        }
        catch (Exception exception)
        {
            failed++;
            details.Add($"FAIL | unhandled verification exception | {exception}");
        }

        summary = failed == 0
            ? $"Pass ({passed}/{passed} checks)"
            : $"Fail ({failed} failed, {passed} passed)";
        if (details.Count > 0)
        {
            summary += Environment.NewLine + string.Join(Environment.NewLine, details);
        }
        return failed == 0;
    }

    private static TeachingCaptureRequest Request(
        string id,
        string kind,
        ToolRecipeSelectionSourceBinding binding) =>
        new(
            id,
            id,
            kind,
            kind == ToolRecipeSelectionKinds.GridPolygon ? 3 : 2,
            "source.c3d.height-map",
            "frame.c3d-grid-index",
            binding);
}
