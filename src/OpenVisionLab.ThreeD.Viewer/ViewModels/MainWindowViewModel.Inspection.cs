using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Media;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Tools;
using OpenVisionLab.ThreeD.Viewer;

namespace OpenVisionLab.ThreeD.Viewer.ViewModels;

public sealed partial class MainWindowViewModel
{
    public HeightDeviationRecipePlaneFlatness CreatePlaneFlatnessRecipeStep() =>
        new(
            planeFlatnessStepId,
            planeFlatnessSourceEntityId,
            planeFlatnessReferenceId,
            new HeightDeviationRecipeRoiRegion(
                PlaneFlatnessReferenceCenterX,
                PlaneFlatnessReferenceCenterZ,
                PlaneFlatnessReferenceHalfWidth,
            PlaneFlatnessReferenceHalfDepth),
            PlaneFlatnessTolerance,
            planeFlatnessUnit,
            planeFlatnessMaxSampledPoints,
            planeFlatnessEnabled);

    public void SetPlaneFlatnessRecipeStep(HeightDeviationRecipePlaneFlatness step)
    {
        planeFlatnessStepId = step.Id;
        planeFlatnessSourceEntityId = step.SourceEntityId;
        planeFlatnessReferenceId = step.ReferenceId;
        SetField(ref planeFlatnessUnit, step.Unit, nameof(PlaneFlatnessUnit));
        planeFlatnessMaxSampledPoints = step.MaxSampledPoints;
        planeFlatnessEnabled = step.Enabled;
        SetField(ref planeFlatnessReferenceCenterX, step.ReferenceRegion.CenterX, nameof(PlaneFlatnessReferenceCenterX));
        SetField(ref planeFlatnessReferenceCenterZ, step.ReferenceRegion.CenterZ, nameof(PlaneFlatnessReferenceCenterZ));
        SetField(ref planeFlatnessReferenceHalfWidth, step.ReferenceRegion.HalfWidth, nameof(PlaneFlatnessReferenceHalfWidth));
        SetField(ref planeFlatnessReferenceHalfDepth, step.ReferenceRegion.HalfDepth, nameof(PlaneFlatnessReferenceHalfDepth));
        SetField(ref planeFlatnessTolerance, step.Tolerance, nameof(PlaneFlatnessTolerance));
        PlaneFlatnessConfigured = true;
        RefreshRecipeSummary();
        RefreshRecipeParameterSummary();
    }

    public void SetPlaneFlatnessPreview(PlaneFlatnessEvaluation evaluation)
    {
        ClearPointPairDimensionsPreview();
        ClearGapFlushPreview();
        ClearVolumePreview();
        ClearCrossSectionPreview();
        PlaneFlatnessConfigured = true;
        planeFlatnessEnabled = true;
        c3dPlaneFlatnessPreview = evaluation.Result;
        c3dPlaneFlatnessPreviewActive = true;
        PlaneFlatnessVisible = true;
        PlaneFlatnessValue = evaluation.Flatness;
        PlaneFlatnessMinimumDeviation = evaluation.MinimumSignedDistance;
        PlaneFlatnessMaximumDeviation = evaluation.MaximumSignedDistance;
        PlaneFlatnessRms = evaluation.RootMeanSquareDistance;
        PlaneFlatnessReferenceSampleCount = evaluation.ReferenceSampleCount;
        PlaneFlatnessMeasurementSampleCount = evaluation.MeasurementSampleCount;

        PlaneFlatnessSummary = evaluation.ReferencePlane is null
            ? $"Flatness: {evaluation.Result.Status} | {evaluation.Result.Message}"
            : string.Create(
                CultureInfo.InvariantCulture,
                $"Flatness: {evaluation.Result.Status} | {evaluation.Flatness:F3} / {PlaneFlatnessTolerance:F3} {PlaneFlatnessUnit}");
        PlaneFlatnessDetails = evaluation.ReferencePlane is null
            ? "Reference ROI did not produce a valid fitted plane."
            : string.Create(
                CultureInfo.InvariantCulture,
                $"Signed min {evaluation.MinimumSignedDistance:F3}, max {evaluation.MaximumSignedDistance:F3}, RMS {evaluation.RootMeanSquareDistance:F3} {PlaneFlatnessUnit} | reference {evaluation.ReferenceSampleCount:N0}, measured {evaluation.MeasurementSampleCount:N0}");

        activePreviewLayerId = "layer.preview.c3d-plane-flatness";
        activePreviewLayerName = "Preview: C3D Plane Flatness";
        activePreviewSourceEntityId = C3DEntityId;
        activeResultEntityId = C3DPlaneFlatnessResultEntityId;
        activeResultEntityName = "Published C3D Plane Flatness";
        SetField(ref resultOverlayVisible, true, nameof(ResultOverlayVisible));
        PreviewToolResult = evaluation.Result;
        ResultSummary = FormatToolResult(PreviewToolResult);
        RefreshDisplaySettingsContext();
        SelectedColorMode = "Deviation";
        SelectedSelectionMode = "Plane Flatness";
        SelectedEntity = "C3D Plane Flatness";
        SelectionSummary = PlaneFlatnessDetails;
        MeasurementSummary = PlaneFlatnessDetails;
        ViewerStatus = "C3D plane flatness preview updated";
        RefreshRecipeSummary();
        RefreshRecipeParameterSummary();
        RefreshSceneContracts();
    }

    public void ClearPlaneFlatnessPreview()
    {
        c3dPlaneFlatnessPreview = null;
        c3dPlaneFlatnessPreviewActive = false;
        PlaneFlatnessVisible = false;
        PlaneFlatnessValue = double.NaN;
        PlaneFlatnessMinimumDeviation = double.NaN;
        PlaneFlatnessMaximumDeviation = double.NaN;
        PlaneFlatnessRms = double.NaN;
        PlaneFlatnessReferenceSampleCount = 0;
        PlaneFlatnessMeasurementSampleCount = 0;
        PlaneFlatnessSummary = "Flatness: preview not run";
        PlaneFlatnessDetails = "Reference ROI and signed surface deviation: pending";
    }

