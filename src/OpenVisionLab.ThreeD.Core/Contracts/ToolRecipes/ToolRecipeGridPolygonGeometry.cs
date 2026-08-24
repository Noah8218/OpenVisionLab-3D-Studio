namespace OpenVisionLab.ThreeD.Core;

/// <summary>
/// Runtime-neutral validation for an ordered polygon in source-grid space.
/// The contract deliberately stops at the outline: no filled mask is created
/// here and no inspection algorithm is implied.
/// </summary>
public static class ToolRecipeGridPolygonGeometry
{
    public const int MinimumVertexCount = 3;
    public const int MaximumVertexCount = 256;

    public static IReadOnlyList<string> Validate(
        ToolRecipeGridPolygon? polygon,
        int gridWidth,
        int gridHeight)
    {
        var errors = new List<string>();
        if (polygon is null)
        {
            errors.Add("grid polygon payload is required");
            return errors;
        }

        var vertices = polygon.Vertices;
        if (vertices is null)
        {
            errors.Add("grid polygon vertices are required");
            return errors;
        }

        if (vertices.Count < MinimumVertexCount)
        {
            errors.Add($"grid polygon must contain at least {MinimumVertexCount} vertices");
            return errors;
        }
        if (vertices.Count > MaximumVertexCount)
        {
            errors.Add($"grid polygon cannot contain more than {MaximumVertexCount} vertices");
            return errors;
        }

        var uniqueVertices = new HashSet<(double Row, double Column)>();
        for (var index = 0; index < vertices.Count; index++)
        {
            var vertex = vertices[index];
            var label = $"grid polygon vertex {index + 1}";
            if (vertex is null)
            {
                errors.Add($"{label} is required");
                continue;
            }

            if (!double.IsFinite(vertex.Row) || !double.IsFinite(vertex.Column))
            {
                errors.Add($"{label} row and column must be finite");
                continue;
            }

            if (gridHeight > 0 && (vertex.Row < 0 || vertex.Row > gridHeight - 1))
            {
                errors.Add($"{label} row must stay inside 0..{gridHeight - 1}");
            }
            if (gridWidth > 0 && (vertex.Column < 0 || vertex.Column > gridWidth - 1))
            {
                errors.Add($"{label} column must stay inside 0..{gridWidth - 1}");
            }
            if (!uniqueVertices.Add((vertex.Row, vertex.Column)))
            {
                errors.Add($"grid polygon repeats vertex ({vertex.Row:G6}, {vertex.Column:G6})");
            }
        }

        if (errors.Count > 0)
        {
            return errors;
        }

        var signedAreaTwice = 0.0;
        for (var index = 0; index < vertices.Count; index++)
        {
            var current = vertices[index];
            var next = vertices[(index + 1) % vertices.Count];
            signedAreaTwice += current.Column * next.Row - next.Column * current.Row;
        }
        if (Math.Abs(signedAreaTwice) <= 1e-12)
        {
            errors.Add("grid polygon must enclose a non-zero area");
        }

        for (var firstEdge = 0; firstEdge < vertices.Count; firstEdge++)
        {
            var secondEdge = (firstEdge + 1) % vertices.Count;
            for (var otherEdge = firstEdge + 1; otherEdge < vertices.Count; otherEdge++)
            {
                var otherNextEdge = (otherEdge + 1) % vertices.Count;
                if (firstEdge == otherEdge
                    || secondEdge == otherEdge
                    || firstEdge == otherNextEdge)
                {
                    continue;
                }

                if (SegmentsIntersect(
                        vertices[firstEdge],
                        vertices[secondEdge],
                        vertices[otherEdge],
                        vertices[otherNextEdge]))
                {
                    errors.Add(
                        $"grid polygon edges {firstEdge + 1} and {otherEdge + 1} self-intersect or overlap");
                }
            }
        }

        return errors;
    }

    private static bool SegmentsIntersect(
        ToolRecipeGridPolygonVertex first,
        ToolRecipeGridPolygonVertex second,
        ToolRecipeGridPolygonVertex otherFirst,
        ToolRecipeGridPolygonVertex otherSecond)
    {
        var firstOrientation = Cross(first, second, otherFirst);
        var secondOrientation = Cross(first, second, otherSecond);
        var otherFirstOrientation = Cross(otherFirst, otherSecond, first);
        var otherSecondOrientation = Cross(otherFirst, otherSecond, second);
        const double epsilon = 1e-12;

        if (Math.Abs(firstOrientation) <= epsilon
            && IsOnSegment(first, second, otherFirst, epsilon))
        {
            return true;
        }
        if (Math.Abs(secondOrientation) <= epsilon
            && IsOnSegment(first, second, otherSecond, epsilon))
        {
            return true;
        }
        if (Math.Abs(otherFirstOrientation) <= epsilon
            && IsOnSegment(otherFirst, otherSecond, first, epsilon))
        {
            return true;
        }
        if (Math.Abs(otherSecondOrientation) <= epsilon
            && IsOnSegment(otherFirst, otherSecond, second, epsilon))
        {
            return true;
        }

        return ((firstOrientation > epsilon && secondOrientation < -epsilon)
                || (firstOrientation < -epsilon && secondOrientation > epsilon))
            && ((otherFirstOrientation > epsilon && otherSecondOrientation < -epsilon)
                || (otherFirstOrientation < -epsilon && otherSecondOrientation > epsilon));
    }

    private static double Cross(
        ToolRecipeGridPolygonVertex origin,
        ToolRecipeGridPolygonVertex first,
        ToolRecipeGridPolygonVertex second) =>
        (first.Column - origin.Column) * (second.Row - origin.Row)
        - (first.Row - origin.Row) * (second.Column - origin.Column);

    private static bool IsOnSegment(
        ToolRecipeGridPolygonVertex first,
        ToolRecipeGridPolygonVertex second,
        ToolRecipeGridPolygonVertex point,
        double epsilon) =>
        point.Row >= Math.Min(first.Row, second.Row) - epsilon
        && point.Row <= Math.Max(first.Row, second.Row) + epsilon
        && point.Column >= Math.Min(first.Column, second.Column) - epsilon
        && point.Column <= Math.Max(first.Column, second.Column) + epsilon;
}
