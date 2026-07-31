namespace OpenVisionLab.ThreeD.Core;

public sealed record SurfaceEdgeArtifactValidityReport(
    string SchemaVersion,
    bool IsValid,
    int ItemCount,
    int ValidItemCount,
    bool ContentIdentityValid,
    IReadOnlyList<string> Errors,
    string Evidence)
{
    public const string CurrentSchemaVersion = "1.0";
}

/// <summary>
/// Fail-closed validation for identified model/scene edge artifacts and the
/// separate surface/edge diagnostic score. Validation never repairs
/// topology, guesses organized-grid adjacency, or creates acceptance policy.
/// </summary>
public static class SurfaceEdgeArtifactValidator
{
    private const double Tolerance = 1e-10;

    public static SurfaceEdgeArtifactValidityReport Inspect(
        ModelSurfaceEdgeArtifact artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        var errors = new List<string>();
        var validEdgeCount = 0;
        var edges = artifact.Edges ?? [];
        ValidateHeader(
            artifact.SchemaVersion,
            ModelSurfaceEdgeArtifact.CurrentSchemaVersion,
            artifact.Semantics,
            ModelSurfaceEdgeArtifact.CurrentSemantics,
            artifact.ArtifactId,
            artifact.ModelContentSha256,
            artifact.Unit,
            artifact.FrameId,
            "model edge",
            errors);

        var parameters = artifact.Parameters;
        if (parameters is null
            || parameters.Method
                != ModelSurfaceEdgeExtractionParameters
                    .TopologyBoundaryAndCreaseMethod
            || !FinitePositive(parameters.MinimumEdgeLength)
            || !double.IsFinite(parameters.MinimumCreaseAngleDegrees)
            || parameters.MinimumCreaseAngleDegrees < 0.0
            || parameters.MinimumCreaseAngleDegrees > 180.0)
        {
            errors.Add("Model edge extraction parameters are invalid.");
        }

        if (artifact.SourcePointCount <= 0
            || artifact.SourceTriangleCount <= 0)
        {
            errors.Add("Model edge source counts must be positive.");
        }

        var pairs = new HashSet<(int First, int Second)>();
        (int First, int Second)? previous = null;
        for (var index = 0; index < edges.Length; index++)
        {
            var edge = edges[index];
            if (edge is null)
            {
                errors.Add($"Model edge {index} is missing.");
                continue;
            }

            var edgeValid = true;

            if (edge.Order != index)
            {
                errors.Add($"Model edge {index} has a non-canonical order.");
                edgeValid = false;
            }

            var pair = (
                First: edge.FirstPointIndex,
                Second: edge.SecondPointIndex);
            if (pair.First < 0
                || pair.Second <= pair.First
                || pair.Second >= artifact.SourcePointCount)
            {
                errors.Add($"Model edge {index} has invalid point indices.");
                edgeValid = false;
            }
            else
            {
                if (!pairs.Add(pair))
                {
                    errors.Add($"Model edge {index} duplicates a topology pair.");
                    edgeValid = false;
                }

                if (previous is { } prior
                    && Compare(prior, pair) >= 0)
                {
                    errors.Add($"Model edge {index} is not in stable pair order.");
                    edgeValid = false;
                }

                previous = pair;
            }

            if (!Finite(edge.FirstPosition)
                || !Finite(edge.SecondPosition)
                || !Finite(edge.Anchor)
                || !FinitePositive(edge.Length)
                || parameters is not null
                    && edge.Length + Tolerance
                        < parameters.MinimumEdgeLength
                || !Approximately(
                    edge.Length,
                    Distance(edge.FirstPosition, edge.SecondPosition))
                || !Approximately(
                    edge.Anchor,
                    Midpoint(edge.FirstPosition, edge.SecondPosition)))
            {
                errors.Add($"Model edge {index} geometry is invalid.");
                edgeValid = false;
            }

            if (!Enum.IsDefined(edge.Kind)
                || !double.IsFinite(edge.StrengthDegrees)
                || edge.StrengthDegrees < 0.0
                || edge.StrengthDegrees > 180.0)
            {
                errors.Add($"Model edge {index} classification is invalid.");
                edgeValid = false;
            }
            else if (edge.Kind == ModelSurfaceEdgeKind.Boundary
                     && (parameters is null
                         || !parameters.IncludeBoundaryEdges
                         || !Approximately(edge.StrengthDegrees, 180.0)))
            {
                errors.Add($"Model boundary edge {index} is inconsistent.");
                edgeValid = false;
            }
            else if (edge.Kind == ModelSurfaceEdgeKind.Crease
                     && parameters is not null
                     && edge.StrengthDegrees + Tolerance
                        < parameters.MinimumCreaseAngleDegrees)
            {
                errors.Add($"Model crease edge {index} is below the declared threshold.");
                edgeValid = false;
            }

            if (edgeValid)
            {
                validEdgeCount++;
            }
        }

        var identityValid = IdentityEquals(
            artifact.ContentSha256,
            () => ModelSurfaceEdgeArtifact.CalculateContentSha256(artifact));
        if (!identityValid)
        {
            errors.Add("Model edge content identity is invalid.");
        }

        return Report(
            edges.Length,
            validEdgeCount,
            identityValid,
            errors,
            $"kind=model;edges={validEdgeCount}/{edges.Length}");
    }