    public void ClearPlaneFlatnessRecipeStep()
    {
        ClearPlaneFlatnessPreview();
        planeFlatnessStepId = PlaneFlatnessStepId;
        planeFlatnessSourceEntityId = C3DEntityId;
        planeFlatnessReferenceId = PlaneFlatnessReferenceId;
        planeFlatnessUnit = "model";
        planeFlatnessMaxSampledPoints = PlaneFlatnessMaxSampledPoints;
        planeFlatnessEnabled = true;
        OnPropertyChanged(nameof(PlaneFlatnessUnit));
        PlaneFlatnessConfigured = false;
        RefreshRecipeSummary();
        RefreshRecipeParameterSummary();
    }

    public void InvalidatePlaneFlatnessPreview(string reason)
    {
        if (!c3dPlaneFlatnessPreviewActive)
        {
            return;
        }

        ClearPlaneFlatnessPreview();
        SetField(ref resultOverlayVisible, false, nameof(ResultOverlayVisible));
        ResetActivePreviewIdentity();
        PreviewToolResult = CreateNotRunToolResult();
        ResultSummary = FormatToolResult(PreviewToolResult);
        ViewerStatus = reason;
        RefreshSceneContracts();
    }

    public C3DPointPairDimensionsStep? CreatePointPairDimensionsRecipeStep()
    {
        if (pointPairFirstReference is null || pointPairSecondReference is null)
        {
            return null;
        }

        return new C3DPointPairDimensionsStep(
            pointPairDimensionsStepId,
            pointPairDimensionsSourceEntityId,
            pointPairFirstReference,
            pointPairSecondReference,
            new C3DPointPairDimensionsAcceptance(
                PointPairExpectedDistance,
                PointPairDistanceTolerance,
                PointPairExpectedWidth,
                PointPairWidthTolerance,
                PointPairExpectedAngleDegrees,
                PointPairAngleToleranceDegrees),
            pointPairDimensionsUnit,
            pointPairDimensionsEnabled);
    }

    public void SetPointPairFirstReference(C3DGridPointReference reference)
    {
        pointPairFirstReference = reference;
        pointPairSecondReference = null;
        PointPairDimensionsConfigured = true;
        InvalidatePointPairDimensionsPreview("Point pair selection changed; select P2 and run Preview Dimensions again");
        OnPropertyChanged(nameof(HasPointPairReferences));
        RefreshCommandCanExecute();
    }

    public void SetPointPairFirstReference(int row, int column) =>
        SetPointPairFirstReference(new C3DGridPointReference(PointPairFirstReferenceId, row, column));

    public void SetPointPairReferences(C3DGridPointReference first, C3DGridPointReference second)
    {
        pointPairFirstReference = first;
        pointPairSecondReference = second;
        PointPairDimensionsConfigured = true;
        InvalidatePointPairDimensionsPreview("Point pair selection changed; run Preview Dimensions again");
        OnPropertyChanged(nameof(HasPointPairReferences));
        RefreshCommandCanExecute();
    }

    public void SetPointPairReferences(int firstRow, int firstColumn, int secondRow, int secondColumn) =>
        SetPointPairReferences(
            new C3DGridPointReference(PointPairFirstReferenceId, firstRow, firstColumn),
            new C3DGridPointReference(PointPairSecondReferenceId, secondRow, secondColumn));

    public void SetPointPairDimensionsRecipeStep(C3DPointPairDimensionsStep step)
    {
        pointPairDimensionsStepId = step.Id;
        pointPairDimensionsSourceEntityId = step.SourceEntityId;
        pointPairFirstReference = step.First;
        pointPairSecondReference = step.Second;
        pointPairDimensionsUnit = step.Unit;
        pointPairDimensionsEnabled = step.Enabled;
        SetField(ref pointPairExpectedDistance, step.Acceptance.ExpectedDistance, nameof(PointPairExpectedDistance));
        SetField(ref pointPairDistanceTolerance, step.Acceptance.DistanceTolerance, nameof(PointPairDistanceTolerance));
        SetField(ref pointPairExpectedWidth, step.Acceptance.ExpectedWidth, nameof(PointPairExpectedWidth));
        SetField(ref pointPairWidthTolerance, step.Acceptance.WidthTolerance, nameof(PointPairWidthTolerance));
        SetField(ref pointPairExpectedAngleDegrees, step.Acceptance.ExpectedElevationAngleDegrees, nameof(PointPairExpectedAngleDegrees));
        SetField(ref pointPairAngleToleranceDegrees, step.Acceptance.ElevationAngleToleranceDegrees, nameof(PointPairAngleToleranceDegrees));
        OnPropertyChanged(nameof(PointPairDimensionsUnit));
        OnPropertyChanged(nameof(HasPointPairReferences));
        PointPairDimensionsConfigured = true;
        RefreshCommandCanExecute();
        RefreshRecipeSummary();
        RefreshRecipeParameterSummary();
    }

