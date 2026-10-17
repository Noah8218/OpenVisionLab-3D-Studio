using System.ComponentModel;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Viewer;
using OpenVisionLab.ThreeD.Viewer.Hosting;
using OpenVisionLab.ThreeD.Viewer.Models;

namespace OpenVisionLab.ThreeD.Shell.Coordination;

/// <summary>
/// Owns the cross-view state links for the Workbench Viewer workspace.
/// Layout and popup presentation stay with <see cref="ViewerWorkspaceView"/>;
/// this type only synchronizes the shared height range, hover cursor, and
/// linked 3D camera between the existing state owners.
/// </summary>
internal sealed class ViewerWorkspaceLinkCoordinator : IDisposable
{
    private ToolWorkbenchViewModel? workbench;
    private OpenVisionThreeDViewerControl? mainViewer;
    private OpenVisionThreeDViewerControl? auxiliaryViewer;
    private OpenVisionThreeDViewerControl? sectionViewer;
    private OpenVisionThreeDViewerControl? heightViewer;
    private readonly HashSet<HeightImageViewerViewModel> heightImages = [];
    private readonly Dictionary<HeightImageViewerViewModel, string> imageSources = [];
    private readonly Dictionary<string, HeightDisplayProfile> storedDisplays = [];
    private readonly HashSet<object> restoredDisplays = [];
    private string recipeContext = string.Empty;
    private bool synchronizingLinkedHeightDisplayRange;
    private bool synchronizingLinkedCamera;
    private int disposalState;

    public void SetWorkbench(ToolWorkbenchViewModel? value)
    {
        if (Volatile.Read(ref disposalState) != 0)
        {
            return;
        }

        if (ReferenceEquals(workbench, value))
        {
            Refresh();
            return;
        }

        if (workbench is not null)
        {
            workbench.PropertyChanged -= OnWorkbenchPropertyChanged;
            workbench.SharedHeightCursor.PropertyChanged -= OnSharedHeightCursorChanged;
            foreach (var pane in Panes()) pane.PropertyChanged -= OnPanePropertyChanged;
        }

        foreach (var image in heightImages) image.PropertyChanged -= OnHeightImageViewerPropertyChanged;
        heightImages.Clear();
        imageSources.Clear();

        restoredDisplays.Clear();
        recipeContext = string.Empty;
        workbench = value;
        if (workbench is not null)
        {
            workbench.PropertyChanged += OnWorkbenchPropertyChanged;
            workbench.SharedHeightCursor.PropertyChanged += OnSharedHeightCursorChanged;
            foreach (var pane in Panes()) pane.PropertyChanged += OnPanePropertyChanged;
        }

        Refresh();
    }

    public void SetMainViewer(OpenVisionThreeDViewerControl? value)
    {
        if (Volatile.Read(ref disposalState) != 0)
        {
            return;
        }

        if (ReferenceEquals(mainViewer, value))
        {
            Refresh();
            return;
        }

        if (mainViewer is not null)
        {
            mainViewer.C3DGridHoverChanged -= OnMainViewerC3DGridHoverChanged;
            mainViewer.CameraChanged -= OnMainViewerCameraChanged;
            mainViewer.HostStateChanged -= OnMainViewerHostStateChanged;
            mainViewer.SetLinkedHeightCursor(null);
        }

        mainViewer = value;
        if (mainViewer is not null)
        {
            mainViewer.C3DGridHoverChanged += OnMainViewerC3DGridHoverChanged;
            mainViewer.CameraChanged += OnMainViewerCameraChanged;
            mainViewer.HostStateChanged += OnMainViewerHostStateChanged;
        }

        Refresh();
    }

    public void SetAuxiliaryViewer(OpenVisionThreeDViewerControl? value)
    {
        if (Volatile.Read(ref disposalState) != 0)
        {
            return;
        }

        if (ReferenceEquals(auxiliaryViewer, value)) return;
        if (auxiliaryViewer is not null) auxiliaryViewer.HostStateChanged -= OnMainViewerHostStateChanged;
        auxiliaryViewer = value;
        if (auxiliaryViewer is not null) auxiliaryViewer.HostStateChanged += OnMainViewerHostStateChanged;
        Refresh();
    }

