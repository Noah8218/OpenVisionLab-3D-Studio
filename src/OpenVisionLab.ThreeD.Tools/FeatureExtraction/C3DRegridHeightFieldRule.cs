using System.Diagnostics;
using NoahInputPoint = Lib.ThreeD.FeatureExtraction.ReferenceGridInputPoint;
using NoahProfile = Lib.ThreeD.FeatureExtraction.ReferenceGridProfile;
using NoahRegridTool = Lib.ThreeD.FeatureExtraction.ReferenceGridRegridTool;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Tools;

public sealed record C3DRegridHeightFieldInput(
    string StepId,
    C3DTransformedPointCloud PublishedTransformedPointCloud,
    C3DReferenceGridProfile ReferenceGridProfile,
    string OutputEntityId);

public sealed record C3DRegridHeightFieldEvaluation(ToolResult Result, C3DTransformedHeightField? Output);

/// <summary>
/// Strict Studio adapter for Library-Noah deterministic re-gridding. A3 only
/// projects a Published A2 cloud into its authored frame; it never derives a
/// frame, interpolates holes, writes C3D, or measures a feature.
/// </summary>
public static class C3DRegridHeightFieldRule
{
    public const string ToolName = "Re-grid Height Map";

    public static C3DRegridHeightFieldEvaluation Evaluate(C3DRegridHeightFieldInput input, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            Validate(input);
            var points = input.PublishedTransformedPointCloud.Points
                .Select(point => new NoahInputPoint(point.Row, point.Column, point.X, point.Y, point.Z))
                .ToArray();
            var profile = input.ReferenceGridProfile;
            var noahProfile = new NoahProfile(
                profile.ReferenceFrameId, profile.ReferenceUnit, profile.ReferenceProvenance, profile.ReferenceRevision,
                profile.Origin.X, profile.Origin.Y, profile.Origin.Z,
                profile.UAxis.X, profile.UAxis.Y, profile.UAxis.Z,
                profile.VAxis.X, profile.VAxis.Y, profile.VAxis.Z,
                profile.HAxis.X, profile.HAxis.Y, profile.HAxis.Z,
                profile.PitchU, profile.PitchV, profile.RowCount, profile.ColumnCount, profile.MinimumCoverageRatio);
            var noahResult = new NoahRegridTool().Execute(points, noahProfile, cancellationToken);
            if (!noahResult.Success) throw new InvalidDataException(noahResult.Message);

            var cells = noahResult.Cells
                .Select(cell => new C3DTransformedHeightCell(cell.Row, cell.Column, cell.Height, cell.SourceRow, cell.SourceColumn, cell.PlanarDistanceSquared))
                .ToArray();
            var output = C3DTransformedHeightField.Create(
                input.OutputEntityId,
                input.PublishedTransformedPointCloud,
                profile,
                cells,
                noahResult.CollisionCount,
                $"{input.StepId}:RegridHeightMap:{C3DTransformedHeightField.ContractVersion}:source={input.PublishedTransformedPointCloud.ContentSha256}:profile={profile.ContentSha256}");
            stopwatch.Stop();
            var status = output.MeetsMinimumCoverage ? ResultStatus.Pass : ResultStatus.Warning;
            var message = output.MeetsMinimumCoverage
                ? "Completed deterministic reference-grid re-sampling. Preview is ready for explicit Publish."
                : "Completed deterministic reference-grid re-sampling, but coverage is below the authored Publish minimum. Missing cells remain missing and Publish is blocked.";
            return new C3DRegridHeightFieldEvaluation(
                new ToolResult(
                    ToolName,
                    status,
                    message,
                    stopwatch.Elapsed,
                    [
                        new Metric("Populated grid cells", MetricKind.Count, output.PopulatedCellCount, "cells"),
                        new Metric("Missing grid cells", MetricKind.Count, output.MissingCellCount, "cells"),
                        new Metric("Coverage", MetricKind.Number, output.CoverageRatio, "ratio", status),
                        new Metric("Cell collisions", MetricKind.Count, output.CollisionCount, "points")
                    ],
                    [new Overlay(output.OutputEntityId, OverlayKind.ColorMap, "Reference-grid height and missing-cell evidence", SourceEntityId: input.PublishedTransformedPointCloud.OutputEntityId)]),
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
            return new C3DRegridHeightFieldEvaluation(new ToolResult(ToolName, ResultStatus.Error, exception.Message, stopwatch.Elapsed, [], []), null);
        }
    }

    private static void Validate(C3DRegridHeightFieldInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.StepId);
        ArgumentNullException.ThrowIfNull(input.PublishedTransformedPointCloud);
        ArgumentNullException.ThrowIfNull(input.ReferenceGridProfile);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.OutputEntityId);
        var cloud = input.PublishedTransformedPointCloud;
        var profile = input.ReferenceGridProfile;
        if (!string.Equals(profile.ReferenceFrameId, cloud.ReferenceFrameId, StringComparison.Ordinal)
            || !string.Equals(profile.ReferenceUnit, cloud.ReferenceUnit, StringComparison.Ordinal)
            || !string.Equals(profile.ReferenceProvenance, cloud.ReferenceProvenance, StringComparison.Ordinal)
            || !string.Equals(profile.ReferenceRevision, cloud.ReferenceRevision, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Re-grid Height Map v1 requires an authored ReferenceGridProfile that exactly matches the Published TransformedPointCloud reference identity/frame/unit.");
        }
    }
}
