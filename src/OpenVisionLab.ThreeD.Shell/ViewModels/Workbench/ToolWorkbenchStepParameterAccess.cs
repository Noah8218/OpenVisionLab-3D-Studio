namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Provides the neutral parameter lookups shared by typed PropertyGrid models.
/// It has no draft state and does not depend on the editing session.
/// </summary>
internal static class ToolWorkbenchStepParameterAccess
{
    internal static string GetParameter(
        ToolWorkbenchPipelineStepItem step,
        string name) =>
        step.Parameters.FirstOrDefault(parameter =>
            string.Equals(parameter.Name, name, StringComparison.Ordinal))?.Value
        ?? string.Empty;

    internal static string GetUnmappedParameters(
        ToolWorkbenchPipelineStepItem step,
        IReadOnlySet<string> mappedNames)
    {
        var values = step.Parameters
            .Where(parameter => !mappedNames.Contains(parameter.Name))
            .Select(parameter => $"{parameter.Name}={parameter.Value}")
            .ToArray();
        return values.Length == 0 ? "(none)" : string.Join("; ", values);
    }
}
