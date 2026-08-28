using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using SdkGapFlushInspectionOptions = OpenVisionLab.Vision3D.Inspection.GapFlushInspectionOptions;
using SdkGapFlushInspectionTool = OpenVisionLab.Vision3D.Inspection.GapFlushInspectionTool;
using SdkGapFlushRegionStatistics = OpenVisionLab.Vision3D.Inspection.GapFlushRegionStatistics;
using SdkCrossSectionDimensionsInspectionOptions = OpenVisionLab.Vision3D.Inspection.CrossSectionDimensionsInspectionOptions;
using SdkCrossSectionDimensionsInspectionTool = OpenVisionLab.Vision3D.Inspection.CrossSectionDimensionsInspectionTool;
using SdkCrossSectionDimensionsSample = OpenVisionLab.Vision3D.Inspection.CrossSectionDimensionsSample;
using SdkHeightGridRegion = OpenVisionLab.Vision3D.FeatureExtraction.HeightGridRegion;
using SdkHeightMapRegionStatisticsTool = OpenVisionLab.Vision3D.FeatureExtraction.HeightMapRegionStatisticsTool;
using SdkReferenceGridCoordinateMode = OpenVisionLab.Vision3D.FeatureExtraction.ReferenceGridCoordinateMode;
using SdkReferenceGridDefinition = OpenVisionLab.Vision3D.FeatureExtraction.ReferenceGridDefinition;
using SdkReferenceGridPointReconstructionOptions = OpenVisionLab.Vision3D.FeatureExtraction.ReferenceGridPointReconstructionOptions;
using SdkReferenceGridPointReconstructionTool = OpenVisionLab.Vision3D.FeatureExtraction.ReferenceGridPointReconstructionTool;
using SdkReferenceGridVector = OpenVisionLab.Vision3D.FeatureExtraction.ReferenceGridVector;
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
    string EvidenceSummary,
    C3DCompletenessGridMetricOutput? CompletenessGrid = null);

public sealed record ToolRecipeHeightMeasurementEvaluation(
    ToolResult Result,
    ToolRecipeHeightMeasurementOutput? Output);

