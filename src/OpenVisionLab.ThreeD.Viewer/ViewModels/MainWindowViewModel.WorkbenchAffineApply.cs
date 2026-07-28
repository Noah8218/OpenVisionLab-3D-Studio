using System.Globalization;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Viewer.ViewModels;

public sealed partial class MainWindowViewModel
{
    private C3DTransformedPointCloud? workbenchAffineApply;
    private bool isWorkbenchAffineApplyPublished;
    private string affineApplyHudSummary = string.Empty;

    public C3DTransformedPointCloud? WorkbenchAffineApply => workbenchAffineApply;
    public bool IsWorkbenchAffineApplyPublished => isWorkbenchAffineApplyPublished;
    public bool AffineApplyHudVisible => workbenchAffineApply is not null;
    public string AffineApplyHudSummary { get => affineApplyHudSummary; private set => SetField(ref affineApplyHudSummary, value); }

    internal void SetWorkbenchAffineApply(C3DTransformedPointCloud output, bool isPublished)
    {
        ArgumentNullException.ThrowIfNull(output);
        workbenchAffineApply = output;
        isWorkbenchAffineApplyPublished = isPublished;
        var status = isPublished ? "Published" : "Preview - not published";
        SelectionSummary = string.Create(
            CultureInfo.InvariantCulture,
            $"{status} A2 TransformedPointCloud | finite {output.FinitePointCount:N0} | missing {output.MissingPointCount:N0} | source-grid locators retained");
        ViewerStatus = isPublished
            ? "Published full-XYZ transformed point cloud - visual reference-frame display only"
            : "Full-XYZ transformed point-cloud Preview - not published";
        AffineApplyHudSummary = string.Create(
            CultureInfo.InvariantCulture,
            $"A2 {status} | {output.FinitePointCount:N0} finite points | raw C3D unchanged | no re-grid / no measurement");
        OnPropertyChanged(nameof(WorkbenchAffineApply));
        OnPropertyChanged(nameof(IsWorkbenchAffineApplyPublished));
        OnPropertyChanged(nameof(AffineApplyHudVisible));
    }

    internal void ClearWorkbenchAffineApply()
    {
        workbenchAffineApply = null;
        isWorkbenchAffineApplyPublished = false;
        AffineApplyHudSummary = string.Empty;
        OnPropertyChanged(nameof(WorkbenchAffineApply));
        OnPropertyChanged(nameof(IsWorkbenchAffineApplyPublished));
        OnPropertyChanged(nameof(AffineApplyHudVisible));
    }
}
