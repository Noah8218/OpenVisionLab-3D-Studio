using System.Diagnostics;
using OpenVisionLab.ThreeD.Core;
using SdkCorrespondence = OpenVisionLab.Vision3D.FeatureExtraction.ConstrainedBestFitRigidCorrespondence;
using SdkOptions = OpenVisionLab.Vision3D.FeatureExtraction.ConstrainedBestFitRigidAlignmentOptions;
using SdkPoint = OpenVisionLab.Vision3D.FeatureExtraction.ThreeDPoint;
using SdkTool = OpenVisionLab.Vision3D.FeatureExtraction.ConstrainedBestFitRigidAlignmentTool;

namespace OpenVisionLab.ThreeD.Tools;

public sealed record C3DConstrainedBestFitRigidAlignmentInput(
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
    IReadOnlyList<C3DConstrainedBestFitRigidAlignmentPair> Pairs,
    int MaximumCorrespondenceCount,
    double MinimumNormalizedLineSpread,
    double ArithmeticResidualWarning);

public sealed record C3DConstrainedBestFitRigidAlignmentEvaluation(
    ToolResult Result,
    C3DConstrainedBestFitRigidAlignmentArtifact? Output);

/// <summary>
/// Strict Studio adapter for the SDK-owned constrained all-pair proper-rigid
/// best-fit tool. It owns identity, unit/frame policy, artifact composition,
/// and product-facing evidence only; it does not perform pose arithmetic or
/// mutate a source cloud.
/// </summary>
public static class C3DConstrainedBestFitRigidAlignmentAdapter
{
    public const string ToolName = "Constrained Best-Fit Rigid Alignment";

    private const int MinimumCorrespondenceCount = 4;
    private const int MaximumSupportedCorrespondenceCount = 64;

