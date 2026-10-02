using System.Threading.Tasks;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

public sealed partial class ToolWorkbenchViewModel
{
    public bool IsSelectedStepGridPolygonDomain => gridPolygonDomainExecutionOwner.IsSelectedStep;
    public bool IsGridPolygonDomainPreviewRunning => gridPolygonDomainExecutionOwner.IsPreviewRunning;
    public bool HasCurrentGridPolygonDomainPreview => gridPolygonDomainExecutionOwner.HasCurrentPreview;
    public bool IsGridPolygonDomainPreviewStale => gridPolygonDomainExecutionOwner.IsPreviewStale;
    public bool IsGridPolygonDomainPreviewPublished => gridPolygonDomainExecutionOwner.IsPreviewPublished;
    public C3DGridDomainArtifact? CurrentGridPolygonDomainArtifact => gridPolygonDomainExecutionOwner.CurrentArtifact;
    public string? CurrentGridPolygonDomainArtifactPath => gridPolygonDomainExecutionOwner.CurrentArtifactPath;
    public string GridPolygonDomainExecutionSummary => gridPolygonDomainExecutionOwner.ExecutionSummary;
    public string GridPolygonDomainOutputSummary => gridPolygonDomainExecutionOwner.OutputSummary;

    public Task<bool> PreviewSelectedGridPolygonDomainAsync() =>
        gridPolygonDomainExecutionOwner.PreviewAsync();

    private bool CanPreviewSelectedGridPolygonDomain() =>
        gridPolygonDomainExecutionOwner.CanPreview();

    private void PublishSelectedGridPolygonDomain() =>
        gridPolygonDomainExecutionOwner.Publish();

    private void CancelGridPolygonDomainPreview() =>
        gridPolygonDomainExecutionOwner.Cancel();

    private void MarkGridPolygonDomainPreviewStaleIfNeeded(object? sender) =>
        gridPolygonDomainExecutionOwner.MarkStaleIfNeeded(sender);

    private void MarkGridPolygonDomainPreviewStaleIfUpstreamChanged() =>
        gridPolygonDomainExecutionOwner.MarkStaleIfUpstreamChanged();

    private void ClearGridPolygonDomainPreview(string summary) =>
        gridPolygonDomainExecutionOwner.Clear(summary);

    private void RefreshGridPolygonDomainExecutionState() =>
        gridPolygonDomainExecutionOwner.RefreshState();

    private void SetGridPolygonDomainRunning(bool value) =>
        gridPolygonDomainExecutionOwner.SetRunning(value);
}
