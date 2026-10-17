using System.Globalization;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Viewer.ViewModels;

public sealed partial class MainWindowViewModel
{
    private C3DHeightDifferenceEdgePointSet? workbenchHeightDifferenceEdge;
    private C3DHeightDifferenceEdgePoint? selectedWorkbenchHeightDifferenceEdgePoint;
    private bool isWorkbenchHeightDifferenceEdgePublished;

    public C3DHeightDifferenceEdgePointSet? WorkbenchHeightDifferenceEdge => workbenchHeightDifferenceEdge;
    public C3DHeightDifferenceEdgePoint? SelectedWorkbenchHeightDifferenceEdgePoint => selectedWorkbenchHeightDifferenceEdgePoint;
    public bool IsWorkbenchHeightDifferenceEdgePublished => isWorkbenchHeightDifferenceEdgePublished;

    internal void SetWorkbenchHeightDifferenceEdge(C3DHeightDifferenceEdgePointSet output, bool isPublished)
    {
        ArgumentNullException.ThrowIfNull(output);
        workbenchHeightDifferenceEdge = output;
        selectedWorkbenchHeightDifferenceEdgePoint = output.Points.FirstOrDefault();
        isWorkbenchHeightDifferenceEdgePublished = isPublished;
        SelectionSummary = isPublished
            ? $"Published EdgePointSet | {output.Points.Count:N0} points | {output.ContentSha256[..12]}"
            : $"Edge Preview output - not published | {output.Points.Count:N0} points | {output.ContentSha256[..12]}";
        OnPropertyChanged(nameof(WorkbenchHeightDifferenceEdge));
        OnPropertyChanged(nameof(SelectedWorkbenchHeightDifferenceEdgePoint));
        OnPropertyChanged(nameof(IsWorkbenchHeightDifferenceEdgePublished));
    }

    internal void ClearWorkbenchHeightDifferenceEdge()
    {
        workbenchHeightDifferenceEdge = null;
        selectedWorkbenchHeightDifferenceEdgePoint = null;
        isWorkbenchHeightDifferenceEdgePublished = false;
        OnPropertyChanged(nameof(WorkbenchHeightDifferenceEdge));
        OnPropertyChanged(nameof(SelectedWorkbenchHeightDifferenceEdgePoint));
        OnPropertyChanged(nameof(IsWorkbenchHeightDifferenceEdgePublished));
    }

    internal bool TrySelectWorkbenchHeightDifferenceEdgePoint(int row, int column)
    {
        var output = workbenchHeightDifferenceEdge;
        if (output is null)
        {
            return false;
        }

        var scanline = output.ComparisonAxis == C3DHeightDifferenceComparisonAxis.AcrossColumns ? row : column;
        var point = output.Points.FirstOrDefault(item =>
            item.ScanlineIndex == scanline
            && (output.ComparisonAxis == C3DHeightDifferenceComparisonAxis.AcrossColumns
                ? Math.Abs(item.X - column) <= 2.0
                : Math.Abs(item.Z - row) <= 2.0));
        if (point is null)
        {
            return false;
        }

        selectedWorkbenchHeightDifferenceEdgePoint = point;
        SelectedEntity = "Height Difference Edge Point";
        PickCoordinate = string.Create(CultureInfo.InvariantCulture, $"midpoint ({point.X:0.##}, {point.Y:0.##}, {point.Z:0.##})");
        SelectionSummary = string.Create(
            CultureInfo.InvariantCulture,
            $"Edge marker | ({point.FirstRow},{point.FirstColumn}) h0={point.FirstHeight:0.##} -> ({point.SecondRow},{point.SecondColumn}) h1={point.SecondHeight:0.##} | delta={point.SignedDelta:0.##} | magnitude={point.Magnitude:0.##} | midpoint=({point.X:0.##},{point.Y:0.##},{point.Z:0.##})");
        ViewerStatus = isWorkbenchHeightDifferenceEdgePublished
            ? "Selected published Height Difference Edge marker"
            : "Selected Height Difference Edge Preview marker - not published";
        OnPropertyChanged(nameof(SelectedWorkbenchHeightDifferenceEdgePoint));
        return true;
    }
}
