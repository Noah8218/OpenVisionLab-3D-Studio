using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Owns the retained Surface Match collection and its presentation-only
/// selection policy. Selection publishes existing evidence without execution.
/// </summary>
internal sealed class ToolWorkbenchSurfaceMatchCollectionOwner : INotifyPropertyChanged
{
    private readonly Func<bool> isSurfaceMatchExperimentVisible;
    private readonly Func<bool> isSurfaceMatchExperimentRunning;
    private readonly Func<bool> hasSurfaceMatchExperimentCandidate;
    private readonly Action<SurfaceMatchExperimentEvidence> loadPublishedEvidence;
    private readonly Action<SurfaceMatchExperimentEvidence> requestDisplay;
    private readonly Action<string, string> appendLog;
    private readonly RelayCommand previousSurfaceMatchCollectionItemCommand;
    private readonly RelayCommand nextSurfaceMatchCollectionItemCommand;

    private SurfaceMatchCollectionArtifact? surfaceMatchCollection;
    private SurfaceModelArtifact? surfaceMatchCollectionModel;
    private PreparedSceneArtifact? surfaceMatchCollectionScene;
    private SurfaceMatchCollectionSelectionItem[] surfaceMatchCollectionItems = [];
    private SurfaceMatchCollectionSelectionItem? selectedSurfaceMatchCollectionItem;

    public ToolWorkbenchSurfaceMatchCollectionOwner(
        Func<bool> isSurfaceMatchExperimentVisible,
        Func<bool> isSurfaceMatchExperimentRunning,
        Func<bool> hasSurfaceMatchExperimentCandidate,
        Action<SurfaceMatchExperimentEvidence> loadPublishedEvidence,
        Action<SurfaceMatchExperimentEvidence> requestDisplay,
        Action<string, string> appendLog)
    {
        this.isSurfaceMatchExperimentVisible = isSurfaceMatchExperimentVisible
            ?? throw new ArgumentNullException(nameof(isSurfaceMatchExperimentVisible));
        this.isSurfaceMatchExperimentRunning = isSurfaceMatchExperimentRunning
            ?? throw new ArgumentNullException(nameof(isSurfaceMatchExperimentRunning));
        this.hasSurfaceMatchExperimentCandidate = hasSurfaceMatchExperimentCandidate
            ?? throw new ArgumentNullException(nameof(hasSurfaceMatchExperimentCandidate));
        this.loadPublishedEvidence = loadPublishedEvidence
            ?? throw new ArgumentNullException(nameof(loadPublishedEvidence));
        this.requestDisplay = requestDisplay
            ?? throw new ArgumentNullException(nameof(requestDisplay));
        this.appendLog = appendLog
            ?? throw new ArgumentNullException(nameof(appendLog));

        previousSurfaceMatchCollectionItemCommand = new RelayCommand(
            _ => Navigate(-1),
            _ => CanNavigatePreviousSurfaceMatchCollectionItem);
        nextSurfaceMatchCollectionItemCommand = new RelayCommand(
            _ => Navigate(1),
            _ => CanNavigateNextSurfaceMatchCollectionItem);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public SurfaceMatchCollectionArtifact? SurfaceMatchCollection =>
        surfaceMatchCollection;

    public IReadOnlyList<SurfaceMatchCollectionSelectionItem>
        SurfaceMatchCollectionItems => surfaceMatchCollectionItems;

    public bool IsSurfaceMatchCollectionVisible =>
        isSurfaceMatchExperimentVisible()
        && surfaceMatchCollectionItems.Length > 1;

    public bool CanSelectSurfaceMatchCollectionItem =>
        surfaceMatchCollectionItems.Length > 1
        && !isSurfaceMatchExperimentRunning()
        && !hasSurfaceMatchExperimentCandidate();

    public bool CanNavigatePreviousSurfaceMatchCollectionItem =>
        CanSelectSurfaceMatchCollectionItem
        && Array.IndexOf(
            surfaceMatchCollectionItems,
            selectedSurfaceMatchCollectionItem) > 0;

    public bool CanNavigateNextSurfaceMatchCollectionItem
    {
        get
        {
            var index = Array.IndexOf(
                surfaceMatchCollectionItems,
                selectedSurfaceMatchCollectionItem);
            return CanSelectSurfaceMatchCollectionItem
                   && index >= 0
                   && index < surfaceMatchCollectionItems.Length - 1;
        }
    }

    public ICommand PreviousSurfaceMatchCollectionItemCommand =>
        previousSurfaceMatchCollectionItemCommand;

    public ICommand NextSurfaceMatchCollectionItemCommand =>
        nextSurfaceMatchCollectionItemCommand;

    public SurfaceMatchCollectionSelectionItem? SelectedSurfaceMatchCollectionItem
    {
        get => selectedSurfaceMatchCollectionItem;
        set
        {
            if (value is null
                || ReferenceEquals(value, selectedSurfaceMatchCollectionItem)
                || !CanSelectSurfaceMatchCollectionItem
                || !surfaceMatchCollectionItems.Contains(value))
            {
                return;
            }

            selectedSurfaceMatchCollectionItem = value;
            ApplySelection(value);
        }
    }

    public string SurfaceMatchCollectionSummary =>
        surfaceMatchCollection is null
            ? string.Empty
            : string.Create(
                CultureInfo.InvariantCulture,
                $"{surfaceMatchCollection.Items.Length} retained matches | selected {(selectedSurfaceMatchCollectionItem?.Item.Order ?? 0) + 1} | collection {ShortHash(surfaceMatchCollection.ContentSha256)}");

    public void Show(
        SurfaceModelArtifact model,
        PreparedSceneArtifact scene,
        SurfaceMatchCollectionArtifact collection)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(collection);
        var validity = SurfaceMatchCollectionArtifactValidator.Inspect(collection);
        if (!validity.IsValid
            || collection.Items.Length == 0
            || collection.ModelContentSha256 != model.ContentSha256
            || collection.SceneContentSha256 != scene.ContentSha256)
        {
            throw new InvalidDataException(
                "Workbench multiple-match evidence is invalid, empty, or linked to different model/scene inputs.");
        }

        Clear();
        surfaceMatchCollection = collection;
        surfaceMatchCollectionModel = model;
        surfaceMatchCollectionScene = scene;
        surfaceMatchCollectionItems = collection.Items
            .Select(item => new SurfaceMatchCollectionSelectionItem(
                item.MatchId,
                FormatItem(item),
                item))
            .ToArray();
        selectedSurfaceMatchCollectionItem = surfaceMatchCollectionItems[0];
        ApplySelection(selectedSurfaceMatchCollectionItem);
    }