    public void SetLowerViewers(OpenVisionThreeDViewerControl? section, OpenVisionThreeDViewerControl? height)
    {
        if (Volatile.Read(ref disposalState) != 0) return;
        if (!ReferenceEquals(sectionViewer, section))
        {
            if (sectionViewer is not null) sectionViewer.HostStateChanged -= OnMainViewerHostStateChanged;
            sectionViewer = section;
            if (sectionViewer is not null) sectionViewer.HostStateChanged += OnMainViewerHostStateChanged;
        }
        if (!ReferenceEquals(heightViewer, height))
        {
            if (heightViewer is not null) heightViewer.HostStateChanged -= OnMainViewerHostStateChanged;
            heightViewer = height;
            if (heightViewer is not null) heightViewer.HostStateChanged += OnMainViewerHostStateChanged;
        }
        Refresh();
    }

    public void Refresh()
    {
        if (Volatile.Read(ref disposalState) != 0)
        {
            return;
        }

        RefreshRecipeDisplayContext();
        RestorePendingDisplayProfiles();
        RefreshHeightDisplayContext();
        ApplySharedHeightCursorToMainViewer();
        foreach (var viewer in Viewers()) SynchronizeLinkedHeightDisplayRangeFromViewer(viewer);
        SynchronizeLinkedCamera();
    }

    public IReadOnlyDictionary<string, HeightDisplayProfile> CaptureDisplayProfiles()
    {
        RefreshRecipeDisplayContext();
        var profiles = new Dictionary<string, HeightDisplayProfile>(storedDisplays);
        var viewers = new[] { mainViewer, auxiliaryViewer, sectionViewer, heightViewer };
        var panes = Panes();
        var slots = new[] { "main", "auxiliary", "section", "height" };
        for (var index = 0; index < panes.Length; index++)
        {
            var pane = panes[index];
            foreach (var (kind, image) in new[] { ("input", pane.InputDisplay), ("output", pane.OutputDisplay) })
                profiles[slots[index] + "/" + kind] = new(image.HeightDisplayOptions.Sanitize(forPersistence: true), image.CaptureHeightDisplayModes(), recipeContext);
            if (viewers[index] is { } viewer)
                profiles[slots[index] + "/3d"] = new(viewer.HostState.Presentation.HeightDisplayOptions.Sanitize(forPersistence: true), viewer.CaptureHeightDisplayModes(), recipeContext) { Textures = viewer.CaptureC3DTextureSettings() };
        }
        return profiles;
    }

    public void RestoreDisplayProfiles(IReadOnlyDictionary<string, HeightDisplayProfile> profiles)
    {
        storedDisplays.Clear();
        foreach (var entry in profiles) storedDisplays[entry.Key] = entry.Value;
        restoredDisplays.Clear();
        Refresh();
    }

    private void RefreshRecipeDisplayContext()
    {
        if (workbench is not { } current) return;
        var identity = current.RecipePath is { } path ? Path.GetFullPath(path).ToUpperInvariant() : "unsaved:" + current.RecipeName;
        var next = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
        if (recipeContext == next) return;
        if (recipeContext.Length > 0)
        {
            synchronizingLinkedHeightDisplayRange = true;
            try
            {
                foreach (var image in Panes().SelectMany(pane => new[] { pane.InputDisplay, pane.OutputDisplay }).Append(current.HeightImageViewer).Distinct())
                    image.RestoreHeightDisplayModes(new Dictionary<string, string>());
                foreach (var viewer in Viewers())
                {
                    viewer.RestoreHeightDisplayModes(new Dictionary<string, string>());
                    viewer.RestoreC3DTextureSettings(null);
                }
            }
            finally { synchronizingLinkedHeightDisplayRange = false; }
        }
        recipeContext = next;
    }

    private bool HasPendingDisplayProfile(OpenVisionThreeDViewerControl viewer)
    {
        var slot = ReferenceEquals(viewer, mainViewer) ? "main" : ReferenceEquals(viewer, auxiliaryViewer) ? "auxiliary"
            : ReferenceEquals(viewer, sectionViewer) ? "section" : ReferenceEquals(viewer, heightViewer) ? "height" : null;
        return slot is not null && storedDisplays.ContainsKey(slot + "/3d") && !restoredDisplays.Contains(viewer);
    }

