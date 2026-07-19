namespace OpenVisionLab.ThreeD.Data;

/// <summary>
/// Headless contract verification for inclusive C3D P1-P2 profile sampling.
/// </summary>
public static class C3DHeightProfileVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        var lines = new List<string>
        {
            "OpenVisionLab 3D C3D height-profile verification",
            $"Generated: {DateTimeOffset.Now:O}"
        };
        var passed = 0;
        var total = 0;
        var fixtureRoot = Path.Combine(
            Path.GetTempPath(),
            "OpenVisionLab.ThreeD",
            "C3DHeightProfileVerification",
            Guid.NewGuid().ToString("N"));

        void Check(string name, bool condition, string detail)
        {
            total++;
            lines.Add($"{(condition ? "PASS" : "FAIL")} | {name} | {detail}");
            if (condition) passed++;
        }

        try
        {
            Directory.CreateDirectory(fixtureRoot);
            var sourcePath = Path.Combine(fixtureRoot, "profile.C3D");
            WriteC3D(sourcePath, 5, 5, (row, column) => 1.0f + row * 10 + column);
            var grid = C3DHeightGrid.Load(sourcePath, maxRenderedPoints: 0);

            var horizontal = grid.ReadLineProfile(2, 0, 2, 4);
            Check(
                "horizontal profile is inclusive and ordered",
                CoordinatesEqual(horizontal, [(2, 0), (2, 1), (2, 2), (2, 3), (2, 4)]),
                Describe(horizontal));

            var vertical = grid.ReadLineProfile(0, 3, 4, 3);
            Check(
                "vertical profile is inclusive and ordered",
                CoordinatesEqual(vertical, [(0, 3), (1, 3), (2, 3), (3, 3), (4, 3)]),
                Describe(vertical));

            var diagonal = grid.ReadLineProfile(0, 0, 4, 4);
            Check(
                "diagonal profile is inclusive and ordered",
                CoordinatesEqual(diagonal, [(0, 0), (1, 1), (2, 2), (3, 3), (4, 4)]),
                Describe(diagonal));

            var forward = grid.ReadLineProfile(0, 0, 2, 4);
            var reverse = grid.ReadLineProfile(2, 4, 0, 0);
            Check(
                "reverse sampling preserves endpoints and exact cell symmetry",
                CoordinatesEqual(forward, [(0, 0), (1, 1), (1, 2), (2, 3), (2, 4)])
                && Coordinates(forward).SequenceEqual(Coordinates(reverse).Reverse()),
                $"forward={Describe(forward)}; reverse={Describe(reverse)}");

            var single = grid.ReadLineProfile(3, 2, 3, 2);
            Check(
                "identical P1 and P2 returns one endpoint",
                CoordinatesEqual(single, [(3, 2)]),
                Describe(single));

            var missingPath = Path.Combine(fixtureRoot, "profile-missing.C3D");
            WriteC3D(
                missingPath,
                5,
                5,
                (row, column) => (row, column) switch
                {
                    (1, 1) => 0.0f,
                    (3, 3) => float.NaN,
                    _ => 1.0f + row * 10 + column
                });
            var missingGrid = C3DHeightGrid.Load(missingPath, maxRenderedPoints: 0);
            var missingProfile = missingGrid.ReadLineProfile(0, 0, 4, 4);
            Check(
                "zero and non-finite cells are skipped without reordering",
                CoordinatesEqual(missingProfile, [(0, 0), (2, 2), (4, 4)]),
                Describe(missingProfile));

            Check(
                "start row below bounds is rejected",
                Throws<ArgumentOutOfRangeException>(() => grid.ReadLineProfile(-1, 0, 2, 2)),
                "startRow=-1");
            Check(
                "end column beyond bounds is rejected",
                Throws<ArgumentOutOfRangeException>(() => grid.ReadLineProfile(0, 0, 2, 5)),
                "endColumn=5");

            var mutatedPath = Path.Combine(fixtureRoot, "profile-mutated.C3D");
            WriteC3D(mutatedPath, 5, 5, (row, column) => 1.0f + row * 10 + column);
            var mutatedGrid = C3DHeightGrid.Load(mutatedPath, maxRenderedPoints: 0);
            WriteC3D(mutatedPath, 5, 5, (row, column) => 100.0f + row * 10 + column);
            Check(
                "same-size source content mutation is rejected by SHA-256",
                Throws<InvalidDataException>(() => mutatedGrid.ReadLineProfile(0, 0, 4, 4)),
                "dimensions and byte length unchanged; payload changed");

            var dimensionPath = Path.Combine(fixtureRoot, "profile-dimension.C3D");
            WriteC3D(dimensionPath, 5, 5, (row, column) => 1.0f + row * 10 + column);
            var dimensionGrid = C3DHeightGrid.Load(dimensionPath, maxRenderedPoints: 0);
            using (var writer = new BinaryWriter(File.Open(dimensionPath, FileMode.Open, FileAccess.Write, FileShare.None)))
            {
                writer.Write(4);
            }

            Check(
                "source dimension mutation is rejected before sampling",
                Throws<InvalidDataException>(() => dimensionGrid.ReadLineProfile(0, 0, 4, 4)),
                "loaded width=5; current header width=4");
        }
        catch (Exception exception)
        {
            lines.Add($"FAIL | unexpected exception | {exception.GetType().Name}: {exception.Message}");
        }
        finally
        {
            try
            {
                if (Directory.Exists(fixtureRoot)) Directory.Delete(fixtureRoot, recursive: true);
            }
            catch (IOException exception)
            {
                lines.Add($"FAIL | fixture cleanup | {exception.Message}");
            }
        }

        var reportDirectory = Path.GetDirectoryName(reportPath);
        if (!string.IsNullOrWhiteSpace(reportDirectory)) Directory.CreateDirectory(reportDirectory);
        var succeeded = passed == total
            && total > 0
            && !lines.Any(line => line.StartsWith("FAIL | unexpected exception", StringComparison.Ordinal));
        lines.Add($"Result: {(succeeded ? "Pass" : "Fail")} ({passed}/{total} checks)");
        File.WriteAllLines(reportPath, lines);
        summary = $"C3D height-profile verification: {(succeeded ? "Pass" : "Fail")} ({passed}/{total} checks)";
        return succeeded;
    }

    private static IReadOnlyList<(int Row, int Column)> Coordinates(IEnumerable<HeightGridPoint> points) =>
        points.Select(point => (point.Row, point.Column)).ToArray();

    private static bool CoordinatesEqual(
        IEnumerable<HeightGridPoint> actual,
        IReadOnlyList<(int Row, int Column)> expected) =>
        Coordinates(actual).SequenceEqual(expected);

    private static string Describe(IEnumerable<HeightGridPoint> points) =>
        string.Join(" -> ", points.Select(point => $"({point.Row},{point.Column})={point.RawValue:R}"));

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

    private static void WriteC3D(
        string path,
        int width,
        int height,
        Func<int, int, float> valueFactory)
    {
        using var writer = new BinaryWriter(File.Create(path));
        writer.Write(width);
        writer.Write(height);
        for (var row = 0; row < height; row++)
        {
            for (var column = 0; column < width; column++)
            {
                writer.Write(valueFactory(row, column));
            }
        }
    }
}
