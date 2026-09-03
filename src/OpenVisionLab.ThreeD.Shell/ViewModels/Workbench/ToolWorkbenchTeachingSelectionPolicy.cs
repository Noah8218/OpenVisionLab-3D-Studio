namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

using OpenVisionLab.ThreeD.Core;

/// <summary>
/// Pure teaching-selection requirement, identity, geometry, and presentation policy.
/// Runtime source ownership and recipe mutation are supplied by the teaching owners.
/// </summary>
internal static class ToolWorkbenchTeachingSelectionPolicy
{
    public static ToolWorkbenchTeachingSelectionRequirement? CreateRequirement(
        ToolWorkbenchPipelineStepItem? step,
        ToolWorkbenchTeachingSelectionRequirement dualRoiRequirement,
        string crossSectionName,
        string crossSectionDetail,
        bool measurementRole)
    {
        if (step is null)
        {
            return null;
        }

        var presentation = step.ToolId switch
        {
            "roi-crop" => new ToolWorkbenchTeachingSelectionRequirement(
                "Grid rectangle",
                string.Empty,
                0,
                true,
                "Pick two opposite grid-cell corners for the crop ROI."),
            "height-difference-edge" => new(
                "Edge search band",
                string.Empty,
                0,
                true,
                "Pick two opposite grid-cell corners for the explicit edge search band."),
            "level-surface" => new(
                "Level reference ROI",
                string.Empty,
                0,
                true,
                "Pick two opposite grid-cell corners on a stable reference surface. Additional reference ROIs may be routed to the same step."),
            "thickness" or "plane-flatness" or "gap-flush" or "volume" or "completeness-grid" =>
                dualRoiRequirement,
            "warpage" => new(
                "Warpage measurement ROI",
                string.Empty,
                0,
                true,
                "Pick two opposite grid-cell corners for the measurement ROI."),
            "point-pair-dimensions" => new(
                "Point pair",
                string.Empty,
                0,
                true,
                "Pick exactly two distinct cells in the Published TransformedHeightField."),
            "cross-section-dimensions" => new(
                crossSectionName,
                string.Empty,
                0,
                true,
                crossSectionDetail),
            "two-point-line" => new(
                "Line points",
                string.Empty,
                0,
                true,
                "Pick exactly two distinct C3D grid cells."),
            "three-point-plane" => new(
                "Plane points",
                string.Empty,
                0,
                true,
                "Pick exactly three distinct, non-collinear C3D grid cells."),
            "datum-plane-raw-height-deviation" => new(
                "Datum measurement ROI",
                string.Empty,
                0,
                true,
                "Pick two opposite grid-cell corners for raw-height residual measurement."),
            "grid-circle-authoring" => new(
                "Circular surface ROI",
                string.Empty,
                0,
                true,
                "Pick the center cell, then one boundary cell. Radius is measured between grid-cell centers."),
            "grid-polygon-authoring" => new(
                "Irregular surface region",
                string.Empty,
                3,
                true,
                "Pick three or more ordered grid vertices. This slice stores the outline only; no mask or inspection is generated."),
            "landmark-correspondence" => new(
                "Landmark correspondences",
                string.Empty,
                0,
                false,
                "Enter explicit source entities and fixture coordinates."),
            _ => null
        };
        if (presentation is null)
        {
            return null;
        }

        var inputIndex = step.ToolId switch
        {
            "landmark-correspondence" => 0,
            "datum-plane-raw-height-deviation" => 2,
            "level-surface" => Math.Max(1, step.InputEntityIds.Count),
            "thickness" or "plane-flatness" or "gap-flush" or "volume" or "completeness-grid" =>
                measurementRole ? 2 : 1,
            _ => 1
        };
        return ToolRecipeSelectionContract.TryGetRequirement(
            step.ToolId,
            inputIndex,
            out var requirement)
                ? presentation with
                {
                    Kind = requirement.Kind,
                    RequiredPointCount = requirement.RequiredPointCount > 0
                        ? requirement.RequiredPointCount
                        : requirement.Kind is ToolRecipeSelectionKinds.GridRectangle
                            or ToolRecipeSelectionKinds.GridCircle
                            ? 2
                            : requirement.Kind == ToolRecipeSelectionKinds.GridPolygon
                                ? 3
                                : 0
                }
                : null;
    }

