using OpenVisionLab.ThreeD.Viewer.Models;
using OpenVisionLab.ThreeD.Viewer.ViewModels;

namespace OpenVisionLab.ThreeD.Viewer.Hosting;

/// <summary>
/// Named ports used by <see cref="ViewerHostOperationFacade"/> to cross back
/// into the WPF control only for lifecycle-sensitive work.
/// </summary>
internal sealed class ViewerHostOperationCallbacks
{
    public ViewerHostOperationCallbacks(
        Action fitAll,
        Action fitSelection,
        Action fitRoi,
        Action useTopView,
        Action usePerspectiveView,
        Action resetView,
        Func<string, bool> saveRecipe)
    {
        FitAll = fitAll ?? throw new ArgumentNullException(nameof(fitAll));
        FitSelection = fitSelection ?? throw new ArgumentNullException(nameof(fitSelection));
        FitRoi = fitRoi ?? throw new ArgumentNullException(nameof(fitRoi));
        UseTopView = useTopView ?? throw new ArgumentNullException(nameof(useTopView));
        UsePerspectiveView = usePerspectiveView ?? throw new ArgumentNullException(nameof(usePerspectiveView));
        ResetView = resetView ?? throw new ArgumentNullException(nameof(resetView));
        SaveRecipe = saveRecipe ?? throw new ArgumentNullException(nameof(saveRecipe));
    }

    public Action FitAll { get; }
    public Action FitSelection { get; }
    public Action FitRoi { get; }
    public Action UseTopView { get; }
    public Action UsePerspectiveView { get; }
    public Action ResetView { get; }
    public Func<string, bool> SaveRecipe { get; }
}

/// <summary>
/// Owns the policy behind the public Viewer Host operations.
/// <para>
/// The facade validates inputs, applies the disposed gate, and delegates
/// mutable state changes to <see cref="MainWindowViewModel"/>. The WPF
/// control supplies only the callbacks that require its lifecycle or command
/// composition context.
/// </para>
/// </summary>
internal sealed class ViewerHostOperationFacade
{
    private readonly MainWindowViewModel viewModel;
    private readonly Func<bool> isDisposed;
    private readonly Action requestVisibleFrame;
    private readonly ViewerHostOperationCallbacks callbacks;

    public ViewerHostOperationFacade(
        MainWindowViewModel viewModel,
        Func<bool> isDisposed,
        Action requestVisibleFrame,
        ViewerHostOperationCallbacks callbacks)
    {
        this.viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        this.isDisposed = isDisposed ?? throw new ArgumentNullException(nameof(isDisposed));
        this.requestVisibleFrame = requestVisibleFrame ?? throw new ArgumentNullException(nameof(requestVisibleFrame));
        this.callbacks = callbacks ?? throw new ArgumentNullException(nameof(callbacks));
    }

    public ViewerCameraState CaptureCameraState() => viewModel.CaptureCameraState();

    public bool TryApplyCameraState(ViewerCameraState state)
    {
        if (IsDisposed)
        {
            return false;
        }

        if (!viewModel.TryApplyCameraState(state))
        {
            return false;
        }

        requestVisibleFrame();
        return true;
    }

    public bool TrySetSelectionMode(string selectionMode)
    {
        if (IsDisposed || string.IsNullOrWhiteSpace(selectionMode))
        {
            return false;
        }

        viewModel.SelectedSelectionMode = selectionMode;
        return string.Equals(
            viewModel.SelectedSelectionMode,
            selectionMode,
            StringComparison.Ordinal);
    }

    public bool TrySetSelectionOverlayVisible(bool visible)
    {
        if (IsDisposed)
        {
            return false;
        }

        viewModel.SelectionOverlayVisible = visible;
        return viewModel.SelectionOverlayVisible == visible;
    }

    public bool TrySetHudDetailsVisible(bool visible)
    {
        if (IsDisposed)
        {
            return false;
        }

        viewModel.HudDetailsVisible = visible;
        return viewModel.HudDetailsVisible == visible;
    }

