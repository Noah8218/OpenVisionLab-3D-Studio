using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Viewer.ViewModels;

public sealed partial class MainWindowViewModel
{
    private C3DConnectedRegionOutput? connectedRegionOutput;
    private string? selectedConnectedRegionId;

    public C3DConnectedRegionOutput? ConnectedRegionOutput => connectedRegionOutput;

    public string? SelectedConnectedRegionId => selectedConnectedRegionId;

    internal void SetConnectedRegionOverlay(
        C3DConnectedRegionOutput? output,
        string? selectedRegionId)
    {
        var normalizedSelectedRegionId = output?.Regions.FirstOrDefault(region =>
            string.Equals(region.RegionId, selectedRegionId, StringComparison.OrdinalIgnoreCase))?.RegionId;
        if (ReferenceEquals(connectedRegionOutput, output)
            && string.Equals(
                selectedConnectedRegionId,
                normalizedSelectedRegionId,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        connectedRegionOutput = output;
        selectedConnectedRegionId = normalizedSelectedRegionId;
        OnPropertyChanged(nameof(ConnectedRegionOutput));
        OnPropertyChanged(nameof(SelectedConnectedRegionId));
    }
}
