using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

internal static class StanfordTransformParityVerification
{
    private const double PointTolerance = 1e-12;
    private const double SumTolerance = 1e-9;
    private const double WeightedSumTolerance = 1e-6;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static int Run(string confPath, string referencePath, string reportPath)
    {
        var failures = new List<string>();
        var lines = new List<string>();
        try
        {
            confPath = Path.GetFullPath(confPath);
            referencePath = Path.GetFullPath(referencePath);
            var reference = JsonSerializer.Deserialize<ReferenceDocument>(
                File.ReadAllText(referencePath),
                JsonOptions) ?? throw new InvalidDataException("Transform reference JSON is empty.");
            ValidateReference(reference);
            var configuration = ReadConfiguration(confPath);
            var synthetic = RunSyntheticChecks();
            if (synthetic.Passed != synthetic.Total)
            {
                failures.Add($"Synthetic transform checks failed: {synthetic.Passed}/{synthetic.Total}");
            }

            Require(reference.SchemaVersion == "1.0", "Reference schema must be 1.0.", failures);
            Require(reference.Provenance.GeneratorVersion == "1.0", "Reference generator version must be 1.0.", failures);
            Require(
                reference.Contract.Application == "transpose(ShoemakeQuaternionMatrix(q))*point+translation",
                "Reference transform application contract differs.",
                failures);
            Require(reference.Contract.QuaternionOrder == "x,y,z,w", "Reference quaternion order differs.", failures);
            Require(!reference.Contract.CameraApplied, "Camera must not be applied to bmesh points.", failures);
            Require(configuration.Scans.Count == reference.ScanCount, "Configuration scan count differs.", failures);
            CompareVector(reference.Camera, configuration.Camera, PointTolerance, "camera", failures);
            Require(
                new FileInfo(confPath).Length == reference.Provenance.ConfigurationByteLength,
                "Configuration byte length differs from the reference.",
                failures);
            Require(
                Sha256(confPath) == reference.Provenance.ConfigurationSha256,
                "Configuration SHA-256 differs from the reference.",
                failures);

            var aggregate = new Statistics();
            var globalIndex = 0L;
            var pointChecks = 0;
            var maximumPointDelta = 0.0;
            var maximumScanStatisticDelta = 0.0;
            var pointParityPassed = true;
            var scanStatisticParityPassed = true;
            for (var scanIndex = 0; scanIndex < configuration.Scans.Count; scanIndex++)
            {
                var configured = configuration.Scans[scanIndex];
                var expected = reference.Scans[scanIndex];
                Require(configured.FileName == expected.FileName, $"Scan order differs at index {scanIndex}.", failures);
                CompareVector(expected.Translation, configured.Translation, PointTolerance, $"{configured.FileName} translation", failures);
                CompareVector(expected.QuaternionXyzw, configured.QuaternionXyzw, PointTolerance, $"{configured.FileName} quaternion", failures);

                var scanPath = Path.Combine(Path.GetDirectoryName(confPath)!, configured.FileName);
                var source = ReadRangePly(scanPath);
                Require(source.ByteLength == expected.ByteLength, $"{configured.FileName} byte length differs.", failures);
                Require(source.Sha256 == expected.Sha256, $"{configured.FileName} SHA-256 differs.", failures);
                Require(source.Points.Length == expected.VertexCount, $"{configured.FileName} vertex count differs.", failures);
                Require(source.RangeGridCount == expected.RangeGridCount, $"{configured.FileName} range-grid count differs.", failures);
                Require(source.Columns == expected.Columns && source.Rows == expected.Rows, $"{configured.FileName} dimensions differ.", failures);

                var matrix = RotationMatrix.Create(configured.QuaternionXyzw);
                var sourceStatistics = new Statistics();
                var transformedStatistics = new Statistics();
                var checkpointMap = expected.Checkpoints.ToDictionary(item => item.Index);
                foreach (var checkpoint in expected.Checkpoints)
                {
                    Require(
                        checkpoint.Index >= 0 && checkpoint.Index < source.Points.Length,
                        $"{configured.FileName} checkpoint index is outside the source.",
                        failures);
                }

                for (var pointIndex = 0; pointIndex < source.Points.Length; pointIndex++)
                {
                    var point = source.Points[pointIndex];
                    var transformed = matrix.Transform(point, configured.Translation);
                    sourceStatistics.Add(point, pointIndex + 1L);
                    transformedStatistics.Add(transformed, pointIndex + 1L);
                    aggregate.Add(transformed, globalIndex + 1L);
                    globalIndex++;

                    if (!checkpointMap.TryGetValue(pointIndex, out var checkpoint))
                    {
                        continue;
                    }

                    var pointFailureCount = failures.Count;
                    maximumPointDelta = Math.Max(
                        maximumPointDelta,
                        ComparePoint(checkpoint.Source, point, PointTolerance, $"{configured.FileName} source point {pointIndex}", failures));
                    maximumPointDelta = Math.Max(
                        maximumPointDelta,
                        ComparePoint(checkpoint.Transformed, transformed, PointTolerance, $"{configured.FileName} transformed point {pointIndex}", failures));
                    pointParityPassed &= failures.Count == pointFailureCount;
                    pointChecks++;
                }

                var scanStatisticFailureCount = failures.Count;
                maximumScanStatisticDelta = Math.Max(
                    maximumScanStatisticDelta,
                    CompareStatistics(expected.SourceStatistics, sourceStatistics, $"{configured.FileName} source", failures));
                maximumScanStatisticDelta = Math.Max(
                    maximumScanStatisticDelta,
                    CompareStatistics(expected.TransformedStatistics, transformedStatistics, $"{configured.FileName} transformed", failures));
                scanStatisticParityPassed &= failures.Count == scanStatisticFailureCount;
                CompareScalar(expected.QuaternionNorm, configured.QuaternionNorm, PointTolerance, $"{configured.FileName} quaternion norm", failures);
                CompareScalar(expected.RotationAngleDegrees, configured.RotationAngleDegrees, PointTolerance, $"{configured.FileName} angle", failures);
                CompareScalar(expected.RotationDeterminant, matrix.Determinant, PointTolerance, $"{configured.FileName} determinant", failures);
                CompareScalar(expected.RotationOrthogonalityMaxError, matrix.OrthogonalityMaxError, PointTolerance, $"{configured.FileName} orthogonality", failures);

                lines.Add(
                    $"Scan|file={configured.FileName}|sha256={source.Sha256}|points={source.Points.Length}"
                    + $"|angleDegrees={Format(configured.RotationAngleDegrees)}"
                    + $"|minimum={Format(transformedStatistics.Minimum)}|maximum={Format(transformedStatistics.Maximum)}");
            }

            Require(pointChecks == reference.PointReferenceCount, "Point checkpoint count differs.", failures);
            pointParityPassed &= pointChecks == reference.PointReferenceCount;
            var aggregateFailureCount = failures.Count;
            var maximumAggregateDelta = CompareStatistics(
                reference.AggregateTransformedStatistics,
                aggregate,
                "aggregate transformed",
                failures);
            var aggregateParityPassed = failures.Count == aggregateFailureCount;
            var status = failures.Count == 0 ? "Pass" : "Fail";
            lines.InsertRange(0,
            [
                $"StanfordTransformParity|{status}|scans={configuration.Scans.Count}|points={aggregate.Count}|pointChecks={pointChecks}|failures={failures.Count}",
                "TransformContract|quaternion=x,y,z,w|application=transpose(ShoemakeQuaternionMatrix(q))*point+translation|cameraApplied=False|input=binary-big-endian-float32|calculation=float64|unit=source-unspecified",
                $"Reference|path={referencePath}|bytes={new FileInfo(referencePath).Length}|sha256={Sha256(referencePath)}|generatorVersion={reference.Provenance.GeneratorVersion}",
                $"Source|conf={confPath}|bytes={new FileInfo(confPath).Length}|sha256={Sha256(confPath)}|archiveSha256={reference.Provenance.ArchiveSha256}",
                $"Synthetic|{(synthetic.Passed == synthetic.Total ? "Pass" : "Fail")}|cases={synthetic.Total}|passed={synthetic.Passed}",
                $"PointParity|{(pointParityPassed ? "Pass" : "Fail")}|checks={pointChecks}|maxCoordinateDelta={Format(maximumPointDelta)}|tolerance={Format(PointTolerance)}",
                $"ScanStatisticParity|{(scanStatisticParityPassed ? "Pass" : "Fail")}|maxComponentDelta={Format(maximumScanStatisticDelta)}|componentTolerance={Format(PointTolerance)}|sumTolerance={Format(SumTolerance)}|weightedSumTolerance={Format(WeightedSumTolerance)}",
                $"AggregateParity|{(aggregateParityPassed ? "Pass" : "Fail")}|points={aggregate.Count}|maxComponentDelta={Format(maximumAggregateDelta)}|minimum={Format(aggregate.Minimum)}|maximum={Format(aggregate.Maximum)}|centroid={Format(aggregate.Centroid)}"
            ]);
            lines.AddRange(failures.Select(failure => $"Failure|{failure}"));
            WriteReport(reportPath, lines);
            Console.WriteLine($"Stanford transform parity: {status} ({configuration.Scans.Count} scans, {aggregate.Count} points)");
            return failures.Count == 0 ? 0 : 5;
        }
        catch (Exception exception)
        {
            lines.Insert(0, $"StanfordTransformParity|Fail|cause={Clean(exception.Message)}");
            WriteReport(reportPath, lines);
            Console.Error.WriteLine($"Stanford transform parity failed: {exception.Message}");
            return 5;
        }
    }