    public static bool MatchesRequirement(
        ToolRecipeSelection selection,
        ToolWorkbenchTeachingSelectionRequirement? requirement)
    {
        if (requirement is null
            || !string.Equals(
                selection.Kind,
                requirement.Kind,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return requirement.Kind switch
        {
            ToolRecipeSelectionKinds.GridRectangle => selection.GridRectangle is not null,
            ToolRecipeSelectionKinds.GridCircle => selection.GridCircle is not null,
            ToolRecipeSelectionKinds.GridPolygon => selection.GridPolygon is not null,
            ToolRecipeSelectionKinds.PointSet =>
                selection.Points?.Count == requirement.RequiredPointCount,
            ToolRecipeSelectionKinds.LandmarkCorrespondenceSet => selection.Rows is not null,
            _ => false
        };
    }

    public static string CreateSelectionId(
        ToolWorkbenchPipelineStepItem step,
        ToolWorkbenchTeachingSelectionRequirement requirement,
        bool isDualRoiMeasurement,
        bool isGapFlush,
        bool isCompletenessGrid,
        bool measurementRole,
        bool isAdditionalLevelSurfaceReference,
        IEnumerable<ToolRecipeSelection> selections)
    {
        var suffix = isDualRoiMeasurement
            ? isGapFlush
                ? measurementRole ? "second-roi" : "first-roi"
                : isCompletenessGrid
                    ? measurementRole ? "inspection-grid-roi" : "reference-roi"
                    : measurementRole ? "measurement-roi" : "reference-roi"
            : requirement.Kind switch
            {
                ToolRecipeSelectionKinds.GridRectangle => "roi",
                ToolRecipeSelectionKinds.GridPolygon => "polygon",
                ToolRecipeSelectionKinds.PointSet => "points",
                ToolRecipeSelectionKinds.LandmarkCorrespondenceSet => "correspondences",
                _ => "selection"
            };
        var stepId = step.Id.StartsWith("step.", StringComparison.OrdinalIgnoreCase)
            ? step.Id[5..]
            : step.Id;
        var baseId = $"selection.{NormalizeId(stepId)}.{suffix}";
        if (!isAdditionalLevelSurfaceReference
            || !string.Equals(step.ToolId, "level-surface", StringComparison.Ordinal))
        {
            return baseId;
        }

        var existingIds = selections
            .Select(selection => selection.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var ordinal = 2;
        var candidate = $"{baseId}.{ordinal:D2}";
        while (existingIds.Contains(candidate))
        {
            ordinal++;
            candidate = $"{baseId}.{ordinal:D2}";
        }

        return candidate;
    }

    public static bool ValidateGridRectangle(
        ToolRecipeGridRectangle rectangle,
        ToolRecipeSelectionSourceBinding? binding,
        bool isCrossSectionDimensions,
        out string message)
    {
        if (binding is null || binding.GridWidth <= 0 || binding.GridHeight <= 0)
        {
            message = "The selected ROI has no current source-grid identity.";
            return false;
        }
        if (rectangle.Row < 0
            || rectangle.Column < 0
            || rectangle.RowCount <= 0
            || rectangle.ColumnCount <= 0)
        {
            message = "Row and column must be zero or greater; width and height must be greater than zero.";
            return false;
        }
        if ((long)rectangle.Row + rectangle.RowCount > binding.GridHeight
            || (long)rectangle.Column + rectangle.ColumnCount > binding.GridWidth)
        {
            message = $"ROI must stay inside rows 0..{binding.GridHeight - 1} and columns 0..{binding.GridWidth - 1}.";
            return false;
        }
        if (isCrossSectionDimensions
            && (rectangle.RowCount != 1 || rectangle.ColumnCount < 2))
        {
            message = "Cross-section Dimensions requires one row and at least two columns.";
            return false;
        }

        message = "Valid source-grid footprint. Apply remains explicit and does not run inspection.";
        return true;
    }

    public static bool ValidateGridCircle(
        ToolRecipeGridCircle circle,
        ToolRecipeSelectionSourceBinding? binding,
        out string message)
    {
        if (binding is null)
        {
            message = "The selected circle has no current source-grid identity.";
            return false;
        }

        var errors = ToolRecipeGridCircleGeometry.Validate(
            circle,
            binding.GridWidth,
            binding.GridHeight);
        if (errors.Count > 0)
        {
            message = string.Join(" ", errors.Select(error => $"{error}."));
            return false;
        }

        message = "Valid circular source-grid footprint. Apply remains explicit and does not run inspection.";
        return true;
    }

    public static bool ValidateGridPolygon(
        ToolRecipeGridPolygon polygon,
        ToolRecipeSelectionSourceBinding? binding,
        out string message)
    {
        if (binding is null)
        {
            message = "The selected polygon has no current source-grid identity.";
            return false;
        }

        var errors = ToolRecipeGridPolygonGeometry.Validate(
            polygon,
            binding.GridWidth,
            binding.GridHeight);
        if (errors.Count > 0)
        {
            message = string.Join(" ", errors.Select(error => $"{error}."));
            return false;
        }

        message = "Valid ordered source-grid polygon. Apply remains explicit and does not run inspection.";
        return true;
    }

    public static string FormatSelection(ToolRecipeSelection selection)
    {
        var geometry = selection.GridRectangle is { } rectangle
            ? $"row {rectangle.Row}..{rectangle.Row + rectangle.RowCount - 1}, column {rectangle.Column}..{rectangle.Column + rectangle.ColumnCount - 1}"
            : selection.GridCircle is { } circle
                ? $"center row {circle.CenterRow}, column {circle.CenterColumn}, radius {circle.Radius:G6} cells"
                : selection.GridPolygon is { Vertices: { } vertices }
                    ? $"{vertices.Count} ordered vertices ({FormatGridPolygonVertex(vertices.FirstOrDefault())} → {FormatGridPolygonVertex(vertices.LastOrDefault())})"
                    : selection.Points is { } points
                        ? $"{points.Count} grid point(s)"
                        : selection.Rows is { } rows
                            ? $"{rows.Count} correspondence row(s)"
                            : "geometry unavailable";
        var hash = selection.SourceBinding.ContentSha256.Length >= 8
            ? selection.SourceBinding.ContentSha256[..8]
            : selection.SourceBinding.ContentSha256;
        return $"{selection.Name} | {geometry} | {selection.FrameId} | sha256 {hash}";
    }

    public static string FormatSelectionGeometryForLog(ToolRecipeSelection selection) =>
        selection.GridPolygon is { Vertices: { } vertices }
            ? $"polygonVertices={vertices.Count};first={FormatGridPolygonVertex(vertices.FirstOrDefault())};last={FormatGridPolygonVertex(vertices.LastOrDefault())}"
            : selection.GridCircle is { } circle
                ? $"circleCenter=({circle.CenterRow},{circle.CenterColumn});radius={circle.Radius:G6}"
                : $"rectangle={FormatGridRectangleForLog(selection.GridRectangle)}";

    public static string GetAppliedRoleName(
        ToolWorkbenchPipelineStepItem step,
        string selectionId)
    {
        if (step.DualRoiRouting is { } routing)
        {
            if (string.Equals(
                routing.FirstRegionSelectionId,
                selectionId,
                StringComparison.OrdinalIgnoreCase))
            {
                return "reference";
            }
            if (string.Equals(
                routing.SecondRegionSelectionId,
                selectionId,
                StringComparison.OrdinalIgnoreCase))
            {
                return "measurement";
            }
        }

        var inputIndex = step.InputEntityIds
            .Select((id, index) => (id, index))
            .FirstOrDefault(item => string.Equals(
                item.id,
                selectionId,
                StringComparison.OrdinalIgnoreCase))
            .index;
        return inputIndex == 1 ? "reference" : inputIndex == 2 ? "measurement" : "selection";
    }

    public static bool IsMeasurementRoleSelection(
        ToolWorkbenchPipelineStepItem step,
        string selectionId,
        bool isThickness,
        string recipeSchemaVersion)
    {
        if (step.DualRoiRouting is { } routing)
        {
            return string.Equals(
                routing.SecondRegionSelectionId,
                selectionId,
                StringComparison.OrdinalIgnoreCase);
        }

        var inputIndex = step.InputEntityIds
            .Select((id, index) => (id, index))
            .FirstOrDefault(item => string.Equals(
                item.id,
                selectionId,
                StringComparison.OrdinalIgnoreCase))
            .index;
        return inputIndex == 2
            || (isThickness
                && step.InputEntityIds.Count == 2
                && !ToolRecipeDocument.SupportsArtifactOwnedSelections(recipeSchemaVersion)
                && inputIndex == 1);
    }

    private static string FormatGridPolygonVertex(ToolRecipeGridPolygonVertex? vertex) =>
        vertex is null ? "(none)" : $"X {vertex.Column:G6}, Z {vertex.Row:G6}";

    private static string FormatGridRectangleForLog(ToolRecipeGridRectangle? rectangle) =>
        rectangle is null
            ? "(none)"
            : $"row={rectangle.Row},column={rectangle.Column},rowCount={rectangle.RowCount},columnCount={rectangle.ColumnCount}";

    private static string NormalizeId(string? value)
    {
        var characters = (value ?? string.Empty)
            .Trim()
            .ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : '-')
            .ToArray();
        var normalized = new string(characters).Trim('-');
        while (normalized.Contains("--", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("--", "-", StringComparison.Ordinal);
        }

        return string.IsNullOrWhiteSpace(normalized) ? "entity" : normalized;
    }
}
