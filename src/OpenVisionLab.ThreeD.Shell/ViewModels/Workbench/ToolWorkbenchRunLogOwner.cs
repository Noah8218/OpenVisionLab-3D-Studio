using System.Collections.ObjectModel;
using OpenVisionLab.Logging;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Owns the Workbench's bounded in-memory activity projection and its durable
/// application-log write. Recipe, selection, and execution owners only supply
/// a category and message; they do not own retention or log ordering.
/// </summary>
internal sealed class ToolWorkbenchRunLogOwner
{
    internal const int MaximumEntries = 3000;

    public ObservableCollection<ToolWorkbenchLogItem> Entries { get; } = [];

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
}

public sealed record ToolWorkbenchLogItem(string Time, string Category, string Message);
