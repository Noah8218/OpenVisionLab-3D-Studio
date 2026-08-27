using System.Globalization;
using System.Text;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

internal static class C3DConnectedRegionGoldenVerification
{
    public static int Run(string reportPath)
    {
        var fullReportPath = Path.GetFullPath(reportPath);
        var directory = Path.GetDirectoryName(fullReportPath)
            ?? Environment.CurrentDirectory;
        Directory.CreateDirectory(directory);

        var sourceValues = Enumerable.Range(1, 20)
            .Select(value => (double)value)
            .ToArray();
        var sourceExpectedValues = sourceValues.ToArray();
        var source = C3DHeightFieldSnapshot.CreateForVerification(
            "source.connected-region.fixture",
            5,
            4,
            sourceValues,
            "raw-height",
            "frame.connected-region.fixture");
        sourceValues[0] = 999d;

        var maskValues =
            new[]
            {
                true, true, false, false, false,
                true, false, false, true, false,
                false, false, false, true, true,
                false, false, false, false, false
            };
        var maskExpectedValues = maskValues.ToArray();
        var callerOwnedMaskValues = maskValues.ToArray();
        var mask = new C3DConnectedRegionMask(
            "mask.connected-region.fixture",
            source.EntityId,
            source.ContentSha256,
            source.Width,
            source.Height,
            callerOwnedMaskValues);
        callerOwnedMaskValues[0] = false;

        var input = new C3DConnectedRegionInput(
            "result.connected-region.fixture",
            source.RootSourceSha256,
            source,
            mask,
            C3DConnectedRegionConnectivity.Four);
        var first = C3DConnectedRegionRule.Evaluate(input);
        var repeated = C3DConnectedRegionRule.Evaluate(input);

        var diagonalSource = C3DHeightFieldSnapshot.CreateForVerification(
            "source.connected-region.diagonal",
            3,
            2,
            [1d, 2d, 3d, 4d, 5d, 6d]);
        var diagonalValues = new[] { true, false, false, false, true, false };
        var diagonalMask = new C3DConnectedRegionMask(
            "mask.connected-region.diagonal",
            diagonalSource.EntityId,
            diagonalSource.ContentSha256,
            diagonalSource.Width,
            diagonalSource.Height,
            diagonalValues);
        var diagonalFour = C3DConnectedRegionRule.Evaluate(
            new C3DConnectedRegionInput(
                "result.connected-region.diagonal-four",
                diagonalSource.RootSourceSha256,
                diagonalSource,
                diagonalMask,
                C3DConnectedRegionConnectivity.Four));
        var diagonalEight = C3DConnectedRegionRule.Evaluate(
            new C3DConnectedRegionInput(
                "result.connected-region.diagonal-eight",
                diagonalSource.RootSourceSha256,
                diagonalSource,
                diagonalMask,
                C3DConnectedRegionConnectivity.Eight));

        var emptyMask = new C3DConnectedRegionMask(
            "mask.connected-region.empty",
            source.EntityId,
            source.ContentSha256,
            source.Width,
            source.Height,
            new bool[source.Width * source.Height]);
        var empty = C3DConnectedRegionRule.Evaluate(
            input with { Mask = emptyMask });

        var mismatchedMask = new C3DConnectedRegionMask(
            "mask.connected-region.mismatch",
            "source.other",
            source.ContentSha256,
            source.Width,
            source.Height,
            maskExpectedValues);
        var mismatch = C3DConnectedRegionRule.Evaluate(
            input with { Mask = mismatchedMask });

        var missingSource = C3DHeightFieldSnapshot.CreateForVerification(
            "source.connected-region.missing",
            5,
            4,
            [double.NaN, 2d, 3d, 4d, 5d, 6d, 7d, 8d, 9d, 10d, 11d, 12d, 13d, 14d, 15d, 16d, 17d, 18d, 19d, 20d]);
        var missingMask = new C3DConnectedRegionMask(
            "mask.connected-region.missing",
            missingSource.EntityId,
            missingSource.ContentSha256,
            missingSource.Width,
            missingSource.Height,
            maskExpectedValues);
        var missingForeground = C3DConnectedRegionRule.Evaluate(
            new C3DConnectedRegionInput(
                "result.connected-region.missing",
                missingSource.RootSourceSha256,
                missingSource,
                missingMask));

        var invalidConnectivity = C3DConnectedRegionRule.Evaluate(
            input with
            {
                Connectivity = (C3DConnectedRegionConnectivity)99
            });

        var cases = new List<(string Name, bool Passed, string Evidence)>
        {
            Check(
                "deterministic-two-region-labeling",
                () => first.Result.Status == ResultStatus.Pass
                    && first.Output is { RegionCount: 2, ForegroundCellCount: 6, VisitedCellCount: 6 }
                    && first.Output.Regions.Select(region => region.CellCount)
                        .SequenceEqual([3, 3]),
                () =>
                    $"status={first.Result.Status};regions={first.Output?.RegionCount};foreground={first.Output?.ForegroundCellCount};counts={string.Join(',', first.Output?.Regions.Select(region => region.CellCount) ?? [])}"),
            Check(
                "row-major-region-and-cell-order",
                () => first.Output?.Regions.Select(region => region.RegionId)
                        .SequenceEqual([
                            "result.connected-region.fixture.region.001",
                            "result.connected-region.fixture.region.002"])
                    == true
                    && first.Output.Regions[0].Cells.SequenceEqual([
                        new C3DConnectedRegionCell(0, 0),
                        new C3DConnectedRegionCell(0, 1),
                        new C3DConnectedRegionCell(1, 0)])
                    && first.Output.Regions[1].Cells.SequenceEqual([
                        new C3DConnectedRegionCell(1, 3),
                        new C3DConnectedRegionCell(2, 3),
                        new C3DConnectedRegionCell(2, 4)]),
                () => string.Join(
                    ";",
                    first.Output?.Regions.Select(region =>
                        $"{region.RegionId}=[{string.Join(',', region.Cells.Select(cell => $"{cell.Row}:{cell.Column}"))}]")
                    ?? [])),
            Check(
                "sdk-metrics-mapped-with-grid-index-contract",
                () => first.Output is not null
                    && Approximately(first.Output.Regions[0].Area, 3d)
                    && Approximately(first.Output.Regions[1].Area, 3d)
                    && Approximately(first.Output.Regions[0].CenterX, 1d / 3d)
                    && Approximately(first.Output.Regions[0].CenterY, 1d / 3d)
                    && Approximately(first.Output.Regions[0].OrientationDegrees, 135d)
                    && Approximately(first.Output.Regions[1].CenterX, 10d / 3d)
                    && Approximately(first.Output.Regions[1].CenterY, 5d / 3d)
                    && Approximately(first.Output.Regions[1].OrientationDegrees, 45d)
                    && Metric(first.Result, "Total region area") == 6d
                    && first.Output.Regions.All(region =>
                        region.CoordinateConvention == "GridXGridYCellCenterFootprint"),
                () => first.Output is null
                    ? "no output"
                    : $"areas={string.Join(',', first.Output.Regions.Select(region => region.Area.ToString("R", CultureInfo.InvariantCulture)))};centers={string.Join(';', first.Output.Regions.Select(region => $"{region.CenterX:R},{region.CenterY:R}"))};orientations={string.Join(',', first.Output.Regions.Select(region => region.OrientationDegrees.ToString("R", CultureInfo.InvariantCulture)))}"),
            Check(
                "repeatability-and-identity-hash",
                () => first.Output is not null
                    && repeated.Output is not null
                    && first.Output.ContentSha256 == repeated.Output.ContentSha256
                    && first.Output.MaskContentSha256 == mask.ContentSha256
                    && RegionsEquivalent(first.Output.Regions, repeated.Output.Regions)
                    && Metric(first.Result, "Region count") == 2d
                    && Metric(first.Result, "Foreground cell count") == 6d,
                () => $"first={first.Output?.ContentSha256};repeated={repeated.Output?.ContentSha256};mask={mask.ContentSha256}"),
            Check(
                "source-and-mask-immutability",
                () => source.Values.Span.SequenceEqual(sourceExpectedValues)
                    && mask.Foreground.SequenceEqual(maskExpectedValues),
                () => $"sourceUnchanged={source.Values.Span.SequenceEqual(sourceExpectedValues)};maskUnchanged={mask.Foreground.SequenceEqual(maskExpectedValues)}"),
            Check(
                "four-versus-eight-diagonal-connectivity",
                () => diagonalFour.Result.Status == ResultStatus.Pass
                    && diagonalEight.Result.Status == ResultStatus.Pass
                    && diagonalFour.Output?.RegionCount == 2
                    && diagonalEight.Output?.RegionCount == 1
                    && diagonalFour.Output.Regions.All(region => region.CellCount == 1)
                    && diagonalEight.Output.Regions[0].CellCount == 2,
                () => $"four={diagonalFour.Output?.RegionCount};eight={diagonalEight.Output?.RegionCount};eightCells={diagonalEight.Output?.Regions.FirstOrDefault()?.CellCount}"),
            Check(
                "empty-mask-fails-closed",
                () => empty.Result.Status == ResultStatus.Error
                    && empty.Output is null
                    && empty.Result.Message.Contains("at least one foreground", StringComparison.OrdinalIgnoreCase),
                () => $"status={empty.Result.Status};output={empty.Output is not null};message={empty.Result.Message}"),
            Check(
                "source-binding-fails-closed",
                () => mismatch.Result.Status == ResultStatus.Error
                    && mismatch.Output is null
                    && mismatch.Result.Message.Contains("exact current source identity", StringComparison.OrdinalIgnoreCase),
                () => $"status={mismatch.Result.Status};output={mismatch.Output is not null};message={mismatch.Result.Message}"),
            Check(
                "foreground-missing-height-fails-closed",
                () => missingForeground.Result.Status == ResultStatus.Error
                    && missingForeground.Output is null
                    && missingForeground.Result.Message.Contains("finite source heights", StringComparison.OrdinalIgnoreCase),
                () => $"status={missingForeground.Result.Status};output={missingForeground.Output is not null};message={missingForeground.Result.Message}"),
            Check(
                "invalid-connectivity-fails-closed",
                () => invalidConnectivity.Result.Status == ResultStatus.Error
                    && invalidConnectivity.Output is null,
                () => $"status={invalidConnectivity.Result.Status};output={invalidConnectivity.Output is not null};message={invalidConnectivity.Result.Message}")
        };

        var passed = cases.Count(item => item.Passed);
        var lines = new List<string>
        {
            $"ConnectedRegionVerification|{(passed == cases.Count ? "PASS" : "FAIL")}|cases={cases.Count}|passed={passed}|failed={cases.Count - passed}",
            $"Contract|schema={C3DConnectedRegionOutput.ContractVersion}|maskSchema={C3DConnectedRegionMask.ContractVersion}|connectivity=Four,Eight|sourceBinding=entity+contentSha256+grid+frame|areaUnit=grid-index²|physicalCalibration=false|ui=false",
            $"Sdk|package=OpenVisionLab.Vision3D {VisionSdkHeightMapInspection.PackageVersion}|sourceCommit={VisionSdkHeightMapInspection.PackageSourceCommit}|tools=ConnectedRegionTool,ConnectedRegionMetricsTool",
            $"Output|regions={first.Output?.RegionCount}|foreground={first.Output?.ForegroundCellCount}|totalArea={Metric(first.Result, "Total region area").ToString("R", CultureInfo.InvariantCulture)}|sha256={first.Output?.ContentSha256}"
        };
        lines.AddRange(cases.Select(item =>
            $"{item.Name}|{(item.Passed ? "PASS" : "FAIL")}|{item.Evidence}"));
        File.WriteAllLines(fullReportPath, lines, new UTF8Encoding(false));
        Console.WriteLine(
            $"Connected-region verification: {(passed == cases.Count ? "PASS" : "FAIL")} ({passed}/{cases.Count})");
        return passed == cases.Count ? 0 : 1;
    }