    public static SurfaceEdgeArtifactValidityReport Inspect(
        SceneSurfaceEdgeArtifact artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        var errors = new List<string>();
        var validEdgeCount = 0;
        var edges = artifact.Edges ?? [];
        ValidateHeader(
            artifact.SchemaVersion,
            SceneSurfaceEdgeArtifact.CurrentSchemaVersion,
            artifact.Semantics,
            SceneSurfaceEdgeArtifact.CurrentSemantics,
            artifact.ArtifactId,
            artifact.SceneContentSha256,
            artifact.Unit,
            artifact.FrameId,
            "scene edge",
            errors);

        var parameters = artifact.Parameters;
        if (parameters is null
            || parameters.Method
                != SceneSurfaceEdgeExtractionParameters
                    .OrganizedHeightStepMethod
            || !FinitePositive(parameters.MinimumAbsoluteHeightStep)
            || !parameters.IncludeColumnNeighbors
                && !parameters.IncludeRowNeighbors)
        {
            errors.Add("Scene edge extraction parameters are invalid.");
        }

        var expectedPointCount = 0L;
        try
        {
            expectedPointCount = checked(
                (long)artifact.SourceWidth * artifact.SourceHeight);
        }
        catch (OverflowException)
        {
            errors.Add("Scene edge grid dimensions overflow.");
        }

        if (artifact.SourceWidth <= 0
            || artifact.SourceHeight <= 0
            || artifact.SourcePointCount <= 0
            || expectedPointCount != artifact.SourcePointCount)
        {
            errors.Add("Scene edge source must be one complete organized grid.");
        }

        var pairs = new HashSet<(int First, int Second)>();
        (int First, int Second)? previous = null;
        for (var index = 0; index < edges.Length; index++)
        {
            var edge = edges[index];
            if (edge is null)
            {
                errors.Add($"Scene edge {index} is missing.");
                continue;
            }

            var edgeValid = true;

            if (edge.Order != index)
            {
                errors.Add($"Scene edge {index} has a non-canonical order.");
                edgeValid = false;
            }

            var pair = (
                First: edge.FirstPointIndex,
                Second: edge.SecondPointIndex);
            if (pair.First < 0
                || pair.Second <= pair.First
                || pair.Second >= artifact.SourcePointCount)
            {
                errors.Add($"Scene edge {index} has invalid point indices.");
                edgeValid = false;
            }
            else
            {
                if (!pairs.Add(pair))
                {
                    errors.Add($"Scene edge {index} duplicates a grid pair.");
                    edgeValid = false;
                }

                if (previous is { } prior
                    && Compare(prior, pair) >= 0)
                {
                    errors.Add($"Scene edge {index} is not in stable pair order.");
                    edgeValid = false;
                }

                previous = pair;
            }

            var adjacencyValid = edge.Axis switch
            {
                SceneSurfaceEdgeAxis.AcrossColumns =>
                    parameters?.IncludeColumnNeighbors == true
                    && artifact.SourceWidth > 0
                    && pair.Second == pair.First + 1
                    && pair.First / artifact.SourceWidth
                        == pair.Second / artifact.SourceWidth,
                SceneSurfaceEdgeAxis.AcrossRows =>
                    parameters?.IncludeRowNeighbors == true
                    && artifact.SourceWidth > 0
                    && pair.Second == pair.First + artifact.SourceWidth,
                _ => false
            };
            if (!adjacencyValid)
            {
                errors.Add($"Scene edge {index} does not identify adjacent grid cells.");
                edgeValid = false;
            }

            if (!Finite(edge.FirstPosition)
                || !Finite(edge.SecondPosition)
                || !Finite(edge.Anchor))
            {
                errors.Add($"Scene edge {index} geometry is invalid.");
                continue;
            }

            var expectedStep = Math.Abs(
                edge.FirstPosition.Z - edge.SecondPosition.Z);
            var expectedAnchorIndex = edge.FirstPosition.Z
                > edge.SecondPosition.Z
                ? pair.First
                : pair.Second;
            var expectedAnchor = expectedAnchorIndex == pair.First
                ? edge.FirstPosition
                : edge.SecondPosition;
            if (!FinitePositive(edge.AbsoluteHeightStep)
                || parameters is not null
                    && edge.AbsoluteHeightStep + Tolerance
                        < parameters.MinimumAbsoluteHeightStep
                || !Approximately(edge.AbsoluteHeightStep, expectedStep)
                || edge.AnchorPointIndex != expectedAnchorIndex
                || !Approximately(edge.Anchor, expectedAnchor))
            {
                errors.Add($"Scene edge {index} geometry is invalid.");
                edgeValid = false;
            }

            if (edgeValid)
            {
                validEdgeCount++;
            }
        }

        var identityValid = IdentityEquals(
            artifact.ContentSha256,
            () => SceneSurfaceEdgeArtifact.CalculateContentSha256(artifact));
        if (!identityValid)
        {
            errors.Add("Scene edge content identity is invalid.");
        }

        return Report(
            edges.Length,
            validEdgeCount,
            identityValid,
            errors,
            $"kind=scene;edges={validEdgeCount}/{edges.Length};"
            + $"grid={artifact.SourceWidth}x{artifact.SourceHeight}");
    }

