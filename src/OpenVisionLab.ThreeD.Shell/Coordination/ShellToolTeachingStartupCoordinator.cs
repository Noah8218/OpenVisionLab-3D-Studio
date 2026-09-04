using System.IO;
using OpenVisionLab.Logging;
using OpenVisionLab.ThreeD.Shell;
using OpenVisionLab.ThreeD.Shell.Verification.Smoke;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

namespace OpenVisionLab.ThreeD.Shell.Coordination;

/// <summary>
/// View-free callbacks needed to project a teaching recipe into the existing
/// Shell Viewer. The callbacks keep WPF/OpenGL ownership in the View while
/// the startup policy remains independently executable.
/// </summary>
internal sealed class ShellToolTeachingStartupCallbacks
{
    public required Action<string> ClearViewerSource { get; init; }
    public required Action<bool> UpdateSampleVisible { get; init; }
    public required Func<bool> ViewerSampleVisible { get; init; }
    public required Func<string, bool> IsViewerSourceAlreadyLoaded { get; init; }
    public required Func<string, bool> LoadViewerSource { get; init; }
    public required Func<string?> CurrentViewerSourcePath { get; init; }
    public required Func<string> ViewerStatus { get; init; }
    public required Action<string> SetWorkbenchSourceFromViewer { get; init; }
    public required Func<bool> IsWorkbenchWorkspaceSelected { get; init; }
    public required Action HideWorkbenchHudDetails { get; init; }
}

internal sealed record ShellToolTeachingStartupRequest(
    string? FixtureDirectory,
    string? RecipePath,
    string? RequestedStepId);

internal sealed record ShellToolTeachingStartupResult(string? SmokeFailure)
{
    public bool Succeeded => SmokeFailure is null;
}

/// <summary>
/// Owns command-line teaching-recipe startup policy. It resolves the optional
/// Plane Flatness fixture, opens/selects the Workbench recipe, validates source
/// readiness, and asks explicit View callbacks to project the loaded source.
/// </summary>
internal sealed class ShellToolTeachingStartupCoordinator
{
    private readonly ToolWorkbenchViewModel workbench;
    private readonly ShellToolTeachingStartupCallbacks callbacks;

    public ShellToolTeachingStartupCoordinator(
        ToolWorkbenchViewModel workbench,
        ShellToolTeachingStartupCallbacks callbacks)
    {
        this.workbench = workbench ?? throw new ArgumentNullException(nameof(workbench));
        this.callbacks = callbacks ?? throw new ArgumentNullException(nameof(callbacks));
    }

    public ShellToolTeachingStartupResult Configure(
        ShellToolTeachingStartupRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var recipePath = request.RecipePath;
        if (!string.IsNullOrWhiteSpace(request.FixtureDirectory))
        {
            try
            {
                var fixture = PlaneFlatnessLiveA3PointerSmokeFixture.Prepare(
                    request.FixtureDirectory);
                recipePath = fixture.RecipePath;
                OVLog.Write(LogCategory.UI, LogLevel.Info, fixture.Summary);
            }
            catch (Exception exception) when (exception is IOException
                or UnauthorizedAccessException
                or InvalidDataException
                or ArgumentException
                or InvalidOperationException
                or OverflowException)
            {
                OVLog.Write(
                    LogCategory.UI,
                    LogLevel.Error,
                    $"Plane Flatness live A3 fixture preparation failed: {exception}");
                return new(
                    $"Plane Flatness live A3 fixture preparation failed: {exception.Message}");
            }
        }

        if (string.IsNullOrWhiteSpace(recipePath))
        {
            return new(null);
        }

        if (!workbench.TryOpenTeachingRecipe(recipePath, out var message))
        {
            OVLog.Write(
                LogCategory.UI,
                LogLevel.Error,
                $"Tool teaching recipe command-line load failed: {message}");
            return new(null);
        }

        string? smokeFailure = null;
        if (!string.IsNullOrWhiteSpace(request.RequestedStepId)
            && !workbench.SelectPipelineStep(request.RequestedStepId))
        {
            smokeFailure = $"Tool teaching step was not found: {request.RequestedStepId}";
        }

        var source = workbench.Source;
        if (!workbench.IsSourceReadyForRecipe)
        {
            callbacks.ClearViewerSource(workbench.SourceReadinessSummary);
            callbacks.UpdateSampleVisible(false);
            OVLog.Write(
                LogCategory.UI,
                LogLevel.Warning,
                $"Tool teaching recipe source is not ready: {workbench.SourceReadinessSummary}");
            return new(smokeFailure);
        }

        if (callbacks.IsViewerSourceAlreadyLoaded(source.Path))
        {
            ApplyViewerProjection();
            return new(smokeFailure);
        }

        if (callbacks.LoadViewerSource(source.Path)
            && callbacks.CurrentViewerSourcePath() is { } loadedSourcePath)
        {
            callbacks.SetWorkbenchSourceFromViewer(loadedSourcePath);
            ApplyViewerProjection();
            return new(smokeFailure);
        }

        OVLog.Write(
            LogCategory.UI,
            LogLevel.Error,
            $"Tool teaching recipe source load failed: {callbacks.ViewerStatus()}");
        return new(smokeFailure);
    }

    private void ApplyViewerProjection()
    {
        callbacks.UpdateSampleVisible(callbacks.ViewerSampleVisible());
        if (callbacks.IsWorkbenchWorkspaceSelected())
        {
            callbacks.HideWorkbenchHudDetails();
        }
    }
}
