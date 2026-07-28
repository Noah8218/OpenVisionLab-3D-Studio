namespace OpenVisionLab.ThreeD.Viewer.ViewModels;

public sealed partial class MainWindowViewModel
{
    internal void RefreshLocalizedPresentation()
    {
        Display.RefreshLocalizedPresentation();

        string[] propertyNames =
        [
            nameof(ViewerStatus),
            nameof(BottomStatus),
            nameof(ColorModes),
            nameof(SelectedColorMode),
            nameof(RenderDensityModes),
            nameof(SelectedRenderDensity),
            nameof(RenderDensitySummary),
            nameof(CoordinateFrameSummary),
            nameof(SelectedSelectionMode),
            nameof(PickCoordinate),
            nameof(TransformSummary),
            nameof(AlignmentSummary),
            nameof(CoordinateMappingSummary),
            nameof(LineFitHudSummary),
            nameof(LineIntersectionHudSummary),
            nameof(AffineApplyHudSummary),
            nameof(RegridHeightFieldHudSummary),
            nameof(TwoPointMeasurementSummary),
            nameof(TwoPointMeasurementDetails),
            nameof(PointPairDimensionsSummary),
            nameof(PointPairDimensionsDetails),
            nameof(ThicknessSummary),
            nameof(ThicknessDetails),
            nameof(PlaneReferenceMeasurementSummary),
            nameof(PlaneReferenceMeasurementDetails),
            nameof(PlaneFlatnessSummary),
            nameof(PlaneFlatnessDetails),
            nameof(GapFlushSummary),
            nameof(GapFlushDetails),
            nameof(VolumeSummary),
            nameof(VolumeDetails),
            nameof(CrossSectionSummary),
            nameof(CrossSectionDetails),
            nameof(RoiStepMeasurementSummary),
            nameof(RoiStepMeasurementDetails),
            nameof(PerformanceSummary)
        ];

        foreach (var propertyName in propertyNames)
        {
            OnPropertyChanged(propertyName);
        }
    }
}
