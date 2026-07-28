using System.ComponentModel;
using System.Runtime.CompilerServices;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Tools.Authoring;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Owns the editable repeat-grid request and its display-only candidate. The
/// recipe remains external and unchanged until the Workbench explicitly
/// applies <see cref="Draft"/>.
/// </summary>
public sealed class ThicknessRepeatGridAuthoringSession : INotifyPropertyChanged
{
    private ToolRecipeDocument? originalDocument;
    private string selectedStepId = string.Empty;
    private int columns = 4;
    private int rows = 2;
    private int columnPitch = 288;
    private int rowPitch = 336;
    private string namePattern = "Pad {n}";
    private ThicknessRepeatGridAuthoringResult? result;

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool IsActive => originalDocument is not null;
    public bool IsInactive => !IsActive;
    public int Columns { get => columns; set => SetRequestValue(ref columns, value); }
    public int Rows { get => rows; set => SetRequestValue(ref rows, value); }
    public int ColumnPitch { get => columnPitch; set => SetRequestValue(ref columnPitch, value); }
    public int RowPitch { get => rowPitch; set => SetRequestValue(ref rowPitch, value); }
    public string NamePattern
    {
        get => namePattern;
        set => SetRequestValue(ref namePattern, value ?? string.Empty);
    }
    public ThicknessRepeatGridDraft? Draft => result?.Draft;
    public IReadOnlyList<ThicknessRepeatGridCandidate> Candidates => result?.Candidates ?? [];
    public bool IsValid => result?.IsValid == true;
    public string ValidationSummary => result is null
        ? "Select Repeat as grid to review translated ROI pairs."
        : result.Errors.Count == 0
            ? "All repeated ROI pairs are inside the recorded source grid."
            : string.Join(" ", result.Errors);

    public void Begin(ToolRecipeDocument document, string stepId)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(stepId);
        originalDocument = document;
        selectedStepId = stepId;
        columns = 4;
        rows = 2;
        columnPitch = 288;
        rowPitch = 336;
        namePattern = "Pad {n}";
        Rebuild();
        RaiseAll();
    }

    public void Cancel()
    {
        if (!IsActive && result is null)
        {
            return;
        }

        originalDocument = null;
        selectedStepId = string.Empty;
        result = null;
        RaiseAll();
    }

    private void Rebuild()
    {
        result = originalDocument is null
            ? null
            : ThicknessRepeatGridAuthoringService.CreateCandidate(
                originalDocument,
                selectedStepId,
                new ThicknessRepeatGridRequest(
                    Columns,
                    Rows,
                    ColumnPitch,
                    RowPitch,
                    NamePattern));
        OnPropertyChanged(nameof(Draft));
        OnPropertyChanged(nameof(Candidates));
        OnPropertyChanged(nameof(IsValid));
        OnPropertyChanged(nameof(ValidationSummary));
    }

    private void SetRequestValue<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        OnPropertyChanged(propertyName);
        Rebuild();
    }

    private void RaiseAll()
    {
        OnPropertyChanged(nameof(IsActive));
        OnPropertyChanged(nameof(IsInactive));
        OnPropertyChanged(nameof(Columns));
        OnPropertyChanged(nameof(Rows));
        OnPropertyChanged(nameof(ColumnPitch));
        OnPropertyChanged(nameof(RowPitch));
        OnPropertyChanged(nameof(NamePattern));
        OnPropertyChanged(nameof(Draft));
        OnPropertyChanged(nameof(Candidates));
        OnPropertyChanged(nameof(IsValid));
        OnPropertyChanged(nameof(ValidationSummary));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