    public void SetPointPairDimensionsPreview(PointPairDimensionsEvaluation evaluation)
    {
        ClearPlaneFlatnessPreview();
        ClearGapFlushPreview();
        ClearVolumePreview();
        ClearCrossSectionPreview();
        PointPairDimensionsConfigured = true;
        pointPairDimensionsEnabled = true;
        c3dPointPairDimensionsPreview = evaluation.Result;
        c3dPointPairDimensionsPreviewActive = true;
        PointPairDimensionsVisible = true;
        PointPairDistance = evaluation.Distance;
        PointPairWidth = evaluation.PlanarWidth;
        PointPairAngleDegrees = evaluation.ElevationAngleDegrees;
        PointPairDimensionsSummary = string.Create(
            CultureInfo.InvariantCulture,
            $"Point pair: {evaluation.Result.Status} | D {evaluation.Distance:F3}, W {evaluation.PlanarWidth:F3} {PointPairDimensionsUnit}, A {evaluation.ElevationAngleDegrees:F3} deg");
        var referenceSummary = pointPairFirstReference is not null && pointPairSecondReference is not null
            ? $"Refs {pointPairFirstReference.Id} ({pointPairFirstReference.Row},{pointPairFirstReference.Column}) -> {pointPairSecondReference.Id} ({pointPairSecondReference.Row},{pointPairSecondReference.Column}) | "
            : string.Empty;
        PointPairDimensionsDetails = string.Create(
            CultureInfo.InvariantCulture,
            $"{referenceSummary}Expected D {PointPairExpectedDistance:F3} +/- {PointPairDistanceTolerance:F3}, W {PointPairExpectedWidth:F3} +/- {PointPairWidthTolerance:F3} {PointPairDimensionsUnit} | A {PointPairExpectedAngleDegrees:F3} +/- {PointPairAngleToleranceDegrees:F3} deg");

        activePreviewLayerId = "layer.preview.c3d-point-pair-dimensions";
        activePreviewLayerName = "Preview: C3D Point Pair Dimensions";
        activePreviewSourceEntityId = C3DEntityId;
        activeResultEntityId = C3DPointPairDimensionsResultEntityId;
        activeResultEntityName = "Published C3D Point Pair Dimensions";
        SetField(ref resultOverlayVisible, true, nameof(ResultOverlayVisible));
        PreviewToolResult = evaluation.Result;
        ResultSummary = FormatToolResult(PreviewToolResult);
        SelectedColorMode = "Height";
        SelectedSelectionMode = "Two Point Measure";
        SelectedEntity = "C3D Point Pair Dimensions";
        SelectionSummary = PointPairDimensionsDetails;
        MeasurementSummary = PointPairDimensionsDetails;
        ViewerStatus = "C3D point pair dimensions preview updated";
        RefreshRecipeSummary();
        RefreshRecipeParameterSummary();
        RefreshSceneContracts();
    }

    public void ClearPointPairDimensionsPreview()
    {
        c3dPointPairDimensionsPreview = null;
        c3dPointPairDimensionsPreviewActive = false;
        PointPairDimensionsVisible = false;
        PointPairDistance = double.NaN;
        PointPairWidth = double.NaN;
        PointPairAngleDegrees = double.NaN;
        PointPairDimensionsSummary = "Point pair dimensions: preview not run";
        PointPairDimensionsDetails = "Select two C3D points and run Preview Dimensions.";
    }

    public void ClearPointPairDimensionsRecipeStep()
    {
        ClearPointPairDimensionsPreview();
        pointPairDimensionsStepId = PointPairDimensionsStepId;
        pointPairDimensionsSourceEntityId = C3DEntityId;
        pointPairDimensionsUnit = "model";
        pointPairDimensionsEnabled = true;
        pointPairFirstReference = null;
        pointPairSecondReference = null;
        SetField(ref pointPairExpectedDistance, 5.0, nameof(PointPairExpectedDistance));
        SetField(ref pointPairDistanceTolerance, 0.5, nameof(PointPairDistanceTolerance));
        SetField(ref pointPairExpectedWidth, 5.0, nameof(PointPairExpectedWidth));
        SetField(ref pointPairWidthTolerance, 0.5, nameof(PointPairWidthTolerance));
        SetField(ref pointPairExpectedAngleDegrees, 0.0, nameof(PointPairExpectedAngleDegrees));
        SetField(ref pointPairAngleToleranceDegrees, 5.0, nameof(PointPairAngleToleranceDegrees));
        OnPropertyChanged(nameof(PointPairDimensionsUnit));
        OnPropertyChanged(nameof(HasPointPairReferences));
        PointPairDimensionsConfigured = false;
        RefreshCommandCanExecute();
        RefreshRecipeSummary();
        RefreshRecipeParameterSummary();
    }

    public void InvalidatePointPairDimensionsPreview(string reason)
    {
        if (!c3dPointPairDimensionsPreviewActive)
        {
            return;
        }

        ClearPointPairDimensionsPreview();
        SetField(ref resultOverlayVisible, false, nameof(ResultOverlayVisible));
        ResetActivePreviewIdentity();
        PreviewToolResult = CreateNotRunToolResult();
        ResultSummary = FormatToolResult(PreviewToolResult);
        ViewerStatus = reason;
        RefreshSceneContracts();
    }

    public void BeginThicknessRoiTeaching()
    {
        SelectedSelectionMode = ThicknessRoiSelectionMode;
        SelectionOverlayVisible = true;
        SelectedEntity = "C3D Thickness ROI";
        ThicknessSummary = "Thickness ROI teaching: click one C3D grid location.";
        ThicknessDetails = string.Create(
            CultureInfo.InvariantCulture,
            $"Current grid ROI row {ThicknessRoiRow}, column {ThicknessRoiColumn}, size {ThicknessRoiRowCount} x {ThicknessRoiColumnCount} | limits [{ThicknessMinimum:F3}, {ThicknessMaximum:F3}] {ThicknessUnit}");
        SelectionSummary = ThicknessSummary;
        MeasurementSummary = ThicknessDetails;
        ViewerStatus = "Thickness ROI teaching: click the C3D surface to place the ROI";
    }

