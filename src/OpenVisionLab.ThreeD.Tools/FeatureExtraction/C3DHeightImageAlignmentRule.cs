using System.Diagnostics;
using System.Runtime.InteropServices;
using OpenCvSharp;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.Vision2D.Property;
using OpenVisionLab.Vision2D.Result;
using OpenVisionLab.Vision2D.Tool;

namespace OpenVisionLab.ThreeD.Tools;

public sealed record C3DHeightImageAlignmentInput(
    string StepId,
    C3DHeightFieldSnapshot Reference,
    C3DHeightFieldSnapshot Moving,
    string SelectionId,
    ToolRecipeGridRectangle TemplateSelection,
    string OutputEntityId,
    C3DHeightImageAlignmentMode Mode,
    double SearchScoreMinimum,
    double AcceptanceScoreMinimumPercent,
    double MinimumCandidateMarginPercent,
    int AngleMinimumDegrees,
    int AngleMaximumDegrees,
    double AngleStepDegrees,
    double RansacReprojectionThreshold = 3d);

public sealed record C3DHeightImageAlignmentEvaluation(
    ToolResult Result,
    C3DHeightImageAlignmentArtifact? Output);

/// <summary>
/// Strict product adapter for the F-08 height-image alignment route. Native
/// pixel conversion and matching arithmetic remain in the Data boundary and
/// the committed Vision SDK; Studio owns identity, units, ROI, policy, and
/// evidence composition.
/// </summary>
public static class C3DHeightImageAlignmentAdapter
{
    public static C3DHeightImageAlignmentEvaluation Evaluate(
        C3DHeightImageAlignmentInput input,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            ValidateAdapterInput(input);
            cancellationToken.ThrowIfCancellationRequested();

            using var referenceImage = ToGrayscaleImage(input.Reference, cancellationToken);
            using var movingImage = ToGrayscaleImage(input.Moving, cancellationToken);
            using var template = new Mat(
                referenceImage,
                new Rect(
                    input.TemplateSelection.Column,
                    input.TemplateSelection.Row,
                    input.TemplateSelection.ColumnCount,
                    input.TemplateSelection.RowCount)).Clone();

            using var execution = input.Mode switch
            {
                C3DHeightImageAlignmentMode.BorderTemplate => ExecuteBorder(input, template, movingImage),
                C3DHeightImageAlignmentMode.FeatureHomography => ExecuteFeature(input, template, movingImage),
                _ => throw new ArgumentOutOfRangeException(nameof(input.Mode))
            };
            if (!execution.Result.Success)
            {
                throw new InvalidDataException(
                    $"Height-image alignment SDK execution failed: {execution.Result.ErrorName}: {execution.Result.Message}");
            }
            return CompleteEvaluation(input, execution.Candidates, stopwatch);
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            throw;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidDataException or OverflowException)
        {
            stopwatch.Stop();
            return new C3DHeightImageAlignmentEvaluation(
                new ToolResult(
                    "C3D Height Image Alignment",
                    ResultStatus.Error,
                    exception.Message,
                    stopwatch.Elapsed,
                    [],
                    []),
                null);
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            return new C3DHeightImageAlignmentEvaluation(
                new ToolResult(
                    "C3D Height Image Alignment",
                    ResultStatus.Error,
                    $"Height-image alignment failed closed after SDK or image-processing failure: {exception.Message}",
                    stopwatch.Elapsed,
                    [],
                    []),
                null);
        }
    }

    private sealed class SdkExecution : IDisposable
    {
        private readonly IDisposable tool;

        public SdkExecution(IDisposable tool, VisionToolResult result, IReadOnlyList<MatchingResult> candidates)
        {
            this.tool = tool;
            Result = result;
            Candidates = candidates;
        }

        public VisionToolResult Result { get; }
        public IReadOnlyList<MatchingResult> Candidates { get; }

        public void Dispose()
        {
            Result.Dispose();
            tool.Dispose();
        }
    }

    private static SdkExecution ExecuteBorder(
        C3DHeightImageAlignmentInput input,
        Mat template,
        Mat movingImage)
    {
        var tool = new MatchingTool();
        try
        {
            tool.SetProperty(new MatchingToolProperty
            {
                MATCH_MODE = TemplateMatchModes.CCoeffNormed,
                SCORE_MIN = input.SearchScoreMinimum,
                NUM_MATCH = 2,
                USE_FIND_ANGLE = true,
                FIND_ANGLE = input.AngleStepDegrees,
                FIND_ANGLE_MIN = input.AngleMinimumDegrees,
                FIND_ANGLE_MAX = input.AngleMaximumDegrees,
                USE_FIND_SCALE = false,
                USE_CANNY = true,
                CANNY_LOW = 30,
                CANNY_HIGH = 90,
                USE_ROI = false,
                USE_MULTI_ROI = false,
                USE_COARSE_TO_FINE_ANGLE_SEARCH = false,
                USE_PYRAMID_POSITION_PROPOSAL = false
            });
            tool.SetTemplateImage(template);
            var result = tool.Execute(movingImage);
            return new SdkExecution(tool, result, tool.results?.ToArray() ?? []);
        }
        catch
        {
            tool.Dispose();
            throw;
        }
    }

    private static SdkExecution ExecuteFeature(
        C3DHeightImageAlignmentInput input,
        Mat template,
        Mat movingImage)
    {
        var tool = new SiftTool();
        try
        {
            tool.SetProperty(new SiftToolProperty
            {
                SCORE_MIN = input.SearchScoreMinimum,
                RANSAC_REPROJ_THRESHOLD = input.RansacReprojectionThreshold,
                USE_ROI = false,
                USE_MULTI_ROI = false
            });
            tool.SetTemplateImage(template);
            var result = tool.Execute(movingImage);
            return new SdkExecution(tool, result, tool.results?.ToArray() ?? []);
        }
        catch
        {
            tool.Dispose();
            throw;
        }
    }

    private static C3DHeightImageAlignmentEvaluation CompleteEvaluation(
        C3DHeightImageAlignmentInput input,
        IReadOnlyList<MatchingResult> rawCandidates,
        Stopwatch stopwatch)
    {
        var candidates = rawCandidates
            .Where(candidate => candidate is not null)
            .OrderByDescending(candidate => NormalizeScorePercent(candidate.Score))
            .ThenBy(candidate => candidate.Center.Y)
            .ThenBy(candidate => candidate.Center.X)
            .ThenBy(candidate => candidate.Angle)
            .ToArray();
        if (candidates.Length == 0)
        {
            throw new InvalidDataException("Height-image alignment produced no candidate.");
        }

        var best = candidates[0];
        var bestScore = NormalizeScorePercent(best.Score);
        var secondScore = candidates.Length > 1
            ? NormalizeScorePercent(candidates[1].Score)
            : 0d;
        var scoreMargin = candidates.Length > 1
            ? Math.Max(0d, bestScore - secondScore)
            : bestScore;
        if (bestScore < input.AcceptanceScoreMinimumPercent)
        {
            throw new InvalidDataException(
                $"Height-image alignment score is below the acceptance threshold. Score={bestScore:0.###}%, Minimum={input.AcceptanceScoreMinimumPercent:0.###}%.");
        }

        if (candidates.Length > 1 && scoreMargin < input.MinimumCandidateMarginPercent)
        {
            throw new InvalidDataException(
                $"Height-image alignment is ambiguous. Best={bestScore:0.###}%, Second={secondScore:0.###}%, Margin={scoreMargin:0.###}%, Required={input.MinimumCandidateMarginPercent:0.###}%.");
        }

        if (best.Angle < input.AngleMinimumDegrees - input.AngleStepDegrees * 0.5d
            || best.Angle > input.AngleMaximumDegrees + input.AngleStepDegrees * 0.5d)
        {
            throw new InvalidDataException(
                $"Height-image alignment angle is outside the declared acceptance range. Angle={best.Angle:0.###}°, Range={input.AngleMinimumDegrees}..{input.AngleMaximumDegrees}°.");
        }

        var referenceCenterX = input.TemplateSelection.Column + input.TemplateSelection.ColumnCount / 2d;
        var referenceCenterY = input.TemplateSelection.Row + input.TemplateSelection.RowCount / 2d;
        var pose = new C3DHeightImageAlignmentPose(
            best.Center.X - referenceCenterX,
            best.Center.Y - referenceCenterY,
            best.Angle,
            best.Scale <= 0d ? 1d : best.Scale,
            best.Center.X,
            best.Center.Y,
            best.Bounding.Left,
            best.Bounding.Top,
            best.Bounding.Width,
            best.Bounding.Height);
        var diagnostics = new C3DHeightImageAlignmentDiagnostics(
            candidates.Length,
            bestScore,
            secondScore,
            scoreMargin,
            input.SearchScoreMinimum,
            input.AcceptanceScoreMinimumPercent,
            input.MinimumCandidateMarginPercent,
            input.AngleMinimumDegrees,
            input.AngleMaximumDegrees,
            input.AngleStepDegrees);
        var provenance =
            $"{input.StepId}:HeightImageAlignment:{C3DHeightImageAlignmentArtifact.ContractVersion}:mode={input.Mode}:mapping={C3DHeightImageFrame.CoordinateMapping}:selection={input.SelectionId}:reference={input.Reference.ContentSha256}:moving={input.Moving.ContentSha256}:scoreMin={input.SearchScoreMinimum:R}:acceptMinPercent={input.AcceptanceScoreMinimumPercent:R}:marginMinPercent={input.MinimumCandidateMarginPercent:R}:angle={input.AngleMinimumDegrees}..{input.AngleMaximumDegrees}/{input.AngleStepDegrees:R}:candidate=SDK:{input.Mode}";
        var output = C3DHeightImageAlignmentArtifact.Create(
            input.OutputEntityId,
            input.StepId,
            input.Reference.EntityId,
            input.Reference.ContentSha256,
            input.Moving.EntityId,
            input.Moving.ContentSha256,
            input.Reference.Unit,
            input.Reference.FrameId,
            input.SelectionId,
            input.TemplateSelection,
            input.Mode,
            pose,
            diagnostics,
            provenance);
        stopwatch.Stop();
        return new C3DHeightImageAlignmentEvaluation(
            new ToolResult(
                "C3D Height Image Alignment",
                ResultStatus.Pass,
                "Completed - 2D height-image alignment; no calibrated physical pose claimed.",
                stopwatch.Elapsed,
                [
                    new Metric("Alignment score", MetricKind.Number, bestScore, "%"),
                    new Metric("Alignment score margin", MetricKind.Number, scoreMargin, "%"),
                    new Metric("Translation X", MetricKind.Length, pose.TranslationX, "pixel"),
                    new Metric("Translation Y", MetricKind.Length, pose.TranslationY, "pixel"),
                    new Metric("Rotation", MetricKind.Angle, pose.RotationDegrees, "degree")
                ],
                [new Overlay(
                    input.OutputEntityId,
                    OverlayKind.Box,
                    "Height-image alignment bounding box",
                    SourceEntityId: input.Moving.EntityId)]),
            output);
    }

    private static double NormalizeScorePercent(double score)
    {
        if (!double.IsFinite(score))
        {
            return 0d;
        }

        return score <= 1.0000001d ? score * 100d : score;
    }

    private static Mat ToGrayscaleImage(
        C3DHeightFieldSnapshot source,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var frame = C3DHeightImageFrame.Create(source, cancellationToken);
        var display = frame.CreateDisplayFrame(
            C3DHeightImagePalette.Grayscale,
            source.Minimum,
            source.Maximum,
            cancellationToken);
        using var bgra = new Mat(display.Height, display.Width, MatType.CV_8UC4);
        var pixels = display.Bgra32Pixels.ToArray();
        Marshal.Copy(pixels, 0, bgra.Data, pixels.Length);
        using var gray = new Mat();
        Cv2.CvtColor(bgra, gray, ColorConversionCodes.BGRA2GRAY);
        return gray.Clone();
    }

    private static void ValidateAdapterInput(C3DHeightImageAlignmentInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.StepId);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.SelectionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.OutputEntityId);
        ArgumentNullException.ThrowIfNull(input.Reference);
        ArgumentNullException.ThrowIfNull(input.Moving);
        ArgumentNullException.ThrowIfNull(input.TemplateSelection);
        if (!Enum.IsDefined(input.Mode))
        {
            throw new ArgumentOutOfRangeException(nameof(input.Mode));
        }

        if (!string.Equals(input.Reference.ScalarMeaning, "raw-height", StringComparison.Ordinal)
            || !string.Equals(input.Moving.ScalarMeaning, "raw-height", StringComparison.Ordinal))
        {
            throw new InvalidDataException("Height-image alignment accepts raw-height snapshots only.");
        }

        if (!string.Equals(input.Reference.Unit, input.Moving.Unit, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Reference and moving height images must use the same height unit.");
        }

        if (!string.Equals(input.Reference.FrameId, input.Moving.FrameId, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Reference and moving height images must use the same source-grid frame.");
        }

        if (input.Reference.ValidCount <= 0 || input.Moving.ValidCount <= 0)
        {
            throw new InvalidDataException("Reference and moving height images must contain finite samples.");
        }

        if (input.TemplateSelection.Row < 0
            || input.TemplateSelection.Column < 0
            || input.TemplateSelection.RowCount < 8
            || input.TemplateSelection.ColumnCount < 8
            || input.TemplateSelection.Row + input.TemplateSelection.RowCount > input.Reference.Height
            || input.TemplateSelection.Column + input.TemplateSelection.ColumnCount > input.Reference.Width)
        {
            throw new InvalidDataException("Height-image alignment template selection must be at least 8x8 and inside the reference grid.");
        }

        if (!double.IsFinite(input.SearchScoreMinimum)
            || input.SearchScoreMinimum < 0d
            || input.SearchScoreMinimum > 1d)
        {
            throw new ArgumentOutOfRangeException(nameof(input.SearchScoreMinimum));
        }

        if (!double.IsFinite(input.AcceptanceScoreMinimumPercent)
            || input.AcceptanceScoreMinimumPercent < 0d
            || input.AcceptanceScoreMinimumPercent > 100d)
        {
            throw new ArgumentOutOfRangeException(nameof(input.AcceptanceScoreMinimumPercent));
        }

        if (!double.IsFinite(input.MinimumCandidateMarginPercent)
            || input.MinimumCandidateMarginPercent < 0d
            || input.MinimumCandidateMarginPercent > 100d)
        {
            throw new ArgumentOutOfRangeException(nameof(input.MinimumCandidateMarginPercent));
        }

        if (!double.IsFinite(input.AngleStepDegrees)
            || input.AngleStepDegrees <= 0d
            || input.AngleMinimumDegrees > input.AngleMaximumDegrees)
        {
            throw new ArgumentOutOfRangeException(nameof(input.AngleStepDegrees));
        }

        if (!double.IsFinite(input.RansacReprojectionThreshold)
            || input.RansacReprojectionThreshold <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(input.RansacReprojectionThreshold));
        }
    }
}
