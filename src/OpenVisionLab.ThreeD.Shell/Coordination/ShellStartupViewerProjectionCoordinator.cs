namespace OpenVisionLab.ThreeD.Shell.Coordination;

/// <summary>
/// View-free projection of the parsed startup Viewer intent. MainWindow wires
/// the callbacks to its ViewModel and Viewer, while this type owns the stable
/// stage/view/fit/color application order.
/// </summary>
internal sealed class ShellStartupViewerProjectionCallbacks
{
    public required Action SelectWorkbenchWorkspace { get; init; }
    public required Action SelectTeachWorkspace { get; init; }
    public required Action SelectInspectWorkspace { get; init; }
    public required Action SelectReviewWorkspace { get; init; }
    public required Action UseTopView { get; init; }
    public required Action UsePerspectiveView { get; init; }
    public required Action FitRoi { get; init; }
    public required Action<double> SetHeightColorMinimumRaw { get; init; }
    public required Action<double> SetHeightColorMaximumRaw { get; init; }
}

/// <summary>
/// Applies immutable Shell startup Viewer intent without depending on WPF
/// controls, UI lifetime objects, or a concrete Viewer type.
/// </summary>
internal sealed class ShellStartupViewerProjectionCoordinator
{
    private readonly ShellStartupViewerProjectionCallbacks callbacks;

    public ShellStartupViewerProjectionCoordinator(
        ShellStartupViewerProjectionCallbacks callbacks)
    {
        this.callbacks = callbacks ?? throw new ArgumentNullException(nameof(callbacks));
    }

    public void Apply(ShellStartupConfigurationPlan configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        switch (configuration.StageWorkspace)
        {
            case ShellWorkspaceMode.Workbench:
                callbacks.SelectWorkbenchWorkspace();
                break;
            case ShellWorkspaceMode.Teach:
                callbacks.SelectTeachWorkspace();
                break;
            case ShellWorkspaceMode.Inspect:
                callbacks.SelectInspectWorkspace();
                break;
            case ShellWorkspaceMode.Review:
                callbacks.SelectReviewWorkspace();
                break;
        }

        switch (configuration.ViewerView)
        {
            case ShellStartupViewerView.Top:
                callbacks.UseTopView();
                break;
            case ShellStartupViewerView.Perspective:
                callbacks.UsePerspectiveView();
                break;
        }

        if (configuration.FitRoi)
        {
            callbacks.FitRoi();
        }

        if (configuration.HeightColorMinimumRaw is { } minimum)
        {
            callbacks.SetHeightColorMinimumRaw(minimum);
        }

        if (configuration.HeightColorMaximumRaw is { } maximum)
        {
            callbacks.SetHeightColorMaximumRaw(maximum);
        }
    }
}