    public void SetThicknessRoiFromCenter(int row, int column, int sourceRows, int sourceColumns)
    {
        if (sourceRows <= 0 || sourceColumns <= 0)
        {
            ViewerStatus = "Thickness ROI teaching requires C3D grid dimensions";
            return;
        }

        var rowCount = Math.Min(Math.Max(1, ThicknessRoiRowCount), sourceRows);
        var columnCount = Math.Min(Math.Max(1, ThicknessRoiColumnCount), sourceColumns);
        var boundedRow = Math.Clamp(row - rowCount / 2, 0, sourceRows - rowCount);
        var boundedColumn = Math.Clamp(column - columnCount / 2, 0, sourceColumns - columnCount);
        var changed = SetField(ref thicknessRoiRow, boundedRow, nameof(ThicknessRoiRow));
        changed |= SetField(ref thicknessRoiColumn, boundedColumn, nameof(ThicknessRoiColumn));
        changed |= SetField(ref thicknessRoiRowCount, rowCount, nameof(ThicknessRoiRowCount));
        changed |= SetField(ref thicknessRoiColumnCount, columnCount, nameof(ThicknessRoiColumnCount));
        ThicknessConfigured = true;

        if (changed)
        {
            InvalidateThicknessPreview("Thickness ROI changed; run Preview Thickness again");
        }

        ThicknessSummary = string.Create(
            CultureInfo.InvariantCulture,
            $"Thickness ROI taught: row {ThicknessRoiRow}, column {ThicknessRoiColumn}, size {ThicknessRoiRowCount} x {ThicknessRoiColumnCount}");
        ThicknessDetails = string.Create(
            CultureInfo.InvariantCulture,
            $"Anchor cell ({row}, {column}) | limits [{ThicknessMinimum:F3}, {ThicknessMaximum:F3}] {ThicknessUnit} | frame {ThicknessFrameId}");
        SelectedSelectionMode = ThicknessRoiSelectionMode;
        SelectedEntity = "C3D Thickness ROI";
        SelectionOverlayVisible = true;
        SelectionSummary = ThicknessSummary;
        MeasurementSummary = ThicknessDetails;
        ViewerStatus = "Thickness ROI taught; configure limits and run Preview Thickness";
        RefreshRecipeSummary();
        RefreshRecipeParameterSummary();
        RefreshCommandCanExecute();
    }

    public C3DThicknessStep CreateThicknessRecipeStep() =>
        new(
            thicknessStepId,
            thicknessSourceEntityId,
            thicknessRoiReferenceId,
            new C3DGridRoi(ThicknessRoiRow, ThicknessRoiColumn, ThicknessRoiRowCount, ThicknessRoiColumnCount),
            new C3DThicknessAcceptance(ThicknessMinimum, ThicknessMaximum),
            thicknessUnit,
            thicknessFrameId,
            ThicknessMinimumValidSamples,
            Enabled: true);

    public void SetThicknessRecipeStep(C3DThicknessStep step)
    {
        ArgumentNullException.ThrowIfNull(step);
        thicknessStepId = step.Id;
        thicknessSourceEntityId = step.SourceEntityId;
        thicknessRoiReferenceId = step.RoiReferenceId;
        thicknessUnit = step.Unit;
        thicknessFrameId = step.FrameId;
        SetField(ref thicknessRoiRow, step.Roi.Row, nameof(ThicknessRoiRow));
        SetField(ref thicknessRoiColumn, step.Roi.Column, nameof(ThicknessRoiColumn));
        SetField(ref thicknessRoiRowCount, step.Roi.RowCount, nameof(ThicknessRoiRowCount));
        SetField(ref thicknessRoiColumnCount, step.Roi.ColumnCount, nameof(ThicknessRoiColumnCount));
        SetField(ref thicknessMinimum, step.Acceptance.MinimumThickness, nameof(ThicknessMinimum));
        SetField(ref thicknessMaximum, step.Acceptance.MaximumThickness, nameof(ThicknessMaximum));
        SetField(ref thicknessMinimumValidSamples, step.MinimumValidSamples, nameof(ThicknessMinimumValidSamples));
        OnPropertyChanged(nameof(ThicknessUnit));
        OnPropertyChanged(nameof(ThicknessFrameId));
        ThicknessConfigured = true;
        RefreshRecipeSummary();
        RefreshRecipeParameterSummary();
        RefreshCommandCanExecute();
    }

    public void SetThicknessPreview(C3DThicknessEvaluation evaluation)
    {
        ArgumentNullException.ThrowIfNull(evaluation);
        c3dHeightDeviationPreview = null;
        ClearPlaneFlatnessPreview();
        ClearPointPairDimensionsPreview();
        ClearGapFlushPreview();
        ClearVolumePreview();
        ClearCrossSectionPreview();
        ThicknessConfigured = true;
        c3dThicknessPreview = evaluation.Result;
        c3dThicknessPreviewActive = true;
        ThicknessVisible = true;
        ThicknessMean = evaluation.Mean;
        ThicknessMinimumMeasured = evaluation.Minimum;
        ThicknessMaximumMeasured = evaluation.Maximum;
        ThicknessRange = evaluation.Range;
        ThicknessValidSampleCount = evaluation.ValidSampleCount;
        ThicknessBelowLowerLimitCount = evaluation.BelowLowerLimitCount;
        ThicknessAboveUpperLimitCount = evaluation.AboveUpperLimitCount;
        ThicknessSummary = string.Create(
            CultureInfo.InvariantCulture,
            $"Thickness: {evaluation.Result.Status} | mean {evaluation.Mean:F3} {ThicknessUnit} | range {evaluation.Range:F3}");
        ThicknessDetails = string.Create(
            CultureInfo.InvariantCulture,
            $"Grid ROI row {evaluation.Roi.Row}, column {evaluation.Roi.Column}, size {evaluation.Roi.RowCount} x {evaluation.Roi.ColumnCount} | valid {evaluation.ValidSampleCount:N0} | below {evaluation.BelowLowerLimitCount:N0}, above {evaluation.AboveUpperLimitCount:N0} | limits [{ThicknessMinimum:F3}, {ThicknessMaximum:F3}] {ThicknessUnit} | package {evaluation.PackageResultStatus}/{evaluation.PackageErrorCode} | declared scalar, uncalibrated");

        activePreviewLayerId = "layer.preview.c3d-thickness";
        activePreviewLayerName = "Preview: C3D Thickness";
        activePreviewSourceEntityId = C3DEntityId;
        activeResultEntityId = C3DThicknessResultEntityId;
        activeResultEntityName = "Published C3D Thickness";
        SetField(ref resultOverlayVisible, true, nameof(ResultOverlayVisible));
        PreviewToolResult = evaluation.Result;
        ResultSummary = FormatToolResult(PreviewToolResult);
        SelectedColorMode = "Height";
        SelectedSelectionMode = ThicknessRoiSelectionMode;
        SelectedEntity = "C3D Thickness";
        SelectionSummary = ThicknessDetails;
        MeasurementSummary = ThicknessDetails;
        ViewerStatus = "C3D Thickness preview updated";
        RefreshRecipeSummary();
        RefreshRecipeParameterSummary();
        RefreshSceneContracts();
    }

