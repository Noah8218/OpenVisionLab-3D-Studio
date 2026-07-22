using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using NoahGapFlushInspectionOptions = Lib.ThreeD.Inspection.GapFlushInspectionOptions;
using NoahGapFlushInspectionTool = Lib.ThreeD.Inspection.GapFlushInspectionTool;
using NoahGapFlushRegionStatistics = Lib.ThreeD.Inspection.GapFlushRegionStatistics;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;

namespace OpenVisionLab.ThreeD.Tools;

public sealed record ToolRecipeHeightMeasurementOutput(
    string OutputEntityId,
    string RootSourceEntityId,
    string InputEntityId,
    string SelectionId,
    string Unit,
    string FrameId,
    string ContentSha256,
    ToolResult Result,
    string EvidenceSummary);

public sealed record ToolRecipeHeightMeasurementEvaluation(
    ToolResult Result,
    ToolRecipeHeightMeasurementOutput? Output);

/// <summary>
/// Typed adapters that let scalar and plane-relative measurements participate as ordinary steps
/// in the canonical tool recipe. The first input can be either the verified raw
/// C3D HeightField or one exact Published TransformedHeightField. The ROI must
/// be bound to that same input identity.
/// </summary>
public static class ToolRecipeHeightMeasurementExecution
{
    private static readonly string[] ThicknessParameterNames =
        ["MinimumThickness", "MaximumThickness", "MinimumValidSampleCount"];
    private static readonly string[] WarpageParameterNames =
        ["MaximumPeakToValley", "MaximumRms", "MinimumValidSampleCount"];
    private static readonly string[] PlaneFlatnessParameterNames =
        ["MaximumFlatness", "MinimumReferenceSampleCount", "MinimumMeasurementSampleCount"];
    private static readonly string[] PointPairParameterNames =
        ["ExpectedDistance", "DistanceTolerance", "ExpectedPlanarWidth", "PlanarWidthTolerance", "ExpectedElevationAngleDegrees", "ElevationAngleToleranceDegrees"];
    private static readonly string[] GapFlushParameterNames =
        ["ExpectedGap", "GapTolerance", "ExpectedFlush", "FlushTolerance"];
    private static readonly string[] VolumeParameterNames =
        ["ExpectedNetVolume", "VolumeTolerance"];

    public static ToolRecipeHeightMeasurementEvaluation Execute(
        ToolRecipeDocument document,
        string stepId,
        string? recipeDirectory = null,
        CancellationToken cancellationToken = default) =>
        Execute(document, stepId, null, recipeDirectory, cancellationToken);

    public static ToolRecipeHeightMeasurementEvaluation Execute(
        ToolRecipeDocument document,
        string stepId,
        C3DTransformedHeightField? publishedTransformedHeightField,
        string? recipeDirectory = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryPrepare(document, stepId, publishedTransformedHeightField, recipeDirectory, out var prepared, out var message))
        {
            var error = new ToolResult("Height measurement", ResultStatus.Error, message, TimeSpan.Zero, [], []);
            return new ToolRecipeHeightMeasurementEvaluation(error, null);
        }