    public static C3DConstrainedBestFitRigidAlignmentEvaluation Evaluate(
        C3DConstrainedBestFitRigidAlignmentInput input,
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
                    MaximumCorrespondenceCount = input.MaximumCorrespondenceCount,
                    MinimumNormalizedLineSpread = input.MinimumNormalizedLineSpread,
                    ArithmeticResidualWarning = input.ArithmeticResidualWarning
                },
                cancellationToken);
            if (!sdkResult.Success || sdkResult.Pose is null)
            {
                throw new InvalidDataException(sdkResult.Message);
            }
            if (sdkResult.Residuals.Count != input.Pairs.Count
                || !sdkResult.UsedAllCorrespondences)
            {
                throw new InvalidDataException("OpenVisionLab Vision SDK constrained best-fit rigid alignment returned incomplete residual evidence.");
            }

            var pose = new C3DConstrainedBestFitRigidAlignmentPose(
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
                    return new C3DConstrainedBestFitRigidAlignmentResidual(
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
                        sdkResult.SourceNormalizedLineSpread,
                        sdkResult.ReferenceNormalizedLineSpread,
                        sdkResult.SourceCentroid.X,
                        sdkResult.SourceCentroid.Y,
                        sdkResult.SourceCentroid.Z,
                        sdkResult.ReferenceCentroid.X,
                        sdkResult.ReferenceCentroid.Y,
                        sdkResult.ReferenceCentroid.Z,
                        sdkResult.RmsResidual,
                        sdkResult.MaximumResidual
                    }),
                "OpenVisionLab Vision SDK constrained best-fit rigid alignment evidence");
            var output = C3DConstrainedBestFitRigidAlignmentArtifact.Create(
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
                input.MaximumCorrespondenceCount,
                input.MinimumNormalizedLineSpread,
                input.ArithmeticResidualWarning,
                pose,
                sdkResult.SourceNormalizedLineSpread,
                sdkResult.ReferenceNormalizedLineSpread,
                new C3DConstrainedBestFitRigidAlignmentCentroid(
                    sdkResult.SourceCentroid.X,
                    sdkResult.SourceCentroid.Y,
                    sdkResult.SourceCentroid.Z),
                new C3DConstrainedBestFitRigidAlignmentCentroid(
                    sdkResult.ReferenceCentroid.X,
                    sdkResult.ReferenceCentroid.Y,
                    sdkResult.ReferenceCentroid.Z),
                sdkResult.RmsResidual,
                sdkResult.MaximumResidual,
                sdkResult.ArithmeticResidualWarningExceeded,
                residuals,
                $"{input.StepId}:ConstrainedBestFitRigidAlignment:{C3DConstrainedBestFitRigidAlignmentArtifact.ContractVersion}:policy={C3DConstrainedBestFitRigidAlignmentArtifact.CorrespondenceCountPolicyName}:pose={C3DConstrainedBestFitRigidAlignmentArtifact.PoseConstraintPolicyName}:source={input.SourceContentSha256}:reference={input.ReferenceContentSha256}");
            stopwatch.Stop();
            var warning = output.ArithmeticResidualWarningExceeded;
            return new C3DConstrainedBestFitRigidAlignmentEvaluation(
                new ToolResult(
                    ToolName,
                    ResultStatus.Pass,
                    warning
                        ? "Completed constrained all-pair proper-rigid best-fit evidence; residual exceeds the authored review threshold. No point cloud was moved and no product acceptance was evaluated."
                        : "Completed constrained all-pair proper-rigid best-fit evidence; no point cloud was moved and no product acceptance was evaluated.",
                    stopwatch.Elapsed,
                    [
                        new Metric("Correspondence count", MetricKind.Count, output.Pairs.Count, "count"),
                        new Metric("Source normalized line spread", MetricKind.Number, output.SourceNormalizedLineSpread, "ratio"),
                        new Metric("Reference normalized line spread", MetricKind.Number, output.ReferenceNormalizedLineSpread, "ratio"),
                        new Metric("RMS residual", MetricKind.Deviation, output.RmsResidual, output.SourceUnit, warning ? ResultStatus.Warning : ResultStatus.Pass),
                        new Metric("Maximum residual", MetricKind.Deviation, output.MaximumResidual, output.SourceUnit, warning ? ResultStatus.Warning : ResultStatus.Pass)
                    ],
                    [new Overlay(output.OutputEntityId, OverlayKind.Point, "Constrained best-fit rigid correspondence evidence", SourceEntityId: input.SourceEntityId)]),
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
            return new C3DConstrainedBestFitRigidAlignmentEvaluation(
                new ToolResult(ToolName, ResultStatus.Error, exception.Message, stopwatch.Elapsed, [], []),
                null);
        }
    }

    private static void Validate(C3DConstrainedBestFitRigidAlignmentInput input)
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
            throw new InvalidDataException("Constrained best-fit rigid alignment requires distinct source and reference entity IDs.");
        }
        if (!string.Equals(input.SourceUnit, input.ReferenceUnit, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Constrained best-fit rigid alignment requires matching source/reference units; frame IDs remain explicit endpoints.");
        }
        if (input.Pairs.Count < MinimumCorrespondenceCount
            || input.Pairs.Count > MaximumSupportedCorrespondenceCount)
        {
            throw new InvalidDataException("Constrained best-fit rigid alignment requires four to sixty-four ordered source/reference point pairs.");
        }
        if (input.MaximumCorrespondenceCount < MinimumCorrespondenceCount
            || input.MaximumCorrespondenceCount > MaximumSupportedCorrespondenceCount
            || input.Pairs.Count > input.MaximumCorrespondenceCount)
        {
            throw new InvalidDataException("MaximumCorrespondenceCount must be between four and sixty-four and cover every supplied pair.");
        }
        if (!double.IsFinite(input.MinimumNormalizedLineSpread)
            || input.MinimumNormalizedLineSpread <= 0d
            || input.MinimumNormalizedLineSpread >= 1d)
        {
            throw new InvalidDataException("MinimumNormalizedLineSpread must be explicit, finite, greater than zero, and less than one.");
        }
        if (!double.IsFinite(input.ArithmeticResidualWarning)
            || input.ArithmeticResidualWarning < 0d)
        {
            throw new InvalidDataException("ArithmeticResidualWarning must be an explicit finite non-negative number.");
        }
        if (string.Equals(input.OutputEntityId, input.SourceEntityId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(input.OutputEntityId, input.ReferenceEntityId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Constrained best-fit rigid alignment output ID must differ from both source and reference IDs.");
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
                throw new InvalidDataException("Constrained best-fit rigid alignment requires distinct finite source/reference point identities and coordinates.");
            }
        }
    }

    private static bool Finite(params double[] values) => values.All(double.IsFinite);

    private static void EnsureFinite(IEnumerable<double> values, string label)
    {
        if (!values.All(double.IsFinite)) throw new InvalidDataException($"{label} contains a non-finite value.");
    }
}
