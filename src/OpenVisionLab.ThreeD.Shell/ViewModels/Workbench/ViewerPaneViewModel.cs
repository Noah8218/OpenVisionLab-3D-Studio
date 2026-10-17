using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Owns one pane's display kind and optional height-image lifetime. Source,
/// recipe, ROI editing and execution remain with their existing owners.
/// </summary>
public sealed class ViewerPaneViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly HeightImageViewerViewModel inputHeightImage;
    private readonly HeightImageViewerViewModel outputHeightImage;
    private readonly HeightImageViewerViewModel paneInputHeightImage;
    private readonly Func<ViewerWorkspaceCandidateItem, bool> isInput;
    private readonly Func<HeightImageViewerViewModel, ViewerWorkspaceCandidateItem, Task> loadHeightImage;
    private readonly RelayCommand setKindCommand;
    private ViewerWorkspaceCandidateItem? source;
    private ViewerPaneKind? explicitKind;
    private ViewerPaneKind defaultKind;
    private bool isActive;
    private bool useLegacyInputControls;
    private bool disposed;

    internal ViewerPaneViewModel(ThreeDLocalization localization, SharedHeightCursorSession sharedCursor,
        HeightImageViewerViewModel inputHeightImage, ViewerPaneKind defaultKind,
        Func<ViewerWorkspaceCandidateItem, bool> isInput,
        Func<HeightImageViewerViewModel, ViewerWorkspaceCandidateItem, Task> loadHeightImage)
    {
        this.inputHeightImage = inputHeightImage;
        paneInputHeightImage = new HeightImageViewerViewModel(localization, sharedCursor, inputHeightImage.RoiWorkspace);
        outputHeightImage = new HeightImageViewerViewModel(localization, sharedCursor);
        this.defaultKind = defaultKind;
        this.isInput = isInput;
        this.loadHeightImage = loadHeightImage;
        setKindCommand = new RelayCommand(value =>
        {
            if (value is not ViewerPaneKind kind || !Enum.IsDefined(kind)) return;
            explicitKind = kind;
            NotifyKind();
            EnsureHeightImage();
        }, value => !disposed && value is ViewerPaneKind kind && Enum.IsDefined(kind));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ViewerPaneKind Kind => explicitKind ?? defaultKind;
    public bool IsThreeD => Kind == ViewerPaneKind.ThreeD;
    public bool IsHeight => Kind == ViewerPaneKind.Height;
    public bool IsSection => Kind == ViewerPaneKind.Section;
    public bool HasSource => source is not null;
    public bool IsInputSource => source is not null && isInput(source) && source.Contract != "SignedDeltaHeightField";
    public bool IsOutputSource => source is not null && (!isInput(source) || source.Contract == "SignedDeltaHeightField");
    public string SourceName => source?.ViewerName ?? "—";
    public string SourcePath => source?.SourcePath ?? string.Empty;
    public HeightImageViewerViewModel HeightImage => source is not null && isInput(source)
        ? useLegacyInputControls ? inputHeightImage : paneInputHeightImage : outputHeightImage;
    public ICommand SetKindCommand => setKindCommand;
    internal ViewerWorkspaceCandidateItem? Source => source;
    internal HeightImageViewerViewModel InputDisplay => useLegacyInputControls ? inputHeightImage : paneInputHeightImage;
    internal HeightImageViewerViewModel OutputDisplay => outputHeightImage;

    internal void UpdateSource(ViewerWorkspaceCandidateItem? next, ViewerPaneKind nextDefault, bool active, bool preserveLegacyInputControls = false)
    {
        if (disposed) return;
        var previousKind = Kind;
        var changed = source?.Id != next?.Id || source?.SourcePath != next?.SourcePath;
        if (changed)
        {
            outputHeightImage.ClearSource();
            paneInputHeightImage.ClearSource();
        }
        source = next;
        defaultKind = nextDefault;
        isActive = active;
        useLegacyInputControls = preserveLegacyInputControls;
        OnPropertyChanged(nameof(SourceName));
        OnPropertyChanged(nameof(SourcePath));
        OnPropertyChanged(nameof(HasSource));
        OnPropertyChanged(nameof(IsInputSource));
        OnPropertyChanged(nameof(IsOutputSource));
        OnPropertyChanged(nameof(HeightImage));
        if (previousKind != Kind) NotifyKind();
        EnsureHeightImage();
    }

    internal void RestoreDefaultKind()
    {
        explicitKind = null;
        NotifyKind();
        EnsureHeightImage();
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        outputHeightImage.Dispose();
        paneInputHeightImage.Dispose();
        setKindCommand.RaiseCanExecuteChanged();
    }

    private void EnsureHeightImage()
    {
        if (!isActive || !IsHeight || source is not { } current) return;
        var image = HeightImage;
        image.StartObservedLoad(() => loadHeightImage(image, current), exception => image.ReportSourceFailure(exception.Message));
    }

    private void NotifyKind()
    {
        OnPropertyChanged(nameof(Kind));
        OnPropertyChanged(nameof(IsThreeD));
        OnPropertyChanged(nameof(IsHeight));
        OnPropertyChanged(nameof(IsSection));
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public enum ViewerPaneKind
{
    ThreeD,
    Height,
    Section
}
