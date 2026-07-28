using System.Globalization;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Viewer.ViewModels;

public sealed partial class MainWindowViewModel
{
    private C3DTransformedHeightField? workbenchRegridHeightField;
    private bool isWorkbenchRegridHeightFieldPublished;
    private string regridHeightFieldHudSummary = string.Empty;

    public C3DTransformedHeightField? WorkbenchRegridHeightField => workbenchRegridHeightField;
    public bool IsWorkbenchRegridHeightFieldPublished => isWorkbenchRegridHeightFieldPublished;
    public bool RegridHeightFieldHudVisible => workbenchRegridHeightField is not null;
    public string RegridHeightFieldHudSummary { get => regridHeightFieldHudSummary; private set => SetField(ref regridHeightFieldHudSummary, value); }

    internal void SetWorkbenchRegridHeightField(C3DTransformedHeightField output, bool isPublished)
    {
        ArgumentNullException.ThrowIfNull(output);
        workbenchRegridHeightField = output;
        isWorkbenchRegridHeightFieldPublished = isPublished;
        var status = isPublished ? "Published" : "Preview - not published";
        SelectionSummary = string.Create(
            CultureInfo.InvariantCulture,
            $"{status} A3 TransformedHeightField | {output.PopulatedCellCount:N0}/{output.Cells.Count:N0} cells | coverage {output.CoverageRatio:P2} | missing {output.MissingCellCount:N0}");
        ViewerStatus = isPublished
            ? "Published deterministic reference-grid height field - display frame only"
            : "Reference-grid height-field Preview - not published";
        RegridHeightFieldHudSummary = string.Create(
            CultureInfo.InvariantCulture,
            $"A3 {status} | {output.PopulatedCellCount:N0}/{output.Cells.Count:N0} populated | coverage {output.CoverageRatio:P2} | holes preserved | no measurement");
        OnPropertyChanged(nameof(WorkbenchRegridHeightField));
        OnPropertyChanged(nameof(IsWorkbenchRegridHeightFieldPublished));
        OnPropertyChanged(nameof(RegridHeightFieldHudVisible));
    }

    internal void ClearWorkbenchRegridHeightField()
    {
        workbenchRegridHeightField = null;
        isWorkbenchRegridHeightFieldPublished = false;
        RegridHeightFieldHudSummary = string.Empty;
        OnPropertyChanged(nameof(WorkbenchRegridHeightField));
        OnPropertyChanged(nameof(IsWorkbenchRegridHeightFieldPublished));
        OnPropertyChanged(nameof(RegridHeightFieldHudVisible));
    }
}
