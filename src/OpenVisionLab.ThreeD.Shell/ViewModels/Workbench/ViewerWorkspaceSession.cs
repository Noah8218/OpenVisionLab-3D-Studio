using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Owns the presentation-only composition of the normal inspection Viewer.
/// Each slot keeps its own Viewer instance and display state in the View; this
/// session owns only layout, focus, and the auxiliary slot's content pin.
/// </summary>
public sealed class ViewerWorkspaceSession : INotifyPropertyChanged
{
    public const string MainSlotId = InspectionWorkspaceSelectionSession.MainViewerSlotId;
    public const string AuxiliarySlotId = "viewer.auxiliary";

    private ViewerWorkspaceLayout layout = ViewerWorkspaceLayout.Single;
    private string auxiliaryContentId = string.Empty;
    private string focusedSlotId = MainSlotId;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ViewerWorkspaceLayout Layout => layout;
    public string AuxiliaryContentId => auxiliaryContentId;
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
        if (string.Equals(auxiliaryContentId, normalized, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        auxiliaryContentId = normalized;
        OnPropertyChanged(nameof(AuxiliaryContentId));
    }

    public void ReconcileContents(IEnumerable<string> availableContentIds, string? preferredContentId)
    {
        ArgumentNullException.ThrowIfNull(availableContentIds);
        var available = availableContentIds
            .Select(NormalizeIdentity)
            .Where(id => id.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (available.Contains(auxiliaryContentId, StringComparer.OrdinalIgnoreCase))
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
