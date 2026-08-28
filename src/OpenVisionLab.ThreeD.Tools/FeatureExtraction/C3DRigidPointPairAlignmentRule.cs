using System.Diagnostics;
using OpenVisionLab.ThreeD.Core;
using SdkCorrespondence = OpenVisionLab.Vision3D.FeatureExtraction.RigidPointPairCorrespondence;
using SdkPoint = OpenVisionLab.Vision3D.FeatureExtraction.ThreeDPoint;
using SdkTool = OpenVisionLab.Vision3D.FeatureExtraction.RigidPointPairAlignmentTool;
using SdkOptions = OpenVisionLab.Vision3D.FeatureExtraction.RigidPointPairAlignmentOptions;

namespace OpenVisionLab.ThreeD.Tools;

public sealed record C3DRigidPointPairAlignmentInput(
    string StepId,
    string OutputEntityId,
    string SourceEntityId,
    string SourceContentSha256,
    string ReferenceEntityId,
    string ReferenceContentSha256,
    string SourceUnit,
    string SourceFrameId,
    string ReferenceUnit,
    string ReferenceFrameId,
    IReadOnlyList<C3DRigidPointPairAlignmentPair> Pairs,
    double MaximumPairLengthError,
    double MinimumNormalizedCrossMagnitude);

public sealed record C3DRigidPointPairAlignmentEvaluation(
    ToolResult Result,
    C3DRigidPointPairAlignmentArtifact? Output);

/// <summary>
/// Strict Studio adapter for the SDK-owned deterministic three-pair rigid
/// alignment. It owns source identity, unit/frame compatibility, artifact
/// identity, and product-facing evidence only; it does not calculate pose or
/// residual arithmetic and never mutates a source cloud.
/// </summary>
public static class C3DRigidPointPairAlignmentAdapter
{
    public const string ToolName = "Rigid Point Pair Alignment";

    private const int RequiredPairCount = 3;

