using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using OpenVisionLab.ThreeD.Data;

internal static class C3DMapFidelityVerification
{
    public static int RunProbe(string sourcePath, string plyPath, string reportPath, int maxSampledPoints, bool includeFaces)
    {
        try
        {
            if (maxSampledPoints <= 0)
            {
                throw new InvalidDataException("C3D map probe max sampled points must be positive.");
            }

            var fullSourcePath = Path.GetFullPath(sourcePath);
            var grid = C3DHeightGrid.Load(fullSourcePath, maxSampledPoints);
            var export = C3DPointMapPly.Export(grid, plyPath, includeFaces);
            var roundtrip = ReadPly(export.Path);
            var comparison = Compare(grid.Points, roundtrip.Points);
            var passed = roundtrip.DeclaredPointCount == grid.Points.Length
                && roundtrip.DeclaredFaceCount == export.FaceCount
                && comparison.MaxCoordinateError <= 1e-6
                && comparison.MaxColorChannelError == 0;
            var status = passed ? "Pass" : "Fail";
            var lines = new List<string>
            {
                $"C3DMapFidelity|{status}|displayFrame={(passed ? "Verified" : "Mismatch")}|physicalScale=Unverified|reason=calibration metadata and official C3D specification unavailable",
                $"Source|path={fullSourcePath}|bytes={new FileInfo(fullSourcePath).Length}|sha256={HashFile(fullSourcePath)}|layout=inferred-int32-width,int32-height,float32-grid",
                $"Grid|width={grid.Width}|height={grid.Height}|valid={grid.ValidSampleCount}|zero={grid.ZeroSampleCount}|minRaw={Format(grid.Min)}|maxRaw={Format(grid.Max)}|meanRaw={Format(grid.Mean)}",
                $"ViewerMapping|frame=right-handed-y-up|x=column|y=raw-height|z=row|horizontalSpan={Format(C3DHeightGrid.ViewerHorizontalSpan)}|horizontalScale={Format(grid.HorizontalScale)}|heightScale={Format(C3DHeightGrid.ViewerHeightScale)}|heightCenterRaw={Format(grid.Mean)}|modelUnit=unitless|rawUnit=raw-height",
                $"Sampling|budget={maxSampledPoints}|stride={grid.PointStride}|renderedPoints={grid.Points.Length}",
                $"ViewerBounds|min={FormatVector(export.Minimum)}|max={FormatVector(export.Maximum)}",
                $"PLY|path={export.Path}|format=ascii-1.0|points={export.PointCount}|faces={export.FaceCount}|bytes={export.ByteLength}|sha256={HashFile(export.Path)}",
                includeFaces
                    ? "ReferenceGeometry|mode=compatibility-mesh|vertices=exact-render-sample|faces=visualization-only|measurementUse=false"
                    : "ReferenceGeometry|mode=point-only|vertices=exact-render-sample|faces=none|measurementUse=vertices-only",
                $"Roundtrip|declaredPoints={roundtrip.DeclaredPointCount}|readPoints={roundtrip.Points.Length}|pointCountMatch={roundtrip.DeclaredPointCount == grid.Points.Length}|declaredFaces={roundtrip.DeclaredFaceCount}|readFaces={roundtrip.Faces.Length}|faceCountMatch={roundtrip.DeclaredFaceCount == export.FaceCount}|maxCoordinateError={comparison.MaxCoordinateError.ToString("G9", CultureInfo.InvariantCulture)}|maxColorChannelError={comparison.MaxColorChannelError}"
            };
            AddRepresentativePoints(lines, grid.Points);

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
            File.WriteAllLines(reportPath, lines);
            Console.WriteLine($"C3D map display-frame fidelity: {status} ({grid.Points.Length:N0} points, {export.FaceCount:N0} faces, max error {comparison.MaxCoordinateError:G3})");
            return passed ? 0 : 5;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or OverflowException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    public static int RunGolden(string reportPath)
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"OpenVisionLab-C3DMap-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);
        try
        {
            var sourcePath = Path.Combine(tempDirectory, "known-grid.c3d");
            var plyPath = Path.Combine(tempDirectory, "known-grid.ply");
            WriteC3D(sourcePath, 3, 2, [10.0f, 20.0f, 30.0f, 0.0f, 40.0f, 50.0f]);
            var grid = C3DHeightGrid.Load(sourcePath, int.MaxValue);
            C3DPointMapPly.Export(grid, plyPath);
            var roundtrip = ReadPly(plyPath);
            var cases = new[]
            {
                Check("known-grid-statistics", () => VerifyStatistics(grid)),
                Check("known-grid-coordinate-mapping", () => VerifyCoordinates(grid)),
                Check("direct-cell-reference", () => VerifyDirectCell(grid)),
                Check("height-scalar", () => VerifyHeightScalars(grid)),
                Check("deviation-scalar", () => VerifyDeviationScalars(grid)),
                Check("render-stride-contract", () => VerifyStride(sourcePath)),
                Check("ply-coordinate-roundtrip", () => VerifyPlyRoundtrip(grid, roundtrip)),
                Check("ply-height-colors", () => VerifyPlyColors(roundtrip)),
                Check("single-cell-grid", () => VerifySingleCellGrid(tempDirectory)),
                Check("all-zero-source-error", () => VerifyAllZeroSource(tempDirectory))
            };

            var passedCount = cases.Count(item => item.Passed);
            var status = passedCount == cases.Length ? "Pass" : "Fail";
            var lines = new List<string>
            {
                $"C3DMapFidelityGoldenVerification|{status}|cases={cases.Length}|passed={passedCount}|failed={cases.Length - passedCount}",
                $"KnownGrid|width=3|height=2|values=10,20,30,0,40,50|mean=30|horizontalScale=5|heightScale={Format(C3DHeightGrid.ViewerHeightScale)}"
            };
            lines.AddRange(cases.Select(item => $"Case|{item.Name}|{(item.Passed ? "Pass" : "Fail")}|{Clean(item.Evidence)}"));
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
            File.WriteAllLines(reportPath, lines);
            Console.WriteLine($"C3D map fidelity golden verification: {status} ({passedCount}/{cases.Length})");
            return passedCount == cases.Length ? 0 : 5;
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private static (bool Passed, string Evidence) VerifyStatistics(C3DHeightGrid grid)
    {
        var passed = grid.Width == 3
            && grid.Height == 2
            && grid.ValidSampleCount == 5
            && grid.ZeroSampleCount == 1
            && Approximately(grid.Min, 10.0)
            && Approximately(grid.Max, 50.0)
            && Approximately(grid.Mean, 30.0)
            && grid.PointStride == 1;
        return (passed, $"size={grid.Width}x{grid.Height},valid={grid.ValidSampleCount},zero={grid.ZeroSampleCount},min={Format(grid.Min)},max={Format(grid.Max)},mean={Format(grid.Mean)},stride={grid.PointStride}");
    }

    private static (bool Passed, string Evidence) VerifyCoordinates(C3DHeightGrid grid)
    {
        var expected = new Dictionary<(int Row, int Column), Vector3>
        {
            [(0, 0)] = new(-5.0f, -0.012f, -2.5f),
            [(0, 1)] = new(0.0f, -0.006f, -2.5f),
            [(0, 2)] = new(5.0f, 0.0f, -2.5f),
            [(1, 1)] = new(0.0f, 0.006f, 2.5f),
            [(1, 2)] = new(5.0f, 0.012f, 2.5f)
        };
        var maximumError = grid.Points.Max(point => ComponentError(point.Position, expected[(point.Row, point.Column)]));
        return (grid.Points.Length == expected.Count && maximumError <= 1e-6, $"points={grid.Points.Length},maxError={maximumError:G9}");
    }

    private static (bool Passed, string Evidence) VerifyDirectCell(C3DHeightGrid grid)
    {
        var direct = grid.ReadPoint(1, 2);
        var sampled = grid.Points.Single(point => point.Row == 1 && point.Column == 2);
        var error = ComponentError(direct.Position, sampled.Position);
        return (Approximately(direct.RawValue, 50.0) && error <= 1e-9, $"raw={Format(direct.RawValue)},position={FormatVector(direct.Position)},error={error:G9}");
    }

    private static (bool Passed, string Evidence) VerifyHeightScalars(C3DHeightGrid grid)
    {
        var expected = new Dictionary<float, double> { [10.0f] = 0.0, [20.0f] = 0.25, [30.0f] = 0.5, [40.0f] = 0.75, [50.0f] = 1.0 };
        var maximumError = grid.Points.Max(point => Math.Abs(point.HeightScalar - expected[point.RawValue]));
        return (maximumError <= 1e-9, $"maxError={maximumError:G9}");
    }

    private static (bool Passed, string Evidence) VerifyDeviationScalars(C3DHeightGrid grid)
    {
        var expected = new Dictionary<float, double> { [10.0f] = 1.0, [20.0f] = 0.5, [30.0f] = 0.0, [40.0f] = 0.5, [50.0f] = 1.0 };
        var maximumError = grid.Points.Max(point => Math.Abs(point.DeviationScalar - expected[point.RawValue]));
        return (maximumError <= 1e-9, $"maxError={maximumError:G9}");
    }

    private static (bool Passed, string Evidence) VerifyStride(string sourcePath)
    {
        var grid = C3DHeightGrid.Load(sourcePath, maxRenderedPoints: 2);
        var passed = grid.PointStride == 2
            && grid.Points.Length == 2
            && grid.Points.All(point => point.Row % 2 == 0 && point.Column % 2 == 0);
        return (passed, $"stride={grid.PointStride},points={grid.Points.Length},cells={string.Join(';', grid.Points.Select(point => $"{point.Row},{point.Column}"))}");
    }

    private static (bool Passed, string Evidence) VerifyPlyRoundtrip(C3DHeightGrid grid, PlyPointCloud roundtrip)
    {
        var comparison = Compare(grid.Points, roundtrip.Points);
        var passed = roundtrip.DeclaredPointCount == grid.Points.Length
            && roundtrip.DeclaredFaceCount == 2
            && roundtrip.Faces.Length == 2
            && comparison.MaxCoordinateError <= 1e-6
            && comparison.MaxColorChannelError == 0;
        return (passed, $"declaredPoints={roundtrip.DeclaredPointCount},readPoints={roundtrip.Points.Length},declaredFaces={roundtrip.DeclaredFaceCount},readFaces={roundtrip.Faces.Length},coordinateError={comparison.MaxCoordinateError:G9},colorError={comparison.MaxColorChannelError}");
    }

    private static (bool Passed, string Evidence) VerifyPlyColors(PlyPointCloud roundtrip)
    {
        var first = roundtrip.Points[0];
        var last = roundtrip.Points[^1];
        var passed = (first.Red, first.Green, first.Blue) == (12, 89, 242)
            && (last.Red, last.Green, last.Blue) == (255, 178, 25);
        return (passed, $"first={first.Red},{first.Green},{first.Blue},last={last.Red},{last.Green},{last.Blue}");
    }

    private static (bool Passed, string Evidence) VerifyAllZeroSource(string tempDirectory)
    {
        var path = Path.Combine(tempDirectory, "all-zero.c3d");
        WriteC3D(path, 2, 2, [0.0f, 0.0f, 0.0f, 0.0f]);
        try
        {
            C3DHeightGrid.Load(path);
            return (false, "load unexpectedly succeeded");
        }
        catch (InvalidDataException exception)
        {
            return (exception.Message.Contains("no non-zero finite samples", StringComparison.OrdinalIgnoreCase), exception.Message);
        }
    }

    private static (bool Passed, string Evidence) VerifySingleCellGrid(string tempDirectory)
    {
        var path = Path.Combine(tempDirectory, "single-cell.c3d");
        WriteC3D(path, 1, 1, [42.0f]);
        var grid = C3DHeightGrid.Load(path, int.MaxValue);
        var point = grid.Points.Single();
        var passed = float.IsFinite(grid.HorizontalScale)
            && point.Position == Vector3.Zero
            && grid.XHalfExtent == 0.0f
            && grid.ZHalfExtent == 0.0f;
        return (passed, $"horizontalScale={Format(grid.HorizontalScale)},position={FormatVector(point.Position)},xHalfExtent={Format(grid.XHalfExtent)},zHalfExtent={Format(grid.ZHalfExtent)}");
    }

    private static VerificationCase Check(string name, Func<(bool Passed, string Evidence)> verify)
    {
        try
        {
            var result = verify();
            return new VerificationCase(name, result.Passed, result.Evidence);
        }
        catch (Exception exception)
        {
            return new VerificationCase(name, false, $"unexpected {exception.GetType().Name}: {exception.Message}");
        }
    }

    private static PlyPointCloud ReadPly(string path)
    {
        using var reader = new StreamReader(path);
        if (!string.Equals(reader.ReadLine(), "ply", StringComparison.Ordinal)
            || !string.Equals(reader.ReadLine(), "format ascii 1.0", StringComparison.Ordinal))
        {
            throw new InvalidDataException("PLY reference must use ASCII PLY 1.0.");
        }

        var declaredPointCount = -1;
        var declaredFaceCount = -1;
        string? line;
        while ((line = reader.ReadLine()) is not null && !line.Equals("end_header", StringComparison.Ordinal))
        {
            if (line.StartsWith("element vertex ", StringComparison.Ordinal))
            {
                declaredPointCount = int.Parse(line["element vertex ".Length..], CultureInfo.InvariantCulture);
            }
            else if (line.StartsWith("element face ", StringComparison.Ordinal))
            {
                declaredFaceCount = int.Parse(line["element face ".Length..], CultureInfo.InvariantCulture);
            }
        }

        if (line is null || declaredPointCount < 0 || declaredFaceCount < 0)
        {
            throw new InvalidDataException("PLY reference header is incomplete.");
        }

        var points = new PlyPoint[declaredPointCount];
        for (var index = 0; index < points.Length; index++)
        {
            line = reader.ReadLine() ?? throw new InvalidDataException($"PLY reference ended at vertex {index}.");
            var fields = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length != 6)
            {
                throw new InvalidDataException($"PLY reference vertex {index} must have XYZ and RGB fields.");
            }

            points[index] = new PlyPoint(
                new Vector3(
                    float.Parse(fields[0], CultureInfo.InvariantCulture),
                    float.Parse(fields[1], CultureInfo.InvariantCulture),
                    float.Parse(fields[2], CultureInfo.InvariantCulture)),
                byte.Parse(fields[3], CultureInfo.InvariantCulture),
                byte.Parse(fields[4], CultureInfo.InvariantCulture),
                byte.Parse(fields[5], CultureInfo.InvariantCulture));
        }

        var faces = new PlyFace[declaredFaceCount];
        for (var index = 0; index < faces.Length; index++)
        {
            line = reader.ReadLine() ?? throw new InvalidDataException($"PLY reference ended at face {index}.");
            var fields = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length != 4 || fields[0] != "3")
            {
                throw new InvalidDataException($"PLY reference face {index} must be a triangle.");
            }

            var face = new PlyFace(
                int.Parse(fields[1], CultureInfo.InvariantCulture),
                int.Parse(fields[2], CultureInfo.InvariantCulture),
                int.Parse(fields[3], CultureInfo.InvariantCulture));
            if (face.A < 0 || face.A >= points.Length
                || face.B < 0 || face.B >= points.Length
                || face.C < 0 || face.C >= points.Length)
            {
                throw new InvalidDataException($"PLY reference face {index} has an out-of-range vertex index.");
            }

            faces[index] = face;
        }

        return new PlyPointCloud(declaredPointCount, declaredFaceCount, points, faces);
    }

    private static (double MaxCoordinateError, int MaxColorChannelError) Compare(
        IReadOnlyList<HeightGridPoint> expected,
        IReadOnlyList<PlyPoint> actual)
    {
        if (expected.Count != actual.Count)
        {
            return (double.PositiveInfinity, int.MaxValue);
        }

        var maxCoordinateError = 0.0;
        var maxColorChannelError = 0;
        for (var index = 0; index < expected.Count; index++)
        {
            maxCoordinateError = Math.Max(maxCoordinateError, ComponentError(expected[index].Position, actual[index].Position));
            var expectedColor = C3DPointMapPalette.HeightBytes(expected[index].HeightScalar);
            maxColorChannelError = Math.Max(maxColorChannelError, Math.Abs(expectedColor.R - actual[index].Red));
            maxColorChannelError = Math.Max(maxColorChannelError, Math.Abs(expectedColor.G - actual[index].Green));
            maxColorChannelError = Math.Max(maxColorChannelError, Math.Abs(expectedColor.B - actual[index].Blue));
        }

        return (maxCoordinateError, maxColorChannelError);
    }

    private static void AddRepresentativePoints(List<string> lines, IReadOnlyList<HeightGridPoint> points)
    {
        foreach (var index in new[] { 0, points.Count / 2, points.Count - 1 }.Distinct())
        {
            var point = points[index];
            var color = C3DPointMapPalette.HeightBytes(point.HeightScalar);
            lines.Add($"Point|index={index}|cell=({point.Row},{point.Column})|position={FormatVector(point.Position)}|raw={Format(point.RawValue)}|heightScalar={Format(point.HeightScalar)}|rgb={color.R},{color.G},{color.B}");
        }
    }

    private static void WriteC3D(string path, int width, int height, IReadOnlyList<float> values)
    {
        using var writer = new BinaryWriter(File.Create(path));
        writer.Write(width);
        writer.Write(height);
        foreach (var value in values)
        {
            writer.Write(value);
        }
    }

    private static string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static double ComponentError(Vector3 first, Vector3 second) =>
        Math.Max(Math.Abs(first.X - second.X), Math.Max(Math.Abs(first.Y - second.Y), Math.Abs(first.Z - second.Z)));

    private static bool Approximately(double actual, double expected, double tolerance = 1e-6) =>
        double.IsFinite(actual) && Math.Abs(actual - expected) <= tolerance;

    private static string Format(double value) =>
        value.ToString("F6", CultureInfo.InvariantCulture);

    private static string FormatVector(Vector3 value) =>
        string.Create(CultureInfo.InvariantCulture, $"({value.X:F6},{value.Y:F6},{value.Z:F6})");

    private static string Clean(string value) =>
        value.Replace('|', '/').Replace('\r', ' ').Replace('\n', ' ');

    private sealed record VerificationCase(string Name, bool Passed, string Evidence);

    private sealed record PlyPointCloud(int DeclaredPointCount, int DeclaredFaceCount, PlyPoint[] Points, PlyFace[] Faces);

    private sealed record PlyPoint(Vector3 Position, byte Red, byte Green, byte Blue);

    private sealed record PlyFace(int A, int B, int C);
}
