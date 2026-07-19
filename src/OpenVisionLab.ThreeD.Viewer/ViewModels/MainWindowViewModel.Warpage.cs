using System.Globalization;
using System.IO;
using System.Windows.Input;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Tools;

namespace OpenVisionLab.ThreeD.Viewer.ViewModels;

public sealed partial class MainWindowViewModel
{
    public const string C3DWarpageEntityId = "source.c3d-warpage";
    public const string C3DWarpageResultEntityId = "result.c3d-warpage";
    public const string WarpageRoiSelectionMode = "Warpage ROI Teach";

    private const string WarpageStepId = "step.c3d-warpage";
    private const string WarpageReferenceId = "reference.c3d-warpage-best-fit-roi";
    private const string WarpageDefaultFrameId = "frame.c3d-grid-index";
    private readonly RelayCommand teachWarpageRoiCommand;
    private readonly RelayCommand previewWarpageCommand;
    private ToolResult? c3dWarpagePreview;
    private bool c3dWarpagePreviewActive;
    private bool warpageConfigured;
    private string warpageStepId = WarpageStepId;
    private string warpageSourceEntityId = C3DWarpageEntityId;
    private string warpageReferenceId = WarpageReferenceId;
    private string warpageReferenceMode = C3DWarpageRecipe.BestFitInspectionRoiReferenceMode;
    private int warpageRoiRow = 900;
    private int warpageRoiColumn = 570;
    private int warpageRoiRowCount = 160;
    private int warpageRoiColumnCount = 160;
    private double warpageMaximumPeakToValley = 10000.0;
    private int warpageMinimumValidSamples = 3;
    private string warpageUnit = "raw-height";
    private string warpageFrameId = WarpageDefaultFrameId;
    private bool warpageVisible;
    private string warpageSummary = "Warpage: teach one C3D ROI, then run Preview.";
    private string warpageDetails = "Best-fit residuals use declared raw-height only; physical calibration is not inferred.";
    private double warpagePeakToValley = double.NaN;
    private double warpageRms = double.NaN;
    private double warpageMinimumResidual = double.NaN;
    private double warpageMaximumResidual = double.NaN;
    private int warpageValidSampleCount;

    public event EventHandler? PreviewWarpageRequested;

    public ICommand TeachWarpageRoiCommand => teachWarpageRoiCommand;

    public ICommand PreviewWarpageCommand => previewWarpageCommand;

    public bool WarpageConfigured
    {
        get => warpageConfigured;
        private set => SetField(ref warpageConfigured, value);
    }

    public int WarpageRoiRow
    {
        get => warpageRoiRow;
        set => SetWarpageParameter(ref warpageRoiRow, Math.Max(0, value), nameof(WarpageRoiRow), "Warpage ROI changed; run Preview Warpage again");
    }

    public int WarpageRoiColumn
    {
        get => warpageRoiColumn;
        set => SetWarpageParameter(ref warpageRoiColumn, Math.Max(0, value), nameof(WarpageRoiColumn), "Warpage ROI changed; run Preview Warpage again");
    }

    public int WarpageRoiRowCount
    {
        get => warpageRoiRowCount;
        set => SetWarpageParameter(ref warpageRoiRowCount, Math.Max(1, value), nameof(WarpageRoiRowCount), "Warpage ROI size changed; run Preview Warpage again");
    }

    public int WarpageRoiColumnCount
    {
        get => warpageRoiColumnCount;
        set => SetWarpageParameter(ref warpageRoiColumnCount, Math.Max(1, value), nameof(WarpageRoiColumnCount), "Warpage ROI size changed; run Preview Warpage again");
    }

    public double WarpageMaximumPeakToValley
    {
        get => warpageMaximumPeakToValley;
        set => SetWarpageParameter(
            ref warpageMaximumPeakToValley,
            Math.Max(double.Epsilon, CoerceFinite(value, warpageMaximumPeakToValley)),
            nameof(WarpageMaximumPeakToValley),
            "Warpage P2V limit changed; run Preview Warpage again");
    }

    public int WarpageMinimumValidSamples
    {
        get => warpageMinimumValidSamples;
        set => SetWarpageParameter(ref warpageMinimumValidSamples, Math.Max(3, value), nameof(WarpageMinimumValidSamples), "Warpage minimum sample count changed; run Preview Warpage again");
    }

    public string WarpageUnit => warpageUnit;

    public string WarpageFrameId => warpageFrameId;

