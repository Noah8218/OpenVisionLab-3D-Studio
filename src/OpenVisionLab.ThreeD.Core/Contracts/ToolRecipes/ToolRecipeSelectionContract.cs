namespace OpenVisionLab.ThreeD.Core;

public static class ToolRecipeSelectionRoles
{
    public const string Region = "region";
    public const string ReferenceRegion = "reference-region";
    public const string MeasurementRegion = "measurement-region";
    public const string InspectionRegion = "inspection-region";
    public const string FirstRegion = "first-region";
    public const string SecondRegion = "second-region";
    public const string SearchRegion = "search-region";
    public const string LinePoints = "line-points";
    public const string PlanePoints = "plane-points";
    public const string MeasurementPoints = "measurement-points";
    public const string Correspondences = "correspondences";
}

public sealed record ToolRecipeSelectionRouteRequirement(
    string ToolId,
    string Role,
    string Kind,
    int FirstInputIndex,
    int LastInputIndex,
    int MinimumCount = 1,
    int MaximumCount = 1,
    int RequiredPointCount = 0)
{
    public bool AcceptsInputIndex(int inputIndex) =>
        inputIndex >= FirstInputIndex && inputIndex <= LastInputIndex;
}

/// <summary>
/// Owns the supported selection kind and semantic input-role matrix. A routed
/// selection is compatible only when one explicit declaration accepts its tool,
/// input position, kind, and cardinality.
/// </summary>
public static class ToolRecipeSelectionContract
{
    private const int UnboundedInputIndex = int.MaxValue;

    private static readonly ToolRecipeSelectionRouteRequirement[] Requirements =
    [
        Many("level-surface", ToolRecipeSelectionRoles.ReferenceRegion, ToolRecipeSelectionKinds.GridRectangle, 1),
        One("grid-circle-authoring", ToolRecipeSelectionRoles.Region, ToolRecipeSelectionKinds.GridCircle, 1),
        One("roi-crop", ToolRecipeSelectionRoles.Region, ToolRecipeSelectionKinds.GridRectangle, 1),
        One("height-difference-edge", ToolRecipeSelectionRoles.SearchRegion, ToolRecipeSelectionKinds.GridRectangle, 1),
        One("two-point-line", ToolRecipeSelectionRoles.LinePoints, ToolRecipeSelectionKinds.PointSet, 1, 2),
        One("three-point-plane", ToolRecipeSelectionRoles.PlanePoints, ToolRecipeSelectionKinds.PointSet, 1, 3),
        One("datum-plane-raw-height-deviation", ToolRecipeSelectionRoles.MeasurementRegion, ToolRecipeSelectionKinds.GridRectangle, 2),
        One("landmark-correspondence", ToolRecipeSelectionRoles.Correspondences, ToolRecipeSelectionKinds.LandmarkCorrespondenceSet, 0),
        One("thickness", ToolRecipeSelectionRoles.ReferenceRegion, ToolRecipeSelectionKinds.GridRectangle, 1),
        One("thickness", ToolRecipeSelectionRoles.MeasurementRegion, ToolRecipeSelectionKinds.GridRectangle, 2),
        One("warpage", ToolRecipeSelectionRoles.MeasurementRegion, ToolRecipeSelectionKinds.GridRectangle, 1),
        One("plane-flatness", ToolRecipeSelectionRoles.ReferenceRegion, ToolRecipeSelectionKinds.GridRectangle, 1),
        One("plane-flatness", ToolRecipeSelectionRoles.MeasurementRegion, ToolRecipeSelectionKinds.GridRectangle, 2),
        One("point-pair-dimensions", ToolRecipeSelectionRoles.MeasurementPoints, ToolRecipeSelectionKinds.PointSet, 1, 2),
        One("gap-flush", ToolRecipeSelectionRoles.FirstRegion, ToolRecipeSelectionKinds.GridRectangle, 1),
        One("gap-flush", ToolRecipeSelectionRoles.SecondRegion, ToolRecipeSelectionKinds.GridRectangle, 2),
        One("volume", ToolRecipeSelectionRoles.ReferenceRegion, ToolRecipeSelectionKinds.GridRectangle, 1),
        One("volume", ToolRecipeSelectionRoles.MeasurementRegion, ToolRecipeSelectionKinds.GridRectangle, 2),
        One("cross-section-dimensions", ToolRecipeSelectionRoles.MeasurementRegion, ToolRecipeSelectionKinds.GridRectangle, 1),
        One("completeness-grid", ToolRecipeSelectionRoles.ReferenceRegion, ToolRecipeSelectionKinds.GridRectangle, 1),
        One("completeness-grid", ToolRecipeSelectionRoles.InspectionRegion, ToolRecipeSelectionKinds.GridRectangle, 2)
    ];

