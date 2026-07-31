namespace OpenVisionLab.ThreeD.Core;

public sealed record PreparedSceneValidityReport(
    string SchemaVersion,
    PreparedSceneValidityState State,
    int PointCount,
    int FinitePointCount,
    int SampleCount,
    int ValidSampleCount,
    bool SourceQualityIdentityValid,
    bool ContentIdentityValid,
    IReadOnlyList<string> Errors,
    string Evidence)
{
    public const string CurrentSchemaVersion = "1.0";

    public bool IsValid => State == PreparedSceneValidityState.Valid;
}

public enum PreparedSceneValidityState
{
    Valid,
    Invalid
}

/// <summary>
/// Fail-closed validation for one Prepared Scene. Validation never repairs or
/// resamples scene evidence.
/// </summary>
public static class PreparedSceneArtifactValidator
{
    public static PreparedSceneValidityReport Inspect(
        PreparedSceneArtifact artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact);

        var errors = new List<string>();
        var points = artifact.Points ?? [];
        var samples = artifact.Samples ?? [];
        var quality = artifact.SourceQuality;
        var preparation = artifact.Preparation;

        if (artifact.SchemaVersion
            != PreparedSceneArtifact.CurrentSchemaVersion)
        {
            errors.Add(
                $"Unsupported Prepared Scene schema '{artifact.SchemaVersion}'.");
        }

        RequireText(artifact.ArtifactId, "artifact ID", errors);
        RequireText(artifact.Name, "name", errors);
        RequireText(artifact.Unit, "unit", errors);
        RequireText(artifact.FrameId, "frame ID", errors);
        if (artifact.CoordinateConvention
            != PreparedSceneArtifact.CurrentCoordinateConvention)
        {
            errors.Add(
                "Prepared Scene coordinate convention is unsupported.");
        }

        ValidateSourceQuality(quality, errors);
        if (quality is not null)
        {
            if (!string.Equals(
                    artifact.Unit,
                    quality.Coordinates?.Unit,
                    StringComparison.Ordinal))
            {
                errors.Add(
                    "Prepared Scene unit differs from Source Quality.");
            }

            if (!string.Equals(
                    artifact.FrameId,
                    quality.Coordinates?.FrameId,
                    StringComparison.Ordinal))
            {
                errors.Add(
                    "Prepared Scene frame differs from Source Quality.");
            }

            if (!string.Equals(
                    artifact.CoordinateConvention,
                    quality.Coordinates?.CoordinateConvention,
                    StringComparison.Ordinal))
            {
                errors.Add(
                    "Prepared Scene coordinate convention differs from Source Quality.");
            }

            if (quality.Coverage is not null
                && points.LongLength
                    != quality.Coverage.ValidSampleCount)
            {
                errors.Add(
                    "Prepared Scene point count must equal Source Quality valid-sample count.");
            }
        }

        if (preparation is null)
        {
            errors.Add(
                "Prepared Scene preparation parameters are missing.");
        }
        else
        {
            if (preparation.SamplingPolicy
                != PreparedScenePreparationParameters
                    .DeterministicEvenPointSampling)
            {
                errors.Add(
                    "Prepared Scene sampling policy is unsupported.");
            }

            if (preparation.MaximumSampleCount <= 0)
            {
                errors.Add(
                    "Prepared Scene maximum sample count must be positive.");
            }
        }

        if (points.Length == 0)
        {
            errors.Add("Prepared Scene requires at least one point.");
        }

        var finitePointCount = points.Count(IsFinite);
        if (finitePointCount != points.Length)
        {
            errors.Add(
                "Prepared Scene points must all be finite.");
        }

        var expectedSampleCount =
            preparation is null || points.Length == 0
                ? 0
                : Math.Min(
                    preparation.MaximumSampleCount,
                    points.Length);
        if (samples.Length != expectedSampleCount)
        {
            errors.Add(
                $"Prepared Scene sample count {samples.Length} does not match deterministic expectation {expectedSampleCount}.");
        }