    public void ClearThicknessPreview()
    {
        c3dThicknessPreview = null;
        c3dThicknessPreviewActive = false;
        ThicknessVisible = false;
        ThicknessMean = double.NaN;
        ThicknessMinimumMeasured = double.NaN;
        ThicknessMaximumMeasured = double.NaN;
        ThicknessRange = double.NaN;
        ThicknessValidSampleCount = 0;
        ThicknessBelowLowerLimitCount = 0;
        ThicknessAboveUpperLimitCount = 0;
        ThicknessSummary = ThicknessConfigured
            ? "Thickness: ROI taught; Preview is required."
            : "Thickness: teach one C3D ROI, then run Preview.";
        ThicknessDetails = ThicknessConfigured
            ? string.Create(CultureInfo.InvariantCulture, $"Grid ROI row {ThicknessRoiRow}, column {ThicknessRoiColumn}, size {ThicknessRoiRowCount} x {ThicknessRoiColumnCount} | declared scalar, uncalibrated")
            : "Declared scalar raw-height only; calibration is not inferred.";
    }

    public void InvalidateThicknessPreview(string reason)
    {
        if (!c3dThicknessPreviewActive)
        {
            return;
        }

        ClearThicknessPreview();
        SetField(ref resultOverlayVisible, false, nameof(ResultOverlayVisible));
        ResetActivePreviewIdentity();
        PreviewToolResult = CreateNotRunToolResult();
        ResultSummary = FormatToolResult(PreviewToolResult);
        ViewerStatus = reason;
        RefreshSceneContracts();
    }

    public C3DGapFlushStep CreateGapFlushRecipeStep() =>
        new(
            gapFlushStepId,
            gapFlushSourceEntityId,
            gapFlushLeftReferenceId,
            gapFlushRightReferenceId,
            new HeightDeviationRecipeRoiRegion(
                RecipeRoiLeftCenterX,
                RecipeRoiLeftCenterZ,
                RecipeRoiLeftHalfWidth,
                RecipeRoiLeftHalfDepth),
            new HeightDeviationRecipeRoiRegion(
                RecipeRoiRightCenterX,
                RecipeRoiRightCenterZ,
                RecipeRoiRightHalfWidth,
                RecipeRoiRightHalfDepth),
            new C3DGapFlushAcceptance(
                GapFlushExpectedGap,
                GapFlushGapTolerance,
                GapFlushExpectedFlush,
                GapFlushFlushTolerance),
            gapFlushGapUnit,
            gapFlushFlushUnit,
            gapFlushMaxSampledPoints,
            gapFlushEnabled);

    public void SetGapFlushRecipeStep(C3DGapFlushStep step)
    {
        gapFlushStepId = step.Id;
        gapFlushSourceEntityId = step.SourceEntityId;
        gapFlushLeftReferenceId = step.LeftReferenceId;
        gapFlushRightReferenceId = step.RightReferenceId;
        gapFlushGapUnit = step.GapUnit;
        gapFlushFlushUnit = step.FlushUnit;
        gapFlushMaxSampledPoints = step.MaxSampledPoints;
        gapFlushEnabled = step.Enabled;
        SetField(ref gapFlushExpectedGap, step.Acceptance.ExpectedGap, nameof(GapFlushExpectedGap));
        SetField(ref gapFlushGapTolerance, step.Acceptance.GapTolerance, nameof(GapFlushGapTolerance));
        SetField(ref gapFlushExpectedFlush, step.Acceptance.ExpectedFlush, nameof(GapFlushExpectedFlush));
        SetField(ref gapFlushFlushTolerance, step.Acceptance.FlushTolerance, nameof(GapFlushFlushTolerance));
        SetRecipeRoiStepEdit(
            "GapFlush",
            step.LeftRegion.CenterX,
            step.LeftRegion.CenterZ,
            step.LeftRegion.HalfWidth,
            step.LeftRegion.HalfDepth,
            step.RightRegion.CenterX,
            step.RightRegion.CenterZ,
            step.RightRegion.HalfWidth,
            step.RightRegion.HalfDepth,
            step.MaxSampledPoints);
        OnPropertyChanged(nameof(GapFlushGapUnit));
        OnPropertyChanged(nameof(GapFlushFlushUnit));
        GapFlushConfigured = true;
        RefreshRecipeSummary();
        RefreshRecipeParameterSummary();
    }