    private static double Metric(ToolResult result, string name) =>
        result.Metrics.Single(metric => metric.Name == name).Value;

    private static bool Approximately(double actual, double expected, double tolerance = 1e-9) =>
        double.IsFinite(actual) && Math.Abs(actual - expected) <= tolerance;

    private static bool RegionsEquivalent(
        IReadOnlyList<C3DConnectedRegionMetricOutput> left,
        IReadOnlyList<C3DConnectedRegionMetricOutput> right) =>
        left.Count == right.Count
        && left.Zip(right).All(pair =>
            pair.First.RegionId == pair.Second.RegionId
            && pair.First.Index == pair.Second.Index
            && pair.First.CellCount == pair.Second.CellCount
            && Approximately(pair.First.Area, pair.Second.Area)
            && Approximately(pair.First.CenterX, pair.Second.CenterX)
            && Approximately(pair.First.CenterY, pair.Second.CenterY)
            && pair.First.HasOrientation == pair.Second.HasOrientation
            && (!pair.First.HasOrientation
                || Approximately(pair.First.OrientationDegrees, pair.Second.OrientationDegrees))
            && pair.First.Cells.SequenceEqual(pair.Second.Cells));

    private static (string Name, bool Passed, string Evidence) Check(
        string name,
        Func<bool> assertion,
        Func<string> evidence)
    {
        try
        {
            var passed = assertion();
            return (name, passed, evidence());
        }
        catch (Exception exception)
        {
            return (name, false, $"unexpected {exception.GetType().Name}: {exception.Message}");
        }
    }
}
