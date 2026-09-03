using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Owns authored reference definitions, their selected item, and the draft
/// fields used by the reference editor. Recipe dirty-state and persistence
/// remain with the Workbench facade.
/// </summary>
internal sealed class ToolWorkbenchReferenceCatalogOwner
{
    private readonly Func<string?, string> normalizeId;
    private readonly RelayCommand removeSelectedReferenceCommand;
    private ToolWorkbenchReferenceItem? selectedReference;
    private string newReferenceId = "reference.fixture-landmarks";
    private string newReferenceName = "Fixture landmarks";
    private string newReferenceKind = "Landmark set";

    public ToolWorkbenchReferenceCatalogOwner(Func<string?, string> normalizeId)
    {
        this.normalizeId = normalizeId ?? throw new ArgumentNullException(nameof(normalizeId));
        AddReferenceCommand = new RelayCommand(_ => AddReference());
        removeSelectedReferenceCommand = new RelayCommand(
            _ => RemoveSelectedReference(),
            _ => SelectedReference is not null);
    }

    public ObservableCollection<ToolWorkbenchReferenceItem> References { get; } = [];

    public ICommand AddReferenceCommand { get; }

    public ICommand RemoveSelectedReferenceCommand => removeSelectedReferenceCommand;

    public ToolWorkbenchReferenceItem? SelectedReference
    {
        get => selectedReference;
        set
        {
            if (ReferenceEquals(selectedReference, value))
            {
                return;
            }

            selectedReference = value;
            OnPropertyChanged();
            removeSelectedReferenceCommand.RaiseCanExecuteChanged();
        }
    }

    public string NewReferenceId
    {
        get => newReferenceId;
        set => SetField(ref newReferenceId, value ?? string.Empty);
    }

    public string NewReferenceName
    {
        get => newReferenceName;
        set => SetField(ref newReferenceName, value ?? string.Empty);
    }

    public string NewReferenceKind
    {
        get => newReferenceKind;
        set => SetField(ref newReferenceKind, value ?? string.Empty);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event PropertyChangedEventHandler? ReferencePropertyChanged;
    public event EventHandler<ToolWorkbenchReferenceMutationEventArgs>? Mutated;

    public void Clear()
    {
        DetachReferenceHandlers();
        References.Clear();
        SelectedReference = null;
    }

    public void ReplaceAll(IEnumerable<ToolRecipeReference> references)
    {
        ArgumentNullException.ThrowIfNull(references);
        DetachReferenceHandlers();
        References.Clear();
        foreach (var reference in references)
        {
            AddReferenceItem(new ToolWorkbenchReferenceItem(
                reference.Id,
                reference.Name,
                reference.Kind));
        }

        SelectedReference = References.FirstOrDefault();
    }

    public ToolRecipeReference[] CreateSnapshot() =>
        References
            .Select(reference => new ToolRecipeReference(
                reference.Id.Trim(),
                reference.Name.Trim(),
                reference.Kind.Trim()))
            .ToArray();

    private void AddReference()
    {
        var id = string.IsNullOrWhiteSpace(NewReferenceId)
            ? $"reference.{normalizeId(NewReferenceName)}"
            : NewReferenceId.Trim();
        var reference = new ToolWorkbenchReferenceItem(
            id,
            string.IsNullOrWhiteSpace(NewReferenceName) ? id : NewReferenceName.Trim(),
            string.IsNullOrWhiteSpace(NewReferenceKind) ? "Reference" : NewReferenceKind.Trim());
        AddReferenceItem(reference);
        SelectedReference = reference;
        NewReferenceId = $"reference.{normalizeId(NewReferenceName)}";
        Mutated?.Invoke(
            this,
            new ToolWorkbenchReferenceMutationEventArgs(
                reference,
                Added: true));
    }

    private void RemoveSelectedReference()
    {
        if (SelectedReference is not { } reference)
        {
            return;
        }

        reference.PropertyChanged -= OnReferencePropertyChanged;
        References.Remove(reference);
        SelectedReference = References.LastOrDefault();
        Mutated?.Invoke(
            this,
            new ToolWorkbenchReferenceMutationEventArgs(
                reference,
                Added: false));
    }

    private void AddReferenceItem(ToolWorkbenchReferenceItem reference)
    {
        reference.PropertyChanged += OnReferencePropertyChanged;
        References.Add(reference);
    }

    private void DetachReferenceHandlers()
    {
        foreach (var reference in References)
        {
            reference.PropertyChanged -= OnReferencePropertyChanged;
        }
    }

    private void OnReferencePropertyChanged(object? sender, PropertyChangedEventArgs args) =>
        ReferencePropertyChanged?.Invoke(sender, args);

    private void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private void SetField(
        ref string field,
        string value,
        [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        if (string.Equals(field, value, StringComparison.Ordinal))
        {
            return;
        }

        field = value;
        OnPropertyChanged(propertyName);
    }
}

internal sealed record ToolWorkbenchReferenceMutationEventArgs(
    ToolWorkbenchReferenceItem Reference,
    bool Added);