    public static C3DRigidPointPairAlignmentEvaluation Evaluate(
        C3DRigidPointPairAlignmentInput input,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            Validate(input);
            cancellationToken.ThrowIfCancellationRequested();
            var sdkResult = new SdkTool().Execute(
                input.Pairs
                    .Select(pair => new SdkCorrespondence(
                        new SdkPoint(pair.SourceX, pair.SourceY, pair.SourceZ),
                        new SdkPoint(pair.ReferenceX, pair.ReferenceY, pair.ReferenceZ)))
                    .ToArray(),
                new SdkOptions
                {
                    MaximumPairLengthError = input.MaximumPairLengthError,
                    MinimumNormalizedCrossMagnitude = input.MinimumNormalizedCrossMagnitude
                },
                cancellationToken);
            if (!sdkResult.Success || sdkResult.Pose is null)
            {
                throw new InvalidDataException(sdkResult.Message);
            }
            if (sdkResult.Residuals.Count != RequiredPairCount)
            {
                throw new InvalidDataException("OpenVisionLab Vision SDK rigid point-pair alignment returned incomplete residual evidence.");
            }

            var pose = new C3DRigidPointPairAlignmentPose(
                sdkResult.Pose.M11,
                sdkResult.Pose.M12,
                sdkResult.Pose.M13,
                sdkResult.Pose.M21,
                sdkResult.Pose.M22,
                sdkResult.Pose.M23,
                sdkResult.Pose.M31,
                sdkResult.Pose.M32,
                sdkResult.Pose.M33,
                sdkResult.Pose.TranslationX,
                sdkResult.Pose.TranslationY,
                sdkResult.Pose.TranslationZ);
            var residuals = input.Pairs
                .Select((pair, index) =>
                {
                    var residual = sdkResult.Residuals[index];
                    return new C3DRigidPointPairAlignmentResidual(
                        index,
                        pair.SourcePointId,
                        pair.ReferencePointId,
                        pair.SourceX,
                        pair.SourceY,
                        pair.SourceZ,
                        pair.ReferenceX,
                        pair.ReferenceY,
                        pair.ReferenceZ,
                        residual.Transformed.X,
                        residual.Transformed.Y,
                        residual.Transformed.Z,
                        residual.Residual.X,
                        residual.Residual.Y,
                        residual.Residual.Z,
                        residual.ResidualNorm);
                })
                .ToArray();
            EnsureFinite(
                pose.Values
                    .Concat(new[]
                    {
                        sdkResult.SourceNormalizedCrossMagnitude,
                        sdkResult.ReferenceNormalizedCrossMagnitude,
                        sdkResult.MaximumObservedPairLengthError,
                        sdkResult.RmsResidual,
                        sdkResult.MaximumResidual
                    }),
                "OpenVisionLab Vision SDK rigid point-pair alignment evidence");
            var output = C3DRigidPointPairAlignmentArtifact.Create(
                input.OutputEntityId,
                input.StepId,
                input.SourceEntityId,
                input.SourceContentSha256,
                input.ReferenceEntityId,
                input.ReferenceContentSha256,
                input.SourceUnit,
                input.SourceFrameId,
                input.ReferenceUnit,
                input.ReferenceFrameId,
                input.Pairs,
                input.MaximumPairLengthError,
                input.MinimumNormalizedCrossMagnitude,
                pose,
                sdkResult.SourceNormalizedCrossMagnitude,
                sdkResult.ReferenceNormalizedCrossMagnitude,
                sdkResult.MaximumObservedPairLengthError,
                sdkResult.RmsResidual,
                sdkResult.MaximumResidual,
                residuals,
                $"{input.StepId}:RigidPointPairAlignment:{C3DRigidPointPairAlignmentArtifact.ContractVersion}:policy={C3DRigidPointPairAlignmentArtifact.PairCountPolicyName}:source={input.SourceContentSha256}:reference={input.ReferenceContentSha256}");
            stopwatch.Stop();
            return new C3DRigidPointPairAlignmentEvaluation(
                new ToolResult(
                    ToolName,
                    ResultStatus.Pass,
                    "Completed deterministic rigid pose evidence from exactly three ordered point pairs; no point cloud was moved and no product acceptance was evaluated.",
                    stopwatch.Elapsed,
                    [
                        new Metric("Correspondence count", MetricKind.Count, RequiredPairCount, "count"),
                        new Metric("Source normalized cross magnitude", MetricKind.Number, output.SourceNormalizedCrossMagnitude, "ratio"),
                        new Metric("Reference normalized cross magnitude", MetricKind.Number, output.ReferenceNormalizedCrossMagnitude, "ratio"),
                        new Metric("Maximum pair-length error", MetricKind.Deviation, output.MaximumObservedPairLengthError, output.SourceUnit),
                        new Metric("Maximum residual", MetricKind.Deviation, output.MaximumResidual, output.SourceUnit)
                    ],
                    [new Overlay(output.OutputEntityId, OverlayKind.Point, "Rigid point-pair alignment correspondence evidence", SourceEntityId: input.SourceEntityId)]),
                output);
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            throw;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidDataException or InvalidOperationException or OverflowException)
        {
            stopwatch.Stop();
            return new C3DRigidPointPairAlignmentEvaluation(
                new ToolResult(ToolName, ResultStatus.Error, exception.Message, stopwatch.Elapsed, [], []),
                null);
        }
    }

    private static void Validate(C3DRigidPointPairAlignmentInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.StepId);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.OutputEntityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.SourceEntityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.SourceContentSha256);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.ReferenceEntityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.ReferenceContentSha256);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.SourceUnit);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.SourceFrameId);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.ReferenceUnit);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.ReferenceFrameId);
        ArgumentNullException.ThrowIfNull(input.Pairs);
        if (string.Equals(input.SourceEntityId, input.ReferenceEntityId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Rigid point-pair alignment requires distinct source and reference entity IDs.");
        }
        if (!string.Equals(input.SourceUnit, input.ReferenceUnit, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Rigid point-pair alignment requires matching source/reference units; frame IDs remain explicit endpoints.");
        }
        if (input.Pairs.Count != RequiredPairCount)
        {
            throw new InvalidDataException("Rigid Point Pair Alignment v1 requires exactly three ordered source/reference point pairs.");
        }
        if (!double.IsFinite(input.MaximumPairLengthError) || input.MaximumPairLengthError < 0d)
        {
            throw new InvalidDataException("MaximumPairLengthError must be an explicit finite non-negative number.");
        }
        if (!double.IsFinite(input.MinimumNormalizedCrossMagnitude)
            || input.MinimumNormalizedCrossMagnitude <= 0d
            || input.MinimumNormalizedCrossMagnitude >= 1d)
        {
            throw new InvalidDataException("MinimumNormalizedCrossMagnitude must be explicit, finite, greater than zero, and less than one.");
        }
        if (string.Equals(input.OutputEntityId, input.SourceEntityId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(input.OutputEntityId, input.ReferenceEntityId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Rigid point-pair alignment output ID must differ from both source and reference IDs.");
        }

        var sourcePointIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var referencePointIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sourceCoordinates = new HashSet<(double X, double Y, double Z)>();
        var referenceCoordinates = new HashSet<(double X, double Y, double Z)>();
        foreach (var pair in input.Pairs)
        {
            if (pair is null
                || string.IsNullOrWhiteSpace(pair.SourcePointId)
                || string.IsNullOrWhiteSpace(pair.ReferencePointId)
                || !Finite(pair.SourceX, pair.SourceY, pair.SourceZ, pair.ReferenceX, pair.ReferenceY, pair.ReferenceZ)
                || !sourcePointIds.Add(pair.SourcePointId)
                || !referencePointIds.Add(pair.ReferencePointId)
                || !sourceCoordinates.Add((pair.SourceX, pair.SourceY, pair.SourceZ))
                || !referenceCoordinates.Add((pair.ReferenceX, pair.ReferenceY, pair.ReferenceZ)))
            {
                throw new InvalidDataException("Rigid point-pair alignment requires three distinct finite point identities and coordinates.");
            }
        }
    }

    private static bool Finite(params double[] values) => values.All(double.IsFinite);

    private static void EnsureFinite(IEnumerable<double> values, string label)
    {
        if (!values.All(double.IsFinite)) throw new InvalidDataException($"{label} contains a non-finite value.");
    }
}
