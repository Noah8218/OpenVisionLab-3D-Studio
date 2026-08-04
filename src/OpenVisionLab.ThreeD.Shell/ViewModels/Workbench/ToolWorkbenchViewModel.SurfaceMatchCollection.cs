using System.Globalization;
using System.IO;
using System.Windows.Input;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Viewer;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Presentation-only selection over one retained multiple-match collection.
/// Selection never executes matching or changes collection identity.
/// </summary>
public sealed partial class ToolWorkbenchViewModel
{
    private SurfaceMatchCollectionArtifact? surfaceMatchCollection;
    private SurfaceModelArtifact? surfaceMatchCollectionModel;
    private PreparedSceneArtifact? surfaceMatchCollectionScene;
    private SurfaceMatchCollectionSelectionItem[] surfaceMatchCollectionItems = [];
    private SurfaceMatchCollectionSelectionItem? selectedSurfaceMatchCollectionItem;
    private RelayCommand previousSurfaceMatchCollectionItemCommand = null!;
    private RelayCommand nextSurfaceMatchCollectionItemCommand = null!;

    public SurfaceMatchCollectionArtifact? SurfaceMatchCollection =>
        surfaceMatchCollection;

    public IReadOnlyList<SurfaceMatchCollectionSelectionItem>
        SurfaceMatchCollectionItems => surfaceMatchCollectionItems;

    public bool IsSurfaceMatchCollectionVisible =>
        IsSurfaceMatchExperimentVisible
        && surfaceMatchCollectionItems.Length > 1;

    public bool CanSelectSurfaceMatchCollectionItem =>
        surfaceMatchCollectionItems.Length > 1
        && !IsSurfaceMatchExperimentRunning
        && !HasSurfaceMatchExperimentCandidate;

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

    public SurfaceMatchCollectionSelectionItem?
        SelectedSurfaceMatchCollectionItem
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
            ApplySurfaceMatchCollectionSelection(value);
        }
    }

    public string SurfaceMatchCollectionSummary =>
        surfaceMatchCollection is null
            ? string.Empty
            : string.Create(
                CultureInfo.InvariantCulture,
                $"{surfaceMatchCollection.Items.Length} retained matches | selected {(selectedSurfaceMatchCollectionItem?.Item.Order ?? 0) + 1} | collection {ShortHash(surfaceMatchCollection.ContentSha256)}");

    private void InitializeSurfaceMatchCollectionNavigation()
    {
        previousSurfaceMatchCollectionItemCommand = new RelayCommand(
            _ => NavigateSurfaceMatchCollectionItem(-1),
            _ => CanNavigatePreviousSurfaceMatchCollectionItem);
        nextSurfaceMatchCollectionItemCommand = new RelayCommand(
            _ => NavigateSurfaceMatchCollectionItem(1),
            _ => CanNavigateNextSurfaceMatchCollectionItem);
    }

    public void ShowSurfaceMatchCollectionEvidence(
        SurfaceModelArtifact model,
        PreparedSceneArtifact scene,
        SurfaceMatchCollectionArtifact collection)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(collection);
        var validity = SurfaceMatchCollectionArtifactValidator.Inspect(
            collection);
        if (!validity.IsValid
            || collection.Items.Length == 0
            || collection.ModelContentSha256 != model.ContentSha256
            || collection.SceneContentSha256 != scene.ContentSha256)
        {
            throw new InvalidDataException(
                "Workbench multiple-match evidence is invalid, empty, or linked to different model/scene inputs.");
        }

        ClearSurfaceMatchCollection();
        surfaceMatchCollection = collection;
        surfaceMatchCollectionModel = model;
        surfaceMatchCollectionScene = scene;
        surfaceMatchCollectionItems = collection.Items
            .Select(item => new SurfaceMatchCollectionSelectionItem(
                item.MatchId,
                FormatSurfaceMatchCollectionItem(item),
                item))
            .ToArray();
        selectedSurfaceMatchCollectionItem = surfaceMatchCollectionItems[0];
        ApplySurfaceMatchCollectionSelection(
            selectedSurfaceMatchCollectionItem);
    }

    public bool SelectSurfaceMatchCollectionItem(string matchId)
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

    private void NavigateSurfaceMatchCollectionItem(int direction)
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

        SelectedSurfaceMatchCollectionItem =
            surfaceMatchCollectionItems[nextIndex];
    }

    private void ApplySurfaceMatchCollectionSelection(
        SurfaceMatchCollectionSelectionItem selected)
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
        LoadPublishedSurfaceMatchExperiment(evidence);
        RaiseSurfaceMatchExperimentDisplay(evidence);
        RefreshSurfaceMatchCollectionState();
        AppendLog(
            "Viewer",
            $"Selected retained Surface Match result without execution: matchId={selected.MatchId};collectionSha256={surfaceMatchCollection!.ContentSha256}.");
    }

    private void ClearSurfaceMatchCollection()
    {
        surfaceMatchCollection = null;
        surfaceMatchCollectionModel = null;
        surfaceMatchCollectionScene = null;
        surfaceMatchCollectionItems = [];
        selectedSurfaceMatchCollectionItem = null;
        RefreshSurfaceMatchCollectionState();
    }

    private void RefreshSurfaceMatchCollectionState()
    {
        OnPropertyChanged(nameof(SurfaceMatchCollection));
        OnPropertyChanged(nameof(SurfaceMatchCollectionItems));
        OnPropertyChanged(nameof(IsSurfaceMatchCollectionVisible));
        OnPropertyChanged(nameof(CanSelectSurfaceMatchCollectionItem));
        OnPropertyChanged(nameof(CanNavigatePreviousSurfaceMatchCollectionItem));
        OnPropertyChanged(nameof(CanNavigateNextSurfaceMatchCollectionItem));
        OnPropertyChanged(nameof(SelectedSurfaceMatchCollectionItem));
        OnPropertyChanged(nameof(SurfaceMatchCollectionSummary));
        previousSurfaceMatchCollectionItemCommand?.RaiseCanExecuteChanged();
        nextSurfaceMatchCollectionItemCommand?.RaiseCanExecuteChanged();
    }

    private static string FormatSurfaceMatchCollectionItem(
        SurfaceMatchCollectionItem item)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"#{item.Order + 1} | {MatchIdSuffix(item.MatchId)} | {item.Assessment.Decision.ToString().ToUpperInvariant()}");
    }

    private static string MatchIdSuffix(string matchId) =>
        matchId.Length <= 4 ? matchId : matchId[^4..];
}

public sealed record SurfaceMatchCollectionSelectionItem(
    string MatchId,
    string DisplayName,
    SurfaceMatchCollectionItem Item);
