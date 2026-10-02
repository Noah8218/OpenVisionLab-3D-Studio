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
        var alignment = ResolveAlignment(input);

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
                ToSdk(triangle.A, alignment?.Pose),
                ToSdk(triangle.B, alignment?.Pose),
                ToSdk(triangle.C, alignment?.Pose))));
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
        if (alignment is not null)
        {
            message += string.Create(
                CultureInfo.InvariantCulture,
                $" Accepted alignment coverage {alignment.Evidence.CoverageRatio:G6} (minimum {alignment.Evidence.MinimumCoverageRatio:G6}).");
        }

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
            totalStopwatch.Elapsed,
            alignment?.Evidence);
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

    private static Sdk.ThreeDPoint ToSdk(Vector3 point, RigidPose3D? pose)
    {
        if (pose is null)
        {
            return ToSdk(point);
        }

        var transformed = pose.TransformPoint(new SurfaceModelPoint3(point.X, point.Y, point.Z));
        return new Sdk.ThreeDPoint(transformed.X, transformed.Y, transformed.Z);
    }

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
        if (input.Alignment is null
            && !NominalActualComparisonInput.IsIdentityAlignmentId(input.AlignmentId))
        {
            throw new InvalidDataException(
                "A non-identity Nominal/Actual alignment requires exact Surface Match artifact references.");
        }
    }

    public static NominalActualAlignmentEvidence? ValidateAlignment(
        NominalActualComparisonInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ValidateInput(input);
        return ResolveAlignment(input)?.Evidence;
    }

    private static ResolvedAlignment? ResolveAlignment(NominalActualComparisonInput input)
    {
        if (input.Alignment is null)
        {
            return null;
        }

        ValidateArtifactReference(input.Alignment.Model, "model");
        ValidateArtifactReference(input.Alignment.Scene, "scene");
        ValidateArtifactReference(input.Alignment.Execution, "execution");
        ValidateArtifactReference(input.Alignment.Assessment, "assessment");
        var model = SurfaceModelArtifactStore.Load(input.Alignment.Model.Path);
        var scene = PreparedSceneArtifactStore.Load(input.Alignment.Scene.Path);
        var execution = SurfaceMatchExecutionArtifactStore.Load(input.Alignment.Execution.Path);
        var assessment = SurfaceMatchAssessmentArtifactStore.Load(input.Alignment.Assessment.Path);
        RequireContentIdentity(model.ContentSha256, input.Alignment.Model, "model");
        RequireContentIdentity(scene.ContentSha256, input.Alignment.Scene, "scene");
        RequireContentIdentity(execution.ContentSha256, input.Alignment.Execution, "execution");
        RequireContentIdentity(assessment.ContentSha256, input.Alignment.Assessment, "assessment");

        if (!input.AlignmentId.Equals(assessment.ContentSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "Nominal/actual alignment ID must equal the accepted assessment content identity.");
        }

        if (!model.SourceEntityId.Equals(input.NominalSource.Id, StringComparison.Ordinal)
            || !model.SourceContentSha256.Equals(input.NominalSource.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "Surface Match model source identity does not match the Nominal/Actual nominal source.");
        }

        if (!scene.SourceQuality.Source.EntityId.Equals(input.QuerySource.Id, StringComparison.Ordinal)
            || scene.SourceQuality.Source.ByteLength != input.QuerySource.ByteLength
            || !scene.SourceQuality.Source.ContentSha256.Equals(input.QuerySource.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "Prepared Scene source identity does not match the Nominal/Actual validation query.");
        }

        if (!execution.ModelContentSha256.Equals(model.ContentSha256, StringComparison.Ordinal)
            || !execution.SceneContentSha256.Equals(scene.ContentSha256, StringComparison.Ordinal)
            || !assessment.ExecutionContentSha256.Equals(execution.ContentSha256, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Surface Match model, scene, execution, and assessment identities are not linked exactly.");
        }

        if (assessment.Decision != SurfaceMatchDecision.Pass
            || assessment.RawPoseState != RigidSurfacePoseSearchState.Matched
            || execution.PoseResult.State != RigidSurfacePoseSearchState.Matched
            || execution.PoseResult.Pose is not { } pose)
        {
            throw new InvalidDataException(
                "Nominal/actual deviation requires a matched Surface Match execution with a Pass assessment.");
        }

        if (!pose.IsRigid(1e-9))
        {
            throw new InvalidDataException(
                "Nominal/actual alignment pose must be rigid; scale or shear is unsupported.");
        }

        if (!input.Unit.Equals(model.Unit, StringComparison.Ordinal)
            || !input.Unit.Equals(scene.Unit, StringComparison.Ordinal)
            || !input.Unit.Equals(pose.Unit, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Nominal/actual and Surface Match units must agree exactly; unit conversion is unsupported.");
        }

        if (!model.FrameId.Equals(pose.SourceFrameId, StringComparison.Ordinal)
            || !scene.FrameId.Equals(pose.TargetFrameId, StringComparison.Ordinal)
            || !input.FrameId.Equals(scene.FrameId, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Nominal/actual and Surface Match source/target frames do not agree.");
        }

        if (!model.CoordinateConvention.Equals(scene.CoordinateConvention, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Nominal model and Prepared Scene coordinate conventions do not agree.");
        }

        var coverage = execution.PoseResult.Coverage;
        var policy = assessment.Policy;
        var evidence = new NominalActualAlignmentEvidence(
            NominalActualAlignmentEvidence.CurrentSemantics,
            model.ArtifactId,
            model.ContentSha256,
            scene.ArtifactId,
            scene.ContentSha256,
            execution.ContentSha256,
            execution.PoseResult.ContentSha256,
            assessment.ContentSha256,
            policy.ContentSha256,
            coverage.Semantics,
            coverage.CoverageRatio,
            policy.MinimumCoverageRatio,
            coverage.InlierRmse!.Value,
            policy.MaximumInlierRmse,
            input.Unit,
            pose.SourceFrameId,
            pose.TargetFrameId);
        return new ResolvedAlignment(pose, evidence);
    }

    private static void ValidateArtifactReference(
        NominalActualArtifactReference? reference,
        string role)
    {
        if (reference is null
            || string.IsNullOrWhiteSpace(reference.Path)
            || reference.ContentSha256?.Length != 64
            || reference.ContentSha256.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new InvalidDataException(
                $"Nominal/actual alignment {role} reference requires a path and SHA-256 content identity.");
        }
    }

    private static void RequireContentIdentity(
        string actual,
        NominalActualArtifactReference expected,
        string role)
    {
        if (!actual.Equals(expected.ContentSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"Nominal/actual alignment {role} content identity does not match its recipe reference.");
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

    private sealed record ResolvedAlignment(
        RigidPose3D Pose,
        NominalActualAlignmentEvidence Evidence);
}
