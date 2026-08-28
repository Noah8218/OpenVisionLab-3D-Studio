using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Owns the presentation-only composition of the normal inspection Viewer.
/// Each slot keeps its own Viewer instance and display state in the View; this
/// session owns only layout, focus, and the session-only source/output identity
/// pinned to each slot. A missing pin is retained instead of being replaced by
/// another candidate so the View can report an explicit stale/unavailable state.
/// </summary>
public sealed class ViewerWorkspaceSession : INotifyPropertyChanged
{
    public const string MainSlotId = InspectionWorkspaceSelectionSession.MainViewerSlotId;
    public const string AuxiliarySlotId = "viewer.auxiliary";

    private ViewerWorkspaceLayout layout = ViewerWorkspaceLayout.Single;
    private string mainContentId = string.Empty;
    private string auxiliaryContentId = string.Empty;
    private bool mainContentExplicitlyCleared;
    private bool auxiliaryContentExplicitlyCleared;
    private bool cameraLinked;
    private string focusedSlotId = MainSlotId;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ViewerWorkspaceLayout Layout => layout;
    public string MainContentId => mainContentId;
    public string AuxiliaryContentId => auxiliaryContentId;
    public bool IsMainContentPinned => mainContentId.Length > 0;
    public bool IsAuxiliaryContentPinned => auxiliaryContentId.Length > 0;
    public bool IsMainContentExplicitlyCleared => mainContentExplicitlyCleared;
    public bool IsAuxiliaryContentExplicitlyCleared => auxiliaryContentExplicitlyCleared;
    public bool IsCameraLinked => cameraLinked;
    public string FocusedSlotId => focusedSlotId;
    public bool HasAuxiliarySlot => layout != ViewerWorkspaceLayout.Single;
    public bool IsInlineSplit =>
        layout is ViewerWorkspaceLayout.SplitVertical or ViewerWorkspaceLayout.SplitHorizontal;
    public bool IsPopOut => layout == ViewerWorkspaceLayout.PopOut;
    public bool IsMainFocused => string.Equals(focusedSlotId, MainSlotId, StringComparison.Ordinal);
    public bool IsAuxiliaryFocused =>
        string.Equals(focusedSlotId, AuxiliarySlotId, StringComparison.Ordinal);

    public void SetLayout(ViewerWorkspaceLayout value)
    {
        if (layout == value)
        {
            return;
        }

        layout = value;
        if (layout == ViewerWorkspaceLayout.Single)
        {
            SetCameraLinked(false);
            focusedSlotId = MainSlotId;
        }

        OnPropertyChanged(nameof(Layout));
        OnPropertyChanged(nameof(HasAuxiliarySlot));
        OnPropertyChanged(nameof(IsInlineSplit));
        OnPropertyChanged(nameof(IsPopOut));
        OnPropertyChanged(nameof(FocusedSlotId));
        OnPropertyChanged(nameof(IsMainFocused));
        OnPropertyChanged(nameof(IsAuxiliaryFocused));
    }