    public static SurfaceEdgeArtifactValidityReport Inspect(
        SurfaceAndEdgeMatchScoreArtifact artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        var errors = new List<string>();
        var surface = artifact.SurfaceScore;
        var edge = artifact.EdgeScore;
        if (artifact.SchemaVersion
                != SurfaceAndEdgeMatchScoreArtifact.CurrentSchemaVersion
            || artifact.Semantics
                != SurfaceAndEdgeMatchScoreArtifact.CurrentSemantics)
        {
            errors.Add("Surface/edge score schema or semantics are unsupported.");
        }

        if (!CanonicalSha(artifact.SurfaceMatchExecutionContentSha256)
            || !CanonicalSha(artifact.ModelEdgeContentSha256)
            || !CanonicalSha(artifact.SceneEdgeContentSha256))
        {
            errors.Add("Surface/edge score input identities are invalid.");
        }

        if (surface is null
            || surface.Semantics != SurfaceCoverageEvaluation.CurrentSemantics
            || !CanonicalSha(surface.PoseResultContentSha256)
            || surface.ModelSampleCount <= 0
            || surface.SceneSampleCount <= 0
            || surface.MatchedModelSampleCount < 0
            || surface.MatchedModelSampleCount > surface.ModelSampleCount
            || !Ratio(
                surface.CoverageRatio,
                surface.MatchedModelSampleCount,
                surface.ModelSampleCount)
            || !OptionalNonNegative(surface.InlierRmse)
            || (surface.MatchedModelSampleCount == 0)
                != !surface.InlierRmse.HasValue
            || !FinitePositive(surface.MaximumCorrespondenceDistance))
        {
            errors.Add("Surface score component is invalid.");
        }

        var matches = edge?.Matches ?? [];
        var validMatchCount = 0;
        if (edge is null
            || edge.Semantics != SurfaceEdgeScoreComponent.CurrentSemantics
            || edge.ModelEdgeCount <= 0
            || edge.SceneEdgeCount < 0
            || edge.MatchedModelEdgeCount < 0
            || edge.MatchedModelEdgeCount > edge.ModelEdgeCount
            || edge.UnmatchedModelEdgeCount
                != edge.ModelEdgeCount - edge.MatchedModelEdgeCount
            || matches.Length != edge.MatchedModelEdgeCount
            || !Ratio(
                edge.CoverageRatio,
                edge.MatchedModelEdgeCount,
                edge.ModelEdgeCount)
            || !OptionalNonNegative(edge.InlierRmse)
            || edge.MatchedModelEdgeCount == 0
                != !edge.InlierRmse.HasValue
            || !FinitePositive(edge.MaximumCorrespondenceDistance)
            || string.IsNullOrWhiteSpace(edge.Evidence))
        {
            errors.Add("Edge score component is invalid.");
        }
        else
        {
            var modelOrders = new HashSet<int>();
            var sceneOrders = new HashSet<int>();
            var squaredErrorSum = 0.0;
            for (var index = 0; index < matches.Length; index++)
            {
                var match = matches[index];
                if (match is null)
                {
                    errors.Add($"Edge score match {index} is invalid.");
                    continue;
                }

                var matchValid = match.ModelEdgeOrder >= 0
                    && match.ModelEdgeOrder < edge.ModelEdgeCount
                    && match.SceneEdgeOrder >= 0
                    && match.SceneEdgeOrder < edge.SceneEdgeCount
                    && modelOrders.Add(match.ModelEdgeOrder)
                    && sceneOrders.Add(match.SceneEdgeOrder)
                    && double.IsFinite(match.Distance)
                    && match.Distance >= 0.0
                    && match.Distance
                        <= edge.MaximumCorrespondenceDistance + Tolerance;
                if (!matchValid)
                {
                    errors.Add($"Edge score match {index} is invalid.");
                    continue;
                }

                validMatchCount++;
                squaredErrorSum += match.Distance * match.Distance;
            }

            var expectedRmse = matches.Length == 0
                ? (double?)null
                : Math.Sqrt(squaredErrorSum / matches.Length);
            if (!NullableApproximately(edge.InlierRmse, expectedRmse))
            {
                errors.Add("Edge score RMSE is inconsistent with its matches.");
            }
        }

        var identityValid = IdentityEquals(
            artifact.ContentSha256,
            () => SurfaceAndEdgeMatchScoreArtifact
                .CalculateContentSha256(artifact));
        if (!identityValid)
        {
            errors.Add("Surface/edge score content identity is invalid.");
        }

        return Report(
            matches.Length,
            validMatchCount,
            identityValid,
            errors,
            $"kind=score;edgeMatches={validMatchCount}/{matches.Length}");
    }

