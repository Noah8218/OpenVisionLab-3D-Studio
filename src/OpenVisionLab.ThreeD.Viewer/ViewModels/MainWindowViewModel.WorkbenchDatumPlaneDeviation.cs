using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Viewer.ViewModels;

public sealed partial class MainWindowViewModel
{
    private C3DThreePointPlaneFeature? workbenchDatumPlane;
    private ToolRecipeSelection? workbenchDatumPlaneMeasurementSelection;
    private C3DDatumPlaneDeviationFeature? workbenchDatumPlaneDeviation;
    private bool isWorkbenchDatumPlaneDeviationPublished;

    public C3DThreePointPlaneFeature? WorkbenchDatumPlane => workbenchDatumPlane;
    public ToolRecipeSelection? WorkbenchDatumPlaneMeasurementSelection => workbenchDatumPlaneMeasurementSelection;
    public C3DDatumPlaneDeviationFeature? WorkbenchDatumPlaneDeviation => workbenchDatumPlaneDeviation;
    public bool IsWorkbenchDatumPlaneDeviationPublished => isWorkbenchDatumPlaneDeviationPublished;

    internal void SetWorkbenchDatumPlaneDeviation(
        C3DThreePointPlaneFeature plane,
        ToolRecipeSelection measurementSelection,
        C3DDatumPlaneDeviationFeature output,
        bool isPublished)
    {
        ArgumentNullException.ThrowIfNull(plane);
        ArgumentNullException.ThrowIfNull(measurementSelection);
        ArgumentNullException.ThrowIfNull(output);
        workbenchDatumPlane = plane;
        workbenchDatumPlaneMeasurementSelection = measurementSelection;
        workbenchDatumPlaneDeviation = output;
        isWorkbenchDatumPlaneDeviationPublished = isPublished;
        var state = isPublished ? "Published" : "Preview - not published";
        SelectionSummary = $"{state} Datum Plane Deviation | {output.OutputRole} | P2V {output.PeakToValleyRawHeight:G6} raw-height | {output.ContentSha256[..12]}";
        ViewerStatus = isPublished
            ? "Published datum-plane raw-height deviation (local software result)"
            : "Datum-plane raw-height deviation Preview - not published";
        OnPropertyChanged(nameof(WorkbenchDatumPlane));
        OnPropertyChanged(nameof(WorkbenchDatumPlaneMeasurementSelection));
        OnPropertyChanged(nameof(WorkbenchDatumPlaneDeviation));
        OnPropertyChanged(nameof(IsWorkbenchDatumPlaneDeviationPublished));
    }

    internal void ClearWorkbenchDatumPlaneDeviation()
    {
        workbenchDatumPlane = null;
        workbenchDatumPlaneMeasurementSelection = null;
        workbenchDatumPlaneDeviation = null;
        isWorkbenchDatumPlaneDeviationPublished = false;
        ViewerStatus = "Datum-plane raw-height deviation preview cleared";
        OnPropertyChanged(nameof(WorkbenchDatumPlane));
        OnPropertyChanged(nameof(WorkbenchDatumPlaneMeasurementSelection));
        OnPropertyChanged(nameof(WorkbenchDatumPlaneDeviation));
        OnPropertyChanged(nameof(IsWorkbenchDatumPlaneDeviationPublished));
    }
}