        var validSampleCount = 0;
        var sampledPoints = new HashSet<int>();
        for (var sampleIndex = 0;
             sampleIndex < samples.Length;
             sampleIndex++)
        {
            var sample = samples[sampleIndex];
            var valid = sample is not null
                && sample.Order == sampleIndex
                && IsFinite(sample.Position)
                && sample.SourcePointIndex >= 0
                && sample.SourcePointIndex < points.Length
                && sampledPoints.Add(sample.SourcePointIndex)
                && preparation is not null;
            if (!valid || sample is null || preparation is null)
            {
                errors.Add(
                    $"Prepared Scene sample {sampleIndex} is invalid.");
                continue;
            }

            var expectedPointIndex =
                PreparedSceneSampling.GetEvenPointIndex(
                    sampleIndex,
                    samples.Length,
                    points.Length);
            if (sample.SourcePointIndex != expectedPointIndex
                || sample.Position
                    != points[sample.SourcePointIndex])
            {
                errors.Add(
                    $"Prepared Scene sample {sampleIndex} does not match its deterministic source point.");
                continue;
            }

            validSampleCount++;
        }

        var sourceQualityIdentityValid = false;
        if (quality is not null
            && IsCanonicalSha256(artifact.SourceQualitySha256))
        {
            try
            {
                sourceQualityIdentityValid = string.Equals(
                    artifact.SourceQualitySha256,
                    SourceQualityReportContentIdentity
                        .CalculateSha256(quality),
                    StringComparison.Ordinal);
            }
            catch (Exception exception)
                when (exception is ArgumentException
                      or InvalidOperationException
                      or NullReferenceException
                      or OverflowException)
            {
                sourceQualityIdentityValid = false;
            }
        }

        if (!sourceQualityIdentityValid)
        {
            errors.Add(
                "Prepared Scene Source Quality identity is invalid.");
        }

        var contentIdentityValid = false;
        if (IsCanonicalSha256(artifact.ContentSha256))
        {
            try
            {
                contentIdentityValid = string.Equals(
                    artifact.ContentSha256,
                    PreparedSceneArtifact
                        .CalculateContentSha256(artifact),
                    StringComparison.Ordinal);
            }
            catch (Exception exception)
                when (exception is ArgumentException
                      or InvalidOperationException
                      or NullReferenceException
                      or OverflowException)
            {
                contentIdentityValid = false;
            }
        }

        if (!contentIdentityValid)
        {
            errors.Add(
                "Prepared Scene content identity is invalid.");
        }

        var state = errors.Count == 0
            ? PreparedSceneValidityState.Valid
            : PreparedSceneValidityState.Invalid;
        var evidence =
            $"state={state};points={finitePointCount}/{points.Length};"
            + $"samples={validSampleCount}/{samples.Length};"
            + $"qualityIdentity={sourceQualityIdentityValid};"
            + $"contentIdentity={contentIdentityValid}";

