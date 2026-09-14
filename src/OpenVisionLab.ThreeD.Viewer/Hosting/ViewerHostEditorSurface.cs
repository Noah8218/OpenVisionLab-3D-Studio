using System.ComponentModel;
using System.Windows.Input;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Viewer.ViewModels;

namespace OpenVisionLab.ThreeD.Viewer.Hosting;

/// <summary>
/// Binding surface for the Shell task workspace and C3D recipe editor.
/// <para>
/// This type is a forwarding contract. <see cref="MainWindowViewModel"/>
/// remains the only mutable inspection state owner; the surface exists so a
/// host can bind to the task workflow without taking a dependency on that
/// concrete ViewModel type.
/// </para>
/// </summary>
public sealed class ViewerHostEditorSurface : INotifyPropertyChanged, IDisposable
{
    private readonly MainWindowViewModel viewModel;
    private bool disposed;

    internal ViewerHostEditorSurface(MainWindowViewModel viewModel)
    {
        this.viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        this.viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ICommand FitPlaneCommand => viewModel.FitPlaneCommand;
    public ICommand TeachThicknessRoiCommand => viewModel.TeachThicknessRoiCommand;
    public ICommand PreviewThicknessCommand => viewModel.PreviewThicknessCommand;
    public ICommand PreviewPlaneFlatnessCommand => viewModel.PreviewPlaneFlatnessCommand;
    public ICommand PreviewPointPairDimensionsCommand => viewModel.PreviewPointPairDimensionsCommand;
    public ICommand PreviewGapFlushCommand => viewModel.PreviewGapFlushCommand;
    public ICommand PreviewVolumeCommand => viewModel.PreviewVolumeCommand;
    public ICommand PreviewCrossSectionCommand => viewModel.PreviewCrossSectionCommand;
    public ICommand TeachWarpageRoiCommand => viewModel.TeachWarpageRoiCommand;
    public ICommand PreviewWarpageCommand => viewModel.PreviewWarpageCommand;
    public ICommand SaveRecipeCommand => viewModel.SaveRecipeCommand;
    public ICommand PublishResultCommand => viewModel.PublishResultCommand;

    public string RecipeSourceName
    {
        get => viewModel.RecipeSourceName;
        set => viewModel.RecipeSourceName = value;
    }

    public string RecipeSourcePath
    {
        get => viewModel.RecipeSourcePath;
        set => viewModel.RecipeSourcePath = value;
    }

    public string RecipeSourceUnit
    {
        get => viewModel.RecipeSourceUnit;
        set => viewModel.RecipeSourceUnit = value;
    }

    public double RecipePeakTolerance
    {
        get => viewModel.RecipePeakTolerance;
        set => viewModel.RecipePeakTolerance = value;
    }

    public double RecipeTransformTranslateX
    {
        get => viewModel.RecipeTransformTranslateX;
        set => viewModel.RecipeTransformTranslateX = value;
    }

    public double RecipeTransformTranslateY
    {
        get => viewModel.RecipeTransformTranslateY;
        set => viewModel.RecipeTransformTranslateY = value;
    }

    public double RecipeTransformTranslateZ
    {
        get => viewModel.RecipeTransformTranslateZ;
        set => viewModel.RecipeTransformTranslateZ = value;
    }

    public double RecipeTransformRotateXDegrees
    {
        get => viewModel.RecipeTransformRotateXDegrees;
        set => viewModel.RecipeTransformRotateXDegrees = value;
    }

    public double RecipeTransformRotateYDegrees
    {
        get => viewModel.RecipeTransformRotateYDegrees;
        set => viewModel.RecipeTransformRotateYDegrees = value;
    }

    public double RecipeTransformRotateZDegrees
    {
        get => viewModel.RecipeTransformRotateZDegrees;
        set => viewModel.RecipeTransformRotateZDegrees = value;
    }

    public double RecipeTransformScale
    {
        get => viewModel.RecipeTransformScale;
        set => viewModel.RecipeTransformScale = value;
    }

    public double RecipeRoiLeftCenterX
    {
        get => viewModel.RecipeRoiLeftCenterX;
        set => viewModel.RecipeRoiLeftCenterX = value;
    }

    public double RecipeRoiLeftCenterZ
    {
        get => viewModel.RecipeRoiLeftCenterZ;
        set => viewModel.RecipeRoiLeftCenterZ = value;
    }

    public double RecipeRoiLeftHalfWidth
    {
        get => viewModel.RecipeRoiLeftHalfWidth;
        set => viewModel.RecipeRoiLeftHalfWidth = value;
    }

    public double RecipeRoiLeftHalfDepth
    {
        get => viewModel.RecipeRoiLeftHalfDepth;
        set => viewModel.RecipeRoiLeftHalfDepth = value;
    }

    public double RecipeRoiRightCenterX
    {
        get => viewModel.RecipeRoiRightCenterX;
        set => viewModel.RecipeRoiRightCenterX = value;
    }

    public double RecipeRoiRightCenterZ
    {
        get => viewModel.RecipeRoiRightCenterZ;
        set => viewModel.RecipeRoiRightCenterZ = value;
    }

    public double RecipeRoiRightHalfWidth
    {
        get => viewModel.RecipeRoiRightHalfWidth;
        set => viewModel.RecipeRoiRightHalfWidth = value;
    }

    public double RecipeRoiRightHalfDepth
    {
        get => viewModel.RecipeRoiRightHalfDepth;
        set => viewModel.RecipeRoiRightHalfDepth = value;
    }

    public string RecipeSummary => viewModel.RecipeSummary;
    public string AlignmentWorkflowSummary => viewModel.AlignmentWorkflowSummary;
    public string RecipeSaveSummary => viewModel.RecipeSaveSummary;
    public string RecipeParameterSummary => viewModel.RecipeParameterSummary;
    public string RecipeValidationSummary => viewModel.RecipeValidationSummary;
    public string ResultSummary => viewModel.ResultSummary;
    public string PublishedResultSummary => viewModel.PublishedResultSummary;
    public string ViewerStatus => viewModel.ViewerStatus;
    public IReadOnlyList<ResultEntity> ResultEntities => viewModel.ResultEntities;
    public bool C3DSampleVisible => viewModel.C3DSampleVisible;

    public bool ThicknessConfigured => viewModel.ThicknessConfigured;

    public int ThicknessRoiRow
    {
        get => viewModel.ThicknessRoiRow;
        set => viewModel.ThicknessRoiRow = value;
    }

    public int ThicknessRoiColumn
    {
        get => viewModel.ThicknessRoiColumn;
        set => viewModel.ThicknessRoiColumn = value;
    }

    public int ThicknessRoiRowCount
    {
        get => viewModel.ThicknessRoiRowCount;
        set => viewModel.ThicknessRoiRowCount = value;
    }

    public int ThicknessRoiColumnCount
    {
        get => viewModel.ThicknessRoiColumnCount;
        set => viewModel.ThicknessRoiColumnCount = value;
    }

    public double ThicknessMinimum
    {
        get => viewModel.ThicknessMinimum;
        set => viewModel.ThicknessMinimum = value;
    }

    public double ThicknessMaximum
    {
        get => viewModel.ThicknessMaximum;
        set => viewModel.ThicknessMaximum = value;
    }

    public int ThicknessMinimumValidSamples
    {
        get => viewModel.ThicknessMinimumValidSamples;
        set => viewModel.ThicknessMinimumValidSamples = value;
    }

    public string ThicknessUnit => viewModel.ThicknessUnit;
    public string ThicknessFrameId => viewModel.ThicknessFrameId;
    public bool ThicknessVisible => viewModel.ThicknessVisible;
    public string ThicknessSummary => viewModel.ThicknessSummary;
    public string ThicknessDetails => viewModel.ThicknessDetails;
    public double ThicknessMean => viewModel.ThicknessMean;
    public double ThicknessMinimumMeasured => viewModel.ThicknessMinimumMeasured;
    public double ThicknessMaximumMeasured => viewModel.ThicknessMaximumMeasured;
    public double ThicknessRange => viewModel.ThicknessRange;
    public int ThicknessValidSampleCount => viewModel.ThicknessValidSampleCount;

    public double PlaneFlatnessReferenceCenterX
    {
        get => viewModel.PlaneFlatnessReferenceCenterX;
        set => viewModel.PlaneFlatnessReferenceCenterX = value;
    }

    public double PlaneFlatnessReferenceCenterZ
    {
        get => viewModel.PlaneFlatnessReferenceCenterZ;
        set => viewModel.PlaneFlatnessReferenceCenterZ = value;
    }

    public double PlaneFlatnessReferenceHalfWidth
    {
        get => viewModel.PlaneFlatnessReferenceHalfWidth;
        set => viewModel.PlaneFlatnessReferenceHalfWidth = value;
    }

    public double PlaneFlatnessReferenceHalfDepth
    {
        get => viewModel.PlaneFlatnessReferenceHalfDepth;
        set => viewModel.PlaneFlatnessReferenceHalfDepth = value;
    }

    public double PlaneFlatnessTolerance
    {
        get => viewModel.PlaneFlatnessTolerance;
        set => viewModel.PlaneFlatnessTolerance = value;
    }

    public bool PlaneFlatnessVisible => viewModel.PlaneFlatnessVisible;
    public string PlaneFlatnessSummary => viewModel.PlaneFlatnessSummary;
    public string PlaneFlatnessDetails => viewModel.PlaneFlatnessDetails;

    public double PointPairExpectedDistance
    {
        get => viewModel.PointPairExpectedDistance;
        set => viewModel.PointPairExpectedDistance = value;
    }

    public double PointPairDistanceTolerance
    {
        get => viewModel.PointPairDistanceTolerance;
        set => viewModel.PointPairDistanceTolerance = value;
    }

    public double PointPairExpectedWidth
    {
        get => viewModel.PointPairExpectedWidth;
        set => viewModel.PointPairExpectedWidth = value;
    }

    public double PointPairWidthTolerance
    {
        get => viewModel.PointPairWidthTolerance;
        set => viewModel.PointPairWidthTolerance = value;
    }

    public double PointPairExpectedAngleDegrees
    {
        get => viewModel.PointPairExpectedAngleDegrees;
        set => viewModel.PointPairExpectedAngleDegrees = value;
    }

    public double PointPairAngleToleranceDegrees
    {
        get => viewModel.PointPairAngleToleranceDegrees;
        set => viewModel.PointPairAngleToleranceDegrees = value;
    }

    public bool PointPairDimensionsVisible => viewModel.PointPairDimensionsVisible;
    public string PointPairDimensionsSummary => viewModel.PointPairDimensionsSummary;
    public string PointPairDimensionsDetails => viewModel.PointPairDimensionsDetails;

    public double GapFlushExpectedGap
    {
        get => viewModel.GapFlushExpectedGap;
        set => viewModel.GapFlushExpectedGap = value;
    }

    public double GapFlushGapTolerance
    {
        get => viewModel.GapFlushGapTolerance;
        set => viewModel.GapFlushGapTolerance = value;
    }

    public double GapFlushExpectedFlush
    {
        get => viewModel.GapFlushExpectedFlush;
        set => viewModel.GapFlushExpectedFlush = value;
    }

    public double GapFlushFlushTolerance
    {
        get => viewModel.GapFlushFlushTolerance;
        set => viewModel.GapFlushFlushTolerance = value;
    }

    public bool GapFlushVisible => viewModel.GapFlushVisible;
    public string GapFlushSummary => viewModel.GapFlushSummary;
    public string GapFlushDetails => viewModel.GapFlushDetails;

    public double VolumeExpectedNet
    {
        get => viewModel.VolumeExpectedNet;
        set => viewModel.VolumeExpectedNet = value;
    }

    public double VolumeTolerance
    {
        get => viewModel.VolumeTolerance;
        set => viewModel.VolumeTolerance = value;
    }

    public bool VolumeVisible => viewModel.VolumeVisible;
    public string VolumeSummary => viewModel.VolumeSummary;
    public string VolumeDetails => viewModel.VolumeDetails;

    public int CrossSectionRow
    {
        get => viewModel.CrossSectionRow;
        set => viewModel.CrossSectionRow = value;
    }

    public int CrossSectionStartColumn
    {
        get => viewModel.CrossSectionStartColumn;
        set => viewModel.CrossSectionStartColumn = value;
    }

    public int CrossSectionEndColumn
    {
        get => viewModel.CrossSectionEndColumn;
        set => viewModel.CrossSectionEndColumn = value;
    }

    public double CrossSectionExpectedWidth
    {
        get => viewModel.CrossSectionExpectedWidth;
        set => viewModel.CrossSectionExpectedWidth = value;
    }

    public double CrossSectionWidthTolerance
    {
        get => viewModel.CrossSectionWidthTolerance;
        set => viewModel.CrossSectionWidthTolerance = value;
    }

    public double CrossSectionExpectedHeightRange
    {
        get => viewModel.CrossSectionExpectedHeightRange;
        set => viewModel.CrossSectionExpectedHeightRange = value;
    }

    public double CrossSectionHeightTolerance
    {
        get => viewModel.CrossSectionHeightTolerance;
        set => viewModel.CrossSectionHeightTolerance = value;
    }

    public bool CrossSectionVisible => viewModel.CrossSectionVisible;
    public string CrossSectionSummary => viewModel.CrossSectionSummary;
    public string CrossSectionDetails => viewModel.CrossSectionDetails;

    public bool WarpageConfigured => viewModel.WarpageConfigured;

    public int WarpageRoiRow
    {
        get => viewModel.WarpageRoiRow;
        set => viewModel.WarpageRoiRow = value;
    }

    public int WarpageRoiColumn
    {
        get => viewModel.WarpageRoiColumn;
        set => viewModel.WarpageRoiColumn = value;
    }

    public int WarpageRoiRowCount
    {
        get => viewModel.WarpageRoiRowCount;
        set => viewModel.WarpageRoiRowCount = value;
    }

    public int WarpageRoiColumnCount
    {
        get => viewModel.WarpageRoiColumnCount;
        set => viewModel.WarpageRoiColumnCount = value;
    }

    public double WarpageMaximumPeakToValley
    {
        get => viewModel.WarpageMaximumPeakToValley;
        set => viewModel.WarpageMaximumPeakToValley = value;
    }

    public int WarpageMinimumValidSamples
    {
        get => viewModel.WarpageMinimumValidSamples;
        set => viewModel.WarpageMinimumValidSamples = value;
    }

    public string WarpageUnit => viewModel.WarpageUnit;
    public string WarpageFrameId => viewModel.WarpageFrameId;
    public bool WarpageVisible => viewModel.WarpageVisible;
    public string WarpageSummary => viewModel.WarpageSummary;
    public string WarpageDetails => viewModel.WarpageDetails;

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (!disposed)
        {
            PropertyChanged?.Invoke(this, args);
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        PropertyChanged = null;
        GC.SuppressFinalize(this);
    }
}
