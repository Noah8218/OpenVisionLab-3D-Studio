using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

internal static class C3DCropAxisGridRoundTripVerification
{
    private const int SourceWidth = 7;
    private const int SourceHeight = 6;
    private static readonly GridAxisFixture Axis = new(
        Origin: (100d, 200d, 300d),
        U: (1d, 0d, 0d),
        V: (0d, 0d, -1d),
        H: (0d, 1d, 0d),
        PitchU: 0.7d,
        PitchV: 1.3d);

    public static int Run(string reportPath)
    {
        var fullReportPath = Path.GetFullPath(reportPath);
        var directory = Path.GetDirectoryName(fullReportPath) ?? Environment.CurrentDirectory;
        Directory.CreateDirectory(directory);
        var fixturePath = Path.Combine(directory, "crop-axis-grid-fixture.c3d");
        var source = CreateSource();
        source.SaveC3D(fixturePath);
        var persistenceBoundary = VerifyBinaryBoundary(source, directory);

        var cases = new[]
        {
            Check("crop-origin-display-inspection-four-corners-interior", () => VerifyCropMapping(source)),
            Check("one-cell-roi-inclusive", () => VerifyOneCellRoi(source)),
            Check("crop-outside-fails-closed", () => VerifyCropOutside(source)),
            Check("stale-source-binding-fails-and-recovery", () => VerifyStaleBinding(source)),
            Check("repeated-sample-list-is-deterministic", () => VerifyDeterminism(source))
        };
        var passed = cases.Count(item => item.Passed);
        var status = passed == cases.Length ? "PASS" : "FAIL";
        File.WriteAllLines(fullReportPath,
        [
            $"C3DCropAxisGridRoundTripVerification|{status}|cases={cases.Length}|passed={passed}|failed={cases.Length - passed}",
            "Contract|HeightImage=pixelX-column,pixelY-row,no-flip|ROI=GridRectangle,row-column,half-open-loop-with-inclusive-last-cell|cropOrigin=local+GridOrigin|axis=U:+X,V:-Z,H:+Y|pitchU=0.7|pitchV=1.3|physicalCalibration=excluded",
            $"Fixture|path={fixturePath}|width={source.Width}|height={source.Height}|sha256={source.ContentSha256}|uniqueCellEncoding=value=1000+row*100+column",
            .. cases.Select(item => $"Case|{item.Name}|{(item.Passed ? "PASS" : "FAIL")}|{Clean(item.Evidence)}"),
            $"PersistenceBoundary|{Clean(persistenceBoundary)}"
        ]);
        Console.WriteLine($"C3D crop/axis/grid round-trip verification: {status} ({passed}/{cases.Length})");
        return passed == cases.Length ? 0 : 5;
    }

