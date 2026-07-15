using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

internal static class MeshDeviationGoldenVerification
{
    public static int Run(string reportPath)
    {
        var tempDirectory = Path.Combine(
            Path.GetTempPath(),
            $"OpenVisionLab.ThreeD.MeshDeviation.{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);

        VerificationCase[] cases;
        try
        {
            var validPath = Path.Combine(tempDirectory, "two-triangles.stl");
            var corruptLengthPath = Path.Combine(tempDirectory, "corrupt-length.stl");
            var nonFinitePath = Path.Combine(tempDirectory, "non-finite.stl");
            var measuredPlyPath = Path.Combine(tempDirectory, "measured.ply");
            var unsignedPlyPath = Path.Combine(tempDirectory, "unsigned.ply");
            var signedPlyPath = Path.Combine(tempDirectory, "signed.ply");
            var wrongSignedPlyPath = Path.Combine(tempDirectory, "wrong-signed.ply");
            var changedUnsignedPlyPath = Path.Combine(tempDirectory, "changed-unsigned.ply");
            var truncatedPlyPath = Path.Combine(tempDirectory, "truncated.ply");
            var parityPassReportPath = Path.Combine(tempDirectory, "parity-pass.txt");
            var parityMismatchReportPath = Path.Combine(tempDirectory, "parity-mismatch.txt");
            var paritySignMismatchReportPath = Path.Combine(tempDirectory, "parity-sign-mismatch.txt");
            var first = new BinaryStlTriangle(
                Vector3.UnitZ,
                new Vector3(0, 0, 0),
                new Vector3(2, 0, 0),
                new Vector3(0, 2, 0),
                7);
            var second = new BinaryStlTriangle(
                -Vector3.UnitZ,
                new Vector3(10, 0, 0),
                new Vector3(10, 2, 0),
                new Vector3(12, 0, 0),
                9);
            WriteBinaryStl(validPath, first, second);
            File.Copy(validPath, corruptLengthPath);
            using (var stream = File.OpenWrite(corruptLengthPath))
            {
                stream.SetLength(stream.Length + 1);
            }

            WriteBinaryStl(
                nonFinitePath,
                first with { A = new Vector3(float.NaN, 0, 0) });
            WriteBinaryPly(
                measuredPlyPath,
                ["x", "y", "z"],
                [
                    [0.25f, 0.25f, 1.0f],
                    [0.5f, 0.25f, -2.0f],
                    [0.25f, 0.5f, 0.0f],
                    [1.0f, -1.0f, 1.0f],
                    [0.75f, 0.5f, 0.0f]
                ]);
            WriteBinaryPly(
                unsignedPlyPath,
                ["x", "y", "z", "scalar_C2M_absolute_distances"],
                [
                    [0.25f, 0.25f, 1.0f, 1.0f],
                    [0.5f, 0.25f, -2.0f, 2.0f],
                    [0.25f, 0.5f, 0.0f, 0.0f],
                    [1.0f, -1.0f, 1.0f, (float)Math.Sqrt(2)],
                    [0.75f, 0.5f, 0.0f, 5e-8f]
                ]);
            WriteBinaryPly(
                signedPlyPath,
                ["x", "y", "z", "scalar_C2M_signed_distances"],
                [
                    [0.25f, 0.25f, 1.0f, 1.0f],
                    [0.5f, 0.25f, -2.0f, -2.0f],
                    [0.25f, 0.5f, 0.0f, 0.0f],
                    [1.0f, -1.0f, 1.0f, (float)Math.Sqrt(2)],
                    [0.75f, 0.5f, 0.0f, -5e-8f]
                ]);
            WriteBinaryPly(
                wrongSignedPlyPath,
                ["x", "y", "z", "scalar_C2M_signed_distances"],
                [
                    [0.25f, 0.25f, 1.0f, -1.0f],
                    [0.5f, 0.25f, -2.0f, -2.0f],
                    [0.25f, 0.5f, 0.0f, 0.0f],
                    [1.0f, -1.0f, 1.0f, (float)Math.Sqrt(2)],
                    [0.75f, 0.5f, 0.0f, -5e-8f]
                ]);
            WriteBinaryPly(
                changedUnsignedPlyPath,
                ["x", "y", "z", "scalar_C2M_absolute_distances"],
                [
                    [0.5f, 0.25f, 1.0f, 1.0f],
                    [0.5f, 0.25f, -2.0f, 2.0f],
                    [0.25f, 0.5f, 0.0f, 0.0f],
                    [1.0f, -1.0f, 1.0f, (float)Math.Sqrt(2)],
                    [0.75f, 0.5f, 0.0f, 5e-8f]
                ]);
            File.Copy(measuredPlyPath, truncatedPlyPath);
            using (var stream = File.OpenWrite(truncatedPlyPath))
            {
                stream.SetLength(stream.Length - 1);
            }

            var index = new TriangleMeshDistanceIndex([
                new MeshTriangle(0, first.A, first.B, first.C),
                new MeshTriangle(1, second.A, second.B, second.C)
            ]);
            var splitIndex = CreateSplitIndex();

            cases = [
                Check("stream-reader-contract", () => VerifyReaderContract(validPath, first, second)),
                Check("stream-reader-corrupt-length", () => VerifyInvalidData(corruptLengthPath, "length")),
                Check("stream-reader-nonfinite", () => VerifyInvalidData(nonFinitePath, "non-finite")),
                Check("ordered-binary-ply-reader", () => VerifyPlyReader(measuredPlyPath)),
                Check("truncated-binary-ply-rejected", () => VerifyInvalidPly(truncatedPlyPath)),
                Check("complete-synthetic-parity", () => VerifyCompleteParity(validPath, measuredPlyPath, unsignedPlyPath, signedPlyPath, parityPassReportPath)),
                Check("changed-ply-coordinate-fails-parity", () => VerifyCoordinateMismatchParity(validPath, measuredPlyPath, changedUnsignedPlyPath, signedPlyPath, parityMismatchReportPath)),
                Check("material-signed-mismatch-fails-parity", () => VerifySignedMismatchParity(validPath, measuredPlyPath, unsignedPlyPath, wrongSignedPlyPath, paritySignMismatchReportPath)),
                Check("face-positive-signed-distance", () => VerifyFaceDistance(index, new Vector3(0.5f, 0.5f, 2), 0, 2, 2)),
                Check("face-negative-signed-distance", () => VerifyFaceDistance(index, new Vector3(0.5f, 0.5f, -3), 0, 3, -3)),
                Check("face-zero-distance", () => VerifyFaceDistance(index, new Vector3(0.5f, 0.5f, 0), 0, 0, 0)),
                Check("nearest-triangle-selection", () => VerifyFaceDistance(index, new Vector3(10.25f, 0.25f, -4), 1, 4, 4)),
                Check("bvh-split-nearest-triangle", () => VerifyFaceDistance(splitIndex, new Vector3(90.25f, 0.25f, 2), 9, 2, 2)),
                Check("edge-sign-is-explicitly-unresolved", () => VerifyUnresolvedSign(index, new Vector3(1, -1, 1), MeshClosestFeature.Edge, Math.Sqrt(2))),
                Check("edge-sign-robustly-resolved", () => VerifyRobustSign(index, new Vector3(1, -1, 1), MeshClosestFeature.Edge, Math.Sqrt(2), Math.Sqrt(2))),
                Check("vertex-sign-is-explicitly-unresolved", () => VerifyUnresolvedSign(index, new Vector3(-1, -1, 1), MeshClosestFeature.Vertex, Math.Sqrt(3))),
                Check("degenerate-triangle-rejected", VerifyDegenerateTriangleRejected),
                Check("nonfinite-query-rejected", () => VerifyNonFiniteQueryRejected(index))
            ];
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }

        var passedCount = cases.Count(item => item.Passed);
        var status = passedCount == cases.Length ? "Pass" : "Fail";
        var lines = new List<string>
        {
            $"MeshDeviationGoldenVerification|{status}|cases={cases.Length}|passed={passedCount}|failed={cases.Length - passedCount}",
            "DistanceContract|closest=exact-point-to-triangle|acceleration=median-split-bvh|signedDirect=face-interior-only|signedRobust=epsilon-candidate-selection|ply=ordered-binary-little-endian|renderDensity=independent"
        };
        lines.AddRange(cases.Select(item => $"Case|{item.Name}|{(item.Passed ? "Pass" : "Fail")}|{Clean(item.Evidence)}"));

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        File.WriteAllLines(reportPath, lines);
        Console.WriteLine($"Mesh deviation golden verification: {status} ({passedCount}/{cases.Length})");
        return passedCount == cases.Length ? 0 : 5;
    }

    private static (bool Passed, string Evidence) VerifyReaderContract(
        string path,
        BinaryStlTriangle expectedFirst,
        BinaryStlTriangle expectedSecond)
    {
        var visited = new List<(long Index, BinaryStlTriangle Triangle)>();
        var summary = BinaryStlInspectionReader.Scan(path, (index, triangle) => visited.Add((index, triangle)));
        var expectedHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
        var passed = summary.DeclaredTriangleCount == 2
            && summary.ProcessedTriangleCount == 2
            && summary.ExpandedVertexCount == 6
            && summary.SourceByteLength == 184
            && summary.SourceSha256 == expectedHash
            && summary.BoundsMinimum == Vector3.Zero
            && summary.BoundsMaximum == new Vector3(12, 2, 0)
            && visited.Count == 2
            && visited[0] == (0, expectedFirst)
            && visited[1] == (1, expectedSecond);
        return (
            passed,
            $"sha256={summary.SourceSha256},triangles={summary.ProcessedTriangleCount},vertices={summary.ExpandedVertexCount},bounds={Format(summary.BoundsMinimum)}..{Format(summary.BoundsMaximum)},order={string.Join(',', visited.Select(item => item.Index))}");
    }

    private static (bool Passed, string Evidence) VerifyInvalidData(string path, string messageFragment)
    {
        try
        {
            BinaryStlInspectionReader.Scan(path);
            return (false, "reader accepted invalid STL");
        }
        catch (InvalidDataException exception)
        {
            var matched = exception.Message.Contains(messageFragment, StringComparison.OrdinalIgnoreCase);
            return (
                matched,
                matched
                    ? $"rejected={Path.GetFileName(path)},cause={messageFragment}"
                    : exception.Message);
        }
    }

    private static (bool Passed, string Evidence) VerifyPlyReader(string path)
    {
        using var reader = new BinaryPlyVertexReader(path);
        var firstChunkCount = reader.ReadChunk(2);
        var first = reader.GetPosition(0);
        var second = reader.GetPosition(1);
        var secondChunkCount = reader.ReadChunk();
        var third = reader.GetPosition(0);
        var passed = reader.VertexCount == 5
            && reader.Properties.SequenceEqual(["x", "y", "z"], StringComparer.Ordinal)
            && firstChunkCount == 2
            && secondChunkCount == 3
            && first == new Vector3(0.25f, 0.25f, 1.0f)
            && second == new Vector3(0.5f, 0.25f, -2.0f)
            && third == new Vector3(0.25f, 0.5f, 0.0f)
            && reader.IsComplete;
        return (passed, $"vertices={reader.VertexCount},chunks={firstChunkCount}+{secondChunkCount},complete={reader.IsComplete}");
    }

    private static (bool Passed, string Evidence) VerifyInvalidPly(string path)
    {
        try
        {
            using var reader = new BinaryPlyVertexReader(path);
            return (false, "reader accepted a truncated PLY");
        }
        catch (InvalidDataException exception)
        {
            var passed = exception.Message.Contains("length", StringComparison.OrdinalIgnoreCase);
            return (passed, passed ? "truncated PLY rejected by exact length" : exception.Message);
        }
    }

    private static (bool Passed, string Evidence) VerifyCompleteParity(
        string nominalPath,
        string measuredPath,
        string unsignedPath,
        string signedPath,
        string reportPath)
    {
        var exitCode = MeshDeviationParityVerification.Run(
            nominalPath,
            measuredPath,
            unsignedPath,
            signedPath,
            "test-unit",
            reportPath,
            maxPoints: null);
        var report = File.ReadAllText(reportPath);
        var passed = exitCode == 0
            && report.Contains("MeshDeviationParity|Pass|scope=full", StringComparison.Ordinal)
            && report.Contains("SignedDirectCoverage|resolved=4|unresolved=1|edge=1|vertex=0", StringComparison.Ordinal)
            && report.Contains("SignedRobustParity|Pass|resolved=5|recovered=1|signMismatches=0|recoveredSignMismatches=0|nearZeroSignEquivalent=1", StringComparison.Ordinal)
            && report.Contains("SignedCoverage|complete=True|resolved=5|unresolved=0", StringComparison.Ordinal);
        return (passed, $"exit={exitCode},fullPass={passed}");
    }

    private static (bool Passed, string Evidence) VerifyCoordinateMismatchParity(
        string nominalPath,
        string measuredPath,
        string changedUnsignedPath,
        string signedPath,
        string reportPath)
    {
        var exitCode = MeshDeviationParityVerification.Run(
            nominalPath,
            measuredPath,
            changedUnsignedPath,
            signedPath,
            "test-unit",
            reportPath,
            maxPoints: null);
        var report = File.ReadAllText(reportPath);
        var passed = exitCode == 5
            && report.Contains("MeshDeviationParity|Fail|scope=full", StringComparison.Ordinal)
            && report.Contains("Coordinates|mismatched=1|compared=5", StringComparison.Ordinal);
        return (passed, $"exit={exitCode},coordinateMismatchRejected={passed}");
    }

    private static (bool Passed, string Evidence) VerifySignedMismatchParity(
        string nominalPath,
        string measuredPath,
        string unsignedPath,
        string wrongSignedPath,
        string reportPath)
    {
        var exitCode = MeshDeviationParityVerification.Run(
            nominalPath,
            measuredPath,
            unsignedPath,
            wrongSignedPath,
            "test-unit",
            reportPath,
            maxPoints: null);
        var report = File.ReadAllText(reportPath);
        var passed = exitCode == 5
            && report.Contains("MeshDeviationParity|Fail|scope=full", StringComparison.Ordinal)
            && report.Contains("SignedRobustParity|Fail|resolved=5|recovered=1|signMismatches=1|recoveredSignMismatches=0|nearZeroSignEquivalent=1", StringComparison.Ordinal);
        return (passed, $"exit={exitCode},materialSignMismatchRejected={passed}");
    }

    private static (bool Passed, string Evidence) VerifyFaceDistance(
        TriangleMeshDistanceIndex index,
        Vector3 point,
        long expectedTriangle,
        double expectedUnsignedDistance,
        double expectedSignedDistance)
    {
        var result = index.FindClosest(point);
        var passed = result.SourceTriangleIndex == expectedTriangle
            && result.ClosestFeature == MeshClosestFeature.FaceInterior
            && result.SignResolved
            && Approximately(result.UnsignedDistance, expectedUnsignedDistance)
            && result.SignedDistance is { } signed
            && Approximately(signed, expectedSignedDistance);
        return (
            passed,
            $"triangle={result.SourceTriangleIndex},feature={result.ClosestFeature},unsigned={Format(result.UnsignedDistance)},signed={Format(result.SignedDistance)},resolved={result.SignResolved}");
    }

    private static (bool Passed, string Evidence) VerifyUnresolvedSign(
        TriangleMeshDistanceIndex index,
        Vector3 point,
        MeshClosestFeature expectedFeature,
        double expectedUnsignedDistance)
    {
        var result = index.FindClosest(point);
        var passed = result.ClosestFeature == expectedFeature
            && Approximately(result.UnsignedDistance, expectedUnsignedDistance)
            && result.SignedDistance is null
            && !result.SignResolved;
        return (
            passed,
            $"feature={result.ClosestFeature},unsigned={Format(result.UnsignedDistance)},signed={Format(result.SignedDistance)},resolved={result.SignResolved}");
    }

    private static (bool Passed, string Evidence) VerifyRobustSign(
        TriangleMeshDistanceIndex index,
        Vector3 point,
        MeshClosestFeature expectedFeature,
        double expectedUnsignedDistance,
        double expectedSignedDistance)
    {
        var nearest = index.FindClosest(point);
        var result = index.ResolveRobustSign(point, nearest.UnsignedDistance);
        var passed = !nearest.SignResolved
            && result.ClosestFeature == expectedFeature
            && result.SignResolved
            && Approximately(result.UnsignedDistance, expectedUnsignedDistance)
            && result.SignedDistance is { } signed
            && Approximately(signed, expectedSignedDistance);
        return (
            passed,
            $"feature={result.ClosestFeature},unsigned={Format(result.UnsignedDistance)},signed={Format(result.SignedDistance)},resolved={result.SignResolved}");
    }

    private static (bool Passed, string Evidence) VerifyDegenerateTriangleRejected()
    {
        try
        {
            _ = new TriangleMeshDistanceIndex([
                new MeshTriangle(0, Vector3.Zero, Vector3.UnitX, new Vector3(2, 0, 0))
            ]);
            return (false, "index accepted a degenerate triangle");
        }
        catch (ArgumentException exception)
        {
            return (
                exception.Message.Contains("degenerate", StringComparison.OrdinalIgnoreCase),
                exception.Message);
        }
    }

    private static (bool Passed, string Evidence) VerifyNonFiniteQueryRejected(TriangleMeshDistanceIndex index)
    {
        try
        {
            index.FindClosest(new Vector3(float.PositiveInfinity, 0, 0));
            return (false, "index accepted a non-finite query");
        }
        catch (ArgumentException exception)
        {
            return (
                exception.Message.Contains("finite", StringComparison.OrdinalIgnoreCase),
                exception.Message);
        }
    }

    private static TriangleMeshDistanceIndex CreateSplitIndex()
    {
        var triangles = Enumerable.Range(0, 12)
            .Select(index =>
            {
                var offset = index * 10.0f;
                return new MeshTriangle(
                    index,
                    new Vector3(offset, 0, 0),
                    new Vector3(offset + 2, 0, 0),
                    new Vector3(offset, 2, 0));
            })
            .ToArray();
        return new TriangleMeshDistanceIndex(triangles);
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

    private static void WriteBinaryStl(string path, params BinaryStlTriangle[] triangles)
    {
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);
        writer.Write(new byte[80]);
        writer.Write((uint)triangles.Length);
        foreach (var triangle in triangles)
        {
            WriteVector(writer, triangle.StoredNormal);
            WriteVector(writer, triangle.A);
            WriteVector(writer, triangle.B);
            WriteVector(writer, triangle.C);
            writer.Write(triangle.AttributeByteCount);
        }
    }

    private static void WriteVector(BinaryWriter writer, Vector3 value)
    {
        writer.Write(value.X);
        writer.Write(value.Y);
        writer.Write(value.Z);
    }

    private static void WriteBinaryPly(
        string path,
        IReadOnlyList<string> properties,
        IReadOnlyList<float[]> rows)
    {
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);
        writer.Write(System.Text.Encoding.ASCII.GetBytes(
            $"ply\nformat binary_little_endian 1.0\nelement vertex {rows.Count}\n"));
        foreach (var property in properties)
        {
            writer.Write(System.Text.Encoding.ASCII.GetBytes($"property float {property}\n"));
        }

        writer.Write(System.Text.Encoding.ASCII.GetBytes("end_header\n"));
        foreach (var row in rows)
        {
            if (row.Length != properties.Count)
            {
                throw new InvalidDataException("Synthetic PLY row width does not match its properties.");
            }

            foreach (var value in row)
            {
                writer.Write(value);
            }
        }
    }

    private static bool Approximately(double actual, double expected, double tolerance = 1e-6) =>
        double.IsFinite(actual) && Math.Abs(actual - expected) <= tolerance;

    private static string Format(Vector3 value) =>
        $"({Format(value.X)},{Format(value.Y)},{Format(value.Z)})";

    private static string Format(double? value) =>
        value?.ToString("G17", CultureInfo.InvariantCulture) ?? "unresolved";

    private static string Clean(string value) =>
        value.Replace('|', '/').Replace('\r', ' ').Replace('\n', ' ');

    private sealed record VerificationCase(string Name, bool Passed, string Evidence);
}
