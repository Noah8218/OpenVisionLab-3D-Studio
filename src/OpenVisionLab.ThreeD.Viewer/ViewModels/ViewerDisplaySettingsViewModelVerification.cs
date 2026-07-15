using System.Globalization;
using System.IO;
using System.Numerics;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;
using OpenVisionLab.ThreeD.Viewer.Models;
using OpenVisionLab.ThreeD.Viewer.Rendering;

namespace OpenVisionLab.ThreeD.Viewer.ViewModels;

internal static class ViewerDisplaySettingsViewModelVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        var lines = new List<string>
        {
            "OpenVisionLab 3D display-settings ViewModel verification",
            $"Generated: {DateTimeOffset.Now:O}"
        };
        var passed = 0;

        try
        {
            var viewModel = new ViewerDisplaySettingsViewModel();
            var propertyChanges = 0;
            var renderChanges = 0;
            viewModel.PropertyChanged += (_, _) => propertyChanges++;
            viewModel.RenderSettingsChanged += (_, _) => renderChanges++;

            Check("initial source", viewModel.ActiveSource == "Generated geometry", viewModel.ActiveSource);
            Check("initial geometry", viewModel.EffectiveGeometryStyle == "Points", viewModel.EffectiveSummary);
            Check("initial color", viewModel.EffectiveColorMap == "Height", viewModel.EffectiveSummary);
            Check("initial display-only", viewModel.IsDisplayOnly && !viewModel.CanSelectGeometryStyle, viewModel.EffectiveSummary);
            Check("initial color selectable", viewModel.CanSelectColorMap, viewModel.CanSelectColorMap.ToString());
            Check("initial choices", Sequence(viewModel.AvailableColorMaps, "Solid", "Height"), string.Join(",", viewModel.AvailableColorMaps));
            var initialSettings = viewModel.EffectiveSettings;
            Check(
                "initial typed snapshot",
                initialSettings == new ViewerDisplaySettingsSnapshot(
                    ViewerDisplaySourceKind.GeneratedGeometry,
                    ViewerGeometryStyle.Points,
                    ViewerColorMap.Height,
                    IsDisplayOnly: true),
                initialSettings.ToString());

            viewModel.SelectedGeometryStyle = "Surface";
            Check("geometry bridge guard", viewModel.SelectedGeometryStyle == "Points" && viewModel.FallbackApplied, viewModel.FallbackSummary);
            Check("geometry guard does not render", renderChanges == 0, renderChanges.ToString(CultureInfo.InvariantCulture));

            viewModel.SelectedColorMap = "Solid";
            Check("valid color selection", viewModel.SelectedColorMap == "Solid" && !viewModel.FallbackApplied, viewModel.EffectiveSummary);
            Check("valid color notifies renderer", renderChanges == 1, renderChanges.ToString(CultureInfo.InvariantCulture));
            Check(
                "snapshot is immutable",
                initialSettings.ColorMap == ViewerColorMap.Height
                && viewModel.EffectiveSettings.ColorMap == ViewerColorMap.Solid,
                $"before={initialSettings.ColorMap}|after={viewModel.EffectiveSettings.ColorMap}");

            var c3dViewModel = new ViewerDisplaySettingsViewModel();
            var c3dRenderChanges = 0;
            c3dViewModel.RenderSettingsChanged += (_, _) => c3dRenderChanges++;
            c3dViewModel.ConfigureC3DHeightGrid(deviationAvailable: false);
            Check("C3D source", c3dViewModel.ActiveSource == "C3D height grid", c3dViewModel.ActiveSource);
            Check("C3D geometry choices", Sequence(c3dViewModel.AvailableGeometryStyles, "Points", "Wireframe", "Surface", "Surface + Edges"), string.Join(",", c3dViewModel.AvailableGeometryStyles));
            Check("C3D color choices", Sequence(c3dViewModel.AvailableColorMaps, "Solid", "Height"), string.Join(",", c3dViewModel.AvailableColorMaps));
            Check("C3D current default", c3dViewModel.SelectedGeometryStyle == "Points" && c3dViewModel.SelectedColorMap == "Height", c3dViewModel.EffectiveSummary);
            Check("C3D geometry selectable", c3dViewModel.CanSelectGeometryStyle, c3dViewModel.CanSelectGeometryStyle.ToString());
            Check(
                "C3D typed snapshot",
                c3dViewModel.EffectiveSettings.Source == ViewerDisplaySourceKind.C3DHeightGrid
                && c3dViewModel.EffectiveSettings.GeometryStyle == ViewerGeometryStyle.Points
                && c3dViewModel.EffectiveSettings.ColorMap == ViewerColorMap.Height,
                c3dViewModel.EffectiveSettings.ToString());

            c3dViewModel.SelectedGeometryStyle = "Surface";
            Check(
                "C3D geometry selection",
                c3dViewModel.EffectiveSettings.GeometryStyle == ViewerGeometryStyle.Surface
                && !c3dViewModel.FallbackApplied,
                c3dViewModel.EffectiveSettings.ToString());
            Check("C3D geometry render notification", c3dRenderChanges == 1, c3dRenderChanges.ToString(CultureInfo.InvariantCulture));
            c3dViewModel.SelectedGeometryStyle = "Contours";
            Check(
                "C3D unsupported geometry fallback",
                c3dViewModel.SelectedGeometryStyle == "Surface"
                && c3dViewModel.FallbackApplied
                && c3dRenderChanges == 1,
                c3dViewModel.FallbackSummary);
            c3dViewModel.SelectedGeometryStyle = "Points";
            Check("C3D geometry reset", c3dViewModel.SelectedGeometryStyle == "Points" && c3dRenderChanges == 2, c3dViewModel.EffectiveSummary);

            c3dViewModel.SelectedColorMap = "RGB";
            Check("C3D unsupported color fallback", c3dViewModel.SelectedColorMap == "Height" && c3dViewModel.FallbackApplied, c3dViewModel.FallbackSummary);
            Check("unchanged fallback does not render", c3dRenderChanges == 2, c3dRenderChanges.ToString(CultureInfo.InvariantCulture));

            c3dViewModel.ConfigureC3DHeightGrid(deviationAvailable: true);
            Check("C3D result color capability", c3dViewModel.AvailableColorMaps.Contains("Deviation", StringComparer.Ordinal), string.Join(",", c3dViewModel.AvailableColorMaps));
            c3dViewModel.SelectedColorMap = "Deviation";
            Check("C3D deviation selection", c3dViewModel.SelectedColorMap == "Deviation", c3dViewModel.EffectiveSummary);
            Check("C3D deviation render notification", c3dRenderChanges == 3, c3dRenderChanges.ToString(CultureInfo.InvariantCulture));

            c3dViewModel.ConfigurePointCloud(sourceColorAvailable: true);
            Check("point-cloud geometry capability", Sequence(c3dViewModel.AvailableGeometryStyles, "Points"), string.Join(",", c3dViewModel.AvailableGeometryStyles));
            Check("point-cloud geometry disabled", !c3dViewModel.CanSelectGeometryStyle, c3dViewModel.CanSelectGeometryStyle.ToString());
            Check("point-cloud color capability", Sequence(c3dViewModel.AvailableColorMaps, "Solid", "Height", "RGB"), string.Join(",", c3dViewModel.AvailableColorMaps));
            Check("source-change fallback", c3dViewModel.SelectedColorMap == "RGB" && c3dViewModel.FallbackApplied, c3dViewModel.FallbackSummary);
            Check("deviation fallback is explicit", c3dViewModel.FallbackSummary.Contains("Deviation requires an active result", StringComparison.Ordinal), c3dViewModel.FallbackSummary);

            c3dViewModel.ConfigurePointCloud(sourceColorAvailable: false);
            Check("point-cloud no-RGB capability", Sequence(c3dViewModel.AvailableColorMaps, "Solid", "Height"), string.Join(",", c3dViewModel.AvailableColorMaps));
            Check("point-cloud no-RGB fallback", c3dViewModel.SelectedColorMap == "Height" && c3dViewModel.FallbackApplied, c3dViewModel.FallbackSummary);

            c3dViewModel.ConfigureImportedMesh(sourceColorAvailable: false);
            Check("mesh current geometry", c3dViewModel.SelectedGeometryStyle == "Surface + Edges", c3dViewModel.EffectiveSummary);
            Check("mesh solid capability", Sequence(c3dViewModel.AvailableColorMaps, "Solid") && c3dViewModel.SelectedColorMap == "Solid" && !c3dViewModel.CanSelectColorMap, c3dViewModel.EffectiveSummary);
            c3dViewModel.ConfigureImportedMesh(sourceColorAvailable: true);
            Check("mesh source-color capability", Sequence(c3dViewModel.AvailableColorMaps, "Source") && c3dViewModel.SelectedColorMap == "Source" && !c3dViewModel.CanSelectColorMap, c3dViewModel.EffectiveSummary);

            c3dViewModel.ConfigureNominalActualComparison(deviationAvailable: true);
            Check("nominal-actual current geometry", c3dViewModel.SelectedGeometryStyle == "Points", c3dViewModel.EffectiveSummary);
            Check("nominal-actual deviation", Sequence(c3dViewModel.AvailableColorMaps, "Deviation") && c3dViewModel.SelectedColorMap == "Deviation", c3dViewModel.EffectiveSummary);

            var pointOnlyC3D = new ViewerDisplaySettingsViewModel();
            pointOnlyC3D.ConfigureC3DHeightGrid(deviationAvailable: false, surfaceGeometryAvailable: false);
            Check(
                "C3D point-only capability",
                Sequence(pointOnlyC3D.AvailableGeometryStyles, "Points")
                && !pointOnlyC3D.CanSelectGeometryStyle,
                pointOnlyC3D.EffectiveSummary);
            pointOnlyC3D.SelectedGeometryStyle = "Surface";
            Check(
                "C3D point-only guard",
                pointOnlyC3D.SelectedGeometryStyle == "Points"
                && pointOnlyC3D.FallbackApplied
                && pointOnlyC3D.FallbackSummary.Contains(
                    "not selectable for C3D height grid",
                    StringComparison.Ordinal),
                pointOnlyC3D.FallbackSummary);

            var renderProxy = C3DHeightGridRenderProxy.Create(
                [GridPoint(0, 0), GridPoint(0, 1), GridPoint(1, 0), GridPoint(1, 1)],
                pointStride: 1);
            Check(
                "render proxy quad topology",
                renderProxy.TriangleCount == 2
                && renderProxy.EdgeCount == 5
                && renderProxy.GridEdgeCount == 4
                && renderProxy.SurfaceEdgeCount == 4
                && renderProxy.TriangleIndices.SequenceEqual([0, 2, 1, 1, 2, 3]),
                $"triangles={renderProxy.TriangleCount}|edges={renderProxy.EdgeCount}|gridEdges={renderProxy.GridEdgeCount}|surfaceEdges={renderProxy.SurfaceEdgeCount}");
            Check(
                "render proxy unique edges",
                CountUniqueEdges(renderProxy.EdgeIndices) == renderProxy.EdgeCount,
                renderProxy.EdgeCount.ToString(CultureInfo.InvariantCulture));
            Check(
                "render proxy unique grid edges",
                CountUniqueEdges(renderProxy.GridEdgeIndices) == renderProxy.GridEdgeCount,
                renderProxy.GridEdgeCount.ToString(CultureInfo.InvariantCulture));
            Check(
                "render proxy unique surface edges",
                CountUniqueEdges(renderProxy.SurfaceEdgeIndices) == renderProxy.SurfaceEdgeCount,
                renderProxy.SurfaceEdgeCount.ToString(CultureInfo.InvariantCulture));
            var holeProxy = C3DHeightGridRenderProxy.Create(
                [GridPoint(0, 0), GridPoint(0, 1), GridPoint(1, 0)],
                pointStride: 1);
            Check(
                "render proxy does not bridge holes",
                !holeProxy.HasSurface
                && holeProxy.EdgeCount == 0
                && holeProxy.GridEdgeCount == 0
                && holeProxy.SurfaceEdgeCount == 0,
                $"triangles={holeProxy.TriangleCount}|edges={holeProxy.EdgeCount}|gridEdges={holeProxy.GridEdgeCount}|surfaceEdges={holeProxy.SurfaceEdgeCount}");
            var strideProxy = C3DHeightGridRenderProxy.Create(
                [GridPoint(0, 0), GridPoint(0, 2), GridPoint(2, 0), GridPoint(2, 2)],
                pointStride: 2);
            Check(
                "render proxy respects stride",
                strideProxy.TriangleCount == 2
                && strideProxy.EdgeCount == 5
                && strideProxy.GridEdgeCount == 4
                && strideProxy.SurfaceEdgeCount == 4,
                $"triangles={strideProxy.TriangleCount}|edges={strideProxy.EdgeCount}|gridEdges={strideProxy.GridEdgeCount}|surfaceEdges={strideProxy.SurfaceEdgeCount}");
            var sampledEdgeProxy = C3DHeightGridRenderProxy.Create(
                Enumerable.Range(0, 5)
                    .SelectMany(row => Enumerable.Range(0, 5).Select(column => GridPoint(row, column)))
                    .ToArray(),
                pointStride: 1);
            Check(
                "render proxy samples surface overlay edges",
                sampledEdgeProxy.GridEdgeCount == 40
                && sampledEdgeProxy.SurfaceEdgeCount == 16
                && sampledEdgeProxy.SurfaceEdgeCount < sampledEdgeProxy.GridEdgeCount,
                $"gridEdges={sampledEdgeProxy.GridEdgeCount}|surfaceEdges={sampledEdgeProxy.SurfaceEdgeCount}|interval={C3DHeightGridRenderProxy.SurfaceEdgeSampleInterval}");
            Check(
                "render proxy rejects duplicate cells",
                Throws<InvalidDataException>(() => C3DHeightGridRenderProxy.Create([GridPoint(0, 0), GridPoint(0, 0)], 1)),
                "duplicate rejected");
            Check(
                "render proxy rejects invalid stride",
                Throws<ArgumentOutOfRangeException>(() => C3DHeightGridRenderProxy.Create([GridPoint(0, 0)], 0)),
                "zero stride rejected");

            var rootViewModel = new MainWindowViewModel();
            var previewRequests = 0;
            var publishRequests = 0;
            rootViewModel.NominalActual.PreviewRequested += (_, _) => previewRequests++;
            rootViewModel.NominalActual.PublishRequested += (_, _) => publishRequests++;
            var nominalState = rootViewModel.NominalActual.State;

            rootViewModel.Display.SelectedColorMap = "Solid";
            Check(
                "root color compatibility snapshot",
                rootViewModel.Display.EffectiveSettings.ColorMap == ViewerColorMap.Solid
                && rootViewModel.SelectedColorMode == "Solid",
                $"snapshot={rootViewModel.Display.EffectiveSettings.ColorMap}|bridge={rootViewModel.SelectedColorMode}");
            Check("display change does not preview", previewRequests == 0 && rootViewModel.NominalActual.State == nominalState, $"requests={previewRequests}|state={rootViewModel.NominalActual.State}");
            Check("display change does not publish", publishRequests == 0, publishRequests.ToString(CultureInfo.InvariantCulture));

            rootViewModel.SetC3DDisplayCapabilities(surfaceGeometryAvailable: true);
            rootViewModel.UseC3DSmokeScene();
            Check("root C3D context", rootViewModel.Display.ActiveSource == "C3D height grid" && rootViewModel.Display.SelectedColorMap == "Height", rootViewModel.Display.EffectiveSummary);
            var geometryRevision = rootViewModel.DisplaySettingsRevision;
            rootViewModel.Display.SelectedGeometryStyle = "Surface + Edges";
            Check(
                "root geometry snapshot bridge",
                rootViewModel.Display.EffectiveSettings.GeometryStyle == ViewerGeometryStyle.SurfaceWithEdges
                && rootViewModel.SelectedGeometryStyle == "Surface + Edges"
                && rootViewModel.DisplaySettingsRevision == geometryRevision + 1,
                $"snapshot={rootViewModel.Display.EffectiveSettings.GeometryStyle}|bridge={rootViewModel.SelectedGeometryStyle}|revision={rootViewModel.DisplaySettingsRevision}");
            rootViewModel.SelectedColorMode = "RGB";
            Check("root unavailable color fallback", rootViewModel.SelectedColorMode == "Height" && rootViewModel.Display.FallbackApplied, rootViewModel.Display.FallbackSummary);

            var flatnessReference = new[]
            {
                new HeightFieldPlaneSample(new Vector3(0.0f, 0.0f, 0.0f), 0.0),
                new HeightFieldPlaneSample(new Vector3(1.0f, 0.0f, 0.0f), 0.0),
                new HeightFieldPlaneSample(new Vector3(0.0f, 0.0f, 1.0f), 0.0),
            };
            var flatnessEvaluation = PlaneFlatnessRule.Evaluate(new PlaneFlatnessRuleInput(
                MainWindowViewModel.C3DEntityId,
                flatnessReference,
                [.. flatnessReference, new HeightFieldPlaneSample(new Vector3(0.5f, 0.1f, 0.5f), 0.1)],
                0.2,
                "model"));
            rootViewModel.SetPlaneFlatnessPreview(flatnessEvaluation);
            Check(
                "root C3D result selects deviation",
                rootViewModel.PlaneFlatnessVisible
                && rootViewModel.SelectedColorMode == "Deviation"
                && rootViewModel.Display.EffectiveSettings.ColorMap == ViewerColorMap.Deviation,
                rootViewModel.Display.EffectiveSummary);

            rootViewModel.SetLazDisplayCapabilities(sourceColorAvailable: true);
            rootViewModel.UseLazPointSmokeScene();
            Check("root point-cloud context", rootViewModel.Display.ActiveSource == "LAZ/LAS point cloud" && rootViewModel.SelectedColorMode == "RGB", rootViewModel.Display.EffectiveSummary);
            Check("root point-cloud typed color", rootViewModel.Display.EffectiveSettings.ColorMap == ViewerColorMap.Rgb, rootViewModel.Display.EffectiveSettings.ToString());
            rootViewModel.SelectedColorMode = "Deviation";
            Check("root deviation guard mode", rootViewModel.SelectedColorMode == "RGB", rootViewModel.Display.EffectiveSummary);
            Check("root deviation guard status", rootViewModel.ViewerStatus.Contains("Deviation requires an active result", StringComparison.Ordinal), rootViewModel.ViewerStatus);

            rootViewModel.SetImportedMeshDisplayCapabilities(sourceColorAvailable: true);
            rootViewModel.UseGlbSmokeScene();
            Check("root mesh context", rootViewModel.Display.ActiveSource == "Imported triangle mesh", rootViewModel.Display.EffectiveSummary);
            Check("root mesh effective settings", rootViewModel.Display.SelectedGeometryStyle == "Surface + Edges" && rootViewModel.SelectedColorMode == "Source", rootViewModel.Display.EffectiveSummary);
            Check("property notifications", propertyChanges > 0, propertyChanges.ToString(CultureInfo.InvariantCulture));

            summary = $"Display-settings ViewModel verification: Pass ({passed} checks)";
            lines.Add(summary);
            WriteReport(reportPath, lines);
            return true;
        }
        catch (Exception exception)
        {
            summary = $"Display-settings ViewModel verification: Fail after {passed} checks: {exception.Message}";
            lines.Add(summary);
            WriteReport(reportPath, lines);
            return false;
        }

        void Check(string name, bool condition, string detail)
        {
            if (!condition)
            {
                throw new InvalidOperationException($"{name}: {detail}");
            }

            passed++;
            lines.Add($"PASS|{name}|{detail}");
        }
    }

    private static bool Sequence(IReadOnlyList<string> actual, params string[] expected) =>
        actual.SequenceEqual(expected, StringComparer.Ordinal);

    private static HeightGridPoint GridPoint(int row, int column) =>
        new(new Vector3(column, 0.0f, row), 0.5, 0.0, 1.0f, row, column);

    private static int CountUniqueEdges(IReadOnlyList<int> edgeIndices)
    {
        var edges = new HashSet<(int Minimum, int Maximum)>();
        for (var index = 0; index < edgeIndices.Count; index += 2)
        {
            edges.Add((
                Math.Min(edgeIndices[index], edgeIndices[index + 1]),
                Math.Max(edgeIndices[index], edgeIndices[index + 1])));
        }

        return edges.Count;
    }

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

    private static void WriteReport(string reportPath, IEnumerable<string> lines)
    {
        var fullPath = Path.GetFullPath(reportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllLines(fullPath, lines);
    }
}
