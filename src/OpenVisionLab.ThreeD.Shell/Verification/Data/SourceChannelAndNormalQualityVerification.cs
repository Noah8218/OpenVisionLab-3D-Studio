using System.IO;
using System.Numerics;
using System.Text;
using System.Text.Json;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;

namespace OpenVisionLab.ThreeD.Shell;

internal static class SourceChannelAndNormalQualityVerification
{
    private const string BoxRelativePath = "3D/PublicSamples/glTF/Box.glb";
    private const string VertexColorBoxRelativePath =
        "3D/PublicSamples/glTF/BoxVertexColors.glb";
    private const string TetrahedronRelativePath =
        "3D/PublicSamples/STL/Tetrahedron.stl";
    private const string LasRelativePath =
        "3D/PublicSamples/PointCloud/interesting.las";
    private const string LazRgbRelativePath =
        "3D/PublicSamples/PointCloud/xyzrgb_manuscript.laz";

    public static bool Verify(string reportPath, out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);

        var lines = new List<string>
        {
            "OpenVisionLab 3D source-channel and dense-normal quality verification",
            $"Generated: {DateTimeOffset.Now:O}"
        };
        var passed = 0;
        var total = 0;
        var temporaryRoot = Path.Combine(
            Path.GetTempPath(),
            "OpenVisionLab.ThreeD",
            nameof(SourceChannelAndNormalQualityVerification),
            Guid.NewGuid().ToString("N"));

        void Check(string name, bool condition, string detail)
        {
            total++;
            lines.Add($"{(condition ? "PASS" : "FAIL")} | {name} | {detail}");
            if (condition)
            {
                passed++;
            }
        }