    private void RestorePendingDisplayProfiles()
    {
        var viewers = new[] { mainViewer, auxiliaryViewer, sectionViewer, heightViewer };
        var panes = Panes();
        var slots = new[] { "main", "auxiliary", "section", "height" };
        synchronizingLinkedHeightDisplayRange = true;
        try
        {
            for (var index = 0; index < panes.Length; index++)
            {
                foreach (var (kind, image) in new[] { ("input", panes[index].InputDisplay), ("output", panes[index].OutputDisplay) })
                    if (storedDisplays.TryGetValue(slots[index] + "/" + kind, out var profile) && restoredDisplays.Add(image))
                    {
                        image.TryApplyHeightDisplayOptions(profile.Options);
                        image.RestoreHeightDisplayModes(profile.RecipeContext == recipeContext ? profile.MapModes : new Dictionary<string, string>());
                    }
                var paneSourcePath = panes[index].HeightImage.Frame is { IsSignedDelta: true } signedFrame ? signedFrame.SourcePath : panes[index].SourcePath;
                if (viewers[index] is { } viewer && string.Equals(viewer.CurrentC3DSourcePath, paneSourcePath, StringComparison.OrdinalIgnoreCase)
                    && double.IsFinite(viewer.HostState.Presentation.C3DHeightColorMinimumRaw) && double.IsFinite(viewer.HostState.Presentation.C3DHeightColorMaximumRaw)
                    && storedDisplays.TryGetValue(slots[index] + "/3d", out var threeD) && restoredDisplays.Add(viewer))
                {
                    viewer.TryApplyHeightDisplayOptions(threeD.Options);
                    viewer.RestoreHeightDisplayModes(threeD.RecipeContext == recipeContext ? threeD.MapModes : new Dictionary<string, string>());
                    viewer.RestoreC3DTextureSettings(threeD.RecipeContext == recipeContext ? threeD.Textures : null);
                }
            }
        }
        finally { synchronizingLinkedHeightDisplayRange = false; }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposalState, 1) != 0)
        {
            return;
        }

        if (workbench is not null)
        {
            workbench.PropertyChanged -= OnWorkbenchPropertyChanged;
            workbench.SharedHeightCursor.PropertyChanged -= OnSharedHeightCursorChanged;
            foreach (var pane in Panes()) pane.PropertyChanged -= OnPanePropertyChanged;
        }

        foreach (var image in heightImages) image.PropertyChanged -= OnHeightImageViewerPropertyChanged;
        heightImages.Clear();
        imageSources.Clear();
        foreach (var viewer in Viewers()) viewer.HostStateChanged -= OnMainViewerHostStateChanged;

        if (mainViewer is not null)
        {
            mainViewer.C3DGridHoverChanged -= OnMainViewerC3DGridHoverChanged;
            mainViewer.CameraChanged -= OnMainViewerCameraChanged;
            mainViewer.HostStateChanged -= OnMainViewerHostStateChanged;
            mainViewer.SetLinkedHeightCursor(null);
        }

        workbench = null;
        mainViewer = null;
        auxiliaryViewer = null;
        sectionViewer = null;
        heightViewer = null;
    }

    private void OnMainViewerHostStateChanged(
        object? sender,
        ViewerHostStateChangedEventArgs args)
    {
        if (sender is OpenVisionThreeDViewerControl pending && HasPendingDisplayProfile(pending)) return;
        if (args.PropertyName is nameof(ViewerHostState.Presentation)
            or nameof(ViewerHostState.C3DSampleVisible))
        {
            RefreshHeightDisplayContext();
            if (sender is OpenVisionThreeDViewerControl viewer) SynchronizeLinkedHeightDisplayRangeFromViewer(viewer);
        }
    }

    private void OnWorkbenchPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(ToolWorkbenchViewModel.IsInspectionFileView) or nameof(ToolWorkbenchViewModel.IsValidationSampleView)
            or nameof(ToolWorkbenchViewModel.RecipePath) or nameof(ToolWorkbenchViewModel.RecipeName)
            or nameof(ToolWorkbenchViewModel.CurrentMeasurementOutput) or nameof(ToolWorkbenchViewModel.HasCurrentMeasurementPreview)
            or nameof(ToolWorkbenchViewModel.SelectedPipelineStep) or nameof(ToolWorkbenchViewModel.SelectedValidationSetSample)
            or nameof(ToolWorkbenchViewModel.SelectedValidationSetStep)) Refresh();
    }

    private ViewerPaneViewModel[] Panes() => workbench is { } current
        ? [current.MainViewerPane, current.AuxiliaryViewerPane, current.SectionViewerPane, current.HeightViewerPane] : [];

    private IEnumerable<OpenVisionThreeDViewerControl> Viewers() =>
        new[] { mainViewer, auxiliaryViewer, sectionViewer, heightViewer }.OfType<OpenVisionThreeDViewerControl>().Distinct();

    private void OnPanePropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(ViewerPaneViewModel.HeightImage) or nameof(ViewerPaneViewModel.Kind)) Refresh();
    }

    private void RefreshHeightDisplayContext()
    {
        if (workbench is not { } current || synchronizingLinkedHeightDisplayRange) return;
        var workspace = current.IsInspectionFileView ? "inspect" : current.IsValidationSampleView ? "validate" : "teach";
        synchronizingLinkedHeightDisplayRange = true;
        try
        {
            var panes = Panes();
            var nextImages = panes.Select(pane => pane.HeightImage).Append(current.HeightImageViewer).ToHashSet();
            foreach (var image in heightImages.Except(nextImages).ToArray())
            {
                image.PropertyChanged -= OnHeightImageViewerPropertyChanged;
                heightImages.Remove(image);
                imageSources.Remove(image);
            }
            foreach (var image in nextImages)
                if (heightImages.Add(image))
                {
                    image.PropertyChanged += OnHeightImageViewerPropertyChanged;
                    if (image.Frame is { } loaded) imageSources[image] = image.HeightDisplayWorkspace + "|" + image.HeightDisplayMapId + "|" + loaded.SourceContentSha256;
                }
            current.HeightImageViewer.SetHeightDisplayContext(workspace,
                current.HeightImageViewer.Frame?.IsSignedDelta == true ? current.HeightImageViewer.Frame.SourceEntityId : "source",
                current.HeightImageViewer.Frame is { IsSignedDelta: true } ? current.Localization.Resolve("ThreeD.Viewer.SignedDeltaMap", "부호 있는 높이 차이", "Signed height delta")
                    : current.HeightImageViewer.Frame is { } frame ? Path.GetFileName(frame.SourcePath) : string.Empty);
            var viewers = new[] { mainViewer, auxiliaryViewer, sectionViewer, heightViewer };
            var measurement = current.GetDisplayedMeasurementColor();
            current.HeightImageViewer.SetHeightMeasurementColor(measurement);
            for (var index = 0; index < panes.Length; index++)
            {
                var pane = panes[index];
                var mapId = pane.HeightImage.Frame is { IsSignedDelta: true } signed ? signed.SourceEntityId
                    : pane.IsInputSource ? "source" : pane.Source?.Id ?? "source";
                if (!ReferenceEquals(pane.HeightImage, current.HeightImageViewer)) pane.HeightImage.SetHeightDisplayContext(workspace, mapId, pane.SourceName);
                viewers[index]?.SetHeightDisplayContext(workspace, mapId, pane.SourceName);
                var paneMeasurement = measurement is not null && (pane.Source?.Id == measurement.InputEntityId
                    || pane.IsInputSource && measurement.InputEntityId == current.Source.Id) ? measurement : null;
                pane.HeightImage.SetHeightMeasurementColor(paneMeasurement);
                viewers[index]?.SetHeightMeasurementColor(paneMeasurement);
            }
        }
        finally { synchronizingLinkedHeightDisplayRange = false; }
    }

    private void OnHeightImageViewerPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (synchronizingLinkedHeightDisplayRange || sender is not HeightImageViewerViewModel image || image.Frame is not { } frame) return;
        if (args.PropertyName is not (nameof(HeightImageViewerViewModel.Frame) or nameof(HeightImageViewerViewModel.DisplayRangeRevision))) return;
        if (args.PropertyName == nameof(HeightImageViewerViewModel.Frame)) RefreshHeightDisplayContext();
        var key = image.HeightDisplayWorkspace + "|" + image.HeightDisplayMapId + "|" + frame.SourceContentSha256;
        if (args.PropertyName == nameof(HeightImageViewerViewModel.Frame) || !imageSources.TryGetValue(image, out var previous) || previous != key)
        {
            imageSources[image] = key;
            var viewer = Viewers().FirstOrDefault(candidate => HasMatchingHeightSource(candidate.HostState.Presentation, image));
            if (viewer is not null) SynchronizeLinkedHeightDisplayRangeFromViewer(viewer);
            else if (heightImages.FirstOrDefault(candidate => !ReferenceEquals(candidate, image) && HasMatchingHeightSource(candidate, image)) is { } existing)
                SynchronizeLinkedHeightDisplayRangeFromImage(existing);
            return;
        }
        SynchronizeLinkedHeightDisplayRangeFromImage(image);
    }

    private void OnMainViewerCameraChanged(object? sender, EventArgs args) =>
        SynchronizeLinkedCamera();

    private void SynchronizeLinkedHeightDisplayRangeFromViewer(OpenVisionThreeDViewerControl viewerHost)
    {
        if (synchronizingLinkedHeightDisplayRange || HasPendingDisplayProfile(viewerHost)) return;
        var presentation = viewerHost.HostState.Presentation;
        synchronizingLinkedHeightDisplayRange = true;
        try
        {
            foreach (var heightImage in heightImages.Where(image => HasMatchingHeightSource(presentation, image)))
            {
                heightImage.SetHeightDisplayLinked(presentation.HeightDisplayOptions.IsLinked);
                if (presentation.HeightDisplayOptions.IsLinked)
                    heightImage.TryApplyLinkedDisplayState(presentation.HeightDisplayOptions, presentation.C3DHeightColorMinimumRaw, presentation.C3DHeightColorMaximumRaw);
                imageSources[heightImage] = heightImage.HeightDisplayWorkspace + "|" + heightImage.HeightDisplayMapId + "|" + heightImage.Frame!.SourceContentSha256;
            }
            foreach (var target in Viewers().Where(target => !ReferenceEquals(target, viewerHost) && !HasPendingDisplayProfile(target)))
            {
                var state = target.HostState.Presentation;
                if (state.HeightDisplayWorkspace != presentation.HeightDisplayWorkspace || state.HeightDisplayMapId != presentation.HeightDisplayMapId
                    || !string.Equals(state.C3DHeightDistributionSourceSha256, presentation.C3DHeightDistributionSourceSha256, StringComparison.OrdinalIgnoreCase)) continue;
                target.SetHeightDisplayLinked(presentation.HeightDisplayOptions.IsLinked);
                if (presentation.HeightDisplayOptions.IsLinked)
                    target.TryApplyLinkedHeightDisplayState(presentation.HeightDisplayOptions, presentation.C3DHeightColorMinimumRaw, presentation.C3DHeightColorMaximumRaw);
            }
        }
        finally
        {
            synchronizingLinkedHeightDisplayRange = false;
        }
    }

    private void SynchronizeLinkedHeightDisplayRangeFromImage(HeightImageViewerViewModel heightImage)
    {
        if (synchronizingLinkedHeightDisplayRange
            || heightImage.DisplayFrame is not { } displayFrame)
        {
            return;
        }

        synchronizingLinkedHeightDisplayRange = true;
        try
        {
            foreach (var viewerHost in Viewers().Where(viewer => !HasPendingDisplayProfile(viewer) && HasMatchingHeightSource(viewer.HostState.Presentation, heightImage)))
            {
                viewerHost.SetHeightDisplayLinked(heightImage.HeightDisplayOptions.IsLinked);
                if (heightImage.HeightDisplayOptions.IsLinked)
                    viewerHost.TryApplyLinkedHeightDisplayState(heightImage.HeightDisplayOptions, displayFrame.Minimum, displayFrame.Maximum);
            }
            foreach (var target in heightImages.Where(image => !ReferenceEquals(image, heightImage) && HasMatchingHeightSource(image, heightImage)))
            {
                target.SetHeightDisplayLinked(heightImage.HeightDisplayOptions.IsLinked);
                if (heightImage.HeightDisplayOptions.IsLinked)
                    target.TryApplyLinkedDisplayState(heightImage.HeightDisplayOptions, displayFrame.Minimum, displayFrame.Maximum);
            }
        }
        finally
        {
            synchronizingLinkedHeightDisplayRange = false;
        }
    }

    private static bool HasMatchingHeightSource(
        ViewerHostPresentationState presentation,
        HeightImageViewerViewModel heightImage) =>
        heightImage.Frame is { } frame
        && presentation.HeightDisplayWorkspace == heightImage.HeightDisplayWorkspace
        && presentation.HeightDisplayMapId == heightImage.HeightDisplayMapId
        && string.Equals(
            presentation.C3DHeightDistributionSourceSha256,
            frame.SourceContentSha256,
            StringComparison.OrdinalIgnoreCase);

    private static bool HasMatchingHeightSource(HeightImageViewerViewModel left, HeightImageViewerViewModel right) =>
        left.Frame is { } first && right.Frame is { } second && left.HeightDisplayMapId == right.HeightDisplayMapId
        && left.HeightDisplayWorkspace == right.HeightDisplayWorkspace
        && string.Equals(first.SourceContentSha256, second.SourceContentSha256, StringComparison.OrdinalIgnoreCase);

    private void OnMainViewerC3DGridHoverChanged(
        object? sender,
        C3DGridHoverChangedEventArgs args)
    {
        if (workbench is null)
        {
            return;
        }

        if (args.Cursor is not { } cursor)
        {
            workbench.SharedHeightCursor.Clear(
                SharedHeightCursorOrigin.ThreeDViewer);
            return;
        }

        workbench.SharedHeightCursor.Update(
            SharedHeightCursorOrigin.ThreeDViewer,
            cursor.SourceContentSha256,
            cursor.Row,
            cursor.Column,
            cursor.RawHeight,
            cursor.IsValid);
    }

    private void OnSharedHeightCursorChanged(
        object? sender,
        PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(SharedHeightCursorSession.Cursor)
            or nameof(SharedHeightCursorSession.HasCursor)
            or nameof(SharedHeightCursorSession.Revision))
        {
            ApplySharedHeightCursorToMainViewer();
        }
    }

    private void ApplySharedHeightCursorToMainViewer()
    {
        if (mainViewer is null)
        {
            return;
        }

        mainViewer.SetLinkedHeightCursor(
            workbench?.SharedHeightCursor.Cursor is { } cursor
                ? new C3DGridCursor(
                    cursor.Origin == SharedHeightCursorOrigin.ThreeDViewer
                        ? C3DGridCursorOrigin.ThreeDViewer
                        : C3DGridCursorOrigin.HeightImage,
                    cursor.SourceContentSha256,
                    cursor.Row,
                    cursor.Column,
                    cursor.RawHeight,
                    cursor.IsValid)
                : null);
    }

    private void SynchronizeLinkedCamera()
    {
        var currentWorkbench = workbench;
        if (currentWorkbench?.ViewerWorkspace.IsCameraLinked != true)
        {
            return;
        }

        var candidate = currentWorkbench.GetViewerWorkspaceCandidate(
            currentWorkbench.ViewerWorkspace.AuxiliaryContentId);
        var currentMainViewer = mainViewer;
        var currentAuxiliaryViewer = auxiliaryViewer;
        if (candidate is null
            || candidate.Kind != ViewerWorkspaceCandidateKind.ThreeDArtifact
            || currentMainViewer is null
            || currentAuxiliaryViewer is null
            || !File.Exists(candidate.SourcePath))
        {
            currentWorkbench.ViewerWorkspace.SetCameraLinked(false);
            return;
        }

        if (synchronizingLinkedCamera)
        {
            return;
        }

        synchronizingLinkedCamera = true;
        try
        {
            if (!currentAuxiliaryViewer.TryApplyCameraState(currentMainViewer.CaptureCameraState()))
            {
                currentWorkbench.ViewerWorkspace.SetCameraLinked(false);
            }
        }
        finally
        {
            synchronizingLinkedCamera = false;
        }
    }
}