    public static IReadOnlyList<ToolRecipeSelectionRouteRequirement> Declarations => Requirements;

    public static bool TryGetRequirement(
        string? toolId,
        int inputIndex,
        out ToolRecipeSelectionRouteRequirement requirement)
    {
        requirement = Requirements.FirstOrDefault(candidate =>
            string.Equals(candidate.ToolId, toolId, StringComparison.OrdinalIgnoreCase)
            && candidate.AcceptsInputIndex(inputIndex))!;
        return requirement is not null;
    }

    public static bool IsSupported(string? toolId, int inputIndex, string? kind) =>
        TryGetRequirement(toolId, inputIndex, out var requirement)
        && string.Equals(requirement.Kind, kind, StringComparison.Ordinal);

    public static IReadOnlyList<string> Validate(
        ToolRecipeStep step,
        IReadOnlyList<ToolRecipeSelection> selections,
        bool requireAllRoles = true)
    {
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(selections);

        var errors = new List<string>();
        var selectionById = selections
            .Where(selection => selection is not null && !string.IsNullOrWhiteSpace(selection.Id))
            .GroupBy(selection => selection.Id.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var routedSelections = (step.InputEntityIds ?? [])
            .Select((id, index) => new
            {
                Id = id?.Trim() ?? string.Empty,
                Index = index,
                Selection = !string.IsNullOrWhiteSpace(id)
                    && selectionById.TryGetValue(id.Trim(), out var selection)
                        ? selection
                        : null
            })
            .Where(route => route.Selection is not null)
            .ToArray();
        var requirements = Requirements
            .Where(requirement => string.Equals(
                requirement.ToolId,
                step.ToolId,
                StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (requirements.Length == 0)
        {
            foreach (var route in routedSelections)
            {
                errors.Add(
                    $"tool '{Clean(step.ToolId)}' has no declared selection role for input {route.Index + 1}; "
                    + $"selection '{route.Id}' is rejected.");
            }
            return errors;
        }

        foreach (var requirement in requirements)
        {
            var routes = routedSelections
                .Where(route => requirement.AcceptsInputIndex(route.Index))
                .ToArray();
            if ((requireAllRoles && routes.Length < requirement.MinimumCount)
                || routes.Length > requirement.MaximumCount)
            {
                errors.Add(
                    $"selection role '{requirement.Role}' requires "
                    + FormatCount(requirement.MinimumCount, requirement.MaximumCount)
                    + $" {requirement.Kind} selection(s); found {routes.Length}.");
            }
        }

        foreach (var route in routedSelections)
        {
            var requirement = requirements.FirstOrDefault(candidate =>
                candidate.AcceptsInputIndex(route.Index));
            if (requirement is null)
            {
                errors.Add(
                    $"tool '{Clean(step.ToolId)}' does not support a selection at input {route.Index + 1}; "
                    + $"selection '{route.Id}' is rejected.");
                continue;
            }

            if (!string.Equals(route.Selection!.Kind, requirement.Kind, StringComparison.Ordinal))
            {
                errors.Add(
                    $"selection role '{requirement.Role}' requires {requirement.Kind}; "
                    + $"selection '{route.Id}' is {Clean(route.Selection.Kind)}.");
                continue;
            }

            if (requirement.RequiredPointCount > 0
                && route.Selection.Points?.Count != requirement.RequiredPointCount)
            {
                errors.Add(
                    $"selection role '{requirement.Role}' requires exactly "
                    + $"{requirement.RequiredPointCount} point(s); selection '{route.Id}' has "
                    + $"{route.Selection.Points?.Count ?? 0}.");
            }
        }

        return errors;
    }

    private static ToolRecipeSelectionRouteRequirement One(
        string toolId,
        string role,
        string kind,
        int inputIndex,
        int requiredPointCount = 0) =>
        new(toolId, role, kind, inputIndex, inputIndex, RequiredPointCount: requiredPointCount);

    private static ToolRecipeSelectionRouteRequirement Many(
        string toolId,
        string role,
        string kind,
        int firstInputIndex) =>
        new(toolId, role, kind, firstInputIndex, UnboundedInputIndex, 1, int.MaxValue);

    private static string FormatCount(int minimum, int maximum) =>
        minimum == maximum ? minimum.ToString() : $"{minimum}..{maximum}";

    private static string Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "(missing)" : value.Trim();
}