/// <summary>
/// Typed adapters that let scalar and plane-relative measurements participate as ordinary steps
/// in the canonical tool recipe. The first input can be either the verified raw
/// C3D HeightField or one exact Published HeightField artifact. The ROI must
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
    private static readonly string[] CrossSectionParameterNames =
        ["ExpectedWidth", "WidthTolerance", "ExpectedHeightRange", "HeightTolerance"];

    public static ToolRecipeHeightMeasurementEvaluation Execute(
        ToolRecipeDocument document,
        string stepId,
        string? recipeDirectory = null,
        CancellationToken cancellationToken = default) =>
        Execute(document, stepId, null, null, null, recipeDirectory, cancellationToken);

    public static ToolRecipeHeightMeasurementEvaluation Execute(
        ToolRecipeDocument document,
        string stepId,
        C3DTransformedHeightField? publishedTransformedHeightField,
        string? recipeDirectory = null,
        CancellationToken cancellationToken = default) =>
        Execute(document, stepId, null, publishedTransformedHeightField, null, recipeDirectory, cancellationToken);

    public static ToolRecipeHeightMeasurementEvaluation Execute(
        ToolRecipeDocument document,
        string stepId,
        C3DHeightFieldSnapshot? publishedHeightField,
        C3DTransformedHeightField? publishedTransformedHeightField,
        string? recipeDirectory = null,
        CancellationToken cancellationToken = default)
        => Execute(
            document,
            stepId,
            publishedHeightField,
            publishedTransformedHeightField,
            null,
            recipeDirectory,
            cancellationToken);

    public static ToolRecipeHeightMeasurementEvaluation Execute(
        ToolRecipeDocument document,
        string stepId,
        C3DHeightFieldSnapshot? publishedHeightField,
        C3DTransformedHeightField? publishedTransformedHeightField,
        C3DEditableRegionArtifact? publishedEditableRegionArtifact,
        string? recipeDirectory = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryPrepare(
                document,
                stepId,
                publishedHeightField,
                publishedTransformedHeightField,
                publishedEditableRegionArtifact,
                recipeDirectory,
                out var prepared,
                out var message))
        {
            var error = new ToolResult("Height measurement", ResultStatus.Error, message, TimeSpan.Zero, [], []);
            return new ToolRecipeHeightMeasurementEvaluation(error, null);
        }

        cancellationToken.ThrowIfCancellationRequested();
        var step = prepared!.Step;
        ToolResult result;
        string evidence;
        C3DCompletenessGridMetricOutput? completenessGrid = null;
        if (string.Equals(step.ToolId, "completeness-grid", StringComparison.Ordinal))
        {
            var profile = C3DCompletenessGridProfile.FromRecipeParameters(
                step.Parameters ?? []);
            var presencePolicy =
                C3DCompletenessPresencePolicy.FromOptionalRecipeParameters(
                    step.Parameters ?? []);
            var evaluation = C3DCompletenessGridRule.Evaluate(
                new C3DCompletenessGridInput(
                    step.OutputEntityId,
                    document.Source.Id,
                    prepared.InputEntityId,
                    prepared.InputContentSha256,
                    prepared.Unit,
                    prepared.FrameId,
                    prepared.Width,
                    prepared.Height,
                    prepared.Values,
                    prepared.Selections[0],
                    prepared.Selections[1],
                    profile,
                    presencePolicy,
                    prepared.InspectionRegionArtifact));
            if (evaluation.Output is null)
            {
                return new ToolRecipeHeightMeasurementEvaluation(
                    evaluation.Result,
                    null);
            }

            completenessGrid = evaluation.Output;
            result = evaluation.Result;
            var minimumCoverage = completenessGrid.Cells.Min(
                cell => cell.FiniteCoverageRatio);
            var cellsWithMissing = completenessGrid.Cells.Count(
                cell => cell.MissingCellCount > 0);
            evidence = completenessGrid.PresencePolicy is null
                ? $"{completenessGrid.Cells.Count} deterministic cells | "
                  + $"minimum finite coverage {minimumCoverage:P1} | "
                  + $"{cellsWithMissing} cell(s) contain missing samples | "
                  + $"reference mean {completenessGrid.ReferenceMeanRawHeight:G6} {prepared.Unit} | "
                  + "evidence only, no acceptance policy"
                : $"{completenessGrid.Cells.Count} deterministic cells | "
                  + $"pass {completenessGrid.PassedCellCount} | "
                  + $"fail {completenessGrid.FailedCellCount} | "
                  + $"aggregate {completenessGrid.AggregateStatus} | "
                  + $"minimum finite coverage {minimumCoverage:P1} | "
                  + $"reference mean {completenessGrid.ReferenceMeanRawHeight:G6} {prepared.Unit}";
        }
        else if (string.Equals(step.ToolId, "thickness", StringComparison.Ordinal))
        {
            var minimum = ParseFinite(Parameter(step, "MinimumThickness"), "MinimumThickness");
            var maximum = ParseFinite(Parameter(step, "MaximumThickness"), "MaximumThickness");
            var minimumSamples = ParsePositiveInt(Parameter(step, "MinimumValidSampleCount"), "MinimumValidSampleCount", 1);
            var referenceSamples = CreateReferenceAxisPlaneSamples(prepared, prepared.ReferenceRoi!, "Thickness");
            var measurementSamples = CreateReferenceAxisPlaneSamples(prepared, prepared.MeasurementRoi!, "Thickness");
            var evaluation = DualSurfaceThicknessRule.Evaluate(new DualSurfaceThicknessInput(
                prepared.InputEntityId,
                referenceSamples,
                measurementSamples,
                minimum,
                maximum,
                minimumSamples,
                prepared.Unit));
            result = evaluation.Result;
            evidence = $"H-axis thickness mean {evaluation.Mean:G6} | min {evaluation.Minimum:G6} | max {evaluation.Maximum:G6} | reference {evaluation.ReferenceSampleCount:N0} | measurement {evaluation.MeasurementSampleCount:N0} finite samples";
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
                var sdk = new SdkGapFlushInspectionTool().Execute(
                    firstRoi.Column * profile.PitchU,
                    (firstRoi.Column + firstRoi.ColumnCount) * profile.PitchU,
                    secondRoi.Column * profile.PitchU,
                    (secondRoi.Column + secondRoi.ColumnCount) * profile.PitchU,
                    first,
                    second,
                    new SdkGapFlushInspectionOptions
                    {
                        ExpectedGap = ParseFinite(Parameter(step, "ExpectedGap"), "ExpectedGap"),
                        GapTolerance = ParseNonNegative(Parameter(step, "GapTolerance"), "GapTolerance"),
                        ExpectedFlush = ParseFinite(Parameter(step, "ExpectedFlush"), "ExpectedFlush"),
                        FlushTolerance = ParseNonNegative(Parameter(step, "FlushTolerance"), "FlushTolerance")
                    });
                var gapStatus = sdk.GapPassed ? ResultStatus.Pass : ResultStatus.Fail;
                var flushStatus = sdk.FlushPassed ? ResultStatus.Pass : ResultStatus.Fail;
                var status = sdk.Passed ? ResultStatus.Pass : ResultStatus.Fail;
                result = new ToolResult(
                    "Gap / Flush",
                    status,
                    sdk.Passed
                        ? "Signed U-axis gap and H-axis flush are within configured tolerances."
                        : "Signed U-axis gap or H-axis flush exceeds configured tolerance.",
                    TimeSpan.Zero,
                    [
                        new Metric("Signed gap", MetricKind.Length, sdk.SignedGap, prepared.Unit, gapStatus),
                        new Metric("Signed flush", MetricKind.Deviation, sdk.SignedFlush, prepared.Unit, flushStatus),
                        new Metric("First ROI samples", MetricKind.Count, sdk.FirstSampleCount, "count"),
                        new Metric("Second ROI samples", MetricKind.Count, sdk.SecondSampleCount, "count"),
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
                evidence = $"gap {sdk.SignedGap:G6} | flush {sdk.SignedFlush:G6} | first {sdk.FirstSampleCount:N0} | second {sdk.SecondSampleCount:N0} finite samples";
            }
        }
        else if (string.Equals(step.ToolId, "volume", StringComparison.Ordinal))
        {
            var referenceSamples = CreateReferenceAxisPlaneSamples(prepared, prepared.ReferenceRoi!, "Volume");
            var measurementSamples = CreateReferenceAxisPlaneSamples(prepared, prepared.MeasurementRoi!, "Volume");
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
        else if (string.Equals(step.ToolId, "cross-section-dimensions", StringComparison.Ordinal))
        {
            var roi = prepared.MeasurementRoi!;
            var samples = CreateCrossSectionSamples(prepared, roi);
            var expectedWidth = ParseNonNegative(Parameter(step, "ExpectedWidth"), "ExpectedWidth");
            var widthTolerance = ParseNonNegative(Parameter(step, "WidthTolerance"), "WidthTolerance");
            var expectedHeightRange = ParseNonNegative(Parameter(step, "ExpectedHeightRange"), "ExpectedHeightRange");
            var heightTolerance = ParseNonNegative(Parameter(step, "HeightTolerance"), "HeightTolerance");
            var sdk = new SdkCrossSectionDimensionsInspectionTool().Execute(
                samples,
                new SdkCrossSectionDimensionsInspectionOptions
                {
                    ExpectedWidth = expectedWidth,
                    WidthTolerance = widthTolerance,
                    ExpectedHeightRange = expectedHeightRange,
                    HeightTolerance = heightTolerance
                });
            var widthStatus = sdk.WidthPassed ? ResultStatus.Pass : ResultStatus.Fail;
            var heightStatus = sdk.HeightPassed ? ResultStatus.Pass : ResultStatus.Fail;
            var status = sdk.Passed ? ResultStatus.Pass : ResultStatus.Fail;
            result = new ToolResult(
                "Cross-section Dimensions",
                status,
                sdk.Passed
                    ? "A3 U-axis width and H-axis range are within configured tolerances."
                    : "A3 U-axis width or H-axis range exceeds its configured tolerance.",
                TimeSpan.Zero,
                [
                    new Metric("Section width", MetricKind.Length, sdk.Width, prepared.Unit, widthStatus),
                    new Metric("H range", MetricKind.Deviation, sdk.HeightRange, prepared.Unit, heightStatus),
                    new Metric("H minimum", MetricKind.Number, sdk.HeightMinimum, prepared.Unit),
                    new Metric("H maximum", MetricKind.Number, sdk.HeightMaximum, prepared.Unit),
                    new Metric("Valid section samples", MetricKind.Count, sdk.SampleCount, "count")
                ],
                [
                    new Overlay("overlay.cross-section.row", OverlayKind.Polyline, "Artifact-owned A3 row segment", status, prepared.InputEntityId),
                    new Overlay("overlay.cross-section.width", OverlayKind.Polyline, "U-axis width span", widthStatus, prepared.InputEntityId),
                    new Overlay("overlay.cross-section.height", OverlayKind.Marker, "H-axis extrema", heightStatus, prepared.InputEntityId)
                ]);
            evidence = $"width {sdk.Width:G6} | H range {sdk.HeightRange:G6} | minimum {sdk.HeightMinimum:G6} | maximum {sdk.HeightMaximum:G6} | {sdk.SampleCount:N0} finite samples";
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

        var hash = completenessGrid?.ContentSha256
            ?? CalculateHash(step, prepared.InputContentSha256, prepared.Selections);
        var output = new ToolRecipeHeightMeasurementOutput(
            step.OutputEntityId,
            document.Source.Id,
            prepared.InputEntityId,
            string.Join(";", prepared.Selections.Select(selection => selection.Id)),
            prepared.Unit,
            prepared.FrameId,
            hash,
            result,
            evidence,
            completenessGrid);
        return new ToolRecipeHeightMeasurementEvaluation(result, output);
    }

    public static bool TryPrepare(
        ToolRecipeDocument document,
        string stepId,
        string? recipeDirectory,
        out PreparedHeightMeasurement? prepared,
        out string message) =>
        TryPrepare(document, stepId, null, null, null, recipeDirectory, out prepared, out message);

    public static bool TryPrepare(
        ToolRecipeDocument document,
        string stepId,
        C3DTransformedHeightField? publishedTransformedHeightField,
        string? recipeDirectory,
        out PreparedHeightMeasurement? prepared,
        out string message) =>
        TryPrepare(document, stepId, null, publishedTransformedHeightField, null, recipeDirectory, out prepared, out message);

    public static bool TryPrepare(
        ToolRecipeDocument document,
        string stepId,
        C3DHeightFieldSnapshot? publishedHeightField,
        C3DTransformedHeightField? publishedTransformedHeightField,
        string? recipeDirectory,
        out PreparedHeightMeasurement? prepared,
        out string message)
        => TryPrepare(
            document,
            stepId,
            publishedHeightField,
            publishedTransformedHeightField,
            null,
            recipeDirectory,
            out prepared,
            out message);

    public static bool TryPrepare(
        ToolRecipeDocument document,
        string stepId,
        C3DHeightFieldSnapshot? publishedHeightField,
        C3DTransformedHeightField? publishedTransformedHeightField,
        C3DEditableRegionArtifact? publishedEditableRegionArtifact,
        string? recipeDirectory,
        out PreparedHeightMeasurement? prepared,
        out string message)
    {
        prepared = null;
        try
        {
            ArgumentNullException.ThrowIfNull(document);
            ArgumentException.ThrowIfNullOrWhiteSpace(stepId);
            var validation = ToolRecipeValidator.ValidateForStepExecution(document, stepId);
            if (!validation.IsValid) throw new InvalidDataException(string.Join(" ", validation.Errors));
            var step = document.Steps.SingleOrDefault(candidate =>
                string.Equals(candidate.Id, stepId, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidDataException($"Inspection recipe must contain exactly one step with ID '{stepId}'.");
            if (step.ToolId is not ("thickness" or "warpage" or "plane-flatness" or "point-pair-dimensions" or "gap-flush" or "volume" or "cross-section-dimensions" or "completeness-grid"))
            {
                throw new InvalidDataException($"Step '{step.Id}' is not a supported height measurement adapter.");
            }
            var twoRoi = step.ToolId is "thickness" or "plane-flatness" or "gap-flush" or "volume" or "completeness-grid";
            var expectedInputCount = twoRoi ? 3 : 2;
            if (step.InputEntityIds.Count != expectedInputCount)
            {
                if (step.ToolId == "thickness" && step.InputEntityIds.Count == 2)
                {
                    throw new InvalidDataException(
                        "Legacy one-ROI Thickness keeps its existing ROI as the Measurement ROI, but Preview now requires a Reference ROI first. Teach the Reference ROI to upgrade this step.");
                }
                throw new InvalidDataException(twoRoi
                    ? $"{step.ToolName} v1 requires one HeightField and two ordered GridRectangles: Reference ROI, then {(step.ToolId == "completeness-grid" ? "Inspection Grid ROI" : "Measurement ROI")}."
                    : $"{step.ToolName} v1 requires one HeightField first and one GridRectangle second.");
            }
            var usesEditableRegionArtifact = step.ToolId == "completeness-grid"
                && step.InputEntityIds.Count == 3
                && (document.Selections ?? []).All(selection =>
                    !string.Equals(selection.Id, step.InputEntityIds[2], StringComparison.OrdinalIgnoreCase));
            var inspectionRegionArtifact = usesEditableRegionArtifact
                ? publishedEditableRegionArtifact
                    ?? throw new InvalidDataException(
                        $"{step.ToolName} v1 is waiting for its exact EditableRegionArtifact input '{step.InputEntityIds[2]}'.")
                : null;
            if (inspectionRegionArtifact is not null
                && !string.Equals(
                    inspectionRegionArtifact.ArtifactId,
                    step.InputEntityIds[2],
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"{step.ToolName} v1 editable-region input identity does not match '{step.InputEntityIds[2]}'.");
            }

            var selectionInputIds = usesEditableRegionArtifact
                ? step.InputEntityIds.Skip(1).Take(1)
                : step.InputEntityIds.Skip(1);
            var selections = selectionInputIds.Select(inputId =>
                (document.Selections ?? []).SingleOrDefault(candidate =>
                    string.Equals(candidate.Id, inputId, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidDataException($"{step.ToolName} v1 requires recipe-owned selection inputs.")).ToArray();
            if (inspectionRegionArtifact is not null)
            {
                selections =
                [
                    selections.Single(),
                    CreateEditableRegionSelection(inspectionRegionArtifact)
                ];
            }
            var pointPair = step.ToolId == "point-pair-dimensions";
            if (pointPair
                ? selections.Length != 1 || selections[0].Kind != ToolRecipeSelectionKinds.PointSet || selections[0].Points?.Count != 2
                : selections.Any(selection => selection.Kind != ToolRecipeSelectionKinds.GridRectangle || selection.GridRectangle is null))
            {
                throw new InvalidDataException(pointPair
                    ? $"{step.ToolName} v1 requires one ordered PointSet(2)."
                    : $"{step.ToolName} v1 selection inputs must be GridRectangles.");
            }
            if (step.ToolId == "cross-section-dimensions"
                && selections[0].GridRectangle is not { RowCount: 1, ColumnCount: >= 2 })
            {
                throw new InvalidDataException("Cross-section Dimensions v1 requires one GridRectangle spanning exactly one row and at least two columns.");
            }
            ValidateParameters(step);
            var rois = pointPair ? [] : selections.Select(selection => ToRoi(selection.GridRectangle!)).ToArray();
            if (string.Equals(step.InputEntityIds[0], document.Source.Id, StringComparison.OrdinalIgnoreCase))
            {
                if (step.ToolId is "plane-flatness" or "point-pair-dimensions" or "gap-flush" or "volume" or "cross-section-dimensions")
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
                ValidateEditableRegionCompatibility(
                    inspectionRegionArtifact,
                    step.InputEntityIds[0],
                    snapshot.ContentSha256,
                    snapshot.RootSourceSha256,
                    snapshot.Unit,
                    snapshot.FrameId,
                    snapshot.Width,
                    snapshot.Height);
                prepared = new PreparedHeightMeasurement(
                    step, selections, snapshot.EntityId, snapshot.ContentSha256, snapshot.Unit, snapshot.FrameId,
                    snapshot.Height, snapshot.Width, snapshot.Values.ToArray(), null,
                    twoRoi ? rois[0] : null, rois[^1], inspectionRegionArtifact);
                message = twoRoi
                    ? $"{step.ToolName} v1 is ready from the verified raw C3D and ordered Reference/Measurement GridRectangles."
                    : $"{step.ToolName} v1 is ready from the verified raw C3D and source-owned GridRectangle.";
                return true;
            }

            if (publishedHeightField is not null
                && string.Equals(step.InputEntityIds[0], publishedHeightField.EntityId, StringComparison.OrdinalIgnoreCase))
            {
                foreach (var selection in selections.Take(usesEditableRegionArtifact ? 1 : selections.Length))
                {
                    var binding = ToolRecipeSelectionSourceBindingVerifier.Verify(publishedHeightField, selection.SourceBinding);
                    if (!binding.IsCurrent) throw new InvalidDataException(binding.Message);
                }
                ValidateEditableRegionCompatibility(
                    inspectionRegionArtifact,
                    step.InputEntityIds[0],
                    publishedHeightField.ContentSha256,
                    publishedHeightField.RootSourceSha256,
                    publishedHeightField.Unit,
                    publishedHeightField.FrameId,
                    publishedHeightField.Width,
                    publishedHeightField.Height);
                prepared = new PreparedHeightMeasurement(
                    step,
                    selections,
                    publishedHeightField.EntityId,
                    publishedHeightField.ContentSha256,
                    publishedHeightField.Unit,
                    publishedHeightField.FrameId,
                    publishedHeightField.Height,
                    publishedHeightField.Width,
                    publishedHeightField.Values.ToArray(),
                    null,
                    twoRoi ? rois[0] : null,
                    pointPair ? null : rois[^1],
                    inspectionRegionArtifact);
                message = $"{step.ToolName} v1 is ready from the exact Published HeightField and {selections.Length} artifact-owned selection input(s).";
                return true;
            }

            if (publishedTransformedHeightField is null
                || !string.Equals(step.InputEntityIds[0], publishedTransformedHeightField.OutputEntityId, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"{step.ToolName} v1 is waiting for its exact Published compatible HeightField first input.");
            }
            foreach (var selection in selections.Take(usesEditableRegionArtifact ? 1 : selections.Length))
            {
                var binding = ToolRecipeSelectionSourceBindingVerifier.Verify(publishedTransformedHeightField, selection.SourceBinding);
                if (!binding.IsCurrent) throw new InvalidDataException(binding.Message);
            }
            ValidateEditableRegionCompatibility(
                inspectionRegionArtifact,
                step.InputEntityIds[0],
                publishedTransformedHeightField.ContentSha256,
                publishedTransformedHeightField.RootSourceSha256,
                publishedTransformedHeightField.ReferenceUnit,
                publishedTransformedHeightField.ReferenceFrameId,
                publishedTransformedHeightField.ColumnCount,
                publishedTransformedHeightField.RowCount);
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
                pointPair ? null : rois[^1],
                inspectionRegionArtifact);
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
        C3DGridRoi? MeasurementRoi,
        C3DEditableRegionArtifact? InspectionRegionArtifact);

    private static ToolRecipeSelection CreateEditableRegionSelection(
        C3DEditableRegionArtifact artifact) =>
        new(
            artifact.ArtifactId,
            artifact.Name,
            ToolRecipeSelectionKinds.GridRectangle,
            artifact.SourceEntityId,
            artifact.FrameId,
            new ToolRecipeSelectionSourceBinding(
                "HeightField",
                artifact.SourceContentSha256,
                artifact.GridWidth,
                artifact.GridHeight,
                artifact.SourceEntityId,
                artifact.RootSourceSha256,
                artifact.Unit,
                artifact.FrameId),
            new ToolRecipeGridRectangle(
                artifact.Region.MinimumRow,
                artifact.Region.MinimumColumn,
                artifact.Bounding.Height,
                artifact.Bounding.Width),
            null,
            null);

    private static void ValidateEditableRegionCompatibility(
        C3DEditableRegionArtifact? artifact,
        string inputEntityId,
        string inputContentSha256,
        string rootSourceSha256,
        string unit,
        string frameId,
        int width,
        int height)
    {
        if (artifact is null)
        {
            return;
        }
        if (!string.Equals(artifact.SourceEntityId, inputEntityId, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(artifact.SourceContentSha256, inputContentSha256, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(artifact.RootSourceSha256, rootSourceSha256, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(artifact.Unit, unit, StringComparison.Ordinal)
            || !string.Equals(artifact.FrameId, frameId, StringComparison.Ordinal)
            || artifact.GridWidth != width
            || artifact.GridHeight != height)
        {
            throw new InvalidDataException(
                "Completeness Grid EditableRegionArtifact does not match its HeightField input identity, grid, unit, or frame.");
        }
    }

    private static void ValidateParameters(ToolRecipeStep step)
    {
        var expected = step.ToolId switch
        {
            "thickness" => ThicknessParameterNames,
            "warpage" => WarpageParameterNames,
            "point-pair-dimensions" => PointPairParameterNames,
            "gap-flush" => GapFlushParameterNames,
            "volume" => VolumeParameterNames,
            "cross-section-dimensions" => CrossSectionParameterNames,
            "completeness-grid" => [],
            _ => PlaneFlatnessParameterNames
        };
        var parameters = step.Parameters ?? [];
        if (step.ToolId != "completeness-grid"
            && (parameters.Count != expected.Length
                || expected.Any(name =>
                    parameters.Count(parameter => parameter.Name == name) != 1)))
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
        else if (step.ToolId == "cross-section-dimensions")
        {
            _ = ParseNonNegative(Parameter(step, "ExpectedWidth"), "ExpectedWidth");
            _ = ParseNonNegative(Parameter(step, "WidthTolerance"), "WidthTolerance");
            _ = ParseNonNegative(Parameter(step, "ExpectedHeightRange"), "ExpectedHeightRange");
            _ = ParseNonNegative(Parameter(step, "HeightTolerance"), "HeightTolerance");
        }
        else if (step.ToolId == "completeness-grid")
        {
            _ = C3DCompletenessGridProfile.FromRecipeParameters(
                step.Parameters ?? []);
            _ = C3DCompletenessPresencePolicy.FromOptionalRecipeParameters(
                step.Parameters ?? []);
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
        var reconstruction = Reconstruct(
            prepared,
            roi,
            profile,
            SdkReferenceGridCoordinateMode.DeclaredFrame);
        if (!reconstruction.Success)
        {
            throw new InvalidDataException(IsCoordinateRangeFailure(reconstruction.Message)
                ? "Plane Flatness reconstructed point exceeds the supported single-precision geometry range."
                : reconstruction.Message);
        }
        return reconstruction.Samples
            .Select(sample => new HeightFieldPlaneSample(
                new Vector3((float)sample.X, (float)sample.Y, (float)sample.Z),
                sample.Height))
            .ToArray();
    }

    private static IReadOnlyList<HeightFieldPlaneSample> CreateReferenceAxisPlaneSamples(
        PreparedHeightMeasurement prepared,
        C3DGridRoi roi,
        string toolName)
    {
        var profile = prepared.ReferenceGridProfile;
        var reconstruction = Reconstruct(
            prepared,
            roi,
            profile,
            SdkReferenceGridCoordinateMode.ReferenceAxes);
        if (!reconstruction.Success)
        {
            throw new InvalidDataException(IsCoordinateRangeFailure(reconstruction.Message)
                ? $"{toolName} reference-axis sample exceeds the supported single-precision geometry range."
                : reconstruction.Message);
        }
        return reconstruction.Samples
            .Select(sample => new HeightFieldPlaneSample(
                new Vector3((float)sample.U, (float)sample.Height, (float)sample.V),
                sample.Height))
            .ToArray();
    }

    private static IReadOnlyList<SdkCrossSectionDimensionsSample> CreateCrossSectionSamples(
        PreparedHeightMeasurement prepared,
        C3DGridRoi roi)
    {
        var profile = prepared.ReferenceGridProfile
            ?? throw new InvalidDataException("Cross-section Dimensions requires a reference-grid profile.");
        var firstRow = new C3DGridRoi(roi.Row, roi.Column, 1, roi.ColumnCount);
        var reconstruction = Reconstruct(
            prepared,
            firstRow,
            profile,
            SdkReferenceGridCoordinateMode.ReferenceAxes,
            double.MinValue,
            double.MaxValue);
        if (!reconstruction.Success)
        {
            throw new InvalidDataException(IsCoordinateRangeFailure(reconstruction.Message)
                ? "Cross-section Dimensions reference-axis sample exceeds the supported single-precision geometry range."
                : reconstruction.Message);
        }
        return reconstruction.Samples
            .Select(sample => new SdkCrossSectionDimensionsSample(
                sample.Column,
                sample.U,
                sample.Height))
            .ToArray();
    }

    private static SdkGapFlushRegionStatistics? CreateGapFlushRegionStatistics(
        PreparedHeightMeasurement prepared,
        C3DGridRoi roi)
    {
        var statistics = new SdkHeightMapRegionStatisticsTool().Execute(
            prepared.Height,
            prepared.Width,
            prepared.Values,
            ToSdkRegion(roi));
        if (!statistics.Success)
        {
            throw new InvalidDataException(statistics.Message);
        }
        return !statistics.HasFiniteSamples
            ? null
            : new SdkGapFlushRegionStatistics(
                statistics.FiniteCellCount,
                statistics.Mean,
                statistics.Mean);
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
        var reconstruction = Reconstruct(
            prepared,
            new C3DGridRoi(locator.Row, locator.Column, 1, 1),
            profile,
            SdkReferenceGridCoordinateMode.DeclaredFrame);
        if (!reconstruction.Success)
        {
            throw new InvalidDataException(IsCoordinateRangeFailure(reconstruction.Message)
                ? "Point Pair reconstructed point exceeds the supported single-precision geometry range."
                : reconstruction.Message);
        }
        var sample = reconstruction.Samples.Single();
        return (new Vector3((float)sample.X, (float)sample.Y, (float)sample.Z), sample.Height);
    }

    private static OpenVisionLab.Vision3D.FeatureExtraction.ReferenceGridPointReconstructionResult Reconstruct(
        PreparedHeightMeasurement prepared,
        C3DGridRoi roi,
        C3DReferenceGridProfile? profile,
        SdkReferenceGridCoordinateMode coordinateMode,
        double minimumSupportedCoordinate = float.MinValue,
        double maximumSupportedCoordinate = float.MaxValue) =>
        new SdkReferenceGridPointReconstructionTool().Execute(
            prepared.Height,
            prepared.Width,
            prepared.Values,
            ToSdkRegion(roi),
            ToSdkDefinition(profile),
            new SdkReferenceGridPointReconstructionOptions
            {
                CoordinateMode = coordinateMode,
                MinimumSupportedCoordinate = minimumSupportedCoordinate,
                MaximumSupportedCoordinate = maximumSupportedCoordinate
            });

    private static SdkHeightGridRegion ToSdkRegion(C3DGridRoi roi) =>
        new(roi.Row, roi.Column, roi.RowCount, roi.ColumnCount);

    private static SdkReferenceGridDefinition ToSdkDefinition(C3DReferenceGridProfile? profile) =>
        profile is null
            ? new SdkReferenceGridDefinition
            {
                Origin = new SdkReferenceGridVector(0d, 0d, 0d),
                UAxis = new SdkReferenceGridVector(1d, 0d, 0d),
                VAxis = new SdkReferenceGridVector(0d, 0d, 1d),
                HAxis = new SdkReferenceGridVector(0d, 1d, 0d),
                PitchU = 1d,
                PitchV = 1d
            }
            : new SdkReferenceGridDefinition
            {
                Origin = ToSdkVector(profile.Origin),
                UAxis = ToSdkVector(profile.UAxis),
                VAxis = ToSdkVector(profile.VAxis),
                HAxis = ToSdkVector(profile.HAxis),
                PitchU = profile.PitchU,
                PitchV = profile.PitchV
            };

    private static SdkReferenceGridVector ToSdkVector(C3DReferenceGridVector vector) =>
        new(vector.X, vector.Y, vector.Z);

    private static bool IsCoordinateRangeFailure(string message) =>
        message.Contains("exceeds the supported range", StringComparison.Ordinal);

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
