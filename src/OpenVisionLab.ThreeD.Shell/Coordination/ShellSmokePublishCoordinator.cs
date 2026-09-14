namespace OpenVisionLab.ThreeD.Shell.Coordination;

/// <summary>
/// Supplies the Viewer and Shell callbacks required by the Smoke publish
/// workflow. The coordinator owns only ordering and failure policy.
/// </summary>
internal sealed class ShellSmokePublishCallbacks
{
    public required Func<bool> PublishCurrentPreview { get; init; }
    public required Action ShowReviewWorkspace { get; init; }
    public required Func<string, bool> SaveCurrentRecipe { get; init; }
}

/// <summary>
/// Applies the explicit Smoke Preview → Publish → Review → Save sequence
/// without depending on WPF controls, Window lifetime, or concrete Viewer
/// types.
/// </summary>
internal sealed class ShellSmokePublishCoordinator
{
    private readonly ShellSmokePublishCallbacks callbacks;

    public ShellSmokePublishCoordinator(ShellSmokePublishCallbacks callbacks)
    {
        this.callbacks = callbacks ?? throw new ArgumentNullException(nameof(callbacks));
    }

    public bool TryPublishAndSave(
        bool publish,
        string? saveRecipePath,
        out string? failure)
    {
        failure = null;
        if (publish && !callbacks.PublishCurrentPreview())
        {
            failure = "Viewer Publish failed because current Preview evidence was unavailable.";
            return false;
        }

        if (publish)
        {
            callbacks.ShowReviewWorkspace();
        }

        return saveRecipePath is null || callbacks.SaveCurrentRecipe(saveRecipePath);
    }
}