    public void SetGapFlushPreview(GapFlushEvaluation evaluation)
    {
        ClearPlaneFlatnessPreview();
        ClearPointPairDimensionsPreview();
        ClearVolumePreview();
        ClearCrossSectionPreview();
        GapFlushConfigured = true;
        gapFlushEnabled = true;
        c3dGapFlushPreview = evaluation.Result;
        c3dGapFlushPreviewActive = true;
        GapFlushVisible = true;
        GapFlushGap = evaluation.SignedGap;
        GapFlushFlush = evaluation.SignedFlush;
        GapFlushModelFlush = evaluation.ModelFlush;
        GapFlushLeftPointCount = evaluation.LeftPointCount;
        GapFlushRightPointCount = evaluation.RightPointCount;
        GapFlushSummary = string.Create(
            CultureInfo.InvariantCulture,
            $"Gap / Flush: {evaluation.Result.Status} | gap {evaluation.SignedGap:F3} {GapFlushGapUnit}, flush {evaluation.SignedFlush:F3} {GapFlushFlushUnit}");
        GapFlushDetails = string.Create(
            CultureInfo.InvariantCulture,
            $"Signed left-right ROI edges | L {evaluation.LeftPointCount:N0}, R {evaluation.RightPointCount:N0} points | model dY {evaluation.ModelFlush:F3} | expected gap {GapFlushExpectedGap:F3} +/- {GapFlushGapTolerance:F3}, flush {GapFlushExpectedFlush:F3} +/- {GapFlushFlushTolerance:F3}");

        activePreviewLayerId = "layer.preview.c3d-gap-flush";
        activePreviewLayerName = "Preview: C3D Gap / Flush";
        activePreviewSourceEntityId = C3DEntityId;
        activeResultEntityId = C3DGapFlushResultEntityId;
        activeResultEntityName = "Published C3D Gap / Flush";
        SetField(ref resultOverlayVisible, true, nameof(ResultOverlayVisible));
        PreviewToolResult = evaluation.Result;
        ResultSummary = FormatToolResult(PreviewToolResult);
        SelectedColorMode = "Height";
        SelectedSelectionMode = "Gap / Flush";
        SelectedEntity = "C3D Gap / Flush";
        SelectionSummary = GapFlushDetails;
        MeasurementSummary = GapFlushDetails;
        ViewerStatus = "C3D Gap / Flush preview updated";
        RefreshRecipeSummary();
        RefreshRecipeParameterSummary();
        RefreshSceneContracts();
    }

    public void ClearGapFlushPreview()
    {
        c3dGapFlushPreview = null;
        c3dGapFlushPreviewActive = false;
        GapFlushVisible = false;
        GapFlushGap = double.NaN;
        GapFlushFlush = double.NaN;
        GapFlushModelFlush = double.NaN;
        GapFlushLeftPointCount = 0;
        GapFlushRightPointCount = 0;
        GapFlushSummary = "Gap / Flush: preview not run";
        GapFlushDetails = "Two recipe-owned C3D regions are required.";
    }

    public void ClearGapFlushRecipeStep()
    {
        ClearGapFlushPreview();
        gapFlushStepId = GapFlushStepId;
        gapFlushSourceEntityId = C3DEntityId;
        gapFlushLeftReferenceId = GapFlushLeftReferenceId;
        gapFlushRightReferenceId = GapFlushRightReferenceId;
        gapFlushGapUnit = "model";
        gapFlushFlushUnit = "raw-height";
        gapFlushMaxSampledPoints = GapFlushMaxSampledPoints;
        gapFlushEnabled = true;
        SetField(ref gapFlushExpectedGap, 1.322, nameof(GapFlushExpectedGap));
        SetField(ref gapFlushGapTolerance, 0.100, nameof(GapFlushGapTolerance));
        SetField(ref gapFlushExpectedFlush, 243.5, nameof(GapFlushExpectedFlush));
        SetField(ref gapFlushFlushTolerance, 5.0, nameof(GapFlushFlushTolerance));
        GapFlushConfigured = false;
        RefreshRecipeSummary();
        RefreshRecipeParameterSummary();
    }

    public void InvalidateGapFlushPreview(string reason)
    {
        if (!c3dGapFlushPreviewActive)
        {
            return;
        }

        ClearGapFlushPreview();
        SetField(ref resultOverlayVisible, false, nameof(ResultOverlayVisible));
        ResetActivePreviewIdentity();
        PreviewToolResult = CreateNotRunToolResult();
        ResultSummary = FormatToolResult(PreviewToolResult);
        ViewerStatus = reason;
        RefreshSceneContracts();
    }

    public HeightDeviationRecipeVolume CreateVolumeRecipeStep() =>
        new(
            volumeStepId,
            C3DEntityId,
            volumeReferenceId,
            volumeMeasurementId,
            new HeightDeviationRecipeRoiRegion(PlaneFlatnessReferenceCenterX, PlaneFlatnessReferenceCenterZ, PlaneFlatnessReferenceHalfWidth, PlaneFlatnessReferenceHalfDepth),
            new HeightDeviationRecipeRoiRegion(RecipeRoiLeftCenterX, RecipeRoiLeftCenterZ, RecipeRoiLeftHalfWidth, RecipeRoiLeftHalfDepth),
            VolumeExpectedNet,
            VolumeTolerance,
            volumeUnit,
            volumeMaxSampledPoints,
            volumeEnabled);