    private static (bool Passed, string Evidence) VerifyCropMapping(C3DHeightFieldSnapshot source)
    {
        var rectangle = new ToolRecipeGridRectangle(2, 1, 3, 4);
        var evaluation = Crop(source, rectangle, "derived.crop-axis-grid");
        if (evaluation.Output is not { } output)
        {
            return (false, $"status={evaluation.Result.Status};message={evaluation.Result.Message}");
        }

        var sourceFrame = C3DHeightImageFrame.Create(source);
        var outputFrame = C3DHeightImageFrame.Create(output);
        var rasterization = ToolRecipeGridRectangleRasterizer.Rasterize(rectangle, source.Width, source.Height);
        var inspection = rasterization.Cells
            .Select(cell => Read(sourceFrame, cell.Row, cell.Column))
            .ToArray();
        var display = rasterization.Cells
            .Select(cell =>
            {
                var localRow = cell.Row - output.GridOriginRow;
                var localColumn = cell.Column - output.GridOriginColumn;
                return Read(outputFrame, localRow, localColumn) with
                {
                    Row = localRow + output.GridOriginRow,
                    Column = localColumn + output.GridOriginColumn
                };
            })
            .ToArray();
        var picks = new[]
        {
            (Row: 0, Column: 0),
            (Row: 0, Column: output.Width - 1),
            (Row: output.Height - 1, Column: 0),
            (Row: output.Height - 1, Column: output.Width - 1),
            (Row: 1, Column: 2)
        };
        var pickRoundTrips = picks.Select(pick =>
        {
            var displayX = output.Width - 1 - pick.Column;
            var displayY = output.Height - 1 - pick.Row;
            var localColumn = output.Width - 1 - displayX;
            var localRow = output.Height - 1 - displayY;
            var cell = Read(outputFrame, localRow, localColumn);
            var globalRow = localRow + output.GridOriginRow;
            var globalColumn = localColumn + output.GridOriginColumn;
            var world = Axis.ToWorld(globalRow, globalColumn, cell.RawHeight);
            var recovered = Axis.Locate(world);
            return (globalRow, globalColumn, cell.RawHeight, recovered);
        }).ToArray();
        var expectedPicks = picks.Select(pick =>
        {
            var globalRow = pick.Row + output.GridOriginRow;
            var globalColumn = pick.Column + output.GridOriginColumn;
            return (globalRow, globalColumn, source.Values.Span[globalRow * source.Width + globalColumn]);
        }).ToArray();
        var picksMatch = pickRoundTrips.Zip(expectedPicks).All(pair =>
            pair.First.globalRow == pair.Second.globalRow
            && pair.First.globalColumn == pair.Second.globalColumn
            && pair.First.RawHeight == pair.Second.Item3
            && pair.First.recovered == (pair.Second.globalRow, pair.Second.globalColumn));
        var pass = evaluation.Result.Status == ResultStatus.Pass
            && output.GridOriginRow == rectangle.Row
            && output.GridOriginColumn == rectangle.Column
            && rasterization.IsValid
            && inspection.SequenceEqual(display)
            && inspection.Length == rectangle.RowCount * rectangle.ColumnCount
            && picksMatch;
        return (pass,
            $"status={evaluation.Result.Status};origin={output.GridOriginColumn},{output.GridOriginRow};cells={inspection.Length};inspection={FormatSamples(inspection)};display={FormatSamples(display)};reversedAxisPicks={FormatPicks(pickRoundTrips)}");
    }

    private static (bool Passed, string Evidence) VerifyOneCellRoi(C3DHeightFieldSnapshot source)
    {
        var rectangle = new ToolRecipeGridRectangle(4, 5, 1, 1);
        var evaluation = Crop(source, rectangle, "derived.crop-axis-grid.one-cell");
        var output = evaluation.Output;
        var expected = source.Values.Span[rectangle.Row * source.Width + rectangle.Column];
        C3DHeightImageCell? frameCell = output is null || !C3DHeightImageFrame.Create(output).TryGetCell(0, 0, out var cell)
            ? null
            : cell;
        var pass = evaluation.Result.Status == ResultStatus.Pass
            && output is { Width: 1, Height: 1, GridOriginColumn: 5, GridOriginRow: 4 }
            && frameCell is { Row: 0, Column: 0, RawHeight: var value } && value == expected;
        return (pass,
            $"status={evaluation.Result.Status};output={output?.Width}x{output?.Height};origin={output?.GridOriginColumn},{output?.GridOriginRow};expected={expected};actual={frameCell?.RawHeight}");
    }

    private static (bool Passed, string Evidence) VerifyCropOutside(C3DHeightFieldSnapshot source)
    {
        var rectangle = new ToolRecipeGridRectangle(5, 6, 2, 2);
        var evaluation = Crop(source, rectangle, "derived.crop-axis-grid.outside");
        var pass = evaluation.Result.Status == ResultStatus.Error && evaluation.Output is null;
        return (pass, $"status={evaluation.Result.Status};message={evaluation.Result.Message};output={evaluation.Output?.EntityId ?? "none"}");
    }

