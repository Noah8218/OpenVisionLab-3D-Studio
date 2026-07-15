using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

internal static class MeshDeviationParityVerification
{
    private const string UnsignedScalarName = "scalar_C2M_absolute_distances";
    private const string SignedScalarName = "scalar_C2M_signed_distances";
    private const double PointTolerance = 1e-5;
    private const double AggregateTolerance = 1e-6;

    public static int Run(
        string nominalStlPath,
        string measuredPlyPath,
        string cloudCompareUnsignedPlyPath,
        string cloudCompareSignedPlyPath,
        string unit,
        string reportPath,
        int? maxPoints)
    {
        if (maxPoints is <= 0)
        {
            throw new InvalidDataException("--max-points must be a positive integer.");
        }

        var totalStopwatch = Stopwatch.StartNew();
        var triangles = new List<MeshTriangle>();
        var nominalSummary = BinaryStlInspectionReader.Scan(
            nominalStlPath,
            (index, triangle) => triangles.Add(new MeshTriangle(index, triangle.A, triangle.B, triangle.C)));
        var indexStopwatch = Stopwatch.StartNew();
        var distanceIndex = new TriangleMeshDistanceIndex(triangles);
        indexStopwatch.Stop();

        using var measured = new BinaryPlyVertexReader(measuredPlyPath);
        using var cloudUnsigned = new BinaryPlyVertexReader(cloudCompareUnsignedPlyPath);
        using var cloudSigned = new BinaryPlyVertexReader(cloudCompareSignedPlyPath);
        ValidatePlyContracts(measured, cloudUnsigned, cloudSigned);

        var unsignedScalarIndex = cloudUnsigned.GetPropertyIndex(UnsignedScalarName);
        var signedScalarIndex = cloudSigned.GetPropertyIndex(SignedScalarName);
        var requestedPointCount = maxPoints is { } limit
            ? Math.Min(measured.VertexCount, limit)
            : measured.VertexCount;
        var fullScope = requestedPointCount == measured.VertexCount;
        var openUnsignedStats = new RunningStatistics();
        var cloudUnsignedStats = new RunningStatistics();
        var openSignedResolvedStats = new RunningStatistics();
        var cloudSignedResolvedStats = new RunningStatistics();
        var unsignedDifferences = new DifferenceStatistics();
        var signedDifferences = new DifferenceStatistics();
        var thresholds = new UnsignedThresholdCounts();
        long coordinateMismatchCount = 0;
        long directSignedCount = 0;
        long resolvedSignedCount = 0;
        long robustRecoveredCount = 0;
        long directUnresolvedEdgeCount = 0;
        long directUnresolvedVertexCount = 0;
        long finalUnresolvedCount = 0;
        long signedSignMismatchCount = 0;
        long robustRecoveredSignMismatchCount = 0;
        long directUnresolvedCloudPositiveCount = 0;
        long directUnresolvedCloudNegativeCount = 0;
        long directUnresolvedCloudZeroCount = 0;
        long processedPointCount = 0;
        var signedMismatchExamples = new List<string>();

        var calculationStopwatch = Stopwatch.StartNew();
        while (processedPointCount < requestedPointCount)
        {
            var chunkLimit = (int)Math.Min(requestedPointCount - processedPointCount, int.MaxValue);
            var measuredChunkCount = measured.ReadChunk(chunkLimit);
            var unsignedChunkCount = cloudUnsigned.ReadChunk(chunkLimit);
            var signedChunkCount = cloudSigned.ReadChunk(chunkLimit);
            if (measuredChunkCount == 0
                || unsignedChunkCount != measuredChunkCount
                || signedChunkCount != measuredChunkCount)
            {
                throw new InvalidDataException("PLY parity streams ended at different vertex positions.");
            }

            for (var chunkIndex = 0; chunkIndex < measuredChunkCount; chunkIndex++)
            {
                if (!measured.CoordinatesEqual(cloudUnsigned, chunkIndex)
                    || !measured.CoordinatesEqual(cloudSigned, chunkIndex))
                {
                    coordinateMismatchCount++;
                }

                var point = measured.GetPosition(chunkIndex);
                var cloudUnsignedDistance = cloudUnsigned.GetSingle(chunkIndex, unsignedScalarIndex);
                var cloudSignedDistance = cloudSigned.GetSingle(chunkIndex, signedScalarIndex);
                if (!float.IsFinite(cloudUnsignedDistance)
                    || cloudUnsignedDistance < 0
                    || !float.IsFinite(cloudSignedDistance))
                {
                    throw new InvalidDataException(
                        $"CloudCompare scalar is invalid at ordered vertex {processedPointCount + chunkIndex}.");
                }

                var result = distanceIndex.FindClosest(point);
                openUnsignedStats.Add(result.UnsignedDistance);
                cloudUnsignedStats.Add(cloudUnsignedDistance);
                unsignedDifferences.Add(result.UnsignedDistance, cloudUnsignedDistance);
                thresholds.Add(result.UnsignedDistance);

                var signedResult = result;
                var recoveredByRobustSelection = false;
                if (result.SignResolved)
                {
                    directSignedCount++;
                }
                else
                {
                    if (result.ClosestFeature == MeshClosestFeature.Edge)
                    {
                        directUnresolvedEdgeCount++;
                    }
                    else
                    {
                        directUnresolvedVertexCount++;
                    }

                    if (cloudSignedDistance > 0)
                    {
                        directUnresolvedCloudPositiveCount++;
                    }
                    else if (cloudSignedDistance < 0)
                    {
                        directUnresolvedCloudNegativeCount++;
                    }
                    else
                    {
                        directUnresolvedCloudZeroCount++;
                    }

                    signedResult = distanceIndex.ResolveRobustSign(point, result.UnsignedDistance);
                    recoveredByRobustSelection = true;
                }

                if (signedResult.SignResolved && signedResult.SignedDistance is { } openSignedDistance)
                {
                    resolvedSignedCount++;
                    if (recoveredByRobustSelection)
                    {
                        robustRecoveredCount++;
                    }

                    openSignedResolvedStats.Add(openSignedDistance);
                    cloudSignedResolvedStats.Add(cloudSignedDistance);
                    signedDifferences.Add(openSignedDistance, cloudSignedDistance);
                    if (Math.Sign(openSignedDistance) != Math.Sign(cloudSignedDistance))
                    {
                        signedSignMismatchCount++;
                        if (recoveredByRobustSelection)
                        {
                            robustRecoveredSignMismatchCount++;
                        }

                        if (signedMismatchExamples.Count < 8)
                        {
                            signedMismatchExamples.Add(
                                $"SignedMismatch|index={processedPointCount + chunkIndex}|point={Format(point.X)},{Format(point.Y)},{Format(point.Z)}|triangle={signedResult.SourceTriangleIndex}|feature={signedResult.ClosestFeature}|open={Format(openSignedDistance)}|cloud={Format(cloudSignedDistance)}");
                        }
                    }
                }
                else
                {
                    finalUnresolvedCount++;
                }
            }

            processedPointCount += measuredChunkCount;
            if (processedPointCount == requestedPointCount || processedPointCount % 524_288 == 0)
            {
                Console.WriteLine(
                    $"Mesh deviation parity: {processedPointCount:N0}/{requestedPointCount:N0} points");
            }
        }

        calculationStopwatch.Stop();
        if (fullScope && (!measured.IsComplete || !cloudUnsigned.IsComplete || !cloudSigned.IsComplete))
        {
            throw new InvalidDataException("A full-scope PLY parity stream did not consume every vertex.");
        }

        var directUnresolvedCount = directUnresolvedEdgeCount + directUnresolvedVertexCount;
        var unsignedPass = coordinateMismatchCount == 0
            && unsignedDifferences.MaximumAbsoluteDifference <= PointTolerance
            && Math.Abs(openUnsignedStats.Mean - cloudUnsignedStats.Mean) <= AggregateTolerance
            && Math.Abs(openUnsignedStats.StandardDeviationPopulation - cloudUnsignedStats.StandardDeviationPopulation) <= AggregateTolerance;
        var signedRobustPass = resolvedSignedCount > 0
            && signedDifferences.MaximumAbsoluteDifference <= PointTolerance
            && signedSignMismatchCount == 0;
        var status = !unsignedPass || !signedRobustPass
            ? "Fail"
            : !fullScope || finalUnresolvedCount > 0
                ? "Partial"
                : "Pass";

        var hashStopwatch = Stopwatch.StartNew();
        var measuredHash = ComputeSha256(measured.SourcePath);
        var unsignedHash = ComputeSha256(cloudUnsigned.SourcePath);
        var signedHash = ComputeSha256(cloudSigned.SourcePath);
        hashStopwatch.Stop();
        totalStopwatch.Stop();
        using var currentProcess = Process.GetCurrentProcess();
        currentProcess.Refresh();

        var lines = new List<string>
        {
            $"MeshDeviationParity|{status}|scope={(fullScope ? "full" : "prefix")}|processed={processedPointCount}|available={measured.VertexCount}|unit={unit}",
            $"Frame|transform=identity|alignment=none|coordinates=NIST-part-frame|inspectionSource=original-nominal-stl|querySet=CloudCompare-validation-derivative",
            $"Nominal|path={nominalSummary.SourcePath}|bytes={nominalSummary.SourceByteLength}|sha256={nominalSummary.SourceSha256}|triangles={nominalSummary.ProcessedTriangleCount}|bvhTriangles={distanceIndex.TriangleCount}",
            $"MeasuredQuery|path={measured.SourcePath}|sha256={measuredHash}|properties={string.Join(',', measured.Properties)}|vertices={measured.VertexCount}",
            $"CloudUnsigned|path={cloudUnsigned.SourcePath}|sha256={unsignedHash}|scalar={UnsignedScalarName}",
            $"CloudSigned|path={cloudSigned.SourcePath}|sha256={signedHash}|scalar={SignedScalarName}|mode=robust",
            $"Coordinates|mismatched={coordinateMismatchCount}|compared={processedPointCount}|rawFloatBits=True",
            $"UnsignedParity|{(unsignedPass ? "Pass" : "Fail")}|pointTolerance={Format(PointTolerance)}|aggregateTolerance={Format(AggregateTolerance)}|{unsignedDifferences.Format()}",
            $"UnsignedOpenVision|{openUnsignedStats.Format()}|{thresholds.Format()}",
            $"UnsignedCloudCompare|{cloudUnsignedStats.Format()}|meanDelta={Format(openUnsignedStats.Mean - cloudUnsignedStats.Mean)}|stdDelta={Format(openUnsignedStats.StandardDeviationPopulation - cloudUnsignedStats.StandardDeviationPopulation)}",
            $"SignedDirectCoverage|resolved={directSignedCount}|unresolved={directUnresolvedCount}|edge={directUnresolvedEdgeCount}|vertex={directUnresolvedVertexCount}|coverage={FormatRatio(directSignedCount, processedPointCount)}",
            $"SignedRobustParity|{(signedRobustPass ? "Pass" : "Fail")}|resolved={resolvedSignedCount}|recovered={robustRecoveredCount}|signMismatches={signedSignMismatchCount}|recoveredSignMismatches={robustRecoveredSignMismatchCount}|pointTolerance={Format(PointTolerance)}|selectionEpsilon={Format(TriangleMeshDistanceIndex.RobustSignDistanceEpsilon)}|{signedDifferences.Format()}",
            $"SignedOpenVisionResolved|{openSignedResolvedStats.Format()}",
            $"SignedCloudCompareResolved|{cloudSignedResolvedStats.Format()}",
            $"SignedCoverage|complete={(finalUnresolvedCount == 0)}|resolved={resolvedSignedCount}|unresolved={finalUnresolvedCount}|coverage={FormatRatio(resolvedSignedCount, processedPointCount)}",
            $"SignedCloudCompareDirectUnresolved|positive={directUnresolvedCloudPositiveCount}|negative={directUnresolvedCloudNegativeCount}|zero={directUnresolvedCloudZeroCount}",
            $"Performance|indexMs={Format(indexStopwatch.Elapsed.TotalMilliseconds)}|calculationMs={Format(calculationStopwatch.Elapsed.TotalMilliseconds)}|hashMs={Format(hashStopwatch.Elapsed.TotalMilliseconds)}|totalMs={Format(totalStopwatch.Elapsed.TotalMilliseconds)}|peakWorkingSetBytes={currentProcess.PeakWorkingSet64}",
            $"Decision|unsigned={PassFail(unsignedPass)}|signedRobust={PassFail(signedRobustPass)}|signedCoverage={(finalUnresolvedCount == 0 ? "Pass" : "Open")}|overall={status}"
        };
        lines.AddRange(signedMismatchExamples);

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        File.WriteAllLines(reportPath, lines);
        Console.WriteLine(
            $"Mesh deviation parity: {status} (unsigned={PassFail(unsignedPass)}, signed-robust={PassFail(signedRobustPass)}, unresolved={finalUnresolvedCount:N0})");
        return status == "Pass" ? 0 : 5;
    }