        return new PreparedSceneValidityReport(
            PreparedSceneValidityReport.CurrentSchemaVersion,
            state,
            points.Length,
            finitePointCount,
            samples.Length,
            validSampleCount,
            sourceQualityIdentityValid,
            contentIdentityValid,
            errors.AsReadOnly(),
            evidence);
    }

    private static void ValidateSourceQuality(
        SourceQualityReport? quality,
        ICollection<string> errors)
    {
        if (quality is null)
        {
            errors.Add(
                "Prepared Scene requires Source Quality evidence.");
            return;
        }

        if (quality.SchemaVersion
            != SourceQualityReport.CurrentSchemaVersion)
        {
            errors.Add(
                $"Unsupported Source Quality schema '{quality.SchemaVersion}'.");
        }

        var source = quality.Source;
        if (source is null)
        {
            errors.Add("Source Quality source identity is missing.");
        }
        else
        {
            RequireText(source.EntityId, "source entity ID", errors);
            RequireText(source.Format, "source format", errors);
            RequireText(source.Path, "source path", errors);
            if (source.ByteLength <= 0)
            {
                errors.Add(
                    "Source Quality byte length must be positive.");
            }

            if (!IsCanonicalSha256(source.ContentSha256)
                || !IsCanonicalSha256(source.RootSourceSha256))
            {
                errors.Add(
                    "Source Quality content identities must be uppercase SHA-256 values.");
            }
        }

        var grid = quality.Grid;
        if (grid is null
            || grid.Width <= 0
            || grid.Height <= 0
            || grid.CellCount
                != checked((long)grid.Width * grid.Height))
        {
            errors.Add(
                "Source Quality grid dimensions are invalid.");
        }

        var coverage = quality.Coverage;
        if (coverage is null
            || coverage.SampleCount <= 0
            || coverage.ValidSampleCount <= 0
            || coverage.MissingSampleCount < 0
            || coverage.ValidSampleCount
                + coverage.MissingSampleCount
                != coverage.SampleCount
            || grid is not null
                && coverage.SampleCount != grid.CellCount
            || !Ratio(
                coverage.ValidRatio,
                coverage.ValidSampleCount,
                coverage.SampleCount)
            || !Ratio(
                coverage.MissingRatio,
                coverage.MissingSampleCount,
                coverage.SampleCount)
            || string.IsNullOrWhiteSpace(
                coverage.MissingSamplePolicy))
        {
            errors.Add(
                "Source Quality coverage evidence is invalid.");
        }

        if (coverage?.InvalidCellMask is null
            || coverage.InvalidCellMask.ByteLength < 0
            || string.IsNullOrWhiteSpace(
                coverage.InvalidCellMask.ContractVersion)
            || string.IsNullOrWhiteSpace(
                coverage.InvalidCellMask.Encoding)
            || !IsCanonicalSha256(
                coverage.InvalidCellMask.Sha256))
        {
            errors.Add(
                "Source Quality invalid-cell identity is invalid.");
        }

        var coordinates = quality.Coordinates;
        if (coordinates is null)
        {
            errors.Add(
                "Source Quality coordinate context is missing.");
        }
        else
        {
            RequireText(coordinates.Unit, "source unit", errors);
            RequireText(coordinates.FrameId, "source frame ID", errors);
            RequireText(
                coordinates.CoordinateConvention,
                "source coordinate convention",
                errors);
        }

        var height = quality.Height;
        if (height is null
            || string.IsNullOrWhiteSpace(height.ScalarMeaning)
            || !OptionalFinite(height.Minimum)
            || !OptionalFinite(height.Maximum)
            || !OptionalFinite(height.Mean))
        {
            errors.Add(
                "Source Quality height evidence is invalid.");
        }
        else if (height.Minimum.HasValue
                 && height.Maximum.HasValue
                 && height.Minimum.Value > height.Maximum.Value)
        {
            errors.Add(
                "Source Quality height minimum exceeds maximum.");
        }

        if (height?.Distribution is not null)
        {
            var distribution = height.Distribution;
            var bins = distribution.Bins;
            if (distribution.BinCount <= 0
                || bins is null
                || bins.Count != distribution.BinCount
                || distribution.PeakBinIndex < 0
                || distribution.PeakBinIndex
                    >= distribution.BinCount
                || !TrySumNonNegative(
                    bins,
                    out var distributionSampleCount)
                || coverage is not null
                    && distributionSampleCount
                        != coverage.ValidSampleCount)
            {
                errors.Add(
                    "Source Quality distribution is invalid.");
            }
        }

        if (quality.Channels is null)
        {
            errors.Add(
                "Source Quality channel evidence is missing.");
        }
        else
        {
            var expectedChannels =
                Enum.GetValues<SourceQualityChannel>();
            var actualChannels = quality.Channels
                .Where(channel => channel is not null)
                .Select(channel => channel.Channel)
                .ToHashSet();
            if (quality.Channels.Count != expectedChannels.Length
                || !actualChannels.SetEquals(expectedChannels)
                || quality.Channels.Any(channel =>
                    channel is null
                    || !Enum.IsDefined(channel.State)
                    || string.IsNullOrWhiteSpace(channel.Evidence)))
            {
                errors.Add(
                    "Source Quality must report each channel exactly once with evidence.");
            }
        }

        RequireText(
            quality.Provenance,
            "source provenance",
            errors);
    }

    private static bool Ratio(
        double ratio,
        long numerator,
        long denominator) =>
        double.IsFinite(ratio)
        && denominator > 0
        && Math.Abs(ratio - numerator / (double)denominator)
            <= 1e-12;

    private static bool OptionalFinite(double? value) =>
        !value.HasValue || double.IsFinite(value.Value);

    private static bool TrySumNonNegative(
        IReadOnlyList<long> values,
        out long sum)
    {
        sum = 0;
        foreach (var value in values)
        {
            if (value < 0 || long.MaxValue - sum < value)
            {
                return false;
            }

            sum += value;
        }

        return true;
    }

    private static bool IsFinite(SurfaceModelPoint3? point) =>
        point is not null
        && double.IsFinite(point.X)
        && double.IsFinite(point.Y)
        && double.IsFinite(point.Z);

    private static void RequireText(
        string? value,
        string name,
        ICollection<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add(
                $"Prepared Scene {name} is required.");
        }
    }

    private static bool IsCanonicalSha256(string? value) =>
        value is not null
        && value.Length == 64
        && value.All(character =>
            character is >= '0' and <= '9'
            or >= 'A' and <= 'F');
}