        try
        {
            Directory.CreateDirectory(temporaryRoot);
            var repositoryRoot = FindRepositoryRoot();

            var c3dChannels =
                SourceChannelCatalogAnalyzer.CreateForC3DHeightGrid();
            CheckCatalogShape("c3d-catalog-has-seven-unique-channels", c3dChannels);
            Check(
                "c3d-exposes-only-native-height",
                AvailableChannels(c3dChannels).SequenceEqual(
                    [SourceQualityChannel.Height]),
                DescribeChannels(c3dChannels));
            Check(
                "c3d-normal-is-explicitly-unavailable",
                Find(c3dChannels, SourceQualityChannel.Normal) is
                {
                    State: SourceQualityChannelState.Unavailable,
                    Evidence.Length: > 0
                },
                Find(c3dChannels, SourceQualityChannel.Normal).Evidence);

            var box = ImportedMesh.Load(
                Path.Combine(repositoryRoot, BoxRelativePath));
            var boxChannels =
                SourceChannelCatalogAnalyzer.CreateForImportedMesh(box);
            var boxNormals = ImportedMeshNormalQualityAnalyzer.Create(box);
            CheckCatalogShape("glb-catalog-has-seven-unique-channels", boxChannels);
            Check(
                "glb-loader-preserves-declared-normals",
                box.HasDeclaredNormals
                && box.Normals.Length == box.Positions.Length
                && Find(boxChannels, SourceQualityChannel.Normal).State
                    == SourceQualityChannelState.Available,
                $"positions={box.Positions.Length};normals={box.Normals.Length};state={boxNormals.State}");
            Check(
                "public-box-dense-normals-are-valid",
                boxNormals.State == SourceNormalQualityState.Valid
                && boxNormals.IsDense
                && boxNormals.IsUsable
                && boxNormals.ComparableCornerCount == box.Indices.Length,
                DescribeNormalReport(boxNormals));
            Check(
                "plain-box-does-not-fabricate-color",
                Find(boxChannels, SourceQualityChannel.Color).State
                    == SourceQualityChannelState.Unavailable,
                Find(boxChannels, SourceQualityChannel.Color).Evidence);

            var vertexColorBox = ImportedMesh.Load(
                Path.Combine(repositoryRoot, VertexColorBoxRelativePath));
            var vertexColorChannels =
                SourceChannelCatalogAnalyzer.CreateForImportedMesh(vertexColorBox);
            Check(
                "vertex-color-glb-exposes-real-color-and-normal",
                vertexColorBox.HasVertexColors
                && Find(vertexColorChannels, SourceQualityChannel.Color).State
                    == SourceQualityChannelState.Available
                && Find(vertexColorChannels, SourceQualityChannel.Normal).State
                    == SourceQualityChannelState.Available,
                DescribeChannels(vertexColorChannels));

            var tetrahedron = StlMesh.Load(
                Path.Combine(repositoryRoot, TetrahedronRelativePath));
            var tetrahedronChannels =
                SourceChannelCatalogAnalyzer.CreateForImportedMesh(tetrahedron);
            var tetrahedronNormals =
                ImportedMeshNormalQualityAnalyzer.Create(tetrahedron);
            Check(
                "ascii-stl-loader-preserves-facet-normals",
                tetrahedron.HasDeclaredNormals
                && tetrahedron.Normals.Length == tetrahedron.Positions.Length
                && Find(tetrahedronChannels, SourceQualityChannel.Normal).State
                    == SourceQualityChannelState.Available,
                $"positions={tetrahedron.Positions.Length};normals={tetrahedron.Normals.Length}");
            Check(
                "non-unit-public-stl-normals-fail-closed",
                tetrahedronNormals.State == SourceNormalQualityState.Invalid
                && tetrahedronNormals.UnitLengthNormalCount
                    < tetrahedronNormals.NormalCount,
                DescribeNormalReport(tetrahedronNormals));

            var binaryStlPath = Path.Combine(
                temporaryRoot,
                "known-normal-binary.stl");
            WriteSingleTriangleBinaryStl(binaryStlPath);
            var binaryStl = StlMesh.Load(binaryStlPath);
            var binaryStlNormals =
                ImportedMeshNormalQualityAnalyzer.Create(binaryStl);
            Check(
                "binary-stl-loader-preserves-valid-stored-normal",
                binaryStlNormals.State == SourceNormalQualityState.Valid
                && binaryStlNormals.NormalCount == 3
                && binaryStlNormals.ConsistentCornerCount == 3,
                DescribeNormalReport(binaryStlNormals));

            var partialAsciiStlPath = Path.Combine(
                temporaryRoot,
                "partial-normal-ascii.stl");
            WritePartialNormalAsciiStl(partialAsciiStlPath);
            var partialAsciiStl = StlMesh.Load(partialAsciiStlPath);
            var partialAsciiChannels =
                SourceChannelCatalogAnalyzer.CreateForImportedMesh(partialAsciiStl);
            var partialAsciiNormals =
                ImportedMeshNormalQualityAnalyzer.Create(partialAsciiStl);
            Check(
                "partial-stl-normal-channel-remains-visible-and-invalid",
                partialAsciiStl.HasDeclaredNormals
                && !partialAsciiStl.HasDenseNormals
                && partialAsciiStl.DeclaredNormalCount == 3
                && partialAsciiNormals.State == SourceNormalQualityState.Invalid
                && partialAsciiNormals.NormalCount == 3
                && Find(partialAsciiChannels, SourceQualityChannel.Normal).State
                    == SourceQualityChannelState.Available,
                DescribeNormalReport(partialAsciiNormals));

            var noNormalMesh = ImportedMesh.CreateTriangleMesh(
                "synthetic-no-normal.glb",
                "synthetic-no-normal",
                "GLB",
                [
                    new Vector3(0, 0, 0),
                    new Vector3(1, 0, 0),
                    new Vector3(0, 1, 0)
                ],
                [0, 1, 2]);
            var noNormalChannels =
                SourceChannelCatalogAnalyzer.CreateForImportedMesh(noNormalMesh);
            var noNormalReport =
                ImportedMeshNormalQualityAnalyzer.Create(noNormalMesh);
            Check(
                "missing-normal-channel-is-never-fabricated",
                !noNormalMesh.HasDeclaredNormals
                && Find(noNormalChannels, SourceQualityChannel.Normal).State
                    == SourceQualityChannelState.Unavailable
                && noNormalReport.State == SourceNormalQualityState.Unavailable
                && noNormalReport.NormalCount == 0,
                DescribeNormalReport(noNormalReport));

            var positions = new[]
            {
                new Vector3(0, 0, 0),
                new Vector3(1, 0, 0),
                new Vector3(1, 1, 0),
                new Vector3(0, 1, 0)
            };
            var indices = new[] { 0, 1, 2, 0, 2, 3 };
            var validNormals = Enumerable
                .Repeat(Vector3.UnitZ, positions.Length)
                .ToArray();
            var knownNormalReport = ImportedMeshNormalQualityAnalyzer.Create(
                "known-normal-plane",
                "fixture",
                positions,
                indices,
                validNormals);
            Check(
                "known-normal-fixture-is-valid",
                knownNormalReport.State == SourceNormalQualityState.Valid
                && knownNormalReport.NormalCount == positions.Length
                && knownNormalReport.ComparableCornerCount == indices.Length
                && knownNormalReport.ConsistentCornerCount == indices.Length
                && knownNormalReport.ReversedCornerCount == 0,
                DescribeNormalReport(knownNormalReport));

            var reversedNormalReport =
                ImportedMeshNormalQualityAnalyzer.Create(
                    "reversed-normal-plane",
                    "fixture",
                    positions,
                    indices,
                    Enumerable.Repeat(-Vector3.UnitZ, positions.Length).ToArray());
            Check(
                "reversed-normal-fixture-fails-closed",
                reversedNormalReport.State == SourceNormalQualityState.Invalid
                && reversedNormalReport.ReversedCornerCount == indices.Length
                && reversedNormalReport.ConsistentCornerCount == 0,
                DescribeNormalReport(reversedNormalReport));

            var partialNormalReport =
                ImportedMeshNormalQualityAnalyzer.Create(
                    "partial-normal-plane",
                    "fixture",
                    positions,
                    indices,
                    validNormals[..^1]);
            Check(
                "partial-normal-channel-fails-closed",
                partialNormalReport.State == SourceNormalQualityState.Invalid
                && !partialNormalReport.IsDense
                && partialNormalReport.NormalCount == positions.Length - 1,
                DescribeNormalReport(partialNormalReport));

            var invalidNormals = validNormals.ToArray();
            invalidNormals[0] = Vector3.Zero;
            invalidNormals[1] = new Vector3(float.NaN, 0, 1);
            var invalidValueReport =
                ImportedMeshNormalQualityAnalyzer.Create(
                    "invalid-normal-values",
                    "fixture",
                    positions,
                    indices,
                    invalidNormals);
            Check(
                "zero-and-nonfinite-normals-fail-closed",
                invalidValueReport.State == SourceNormalQualityState.Invalid
                && invalidValueReport.FiniteNormalCount == positions.Length - 1
                && invalidValueReport.NonZeroNormalCount == positions.Length - 2,
                DescribeNormalReport(invalidValueReport));

            var degenerateReport =
                ImportedMeshNormalQualityAnalyzer.Create(
                    "degenerate-triangle",
                    "fixture",
                    positions,
                    [0, 0, 1],
                    validNormals);
            Check(
                "degenerate-triangle-fails-closed",
                degenerateReport.State == SourceNormalQualityState.Invalid
                && degenerateReport.DegenerateTriangleCount == 1,
                DescribeNormalReport(degenerateReport));

            var invalidIndexReport =
                ImportedMeshNormalQualityAnalyzer.Create(
                    "invalid-triangle-index",
                    "fixture",
                    positions,
                    [0, 4, 5],
                    validNormals);
            Check(
                "invalid-triangle-indices-fail-closed",
                invalidIndexReport.State == SourceNormalQualityState.Invalid
                && invalidIndexReport.InvalidIndexCount == 2
                && invalidIndexReport.ComparableCornerCount == 0,
                DescribeNormalReport(invalidIndexReport));

            var incompleteIndexReport =
                ImportedMeshNormalQualityAnalyzer.Create(
                    "incomplete-index-buffer",
                    "fixture",
                    positions,
                    [0, 1, 2, 3],
                    validNormals);
            Check(
                "incomplete-triangle-index-buffer-fails-closed",
                incompleteIndexReport.State == SourceNormalQualityState.Invalid,
                DescribeNormalReport(incompleteIndexReport));

            var serializedOnce = JsonSerializer.Serialize(knownNormalReport);
            var serializedTwice = JsonSerializer.Serialize(knownNormalReport);
            Check(
                "normal-report-json-is-deterministic",
                serializedOnce == serializedTwice
                && serializedOnce.Contains(
                    "\"State\":\"Valid\"",
                    StringComparison.Ordinal),
                $"bytes={Encoding.UTF8.GetByteCount(serializedOnce)}");

            var las = LazPointCloud.Load(
                Path.Combine(repositoryRoot, LasRelativePath),
                maxSampledPoints: 64);
            var lasChannels =
                SourceChannelCatalogAnalyzer.CreateForLazPointCloud(las);
            CheckCatalogShape("las-catalog-has-seven-unique-channels", lasChannels);
            Check(
                "las-loader-preserves-declared-intensity",
                las.HasIntensity
                && las.SampledPoints.Length > 0
                && Find(lasChannels, SourceQualityChannel.Intensity).State
                    == SourceQualityChannelState.Available,
                $"format={las.Metadata.PointDataFormat};samples={las.SampledPoints.Length};firstIntensity={las.SampledPoints[0].Intensity}");
            Check(
                "rgb-las-exposes-declared-intensity-and-color",
                las.HasRgb
                && AvailableChannels(lasChannels).SequenceEqual(
                    [
                        SourceQualityChannel.Intensity,
                        SourceQualityChannel.Color
                    ]),
                $"format={las.Metadata.PointDataFormat};{DescribeChannels(lasChannels)}");

            var lazRgb = LazPointCloud.Load(
                Path.Combine(repositoryRoot, LazRgbRelativePath),
                maxSampledPoints: 64);
            var lazRgbChannels =
                SourceChannelCatalogAnalyzer.CreateForLazPointCloud(lazRgb);
            Check(
                "rgb-laz-exposes-only-declared-intensity-and-color",
                lazRgb.HasIntensity
                && lazRgb.HasRgb
                && AvailableChannels(lazRgbChannels).SequenceEqual(
                    [
                        SourceQualityChannel.Intensity,
                        SourceQualityChannel.Color
                    ]),
                $"format={lazRgb.Metadata.PointDataFormat};{DescribeChannels(lazRgbChannels)}");

            Check(
                "every-unavailable-channel-has-evidence",
                new[]
                {
                    c3dChannels,
                    boxChannels,
                    vertexColorChannels,
                    tetrahedronChannels,
                    lasChannels,
                    lazRgbChannels
                }
                .SelectMany(channels => channels)
                .Where(channel =>
                    channel.State == SourceQualityChannelState.Unavailable)
                .All(channel => !string.IsNullOrWhiteSpace(channel.Evidence)),
                "All unavailable entries retain an explicit source-specific reason.");

            void CheckCatalogShape(
                string name,
                IReadOnlyList<SourceQualityChannelAvailability> channels)
            {
                Check(
                    name,
                    channels.Count == 7
                    && channels.Select(channel => channel.Channel).Distinct().Count()
                        == 7
                    && channels.All(channel =>
                        !string.IsNullOrWhiteSpace(channel.Evidence)),
                    DescribeChannels(channels));
            }
        }
        catch (Exception exception)
        {
            Check(
                "unexpected-exception",
                false,
                $"{exception.GetType().Name}: {exception}");
        }
        finally
        {
            try
            {
                if (Directory.Exists(temporaryRoot))
                {
                    Directory.Delete(temporaryRoot, recursive: true);
                }
            }
            catch
            {
                // Report the functional result even if antivirus delays temp cleanup.
            }
        }