    private static (bool Passed, string Evidence) VerifyStaleBinding(C3DHeightFieldSnapshot source)
    {
        var staleSource = C3DHeightFieldSnapshot.CreateForVerification(
            "source.crop-axis-grid.replaced",
            SourceWidth,
            SourceHeight,
            Enumerable.Range(1, SourceWidth * SourceHeight)
                .Select(value => 2000d + value)
                .ToArray());
        var rectangle = new ToolRecipeGridRectangle(1, 2, 2, 3);
        var staleSelection = CreateSelection(source, rectangle);
        var stale = C3DRoiCropRule.Evaluate(new C3DRoiCropInput(
            "step.crop-axis-grid.stale",
            staleSource,
            staleSelection,
            "derived.crop-axis-grid.stale"));
        var recovered = Crop(staleSource, rectangle, "derived.crop-axis-grid.recovered");
        var pass = stale.Result.Status == ResultStatus.Error
            && stale.Output is null
            && recovered.Result.Status == ResultStatus.Pass
            && recovered.Output is { GridOriginColumn: 2, GridOriginRow: 1 };
        return (pass,
            $"stale={stale.Result.Status}:{stale.Result.Message};recovered={recovered.Result.Status};recoveredOrigin={recovered.Output?.GridOriginColumn},{recovered.Output?.GridOriginRow};oldHash={source.ContentSha256};newHash={staleSource.ContentSha256}");
    }

    private static (bool Passed, string Evidence) VerifyDeterminism(C3DHeightFieldSnapshot source)
    {
        var rectangle = new ToolRecipeGridRectangle(2, 1, 3, 4);
        var first = Crop(source, rectangle, "derived.crop-axis-grid.repeat-a");
        var second = Crop(source, rectangle, "derived.crop-axis-grid.repeat-b");
        var firstSamples = first.Output is { } firstOutput
            ? EnumerateSamples(firstOutput)
            : [];
        var secondSamples = second.Output is { } secondOutput
            ? EnumerateSamples(secondOutput)
            : [];
        var pass = first.Result.Status == ResultStatus.Pass
            && second.Result.Status == ResultStatus.Pass
            && first.Output?.ContentSha256 == second.Output?.ContentSha256
            && firstSamples.SequenceEqual(secondSamples);
        return (pass,
            $"first={first.Output?.ContentSha256};second={second.Output?.ContentSha256};samples={FormatSamples(firstSamples)}");
    }

    private static string VerifyBinaryBoundary(C3DHeightFieldSnapshot source, string directory)
    {
        var output = Crop(source, new ToolRecipeGridRectangle(2, 1, 3, 4), "derived.crop-axis-grid.binary-boundary").Output
            ?? throw new InvalidDataException("The binary-boundary crop did not produce an output.");
        var path = Path.Combine(directory, "crop-axis-grid-binary-boundary.c3d");
        output.SaveC3D(path);
        var reopened = C3DHeightFieldSnapshot.LoadIdentified(
            path,
            "source.crop-axis-grid.reopened",
            source.Unit,
            source.FrameId);
        var expectedByteIdentity = reopened.ContentSha256 == output.ContentSha256;
        var metadataNotInferred = reopened.GridOriginColumn == 0
            && reopened.GridOriginRow == 0
            && reopened.RootSourceSha256 == reopened.ContentSha256;
        return $"status={(expectedByteIdentity && metadataNotInferred ? "PASS" : "FAIL")};savedOrigin={output.GridOriginColumn},{output.GridOriginRow};reopenedOrigin={reopened.GridOriginColumn},{reopened.GridOriginRow};savedRoot={output.RootSourceSha256};reopenedRoot={reopened.RootSourceSha256};reopenedSha256={reopened.ContentSha256};scope=3D-025-recipe-metadata";
    }

    private static C3DRoiCropEvaluation Crop(
        C3DHeightFieldSnapshot source,
        ToolRecipeGridRectangle rectangle,
        string outputEntityId) =>
        C3DRoiCropRule.Evaluate(new C3DRoiCropInput(
            "step.crop-axis-grid",
            source,
            CreateSelection(source, rectangle),
            outputEntityId));