    public void SetVolumeRecipeStep(HeightDeviationRecipeVolume step)
    {
        volumeStepId = step.Id;
        volumeReferenceId = step.ReferenceId;
        volumeMeasurementId = step.MeasurementId;
        volumeUnit = step.Unit;
        volumeMaxSampledPoints = step.MaxSampledPoints;
        volumeEnabled = step.Enabled;
        SetField(ref planeFlatnessReferenceCenterX, step.ReferenceRegion.CenterX, nameof(PlaneFlatnessReferenceCenterX));
        SetField(ref planeFlatnessReferenceCenterZ, step.ReferenceRegion.CenterZ, nameof(PlaneFlatnessReferenceCenterZ));
        SetField(ref planeFlatnessReferenceHalfWidth, step.ReferenceRegion.HalfWidth, nameof(PlaneFlatnessReferenceHalfWidth));
        SetField(ref planeFlatnessReferenceHalfDepth, step.ReferenceRegion.HalfDepth, nameof(PlaneFlatnessReferenceHalfDepth));
        SetField(ref recipeRoiLeftCenterX, step.MeasurementRegion.CenterX, nameof(RecipeRoiLeftCenterX));
        SetField(ref recipeRoiLeftCenterZ, step.MeasurementRegion.CenterZ, nameof(RecipeRoiLeftCenterZ));
        SetField(ref recipeRoiLeftHalfWidth, step.MeasurementRegion.HalfWidth, nameof(RecipeRoiLeftHalfWidth));
        SetField(ref recipeRoiLeftHalfDepth, step.MeasurementRegion.HalfDepth, nameof(RecipeRoiLeftHalfDepth));
        SetField(ref volumeExpectedNet, step.ExpectedNetVolume, nameof(VolumeExpectedNet));
        SetField(ref volumeTolerance, step.Tolerance, nameof(VolumeTolerance));
        OnPropertyChanged(nameof(VolumeUnit));
        VolumeConfigured = true;
        RefreshRecipeSummary();
        RefreshRecipeParameterSummary();
    }

    public void SetVolumePreview(VolumeEvaluation evaluation)
    {
        ClearPlaneFlatnessPreview();
        ClearPointPairDimensionsPreview();
        ClearGapFlushPreview();
        ClearCrossSectionPreview();
        VolumeConfigured = true;
        c3dVolumePreview = evaluation.Result;
        c3dVolumePreviewActive = true;
        VolumeVisible = true;
        VolumeAbove = evaluation.AboveVolume;
        VolumeBelow = evaluation.BelowVolume;
        VolumeNet = evaluation.NetVolume;
        VolumeReferenceSampleCount = evaluation.ReferenceSampleCount;
        VolumeMeasurementSampleCount = evaluation.MeasurementSampleCount;
        VolumeSummary = string.Create(CultureInfo.InvariantCulture, $"Volume: {evaluation.Result.Status} | net {evaluation.NetVolume:F3} {VolumeUnit}");
        VolumeDetails = string.Create(CultureInfo.InvariantCulture, $"Above {evaluation.AboveVolume:F3}, below {evaluation.BelowVolume:F3} {VolumeUnit} | expected {VolumeExpectedNet:F3} +/- {VolumeTolerance:F3} | reference {evaluation.ReferenceSampleCount:N0}, measured {evaluation.MeasurementSampleCount:N0}");
        activePreviewLayerId = "layer.preview.c3d-volume";
        activePreviewLayerName = "Preview: C3D Volume";
        activePreviewSourceEntityId = C3DEntityId;
        activeResultEntityId = C3DVolumeResultEntityId;
        activeResultEntityName = "Published C3D Volume";
        SetField(ref resultOverlayVisible, true, nameof(ResultOverlayVisible));
        PreviewToolResult = evaluation.Result;
        ResultSummary = FormatToolResult(PreviewToolResult);
        RefreshDisplaySettingsContext();
        SelectedColorMode = "Deviation";
        SelectedSelectionMode = "Volume";
        SelectedEntity = "C3D Volume";
        SelectionSummary = VolumeDetails;
        MeasurementSummary = VolumeDetails;
        ViewerStatus = "C3D Volume preview updated";
        RefreshRecipeSummary();
        RefreshRecipeParameterSummary();
        RefreshSceneContracts();
    }

    public void ClearVolumePreview()
    {
        c3dVolumePreview = null;
        c3dVolumePreviewActive = false;
        VolumeVisible = false;
        VolumeAbove = double.NaN;
        VolumeBelow = double.NaN;
        VolumeNet = double.NaN;
        VolumeReferenceSampleCount = 0;
        VolumeMeasurementSampleCount = 0;
        VolumeSummary = "Volume: preview not run";
        VolumeDetails = "Reference ROI plane and measurement ROI are required.";
    }

    public void ClearVolumeRecipeStep()
    {
        ClearVolumePreview();
        volumeStepId = VolumeStepId;
        volumeReferenceId = VolumeReferenceId;
        volumeMeasurementId = VolumeMeasurementId;
        volumeUnit = "model^3";
        volumeMaxSampledPoints = VolumeMaxSampledPoints;
        volumeEnabled = true;
        SetField(ref volumeExpectedNet, 0.0, nameof(VolumeExpectedNet));
        SetField(ref volumeTolerance, 1.0, nameof(VolumeTolerance));
        VolumeConfigured = false;
    }

    public void InvalidateVolumePreview(string reason)
    {
        if (!c3dVolumePreviewActive) return;
        ClearVolumePreview();
        SetField(ref resultOverlayVisible, false, nameof(ResultOverlayVisible));
        ResetActivePreviewIdentity();
        PreviewToolResult = CreateNotRunToolResult();
        ResultSummary = FormatToolResult(PreviewToolResult);
        ViewerStatus = reason;
        RefreshSceneContracts();
    }

    public HeightDeviationRecipeCrossSection CreateCrossSectionRecipeStep() =>
        new(
            crossSectionStepId,
            C3DEntityId,
            crossSectionReferenceId,
            CrossSectionRow,
            CrossSectionStartColumn,
            CrossSectionEndColumn,
            CrossSectionExpectedWidth,
            CrossSectionWidthTolerance,
            CrossSectionExpectedHeightRange,
            CrossSectionHeightTolerance,
            crossSectionWidthUnit,
            crossSectionHeightUnit,
            crossSectionEnabled);