    public static SurfaceEdgeArtifactValidityReport Inspect(
        SurfaceAndEdgeMatchScoreArtifact artifact,
        SurfaceMatchExecutionArtifact execution)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        ArgumentNullException.ThrowIfNull(execution);
        var standalone = Inspect(artifact);
        var errors = standalone.Errors.ToList();
        var executionValidity =
            SurfaceMatchExecutionArtifactValidator.Inspect(execution);
        var coverage = execution.PoseResult.Coverage;
        var surface = artifact.SurfaceScore;
        if (!executionValidity.IsValid
            || artifact.SurfaceMatchExecutionContentSha256
                != execution.ContentSha256
            || surface is null
            || surface.PoseResultContentSha256
                != execution.PoseResult.ContentSha256
            || surface.Semantics != coverage.Semantics
            || surface.ModelSampleCount != coverage.ModelSampleCount
            || surface.SceneSampleCount != coverage.SceneSampleCount
            || surface.MatchedModelSampleCount
                != coverage.MatchedModelSampleCount
            || !Approximately(
                surface.CoverageRatio,
                coverage.CoverageRatio)
            || !NullableApproximately(
                surface.InlierRmse,
                coverage.InlierRmse)
            || !Approximately(
                surface.MaximumCorrespondenceDistance,
                coverage.MaximumCorrespondenceDistance))
        {
            errors.Add(
                "Surface score component does not match its identified execution.");
        }

