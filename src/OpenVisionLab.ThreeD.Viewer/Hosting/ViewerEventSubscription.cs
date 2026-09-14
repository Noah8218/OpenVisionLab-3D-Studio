using OpenVisionLab.ThreeD.Viewer.Models;
using System.ComponentModel;
using OpenVisionLab.ThreeD.Viewer.Localization;
using OpenVisionLab.ThreeD.Viewer.ViewModels;

namespace OpenVisionLab.ThreeD.Viewer.Hosting;

/// <summary>
/// The concrete Viewer supplies these callbacks; this owner supplies one
/// symmetric attach/detach boundary for every ViewModel and language event.
/// </summary>
internal sealed class ViewerEventHandlerSet
{
    public required EventHandler FitAllRequested { get; init; }
    public required EventHandler FitSelectionRequested { get; init; }
    public required EventHandler FitRoiRequested { get; init; }
    public required EventHandler TopViewRequested { get; init; }
    public required EventHandler PerspectiveViewRequested { get; init; }
    public required EventHandler ResetRequested { get; init; }
    public required EventHandler OpenRecipeRequested { get; init; }
    public required EventHandler SaveRecipeRequested { get; init; }
    public required EventHandler ApplyRoiAlignmentRequested { get; init; }
    public required EventHandler FitPlaneRequested { get; init; }
    public required EventHandler PreviewThicknessRequested { get; init; }
    public required EventHandler PreviewWarpageRequested { get; init; }
    public required EventHandler PreviewPlaneFlatnessRequested { get; init; }
    public required EventHandler PreviewPointPairDimensionsRequested { get; init; }
    public required EventHandler PreviewGapFlushRequested { get; init; }
    public required EventHandler PreviewVolumeRequested { get; init; }
    public required EventHandler PreviewCrossSectionRequested { get; init; }
    public required EventHandler ScreenshotRequested { get; init; }
    public required EventHandler ProfileViewRequested { get; init; }
    public required EventHandler PublishPreviewResultRequested { get; init; }
    public required EventHandler<NominalActualPreviewRequestedEventArgs> NominalActualPreviewRequested { get; init; }
    public required EventHandler<NominalActualPublishRequestedEventArgs> NominalActualPublishRequested { get; init; }
    public required PropertyChangedEventHandler ViewModelPropertyChanged { get; init; }
    public required PropertyChangedEventHandler NominalActualPropertyChanged { get; init; }
    public required EventHandler CameraChanged { get; init; }
    public required EventHandler<TeachingRoiDisplayHeightChangedEventArgs> TeachingRoiDisplayHeightChanged { get; init; }
    public required EventHandler LanguageChanged { get; init; }
}

/// <summary>
/// Attaches Viewer callbacks while the control is loaded and detaches them on
/// unload or final disposal. Attach/detach are idempotent so transient WPF
/// reparenting cannot duplicate handlers.
/// </summary>
internal sealed class ViewerEventSubscription : IDisposable
{
    private readonly MainWindowViewModel viewModel;
    private readonly IViewerLocalizationProvider localizationProvider;
    private readonly ViewerEventHandlerSet handlers;
    private bool isAttached;
    private bool isDisposed;

    public ViewerEventSubscription(
        MainWindowViewModel viewModel,
        IViewerLocalizationProvider localizationProvider,
        ViewerEventHandlerSet handlers)
    {
        this.viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        this.localizationProvider = localizationProvider ?? throw new ArgumentNullException(nameof(localizationProvider));
        this.handlers = handlers ?? throw new ArgumentNullException(nameof(handlers));
    }

    public bool IsAttached => isAttached;