    private static ToolRecipeSelection CreateSelection(
        C3DHeightFieldSnapshot source,
        ToolRecipeGridRectangle rectangle) =>
        new(
            "selection.crop-axis-grid",
            "Crop axis/grid round trip",
            ToolRecipeSelectionKinds.GridRectangle,
            source.EntityId,
            source.FrameId,
            new ToolRecipeSelectionSourceBinding("C3D", source.ContentSha256, source.Width, source.Height),
            rectangle,
            null,
            null);

    private static C3DHeightFieldSnapshot CreateSource() =>
        C3DHeightFieldSnapshot.CreateForVerification(
            "source.crop-axis-grid.fixture",
            SourceWidth,
            SourceHeight,
            Enumerable.Range(0, SourceWidth * SourceHeight)
                .Select(index => 1000d + (index / SourceWidth) * 100d + index % SourceWidth)
                .ToArray());

    private static Sample Read(C3DHeightImageFrame frame, int row, int column)
    {
        if (!frame.TryGetCell(column, row, out var cell))
        {
            throw new InvalidDataException($"Height Image cell ({row},{column}) was outside {frame.Width}x{frame.Height}.");
        }

        return new Sample(cell.Row, cell.Column, cell.RawHeight);
    }

    private static IReadOnlyList<Sample> EnumerateSamples(C3DHeightFieldSnapshot snapshot) =>
        Enumerable.Range(0, snapshot.Height)
            .SelectMany(row => Enumerable.Range(0, snapshot.Width).Select(column =>
                new Sample(
                    row + snapshot.GridOriginRow,
                    column + snapshot.GridOriginColumn,
                    snapshot.Values.Span[row * snapshot.Width + column])))
            .ToArray();

    private static string FormatSamples(IEnumerable<Sample> samples) =>
        string.Join(';', samples.Select(sample => $"{sample.Row},{sample.Column}={sample.RawHeight:G17}"));

    private static string FormatPicks(IEnumerable<(int globalRow, int globalColumn, double RawHeight, (int Row, int Column) recovered)> picks) =>
        string.Join(';', picks.Select(pick => $"{pick.globalRow},{pick.globalColumn}={pick.RawHeight:G17}->({pick.recovered.Row},{pick.recovered.Column})"));

    private static string Clean(string value) =>
        value.Replace('|', '/').Replace('\r', ' ').Replace('\n', ' ');

    private static (string Name, bool Passed, string Evidence) Check(
        string name,
        Func<(bool Passed, string Evidence)> verify)
    {
        try
        {
            var result = verify();
            return (name, result.Passed, result.Evidence);
        }
        catch (Exception exception)
        {
            return (name, false, $"unexpected {exception.GetType().Name}: {exception.Message}");
        }
    }

    private sealed record Sample(int Row, int Column, double RawHeight);

    private sealed record GridAxisFixture(
        (double X, double Y, double Z) Origin,
        (double X, double Y, double Z) U,
        (double X, double Y, double Z) V,
        (double X, double Y, double Z) H,
        double PitchU,
        double PitchV)
    {
        public (double X, double Y, double Z) ToWorld(int row, int column, double rawHeight)
        {
            var u = (column + 0.5d) * PitchU;
            var v = (row + 0.5d) * PitchV;
            return (
                Origin.X + U.X * u + V.X * v + H.X * rawHeight,
                Origin.Y + U.Y * u + V.Y * v + H.Y * rawHeight,
                Origin.Z + U.Z * u + V.Z * v + H.Z * rawHeight);
        }

        public (int Row, int Column) Locate((double X, double Y, double Z) world)
        {
            var u = Dot((world.X - Origin.X, world.Y - Origin.Y, world.Z - Origin.Z), U) / PitchU;
            var v = Dot((world.X - Origin.X, world.Y - Origin.Y, world.Z - Origin.Z), V) / PitchV;
            return ((int)Math.Floor(v), (int)Math.Floor(u));
        }

        private static double Dot(
            (double X, double Y, double Z) left,
            (double X, double Y, double Z) right) =>
            left.X * right.X + left.Y * right.Y + left.Z * right.Z;
    }
}