    private static void ValidatePlyContracts(
        BinaryPlyVertexReader measured,
        BinaryPlyVertexReader cloudUnsigned,
        BinaryPlyVertexReader cloudSigned)
    {
        if (measured.VertexCount != cloudUnsigned.VertexCount
            || measured.VertexCount != cloudSigned.VertexCount)
        {
            throw new InvalidDataException("PLY parity streams declare different vertex counts.");
        }

        if (!measured.Properties.SequenceEqual(["x", "y", "z"], StringComparer.Ordinal)
            || !cloudUnsigned.Properties.SequenceEqual(["x", "y", "z", UnsignedScalarName], StringComparer.Ordinal)
            || !cloudSigned.Properties.SequenceEqual(["x", "y", "z", SignedScalarName], StringComparer.Ordinal))
        {
            throw new InvalidDataException("PLY parity property contracts do not match the expected CloudCompare outputs.");
        }
    }

    private static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static string PassFail(bool value) => value ? "Pass" : "Fail";

    private static string Format(double value) => value.ToString("G17", CultureInfo.InvariantCulture);

    private static string FormatRatio(long numerator, long denominator) =>
        denominator == 0
            ? "0"
            : ((double)numerator / denominator).ToString("P9", CultureInfo.InvariantCulture);

    private sealed class RunningStatistics
    {
        private double _mean;
        private double _sumSquaredDeviation;
        private double _sumSquares;

