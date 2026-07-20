using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Input;
using OpenVisionLab.ThreeD.Viewer;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Provides a read-first, session-only display manager over artifacts that
/// already exist in the typed registry. It never changes recipe routing and
/// never invokes Preview, Run, or Publish.
/// </summary>
public sealed partial class ToolWorkbenchViewModel
{
    private string displayedViewerArtifactId = string.Empty;

    public event EventHandler<ToolWorkbenchArtifactDisplayRequestEventArgs>? ViewerArtifactDisplayRequested;

    public ObservableCollection<ToolWorkbenchDisplayedOutputItem> DisplayedOutputs { get; } = [];

    public ICommand ShowDisplayedOutputInViewerCommand { get; private set; } = null!;
    public ICommand PinDisplayedOutputToCompareCommand { get; private set; } = null!;
    public ICommand FocusDisplayedOutputStepCommand { get; private set; } = null!;

    public string DisplayedOutputsSummary => DisplayedOutputs.Count == 0
        ? "No typed artifacts are registered."
        : string.Format(
            Localization.DisplayedOutputsSummaryFormat,
            DisplayedOutputs.Count(item => item.IsRenderableInViewer),
            DisplayedOutputs.Count(item => item.IsEvidenceOnly));

    public string CurrentViewerOutputSummary => DisplayedOutputs.FirstOrDefault(item => item.IsShownInViewer) is { } item
        ? $"{item.DisplayName} | {item.Contract}"
        : Localization.DisplayedOutputsNoViewerSelection;

    private void InitializeDisplayedOutputs()
    {
        ShowDisplayedOutputInViewerCommand = new RelayCommand(
            parameter => RequestDisplayedOutputInViewer(parameter as ToolWorkbenchDisplayedOutputItem),
            parameter => parameter is ToolWorkbenchDisplayedOutputItem { CanShowInViewer: true });
        PinDisplayedOutputToCompareCommand = new RelayCommand(
            parameter => PinDisplayedOutputToCompare(parameter as ToolWorkbenchDisplayedOutputItem),
            parameter => parameter is ToolWorkbenchDisplayedOutputItem { CanPinToCompare: true });
        FocusDisplayedOutputStepCommand = new RelayCommand(
            parameter => FocusDisplayedOutputStep(parameter as ToolWorkbenchDisplayedOutputItem),
            parameter => parameter is ToolWorkbenchDisplayedOutputItem { CanFocusStep: true });
    }

    private void OnDisplayedOutputsLocalizationChanged(object? sender, PropertyChangedEventArgs args) =>
        RefreshDisplayedOutputPresentation();

    private void RebuildDisplayedOutputs()
    {
        DisplayedOutputs.Clear();
        foreach (var artifact in ArtifactRegistry)
        {
            DisplayedOutputs.Add(new ToolWorkbenchDisplayedOutputItem(artifact));
        }

        if (!DisplayedOutputs.Any(item => string.Equals(item.Id, displayedViewerArtifactId, StringComparison.OrdinalIgnoreCase)))
        {
            displayedViewerArtifactId = string.Empty;
        }

        RefreshDisplayedOutputPresentation();
        OnPropertyChanged(nameof(DisplayedOutputsSummary));
    }

    private void RequestDisplayedOutputInViewer(ToolWorkbenchDisplayedOutputItem? item)
    {
        if (item is null || !item.CanShowInViewer || GetCompareCandidate(item.Id) is not { } candidate)
        {
            return;
        }

        var request = new ToolWorkbenchArtifactDisplayRequestEventArgs(
            item.Id,
            candidate.C3DPath,
            item.DisplayName,
            item.Contract,
            candidate.State,
            candidate.IsSource);
        ViewerArtifactDisplayRequested?.Invoke(this, request);
        if (!request.WasDisplayed)
        {
            return;
        }

        displayedViewerArtifactId = item.Id;
        RefreshDisplayedOutputPresentation();
    }

    private void PinDisplayedOutputToCompare(ToolWorkbenchDisplayedOutputItem? item)
    {
        if (item is null || !item.CanPinToCompare)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(CompareSlotAArtifactId))
        {
            CompareSlotAArtifactId = item.Id;
        }
        else if (string.IsNullOrWhiteSpace(CompareSlotBArtifactId))
        {
            CompareSlotBArtifactId = item.Id;
        }
        else if (string.IsNullOrWhiteSpace(CompareSlotCArtifactId))
        {
            CompareSlotCArtifactId = item.Id;
        }
    }

    private void FocusDisplayedOutputStep(ToolWorkbenchDisplayedOutputItem? item)
    {
        if (item?.PipelineStep is not { } step)
        {
            return;
        }

        SelectedPipelineStep = step;
        RefreshNavigatorSelection();
    }

    private void RefreshDisplayedOutputPresentation()
    {
        foreach (var item in DisplayedOutputs)
        {
            var isRenderable = GetCompareCandidate(item.Id) is { C3DPath: var path } && File.Exists(path);
            var pins = GetComparePins(item.Id);
            item.UpdatePresentation(
                isRenderable,
                string.Equals(item.Id, displayedViewerArtifactId, StringComparison.OrdinalIgnoreCase),
                pins,
                isRenderable && pins.Length == 0 && HasEmptyCompareSlot(),
                isRenderable
                    ? Localization.DisplayableC3DData
                    : item.IsEvidenceOnly
                        ? Localization.EvidenceOnlyOutput
                        : Localization.NoCurrentDisplayableOutput,
                pins.Length == 0 ? string.Empty : string.Format(Localization.PinnedSlotsFormat, pins));
        }

        OnPropertyChanged(nameof(CurrentViewerOutputSummary));
        OnPropertyChanged(nameof(DisplayedOutputsSummary));
    }

    private bool HasEmptyCompareSlot() =>
        string.IsNullOrWhiteSpace(CompareSlotAArtifactId)
        || string.IsNullOrWhiteSpace(CompareSlotBArtifactId)
        || string.IsNullOrWhiteSpace(CompareSlotCArtifactId);

    private string GetComparePins(string artifactId)
    {
        var slots = new List<string>(3);
        if (string.Equals(CompareSlotAArtifactId, artifactId, StringComparison.OrdinalIgnoreCase)) slots.Add("A");
        if (string.Equals(CompareSlotBArtifactId, artifactId, StringComparison.OrdinalIgnoreCase)) slots.Add("B");
        if (string.Equals(CompareSlotCArtifactId, artifactId, StringComparison.OrdinalIgnoreCase)) slots.Add("C");
        return slots.Count == 0 ? string.Empty : string.Join(", ", slots);
    }
}

