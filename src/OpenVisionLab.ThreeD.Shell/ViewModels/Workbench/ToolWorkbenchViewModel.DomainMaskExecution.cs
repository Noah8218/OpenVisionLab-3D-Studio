using System.Threading.Tasks;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

public sealed partial class ToolWorkbenchViewModel
{
    public bool IsSelectedStepDomainMask => domainMaskExecutionOwner.IsSelectedStepDomainMask;
    public bool IsDomainMaskPreviewRunning => domainMaskExecutionOwner.IsPreviewRunning;
    public bool HasCurrentDomainMaskPreview => domainMaskExecutionOwner.HasCurrentPreview;
    public bool IsDomainMaskPreviewStale => domainMaskExecutionOwner.IsPreviewStale;
    public bool IsDomainMaskPreviewPublished => domainMaskExecutionOwner.IsPreviewPublished;
    public C3DHeightFieldSnapshot? CurrentDomainMaskPreviewOutput => domainMaskExecutionOwner.CurrentOutput;
    public string? CurrentDomainMaskPreviewPath => domainMaskExecutionOwner.CurrentOutputPath;
    public string DomainMaskExecutionSummary => domainMaskExecutionOwner.ExecutionSummary;
    public string DomainMaskOutputSummary => domainMaskExecutionOwner.OutputSummary;

    public Task<bool> PreviewSelectedDomainMaskAsync() =>
        domainMaskExecutionOwner.PreviewAsync();

    private bool CanPreviewSelectedDomainMask() =>
        domainMaskExecutionOwner.CanPreview();

    private void PublishSelectedDomainMask() =>
        domainMaskExecutionOwner.Publish();

    private void CancelDomainMaskPreview() =>
        domainMaskExecutionOwner.Cancel();

    private void MarkDomainMaskPreviewStaleIfNeeded(object? sender) =>
        domainMaskExecutionOwner.MarkStaleIfNeeded(sender);

    private void MarkDomainMaskPreviewStaleIfUpstreamChanged() =>
        domainMaskExecutionOwner.MarkStaleIfUpstreamChanged();

    private void ClearDomainMaskPreview(string summary) =>
        domainMaskExecutionOwner.Clear(summary);

    private void RefreshDomainMaskExecutionState() =>
        domainMaskExecutionOwner.RefreshState();

    private void SetDomainMaskRunning(bool value) =>
        domainMaskExecutionOwner.SetRunning(value);

    internal bool TryGetPublishedDomainMaskOutput(
        string outputEntityId,
        out C3DHeightFieldSnapshot? output) =>
        domainMaskExecutionOwner.TryGetPublishedOutput(outputEntityId, out output);

    internal C3DHeightFieldSnapshot? TryGetCurrentPublishedHeightField(string entityId)
    {
        if (string.Equals(entityId, Source.Id, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (removeOutlierExecutionOwner.TryGetPublishedInput(entityId) is { } outlier)
        {
            return outlier.Output;
        }

        if (string.Equals(
                filterPreviewOutput?.EntityId,
                entityId,
                StringComparison.OrdinalIgnoreCase)
            && isFilterPreviewPublished)
        {
            return filterPreviewOutput;
        }

        if (string.Equals(
                CurrentLevelSurfacePreviewOutput?.EntityId,
                entityId,
                StringComparison.OrdinalIgnoreCase)
            && IsLevelSurfacePreviewPublished)
        {
            return CurrentLevelSurfacePreviewOutput;
        }

        if (TryGetPublishedRoiCropOutput(entityId, out var crop)
            && crop is not null)
        {
            return crop;
        }

        if (TryGetPublishedDomainMaskOutput(entityId, out var domainMask)
            && domainMask is not null)
        {
            return domainMask;
        }

        if (connectedRegionExecutionOwner.TryGetRestoredUpstreamInput(entityId) is { } restored)
        {
            return restored.Output;
        }

        return null;
    }
}