        return new SurfaceEdgeArtifactValidityReport(
            SurfaceEdgeArtifactValidityReport.CurrentSchemaVersion,
            errors.Count == 0,
            standalone.ItemCount,
            standalone.ValidItemCount,
            standalone.ContentIdentityValid,
            errors.AsReadOnly(),
            standalone.Evidence
            + $";executionIdentity={execution.ContentSha256}"
            + $";executionLinked={errors.Count == standalone.Errors.Count}");
    }

    private static void ValidateHeader(
        string? schemaVersion,
        string expectedSchemaVersion,
        string? semantics,
        string expectedSemantics,
        string? artifactId,
        string? sourceIdentity,
        string? unit,
        string? frameId,
        string name,
        ICollection<string> errors)
    {
        if (schemaVersion != expectedSchemaVersion
            || semantics != expectedSemantics)
        {
            errors.Add($"{name} schema or semantics are unsupported.");
        }

        if (string.IsNullOrWhiteSpace(artifactId)
            || string.IsNullOrWhiteSpace(unit)
            || string.IsNullOrWhiteSpace(frameId)
            || !CanonicalSha(sourceIdentity))
        {
            errors.Add($"{name} identity context is invalid.");
        }
    }

    private static SurfaceEdgeArtifactValidityReport Report(
        int itemCount,
        int validItemCount,
        bool identityValid,
        List<string> errors,
        string evidence) =>
        new(
            SurfaceEdgeArtifactValidityReport.CurrentSchemaVersion,
            errors.Count == 0,
            itemCount,
            validItemCount,
            identityValid,
            errors.AsReadOnly(),
            $"{evidence};identity={identityValid}");

    private static int Compare(
        (int First, int Second) first,
        (int First, int Second) second) =>
        first.First != second.First
            ? first.First.CompareTo(second.First)
            : first.Second.CompareTo(second.Second);

    private static bool IdentityEquals(
        string? identity,
        Func<string> calculate)
    {
        if (!CanonicalSha(identity))
        {
            return false;
        }

        try
        {
            return string.Equals(
                identity,
                calculate(),
                StringComparison.Ordinal);
        }
        catch (Exception exception)
            when (exception is ArgumentException
                  or InvalidOperationException
                  or NullReferenceException
                  or OverflowException)
        {
            return false;
        }
    }

    private static bool CanonicalSha(string? value) =>
        value is not null
        && value.Length == 64
        && value.All(character =>
            character is >= '0' and <= '9'
            or >= 'A' and <= 'F');

    private static bool FinitePositive(double value) =>
        double.IsFinite(value) && value > 0.0;

    private static bool OptionalNonNegative(double? value) =>
        !value.HasValue
        || double.IsFinite(value.Value) && value.Value >= 0.0;

    private static bool Finite(SurfaceModelPoint3? point) =>
        point is not null
        && double.IsFinite(point.X)
        && double.IsFinite(point.Y)
        && double.IsFinite(point.Z);

    private static SurfaceModelPoint3 Midpoint(
        SurfaceModelPoint3 first,
        SurfaceModelPoint3 second) =>
        new(
            (first.X + second.X) * 0.5,
            (first.Y + second.Y) * 0.5,
            (first.Z + second.Z) * 0.5);

    private static double Distance(
        SurfaceModelPoint3 first,
        SurfaceModelPoint3 second)
    {
        var x = first.X - second.X;
        var y = first.Y - second.Y;
        var z = first.Z - second.Z;
        return Math.Sqrt(x * x + y * y + z * z);
    }

    private static bool Approximately(double first, double second) =>
        double.IsFinite(first)
        && double.IsFinite(second)
        && Math.Abs(first - second)
            <= Tolerance * Math.Max(1.0, Math.Max(Math.Abs(first), Math.Abs(second)));

    private static bool Approximately(
        SurfaceModelPoint3 first,
        SurfaceModelPoint3 second) =>
        Approximately(first.X, second.X)
        && Approximately(first.Y, second.Y)
        && Approximately(first.Z, second.Z);

    private static bool NullableApproximately(double? first, double? second) =>
        first.HasValue == second.HasValue
        && (!first.HasValue || Approximately(first.Value, second!.Value));

    private static bool Ratio(double ratio, int numerator, int denominator) =>
        double.IsFinite(ratio)
        && denominator > 0
        && Approximately(ratio, numerator / (double)denominator);
}
