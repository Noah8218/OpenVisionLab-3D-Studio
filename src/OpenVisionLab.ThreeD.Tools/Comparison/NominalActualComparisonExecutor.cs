using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;

namespace OpenVisionLab.ThreeD.Tools;

public sealed class NominalActualComparisonExecutor
{
    public Task<NominalActualComparisonResult> ExecuteAsync(
        NominalActualComparisonInput input,
        int maximumDisplaySamples,
        IProgress<NominalActualComparisonProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (maximumDisplaySamples < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumDisplaySamples));
        }

        return Task.Run(
            () => Execute(input, maximumDisplaySamples, progress, cancellationToken),
            cancellationToken);
    }

    private static NominalActualComparisonResult Execute(
        NominalActualComparisonInput input,
        int maximumDisplaySamples,
        IProgress<NominalActualComparisonProgress>? progress,
        CancellationToken cancellationToken)
    {
        var totalStopwatch = Stopwatch.StartNew();
        ValidateInput(input);
        ValidateFileMetadata(input.ActualSource);
        ValidateFileMetadata(input.NominalSource);
        ValidateFileMetadata(input.QuerySource);

        using var query = new BinaryPlyVertexReader(input.QuerySource.Path);
        if (!query.Properties.SequenceEqual(["x", "y", "z"], StringComparer.Ordinal))
        {
            throw new InvalidDataException(
                "The measured validation query must contain only ordered x,y,z float properties.");
        }

        Report(progress, "Validating source identities", 0, query.VertexCount, totalStopwatch.Elapsed);
        ValidateFileHash(input.ActualSource, cancellationToken);
        ValidateFileHash(input.QuerySource, cancellationToken);

        var triangles = new List<MeshTriangle>();
        var nominalSummary = BinaryStlInspectionReader.Scan(
            input.NominalSource.Path,
            (index, triangle) => triangles.Add(new MeshTriangle(index, triangle.A, triangle.B, triangle.C)));
        if (!nominalSummary.SourceSha256.Equals(input.NominalSource.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"Nominal source SHA-256 does not match the expected identity: {input.NominalSource.Id}");
        }

        cancellationToken.ThrowIfCancellationRequested();
        Report(progress, "Indexing nominal mesh", 0, query.VertexCount, totalStopwatch.Elapsed);
        var indexStopwatch = Stopwatch.StartNew();
        var distanceIndex = new TriangleMeshDistanceIndex(triangles);
        indexStopwatch.Stop();

        var unsignedStatistics = new RunningStatistics();
        var signedStatistics = new RunningStatistics();
        long belowToleranceCount = 0;
        long withinToleranceCount = 0;
        long aboveToleranceCount = 0;
        long directSignResolvedCount = 0;
        long robustSignRecoveredCount = 0;
        long unresolvedSignCount = 0;
        long processedPointCount = 0;
        var displayStride = CalculateDisplayStride(query.VertexCount, maximumDisplaySamples);
        var displaySamples = maximumDisplaySamples == 0
            ? []
            : new List<NominalActualDeviationSample>(Math.Min(maximumDisplaySamples, 65_536));

        var calculationStopwatch = Stopwatch.StartNew();
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var chunkCount = query.ReadChunk();
            if (chunkCount == 0)
            {
                break;
            }

            for (var chunkIndex = 0; chunkIndex < chunkCount; chunkIndex++)
            {
                if ((chunkIndex & 1023) == 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                var pointIndex = processedPointCount + chunkIndex;
                var point = query.GetPosition(chunkIndex);
                var closest = distanceIndex.FindClosest(point);
                unsignedStatistics.Add(closest.UnsignedDistance);

                var signed = closest;
                var robustSignRecovered = false;
                if (closest.SignResolved)
                {
                    directSignResolvedCount++;
                }
                else
                {
                    signed = distanceIndex.ResolveRobustSign(point, closest.UnsignedDistance);
                    if (signed.SignResolved)
                    {
                        robustSignRecoveredCount++;
                        robustSignRecovered = true;
                    }
                }

                if (signed.SignedDistance is not { } signedDistance || !signed.SignResolved)
                {
                    unresolvedSignCount++;
                    continue;
                }

                signedStatistics.Add(signedDistance);
                if (signedDistance < input.LowerTolerance)
                {
                    belowToleranceCount++;
                }
                else if (signedDistance > input.UpperTolerance)
                {
                    aboveToleranceCount++;
                }
                else
                {
                    withinToleranceCount++;
                }

                if (displayStride > 0
                    && pointIndex % displayStride == 0
                    && displaySamples.Count < maximumDisplaySamples)
                {
                    displaySamples.Add(new NominalActualDeviationSample(
                        pointIndex,
                        point,
                        closest.ClosestPoint,
                        closest.SourceTriangleIndex,
                        closest.UnsignedDistance,
                        signedDistance,
                        robustSignRecovered));
                }
            }

            processedPointCount += chunkCount;
            Report(
                progress,
                "Comparing actual to nominal",
                processedPointCount,
                query.VertexCount,
                totalStopwatch.Elapsed);
        }

        calculationStopwatch.Stop();
        if (!query.IsComplete || processedPointCount != query.VertexCount)
        {
            throw new InvalidDataException("The measured validation query was not consumed completely.");
        }

        if (unresolvedSignCount != 0)
        {
            throw new InvalidDataException(string.Create(
                CultureInfo.InvariantCulture,
                $"Signed deviation remained unresolved for {unresolvedSignCount:N0} of {processedPointCount:N0} points."));
        }

        totalStopwatch.Stop();
        var outOfToleranceCount = belowToleranceCount + aboveToleranceCount;
        var status = outOfToleranceCount == 0 ? ResultStatus.Pass : ResultStatus.Fail;
        var message = string.Create(
            CultureInfo.InvariantCulture,
            $"{status}: {outOfToleranceCount:N0} of {processedPointCount:N0} points outside [{input.LowerTolerance:G6}, {input.UpperTolerance:G6}] {input.Unit}.");

        return new NominalActualComparisonResult(
            input,
            status,
            message,
            processedPointCount,
            unsignedStatistics.ToContract(),
            signedStatistics.ToContract(),
            belowToleranceCount,
            withinToleranceCount,
            aboveToleranceCount,
            directSignResolvedCount,
            robustSignRecoveredCount,
            displayStride,
            displaySamples,
            indexStopwatch.Elapsed,
            calculationStopwatch.Elapsed,
            totalStopwatch.Elapsed);
    }

    private static void ValidateInput(NominalActualComparisonInput input)
    {
        RequireText(input.StepId, nameof(input.StepId));
        RequireText(input.Unit, nameof(input.Unit));
        RequireText(input.FrameId, nameof(input.FrameId));
        RequireText(input.AlignmentId, nameof(input.AlignmentId));
        ValidateIdentity(input.ActualSource, nameof(input.ActualSource));
        ValidateIdentity(input.NominalSource, nameof(input.NominalSource));
        ValidateIdentity(input.QuerySource, nameof(input.QuerySource));

        if (new[] { input.ActualSource.Id, input.NominalSource.Id, input.QuerySource.Id }
            .Distinct(StringComparer.Ordinal).Count() != 3)
        {
            throw new InvalidDataException("Actual, nominal, and validation query IDs must be distinct.");
        }

        if (new[]
            {
                Path.GetFullPath(input.ActualSource.Path),
                Path.GetFullPath(input.NominalSource.Path),
                Path.GetFullPath(input.QuerySource.Path)
            }.Distinct(StringComparer.OrdinalIgnoreCase).Count() != 3)
        {
            throw new InvalidDataException("Actual, nominal, and validation query paths must be distinct.");
        }

        if (!double.IsFinite(input.LowerTolerance)
            || !double.IsFinite(input.UpperTolerance)
            || input.LowerTolerance >= 0
            || input.UpperTolerance <= 0
            || input.LowerTolerance >= input.UpperTolerance)
        {
            throw new InvalidDataException(
                "Comparison tolerances must be finite, zero-centred, and ordered lower < 0 < upper.");
        }
    }

    private static void ValidateIdentity(NominalActualFileIdentity identity, string name)
    {
        ArgumentNullException.ThrowIfNull(identity);
        RequireText(identity.Id, $"{name}.{nameof(identity.Id)}");
        RequireText(identity.Name, $"{name}.{nameof(identity.Name)}");
        RequireText(identity.Path, $"{name}.{nameof(identity.Path)}");
        if (identity.ByteLength <= 0)
        {
            throw new InvalidDataException($"{name} byte length must be positive.");
        }

        if (identity.Sha256.Length != 64 || identity.Sha256.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new InvalidDataException($"{name} SHA-256 must contain 64 hexadecimal characters.");
        }
    }

    private static void ValidateFileMetadata(NominalActualFileIdentity identity)
    {
        var file = new FileInfo(identity.Path);
        if (!file.Exists)
        {
            throw new FileNotFoundException(
                $"Comparison source is missing: {identity.Id}",
                identity.Path);
        }

        if (file.Length != identity.ByteLength)
        {
            throw new InvalidDataException(
                $"Comparison source byte length does not match the expected identity: {identity.Id}");
        }
    }

    private static void ValidateFileHash(
        NominalActualFileIdentity identity,
        CancellationToken cancellationToken)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using var stream = new FileStream(
            identity.Path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            1024 * 1024,
            FileOptions.SequentialScan);
        var buffer = new byte[1024 * 1024];
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var count = stream.Read(buffer, 0, buffer.Length);
            if (count == 0)
            {
                break;
            }

            hash.AppendData(buffer, 0, count);
        }

        var actualHash = Convert.ToHexString(hash.GetHashAndReset());
        if (!actualHash.Equals(identity.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"Comparison source SHA-256 does not match the expected identity: {identity.Id}");
        }
    }

    private static int CalculateDisplayStride(long pointCount, int maximumDisplaySamples)
    {
        if (maximumDisplaySamples == 0)
        {
            return 0;
        }

        var stride = Math.Max(1, (pointCount + maximumDisplaySamples - 1) / maximumDisplaySamples);
        return (int)Math.Min(int.MaxValue, stride);
    }

    private static void Report(
        IProgress<NominalActualComparisonProgress>? progress,
        string stage,
        long processed,
        long total,
        TimeSpan elapsed) =>
        progress?.Report(new NominalActualComparisonProgress(stage, processed, total, elapsed));

    private static void RequireText(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidDataException($"{name} is required.");
        }
    }

    private sealed class RunningStatistics
    {
        private double mean;
        private double sumSquaredDeviation;
        private double sumSquares;

        public long Count { get; private set; }
        public double Minimum { get; private set; } = double.PositiveInfinity;
        public double Maximum { get; private set; } = double.NegativeInfinity;

        public void Add(double value)
        {
            if (!double.IsFinite(value))
            {
                throw new InvalidDataException("A mesh-deviation calculation produced a non-finite value.");
            }

            Count++;
            Minimum = Math.Min(Minimum, value);
            Maximum = Math.Max(Maximum, value);
            var delta = value - mean;
            mean += delta / Count;
            sumSquaredDeviation += delta * (value - mean);
            sumSquares += value * value;
        }

        public NominalActualDeviationStatistics ToContract()
        {
            if (Count == 0)
            {
                throw new InvalidDataException("The measured validation query produced no deviation values.");
            }

            return new NominalActualDeviationStatistics(
                Count,
                Minimum,
                Maximum,
                mean,
                Math.Sqrt(Math.Max(0.0, sumSquaredDeviation / Count)),
                Math.Sqrt(sumSquares / Count));
        }
    }
}