    public void SetCrossSectionRecipeStep(HeightDeviationRecipeCrossSection step)
    {
        crossSectionStepId = step.Id;
        crossSectionReferenceId = step.ReferenceId;
        crossSectionWidthUnit = step.WidthUnit;
        crossSectionHeightUnit = step.HeightUnit;
        crossSectionEnabled = step.Enabled;
        SetField(ref crossSectionRow, step.Row, nameof(CrossSectionRow));
        SetField(ref crossSectionStartColumn, step.StartColumn, nameof(CrossSectionStartColumn));
        SetField(ref crossSectionEndColumn, step.EndColumn, nameof(CrossSectionEndColumn));
        SetField(ref crossSectionExpectedWidth, step.ExpectedWidth, nameof(CrossSectionExpectedWidth));
        SetField(ref crossSectionWidthTolerance, step.WidthTolerance, nameof(CrossSectionWidthTolerance));
        SetField(ref crossSectionExpectedHeightRange, step.ExpectedHeightRange, nameof(CrossSectionExpectedHeightRange));
        SetField(ref crossSectionHeightTolerance, step.HeightTolerance, nameof(CrossSectionHeightTolerance));
        OnPropertyChanged(nameof(CrossSectionWidthUnit));
        OnPropertyChanged(nameof(CrossSectionHeightUnit));
        CrossSectionConfigured = true;
        RefreshRecipeSummary();
        RefreshRecipeParameterSummary();
    }

    public void SetCrossSectionPreview(CrossSectionEvaluation evaluation)
    {
        ClearPlaneFlatnessPreview();
        ClearPointPairDimensionsPreview();
        ClearGapFlushPreview();
        ClearVolumePreview();
        CrossSectionConfigured = true;
        c3dCrossSectionPreview = evaluation.Result;
        c3dCrossSectionPreviewActive = true;
        CrossSectionVisible = true;
        CrossSectionWidth = evaluation.Width;
        CrossSectionHeightRange = evaluation.HeightRange;
        CrossSectionRawMinimum = evaluation.RawMinimum;
        CrossSectionRawMaximum = evaluation.RawMaximum;
        CrossSectionValidSampleCount = evaluation.ValidSampleCount;
        CrossSectionSummary = string.Create(CultureInfo.InvariantCulture, $"Cross-section: {evaluation.Result.Status} | width {evaluation.Width:F3} {CrossSectionWidthUnit}, height {evaluation.HeightRange:F3} {CrossSectionHeightUnit}");
        CrossSectionDetails = string.Create(CultureInfo.InvariantCulture, $"Row {CrossSectionRow}, columns {CrossSectionStartColumn}..{CrossSectionEndColumn} | valid {evaluation.ValidSampleCount:N0} | raw {evaluation.RawMinimum:F3}..{evaluation.RawMaximum:F3}");
        activePreviewLayerId = "layer.preview.c3d-cross-section-dimensions";
        activePreviewLayerName = "Preview: C3D Cross-section Dimensions";
        activePreviewSourceEntityId = C3DEntityId;
        activeResultEntityId = C3DCrossSectionResultEntityId;
        activeResultEntityName = "Published C3D Cross-section Dimensions";
        SetField(ref resultOverlayVisible, true, nameof(ResultOverlayVisible));
        PreviewToolResult = evaluation.Result;
        ResultSummary = FormatToolResult(PreviewToolResult);
        SelectedColorMode = "Height";
        SelectedSelectionMode = "Cross-section Dimensions";
        SelectedEntity = "C3D Cross-section Dimensions";
        SelectionSummary = CrossSectionDetails;
        MeasurementSummary = CrossSectionDetails;
        ViewerStatus = "C3D Cross-section Dimensions preview updated";
        RefreshRecipeSummary();
        RefreshRecipeParameterSummary();
        RefreshSceneContracts();
    }

    public void ClearCrossSectionPreview()
    {
        c3dCrossSectionPreview = null;
        c3dCrossSectionPreviewActive = false;
        CrossSectionVisible = false;
        CrossSectionWidth = double.NaN;
        CrossSectionHeightRange = double.NaN;
        CrossSectionRawMinimum = double.NaN;
        CrossSectionRawMaximum = double.NaN;
        CrossSectionValidSampleCount = 0;
        CrossSectionSummary = "Cross-section: preview not run";
        CrossSectionDetails = "An exact C3D row and inclusive column range are required.";
    }

    public void ClearCrossSectionRecipeStep()
    {
        ClearCrossSectionPreview();
        crossSectionStepId = CrossSectionStepId;
        crossSectionReferenceId = CrossSectionReferenceId;
        crossSectionWidthUnit = "model";
        crossSectionHeightUnit = "raw-height";
        crossSectionEnabled = true;
        SetField(ref crossSectionRow, 983, nameof(CrossSectionRow));
        SetField(ref crossSectionStartColumn, 200, nameof(CrossSectionStartColumn));
        SetField(ref crossSectionEndColumn, 1100, nameof(CrossSectionEndColumn));
        SetField(ref crossSectionExpectedWidth, 4.247, nameof(CrossSectionExpectedWidth));
        SetField(ref crossSectionWidthTolerance, 0.010, nameof(CrossSectionWidthTolerance));
        SetField(ref crossSectionExpectedHeightRange, 1708.232, nameof(CrossSectionExpectedHeightRange));
        SetField(ref crossSectionHeightTolerance, 0.010, nameof(CrossSectionHeightTolerance));
        CrossSectionConfigured = false;
        RefreshRecipeSummary();
        RefreshRecipeParameterSummary();
    }

    public void InvalidateCrossSectionPreview(string reason)
    {
        if (!c3dCrossSectionPreviewActive) return;
        ClearCrossSectionPreview();
        SetField(ref resultOverlayVisible, false, nameof(ResultOverlayVisible));
        ResetActivePreviewIdentity();
        PreviewToolResult = CreateNotRunToolResult();
        ResultSummary = FormatToolResult(PreviewToolResult);
        ViewerStatus = reason;
        RefreshSceneContracts();
    }

}
