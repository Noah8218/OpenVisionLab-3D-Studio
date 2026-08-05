using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using Sdk = OpenVisionLab.Vision3D.FeatureExtraction;

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

        var triangles = new List<Sdk.MeshTriangle>();
        var nominalSummary = BinaryStlInspectionReader.Scan(
            input.NominalSource.Path,
            (index, triangle) => triangles.Add(new Sdk.MeshTriangle(
                index,
                ToSdk(triangle.A),
                ToSdk(triangle.B),
                ToSdk(triangle.C))));
        if (!nominalSummary.SourceSha256.Equals(input.NominalSource.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"Nominal source SHA-256 does not match the expected identity: {input.NominalSource.Id}");
        }

        cancellationToken.ThrowIfCancellationRequested();
        Report(progress, "Indexing nominal mesh", 0, query.VertexCount, totalStopwatch.Elapsed);
        var sdkProgress = progress is null
            ? null
            : new ComparisonProgressAdapter(progress, totalStopwatch);
        var comparison = new Sdk.NominalActualMeshComparisonTool().Execute(
            triangles,
            ReadQueryPoints(query, cancellationToken),
            new Sdk.NominalActualMeshComparisonOptions(
                query.VertexCount,
                input.LowerTolerance,
                input.UpperTolerance,
                maximumDisplaySamples),
            sdkProgress,
            cancellationToken);
        if (!comparison.Success)
        {
            throw new InvalidDataException(comparison.Message);
        }

        if (!query.IsComplete || comparison.ProcessedPointCount != query.VertexCount)
        {
            throw new InvalidDataException("The measured validation query was not consumed completely.");
        }

        totalStopwatch.Stop();
        var outOfToleranceCount = comparison.BelowToleranceCount + comparison.AboveToleranceCount;
        var status = outOfToleranceCount == 0 ? ResultStatus.Pass : ResultStatus.Fail;
        var message = string.Create(
            CultureInfo.InvariantCulture,
            $"{status}: {outOfToleranceCount:N0} of {comparison.ProcessedPointCount:N0} points outside [{input.LowerTolerance:G6}, {input.UpperTolerance:G6}] {input.Unit}.");

        return new NominalActualComparisonResult(
            input,
            status,
            message,
            comparison.ProcessedPointCount,
            ToStudio(comparison.UnsignedStatistics),
            ToStudio(comparison.SignedStatistics),
            comparison.BelowToleranceCount,
            comparison.WithinToleranceCount,
            comparison.AboveToleranceCount,
            comparison.DirectSignResolvedCount,
            comparison.RobustSignRecoveredCount,
            comparison.DisplayStride,
            comparison.DisplaySamples.Select(ToStudio).ToArray(),
            comparison.IndexDuration,
            comparison.CalculationDuration,
            totalStopwatch.Elapsed);
    }

    private static IEnumerable<Sdk.ThreeDPoint> ReadQueryPoints(
        BinaryPlyVertexReader query,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var chunkCount = query.ReadChunk();
            if (chunkCount == 0)
            {
                yield break;
            }

            for (var chunkIndex = 0; chunkIndex < chunkCount; chunkIndex++)
            {
                if ((chunkIndex & 1023) == 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                yield return ToSdk(query.GetPosition(chunkIndex));
            }
        }
    }

    private static Sdk.ThreeDPoint ToSdk(Vector3 point) =>
        new(point.X, point.Y, point.Z);

    private static NominalActualDeviationStatistics ToStudio(Sdk.MeshDeviationStatistics statistics) =>
        new(
            statistics.Count,
            statistics.Minimum,
            statistics.Maximum,
            statistics.Mean,
            statistics.StandardDeviationPopulation,
            statistics.RootMeanSquare);

    private static NominalActualDeviationSample ToStudio(Sdk.NominalActualMeshDeviationSample sample) =>
        new(
            sample.PointIndex,
            ToStudio(sample.Point),
            ToStudio(sample.ClosestPoint),
            sample.SourceTriangleIndex,
            sample.UnsignedDistance,
            sample.SignedDistance,
            sample.RobustSignRecovered);

    private static Vector3 ToStudio(Sdk.ThreeDPoint point) =>
        new((float)point.X, (float)point.Y, (float)point.Z);

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

    private sealed class ComparisonProgressAdapter(
        IProgress<NominalActualComparisonProgress> progress,
        Stopwatch totalStopwatch) : IProgress<Sdk.NominalActualMeshComparisonProgress>
    {
        public void Report(Sdk.NominalActualMeshComparisonProgress value) =>
            progress.Report(new NominalActualComparisonProgress(
                "Comparing actual to nominal",
                value.ProcessedPointCount,
                value.TotalPointCount,
                totalStopwatch.Elapsed));
    }
}