    public string WarpageReferenceMode => warpageReferenceMode;

    public bool WarpageVisible
    {
        get => warpageVisible;
        private set => SetField(ref warpageVisible, value);
    }

    public string WarpageSummary
    {
        get => warpageSummary;
        private set => SetField(ref warpageSummary, value);
    }

    public string WarpageDetails
    {
        get => warpageDetails;
        private set => SetField(ref warpageDetails, value);
    }

    public double WarpagePeakToValley
    {
        get => warpagePeakToValley;
        private set => SetField(ref warpagePeakToValley, value);
    }

    public double WarpageRms
    {
        get => warpageRms;
        private set => SetField(ref warpageRms, value);
    }

    public double WarpageMinimumResidual
    {
        get => warpageMinimumResidual;
        private set => SetField(ref warpageMinimumResidual, value);
    }

    public double WarpageMaximumResidual
    {
        get => warpageMaximumResidual;
        private set => SetField(ref warpageMaximumResidual, value);
    }

    public int WarpageValidSampleCount
    {
        get => warpageValidSampleCount;
        private set => SetField(ref warpageValidSampleCount, value);
    }

    public void BeginWarpageRoiTeaching()
    {
        SelectedSelectionMode = WarpageRoiSelectionMode;
        SelectionOverlayVisible = true;
        SelectedEntity = "C3D Warpage ROI";
        WarpageSummary = "Warpage ROI teaching: click one C3D grid location.";
        WarpageDetails = string.Create(
            CultureInfo.InvariantCulture,
            $"Current grid ROI row {WarpageRoiRow}, column {WarpageRoiColumn}, size {WarpageRoiRowCount} x {WarpageRoiColumnCount} | P2V limit {WarpageMaximumPeakToValley:F3} {WarpageUnit}");
        SelectionSummary = WarpageSummary;
        MeasurementSummary = WarpageDetails;
        ViewerStatus = "Warpage ROI teaching: click the C3D surface to place the ROI";
    }

    public void SetWarpageRoiFromCenter(int row, int column, int sourceRows, int sourceColumns)
    {
        if (sourceRows <= 0 || sourceColumns <= 0)
        {
            ViewerStatus = "Warpage ROI teaching requires C3D grid dimensions";
            return;
        }

        var rowCount = Math.Min(Math.Max(1, WarpageRoiRowCount), sourceRows);
        var columnCount = Math.Min(Math.Max(1, WarpageRoiColumnCount), sourceColumns);
        var boundedRow = Math.Clamp(row - rowCount / 2, 0, sourceRows - rowCount);
        var boundedColumn = Math.Clamp(column - columnCount / 2, 0, sourceColumns - columnCount);
        var changed = SetField(ref warpageRoiRow, boundedRow, nameof(WarpageRoiRow));
        changed |= SetField(ref warpageRoiColumn, boundedColumn, nameof(WarpageRoiColumn));
        changed |= SetField(ref warpageRoiRowCount, rowCount, nameof(WarpageRoiRowCount));
        changed |= SetField(ref warpageRoiColumnCount, columnCount, nameof(WarpageRoiColumnCount));
        WarpageConfigured = true;

        if (changed)
        {
            InvalidateWarpagePreview("Warpage ROI changed; run Preview Warpage again");
        }

        WarpageSummary = string.Create(
            CultureInfo.InvariantCulture,
            $"Warpage ROI taught: row {WarpageRoiRow}, column {WarpageRoiColumn}, size {WarpageRoiRowCount} x {WarpageRoiColumnCount}");
        WarpageDetails = string.Create(
            CultureInfo.InvariantCulture,
            $"Anchor cell ({row}, {column}) | best-fit residual P2V limit {WarpageMaximumPeakToValley:F3} {WarpageUnit} | frame {WarpageFrameId}");
        SelectedSelectionMode = WarpageRoiSelectionMode;
        SelectedEntity = "C3D Warpage ROI";
        SelectionOverlayVisible = true;
        SelectionSummary = WarpageSummary;
        MeasurementSummary = WarpageDetails;
        ViewerStatus = "Warpage ROI taught; configure the P2V limit and run Preview Warpage";
        RefreshRecipeSummary();
        RefreshRecipeParameterSummary();
        RefreshCommandCanExecute();
    }

