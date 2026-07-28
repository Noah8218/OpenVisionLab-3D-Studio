using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Viewer;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Owns the numeric OrientedBox3D draft and validation surface. It does not
/// mutate a recipe directly; explicit Apply/Delete requests are handled by the
/// Workbench recipe owner.
/// </summary>
public sealed class OrientedBox3DEditorViewModel : INotifyPropertyChanged
{
    private readonly RelayCommand newCommand;
    private readonly RelayCommand applyCommand;
    private readonly RelayCommand cancelCommand;
    private readonly RelayCommand deleteCommand;
    private ToolRecipeSource? source;
    private ToolRecipeSelectionSourceBinding? sourceBinding;
    private ToolRecipeSelection? selectedSelection;
    private string draftId = string.Empty;
    private string name = string.Empty;
    private bool isDraftOpen;
    private double centerX;
    private double centerY;
    private double centerZ;
    private double axisXX = 1;
    private double axisXY;
    private double axisXZ;
    private double axisYX;
    private double axisYY = 1;
    private double axisYZ;
    private double axisZX;
    private double axisZY;
    private double axisZZ = 1;
    private double halfExtentX = 1;
    private double halfExtentY = 1;
    private double halfExtentZ = 1;
    private string status = "Create or select a typed 3D box.";

    public OrientedBox3DEditorViewModel()
    {
        newCommand = new RelayCommand(_ => BeginNew(), _ => HasSourceContext);
        applyCommand = new RelayCommand(_ => RequestApply(), _ => IsDraftOpen && IsDraftValid);
        cancelCommand = new RelayCommand(_ => CancelDraft(), _ => IsDraftOpen);
        deleteCommand = new RelayCommand(
            _ => RequestDelete(),
            _ => SelectedSelection is not null);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler<OrientedBox3DApplyRequestedEventArgs>? ApplyRequested;
    public event EventHandler<OrientedBox3DDeleteRequestedEventArgs>? DeleteRequested;

    public ObservableCollection<ToolRecipeSelection> Selections { get; } = [];

    public bool HasSourceContext => source is not null && sourceBinding is not null;
    public bool HasSelections => Selections.Count > 0;
    public bool IsDraftOpen
    {
        get => isDraftOpen;
        private set
        {
            if (SetField(ref isDraftOpen, value))
            {
                RaiseCommandState();
            }
        }
    }

    public ToolRecipeSelection? SelectedSelection
    {
        get => selectedSelection;
        set
        {
            if (!SetField(ref selectedSelection, value))
            {
                return;
            }

            if (value?.OrientedBox3D is { } box)
            {
                LoadDraft(value.Id, value.Name, box);
                Status = "Editing an applied OrientedBox3D. Apply remains explicit.";
            }
            else if (!IsDraftOpen)
            {
                Status = "Create or select a typed 3D box.";
            }

            RaiseCommandState();
        }
    }

    public string Name
    {
        get => name;
        set => SetDraftField(ref name, value ?? string.Empty);
    }

    public double CenterX { get => centerX; set => SetDraftField(ref centerX, value); }
    public double CenterY { get => centerY; set => SetDraftField(ref centerY, value); }
    public double CenterZ { get => centerZ; set => SetDraftField(ref centerZ, value); }
    public double AxisXX { get => axisXX; set => SetDraftField(ref axisXX, value); }
    public double AxisXY { get => axisXY; set => SetDraftField(ref axisXY, value); }
    public double AxisXZ { get => axisXZ; set => SetDraftField(ref axisXZ, value); }
    public double AxisYX { get => axisYX; set => SetDraftField(ref axisYX, value); }
    public double AxisYY { get => axisYY; set => SetDraftField(ref axisYY, value); }
    public double AxisYZ { get => axisYZ; set => SetDraftField(ref axisYZ, value); }
    public double AxisZX { get => axisZX; set => SetDraftField(ref axisZX, value); }
    public double AxisZY { get => axisZY; set => SetDraftField(ref axisZY, value); }
    public double AxisZZ { get => axisZZ; set => SetDraftField(ref axisZZ, value); }
    public double HalfExtentX { get => halfExtentX; set => SetDraftField(ref halfExtentX, value); }
    public double HalfExtentY { get => halfExtentY; set => SetDraftField(ref halfExtentY, value); }
    public double HalfExtentZ { get => halfExtentZ; set => SetDraftField(ref halfExtentZ, value); }

    public bool IsDraftValid =>
        IsDraftOpen
        && !string.IsNullOrWhiteSpace(Name)
        && ToolRecipeOrientedBox3DGeometry.Validate(CreateGeometry()).Count == 0;

    public string ValidationSummary
    {
        get
        {
            if (!IsDraftOpen)
            {
                return "No numeric box draft is open.";
            }

            if (string.IsNullOrWhiteSpace(Name))
            {
                return "A region name is required.";
            }

            var errors = ToolRecipeOrientedBox3DGeometry.Validate(CreateGeometry());
            return errors.Count == 0
                ? "Valid right-handed box · Apply changes the recipe but does not run inspection."
                : string.Join(" · ", errors);
        }
    }

    public string SourceFrameSummary => source is null
        ? "No identified source frame."
        : $"{source.FrameId} · center/axes/half-extents are stored in declared frame coordinates ({source.Unit}).";

    public string Status
    {
        get => status;
        private set => SetField(ref status, value);
    }

    public ICommand NewCommand => newCommand;
    public ICommand ApplyCommand => applyCommand;
    public ICommand CancelCommand => cancelCommand;
    public ICommand DeleteCommand => deleteCommand;

    public void Synchronize(
        ToolRecipeSource? nextSource,
        ToolRecipeSelectionSourceBinding? nextSourceBinding,
        IEnumerable<ToolRecipeSelection> selections)
    {
        source = nextSource;
        sourceBinding = nextSourceBinding;
        var selectedId = SelectedSelection?.Id;
        var draftWasOpen = IsDraftOpen;
        var draftIdBeforeSync = draftId;

        Selections.Clear();
        foreach (var selection in selections.Where(selection =>
                     selection.Kind == ToolRecipeSelectionKinds.OrientedBox3D
                     && selection.OrientedBox3D is not null))
        {
            Selections.Add(selection);
        }

        OnPropertyChanged(nameof(HasSourceContext));
        OnPropertyChanged(nameof(HasSelections));
        OnPropertyChanged(nameof(SourceFrameSummary));
        SelectedSelection = Selections.FirstOrDefault(selection =>
                                string.Equals(selection.Id, selectedId, StringComparison.OrdinalIgnoreCase))
                            ?? (!draftWasOpen ? Selections.FirstOrDefault() : null);

        if (draftWasOpen && SelectedSelection is null && !string.IsNullOrWhiteSpace(draftIdBeforeSync))
        {
            draftId = draftIdBeforeSync;
            IsDraftOpen = true;
        }

        RaiseCommandState();
    }

    public void SetStatus(string message) =>
        Status = message;

    public void CompleteDelete()
    {
        IsDraftOpen = false;
        draftId = string.Empty;
        SelectedSelection = null;
        Status = "OrientedBox3D deleted from the recipe. Inspection was not run.";
        RaiseAllDraftProperties();
    }

    private void BeginNew()
    {
        if (source is null || sourceBinding is null)
        {
            return;
        }

        SelectedSelection = null;
        draftId = CreateNextId();
        var nextNumber = Selections.Count + 1;
        var geometry = new ToolRecipeOrientedBox3D(
            new ToolRecipeXyz(
                (sourceBinding.GridWidth - 1) / 2.0,
                0,
                (sourceBinding.GridHeight - 1) / 2.0),
            new ToolRecipeXyz(1, 0, 0),
            new ToolRecipeXyz(0, 1, 0),
            new ToolRecipeXyz(0, 0, 1),
            new ToolRecipeXyz(
                Math.Max(0.5, sourceBinding.GridWidth / 4.0),
                1,
                Math.Max(0.5, sourceBinding.GridHeight / 4.0)));
        LoadDraft(draftId, $"3D Box {nextNumber}", geometry);
        Status = "New OrientedBox3D draft. Review frame coordinates and Apply explicitly.";
    }

    private void LoadDraft(string id, string displayName, ToolRecipeOrientedBox3D box)
    {
        draftId = id;
        name = displayName;
        centerX = box.Center.X;
        centerY = box.Center.Y;
        centerZ = box.Center.Z;
        axisXX = box.AxisX.X;
        axisXY = box.AxisX.Y;
        axisXZ = box.AxisX.Z;
        axisYX = box.AxisY.X;
        axisYY = box.AxisY.Y;
        axisYZ = box.AxisY.Z;
        axisZX = box.AxisZ.X;
        axisZY = box.AxisZ.Y;
        axisZZ = box.AxisZ.Z;
        halfExtentX = box.HalfExtents.X;
        halfExtentY = box.HalfExtents.Y;
        halfExtentZ = box.HalfExtents.Z;
        IsDraftOpen = true;
        RaiseAllDraftProperties();
    }

    private void RequestApply()
    {
        if (source is null || sourceBinding is null || !IsDraftValid)
        {
            return;
        }

        ApplyRequested?.Invoke(
            this,
            new OrientedBox3DApplyRequestedEventArgs(
                new ToolRecipeSelection(
                    draftId,
                    Name.Trim(),
                    ToolRecipeSelectionKinds.OrientedBox3D,
                    source.Id,
                    source.FrameId,
                    sourceBinding,
                    null,
                    null,
                    null,
                    null,
                    CreateGeometry())));
    }

    private void RequestDelete()
    {
        if (SelectedSelection is not { } selection)
        {
            return;
        }

        DeleteRequested?.Invoke(this, new OrientedBox3DDeleteRequestedEventArgs(selection.Id));
    }

    private void CancelDraft()
    {
        IsDraftOpen = false;
        draftId = string.Empty;
        SelectedSelection = null;
        Status = "Numeric box draft cancelled; the recipe remains unchanged.";
        RaiseAllDraftProperties();
    }

    private ToolRecipeOrientedBox3D CreateGeometry() =>
        new(
            new ToolRecipeXyz(CenterX, CenterY, CenterZ),
            new ToolRecipeXyz(AxisXX, AxisXY, AxisXZ),
            new ToolRecipeXyz(AxisYX, AxisYY, AxisYZ),
            new ToolRecipeXyz(AxisZX, AxisZY, AxisZZ),
            new ToolRecipeXyz(HalfExtentX, HalfExtentY, HalfExtentZ));

    private string CreateNextId()
    {
        var used = new HashSet<string>(
            Selections.Select(selection => selection.Id),
            StringComparer.OrdinalIgnoreCase);
        for (var index = 1; index < 10000; index++)
        {
            var candidate = $"selection.oriented-box.{index:00}";
            if (!used.Contains(candidate))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("No deterministic OrientedBox3D selection ID is available.");
    }

    private void SetDraftField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        OnPropertyChanged(propertyName);
        OnPropertyChanged(nameof(IsDraftValid));
        OnPropertyChanged(nameof(ValidationSummary));
        applyCommand.RaiseCanExecuteChanged();
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void RaiseAllDraftProperties()
    {
        foreach (var propertyName in new[]
                 {
                     nameof(Name),
                     nameof(CenterX), nameof(CenterY), nameof(CenterZ),
                     nameof(AxisXX), nameof(AxisXY), nameof(AxisXZ),
                     nameof(AxisYX), nameof(AxisYY), nameof(AxisYZ),
                     nameof(AxisZX), nameof(AxisZY), nameof(AxisZZ),
                     nameof(HalfExtentX), nameof(HalfExtentY), nameof(HalfExtentZ),
                     nameof(IsDraftValid), nameof(ValidationSummary)
                 })
        {
            OnPropertyChanged(propertyName);
        }

        RaiseCommandState();
    }

    private void RaiseCommandState()
    {
        newCommand.RaiseCanExecuteChanged();
        applyCommand.RaiseCanExecuteChanged();
        cancelCommand.RaiseCanExecuteChanged();
        deleteCommand.RaiseCanExecuteChanged();
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed class OrientedBox3DApplyRequestedEventArgs(ToolRecipeSelection selection) : EventArgs
{
    public ToolRecipeSelection Selection { get; } = selection;
}

public sealed class OrientedBox3DDeleteRequestedEventArgs(string selectionId) : EventArgs
{
    public string SelectionId { get; } = selectionId;
}