    public bool Select(string matchId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(matchId);
        var item = surfaceMatchCollectionItems.FirstOrDefault(candidate =>
            candidate.MatchId == matchId);
        if (item is null || !CanSelectSurfaceMatchCollectionItem)
        {
            return false;
        }

        SelectedSurfaceMatchCollectionItem = item;
        return ReferenceEquals(selectedSurfaceMatchCollectionItem, item);
    }

    public void Clear()
    {
        surfaceMatchCollection = null;
        surfaceMatchCollectionModel = null;
        surfaceMatchCollectionScene = null;
        surfaceMatchCollectionItems = [];
        selectedSurfaceMatchCollectionItem = null;
        RefreshState();
    }

    public void RefreshState()
    {
        OnPropertyChanged(nameof(SurfaceMatchCollection));
        OnPropertyChanged(nameof(SurfaceMatchCollectionItems));
        OnPropertyChanged(nameof(IsSurfaceMatchCollectionVisible));
        OnPropertyChanged(nameof(CanSelectSurfaceMatchCollectionItem));
        OnPropertyChanged(nameof(CanNavigatePreviousSurfaceMatchCollectionItem));
        OnPropertyChanged(nameof(CanNavigateNextSurfaceMatchCollectionItem));
        OnPropertyChanged(nameof(SelectedSurfaceMatchCollectionItem));
        OnPropertyChanged(nameof(SurfaceMatchCollectionSummary));
        previousSurfaceMatchCollectionItemCommand.RaiseCanExecuteChanged();
        nextSurfaceMatchCollectionItemCommand.RaiseCanExecuteChanged();
    }

    private void Navigate(int direction)
    {
        var currentIndex = Array.IndexOf(
            surfaceMatchCollectionItems,
            selectedSurfaceMatchCollectionItem);
        var nextIndex = currentIndex + direction;
        if (currentIndex < 0
            || nextIndex < 0
            || nextIndex >= surfaceMatchCollectionItems.Length)
        {
            return;
        }

        SelectedSurfaceMatchCollectionItem = surfaceMatchCollectionItems[nextIndex];
    }

    private void ApplySelection(SurfaceMatchCollectionSelectionItem selected)
    {
        var model = surfaceMatchCollectionModel
                    ?? throw new InvalidOperationException(
                        "Multiple-match model evidence is unavailable.");
        var scene = surfaceMatchCollectionScene
                    ?? throw new InvalidOperationException(
                        "Multiple-match scene evidence is unavailable.");
        var evidence = new SurfaceMatchExperimentEvidence(
            model,
            scene,
            selected.Item.Execution,
            selected.Item.Assessment,
            null,
            null,
            null,
            null,
            null);
        loadPublishedEvidence(evidence);
        requestDisplay(evidence);
        RefreshState();
        appendLog(
            "Viewer",
            $"Selected retained Surface Match result without execution: matchId={selected.MatchId};collectionSha256={surfaceMatchCollection!.ContentSha256}.");
    }

    private static string FormatItem(SurfaceMatchCollectionItem item) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"#{item.Order + 1} | {MatchIdSuffix(item.MatchId)} | {item.Assessment.Decision.ToString().ToUpperInvariant()}");

    private static string MatchIdSuffix(string matchId) =>
        matchId.Length <= 4 ? matchId : matchId[^4..];

    private static string ShortHash(string hash) =>
        hash.Length <= 12 ? hash : hash[..12];

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
