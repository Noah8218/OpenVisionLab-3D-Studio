using System.Globalization;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Viewer.ViewModels;

public sealed partial class MainWindowViewModel
{
    private IC3DLineGeometry? workbenchFirstIntersectionLine;
    private IC3DLineGeometry? workbenchSecondIntersectionLine;
    private C3DLineIntersectionFeature? workbenchLineIntersection;
    private bool isWorkbenchLineIntersectionPublished;
    private bool lineIntersectionFirstLineVisible = true;
    private bool lineIntersectionSecondLineVisible = true;
    private bool lineIntersectionClosestConnectorVisible = true;
    private bool lineIntersectionCornerAnchorVisible = true;
    private string lineIntersectionHudSummary = string.Empty;

    public IC3DLineGeometry? WorkbenchFirstIntersectionLine => workbenchFirstIntersectionLine;
    public IC3DLineGeometry? WorkbenchSecondIntersectionLine => workbenchSecondIntersectionLine;
    public C3DLineIntersectionFeature? WorkbenchLineIntersection => workbenchLineIntersection;
    public bool IsWorkbenchLineIntersectionPublished => isWorkbenchLineIntersectionPublished;
    public bool LineIntersectionFirstLineVisible { get => lineIntersectionFirstLineVisible; set => SetField(ref lineIntersectionFirstLineVisible, value); }
    public bool LineIntersectionSecondLineVisible { get => lineIntersectionSecondLineVisible; set => SetField(ref lineIntersectionSecondLineVisible, value); }
    public bool LineIntersectionClosestConnectorVisible { get => lineIntersectionClosestConnectorVisible; set => SetField(ref lineIntersectionClosestConnectorVisible, value); }
    public bool LineIntersectionCornerAnchorVisible { get => lineIntersectionCornerAnchorVisible; set => SetField(ref lineIntersectionCornerAnchorVisible, value); }
    public bool LineIntersectionHudVisible => workbenchLineIntersection is not null;
    public string LineIntersectionHudSummary { get => lineIntersectionHudSummary; private set => SetField(ref lineIntersectionHudSummary, value); }

    internal void SetWorkbenchLineIntersection(
        IC3DLineGeometry firstLine,
        IC3DLineGeometry secondLine,
        C3DLineIntersectionFeature output,
        bool isPublished)
    {
        ArgumentNullException.ThrowIfNull(firstLine);
        ArgumentNullException.ThrowIfNull(secondLine);
        ArgumentNullException.ThrowIfNull(output);
        workbenchFirstIntersectionLine = firstLine;
        workbenchSecondIntersectionLine = secondLine;
        workbenchLineIntersection = output;
        isWorkbenchLineIntersectionPublished = isPublished;
        var status = isPublished ? "Published" : "Preview - not published";
        SelectionSummary = string.Create(
            CultureInfo.InvariantCulture,
            $"{status} Line Intersection | {output.OutputRole} | XYZ ({output.CornerAnchorX:F3}, {output.CornerAnchorY:F3}, {output.CornerAnchorZ:F3}) | gap {output.ClosestApproachDistance:F6}");
        ViewerStatus = isPublished
            ? "Published full-XYZ line intersection feature extraction"
            : "Full-XYZ line intersection Preview - not published";
        LineIntersectionHudSummary = string.Create(
            CultureInfo.InvariantCulture,
            $"Line Intersection {status} | {output.OutputRole} | gap {output.ClosestApproachDistance:G6} | acute {output.AcuteAngleDegrees:G5}°");
        OnPropertyChanged(nameof(WorkbenchFirstIntersectionLine));
        OnPropertyChanged(nameof(WorkbenchSecondIntersectionLine));
        OnPropertyChanged(nameof(WorkbenchLineIntersection));
        OnPropertyChanged(nameof(IsWorkbenchLineIntersectionPublished));
        OnPropertyChanged(nameof(LineIntersectionHudVisible));
    }

    internal void SetWorkbenchLineIntersectionInputs(IC3DLineGeometry firstLine, IC3DLineGeometry secondLine)
    {
        ArgumentNullException.ThrowIfNull(firstLine);
        ArgumentNullException.ThrowIfNull(secondLine);
        workbenchFirstIntersectionLine = firstLine;
        workbenchSecondIntersectionLine = secondLine;
        workbenchLineIntersection = null;
        isWorkbenchLineIntersectionPublished = false;
        SelectionSummary = "Published line-geometry inputs | explicit Line Intersection Preview required for corner evidence";
        ViewerStatus = "Viewing two published line-geometry inputs";
        LineIntersectionHudSummary = string.Empty;
        OnPropertyChanged(nameof(WorkbenchFirstIntersectionLine));
        OnPropertyChanged(nameof(WorkbenchSecondIntersectionLine));
        OnPropertyChanged(nameof(WorkbenchLineIntersection));
        OnPropertyChanged(nameof(IsWorkbenchLineIntersectionPublished));
        OnPropertyChanged(nameof(LineIntersectionHudVisible));
    }

    internal void ClearWorkbenchLineIntersection()
    {
        workbenchFirstIntersectionLine = null;
        workbenchSecondIntersectionLine = null;
        workbenchLineIntersection = null;
        isWorkbenchLineIntersectionPublished = false;
        LineIntersectionHudSummary = string.Empty;
        ViewerStatus = "3D Line Intersection preview cleared";
        OnPropertyChanged(nameof(WorkbenchFirstIntersectionLine));
        OnPropertyChanged(nameof(WorkbenchSecondIntersectionLine));
        OnPropertyChanged(nameof(WorkbenchLineIntersection));
        OnPropertyChanged(nameof(IsWorkbenchLineIntersectionPublished));
        OnPropertyChanged(nameof(LineIntersectionHudVisible));
    }
}