    public void Attach()
    {
        ObjectDisposedException.ThrowIf(isDisposed, this);
        if (isAttached)
        {
            return;
        }

        viewModel.FitAllRequested += handlers.FitAllRequested;
        viewModel.FitSelectionRequested += handlers.FitSelectionRequested;
        viewModel.FitRoiRequested += handlers.FitRoiRequested;
        viewModel.TopViewRequested += handlers.TopViewRequested;
        viewModel.PerspectiveViewRequested += handlers.PerspectiveViewRequested;
        viewModel.ResetRequested += handlers.ResetRequested;
        viewModel.OpenRecipeRequested += handlers.OpenRecipeRequested;
        viewModel.SaveRecipeRequested += handlers.SaveRecipeRequested;
        viewModel.ApplyRoiAlignmentRequested += handlers.ApplyRoiAlignmentRequested;
        viewModel.FitPlaneRequested += handlers.FitPlaneRequested;
        viewModel.PreviewThicknessRequested += handlers.PreviewThicknessRequested;
        viewModel.PreviewWarpageRequested += handlers.PreviewWarpageRequested;
        viewModel.PreviewPlaneFlatnessRequested += handlers.PreviewPlaneFlatnessRequested;
        viewModel.PreviewPointPairDimensionsRequested += handlers.PreviewPointPairDimensionsRequested;
        viewModel.PreviewGapFlushRequested += handlers.PreviewGapFlushRequested;
        viewModel.PreviewVolumeRequested += handlers.PreviewVolumeRequested;
        viewModel.PreviewCrossSectionRequested += handlers.PreviewCrossSectionRequested;
        viewModel.ScreenshotRequested += handlers.ScreenshotRequested;
        viewModel.ProfileViewRequested += handlers.ProfileViewRequested;
        viewModel.PublishPreviewResultRequested += handlers.PublishPreviewResultRequested;
        viewModel.NominalActual.PreviewRequested += handlers.NominalActualPreviewRequested;
        viewModel.NominalActual.PublishRequested += handlers.NominalActualPublishRequested;
        viewModel.NominalActual.PropertyChanged += handlers.NominalActualPropertyChanged;
        viewModel.PropertyChanged += handlers.ViewModelPropertyChanged;
        viewModel.CameraChanged += handlers.CameraChanged;
        viewModel.TeachingRoiDisplayHeightChanged += handlers.TeachingRoiDisplayHeightChanged;
        localizationProvider.LanguageChanged += handlers.LanguageChanged;
        isAttached = true;
    }

    public void Detach()
    {
        if (!isAttached)
        {
            return;
        }

        viewModel.FitAllRequested -= handlers.FitAllRequested;
        viewModel.FitSelectionRequested -= handlers.FitSelectionRequested;
        viewModel.FitRoiRequested -= handlers.FitRoiRequested;
        viewModel.TopViewRequested -= handlers.TopViewRequested;
        viewModel.PerspectiveViewRequested -= handlers.PerspectiveViewRequested;
        viewModel.ResetRequested -= handlers.ResetRequested;
        viewModel.OpenRecipeRequested -= handlers.OpenRecipeRequested;
        viewModel.SaveRecipeRequested -= handlers.SaveRecipeRequested;
        viewModel.ApplyRoiAlignmentRequested -= handlers.ApplyRoiAlignmentRequested;
        viewModel.FitPlaneRequested -= handlers.FitPlaneRequested;
        viewModel.PreviewThicknessRequested -= handlers.PreviewThicknessRequested;
        viewModel.PreviewWarpageRequested -= handlers.PreviewWarpageRequested;
        viewModel.PreviewPlaneFlatnessRequested -= handlers.PreviewPlaneFlatnessRequested;
        viewModel.PreviewPointPairDimensionsRequested -= handlers.PreviewPointPairDimensionsRequested;
        viewModel.PreviewGapFlushRequested -= handlers.PreviewGapFlushRequested;
        viewModel.PreviewVolumeRequested -= handlers.PreviewVolumeRequested;
        viewModel.PreviewCrossSectionRequested -= handlers.PreviewCrossSectionRequested;
        viewModel.ScreenshotRequested -= handlers.ScreenshotRequested;
        viewModel.ProfileViewRequested -= handlers.ProfileViewRequested;
        viewModel.PublishPreviewResultRequested -= handlers.PublishPreviewResultRequested;
        viewModel.NominalActual.PreviewRequested -= handlers.NominalActualPreviewRequested;
        viewModel.NominalActual.PublishRequested -= handlers.NominalActualPublishRequested;
        viewModel.NominalActual.PropertyChanged -= handlers.NominalActualPropertyChanged;
        viewModel.PropertyChanged -= handlers.ViewModelPropertyChanged;
        viewModel.CameraChanged -= handlers.CameraChanged;
        viewModel.TeachingRoiDisplayHeightChanged -= handlers.TeachingRoiDisplayHeightChanged;
        localizationProvider.LanguageChanged -= handlers.LanguageChanged;
        isAttached = false;
    }

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        Detach();
        isDisposed = true;
    }
}