    public bool TrySetLayout(
        ViewerWorkspaceLayout value,
        IEnumerable<string> availableContentIds,
        string? preferredContentId)
    {
        ArgumentNullException.ThrowIfNull(availableContentIds);
        if (value != ViewerWorkspaceLayout.Single)
        {
            var available = availableContentIds
                .Select(NormalizeIdentity)
                .Where(id => id.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (available.Length == 0)
            {
                return false;
            }

            // Returning to a split layout is an explicit request to populate a
            // previously cleared auxiliary slot. A stale non-empty pin is
            // intentionally preserved and is never replaced here.
            auxiliaryContentExplicitlyCleared = false;
            ReconcileContents(available, preferredContentId);
            if (string.IsNullOrWhiteSpace(auxiliaryContentId))
            {
                return false;
            }
        }

        SetLayout(value);
        if (value == ViewerWorkspaceLayout.Single)
        {
            FocusSlot(MainSlotId);
        }
        return true;
    }

    public bool TryOpenAuxiliaryContent(
        string contentId,
        IEnumerable<string> availableContentIds)
    {
        ArgumentNullException.ThrowIfNull(availableContentIds);
        var normalized = NormalizeIdentity(contentId);
        if (normalized.Length == 0
            || !availableContentIds
                .Select(NormalizeIdentity)
                .Contains(normalized, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        PinAuxiliaryContent(normalized);
        SetLayout(ViewerWorkspaceLayout.SplitVertical);
        FocusSlot(AuxiliarySlotId);
        return true;
    }

    public void PinAuxiliaryContent(string? contentId)
    {
        var normalized = NormalizeIdentity(contentId);
        var wasCleared = auxiliaryContentExplicitlyCleared;
        if (string.Equals(auxiliaryContentId, normalized, StringComparison.OrdinalIgnoreCase)
            && !wasCleared)
        {
            return;
        }

        auxiliaryContentId = normalized;
        auxiliaryContentExplicitlyCleared = false;
        OnPropertyChanged(nameof(AuxiliaryContentId));
        OnPropertyChanged(nameof(IsAuxiliaryContentPinned));
        OnPropertyChanged(nameof(IsAuxiliaryContentExplicitlyCleared));
    }

    public void PinMainContent(string? contentId)
    {
        var normalized = NormalizeIdentity(contentId);
        var wasCleared = mainContentExplicitlyCleared;
        if (string.Equals(mainContentId, normalized, StringComparison.OrdinalIgnoreCase)
            && !wasCleared)
        {
            return;
        }

        mainContentId = normalized;
        mainContentExplicitlyCleared = false;
        OnPropertyChanged(nameof(MainContentId));
        OnPropertyChanged(nameof(IsMainContentPinned));
        OnPropertyChanged(nameof(IsMainContentExplicitlyCleared));
    }

    public void ClearAuxiliaryContent()
    {
        if (auxiliaryContentId.Length == 0 && auxiliaryContentExplicitlyCleared)
        {
            return;
        }

        auxiliaryContentId = string.Empty;
        auxiliaryContentExplicitlyCleared = true;
        SetCameraLinked(false);
        OnPropertyChanged(nameof(AuxiliaryContentId));
        OnPropertyChanged(nameof(IsAuxiliaryContentPinned));
        OnPropertyChanged(nameof(IsAuxiliaryContentExplicitlyCleared));
    }

    public void ClearMainContent()
    {
        if (mainContentId.Length == 0 && mainContentExplicitlyCleared)
        {
            return;
        }

        mainContentId = string.Empty;
        mainContentExplicitlyCleared = true;
        SetCameraLinked(false);
        OnPropertyChanged(nameof(MainContentId));
        OnPropertyChanged(nameof(IsMainContentPinned));
        OnPropertyChanged(nameof(IsMainContentExplicitlyCleared));
    }

    /// <summary>
    /// Links only the session-owned presentation cameras. The View validates
    /// that both slots are real 3D Viewers before copying a camera state.
    /// </summary>
    public void SetCameraLinked(bool value)
    {
        if (cameraLinked == value)
        {
            return;
        }

        cameraLinked = value;
        OnPropertyChanged(nameof(IsCameraLinked));
    }

    /// <summary>
    /// Starts a new recipe/source presentation context without changing the
    /// selected layout or focused slot. Opening a recipe is an explicit
    /// context boundary; ordinary candidate refreshes must use
    /// <see cref="ReconcileMainContent"/> and <see cref="ReconcileContents"/>
    /// so they retain stale pins instead of silently rebinding them.
    /// </summary>
    public void ResetContentPins()
    {
        var mainChanged = mainContentId.Length > 0 || mainContentExplicitlyCleared;
        var auxiliaryChanged = auxiliaryContentId.Length > 0 || auxiliaryContentExplicitlyCleared;
        var cameraWasLinked = cameraLinked;
        mainContentId = string.Empty;
        auxiliaryContentId = string.Empty;
        mainContentExplicitlyCleared = false;
        auxiliaryContentExplicitlyCleared = false;
        cameraLinked = false;
        if (mainChanged)
        {
            OnPropertyChanged(nameof(MainContentId));
            OnPropertyChanged(nameof(IsMainContentPinned));
            OnPropertyChanged(nameof(IsMainContentExplicitlyCleared));
        }

        if (auxiliaryChanged)
        {
            OnPropertyChanged(nameof(AuxiliaryContentId));
            OnPropertyChanged(nameof(IsAuxiliaryContentPinned));
            OnPropertyChanged(nameof(IsAuxiliaryContentExplicitlyCleared));
        }

        if (cameraWasLinked)
        {
            OnPropertyChanged(nameof(IsCameraLinked));
        }
    }

    public void ReconcileMainContent(
        IEnumerable<string> availableContentIds,
        string? preferredContentId)
    {
        ArgumentNullException.ThrowIfNull(availableContentIds);
        if (mainContentId.Length > 0 || mainContentExplicitlyCleared)
        {
            return;
        }

        var available = availableContentIds
            .Select(NormalizeIdentity)
            .Where(id => id.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (available.Length == 0)
        {
            return;
        }

        var preferred = NormalizeIdentity(preferredContentId);
        PinMainContent(
            available.Contains(preferred, StringComparer.OrdinalIgnoreCase)
                ? preferred
                : available[0]);
    }

    public void ReconcileContents(IEnumerable<string> availableContentIds, string? preferredContentId)
    {
        ArgumentNullException.ThrowIfNull(availableContentIds);
        var available = availableContentIds
            .Select(NormalizeIdentity)
            .Where(id => id.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (auxiliaryContentId.Length > 0 || auxiliaryContentExplicitlyCleared)
        {
            return;
        }

        var preferred = NormalizeIdentity(preferredContentId);
        PinAuxiliaryContent(
            available.Contains(preferred, StringComparer.OrdinalIgnoreCase)
                ? preferred
                : available.FirstOrDefault());
    }

    public void FocusSlot(string? slotId)
    {
        var normalized = NormalizeIdentity(slotId);
        var next = string.Equals(normalized, AuxiliarySlotId, StringComparison.OrdinalIgnoreCase)
                   && HasAuxiliarySlot
            ? AuxiliarySlotId
            : MainSlotId;
        if (string.Equals(focusedSlotId, next, StringComparison.Ordinal))
        {
            return;
        }

        focusedSlotId = next;
        OnPropertyChanged(nameof(FocusedSlotId));
        OnPropertyChanged(nameof(IsMainFocused));
        OnPropertyChanged(nameof(IsAuxiliaryFocused));
    }

    private static string NormalizeIdentity(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public enum ViewerWorkspaceLayout
{
    Single,
    SplitVertical,
    SplitHorizontal,
    PopOut
}