    public C3DWarpageStep CreateWarpageRecipeStep() =>
        new(
            warpageStepId,
            warpageSourceEntityId,
            warpageReferenceId,
            warpageReferenceMode,
            new C3DGridRoi(WarpageRoiRow, WarpageRoiColumn, WarpageRoiRowCount, WarpageRoiColumnCount),
            new C3DWarpageAcceptance(WarpageMaximumPeakToValley),
            warpageUnit,
            warpageFrameId,
            WarpageMinimumValidSamples,
            Enabled: true);

    public void SetWarpageRecipeStep(C3DWarpageStep step)
    {
        ArgumentNullException.ThrowIfNull(step);
        warpageStepId = step.Id;
        warpageSourceEntityId = step.SourceEntityId;
        warpageReferenceId = step.ReferenceId;
        warpageReferenceMode = step.ReferenceMode;
        warpageUnit = step.Unit;
        warpageFrameId = step.FrameId;
        SetField(ref warpageRoiRow, step.Roi.Row, nameof(WarpageRoiRow));
        SetField(ref warpageRoiColumn, step.Roi.Column, nameof(WarpageRoiColumn));
        SetField(ref warpageRoiRowCount, step.Roi.RowCount, nameof(WarpageRoiRowCount));
        SetField(ref warpageRoiColumnCount, step.Roi.ColumnCount, nameof(WarpageRoiColumnCount));
        SetField(ref warpageMaximumPeakToValley, step.Acceptance.MaximumPeakToValley, nameof(WarpageMaximumPeakToValley));
        SetField(ref warpageMinimumValidSamples, step.MinimumValidSamples, nameof(WarpageMinimumValidSamples));
        OnPropertyChanged(nameof(WarpageUnit));
        OnPropertyChanged(nameof(WarpageFrameId));
        OnPropertyChanged(nameof(WarpageReferenceMode));
        WarpageConfigured = true;
        RefreshRecipeSummary();
        RefreshRecipeParameterSummary();
        RefreshCommandCanExecute();
    }

    public void SetWarpageRecipeLoaded(string recipePath, string sourceName, string sourcePath, string sourceUnit)
    {
        SetField(ref recipeFileName, Path.GetFileName(recipePath), nameof(RecipeSummary));
        RecipeSourceName = sourceName;
        RecipeSourcePath = sourcePath;
        RecipeSourceUnit = sourceUnit;
        RecipePeakTolerance = WarpageMaximumPeakToValley;
        RecipeSaveSummary = $"Recipe loaded: {Path.GetFileName(recipePath)}";
        RefreshRecipeSummary();
        RefreshRecipeParameterSummary();
    }

    public void SetWarpagePreview(C3DWarpageEvaluation evaluation)
    {
        ArgumentNullException.ThrowIfNull(evaluation);
        c3dHeightDeviationPreview = null;
        ClearThicknessPreview();
        ClearPlaneFlatnessPreview();
        ClearPointPairDimensionsPreview();
        ClearGapFlushPreview();
        ClearVolumePreview();
        ClearCrossSectionPreview();
        WarpageConfigured = true;
        c3dWarpagePreview = evaluation.Result;
        c3dWarpagePreviewActive = true;
        WarpageVisible = true;
        WarpagePeakToValley = evaluation.PeakToValley;
        WarpageRms = evaluation.Rms;
        WarpageMinimumResidual = evaluation.MinimumResidual;
        WarpageMaximumResidual = evaluation.MaximumResidual;
        WarpageValidSampleCount = evaluation.ValidSampleCount;
        WarpageSummary = string.Create(
            CultureInfo.InvariantCulture,
            $"Warpage: {evaluation.Result.Status} | P2V {evaluation.PeakToValley:F3} / {WarpageMaximumPeakToValley:F3} {WarpageUnit} | RMS {evaluation.Rms:F3}");
        WarpageDetails = string.Create(
            CultureInfo.InvariantCulture,
            $"Best-fit ROI row {evaluation.Roi.Row}, column {evaluation.Roi.Column}, size {evaluation.Roi.RowCount} x {evaluation.Roi.ColumnCount} | valid {evaluation.ValidSampleCount:N0} | residual {evaluation.MinimumResidual:F3}..{evaluation.MaximumResidual:F3} {WarpageUnit} | package {evaluation.PackageResultStatus}/{evaluation.PackageErrorCode} | declared scalar, uncalibrated");

        activePreviewLayerId = "layer.preview.c3d-warpage";
        activePreviewLayerName = "Preview: C3D Warpage";
        activePreviewSourceEntityId = C3DWarpageEntityId;
        activeResultEntityId = C3DWarpageResultEntityId;
        activeResultEntityName = "Published C3D Warpage";
        SetField(ref resultOverlayVisible, true, nameof(ResultOverlayVisible));
        PreviewToolResult = evaluation.Result;
        ResultSummary = FormatToolResult(PreviewToolResult);
        SelectedColorMode = "Deviation";
        SelectedSelectionMode = WarpageRoiSelectionMode;
        SelectedEntity = "C3D Warpage";
        SelectionSummary = WarpageDetails;
        MeasurementSummary = WarpageDetails;
        ViewerStatus = "C3D Warpage preview updated";
        RefreshRecipeSummary();
        RefreshRecipeParameterSummary();
        RefreshSceneContracts();
    }

