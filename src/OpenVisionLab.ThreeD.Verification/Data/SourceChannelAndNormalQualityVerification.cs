using System.IO;
using System.Numerics;
using System.Text;
using System.Text.Json;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;

namespace OpenVisionLab.ThreeD.Verification.Data;

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
            var glbProgressValues = new List<double>();
            var cancellableBox = GlbMesh.Load(
                Path.Combine(repositoryRoot, BoxRelativePath),
                CancellationToken.None,
                new InlineProgress(glbProgressValues.Add));
            Check(
                "glb-cancellable-overload-preserves-import",
                cancellableBox.TriangleCount == box.TriangleCount
                && cancellableBox.Positions.SequenceEqual(box.Positions)
                && glbProgressValues.Count >= 2
                && glbProgressValues[0] is >= 0.0 and <= 100.0
                && glbProgressValues[^1] == 100.0
                && glbProgressValues.Zip(glbProgressValues.Skip(1), (left, right) => right >= left).All(value => value),
                $"triangles={cancellableBox.TriangleCount};progress={string.Join(',', glbProgressValues.Select(value => value.ToString("F0")))}");
            using (var cancelledGlb = new CancellationTokenSource())
            {
                Check(
                    "glb-import-observes-cancellation",
                    CaptureCancellation(() => GlbMesh.Load(
                        Path.Combine(repositoryRoot, BoxRelativePath),
                        cancelledGlb.Token,
                        new InlineProgress(value =>
                        {
                            if (value >= 20.0)
                            {
                                cancelledGlb.Cancel();
                            }
                        }))),
                    $"cancelled={cancelledGlb.IsCancellationRequested}");
            }
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

            var triangleBinary = CreateSingleTriangleGlbBinary();
            var excessiveAccessorPath = Path.Combine(
                temporaryRoot,
                "excessive-accessor-count.glb");
            WriteGlb(
                excessiveAccessorPath,
                CreateSingleTriangleGlbJson(positionCount: 3_000_001),
                triangleBinary);
            var excessiveAccessorMessage = CaptureInvalidDataMessage(
                () => ImportedMesh.Load(excessiveAccessorPath));
            Check(
                "glb-accessor-count-is-rejected-before-allocation",
                excessiveAccessorMessage.Contains("accessor 0 count", StringComparison.Ordinal)
                && excessiveAccessorMessage.Contains("3,000,000", StringComparison.Ordinal),
                excessiveAccessorMessage);

            var invalidAccessorRangePath = Path.Combine(
                temporaryRoot,
                "invalid-accessor-range.glb");
            WriteGlb(
                invalidAccessorRangePath,
                CreateSingleTriangleGlbJson(positionBufferOffset: 32),
                triangleBinary);
            var invalidAccessorRangeMessage = CaptureInvalidDataMessage(
                () => ImportedMesh.Load(invalidAccessorRangePath));
            Check(
                "glb-accessor-range-is-rejected-before-decode",
                invalidAccessorRangeMessage.Contains("accessor 0 bufferView", StringComparison.Ordinal)
                && invalidAccessorRangeMessage.Contains("BIN chunk", StringComparison.Ordinal),
                invalidAccessorRangeMessage);

            var excessiveTexturePath = Path.Combine(
                temporaryRoot,
                "excessive-texture-length.glb");
            WriteGlb(
                excessiveTexturePath,
                CreateSingleTriangleGlbJson(textureByteLength: 256 * 1024 * 1024 + 1),
                triangleBinary);
            var excessiveTextureMessage = CaptureInvalidDataMessage(
                () => ImportedMesh.Load(excessiveTexturePath));
            Check(
                "glb-texture-length-is-rejected-before-copy",
                excessiveTextureMessage.Contains("embedded texture length", StringComparison.Ordinal)
                && excessiveTextureMessage.Contains("supported limit", StringComparison.Ordinal),
                excessiveTextureMessage);

            var excessiveGlbPath = Path.Combine(temporaryRoot, "excessive-file-size.glb");
            WriteSparseFile(excessiveGlbPath, 512L * 1024 * 1024 + 1);
            var excessiveGlbMessage = CaptureInvalidDataMessage(
                () => ImportedMesh.Load(excessiveGlbPath));
            Check(
                "glb-file-size-is-rejected-before-whole-file-allocation",
                excessiveGlbMessage.Contains("GLB file size", StringComparison.Ordinal)
                && excessiveGlbMessage.Contains("supported limit", StringComparison.Ordinal),
                excessiveGlbMessage);

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
            var stlProgressValues = new List<double>();
            var cancellableBinaryStl = StlMesh.Load(
                binaryStlPath,
                CancellationToken.None,
                new InlineProgress(stlProgressValues.Add));
            Check(
                "stl-cancellable-overload-preserves-import",
                cancellableBinaryStl.TriangleCount == binaryStl.TriangleCount
                && cancellableBinaryStl.Positions.SequenceEqual(binaryStl.Positions)
                && stlProgressValues.Count >= 2
                && stlProgressValues[^1] == 100.0
                && stlProgressValues.Zip(stlProgressValues.Skip(1), (left, right) => right >= left).All(value => value),
                $"triangles={cancellableBinaryStl.TriangleCount};progress={string.Join(',', stlProgressValues.Select(value => value.ToString("F0")))}");
            using (var cancelledStl = new CancellationTokenSource())
            {
                Check(
                    "stl-import-observes-cancellation",
                    CaptureCancellation(() => StlMesh.Load(
                        binaryStlPath,
                        cancelledStl.Token,
                        new InlineProgress(value =>
                        {
                            if (value >= 28.0)
                            {
                                cancelledStl.Cancel();
                            }
                        }))),
                    $"cancelled={cancelledStl.IsCancellationRequested}");
            }
            var binaryStlNormals =
                ImportedMeshNormalQualityAnalyzer.Create(binaryStl);
            Check(
                "binary-stl-loader-preserves-valid-stored-normal",
                binaryStlNormals.State == SourceNormalQualityState.Valid
                && binaryStlNormals.NormalCount == 3
                && binaryStlNormals.ConsistentCornerCount == 3,
                DescribeNormalReport(binaryStlNormals));

            var excessiveBinaryStlPath = Path.Combine(
                temporaryRoot,
                "excessive-triangle-count.stl");
            WriteDeclaredBinaryStl(excessiveBinaryStlPath, 1_000_001);
            var excessiveBinaryStlMessage = CaptureInvalidDataMessage(
                () => StlMesh.Load(excessiveBinaryStlPath));
            Check(
                "binary-stl-triangle-count-is-rejected-before-whole-file-allocation",
                excessiveBinaryStlMessage.Contains("1,000,001", StringComparison.Ordinal)
                && excessiveBinaryStlMessage.Contains("1,000,000", StringComparison.Ordinal),
                excessiveBinaryStlMessage);

            var excessiveStlPath = Path.Combine(temporaryRoot, "excessive-file-size.stl");
            WriteSparseFile(excessiveStlPath, 512L * 1024 * 1024 + 1);
            var excessiveStlMessage = CaptureInvalidDataMessage(
                () => StlMesh.Load(excessiveStlPath));
            Check(
                "stl-file-size-is-rejected-before-whole-file-allocation",
                excessiveStlMessage.Contains("STL file size", StringComparison.Ordinal)
                && excessiveStlMessage.Contains("supported limit", StringComparison.Ordinal),
                excessiveStlMessage);

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
            var lasProgressValues = new List<double>();
            var lasWithProgress = LazPointCloud.Load(
                Path.Combine(repositoryRoot, LasRelativePath),
                maxSampledPoints: 64,
                CancellationToken.None,
                new InlineProgress(lasProgressValues.Add));
            Check(
                "las-cancellable-overload-preserves-sync-result",
                lasWithProgress.FormatContractLine() == las.FormatContractLine()
                && lasWithProgress.SampledPoints.SequenceEqual(las.SampledPoints),
                $"sync={las.SampledPoints.Length};cancellable={lasWithProgress.SampledPoints.Length}");
            Check(
                "las-load-progress-is-monotonic-and-bounded",
                lasProgressValues.Count >= 2
                && lasProgressValues[0] == 0.0
                && lasProgressValues[^1] == 100.0
                && lasProgressValues.All(value => value is >= 0.0 and <= 100.0)
                && lasProgressValues.Zip(lasProgressValues.Skip(1), (left, right) => right >= left).All(value => value),
                string.Join(',', lasProgressValues.Select(value => value.ToString("F1", System.Globalization.CultureInfo.InvariantCulture))));
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

            using var cancelledLoad = new CancellationTokenSource();
            var cancellationObserved = false;
            try
            {
                LazPointCloud.Load(
                    Path.Combine(repositoryRoot, LazRgbRelativePath),
                    maxSampledPoints: 64,
                    cancelledLoad.Token,
                    new InlineProgress(value =>
                    {
                        if (value >= 1.0)
                        {
                            cancelledLoad.Cancel();
                        }
                    }));
            }
            catch (OperationCanceledException)
            {
                cancellationObserved = true;
            }

            Check(
                "laz-load-cancellation-stops-decode",
                cancellationObserved,
                $"cancelled={cancelledLoad.IsCancellationRequested};observed={cancellationObserved}");

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

    private static byte[] CreateSingleTriangleGlbBinary()
    {
        var bytes = new byte[44];
        var positions = new[]
        {
            new Vector3(0, 0, 0),
            new Vector3(1, 0, 0),
            new Vector3(0, 1, 0)
        };
        var offset = 0;
        foreach (var position in positions)
        {
            BitConverter.TryWriteBytes(bytes.AsSpan(offset, 4), position.X);
            BitConverter.TryWriteBytes(bytes.AsSpan(offset + 4, 4), position.Y);
            BitConverter.TryWriteBytes(bytes.AsSpan(offset + 8, 4), position.Z);
            offset += 12;
        }

        BitConverter.TryWriteBytes(bytes.AsSpan(36, 2), (ushort)0);
        BitConverter.TryWriteBytes(bytes.AsSpan(38, 2), (ushort)1);
        BitConverter.TryWriteBytes(bytes.AsSpan(40, 2), (ushort)2);
        return bytes;
    }

    private static string CreateSingleTriangleGlbJson(
        int positionCount = 3,
        int positionBufferOffset = 0,
        int? textureByteLength = null)
    {
        var textureJson = textureByteLength is null
            ? string.Empty
            : """
              ,"materials":[{"pbrMetallicRoughness":{"baseColorTexture":{"index":0}}}]
              ,"textures":[{"source":0}]
              ,"images":[{"bufferView":2,"mimeType":"image/png"}]
              """;
        var materialJson = textureByteLength is null ? string.Empty : ",\"material\":0";
        var textureBufferView = textureByteLength is null
            ? string.Empty
            : $",{{\"buffer\":0,\"byteOffset\":44,\"byteLength\":{textureByteLength.Value}}}";
        return $$"""
            {"asset":{"version":"2.0"},"meshes":[{"primitives":[{"attributes":{"POSITION":0},"indices":1{{materialJson}}}]}],"accessors":[{"bufferView":0,"componentType":5126,"count":{{positionCount}},"type":"VEC3"},{"bufferView":1,"componentType":5123,"count":3,"type":"SCALAR"}],"bufferViews":[{"buffer":0,"byteOffset":{{positionBufferOffset}},"byteLength":36},{"buffer":0,"byteOffset":36,"byteLength":6}{{textureBufferView}}],"buffers":[{"byteLength":44}]{{textureJson}}}
            """;
    }

    private static void WriteGlb(string path, string json, byte[] binary)
    {
        var jsonBytes = Encoding.UTF8.GetBytes(json);
        var paddedJsonLength = (jsonBytes.Length + 3) & ~3;
        var paddedBinaryLength = (binary.Length + 3) & ~3;
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream, Encoding.UTF8);
        writer.Write(0x46546C67u);
        writer.Write(2u);
        writer.Write(checked((uint)(12 + 8 + paddedJsonLength + 8 + paddedBinaryLength)));
        writer.Write(checked((uint)paddedJsonLength));
        writer.Write(0x4E4F534Au);
        writer.Write(jsonBytes);
        writer.Write(Enumerable.Repeat((byte)' ', paddedJsonLength - jsonBytes.Length).ToArray());
        writer.Write(checked((uint)paddedBinaryLength));
        writer.Write(0x004E4942u);
        writer.Write(binary);
        writer.Write(new byte[paddedBinaryLength - binary.Length]);
    }

    private static void WriteDeclaredBinaryStl(string path, uint triangleCount)
    {
        using var stream = File.Create(path);
        stream.SetLength(84L + triangleCount * 50L);
        stream.Position = 80;
        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);
        writer.Write(triangleCount);
    }

    private static void WriteSparseFile(string path, long length)
    {
        using var stream = File.Create(path);
        stream.SetLength(length);
    }

    private static string CaptureInvalidDataMessage(Func<ImportedMesh> load)
    {
        try
        {
            _ = load();
            return "Expected InvalidDataException, but the import succeeded.";
        }
        catch (InvalidDataException exception)
        {
            return exception.Message;
        }
    }

    private static bool CaptureCancellation(Func<ImportedMesh> load)
    {
        try
        {
            _ = load();
            return false;
        }
        catch (OperationCanceledException)
        {
            return true;
        }
    }

    private sealed class InlineProgress(Action<double> report) : IProgress<double>
    {
        public void Report(double value) => report(value);
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
