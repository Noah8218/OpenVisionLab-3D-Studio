using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Identifies the one operator focus shared by the recipe chain, selected-tool
/// workspace, teaching regions, outputs, and Viewer slots. This session is
/// presentation-only: changing it never mutates a recipe or executes a tool.
/// </summary>
public sealed class InspectionWorkspaceSelectionSession : INotifyPropertyChanged
{
    public const string MainViewerSlotId = "viewer.main";

    private InspectionWorkspaceSelectionSnapshot current =
        InspectionWorkspaceSelectionSnapshot.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler<InspectionWorkspaceSelectionChangedEventArgs>? SelectionChanged;

    public InspectionWorkspaceSelectionSnapshot Current => current;
    public string? SelectedStepId => current.SelectedStepId;
    public string? SelectedInputEntityId => current.SelectedInputEntityId;
    public InspectionWorkspaceRegionRole ActiveRegionRole => current.ActiveRegionRole;
    public string? SelectedRegionId => current.SelectedRegionId;
    public string? SelectedOutputEntityId => current.SelectedOutputEntityId;
    public string FocusedViewerSlotId => current.FocusedViewerSlotId;

    /// <summary>
    /// Applies the selected tool's related identities as one notification.
    /// The current Viewer slot is retained because changing a recipe step does
    /// not implicitly move or recreate a Viewer.
    /// </summary>
    internal void SynchronizeTool(
        string? stepId,
        string? inputEntityId,
        InspectionWorkspaceRegionRole activeRegionRole,
        string? regionId,
        string? outputEntityId) =>
        Apply(new InspectionWorkspaceSelectionSnapshot(
            NormalizeIdentity(stepId),
            NormalizeIdentity(inputEntityId),
            NormalizeIdentity(stepId) is null
                ? InspectionWorkspaceRegionRole.None
                : activeRegionRole,
            NormalizeIdentity(stepId) is null
            || activeRegionRole == InspectionWorkspaceRegionRole.None
                ? null
                : NormalizeIdentity(regionId),
            NormalizeIdentity(outputEntityId),
            current.FocusedViewerSlotId));

    public void SelectInput(string? entityId) =>
        Apply(current with { SelectedInputEntityId = NormalizeIdentity(entityId) });

    public void SelectRegion(InspectionWorkspaceRegionRole role, string? regionId) =>
        Apply(current with
        {
            ActiveRegionRole = current.SelectedStepId is null
                ? InspectionWorkspaceRegionRole.None
                : role,
            SelectedRegionId = current.SelectedStepId is null
                               || role == InspectionWorkspaceRegionRole.None
                ? null
                : NormalizeIdentity(regionId)
        });

    public void SelectOutput(string? entityId) =>
        Apply(current with { SelectedOutputEntityId = NormalizeIdentity(entityId) });

    public void FocusViewerSlot(string? viewerSlotId) =>
        Apply(current with { FocusedViewerSlotId = NormalizeViewerSlot(viewerSlotId) });

    public void ClearRecipeSelection() =>
        Apply(InspectionWorkspaceSelectionSnapshot.Empty with
        {
            FocusedViewerSlotId = current.FocusedViewerSlotId
        });

    private void Apply(InspectionWorkspaceSelectionSnapshot next)
    {
        next = next with
        {
            SelectedStepId = NormalizeIdentity(next.SelectedStepId),
            SelectedInputEntityId = NormalizeIdentity(next.SelectedInputEntityId),
            SelectedRegionId = NormalizeIdentity(next.SelectedRegionId),
            SelectedOutputEntityId = NormalizeIdentity(next.SelectedOutputEntityId),
            FocusedViewerSlotId = NormalizeViewerSlot(next.FocusedViewerSlotId)
        };

        if (Equivalent(current, next))
        {
            return;
        }

        var previous = current;
        current = next;
        OnPropertyChanged(nameof(Current));
        NotifyChangedProperties(previous, next);
        SelectionChanged?.Invoke(
            this,
            new InspectionWorkspaceSelectionChangedEventArgs(previous, next));
    }

    private void NotifyChangedProperties(
        InspectionWorkspaceSelectionSnapshot previous,
        InspectionWorkspaceSelectionSnapshot next)
    {
        if (!SameIdentity(previous.SelectedStepId, next.SelectedStepId))
        {
            OnPropertyChanged(nameof(SelectedStepId));
        }

        if (!SameIdentity(previous.SelectedInputEntityId, next.SelectedInputEntityId))
        {
            OnPropertyChanged(nameof(SelectedInputEntityId));
        }

        if (previous.ActiveRegionRole != next.ActiveRegionRole)
        {
            OnPropertyChanged(nameof(ActiveRegionRole));
        }

        if (!SameIdentity(previous.SelectedRegionId, next.SelectedRegionId))
        {
            OnPropertyChanged(nameof(SelectedRegionId));
        }

        if (!SameIdentity(previous.SelectedOutputEntityId, next.SelectedOutputEntityId))
        {
            OnPropertyChanged(nameof(SelectedOutputEntityId));
        }

        if (!SameIdentity(previous.FocusedViewerSlotId, next.FocusedViewerSlotId))
        {
            OnPropertyChanged(nameof(FocusedViewerSlotId));
        }
    }

    private static bool Equivalent(
        InspectionWorkspaceSelectionSnapshot left,
        InspectionWorkspaceSelectionSnapshot right) =>
        SameIdentity(left.SelectedStepId, right.SelectedStepId)
        && SameIdentity(left.SelectedInputEntityId, right.SelectedInputEntityId)
        && left.ActiveRegionRole == right.ActiveRegionRole
        && SameIdentity(left.SelectedRegionId, right.SelectedRegionId)
        && SameIdentity(left.SelectedOutputEntityId, right.SelectedOutputEntityId)
        && SameIdentity(left.FocusedViewerSlotId, right.FocusedViewerSlotId);

    private static bool SameIdentity(string? left, string? right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static string? NormalizeIdentity(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string NormalizeViewerSlot(string? value) =>
        NormalizeIdentity(value) ?? MainViewerSlotId;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public enum InspectionWorkspaceRegionRole
{
    None,
    Selection,
    Reference,
    Measurement,
    First,
    Second
}

public sealed record InspectionWorkspaceSelectionSnapshot(
    string? SelectedStepId,
    string? SelectedInputEntityId,
    InspectionWorkspaceRegionRole ActiveRegionRole,
    string? SelectedRegionId,
    string? SelectedOutputEntityId,
    string FocusedViewerSlotId)
{
    public static InspectionWorkspaceSelectionSnapshot Empty { get; } = new(
        null,
        null,
        InspectionWorkspaceRegionRole.None,
        null,
        null,
        InspectionWorkspaceSelectionSession.MainViewerSlotId);
}

public sealed class InspectionWorkspaceSelectionChangedEventArgs(
    InspectionWorkspaceSelectionSnapshot previous,
    InspectionWorkspaceSelectionSnapshot current) : EventArgs
{
    public InspectionWorkspaceSelectionSnapshot Previous { get; } = previous;
    public InspectionWorkspaceSelectionSnapshot Current { get; } = current;
}