    public void ClearWarpagePreview()
    {
        c3dWarpagePreview = null;
        c3dWarpagePreviewActive = false;
        WarpageVisible = false;
        WarpagePeakToValley = double.NaN;
        WarpageRms = double.NaN;
        WarpageMinimumResidual = double.NaN;
        WarpageMaximumResidual = double.NaN;
        WarpageValidSampleCount = 0;
        WarpageSummary = WarpageConfigured
            ? "Warpage: ROI taught; Preview is required."
            : "Warpage: teach one C3D ROI, then run Preview.";
        WarpageDetails = WarpageConfigured
            ? string.Create(CultureInfo.InvariantCulture, $"Best-fit grid ROI row {WarpageRoiRow}, column {WarpageRoiColumn}, size {WarpageRoiRowCount} x {WarpageRoiColumnCount} | declared scalar, uncalibrated")
            : "Best-fit residuals use declared raw-height only; physical calibration is not inferred.";
    }

    public void ClearWarpageRecipeStep()
    {
        ClearWarpagePreview();
        warpageStepId = WarpageStepId;
        warpageSourceEntityId = C3DWarpageEntityId;
        warpageReferenceId = WarpageReferenceId;
        warpageReferenceMode = C3DWarpageRecipe.BestFitInspectionRoiReferenceMode;
        warpageUnit = "raw-height";
        warpageFrameId = WarpageDefaultFrameId;
        SetField(ref warpageRoiRow, 900, nameof(WarpageRoiRow));
        SetField(ref warpageRoiColumn, 570, nameof(WarpageRoiColumn));
        SetField(ref warpageRoiRowCount, 160, nameof(WarpageRoiRowCount));
        SetField(ref warpageRoiColumnCount, 160, nameof(WarpageRoiColumnCount));
        SetField(ref warpageMaximumPeakToValley, 10000.0, nameof(WarpageMaximumPeakToValley));
        SetField(ref warpageMinimumValidSamples, 3, nameof(WarpageMinimumValidSamples));
        OnPropertyChanged(nameof(WarpageUnit));
        OnPropertyChanged(nameof(WarpageFrameId));
        OnPropertyChanged(nameof(WarpageReferenceMode));
        WarpageConfigured = false;
        RefreshRecipeSummary();
        RefreshRecipeParameterSummary();
        RefreshCommandCanExecute();
    }

    public void InvalidateWarpagePreview(string reason)
    {
        if (!c3dWarpagePreviewActive)
        {
            return;
        }

        ClearWarpagePreview();
        SetField(ref resultOverlayVisible, false, nameof(ResultOverlayVisible));
        ResetActivePreviewIdentity();
        PreviewToolResult = CreateNotRunToolResult();
        ResultSummary = FormatToolResult(PreviewToolResult);
        ViewerStatus = reason;
        RefreshSceneContracts();
    }

    private void SetWarpageParameter(ref int storage, int value, string propertyName, string invalidationReason)
    {
        if (SetField(ref storage, value, propertyName))
        {
            MarkWarpageConfigurationChanged(invalidationReason);
        }
    }

    private void SetWarpageParameter(ref double storage, double value, string propertyName, string invalidationReason)
    {
        if (SetField(ref storage, value, propertyName))
        {
            MarkWarpageConfigurationChanged(invalidationReason);
        }
    }

    private void MarkWarpageConfigurationChanged(string invalidationReason)
    {
        WarpageConfigured = true;
        InvalidateWarpagePreview(invalidationReason);
        RefreshRecipeSummary();
        RefreshRecipeParameterSummary();
        RefreshCommandCanExecute();
    }
}