        public long Count { get; private set; }

        public double Minimum { get; private set; } = double.PositiveInfinity;

        public double Maximum { get; private set; } = double.NegativeInfinity;

        public double Mean => Count == 0 ? double.NaN : _mean;

        public double StandardDeviationPopulation =>
            Count == 0 ? double.NaN : Math.Sqrt(Math.Max(0.0, _sumSquaredDeviation / Count));

        public double RootMeanSquare => Count == 0 ? double.NaN : Math.Sqrt(_sumSquares / Count);

        public void Add(double value)
        {
            if (!double.IsFinite(value))
            {
                throw new InvalidDataException("A mesh-deviation calculation produced a non-finite value.");
            }

            Count++;
            Minimum = Math.Min(Minimum, value);
            Maximum = Math.Max(Maximum, value);
            var delta = value - _mean;
            _mean += delta / Count;
            _sumSquaredDeviation += delta * (value - _mean);
            _sumSquares += value * value;
        }

        public string Format() =>
            $"count={Count}|min={MeshDeviationParityVerification.Format(Minimum)}|max={MeshDeviationParityVerification.Format(Maximum)}|mean={MeshDeviationParityVerification.Format(Mean)}|stdPopulation={MeshDeviationParityVerification.Format(StandardDeviationPopulation)}|rms={MeshDeviationParityVerification.Format(RootMeanSquare)}";
    }