    private static Configuration ReadConfiguration(string path)
    {
        double[]? camera = null;
        var scans = new List<ConfiguredScan>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        var lines = File.ReadAllLines(path, Encoding.ASCII);
        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index].Trim();
            if (line.Length == 0)
            {
                continue;
            }

            var fields = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (fields[0] == "camera")
            {
                if (camera is not null || fields.Length != 8)
                {
                    throw new InvalidDataException($"Invalid camera declaration at line {index + 1}.");
                }

                camera = fields[1..].Select(value => ParseFinite(value, $"camera line {index + 1}")).ToArray();
                continue;
            }

            if (fields[0] != "bmesh" || fields.Length != 9)
            {
                throw new InvalidDataException($"Unsupported configuration line {index + 1}: {line}");
            }

            var fileName = fields[1];
            if (Path.GetFileName(fileName) != fileName
                || !fileName.EndsWith(".ply", StringComparison.Ordinal)
                || !names.Add(fileName))
            {
                throw new InvalidDataException($"Invalid or duplicate scan name at line {index + 1}: {fileName}");
            }

            var values = fields[2..].Select(value => ParseFinite(value, $"bmesh line {index + 1}")).ToArray();
            scans.Add(new ConfiguredScan(fileName, values[..3], values[3..]));
        }

        return new Configuration(
            camera ?? throw new InvalidDataException("Configuration has no camera declaration."),
            scans.Count > 0 ? scans : throw new InvalidDataException("Configuration has no bmesh declarations."));
    }

    private static RangePly ReadRangePly(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var headerLength = FindHeaderLength(bytes);
        var header = Encoding.ASCII.GetString(bytes, 0, headerLength).Replace("\r\n", "\n", StringComparison.Ordinal);
        var lines = header.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 3 || lines[0] != "ply" || lines[1] != "format binary_big_endian 1.0")
        {
            throw new InvalidDataException($"Only binary big-endian PLY 1.0 is supported: {Path.GetFileName(path)}");
        }

        var elements = new Dictionary<string, int>(StringComparer.Ordinal);
        var properties = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var objectInfo = new Dictionary<string, string>(StringComparer.Ordinal);
        var currentElement = string.Empty;
        foreach (var line in lines.Skip(2))
        {
            if (line == "end_header")
            {
                break;
            }

            var fields = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (fields[0] == "element" && fields.Length == 3)
            {
                currentElement = fields[1];
                if (!elements.TryAdd(currentElement, int.Parse(fields[2], CultureInfo.InvariantCulture)))
                {
                    throw new InvalidDataException($"Duplicate PLY element: {currentElement}");
                }

                properties[currentElement] = [];
            }
            else if (fields[0] == "property" && currentElement.Length > 0)
            {
                properties[currentElement].Add(line);
            }
            else if (fields[0] == "obj_info" && fields.Length >= 3)
            {
                objectInfo[fields[1]] = string.Join(' ', fields[2..]);
            }
            else if (fields[0] != "comment")
            {
                throw new InvalidDataException($"Unsupported PLY header line: {line}");
            }
        }

        if (elements.Count != 2
            || !elements.TryGetValue("vertex", out var vertexCount)
            || !elements.TryGetValue("range_grid", out var rangeGridCount)
            || !properties["vertex"].SequenceEqual(["property float x", "property float y", "property float z"], StringComparer.Ordinal)
            || !properties["range_grid"].SequenceEqual(["property list uchar int vertex_indices"], StringComparer.Ordinal))
        {
            throw new InvalidDataException($"PLY element contract differs: {Path.GetFileName(path)}");
        }

        var columns = int.Parse(objectInfo.GetValueOrDefault("num_cols", "-1"), CultureInfo.InvariantCulture);
        var rows = int.Parse(objectInfo.GetValueOrDefault("num_rows", "-1"), CultureInfo.InvariantCulture);
        if (vertexCount <= 0 || columns <= 0 || rows <= 0 || rangeGridCount != checked(columns * rows))
        {
            throw new InvalidDataException($"Invalid PLY dimensions: {Path.GetFileName(path)}");
        }

        var payload = bytes.AsSpan(headerLength);
        var vertexByteLength = checked(vertexCount * 12);
        if (payload.Length < vertexByteLength)
        {
            throw new InvalidDataException($"PLY vertex payload is truncated: {Path.GetFileName(path)}");
        }

        var points = new Point3[vertexCount];
        for (var index = 0; index < points.Length; index++)
        {
            var offset = index * 12;
            points[index] = new Point3(
                ReadBigEndianFloat(payload.Slice(offset, 4)),
                ReadBigEndianFloat(payload.Slice(offset + 4, 4)),
                ReadBigEndianFloat(payload.Slice(offset + 8, 4)));
            if (!points[index].IsFinite)
            {
                throw new InvalidDataException($"PLY contains non-finite vertices: {Path.GetFileName(path)}");
            }
        }

        var payloadOffset = vertexByteLength;
        var referenced = new bool[vertexCount];
        var referencedCount = 0;
        for (var cell = 0; cell < rangeGridCount; cell++)
        {
            if (payloadOffset >= payload.Length)
            {
                throw new InvalidDataException($"PLY range-grid payload is truncated: {Path.GetFileName(path)}");
            }

            var count = payload[payloadOffset++];
            if (count > 1)
            {
                throw new InvalidDataException($"PLY range-grid cell contains multiple indices: {Path.GetFileName(path)}");
            }

            if (count == 0)
            {
                continue;
            }

            if (payloadOffset + 4 > payload.Length)
            {
                throw new InvalidDataException($"PLY range-grid index is truncated: {Path.GetFileName(path)}");
            }

            var pointIndex = BinaryPrimitives.ReadInt32BigEndian(payload.Slice(payloadOffset, 4));
            payloadOffset += 4;
            if (pointIndex < 0 || pointIndex >= vertexCount || referenced[pointIndex])
            {
                throw new InvalidDataException($"PLY range-grid index is invalid or duplicated: {Path.GetFileName(path)}");
            }

            referenced[pointIndex] = true;
            referencedCount++;
        }

        if (payloadOffset != payload.Length || referencedCount != vertexCount)
        {
            throw new InvalidDataException($"PLY range-grid length or coverage differs: {Path.GetFileName(path)}");
        }

        return new RangePly(
            points,
            rangeGridCount,
            columns,
            rows,
            bytes.LongLength,
            Convert.ToHexString(SHA256.HashData(bytes)));
    }

    private static int FindHeaderLength(byte[] bytes)
    {
        var marker = "end_header\n"u8;
        var index = bytes.AsSpan().IndexOf(marker);
        if (index >= 0)
        {
            return index + marker.Length;
        }

        marker = "end_header\r\n"u8;
        index = bytes.AsSpan().IndexOf(marker);
        if (index >= 0)
        {
            return index + marker.Length;
        }

        throw new InvalidDataException("PLY header has no end_header marker.");
    }

    private static float ReadBigEndianFloat(ReadOnlySpan<byte> bytes) =>
        BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32BigEndian(bytes));

    private static double CompareStatistics(
        StatisticsReference expected,
        Statistics actual,
        string context,
        List<string> failures)
    {
        Require(expected.PointCount == actual.Count, $"{context} point count differs.", failures);
        var maximum = 0.0;
        maximum = Math.Max(maximum, CompareVector(expected.Minimum, actual.Minimum, PointTolerance, $"{context} minimum", failures));
        maximum = Math.Max(maximum, CompareVector(expected.Maximum, actual.Maximum, PointTolerance, $"{context} maximum", failures));
        maximum = Math.Max(maximum, CompareVector(expected.Centroid, actual.Centroid, PointTolerance, $"{context} centroid", failures));
        maximum = Math.Max(maximum, CompareVector(expected.Sum, actual.Sum, SumTolerance, $"{context} sum", failures));
        maximum = Math.Max(maximum, CompareVector(expected.SumSquares, actual.SumSquares, SumTolerance, $"{context} sum squares", failures));
        maximum = Math.Max(maximum, CompareVector(expected.OrderedWeightedSum, actual.OrderedWeightedSum, WeightedSumTolerance, $"{context} ordered weighted sum", failures));
        return maximum;
    }

    private static double ComparePoint(
        double[] expected,
        Point3 actual,
        double tolerance,
        string context,
        List<string> failures) =>
        CompareVector(expected, actual.ToArray(), tolerance, context, failures);

    private static double CompareVector(
        IReadOnlyList<double> expected,
        IReadOnlyList<double> actual,
        double tolerance,
        string context,
        List<string> failures)
    {
        if (expected.Count != actual.Count)
        {
            failures.Add($"{context} component count differs.");
            return double.PositiveInfinity;
        }

        var maximum = 0.0;
        for (var index = 0; index < expected.Count; index++)
        {
            var delta = Math.Abs(expected[index] - actual[index]);
            maximum = Math.Max(maximum, delta);
            if (!double.IsFinite(delta) || delta > tolerance)
            {
                failures.Add($"{context}[{index}] delta {Format(delta)} exceeds {Format(tolerance)}.");
            }
        }

        return maximum;
    }

    private static void CompareScalar(
        double expected,
        double actual,
        double tolerance,
        string context,
        List<string> failures)
    {
        var delta = Math.Abs(expected - actual);
        if (!double.IsFinite(delta) || delta > tolerance)
        {
            failures.Add($"{context} delta {Format(delta)} exceeds {Format(tolerance)}.");
        }
    }

    private static (int Passed, int Total) RunSyntheticChecks()
    {
        var passed = 0;
        var identity = RotationMatrix.Create([0, 0, 0, 1]);
        if (identity.Transform(new Point3(1, 2, 3), [4, 5, 6]).Equals(new Point3(5, 7, 9)))
        {
            passed++;
        }

        var half = Math.Sqrt(0.5);
        var quarterTurn = RotationMatrix.Create([0, 0, half, half]);
        var transformed = quarterTurn.Transform(new Point3(1, 0, 0), [1, 2, 3]);
        if (transformed.MaximumDelta(new Point3(1, 1, 3)) < 1e-15)
        {
            passed++;
        }

        var scaled = RotationMatrix.Create([0, 0, half * 3, half * 3]);
        if (scaled.MaximumDelta(quarterTurn) < 1e-15)
        {
            passed++;
        }

        try
        {
            RotationMatrix.Create([0, 0, 0, 0]);
        }
        catch (InvalidDataException)
        {
            passed++;
        }

        return (passed, 4);
    }

    private static void ValidateReference(ReferenceDocument reference)
    {
        if (reference.Contract is null
            || reference.Provenance is null
            || reference.Camera is null
            || reference.AggregateTransformedStatistics is null
            || reference.Scans is null
            || reference.Scans.Any(scan => scan.Checkpoints is null
                || scan.SourceStatistics is null
                || scan.TransformedStatistics is null))
        {
            throw new InvalidDataException("Transform reference JSON is incomplete.");
        }
    }

    private static double ParseFinite(string value, string context)
    {
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            || !double.IsFinite(parsed))
        {
            throw new InvalidDataException($"{context} is not a finite number: {value}");
        }

        return parsed;
    }

    private static void Require(bool condition, string message, List<string> failures)
    {
        if (!condition)
        {
            failures.Add(message);
        }
    }

    private static string Sha256(string path) =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    private static string Format(double value) =>
        value.ToString("G17", CultureInfo.InvariantCulture);

    private static string Format(IReadOnlyList<double> values) =>
        $"({string.Join(',', values.Select(Format))})";

    private static string Clean(string value) =>
        value.Replace('|', '/').Replace('\r', ' ').Replace('\n', ' ');

    private static void WriteReport(string path, IReadOnlyList<string> lines)
    {
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllLines(fullPath, lines, new UTF8Encoding(false));
    }

    private sealed record Configuration(double[] Camera, List<ConfiguredScan> Scans);

    private sealed record ConfiguredScan(string FileName, double[] Translation, double[] QuaternionXyzw)
    {
        public double QuaternionNorm => Math.Sqrt(QuaternionXyzw.Sum(value => value * value));

        public double RotationAngleDegrees =>
            2.0 * Math.Acos(Math.Min(1.0, Math.Abs(QuaternionXyzw[3]) / QuaternionNorm)) * 180.0 / Math.PI;
    }

    private sealed record RangePly(
        Point3[] Points,
        int RangeGridCount,
        int Columns,
        int Rows,
        long ByteLength,
        string Sha256);

    private readonly record struct Point3(double X, double Y, double Z)
    {
        public bool IsFinite => double.IsFinite(X) && double.IsFinite(Y) && double.IsFinite(Z);
        public double[] ToArray() => [X, Y, Z];
        public double MaximumDelta(Point3 other) => Math.Max(Math.Abs(X - other.X), Math.Max(Math.Abs(Y - other.Y), Math.Abs(Z - other.Z)));
    }

    private readonly record struct RotationMatrix(
        double M00,
        double M01,
        double M02,
        double M10,
        double M11,
        double M12,
        double M20,
        double M21,
        double M22)
    {
        public static RotationMatrix Create(IReadOnlyList<double> quaternion)
        {
            if (quaternion.Count != 4)
            {
                throw new InvalidDataException("Quaternion must contain x,y,z,w.");
            }

            var x = quaternion[0];
            var y = quaternion[1];
            var z = quaternion[2];
            var w = quaternion[3];
            var normSquared = x * x + y * y + z * z + w * w;
            if (!double.IsFinite(normSquared) || normSquared <= 1e-24)
            {
                throw new InvalidDataException("Quaternion norm is zero or non-finite.");
            }

            var scale = 2.0 / normSquared;
            var xs = x * scale;
            var ys = y * scale;
            var zs = z * scale;
            var wx = w * xs;
            var wy = w * ys;
            var wz = w * zs;
            var xx = x * xs;
            var xy = x * ys;
            var xz = x * zs;
            var yy = y * ys;
            var yz = y * zs;
            var zz = z * zs;

            return new RotationMatrix(
                1.0 - (yy + zz), xy + wz, xz - wy,
                xy - wz, 1.0 - (xx + zz), yz + wx,
                xz + wy, yz - wx, 1.0 - (xx + yy));
        }

        public Point3 Transform(Point3 point, IReadOnlyList<double> translation) =>
            new(
                M00 * point.X + M01 * point.Y + M02 * point.Z + translation[0],
                M10 * point.X + M11 * point.Y + M12 * point.Z + translation[1],
                M20 * point.X + M21 * point.Y + M22 * point.Z + translation[2]);

        public double Determinant =>
            M00 * (M11 * M22 - M12 * M21)
            - M01 * (M10 * M22 - M12 * M20)
            + M02 * (M10 * M21 - M11 * M20);

        public double OrthogonalityMaxError
        {
            get
            {
                var rows = new[] { new Point3(M00, M01, M02), new Point3(M10, M11, M12), new Point3(M20, M21, M22) };
                var maximum = 0.0;
                for (var row = 0; row < 3; row++)
                {
                    for (var column = 0; column < 3; column++)
                    {
                        var dot = rows[row].X * rows[column].X + rows[row].Y * rows[column].Y + rows[row].Z * rows[column].Z;
                        maximum = Math.Max(maximum, Math.Abs(dot - (row == column ? 1.0 : 0.0)));
                    }
                }

                return maximum;
            }
        }

        public double MaximumDelta(RotationMatrix other) =>
            new[]
            {
                Math.Abs(M00 - other.M00), Math.Abs(M01 - other.M01), Math.Abs(M02 - other.M02),
                Math.Abs(M10 - other.M10), Math.Abs(M11 - other.M11), Math.Abs(M12 - other.M12),
                Math.Abs(M20 - other.M20), Math.Abs(M21 - other.M21), Math.Abs(M22 - other.M22)
            }.Max();
    }

    private sealed class Statistics
    {
        private readonly double[] minimum = [double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity];
        private readonly double[] maximum = [double.NegativeInfinity, double.NegativeInfinity, double.NegativeInfinity];
        private readonly double[] sum = [0, 0, 0];
        private readonly double[] sumSquares = [0, 0, 0];
        private readonly double[] orderedWeightedSum = [0, 0, 0];

        public long Count { get; private set; }
        public double[] Minimum => minimum;
        public double[] Maximum => maximum;
        public double[] Sum => sum;
        public double[] SumSquares => sumSquares;
        public double[] OrderedWeightedSum => orderedWeightedSum;
        public double[] Centroid => sum.Select(value => value / Count).ToArray();

        public void Add(Point3 point, long weight)
        {
            Count++;
            var values = point.ToArray();
            for (var axis = 0; axis < 3; axis++)
            {
                var value = values[axis];
                minimum[axis] = Math.Min(minimum[axis], value);
                maximum[axis] = Math.Max(maximum[axis], value);
                sum[axis] += value;
                sumSquares[axis] += value * value;
                orderedWeightedSum[axis] += weight * value;
            }
        }
    }

    private sealed class ReferenceDocument
    {
        public string SchemaVersion { get; init; } = "";
        public ReferenceContract Contract { get; init; } = null!;
        public ReferenceProvenance Provenance { get; init; } = null!;
        public double[] Camera { get; init; } = null!;
        public int ScanCount { get; init; }
        public int PointReferenceCount { get; init; }
        public StatisticsReference AggregateTransformedStatistics { get; init; } = null!;
        public List<ScanReference> Scans { get; init; } = null!;
    }

    private sealed class ReferenceContract
    {
        public string QuaternionOrder { get; init; } = "";
        public string Application { get; init; } = "";
        public bool CameraApplied { get; init; }
    }

    private sealed class ReferenceProvenance
    {
        public string ArchiveSha256 { get; init; } = "";
        public long ConfigurationByteLength { get; init; }
        public string ConfigurationSha256 { get; init; } = "";
        public string GeneratorVersion { get; init; } = "";
    }

    private sealed class ScanReference
    {
        public string FileName { get; init; } = "";
        public long ByteLength { get; init; }
        public string Sha256 { get; init; } = "";
        public int VertexCount { get; init; }
        public int RangeGridCount { get; init; }
        public int Columns { get; init; }
        public int Rows { get; init; }
        public double[] Translation { get; init; } = [];
        public double[] QuaternionXyzw { get; init; } = [];
        public double QuaternionNorm { get; init; }
        public double RotationAngleDegrees { get; init; }
        public double RotationDeterminant { get; init; }
        public double RotationOrthogonalityMaxError { get; init; }
        public StatisticsReference SourceStatistics { get; init; } = null!;
        public StatisticsReference TransformedStatistics { get; init; } = null!;
        public List<CheckpointReference> Checkpoints { get; init; } = null!;
    }

    private sealed class CheckpointReference
    {
        public int Index { get; init; }
        public double[] Source { get; init; } = [];
        public double[] Transformed { get; init; } = [];
    }

    private sealed class StatisticsReference
    {
        public long PointCount { get; init; }
        public double[] Minimum { get; init; } = [];
        public double[] Maximum { get; init; } = [];
        public double[] Centroid { get; init; } = [];
        public double[] Sum { get; init; } = [];
        public double[] SumSquares { get; init; } = [];
        public double[] OrderedWeightedSum { get; init; } = [];
    }
}