    public bool TrySetC3DSampleVisible(bool visible)
    {
        if (IsDisposed)
        {
            return false;
        }

        viewModel.C3DSampleVisible = visible;
        return viewModel.C3DSampleVisible == visible;
    }

    public bool TrySetSelectedColorMap(string colorMap)
    {
        if (IsDisposed || string.IsNullOrWhiteSpace(colorMap))
        {
            return false;
        }

        viewModel.SelectedColorMode = colorMap;
        return string.Equals(
            viewModel.SelectedColorMode,
            colorMap,
            StringComparison.Ordinal);
    }

    public bool TrySetSelectedDiagnosticChannel(ViewerDiagnosticChannelOption? channel)
    {
        if (IsDisposed)
        {
            return false;
        }

        viewModel.Display.SelectedDiagnosticChannel = channel;
        return viewModel.Display.SelectedDiagnosticChannel?.Channel == channel?.Channel;
    }

    public bool TrySetResultOverlayVisible(bool visible)
    {
        if (IsDisposed)
        {
            return false;
        }

        viewModel.ResultOverlayVisible = visible;
        return viewModel.ResultOverlayVisible == visible;
    }

    public bool TrySetMeasurementVisible(bool visible)
    {
        if (IsDisposed)
        {
            return false;
        }

        viewModel.MeasurementVisible = visible;
        return viewModel.MeasurementVisible == visible;
    }

    public bool TrySetC3DHeightColorMinimumRaw(double value)
    {
        if (IsDisposed || !double.IsFinite(value))
        {
            return false;
        }

        viewModel.C3DHeightColorMinimumRaw = value;
        return true;
    }

    public bool TrySetC3DHeightColorMaximumRaw(double value)
    {
        if (IsDisposed || !double.IsFinite(value))
        {
            return false;
        }

        viewModel.C3DHeightColorMaximumRaw = value;
        return true;
    }

    public bool TryShiftC3DHeightColorMinimum(int direction)
    {
        if (IsDisposed || direction == 0)
        {
            return false;
        }

        viewModel.ShiftC3DHeightColorMinimum(direction);
        return true;
    }

    public bool TryShiftC3DHeightColorMaximum(int direction)
    {
        if (IsDisposed || direction == 0)
        {
            return false;
        }

        viewModel.ShiftC3DHeightColorMaximum(direction);
        return true;
    }

    public bool TryResetC3DHeightColorRange()
    {
        if (IsDisposed)
        {
            return false;
        }

        viewModel.ResetC3DHeightColorRange();
        return true;
    }

    public bool TryApplyLinkedC3DHeightColorRange(double minimum, double maximum)
    {
        if (IsDisposed)
        {
            return false;
        }

        return viewModel.TryApplyLinkedC3DHeightColorRange(minimum, maximum);
    }

    public void FitAll() => Execute(callbacks.FitAll);

    public void FitSelection() => Execute(callbacks.FitSelection);

    public void FitRoi() => Execute(callbacks.FitRoi);

    public void UseTopView() => Execute(callbacks.UseTopView);

    public void UsePerspectiveView() => Execute(callbacks.UsePerspectiveView);

    public void ResetView() => Execute(callbacks.ResetView);

    public bool SaveRecipe(string path) => !IsDisposed && callbacks.SaveRecipe(path);

    public bool PublishCurrentPreviewResult()
    {
        if (IsDisposed || !EnsureRecipeOutputEnabled())
        {
            return false;
        }

        if (viewModel.NominalActualInput is not null)
        {
            if (!viewModel.NominalActual.CanPublish)
            {
                return false;
            }

            viewModel.NominalActual.PublishCommand.Execute(null);
            return viewModel.NominalActual.State == NominalActualComparisonState.Published;
        }

        return viewModel.PublishPreviewResult();
    }

    private bool IsDisposed => isDisposed();

    private void Execute(Action operation)
    {
        if (!IsDisposed)
        {
            operation();
        }
    }

    private bool EnsureRecipeOutputEnabled()
    {
        if (viewModel.RecipeOutputEnabled)
        {
            return true;
        }

        viewModel.ViewerStatus = "Recipe output is disabled; Preview and Publish did not run";
        return false;
    }
}
