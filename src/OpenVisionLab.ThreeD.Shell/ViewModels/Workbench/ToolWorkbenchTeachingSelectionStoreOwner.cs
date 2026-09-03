namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;

/// <summary>
/// Owns persisted teaching-selection state, compatible-selection projection,
/// and source-currentness policy. Recipe mutation remains an injected facade seam.
/// </summary>
internal sealed class ToolWorkbenchTeachingSelectionStoreOwner : INotifyPropertyChanged
{
    private readonly Func<string> getRootSourceId;
    private readonly Func<string> getSourceFrameId;
    private readonly Func<ToolRecipeSelectionSourceBinding?> getSourceBinding;
    private readonly Func<ToolRecipeSelection, ToolWorkbenchPublishedSelectionBindingState>
        getPublishedBindingState;
    private readonly Func<ToolRecipeSelection?> getSelectedTeachingSelection;
    private readonly Func<bool> canUseCompatibleSelection;
    private readonly Action<ToolRecipeSelection> removeSelection;
    private readonly Action<ToolRecipeSelection> useCompatibleSelection;
    private readonly RelayCommand removeSelectedTeachingSelectionCommand;
    private readonly RelayCommand useExistingTeachingSelectionCommand;
    private ToolRecipeSelection? selectedCompatibleSelection;

    public ToolWorkbenchTeachingSelectionStoreOwner(
        Func<string> getRootSourceId,
        Func<string> getSourceFrameId,
        Func<ToolRecipeSelectionSourceBinding?> getSourceBinding,
        Func<ToolRecipeSelection, ToolWorkbenchPublishedSelectionBindingState>
            getPublishedBindingState,
        Func<ToolRecipeSelection?> getSelectedTeachingSelection,
        Func<bool> canUseCompatibleSelection,
        Action<ToolRecipeSelection> removeSelection,
        Action<ToolRecipeSelection> useCompatibleSelection)
    {
        this.getRootSourceId = getRootSourceId
            ?? throw new ArgumentNullException(nameof(getRootSourceId));
        this.getSourceFrameId = getSourceFrameId
            ?? throw new ArgumentNullException(nameof(getSourceFrameId));
        this.getSourceBinding = getSourceBinding
            ?? throw new ArgumentNullException(nameof(getSourceBinding));
        this.getPublishedBindingState = getPublishedBindingState
            ?? throw new ArgumentNullException(nameof(getPublishedBindingState));
        this.getSelectedTeachingSelection = getSelectedTeachingSelection
            ?? throw new ArgumentNullException(nameof(getSelectedTeachingSelection));
        this.canUseCompatibleSelection = canUseCompatibleSelection
            ?? throw new ArgumentNullException(nameof(canUseCompatibleSelection));
        this.removeSelection = removeSelection
            ?? throw new ArgumentNullException(nameof(removeSelection));
        this.useCompatibleSelection = useCompatibleSelection
            ?? throw new ArgumentNullException(nameof(useCompatibleSelection));

        removeSelectedTeachingSelectionCommand = new RelayCommand(
            _ => RemoveSelectedTeachingSelection(),
            _ => getSelectedTeachingSelection() is not null);
        useExistingTeachingSelectionCommand = new RelayCommand(
            _ => UseExistingTeachingSelection(),
            _ => SelectedCompatibleSelection is not null
                && canUseCompatibleSelection());
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler? AppliedSelectionsChanged;

    public ObservableCollection<ToolRecipeSelection> Selections { get; } = [];

    public ObservableCollection<ToolRecipeSelection> AvailableCompatibleSelections { get; } = [];

    public RelayCommand RemoveSelectedTeachingSelectionCommand =>
        removeSelectedTeachingSelectionCommand;

    public RelayCommand UseExistingTeachingSelectionCommand =>
        useExistingTeachingSelectionCommand;

    public ToolRecipeSelection? SelectedCompatibleSelection
    {
        get => selectedCompatibleSelection;
        set
        {
            if (ReferenceEquals(selectedCompatibleSelection, value))
            {
                return;
            }

            selectedCompatibleSelection = value;
            OnPropertyChanged();
            useExistingTeachingSelectionCommand.RaiseCanExecuteChanged();
        }
    }

    public ToolRecipeSelection? Find(string selectionId) =>
        Selections.FirstOrDefault(selection => string.Equals(
            selection.Id,
            selectionId,
            StringComparison.OrdinalIgnoreCase));

    public void Clear() => Selections.Clear();

    public void ReplaceAll(IEnumerable<ToolRecipeSelection> selections)
    {
        ArgumentNullException.ThrowIfNull(selections);
        var replacement = selections.ToArray();
        Selections.Clear();
        foreach (var selection in replacement)
        {
            Selections.Add(selection);
        }
    }

    public ToolRecipeSelection Upsert(ToolRecipeSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        var existing = Find(selection.Id);
        if (existing is null)
        {
            Selections.Add(selection);
        }
        else
        {
            Selections[Selections.IndexOf(existing)] = selection;
        }

        return selection;
    }

    public bool Remove(ToolRecipeSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        return Selections.Remove(selection);
    }

    public void RemoveRange(IEnumerable<ToolRecipeSelection> selections)
    {
        ArgumentNullException.ThrowIfNull(selections);
        foreach (var selection in selections.ToArray())
        {
            Selections.Remove(selection);
        }
    }

    public void RebindAll(ToolRecipeSelectionSourceBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);
        for (var index = 0; index < Selections.Count; index++)
        {
            Selections[index] = Selections[index] with { SourceBinding = binding };
        }
    }