        cancellationToken.ThrowIfCancellationRequested();
        var step = prepared!.Step;
        ToolResult result;
        string evidence;
        if (string.Equals(step.ToolId, "thickness", StringComparison.Ordinal))
        {
            var minimum = ParseFinite(Parameter(step, "MinimumThickness"), "MinimumThickness");
            var maximum = ParseFinite(Parameter(step, "MaximumThickness"), "MaximumThickness");
            var minimumSamples = ParsePositiveInt(Parameter(step, "MinimumValidSampleCount"), "MinimumValidSampleCount", 1);
            var evaluation = C3DThicknessRule.Evaluate(new C3DThicknessInput(
                prepared.InputEntityId,
                prepared.Height,
                prepared.Width,
                prepared.Values,
                prepared.MeasurementRoi!,
                new C3DThicknessAcceptance(minimum, maximum),
                prepared.Unit,
                prepared.FrameId,
                minimumSamples));
            result = evaluation.Result;
            evidence = $"mean {evaluation.Mean:G6} | range {evaluation.Range:G6} | {evaluation.ValidSampleCount:N0} valid samples";
        }
        else if (string.Equals(step.ToolId, "warpage", StringComparison.Ordinal))
        {
            var maximumP2V = ParsePositive(Parameter(step, "MaximumPeakToValley"), "MaximumPeakToValley");
            var maximumRms = ParsePositive(Parameter(step, "MaximumRms"), "MaximumRms");
            var minimumSamples = ParsePositiveInt(Parameter(step, "MinimumValidSampleCount"), "MinimumValidSampleCount", 3);
            var evaluation = C3DWarpageRule.Evaluate(new C3DWarpageInput(
                prepared.InputEntityId,
                prepared.Height,
                prepared.Width,
                prepared.Values,
                prepared.MeasurementRoi!,
                new C3DWarpageAcceptance(maximumP2V, maximumRms),
                prepared.Unit,
                prepared.FrameId,
                minimumSamples));
            result = evaluation.Result;
            evidence = $"P2V {evaluation.PeakToValley:G6} | RMS {evaluation.Rms:G6} | {evaluation.ValidSampleCount:N0} valid samples";
        }
        else if (string.Equals(step.ToolId, "point-pair-dimensions", StringComparison.Ordinal))
        {
            var points = prepared.Selections.Single().Points!;
            var first = ReconstructPoint(prepared, points[0].Locator);
            var second = ReconstructPoint(prepared, points[1].Locator);
            var evaluation = PointPairDimensionsRule.Evaluate(new PointPairDimensionsInput(
                prepared.InputEntityId,
                first.Position,
                second.Position,
                first.Height,
                second.Height,
                new C3DPointPairDimensionsAcceptance(
                    ParseNonNegative(Parameter(step, "ExpectedDistance"), "ExpectedDistance"),
                    ParseNonNegative(Parameter(step, "DistanceTolerance"), "DistanceTolerance"),
                    ParseNonNegative(Parameter(step, "ExpectedPlanarWidth"), "ExpectedPlanarWidth"),
                    ParseNonNegative(Parameter(step, "PlanarWidthTolerance"), "PlanarWidthTolerance"),
                    ParseAngle(Parameter(step, "ExpectedElevationAngleDegrees"), "ExpectedElevationAngleDegrees"),
                    ParseNonNegative(Parameter(step, "ElevationAngleToleranceDegrees"), "ElevationAngleToleranceDegrees")),
                prepared.Unit,
                prepared.Unit,
                new Vector3(
                    (float)prepared.ReferenceGridProfile!.HAxis.X,
                    (float)prepared.ReferenceGridProfile.HAxis.Y,
                    (float)prepared.ReferenceGridProfile.HAxis.Z)));
            result = evaluation.Result;
            evidence = $"distance {evaluation.Distance:G6} | planar width {evaluation.PlanarWidth:G6} | elevation {evaluation.ElevationAngleDegrees:G6} degree | height delta {evaluation.RawHeightDelta:G6}";
        }
        else if (string.Equals(step.ToolId, "gap-flush", StringComparison.Ordinal))
        {
            var profile = prepared.ReferenceGridProfile!;
            var firstRoi = prepared.ReferenceRoi!;
            var secondRoi = prepared.MeasurementRoi!;
            var first = CreateGapFlushRegionStatistics(prepared, firstRoi);
            var second = CreateGapFlushRegionStatistics(prepared, secondRoi);
            if (first is null || second is null)
            {
                result = new ToolResult(
                    "Gap / Flush",
                    ResultStatus.Error,
                    "Both Gap / Flush ROIs require at least one finite height sample.",
                    TimeSpan.Zero,
                    [],
                    []);
                evidence = $"first {first?.SampleCount ?? 0:N0} | second {second?.SampleCount ?? 0:N0} finite samples";
            }
            else
            {
                var noah = new NoahGapFlushInspectionTool().Execute(
                    firstRoi.Column * profile.PitchU,
                    (firstRoi.Column + firstRoi.ColumnCount) * profile.PitchU,
                    secondRoi.Column * profile.PitchU,
                    (secondRoi.Column + secondRoi.ColumnCount) * profile.PitchU,
                    first,
                    second,
                    new NoahGapFlushInspectionOptions
                    {
                        ExpectedGap = ParseFinite(Parameter(step, "ExpectedGap"), "ExpectedGap"),
                        GapTolerance = ParseNonNegative(Parameter(step, "GapTolerance"), "GapTolerance"),
                        ExpectedFlush = ParseFinite(Parameter(step, "ExpectedFlush"), "ExpectedFlush"),
                        FlushTolerance = ParseNonNegative(Parameter(step, "FlushTolerance"), "FlushTolerance")
                    });
                var gapStatus = noah.GapPassed ? ResultStatus.Pass : ResultStatus.Fail;
                var flushStatus = noah.FlushPassed ? ResultStatus.Pass : ResultStatus.Fail;
                var status = noah.Passed ? ResultStatus.Pass : ResultStatus.Fail;
                result = new ToolResult(
                    "Gap / Flush",
                    status,
                    noah.Passed
                        ? "Signed U-axis gap and H-axis flush are within configured tolerances."
                        : "Signed U-axis gap or H-axis flush exceeds configured tolerance.",
                    TimeSpan.Zero,
                    [
                        new Metric("Signed gap", MetricKind.Length, noah.SignedGap, prepared.Unit, gapStatus),
                        new Metric("Signed flush", MetricKind.Deviation, noah.SignedFlush, prepared.Unit, flushStatus),
                        new Metric("First ROI samples", MetricKind.Count, noah.FirstSampleCount, "count"),
                        new Metric("Second ROI samples", MetricKind.Count, noah.SecondSampleCount, "count"),
                        new Metric("Expected gap", MetricKind.Length, ParseFinite(Parameter(step, "ExpectedGap"), "ExpectedGap"), prepared.Unit),
                        new Metric("Gap tolerance", MetricKind.Length, ParseNonNegative(Parameter(step, "GapTolerance"), "GapTolerance"), prepared.Unit),
                        new Metric("Expected flush", MetricKind.Deviation, ParseFinite(Parameter(step, "ExpectedFlush"), "ExpectedFlush"), prepared.Unit),
                        new Metric("Flush tolerance", MetricKind.Deviation, ParseNonNegative(Parameter(step, "FlushTolerance"), "FlushTolerance"), prepared.Unit)
                    ],
                    [
                        new Overlay("overlay.gap-flush.regions", OverlayKind.Box, "First and second artifact-owned Gap / Flush ROIs", status, prepared.InputEntityId),
                        new Overlay("overlay.gap-flush.gap", OverlayKind.Polyline, "Signed U-axis separation between facing ROI edges", gapStatus, prepared.InputEntityId),
                        new Overlay("overlay.gap-flush.flush", OverlayKind.Marker, "Signed mean-height difference along the reference H axis", flushStatus, prepared.InputEntityId)
                    ]);
                evidence = $"gap {noah.SignedGap:G6} | flush {noah.SignedFlush:G6} | first {noah.FirstSampleCount:N0} | second {noah.SecondSampleCount:N0} finite samples";
            }
        }
        else if (string.Equals(step.ToolId, "volume", StringComparison.Ordinal))
        {
            var referenceSamples = CreateReferenceAxisPlaneSamples(prepared, prepared.ReferenceRoi!);
            var measurementSamples = CreateReferenceAxisPlaneSamples(prepared, prepared.MeasurementRoi!);
            var evaluation = VolumeRule.Evaluate(new VolumeRuleInput(
                prepared.InputEntityId,
                referenceSamples,
                measurementSamples,
                prepared.ReferenceGridProfile!.PitchU * prepared.ReferenceGridProfile.PitchV,
                ParseFinite(Parameter(step, "ExpectedNetVolume"), "ExpectedNetVolume"),
                ParseNonNegative(Parameter(step, "VolumeTolerance"), "VolumeTolerance"),
                $"{prepared.Unit}^3"));
            result = evaluation.Result;
            evidence = $"net {evaluation.NetVolume:G6} | above {evaluation.AboveVolume:G6} | below {evaluation.BelowVolume:G6} | reference {evaluation.ReferenceSampleCount:N0} | measurement {evaluation.MeasurementSampleCount:N0}";
        }
        else
        {
            var tolerance = ParsePositive(Parameter(step, "MaximumFlatness"), "MaximumFlatness");
            var minimumReferenceSamples = ParsePositiveInt(Parameter(step, "MinimumReferenceSampleCount"), "MinimumReferenceSampleCount", 3);
            var minimumMeasurementSamples = ParsePositiveInt(Parameter(step, "MinimumMeasurementSampleCount"), "MinimumMeasurementSampleCount", 3);
            var referenceSamples = CreatePlaneSamples(prepared, prepared.ReferenceRoi!);
            var measurementSamples = CreatePlaneSamples(prepared, prepared.MeasurementRoi!);
            if (referenceSamples.Count < minimumReferenceSamples || measurementSamples.Count < minimumMeasurementSamples)
            {
                result = new ToolResult(
                    PlaneFlatnessRule.ToolName,
                    ResultStatus.Error,
                    $"Plane Flatness requires at least {minimumReferenceSamples} finite reference and {minimumMeasurementSamples} finite measurement samples; found {referenceSamples.Count} and {measurementSamples.Count}.",
                    TimeSpan.Zero,
                    [],
                    []);
                evidence = $"reference {referenceSamples.Count:N0} | measurement {measurementSamples.Count:N0} finite samples";
            }
            else
            {
                var evaluation = PlaneFlatnessRule.Evaluate(new PlaneFlatnessRuleInput(
                    prepared.InputEntityId, referenceSamples, measurementSamples, tolerance, prepared.Unit));
                result = evaluation.Result;
                evidence = $"flatness {evaluation.Flatness:G6} | RMS {evaluation.RootMeanSquareDistance:G6} | reference {evaluation.ReferenceSampleCount:N0} | measurement {evaluation.MeasurementSampleCount:N0}";
            }
        }

