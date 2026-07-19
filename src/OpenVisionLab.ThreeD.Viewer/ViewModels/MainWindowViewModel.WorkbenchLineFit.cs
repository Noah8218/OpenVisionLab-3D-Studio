using System.Globalization;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Viewer.ViewModels;

public sealed partial class MainWindowViewModel
{
    private C3DLineFeature? workbenchLineFit;
    private C3DLineFeaturePointDiagnostic? selectedWorkbenchLineFitPoint;
    private bool isWorkbenchLineFitPublished;
    private bool lineFitInliersVisible = true;
    private bool lineFitOutliersVisible = true;
    private bool lineFitSegmentVisible = true;
    private bool lineFitSelectedResidualVisible = true;
    private string lineFitHudSummary = string.Empty;

    public C3DLineFeature? WorkbenchLineFit => workbenchLineFit;
    public C3DLineFeaturePointDiagnostic? SelectedWorkbenchLineFitPoint => selectedWorkbenchLineFitPoint;
    public bool IsWorkbenchLineFitPublished => isWorkbenchLineFitPublished;
    public bool LineFitInliersVisible { get => lineFitInliersVisible; set => SetField(ref lineFitInliersVisible, value); }
    public bool LineFitOutliersVisible { get => lineFitOutliersVisible; set => SetField(ref lineFitOutliersVisible, value); }
    public bool LineFitSegmentVisible { get => lineFitSegmentVisible; set => SetField(ref lineFitSegmentVisible, value); }
    public bool LineFitSelectedResidualVisible { get => lineFitSelectedResidualVisible; set => SetField(ref lineFitSelectedResidualVisible, value); }
    public bool LineFitHudVisible => workbenchLineFit is not null;
    public string LineFitHudSummary { get => lineFitHudSummary; private set => SetField(ref lineFitHudSummary, value); }

    internal void SetWorkbenchLineFit(C3DLineFeature output, bool isPublished)
    {
        ArgumentNullException.ThrowIfNull(output);
        workbenchLineFit = output;
        selectedWorkbenchLineFitPoint = output.PointDiagnostics.FirstOrDefault();
        isWorkbenchLineFitPublished = isPublished;
        SelectionSummary = isPublished
            ? $"Published LineFeature | {output.Diagnostics.InlierCount:N0}/{output.Diagnostics.InputPointCount:N0} inliers | {output.ContentSha256[..12]}"
            : $"Line Fit Preview - not published | {output.Diagnostics.InlierCount:N0}/{output.Diagnostics.InputPointCount:N0} inliers | {output.ContentSha256[..12]}";
        ViewerStatus = isPublished
            ? "Published 3D Line Fit feature extraction"
            : "3D Line Fit Preview - not published";
        LineFitHudSummary = isPublished
            ? $"3D Line Fit Published | {output.Diagnostics.InlierCount:N0}/{output.Diagnostics.InputPointCount:N0} inliers | residual RMS {output.Diagnostics.ResidualRms:G6}"
            : $"3D Line Fit Preview | {output.Diagnostics.InlierCount:N0}/{output.Diagnostics.InputPointCount:N0} inliers | residual RMS {output.Diagnostics.ResidualRms:G6}";
        OnPropertyChanged(nameof(WorkbenchLineFit));
        OnPropertyChanged(nameof(SelectedWorkbenchLineFitPoint));
        OnPropertyChanged(nameof(IsWorkbenchLineFitPublished));
        OnPropertyChanged(nameof(LineFitHudVisible));
    }

    internal void ClearWorkbenchLineFit()
    {
        workbenchLineFit = null;
        selectedWorkbenchLineFitPoint = null;
        isWorkbenchLineFitPublished = false;
        ViewerStatus = "3D Line Fit preview cleared";
        LineFitHudSummary = string.Empty;
        OnPropertyChanged(nameof(WorkbenchLineFit));
        OnPropertyChanged(nameof(SelectedWorkbenchLineFitPoint));
        OnPropertyChanged(nameof(IsWorkbenchLineFitPublished));
        OnPropertyChanged(nameof(LineFitHudVisible));
    }

    internal bool TrySelectWorkbenchLineFitPoint(int row, int column)
    {
        var output = workbenchLineFit;
        if (output is null) return false;
        var point = output.PointDiagnostics
            .OrderBy(item => Math.Abs(item.Z - row) + Math.Abs(item.X - column))
            .FirstOrDefault(item => Math.Abs(item.Z - row) <= 2 && Math.Abs(item.X - column) <= 2);
        if (point is null) return false;
        selectedWorkbenchLineFitPoint = point;
        SelectedEntity = "3D Line Fit point";
        PickCoordinate = string.Create(CultureInfo.InvariantCulture, $"XYZ ({point.X:F3}, {point.Y:F3}, {point.Z:F3})");
        SelectionSummary = string.Create(CultureInfo.InvariantCulture, $"Line Fit {(point.IsInlier ? "inlier" : "outlier")} | scanline {point.ScanlineIndex} | residual {point.OrthogonalResidual:F6} source-coordinate | projected ({point.ProjectedX:F3}, {point.ProjectedY:F3}, {point.ProjectedZ:F3})");
        ViewerStatus = isWorkbenchLineFitPublished ? "Selected published 3D Line Fit diagnostic" : "Selected 3D Line Fit Preview diagnostic - not published";
        OnPropertyChanged(nameof(SelectedWorkbenchLineFitPoint));
        return true;
    }

    internal bool TrySelectWorkbenchLineFitDiagnostic(int inputPointIndex)
    {
        var point = workbenchLineFit?.PointDiagnostics.FirstOrDefault(item => item.InputPointIndex == inputPointIndex);
        if (point is null) return false;
        selectedWorkbenchLineFitPoint = point;
        OnPropertyChanged(nameof(SelectedWorkbenchLineFitPoint));
        return true;
    }
}