        var succeeded = total > 0 && passed == total;
        lines.Add($"Result={(succeeded ? "PASS" : "FAIL")}|{passed}/{total}");
        var fullReportPath = Path.GetFullPath(reportPath);
        Directory.CreateDirectory(
            Path.GetDirectoryName(fullReportPath) ?? Environment.CurrentDirectory);
        File.WriteAllLines(
            fullReportPath,
            lines,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        summary =
            $"Source channel + dense normal quality verification: {(succeeded ? "PASS" : "FAIL")} ({passed}/{total})";
        return succeeded;
    }

    private static SourceQualityChannelAvailability Find(
        IReadOnlyList<SourceQualityChannelAvailability> channels,
        SourceQualityChannel channel) =>
        channels.Single(item => item.Channel == channel);

    private static IEnumerable<SourceQualityChannel> AvailableChannels(
        IReadOnlyList<SourceQualityChannelAvailability> channels) =>
        channels
            .Where(channel =>
                channel.State == SourceQualityChannelState.Available)
            .Select(channel => channel.Channel);

    private static string DescribeChannels(
        IReadOnlyList<SourceQualityChannelAvailability> channels) =>
        string.Join(
            ',',
            channels.Select(channel =>
                $"{channel.Channel}={channel.State}"));

    private static string DescribeNormalReport(
        SourceNormalQualityReport report) =>
        $"state={report.State};dense={report.IsDense};positions={report.PositionCount};normals={report.NormalCount};finite={report.FiniteNormalCount};nonZero={report.NonZeroNormalCount};unit={report.UnitLengthNormalCount};aligned={report.ConsistentCornerCount}/{report.ComparableCornerCount};reversed={report.ReversedCornerCount};degenerate={report.DegenerateTriangleCount}";