/// <summary>
/// View boundary request for a verified C3D file already registered by the
/// workbench. The receiving View reports whether it actually displayed it.
/// </summary>
public sealed class ToolWorkbenchArtifactDisplayRequestEventArgs(
    string artifactId,
    string c3DPath,
    string displayName,
    string contract,
    string state,
    bool isSource) : EventArgs
{
    public string ArtifactId { get; } = artifactId;
    public string C3DPath { get; } = c3DPath;
    public string DisplayName { get; } = displayName;
    public string Contract { get; } = contract;
    public string State { get; } = state;
    public bool IsSource { get; } = isSource;
    public bool WasDisplayed { get; set; }
}

public sealed class ToolWorkbenchDisplayedOutputItem : INotifyPropertyChanged
{
    private bool isRenderableInViewer;
    private bool isShownInViewer;
    private string comparePins = string.Empty;
    private string comparePinsSummary = string.Empty;
    private string availability = string.Empty;
    private bool canPinToCompare;

    public ToolWorkbenchDisplayedOutputItem(ToolWorkbenchArtifactItem artifact)
    {
        Artifact = artifact;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ToolWorkbenchArtifactItem Artifact { get; }
    public string Id => Artifact.Id;
    public string DisplayName => Artifact.DisplayName;
    public string Contract => Artifact.Contract;
    public string State => Artifact.State;
    public string Detail => Artifact.Detail;
    public string NodeKind => Artifact.NodeKind;
    public ToolWorkbenchPipelineStepItem? PipelineStep => Artifact.PipelineStep;
    public bool CanFocusStep => PipelineStep is not null;
    public bool IsRenderableInViewer => isRenderableInViewer;
    public bool IsShownInViewer => isShownInViewer;
    public bool CanShowInViewer => isRenderableInViewer;
    public bool CanPinToCompare => canPinToCompare;
    public bool IsPinnedToCompare => comparePins.Length > 0;
    public string ComparePins => comparePins;
    public bool IsEvidenceOnly => !isRenderableInViewer
                                  && Artifact.HasContentHash
                                  && !string.Equals(Artifact.State, "Stale", StringComparison.OrdinalIgnoreCase)
                                  && Artifact.NodeKind is not "Source" and not "Selection" and not "DeclaredOutput";
    public bool HasNoCurrentOutput => !isRenderableInViewer && !IsEvidenceOnly;
    public string ComparePinsSummary => comparePinsSummary;
    public string Availability => availability;

    internal void UpdatePresentation(
        bool renderable,
        bool shownInViewer,
        string newComparePins,
        bool mayPinToCompare,
        string newAvailability,
        string newComparePinsSummary)
    {
        SetField(ref isRenderableInViewer, renderable, nameof(IsRenderableInViewer));
        OnPropertyChanged(nameof(CanShowInViewer));
        SetField(ref isShownInViewer, shownInViewer, nameof(IsShownInViewer));
        if (!string.Equals(comparePins, newComparePins, StringComparison.Ordinal))
        {
            comparePins = newComparePins;
            OnPropertyChanged(nameof(ComparePins));
            OnPropertyChanged(nameof(IsPinnedToCompare));
        }

        SetField(ref canPinToCompare, mayPinToCompare, nameof(CanPinToCompare));
        SetField(ref availability, newAvailability, nameof(Availability));
        SetField(ref comparePinsSummary, newComparePinsSummary, nameof(ComparePinsSummary));
        OnPropertyChanged(nameof(IsEvidenceOnly));
        OnPropertyChanged(nameof(HasNoCurrentOutput));
        OnPropertyChanged(nameof(Availability));
    }

    private void SetField(ref bool field, bool value, string propertyName)
    {
        if (field == value)
        {
            return;
        }

        field = value;
        OnPropertyChanged(propertyName);
    }

    private void SetField(ref string field, string value, string propertyName)
    {
        if (string.Equals(field, value, StringComparison.Ordinal))
        {
            return;
        }

        field = value;
        OnPropertyChanged(propertyName);
    }

    private void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
