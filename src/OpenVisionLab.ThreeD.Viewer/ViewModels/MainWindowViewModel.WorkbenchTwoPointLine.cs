using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Viewer.ViewModels;

public sealed partial class MainWindowViewModel
{
    private C3DTwoPointLineFeature? workbenchTwoPointLine;
    private bool isWorkbenchTwoPointLinePublished;

    public C3DTwoPointLineFeature? WorkbenchTwoPointLine => workbenchTwoPointLine;
    public bool IsWorkbenchTwoPointLinePublished => isWorkbenchTwoPointLinePublished;

    internal void SetWorkbenchTwoPointLine(C3DTwoPointLineFeature output, bool isPublished)
    {
        ArgumentNullException.ThrowIfNull(output);
        workbenchTwoPointLine = output;
        isWorkbenchTwoPointLinePublished = isPublished;
        var state = isPublished ? "Published" : "Preview - not published";
        SelectionSummary = $"{state} 2-Point Line | {output.OutputRole} | segment {output.SegmentLength:G6} source-coordinate | {output.ContentSha256[..12]}";
        ViewerStatus = isPublished ? "Published ordered 2-Point Line construction" : "Ordered 2-Point Line Preview - not published";
        OnPropertyChanged(nameof(WorkbenchTwoPointLine));
        OnPropertyChanged(nameof(IsWorkbenchTwoPointLinePublished));
    }

    internal void ClearWorkbenchTwoPointLine()
    {
        workbenchTwoPointLine = null;
        isWorkbenchTwoPointLinePublished = false;
        ViewerStatus = "2-Point Line preview cleared";
        OnPropertyChanged(nameof(WorkbenchTwoPointLine));
        OnPropertyChanged(nameof(IsWorkbenchTwoPointLinePublished));
    }
}