    private static void WriteSingleTriangleBinaryStl(string path)
    {
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream, Encoding.ASCII);
        writer.Write(new byte[80]);
        writer.Write((uint)1);
        WriteVector(writer, Vector3.UnitZ);
        WriteVector(writer, new Vector3(0, 0, 0));
        WriteVector(writer, new Vector3(1, 0, 0));
        WriteVector(writer, new Vector3(0, 1, 0));
        writer.Write((ushort)0);
    }

    private static void WritePartialNormalAsciiStl(string path)
    {
        const string contents = """
            solid partial
              facet normal 0 0 1
                outer loop
                  vertex 0 0 0
                  vertex 1 0 0
                  vertex 0 1 0
                endloop
              endfacet
              facet
                outer loop
                  vertex 0 0 1
                  vertex 1 0 1
                  vertex 0 1 1
                endloop
              endfacet
            endsolid partial
            """;
        File.WriteAllText(path, contents, new UTF8Encoding(false));
    }

    private static void WriteVector(BinaryWriter writer, Vector3 value)
    {
        writer.Write(value.X);
        writer.Write(value.Y);
        writer.Write(value.Z);
    }

    private static string FindRepositoryRoot()
    {
        foreach (var start in new[]
                 {
                     AppContext.BaseDirectory,
                     Environment.CurrentDirectory
                 })
        {
            for (var directory = new DirectoryInfo(start);
                 directory is not null;
                 directory = directory.Parent)
            {
                if (File.Exists(
                        Path.Combine(directory.FullName, BoxRelativePath))
                    && File.Exists(
                        Path.Combine(directory.FullName, TetrahedronRelativePath))
                    && File.Exists(
                        Path.Combine(directory.FullName, LazRgbRelativePath)))
                {
                    return directory.FullName;
                }
            }
        }

        throw new DirectoryNotFoundException(
            "Could not locate the OpenVisionLab 3D public source fixtures.");
    }
}
