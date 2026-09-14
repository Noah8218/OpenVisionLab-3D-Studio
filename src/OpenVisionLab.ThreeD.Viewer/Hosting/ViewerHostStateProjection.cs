using System.Runtime.CompilerServices;
using OpenVisionLab.ThreeD.Viewer.ViewModels;

namespace OpenVisionLab.ThreeD.Viewer.Hosting;

/// <summary>
/// Projects the concrete Viewer ViewModel into the immutable public host state.
/// The notification coordinator consumes the result through a delegate so it
/// does not own or depend on the ViewModel type.
/// </summary>
internal static class ViewerHostStateProjection
{
    public static ViewerHostState From(
        MainWindowViewModel viewModel,
        ViewerHostSourceState? sourceState = null)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        return new ViewerHostState(
            viewModel.C3DSampleVisible,
            viewModel.SelectedEntity,
            viewModel.SelectedSelectionMode,
            viewModel.PickCoordinate,
            viewModel.MeasurementSummary,
            viewModel.ResultSummary,
            viewModel.RecipeSummary,
            viewModel.ViewerStatus,
            viewModel.CoordinateFrameSummary)
        {
            GlbSampleVisible = viewModel.GlbSampleVisible,
            LazSampleVisible = viewModel.LazSampleVisible,
            SelectionOverlayVisible = viewModel.SelectionOverlayVisible,
            SelectionSummary = viewModel.SelectionSummary,
            NominalActualState = MapNominalActualState(viewModel.NominalActual.State),
            HasNominalActualInput = viewModel.NominalActualInput is not null,
            NominalActualDisplay = new ViewerHostNominalActualDisplayState(
                viewModel.NominalActual.InputsReady,
                viewModel.NominalActual.EvidenceSummary)
            {
                StateSummary = viewModel.NominalActual.StateSummary,
                DirectionSummary = viewModel.NominalActual.DirectionSummary,
                CurrentDisplaySamplingSummary = viewModel.NominalActual.CurrentDisplaySamplingSummary,
                NextPreviewSamplingSummary = viewModel.NominalActual.NextPreviewSamplingSummary,
                DisplaySamplingChangePending = viewModel.NominalActual.DisplaySamplingChangePending,
                ProgressPercent = viewModel.NominalActual.ProgressPercent,
                DistributionVisible = viewModel.NominalActual.DistributionVisible,
                DistributionSummary = viewModel.NominalActual.DistributionSummary
            },
            ThicknessEvidence = new ViewerHostThicknessEvidenceState(
                viewModel.ThicknessVisible,
                viewModel.ThicknessSummary,
                viewModel.ThicknessDetails,
                viewModel.ThicknessMean,
                viewModel.ThicknessMinimumMeasured,
                viewModel.ThicknessMaximumMeasured,
                viewModel.ThicknessRange,
                viewModel.ThicknessValidSampleCount),
            Profile = new ViewerHostProfileState(
                viewModel.ProfileVisible,
                viewModel.ProfileSummary,
                viewModel.ProfileEndpointSummary,
                viewModel.ProfileRange,
                viewModel.ProfilePathData,
                viewModel.ProfileValidSampleCount,
                viewModel.ProfileMissingSampleCount,
                viewModel.ProfileLinkedCursorVisible,
                viewModel.ProfileLinkedCursorX,
                viewModel.ProfileLinkedCursorY,
                viewModel.ProfileLinkedCursorMarkerLeft,
                viewModel.ProfileLinkedCursorMarkerTop,
                viewModel.ProfileLinkedCursorSummary),
            SectionProfile = new ViewerHostSectionProfileState(
                viewModel.SectionProfileVisible,
                viewModel.SectionProfileSampleCount,
                viewModel.SectionProfileSummary,
                viewModel.SectionProfileRange,
                viewModel.SectionProfilePathData),
            SourceSamples = new ViewerHostSourceSamplesState(
                viewModel.LazSampleName,
                viewModel.LazSampleSummary,
                viewModel.GlbSampleName,
                viewModel.GlbSampleSummary),
            SceneDisplay = new ViewerHostSceneDisplayState(
                viewModel.SceneContractSummary,
                viewModel.TransformSummary,
                viewModel.AlignmentSummary,
                viewModel.CoordinateMappingSummary,
                viewModel.C3DSamplePointCount,
                viewModel.LazSamplingSummary),
            EntityLayers = Array.AsReadOnly(viewModel.EntityLayers.ToArray()),
            Presentation = new ViewerHostPresentationState(
                viewModel.HudDetailsVisible,
                viewModel.C3DHeightDistributionVisible,
                viewModel.C3DHeightDistributionSourceSha256,
                viewModel.C3DHeightColorMinimumRaw,
                viewModel.C3DHeightColorMaximumRaw,
                viewModel.C3DHeightColorRangeAuto,
                viewModel.C3DHeightColorRangeRevision,
                viewModel.C3DHeightColorRangeSummary)
            {
                AvailableColorMaps = Array.AsReadOnly(viewModel.Display.AvailableColorMaps.ToArray()),
                SelectedColorMap = viewModel.Display.SelectedColorMap,
                CanSelectColorMap = viewModel.Display.CanSelectColorMap,
                DiagnosticChannelOptions = Array.AsReadOnly(viewModel.Display.DiagnosticChannelOptions.ToArray()),
                SelectedDiagnosticChannel = viewModel.Display.SelectedDiagnosticChannel,
                CanSelectDiagnosticChannel = viewModel.Display.CanSelectDiagnosticChannel,
                DiagnosticChannelSummary = viewModel.Display.DiagnosticChannelSummary,
                ResultOverlayVisible = viewModel.ResultOverlayVisible,
                MeasurementVisible = viewModel.MeasurementVisible
            },
            Inspection = new ViewerHostInspectionState(
                viewModel.PreviewToolResult.Status,
                RuntimeHelpers.GetHashCode(viewModel.PreviewToolResult),
                viewModel.ResultEntities.Count,
                RuntimeHelpers.GetHashCode(viewModel.ResultEntities),
                viewModel.ProjectionMode),
            Sources = sourceState ?? ViewerHostSourceState.Empty
        };
    }

    private static ViewerNominalActualState MapNominalActualState(
        NominalActualComparisonState state) => state switch
    {
        NominalActualComparisonState.NoInputs => ViewerNominalActualState.NoInputs,
        NominalActualComparisonState.InputsReady => ViewerNominalActualState.InputsReady,
        NominalActualComparisonState.PreviewStale => ViewerNominalActualState.PreviewStale,
        NominalActualComparisonState.PreviewRunning => ViewerNominalActualState.PreviewRunning,
        NominalActualComparisonState.PreviewReady => ViewerNominalActualState.PreviewReady,
        NominalActualComparisonState.Published => ViewerNominalActualState.Published,
        NominalActualComparisonState.Failed => ViewerNominalActualState.Failed,
        _ => ViewerNominalActualState.Unknown
    };
}