        var hash = CalculateHash(step, prepared.InputContentSha256, prepared.Selections);
        var output = new ToolRecipeHeightMeasurementOutput(
            step.OutputEntityId,
            document.Source.Id,
            prepared.InputEntityId,
            string.Join(";", prepared.Selections.Select(selection => selection.Id)),
            prepared.Unit,
            prepared.FrameId,
            hash,
            result,
            evidence);
        return new ToolRecipeHeightMeasurementEvaluation(result, output);
    }

    public static bool TryPrepare(
        ToolRecipeDocument document,
        string stepId,
        string? recipeDirectory,
        out PreparedHeightMeasurement? prepared,
        out string message) =>
        TryPrepare(document, stepId, null, recipeDirectory, out prepared, out message);

    public static bool TryPrepare(
        ToolRecipeDocument document,
        string stepId,
        C3DTransformedHeightField? publishedTransformedHeightField,
        string? recipeDirectory,
        out PreparedHeightMeasurement? prepared,
        out string message)
    {
        prepared = null;
        try
        {
            ArgumentNullException.ThrowIfNull(document);
            ArgumentException.ThrowIfNullOrWhiteSpace(stepId);
            var validation = ToolRecipeValidator.Validate(document);
            if (!validation.IsValid) throw new InvalidDataException(string.Join(" ", validation.Errors));
            var step = document.Steps.SingleOrDefault(candidate =>
                string.Equals(candidate.Id, stepId, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidDataException($"Inspection recipe must contain exactly one step with ID '{stepId}'.");
            if (step.ToolId is not ("thickness" or "warpage" or "plane-flatness" or "point-pair-dimensions" or "gap-flush" or "volume"))
            {
                throw new InvalidDataException($"Step '{step.Id}' is not a supported height measurement adapter.");
            }
            var twoRoi = step.ToolId is "plane-flatness" or "gap-flush" or "volume";
            var expectedInputCount = twoRoi ? 3 : 2;
            if (step.InputEntityIds.Count != expectedInputCount)
            {
                throw new InvalidDataException(twoRoi
                    ? $"{step.ToolName} v1 requires one TransformedHeightField and two ordered GridRectangles."
                    : $"{step.ToolName} v1 requires one HeightField first and one GridRectangle second.");
            }
            var selections = step.InputEntityIds.Skip(1).Select(inputId =>
                (document.Selections ?? []).SingleOrDefault(candidate =>
                    string.Equals(candidate.Id, inputId, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidDataException($"{step.ToolName} v1 requires recipe-owned selection inputs.")).ToArray();
            var pointPair = step.ToolId == "point-pair-dimensions";
            if (pointPair
                ? selections.Length != 1 || selections[0].Kind != ToolRecipeSelectionKinds.PointSet || selections[0].Points?.Count != 2
                : selections.Any(selection => selection.Kind != ToolRecipeSelectionKinds.GridRectangle || selection.GridRectangle is null))
            {
                throw new InvalidDataException(pointPair
                    ? $"{step.ToolName} v1 requires one ordered PointSet(2)."
                    : $"{step.ToolName} v1 selection inputs must be GridRectangles.");
            }
            ValidateParameters(step);
            var rois = pointPair ? [] : selections.Select(selection => ToRoi(selection.GridRectangle!)).ToArray();
            if (string.Equals(step.InputEntityIds[0], document.Source.Id, StringComparison.OrdinalIgnoreCase))
            {
                if (step.ToolId is "plane-flatness" or "point-pair-dimensions" or "gap-flush" or "volume")
                {
                    throw new InvalidDataException($"{step.ToolName} v1 requires a Published TransformedHeightField with an explicit reference frame and unit.");
                }
                var source = document.Source;
                if (!string.Equals(source.Format, "C3D", StringComparison.OrdinalIgnoreCase)
                    || source.ByteLength is null || string.IsNullOrWhiteSpace(source.ContentSha256)
                    || source.GridWidth is null || source.GridHeight is null)
                {
                    throw new InvalidDataException($"{step.ToolName} v1 requires a complete recipe-bound C3D source identity.");
                }
                var path = Path.IsPathFullyQualified(source.Path)
                    ? Path.GetFullPath(source.Path)
                    : Path.GetFullPath(Path.Combine(recipeDirectory ?? Environment.CurrentDirectory, source.Path));
                var snapshot = C3DHeightFieldSnapshot.LoadVerified(
                    path, source.Id, source.Unit, source.FrameId, source.ByteLength.Value,
                    source.ContentSha256, source.GridWidth.Value, source.GridHeight.Value);
                prepared = new PreparedHeightMeasurement(
                    step, selections, snapshot.EntityId, snapshot.ContentSha256, snapshot.Unit, snapshot.FrameId,
                    snapshot.Height, snapshot.Width, snapshot.Values.ToArray(), null, null, rois[0]);
                message = $"{step.ToolName} v1 is ready from the verified raw C3D and source-owned GridRectangle.";
                return true;
            }

            if (publishedTransformedHeightField is null
                || !string.Equals(step.InputEntityIds[0], publishedTransformedHeightField.OutputEntityId, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"{step.ToolName} v1 is waiting for its exact Published TransformedHeightField first input.");
            }
            foreach (var selection in selections)
            {
                var binding = ToolRecipeSelectionSourceBindingVerifier.Verify(publishedTransformedHeightField, selection.SourceBinding);
                if (!binding.IsCurrent) throw new InvalidDataException(binding.Message);
            }
            prepared = new PreparedHeightMeasurement(
                step,
                selections,
                publishedTransformedHeightField.OutputEntityId,
                publishedTransformedHeightField.ContentSha256,
                publishedTransformedHeightField.ReferenceUnit,
                publishedTransformedHeightField.ReferenceFrameId,
                publishedTransformedHeightField.RowCount,
                publishedTransformedHeightField.ColumnCount,
                publishedTransformedHeightField.Cells.Select(cell => cell.HasValue ? cell.Height : double.NaN).ToArray(),
                publishedTransformedHeightField.ReferenceGridProfile,
                twoRoi ? rois[0] : null,
                pointPair ? null : rois[^1]);
            message = $"{step.ToolName} v1 is ready from the exact Published TransformedHeightField and {selections.Length} artifact-owned selection input(s).";
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidDataException or InvalidOperationException or OverflowException)
        {
            message = exception.Message;
            return false;
        }
    }

    public sealed record PreparedHeightMeasurement(
        ToolRecipeStep Step,
        IReadOnlyList<ToolRecipeSelection> Selections,
        string InputEntityId,
        string InputContentSha256,
        string Unit,
        string FrameId,
        int Height,
        int Width,
        double[] Values,
        C3DReferenceGridProfile? ReferenceGridProfile,
        C3DGridRoi? ReferenceRoi,
        C3DGridRoi? MeasurementRoi);

    private static void ValidateParameters(ToolRecipeStep step)
    {
        var expected = step.ToolId switch
        {
            "thickness" => ThicknessParameterNames,
            "warpage" => WarpageParameterNames,
            "point-pair-dimensions" => PointPairParameterNames,
            "gap-flush" => GapFlushParameterNames,
            "volume" => VolumeParameterNames,
            _ => PlaneFlatnessParameterNames
        };
        var parameters = step.Parameters ?? [];
        if (parameters.Count != expected.Length || expected.Any(name => parameters.Count(parameter => parameter.Name == name) != 1))
        {
            throw new InvalidDataException($"{step.ToolName} v1 requires exactly one value for every recognized parameter and no unknown parameters.");
        }
        if (step.ToolId == "thickness")
        {
            var minimum = ParseFinite(Parameter(step, "MinimumThickness"), "MinimumThickness");
            var maximum = ParseFinite(Parameter(step, "MaximumThickness"), "MaximumThickness");
            if (minimum > maximum) throw new InvalidDataException("MinimumThickness must not exceed MaximumThickness.");
            _ = ParsePositiveInt(Parameter(step, "MinimumValidSampleCount"), "MinimumValidSampleCount", 1);
        }
        else if (step.ToolId == "warpage")
        {
            _ = ParsePositive(Parameter(step, "MaximumPeakToValley"), "MaximumPeakToValley");
            _ = ParsePositive(Parameter(step, "MaximumRms"), "MaximumRms");
            _ = ParsePositiveInt(Parameter(step, "MinimumValidSampleCount"), "MinimumValidSampleCount", 3);
        }
        else if (step.ToolId == "point-pair-dimensions")
        {
            _ = ParseNonNegative(Parameter(step, "ExpectedDistance"), "ExpectedDistance");
            _ = ParseNonNegative(Parameter(step, "DistanceTolerance"), "DistanceTolerance");
            _ = ParseNonNegative(Parameter(step, "ExpectedPlanarWidth"), "ExpectedPlanarWidth");
            _ = ParseNonNegative(Parameter(step, "PlanarWidthTolerance"), "PlanarWidthTolerance");
            _ = ParseAngle(Parameter(step, "ExpectedElevationAngleDegrees"), "ExpectedElevationAngleDegrees");
            _ = ParseNonNegative(Parameter(step, "ElevationAngleToleranceDegrees"), "ElevationAngleToleranceDegrees");
        }
        else if (step.ToolId == "gap-flush")
        {
            _ = ParseFinite(Parameter(step, "ExpectedGap"), "ExpectedGap");
            _ = ParseNonNegative(Parameter(step, "GapTolerance"), "GapTolerance");
            _ = ParseFinite(Parameter(step, "ExpectedFlush"), "ExpectedFlush");
            _ = ParseNonNegative(Parameter(step, "FlushTolerance"), "FlushTolerance");
        }
        else if (step.ToolId == "volume")
        {
            _ = ParseFinite(Parameter(step, "ExpectedNetVolume"), "ExpectedNetVolume");
            _ = ParseNonNegative(Parameter(step, "VolumeTolerance"), "VolumeTolerance");
        }
        else
        {
            _ = ParsePositive(Parameter(step, "MaximumFlatness"), "MaximumFlatness");
            _ = ParsePositiveInt(Parameter(step, "MinimumReferenceSampleCount"), "MinimumReferenceSampleCount", 3);
            _ = ParsePositiveInt(Parameter(step, "MinimumMeasurementSampleCount"), "MinimumMeasurementSampleCount", 3);
        }
    }

    private static string Parameter(ToolRecipeStep step, string name) =>
        step.Parameters.Single(parameter => parameter.Name == name).Value;

    private static double ParseFinite(string value, string name)
    {
        if (value != value.Trim() || value.Contains(',', StringComparison.Ordinal)
            || !double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            || !double.IsFinite(parsed))
        {
            throw new InvalidDataException($"{name} must be an invariant finite number.");
        }
        return parsed;
    }

    private static double ParsePositive(string value, string name)
    {
        var parsed = ParseFinite(value, name);
        if (parsed <= 0d) throw new InvalidDataException($"{name} must be greater than zero.");
        return parsed;
    }

    private static double ParseNonNegative(string value, string name)
    {
        var parsed = ParseFinite(value, name);
        if (parsed < 0d) throw new InvalidDataException($"{name} must be zero or greater.");
        return parsed;
    }

    private static double ParseAngle(string value, string name)
    {
        var parsed = ParseFinite(value, name);
        if (parsed is < -90d or > 90d) throw new InvalidDataException($"{name} must be between -90 and 90 degrees.");
        return parsed;
    }

    private static int ParsePositiveInt(string value, string name, int minimum)
    {
        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) || parsed < minimum)
        {
            throw new InvalidDataException($"{name} must be an invariant integer no less than {minimum}.");
        }
        return parsed;
    }

    private static C3DGridRoi ToRoi(ToolRecipeGridRectangle rectangle) =>
        new(rectangle.Row, rectangle.Column, rectangle.RowCount, rectangle.ColumnCount);

    private static IReadOnlyList<HeightFieldPlaneSample> CreatePlaneSamples(PreparedHeightMeasurement prepared, C3DGridRoi roi)
    {
        var profile = prepared.ReferenceGridProfile
            ?? throw new InvalidDataException("Plane Flatness requires a reference-grid profile.");
        var samples = new List<HeightFieldPlaneSample>();
        for (var row = roi.Row; row < roi.Row + roi.RowCount; row++)
        {
            for (var column = roi.Column; column < roi.Column + roi.ColumnCount; column++)
            {
                var height = prepared.Values[row * prepared.Width + column];
                if (!double.IsFinite(height)) continue;
                var u = (column + 0.5d) * profile.PitchU;
                var v = (row + 0.5d) * profile.PitchV;
                var x = profile.Origin.X + profile.UAxis.X * u + profile.VAxis.X * v + profile.HAxis.X * height;
                var y = profile.Origin.Y + profile.UAxis.Y * u + profile.VAxis.Y * v + profile.HAxis.Y * height;
                var z = profile.Origin.Z + profile.UAxis.Z * u + profile.VAxis.Z * v + profile.HAxis.Z * height;
                if (x is < float.MinValue or > float.MaxValue || y is < float.MinValue or > float.MaxValue || z is < float.MinValue or > float.MaxValue)
                {
                    throw new InvalidDataException("Plane Flatness reconstructed point exceeds the supported single-precision geometry range.");
                }
                samples.Add(new HeightFieldPlaneSample(new Vector3((float)x, (float)y, (float)z), height));
            }
        }
        return samples;
    }

    private static IReadOnlyList<HeightFieldPlaneSample> CreateReferenceAxisPlaneSamples(
        PreparedHeightMeasurement prepared,
        C3DGridRoi roi)
    {
        var profile = prepared.ReferenceGridProfile
            ?? throw new InvalidDataException("Volume requires a reference-grid profile.");
        var samples = new List<HeightFieldPlaneSample>();
        for (var row = roi.Row; row < roi.Row + roi.RowCount; row++)
        {
            for (var column = roi.Column; column < roi.Column + roi.ColumnCount; column++)
            {
                var height = prepared.Values[row * prepared.Width + column];
                if (!double.IsFinite(height)) continue;
                var u = (column + 0.5d) * profile.PitchU;
                var v = (row + 0.5d) * profile.PitchV;
                if (u is < float.MinValue or > float.MaxValue || v is < float.MinValue or > float.MaxValue
                    || height is < float.MinValue or > float.MaxValue)
                {
                    throw new InvalidDataException("Volume reference-axis sample exceeds the supported single-precision geometry range.");
                }
                samples.Add(new HeightFieldPlaneSample(new Vector3((float)u, (float)height, (float)v), height));
            }
        }
        return samples;
    }

    private static NoahGapFlushRegionStatistics? CreateGapFlushRegionStatistics(
        PreparedHeightMeasurement prepared,
        C3DGridRoi roi)
    {
        var count = 0;
        var sum = 0d;
        for (var row = roi.Row; row < roi.Row + roi.RowCount; row++)
        {
            for (var column = roi.Column; column < roi.Column + roi.ColumnCount; column++)
            {
                var value = prepared.Values[row * prepared.Width + column];
                if (!double.IsFinite(value)) continue;
                count++;
                sum += value;
            }
        }
        return count == 0 ? null : new NoahGapFlushRegionStatistics(count, sum / count, sum / count);
    }

    private static (Vector3 Position, double Height) ReconstructPoint(
        PreparedHeightMeasurement prepared,
        ToolRecipeGridCellLocator locator)
    {
        if (locator.Row < 0 || locator.Row >= prepared.Height || locator.Column < 0 || locator.Column >= prepared.Width)
        {
            throw new InvalidDataException("Point Pair locator is outside the transformed height field.");
        }
        var height = prepared.Values[locator.Row * prepared.Width + locator.Column];
        if (!double.IsFinite(height)) throw new InvalidDataException("Point Pair locator resolves to a missing height cell.");
        var profile = prepared.ReferenceGridProfile
            ?? throw new InvalidDataException("Point Pair requires a reference-grid profile.");
        var u = (locator.Column + 0.5d) * profile.PitchU;
        var v = (locator.Row + 0.5d) * profile.PitchV;
        var x = profile.Origin.X + profile.UAxis.X * u + profile.VAxis.X * v + profile.HAxis.X * height;
        var y = profile.Origin.Y + profile.UAxis.Y * u + profile.VAxis.Y * v + profile.HAxis.Y * height;
        var z = profile.Origin.Z + profile.UAxis.Z * u + profile.VAxis.Z * v + profile.HAxis.Z * height;
        if (x is < float.MinValue or > float.MaxValue || y is < float.MinValue or > float.MaxValue || z is < float.MinValue or > float.MaxValue)
        {
            throw new InvalidDataException("Point Pair reconstructed point exceeds the supported single-precision geometry range.");
        }
        return (new Vector3((float)x, (float)y, (float)z), height);
    }

    private static string CalculateHash(ToolRecipeStep step, string inputHash, IReadOnlyList<ToolRecipeSelection> selections)
    {
        var canonical = new StringBuilder()
            .Append(step.ToolId).Append('|').Append(step.OutputEntityId).Append('|')
            .Append(inputHash.ToUpperInvariant());
        foreach (var selection in selections)
        {
            canonical.Append('|').Append(selection.Id).Append('|').Append(selection.Kind);
            if (selection.GridRectangle is { } rectangle)
            {
                canonical.Append('|').Append(rectangle.Row).Append(',').Append(rectangle.Column).Append(',')
                    .Append(rectangle.RowCount).Append(',').Append(rectangle.ColumnCount);
            }
            foreach (var point in selection.Points ?? [])
            {
                canonical.Append('|').Append(point.Locator.Row).Append(',').Append(point.Locator.Column);
            }
        }
        foreach (var parameter in step.Parameters.OrderBy(parameter => parameter.Name, StringComparer.Ordinal))
        {
            canonical.Append('|').Append(parameter.Name).Append('=').Append(parameter.Value);
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())));
    }
}