    private sealed class DifferenceStatistics
    {
        private double _sumAbsoluteDifference;
        private double _sumSquaredDifference;

        public long Count { get; private set; }

        public double MaximumAbsoluteDifference { get; private set; }

        public long Above1E7 { get; private set; }

        public long Above1E6 { get; private set; }

        public long Above1E5 { get; private set; }

        public void Add(double openVisionValue, double cloudCompareValue)
        {
            var difference = Math.Abs(openVisionValue - cloudCompareValue);
            Count++;
            MaximumAbsoluteDifference = Math.Max(MaximumAbsoluteDifference, difference);
            _sumAbsoluteDifference += difference;
            _sumSquaredDifference += difference * difference;
            if (difference > 1e-7) Above1E7++;
            if (difference > 1e-6) Above1E6++;
            if (difference > 1e-5) Above1E5++;
        }

        public string Format() =>
            Count == 0
                ? "count=0|maxAbs=unavailable|meanAbs=unavailable|rms=unavailable|above1e-7=0|above1e-6=0|above1e-5=0"
                : $"count={Count}|maxAbs={MeshDeviationParityVerification.Format(MaximumAbsoluteDifference)}|meanAbs={MeshDeviationParityVerification.Format(_sumAbsoluteDifference / Count)}|rms={MeshDeviationParityVerification.Format(Math.Sqrt(_sumSquaredDifference / Count))}|above1e-7={Above1E7}|above1e-6={Above1E6}|above1e-5={Above1E5}";
    }

    private sealed class UnsignedThresholdCounts
    {
        private static readonly double[] Thresholds = [0.05, 0.1, 0.25, 0.5, 1.0];
        private readonly long[] _counts = new long[Thresholds.Length];
        private long _total;

        public void Add(double value)
        {
            _total++;
            for (var index = 0; index < Thresholds.Length; index++)
            {
                if (value <= Thresholds[index])
                {
                    _counts[index]++;
                }
            }
        }

        public string Format() => string.Join(
            '|',
            Thresholds.Select((threshold, index) =>
                $"le{threshold.ToString("G", CultureInfo.InvariantCulture)}={_counts[index]}/{_total}({FormatRatio(_counts[index], _total)})"));
    }
}
