using System.Globalization;

namespace OpenVisionLab.ThreeD.Shell.Coordination;

/// <summary>
/// Immutable process arguments captured by the Shell composition root.
///
/// The object deliberately owns only lookup rules. It does not interpret a
/// workflow or touch WPF, so startup planning and Smoke verification can share
/// the same deterministic input without reading the process environment again.
/// </summary>
internal sealed class ShellCommandLineArguments
{
    private readonly IReadOnlyList<string> values;

    public ShellCommandLineArguments(IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        values = Array.AsReadOnly(arguments.ToArray());
    }

    public static ShellCommandLineArguments Capture() =>
        new(Environment.GetCommandLineArgs());

    public IReadOnlyList<string> Values => values;

    public bool HasFlag(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return values.Contains(name, StringComparer.OrdinalIgnoreCase);
    }

    public string? GetValue(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        for (var index = 0; index < values.Count - 1; index++)
        {
            if (string.Equals(values[index], name, StringComparison.Ordinal))
            {
                return values[index + 1];
            }
        }

        return null;
    }

    public string? GetValueIgnoreCase(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        for (var index = 0; index < values.Count - 1; index++)
        {
            if (string.Equals(values[index], name, StringComparison.OrdinalIgnoreCase))
            {
                return values[index + 1];
            }
        }

        return null;
    }

    public double? GetInvariantDouble(string name) =>
        double.TryParse(
            GetValue(name),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var value)
            ? value
            : null;

    public int? GetInvariantInt(string name) =>
        int.TryParse(
            GetValue(name),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var value)
            ? value
            : null;
}
