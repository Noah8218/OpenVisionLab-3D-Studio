using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Tools;

/// <summary>
/// Deterministic boundary/crease extraction from immutable SurfaceModel
/// topology. Non-manifold topology is rejected instead of repaired.
/// </summary>
public static class ModelSurfaceEdgeExtractor
{
    public static ModelSurfaceEdgeArtifact Extract(
        SurfaceModelArtifact model,
        ModelSurfaceEdgeExtractionParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(parameters);
        var validity = SurfaceModelArtifactValidator.Inspect(model);
        if (!validity.IsValid)
        {
            throw new InvalidDataException(
                "Model edge extraction requires a valid SurfaceModel.");
        }

        if (parameters.Method
                != ModelSurfaceEdgeExtractionParameters
                    .TopologyBoundaryAndCreaseMethod
            || !double.IsFinite(parameters.MinimumEdgeLength)
            || parameters.MinimumEdgeLength <= 0.0
            || !double.IsFinite(parameters.MinimumCreaseAngleDegrees)
            || parameters.MinimumCreaseAngleDegrees < 0.0
            || parameters.MinimumCreaseAngleDegrees > 180.0)
        {
            throw new InvalidDataException(
                "Model edge extraction parameters are invalid.");
        }

        var owners = new Dictionary<(int First, int Second), List<int>>();
        for (var triangleIndex = 0;
             triangleIndex < model.Triangles.Length;
             triangleIndex++)
        {
            var triangle = model.Triangles[triangleIndex];
            AddOwner(owners, triangle.FirstPointIndex, triangle.SecondPointIndex, triangleIndex);
            AddOwner(owners, triangle.SecondPointIndex, triangle.ThirdPointIndex, triangleIndex);
            AddOwner(owners, triangle.ThirdPointIndex, triangle.FirstPointIndex, triangleIndex);
        }

        if (owners.Any(owner => owner.Value.Count > 2))
        {
            throw new InvalidDataException(
                "Model edge extraction rejects non-manifold edges owned by more than two triangles.");
        }

        var extracted = new List<ModelSurfaceEdgeSample>();
        foreach (var owner in owners.OrderBy(pair => pair.Key.First)
                     .ThenBy(pair => pair.Key.Second))
        {
            var first = model.Points[owner.Key.First];
            var second = model.Points[owner.Key.Second];
            var length = Distance(first, second);
            if (length < parameters.MinimumEdgeLength)
            {
                continue;
            }

            ModelSurfaceEdgeKind kind;
            double strengthDegrees;
            if (owner.Value.Count == 1)
            {
                if (!parameters.IncludeBoundaryEdges)
                {
                    continue;
                }

                kind = ModelSurfaceEdgeKind.Boundary;
                strengthDegrees = 180.0;
            }
            else
            {
                var firstNormal = TriangleNormal(model, owner.Value[0]);
                var secondNormal = TriangleNormal(model, owner.Value[1]);
                strengthDegrees = Math.Acos(Math.Clamp(
                    Dot(firstNormal, secondNormal),
                    -1.0,
                    1.0)) * 180.0 / Math.PI;
                if (strengthDegrees < parameters.MinimumCreaseAngleDegrees)
                {
                    continue;
                }

                kind = ModelSurfaceEdgeKind.Crease;
            }

            extracted.Add(new ModelSurfaceEdgeSample(
                extracted.Count,
                owner.Key.First,
                owner.Key.Second,
                first,
                second,
                Midpoint(first, second),
                length,
                strengthDegrees,
                kind));
        }

        return ModelSurfaceEdgeArtifact.Create(model, parameters, extracted);
    }

    private static void AddOwner(
        IDictionary<(int First, int Second), List<int>> owners,
        int first,
        int second,
        int triangleIndex)
    {
        var pair = first < second ? (first, second) : (second, first);
        if (!owners.TryGetValue(pair, out var triangleIndices))
        {
            triangleIndices = [];
            owners.Add(pair, triangleIndices);
        }

        triangleIndices.Add(triangleIndex);
    }

    private static SurfaceModelPoint3 TriangleNormal(
        SurfaceModelArtifact model,
        int triangleIndex)
    {
        var triangle = model.Triangles[triangleIndex];
        var first = model.Points[triangle.FirstPointIndex];
        var second = model.Points[triangle.SecondPointIndex];
        var third = model.Points[triangle.ThirdPointIndex];
        var ab = Subtract(second, first);
        var ac = Subtract(third, first);
        var cross = new SurfaceModelPoint3(
            ab.Y * ac.Z - ab.Z * ac.Y,
            ab.Z * ac.X - ab.X * ac.Z,
            ab.X * ac.Y - ab.Y * ac.X);
        var length = Math.Sqrt(Dot(cross, cross));
        if (!double.IsFinite(length) || length <= 0.0)
        {
            throw new InvalidDataException(
                "Model edge extraction encountered a degenerate triangle.");
        }

        return new SurfaceModelPoint3(
            cross.X / length,
            cross.Y / length,
            cross.Z / length);
    }

    private static SurfaceModelPoint3 Subtract(
        SurfaceModelPoint3 first,
        SurfaceModelPoint3 second) =>
        new(first.X - second.X, first.Y - second.Y, first.Z - second.Z);

    private static double Dot(
        SurfaceModelPoint3 first,
        SurfaceModelPoint3 second) =>
        first.X * second.X + first.Y * second.Y + first.Z * second.Z;

    private static double Distance(
        SurfaceModelPoint3 first,
        SurfaceModelPoint3 second)
    {
        var difference = Subtract(first, second);
        return Math.Sqrt(Dot(difference, difference));
    }

    private static SurfaceModelPoint3 Midpoint(
        SurfaceModelPoint3 first,
        SurfaceModelPoint3 second) =>
        new(
            (first.X + second.X) * 0.5,
            (first.Y + second.Y) * 0.5,
            (first.Z + second.Z) * 0.5);
}
