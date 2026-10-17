using System.IO;
using System.Threading;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

namespace OpenVisionLab.ThreeD.Shell.Coordination;

/// <summary>
/// Owns recipe-specific source preparation after the general source-load owner
/// has admitted a request. Recipe mutation and persistence remain with the
/// lifecycle controller; Viewer decoding and Workbench source state remain with
/// their existing owners.
/// </summary>
internal sealed class ShellWorkbenchRecipeSourceCoordinator
{
    private readonly ShellWorkbenchSourceLoadCoordinator sourceLoad;
    private readonly ToolWorkbenchViewModel workbench;
    private readonly WorkbenchViewerTeachingCoordinator teaching;
    private readonly Action<bool> updateSampleVisible;

    public ShellWorkbenchRecipeSourceCoordinator(
        ShellWorkbenchSourceLoadCoordinator sourceLoad,
        ToolWorkbenchViewModel workbench,
        WorkbenchViewerTeachingCoordinator teaching,
        Action<bool> updateSampleVisible)
    {
        this.sourceLoad = sourceLoad ?? throw new ArgumentNullException(nameof(sourceLoad));
        this.workbench = workbench ?? throw new ArgumentNullException(nameof(workbench));
        this.teaching = teaching ?? throw new ArgumentNullException(nameof(teaching));
        this.updateSampleVisible = updateSampleVisible ?? throw new ArgumentNullException(nameof(updateSampleVisible));
    }

    public async Task<ShellWorkbenchNewRecipeSourceResult> PrepareNewRecipeSourceAsync(
        ToolWorkbenchFirstRecipeSetup setup,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (setup.IsCompatibleSourceVariant)
        {
            try
            {
                // A failed copy must not replace the current Viewer input.
                var binding = await Task.Run(() => ToolRecipeSelectionSourceBindingVerifier.ReadIdentity(setup.SourcePath), cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                return new ShellWorkbenchNewRecipeSourceResult(true, binding, null);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException or NotSupportedException or OverflowException)
            {
                return new ShellWorkbenchNewRecipeSourceResult(false, null, workbench.Localization.SourceUnreadable);
            }
        }

        var sourceLoaded = sourceLoad.IsViewerSourceAlreadyLoaded(setup.SourcePath)
            || await sourceLoad.LoadWorkbenchC3DSourceAsync(
                setup.SourcePath,
                bindToWorkbench: false,
                cancellationToken: cancellationToken);
        return sourceLoaded ? ShellWorkbenchNewRecipeSourceResult.Ready : ShellWorkbenchNewRecipeSourceResult.NotReady;
    }

    public async Task<ShellWorkbenchOpenedRecipeSourceResult> ApplyCreatedVariantSourceAsync(CancellationToken cancellationToken)
    {
        var path = workbench.Source.Path;
        var loaded = await sourceLoad.LoadWorkbenchC3DSourceAsync(path, showFailureDialog: false, bindToWorkbench: false, cancellationToken);
        if (workbench.IsDisposed) return ShellWorkbenchOpenedRecipeSourceResult.NotReady;

        if (!loaded || !sourceLoad.TryGetCurrentC3DSourceBinding(path, out var binding)
            || workbench.SourceSession.SourceBinding is not { } expected
            || !ToolRecipeSelectionSourceBindingVerifier.BindingsEqual(expected, binding))
        {
            var message = loaded ? workbench.Localization.SourceIdentityMismatch : workbench.Localization.SourceUnreadable;
            sourceLoad.ClearC3DTeachingSource(message);
            updateSampleVisible(false);
            return new ShellWorkbenchOpenedRecipeSourceResult(false, message);
        }

        teaching.SyncAppliedSelections();
        updateSampleVisible(false);
        return ShellWorkbenchOpenedRecipeSourceResult.Ready;
    }

    public ShellWorkbenchOpenedRecipeSourceResult ApplyOpenedRecipeSource()
    {
        if (!workbench.IsSourceReadyForRecipe)
        {
            sourceLoad.ClearC3DTeachingSource(workbench.SourceReadinessSummary);
            updateSampleVisible(false);
            return ShellWorkbenchOpenedRecipeSourceResult.NotReady;
        }

        var sourcePath = workbench.Source.Path;
        var expectedBinding = workbench.SourceSession.SourceBinding;
        if (!sourceLoad.IsViewerSourceAlreadyLoaded(sourcePath) && !sourceLoad.LoadC3DSource(sourcePath))
        {
            var loadFailure = sourceLoad.ViewerStatus;
            sourceLoad.ClearC3DTeachingSource("Recipe source could not be loaded. Relink a valid C3D source.");
            updateSampleVisible(false);
            return new ShellWorkbenchOpenedRecipeSourceResult(false, loadFailure);
        }

        // Opening a recipe loads its declared source for display; it is not an input-import command.
        if (expectedBinding is null || !sourceLoad.TryGetCurrentC3DSourceBinding(sourcePath, out var loadedBinding)
            || !ToolRecipeSelectionSourceBindingVerifier.BindingsEqual(expectedBinding, loadedBinding))
        {
            var message = workbench.Localization.SourceIdentityMismatch;
            sourceLoad.ClearC3DTeachingSource(message);
            updateSampleVisible(false);
            return new ShellWorkbenchOpenedRecipeSourceResult(false, message);
        }

        teaching.SyncAppliedSelections();
        return ShellWorkbenchOpenedRecipeSourceResult.Ready;
    }
}

internal sealed record ShellWorkbenchNewRecipeSourceResult(
    bool IsReady,
    ToolRecipeSelectionSourceBinding? VariantBinding,
    string? FailureMessage)
{
    public static ShellWorkbenchNewRecipeSourceResult Ready { get; } =
        new(true, null, null);

    public static ShellWorkbenchNewRecipeSourceResult NotReady { get; } =
        new(false, null, null);
}

internal sealed record ShellWorkbenchOpenedRecipeSourceResult(
    bool IsReady,
    string? FailureMessage)
{
    public static ShellWorkbenchOpenedRecipeSourceResult Ready { get; } =
        new(true, null);

    public static ShellWorkbenchOpenedRecipeSourceResult NotReady { get; } =
        new(false, null);
}
