using System.Collections.ObjectModel;
using OpenVisionLab.Logging;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Owns the Workbench's bounded in-memory activity projection and its durable
/// application-log write. Recipe, selection, and execution owners only supply
/// a category and message; they do not own retention or log ordering.
/// Explicit execution owners also supply typed results for the Teaching drawer;
/// that read-only projection is independent of the complete activity log.
/// </summary>
internal sealed class ToolWorkbenchRunLogOwner
{
    internal const int MaximumEntries = 3000;
    private readonly ObservableCollection<ToolWorkbenchExecutionLogItem> executionEntries = [];

    public ToolWorkbenchRunLogOwner()
    {
        ExecutionEntries = new ReadOnlyObservableCollection<ToolWorkbenchExecutionLogItem>(executionEntries);
    }

    public ObservableCollection<ToolWorkbenchLogItem> Entries { get; } = [];
    public ReadOnlyObservableCollection<ToolWorkbenchExecutionLogItem> ExecutionEntries { get; }

    public void Append(string category, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(category);
        ArgumentNullException.ThrowIfNull(message);

        var level = category.Equals("Error", StringComparison.OrdinalIgnoreCase)
            ? LogLevel.Error
            : category.Equals("Warning", StringComparison.OrdinalIgnoreCase)
                ? LogLevel.Warning
                : LogLevel.Info;
        OVLog.Write(LogCategory.UI, level, $"Workbench[{category}] {message}");
        Entries.Insert(0, new ToolWorkbenchLogItem(
            DateTime.Now.ToString("HH:mm:ss"),
            category,
            message));
        while (Entries.Count > MaximumEntries)
        {
            Entries.RemoveAt(Entries.Count - 1);
        }
    }

    public void AppendExecution(string message, ResultStatus? status, bool wasCanceled = false)
    {
        ArgumentNullException.ThrowIfNull(message);
        executionEntries.Insert(0, new ToolWorkbenchExecutionLogItem(DateTime.Now.ToString("HH:mm:ss"), message, status, wasCanceled));
        while (executionEntries.Count > MaximumEntries) executionEntries.RemoveAt(executionEntries.Count - 1);
    }

    public void ClearExecutionEntries() => executionEntries.Clear();
}

public sealed record ToolWorkbenchLogItem(string Time, string Category, string Message);

public sealed record ToolWorkbenchExecutionLogItem(string Time, string Message, ResultStatus? Status, bool WasCanceled = false)
{
    public string StatusLabel => WasCanceled
        ? ThreeDLocalization.Shared.Resolve("ThreeD.Drawer.ExecutionCanceled", "취소", "Canceled")
        : Status is { } status ? ThreeDLocalization.Shared.ResultStatusLabel(status)
            : ThreeDLocalization.Shared.Resolve("ThreeD.Drawer.ExecutionCompleted", "완료", "Completed");
}
