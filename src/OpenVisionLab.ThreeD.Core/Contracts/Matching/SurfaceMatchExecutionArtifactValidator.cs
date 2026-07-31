namespace OpenVisionLab.ThreeD.Core;

public sealed record SurfaceMatchExecutionValidityReport(
    string SchemaVersion,
    bool IsValid,
    bool PoseIdentityValid,
    bool OverlayIdentityValid,
    bool ExecutionIdentityValid,
    IReadOnlyList<string> Errors,
    string Evidence)
{
    public const string CurrentSchemaVersion = "1.0";
}

/// <summary>
/// Fail-closed validation for the linked pose, coverage, overlay, and
/// execution identities. Validation never repairs geometry or invents a
/// display overlay for a NoMatch result.
/// </summary>
public static class SurfaceMatchExecutionArtifactValidator
{
    public static SurfaceMatchExecutionValidityReport Inspect(
        SurfaceMatchExecutionArtifact artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        var errors = new List<string>();
        var poseIdentityValid = false;
        var overlayIdentityValid = artifact.Overlay is null;
        var executionIdentityValid = false;

        if (artifact.SchemaVersion
            != SurfaceMatchExecutionArtifact.CurrentSchemaVersion)
        {
            errors.Add(
                $"Unsupported surface match execution schema '{artifact.SchemaVersion}'.");
        }

        if (artifact.Semantics
            != SurfaceMatchExecutionArtifact.CurrentSemantics)
        {
            errors.Add("Surface match execution semantics are unsupported.");
        }

        if (!IsCanonicalSha256(artifact.ModelContentSha256)
            || !IsCanonicalSha256(artifact.SceneContentSha256))
        {
            errors.Add(
                "Surface match execution requires canonical model and scene SHA-256 identities.");
        }

        if (artifact.PoseResult is null)
        {
            errors.Add("Surface match pose result is missing.");
        }
        else
        {
            try
            {
                poseIdentityValid = string.Equals(
                    artifact.PoseResult.ContentSha256,
                    RigidSurfacePoseSearchResult.CalculateContentSha256(
                        artifact.PoseResult),
                    StringComparison.Ordinal);
            }
            catch
            {
                poseIdentityValid = false;
            }

            if (!poseIdentityValid)
            {
                errors.Add("Surface match pose result identity is invalid.");
            }

            if (!string.Equals(
                    artifact.ModelContentSha256,
                    artifact.PoseResult.ModelContentSha256,
                    StringComparison.Ordinal)
                || !string.Equals(
                    artifact.SceneContentSha256,
                    artifact.PoseResult.SceneContentSha256,
                    StringComparison.Ordinal))
            {
                errors.Add(
                    "Surface match execution and pose result identities do not agree.");
            }

            if (artifact.PoseResult.State
                    == RigidSurfacePoseSearchState.Matched
                && artifact.Overlay is null)
            {
                errors.Add(
                    "Matched surface match execution requires an identified overlay.");
            }

            if (artifact.PoseResult.State
                    == RigidSurfacePoseSearchState.NoMatch
                && artifact.Overlay is not null)
            {
                errors.Add(
                    "NoMatch surface match execution must not contain an overlay.");
            }
        }

        if (artifact.Overlay is { } overlay)
        {
            overlayIdentityValid =
                InspectOverlay(
                    artifact,
                    overlay,
                    errors);
        }

        try
        {
            executionIdentityValid = string.Equals(
                artifact.ContentSha256,
                SurfaceMatchExecutionArtifact.CalculateContentSha256(
                    artifact),
                StringComparison.Ordinal);
        }
        catch
        {
            executionIdentityValid = false;
        }

        if (!executionIdentityValid)
        {
            errors.Add(
                "Surface match execution content identity is invalid.");
        }

        var evidence =
            $"poseIdentity={poseIdentityValid};"
            + $"overlayIdentity={overlayIdentityValid};"
            + $"executionIdentity={executionIdentityValid};"
            + $"state={artifact.PoseResult?.State};"
            + $"overlay={(artifact.Overlay is null ? "none" : artifact.Overlay.OverlayId)};"
            + "acceptancePolicy=none";
        return new SurfaceMatchExecutionValidityReport(
            SurfaceMatchExecutionValidityReport.CurrentSchemaVersion,
            errors.Count == 0,
            poseIdentityValid,
            overlayIdentityValid,
            executionIdentityValid,
            errors,
            evidence);
    }

    private static bool InspectOverlay(
        SurfaceMatchExecutionArtifact execution,
        SurfaceMatchOverlayArtifact overlay,
        List<string> errors)
    {
        var valid = true;
        if (overlay.SchemaVersion
                != SurfaceMatchOverlayArtifact.CurrentSchemaVersion
            || overlay.Semantics
                != SurfaceMatchOverlayArtifact.CurrentSemantics)
        {
            errors.Add("Surface match overlay schema or semantics are unsupported.");
            valid = false;
        }

        if (string.IsNullOrWhiteSpace(overlay.OverlayId)
            || string.IsNullOrWhiteSpace(overlay.Unit)
            || string.IsNullOrWhiteSpace(overlay.SourceFrameId)
            || string.IsNullOrWhiteSpace(overlay.TargetFrameId))
        {
            errors.Add(
                "Surface match overlay requires an ID, unit, and explicit frames.");
            valid = false;
        }

        if (!string.Equals(
                overlay.ModelContentSha256,
                execution.ModelContentSha256,
                StringComparison.Ordinal)
            || !string.Equals(
                overlay.SceneContentSha256,
                execution.SceneContentSha256,
                StringComparison.Ordinal)
            || !string.Equals(
                overlay.PoseResultContentSha256,
                execution.PoseResult.ContentSha256,
                StringComparison.Ordinal))
        {
            errors.Add(
                "Surface match overlay linkage does not agree with the execution.");
            valid = false;
        }

        var points = overlay.TransformedPoints ?? [];
        var triangles = overlay.Triangles ?? [];
        if (points.Length == 0
            || points.Any(point =>
                point is null
                || !double.IsFinite(point.X)
                || !double.IsFinite(point.Y)
                || !double.IsFinite(point.Z)))
        {
            errors.Add(
                "Surface match overlay requires finite transformed points.");
            valid = false;
        }

        if (triangles.Length == 0
            || triangles.Any(triangle =>
                triangle is null
                || triangle.FirstPointIndex < 0
                || triangle.SecondPointIndex < 0
                || triangle.ThirdPointIndex < 0
                || triangle.FirstPointIndex >= points.Length
                || triangle.SecondPointIndex >= points.Length
                || triangle.ThirdPointIndex >= points.Length
                || triangle.FirstPointIndex
                    == triangle.SecondPointIndex
                || triangle.FirstPointIndex
                    == triangle.ThirdPointIndex
                || triangle.SecondPointIndex
                    == triangle.ThirdPointIndex))
        {
            errors.Add(
                "Surface match overlay triangle indexes are invalid.");
            valid = false;
        }

        try
        {
            if (!string.Equals(
                    overlay.ContentSha256,
                    SurfaceMatchOverlayArtifact
                        .CalculateContentSha256(overlay),
                    StringComparison.Ordinal))
            {
                errors.Add(
                    "Surface match overlay content identity is invalid.");
                valid = false;
            }
        }
        catch
        {
            errors.Add(
                "Surface match overlay content identity could not be calculated.");
            valid = false;
        }

        return valid;
    }

    private static bool IsCanonicalSha256(string? value) =>
        value is { Length: 64 }
        && value.All(character =>
            character is >= '0' and <= '9'
            or >= 'A' and <= 'F');
}
