namespace OpenVisionLab.ThreeD.Shell.Coordination;

internal sealed class ShellViewerPointerSmokeCallbacks
{
    public required Func<Task<bool>> ApplyConfiguredNextDensity { get; init; }
    public required Func<bool> ApplyConfiguredPick { get; init; }
    public required Func<Task<bool>> RunConfiguredPointerInputRegression { get; init; }
    public required Func<string, Task<bool>> RunProfilePointerSmoke { get; init; }
    public required Func<string, Task<bool>> RunTeachingOrientedBoxPointerSmoke { get; init; }
    public required Func<string> ViewerStatus { get; init; }
}

internal sealed record ShellViewerPointerSmokeFailure(string Message, bool Abort);

/// <summary>
/// Owns the configured Viewer pointer Smoke sequence and its continue/abort
/// policy. Concrete Viewer pointer and OpenGL operations remain callbacks.
/// </summary>
internal sealed class ShellViewerPointerSmokeCoordinator
{
    private readonly ShellViewerPointerSmokeCallbacks callbacks;

    public ShellViewerPointerSmokeCoordinator(ShellViewerPointerSmokeCallbacks callbacks)
    {
        this.callbacks = callbacks ?? throw new ArgumentNullException(nameof(callbacks));
    }

    public async Task<ShellViewerPointerSmokeFailure?> RunAsync(
        string? profileReportPath,
        string? orientedBoxReportPath)
    {
        if (!await callbacks.ApplyConfiguredNextDensity())
        {
            return new ShellViewerPointerSmokeFailure(callbacks.ViewerStatus(), Abort: false);
        }

        if (!callbacks.ApplyConfiguredPick())
        {
            return new ShellViewerPointerSmokeFailure(callbacks.ViewerStatus(), Abort: false);
        }

        if (!await callbacks.RunConfiguredPointerInputRegression())
        {
            return new ShellViewerPointerSmokeFailure(callbacks.ViewerStatus(), Abort: false);
        }

        if (profileReportPath is not null
            && !await callbacks.RunProfilePointerSmoke(profileReportPath))
        {
            return new ShellViewerPointerSmokeFailure(
                "Interactive height-profile pointer smoke failed.",
                Abort: true);
        }

        if (orientedBoxReportPath is not null
            && !await callbacks.RunTeachingOrientedBoxPointerSmoke(orientedBoxReportPath))
        {
            return new ShellViewerPointerSmokeFailure(
                "OrientedBox3D actual-pointer editing did not preserve the Review/Apply boundary.",
                Abort: true);
        }

        return null;
    }
}