    public void RefreshCompatibleSelections(
        ToolWorkbenchTeachingSelectionRequirement? requirement)
    {
        AvailableCompatibleSelections.Clear();
        foreach (var selection in Selections.Where(selection =>
                     ToolWorkbenchTeachingSelectionPolicy.MatchesRequirement(
                         selection,
                         requirement)
                     && IsCurrent(selection)))
        {
            AvailableCompatibleSelections.Add(selection);
        }

        if (SelectedCompatibleSelection is null
            || !AvailableCompatibleSelections.Contains(SelectedCompatibleSelection))
        {
            SelectedCompatibleSelection = AvailableCompatibleSelections.FirstOrDefault();
        }

        RefreshCommandStates();
    }

    public bool IsCurrent(ToolRecipeSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        if (!string.Equals(
            selection.RootSourceId,
            getRootSourceId(),
            StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (selection.SourceBinding.Format is "TransformedHeightField" or "HeightField")
        {
            return getPublishedBindingState(selection)
                == ToolWorkbenchPublishedSelectionBindingState.Current;
        }

        var sourceBinding = getSourceBinding();
        return sourceBinding is not null
            && string.Equals(
                selection.FrameId,
                getSourceFrameId(),
                StringComparison.OrdinalIgnoreCase)
            && ToolRecipeSelectionSourceBindingVerifier.BindingsEqual(
                selection.SourceBinding,
                sourceBinding);
    }

    public IReadOnlyList<ToolRecipeSelection> GetCurrent() =>
        Selections.Where(IsCurrent).ToArray();

    public IReadOnlyList<string> ValidateSourceBindings()
    {
        if (Selections.Count == 0)
        {
            return [];
        }

        var rootSourceId = getRootSourceId();
        var sourceBinding = getSourceBinding();
        var sourceFrameId = getSourceFrameId();
        var errors = new List<string>();
        foreach (var selection in Selections)
        {
            if (!string.Equals(
                selection.RootSourceId,
                rootSourceId,
                StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(
                    $"Selection '{selection.Id}' does not match root source '{rootSourceId}'.");
                continue;
            }

            if (string.Equals(
                selection.SourceBinding.Format,
                "TransformedHeightField",
                StringComparison.Ordinal))
            {
                if (getPublishedBindingState(selection)
                    == ToolWorkbenchPublishedSelectionBindingState.Stale)
                {
                    errors.Add(
                        $"Selection '{selection.Id}' is stale because its Published TransformedHeightField identity changed.");
                }
                continue;
            }

            if (string.Equals(
                selection.SourceBinding.Format,
                "HeightField",
                StringComparison.Ordinal))
            {
                if (getPublishedBindingState(selection)
                    != ToolWorkbenchPublishedSelectionBindingState.Current)
                {
                    errors.Add(
                        $"Selection '{selection.Id}' is stale because its Published HeightField identity is unavailable or changed.");
                }
                continue;
            }

            if (sourceBinding is null)
            {
                errors.Add(
                    $"Selection '{selection.Id}' cannot be verified because the C3D source identity is unavailable.");
                continue;
            }

            if (!string.Equals(
                    selection.FrameId,
                    sourceFrameId,
                    StringComparison.OrdinalIgnoreCase)
                || !ToolRecipeSelectionSourceBindingVerifier.BindingsEqual(
                    selection.SourceBinding,
                    sourceBinding))
            {
                errors.Add(
                    $"Selection '{selection.Id}' is stale because the C3D source bytes or grid dimensions changed.");
            }
        }

        return errors;
    }

    public void NotifyAppliedSelectionsChanged() =>
        AppliedSelectionsChanged?.Invoke(this, EventArgs.Empty);

    public void RefreshCommandStates()
    {
        removeSelectedTeachingSelectionCommand.RaiseCanExecuteChanged();
        useExistingTeachingSelectionCommand.RaiseCanExecuteChanged();
    }

    private void RemoveSelectedTeachingSelection()
    {
        if (getSelectedTeachingSelection() is { } selection)
        {
            removeSelection(selection);
        }
    }

    private void UseExistingTeachingSelection()
    {
        if (SelectedCompatibleSelection is { } selection
            && canUseCompatibleSelection())
        {
            useCompatibleSelection(selection);
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

internal enum ToolWorkbenchPublishedSelectionBindingState
{
    Unavailable,
    Current,
    Stale
}
