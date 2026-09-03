using System.Windows.Input;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Presentation-only selection over one retained multiple-match collection.
/// Selection never executes matching or changes collection identity.
/// </summary>
public sealed partial class ToolWorkbenchViewModel
{
    private ToolWorkbenchSurfaceMatchCollectionOwner surfaceMatchCollectionOwner = null!;

    public SurfaceMatchCollectionArtifact? SurfaceMatchCollection =>
        surfaceMatchCollectionOwner.SurfaceMatchCollection;

    public IReadOnlyList<SurfaceMatchCollectionSelectionItem>
        SurfaceMatchCollectionItems => surfaceMatchCollectionOwner.SurfaceMatchCollectionItems;

    public bool IsSurfaceMatchCollectionVisible =>
        surfaceMatchCollectionOwner.IsSurfaceMatchCollectionVisible;

    public bool CanSelectSurfaceMatchCollectionItem =>
        surfaceMatchCollectionOwner.CanSelectSurfaceMatchCollectionItem;

    public bool CanNavigatePreviousSurfaceMatchCollectionItem =>
        surfaceMatchCollectionOwner.CanNavigatePreviousSurfaceMatchCollectionItem;

    public bool CanNavigateNextSurfaceMatchCollectionItem =>
        surfaceMatchCollectionOwner.CanNavigateNextSurfaceMatchCollectionItem;

    public ICommand PreviousSurfaceMatchCollectionItemCommand =>
        surfaceMatchCollectionOwner.PreviousSurfaceMatchCollectionItemCommand;

    public ICommand NextSurfaceMatchCollectionItemCommand =>
        surfaceMatchCollectionOwner.NextSurfaceMatchCollectionItemCommand;

    public SurfaceMatchCollectionSelectionItem?
        SelectedSurfaceMatchCollectionItem
    {
        get => surfaceMatchCollectionOwner.SelectedSurfaceMatchCollectionItem;
        set => surfaceMatchCollectionOwner.SelectedSurfaceMatchCollectionItem = value;
    }

    public string SurfaceMatchCollectionSummary =>
        surfaceMatchCollectionOwner.SurfaceMatchCollectionSummary;

    private void InitializeSurfaceMatchCollectionOwner()
    {
        surfaceMatchCollectionOwner = new ToolWorkbenchSurfaceMatchCollectionOwner(
            () => IsSurfaceMatchExperimentVisible,
            () => IsSurfaceMatchExperimentRunning,
            () => HasSurfaceMatchExperimentCandidate,
            LoadPublishedSurfaceMatchExperiment,
            RaiseSurfaceMatchExperimentDisplay,
            (category, message) => AppendLog(category, message));
        surfaceMatchCollectionOwner.PropertyChanged += (_, args) =>
            OnPropertyChanged(args.PropertyName);
    }

    public void ShowSurfaceMatchCollectionEvidence(
        SurfaceModelArtifact model,
        PreparedSceneArtifact scene,
        SurfaceMatchCollectionArtifact collection) =>
        surfaceMatchCollectionOwner.Show(model, scene, collection);

    public bool SelectSurfaceMatchCollectionItem(string matchId) =>
        surfaceMatchCollectionOwner.Select(matchId);

    private void ClearSurfaceMatchCollection() =>
        surfaceMatchCollectionOwner.Clear();

    private void RefreshSurfaceMatchCollectionState() =>
        surfaceMatchCollectionOwner.RefreshState();
}

public sealed record SurfaceMatchCollectionSelectionItem(
    string MatchId,
    string DisplayName,
    SurfaceMatchCollectionItem Item);
