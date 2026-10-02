using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace OpenVisionLab.ThreeD.Core;

public enum C3DPhysicalEdgeGapFlushMeasurementState
{
    Valid,
    Limited,
    Rejected,
    Invalid
}

public enum C3DPhysicalEdgeGapFlushToleranceState
{
    NotConfigured,
    Pass,
    Fail,
    NotEvaluated
}

public enum C3DPhysicalEdgeSurfaceSide
{
    First,
    Second
}

public enum C3DPhysicalEdgeSurfaceAggregation
{
    MeanInlierPairs
}

public enum C3DPhysicalEdgeAmbiguityPolicy
{
    RejectNearTie
}

/// <summary>
/// Authored policy for one physical-edge Gap/Flush measurement. Values are
/// persisted as invariant recipe parameters and are part of the result hash.
/// This first contract remains in the source-grid frame; it does not assert
/// calibration, normal distance, or physical metrology.
/// </summary>
public sealed record C3DPhysicalEdgeGapFlushProfile(
    string SectionId,
    C3DHeightDifferenceComparisonAxis ComparisonAxis,
    C3DPhysicalEdgeSurfaceSide FirstSurfaceSide,
    C3DPhysicalEdgeSurfaceSide SecondSurfaceSide,
    C3DPhysicalEdgeSurfaceAggregation SurfaceAggregation,
    int MinimumValidPairCount,
    double MinimumCoverageRatio,
    double MaximumResidual,
    double MinimumInlierRatio,
    int MinimumInlierScanlineSpan,
    C3DPhysicalEdgeAmbiguityPolicy AmbiguityPolicy,
    double AmbiguityEpsilon,
    bool ContactAllowed,
    bool ToleranceEnabled,
    double ExpectedGap,
    double GapTolerance,
    double ExpectedFlush,
    double FlushTolerance)
{
    public const string SectionIdParameter = "SectionId";
    public const string ComparisonAxisParameter = "ComparisonAxis";
    public const string FirstSurfaceSideParameter = "FirstSurfaceSide";
    public const string SecondSurfaceSideParameter = "SecondSurfaceSide";
    public const string SurfaceAggregationParameter = "SurfaceAggregation";
    public const string MinimumValidPairCountParameter = "MinimumValidPairCount";
    public const string MinimumCoverageRatioParameter = "MinimumCoverageRatio";
    public const string MaximumResidualParameter = "MaximumResidual";
    public const string MinimumInlierRatioParameter = "MinimumInlierRatio";
    public const string MinimumInlierScanlineSpanParameter = "MinimumInlierScanlineSpan";
    public const string AmbiguityPolicyParameter = "AmbiguityPolicy";
    public const string AmbiguityEpsilonParameter = "AmbiguityEpsilon";
    public const string ContactAllowedParameter = "ContactAllowed";
    public const string ToleranceModeParameter = "ToleranceMode";
    public const string ExpectedGapParameter = "ExpectedGap";
    public const string GapToleranceParameter = "GapTolerance";
    public const string ExpectedFlushParameter = "ExpectedFlush";
    public const string FlushToleranceParameter = "FlushTolerance";

    public static IReadOnlyList<string> ParameterNames { get; } =
    [
        SectionIdParameter,
        ComparisonAxisParameter,
        FirstSurfaceSideParameter,
        SecondSurfaceSideParameter,
        SurfaceAggregationParameter,
        MinimumValidPairCountParameter,
        MinimumCoverageRatioParameter,
        MaximumResidualParameter,
        MinimumInlierRatioParameter,
        MinimumInlierScanlineSpanParameter,
        AmbiguityPolicyParameter,
        AmbiguityEpsilonParameter,
        ContactAllowedParameter,
        ToleranceModeParameter,
        ExpectedGapParameter,
        GapToleranceParameter,
        ExpectedFlushParameter,
        FlushToleranceParameter
    ];

    public static C3DPhysicalEdgeGapFlushProfile Default { get; } = new(
        "section.default",
        C3DHeightDifferenceComparisonAxis.AcrossColumns,
        C3DPhysicalEdgeSurfaceSide.First,
        C3DPhysicalEdgeSurfaceSide.Second,
        C3DPhysicalEdgeSurfaceAggregation.MeanInlierPairs,
        4,
        0.75d,
        0.001d,
        0.75d,
        4,
        C3DPhysicalEdgeAmbiguityPolicy.RejectNearTie,
        0d,
        false,
        true,
        0d,
        100000d,
        0d,
        100000d);

    public IReadOnlyList<ToolRecipeParameter> ToRecipeParameters() =>
    [
        new(SectionIdParameter, SectionId),
        new(ComparisonAxisParameter, ComparisonAxis.ToString()),
        new(FirstSurfaceSideParameter, FirstSurfaceSide.ToString()),
        new(SecondSurfaceSideParameter, SecondSurfaceSide.ToString()),
        new(SurfaceAggregationParameter, SurfaceAggregation.ToString()),
        new(MinimumValidPairCountParameter, MinimumValidPairCount.ToString(CultureInfo.InvariantCulture)),
        new(MinimumCoverageRatioParameter, MinimumCoverageRatio.ToString("G17", CultureInfo.InvariantCulture)),
        new(MaximumResidualParameter, MaximumResidual.ToString("G17", CultureInfo.InvariantCulture)),
        new(MinimumInlierRatioParameter, MinimumInlierRatio.ToString("G17", CultureInfo.InvariantCulture)),
        new(MinimumInlierScanlineSpanParameter, MinimumInlierScanlineSpan.ToString(CultureInfo.InvariantCulture)),
        new(AmbiguityPolicyParameter, AmbiguityPolicy.ToString()),
        new(AmbiguityEpsilonParameter, AmbiguityEpsilon.ToString("G17", CultureInfo.InvariantCulture)),
        new(ContactAllowedParameter, ContactAllowed.ToString()),
        new(ToleranceModeParameter, ToleranceEnabled ? "Enabled" : "Disabled"),
        new(ExpectedGapParameter, ExpectedGap.ToString("G17", CultureInfo.InvariantCulture)),
        new(GapToleranceParameter, GapTolerance.ToString("G17", CultureInfo.InvariantCulture)),
        new(ExpectedFlushParameter, ExpectedFlush.ToString("G17", CultureInfo.InvariantCulture)),
        new(FlushToleranceParameter, FlushTolerance.ToString("G17", CultureInfo.InvariantCulture))
    ];

    public static bool TryParse(
        IReadOnlyList<ToolRecipeParameter>? parameters,
        out C3DPhysicalEdgeGapFlushProfile? profile,
        out string message)
    {
        profile = null;
        try
        {
            ArgumentNullException.ThrowIfNull(parameters);
            if (parameters.Count != ParameterNames.Count
                || ParameterNames.Any(name => parameters.Count(parameter => parameter?.Name == name) != 1))
            {
                throw new InvalidDataException(
                    $"Physical Edge Gap / Flush v1 requires exactly one value for every recognized parameter ({string.Join(", ", ParameterNames)}).");
            }

            string Value(string name) => parameters.Single(parameter => parameter.Name == name).Value;
            var sectionId = Value(SectionIdParameter);
            if (sectionId != sectionId.Trim() || string.IsNullOrWhiteSpace(sectionId))
            {
                throw new InvalidDataException("SectionId must be a non-empty authored identity without surrounding whitespace.");
            }

            var comparisonAxis = ParseEnum<C3DHeightDifferenceComparisonAxis>(Value(ComparisonAxisParameter), ComparisonAxisParameter);
            var firstSurfaceSide = ParseEnum<C3DPhysicalEdgeSurfaceSide>(Value(FirstSurfaceSideParameter), FirstSurfaceSideParameter);
            var secondSurfaceSide = ParseEnum<C3DPhysicalEdgeSurfaceSide>(Value(SecondSurfaceSideParameter), SecondSurfaceSideParameter);
            var aggregation = ParseEnum<C3DPhysicalEdgeSurfaceAggregation>(Value(SurfaceAggregationParameter), SurfaceAggregationParameter);
            var minimumPairCount = ParseInteger(Value(MinimumValidPairCountParameter), MinimumValidPairCountParameter);
            var coverage = ParseFiniteRatio(Value(MinimumCoverageRatioParameter), MinimumCoverageRatioParameter);
            var maximumResidual = ParseFinitePositive(Value(MaximumResidualParameter), MaximumResidualParameter);
            var minimumInlierRatio = ParseFiniteRatio(Value(MinimumInlierRatioParameter), MinimumInlierRatioParameter);
            var minimumSpan = ParseInteger(Value(MinimumInlierScanlineSpanParameter), MinimumInlierScanlineSpanParameter);
            var ambiguityPolicy = ParseEnum<C3DPhysicalEdgeAmbiguityPolicy>(Value(AmbiguityPolicyParameter), AmbiguityPolicyParameter);
            var ambiguityEpsilon = ParseFiniteNonNegative(Value(AmbiguityEpsilonParameter), AmbiguityEpsilonParameter);
            var contactAllowed = ParseBoolean(Value(ContactAllowedParameter), ContactAllowedParameter);
            var toleranceMode = Value(ToleranceModeParameter);
            if (toleranceMode is not ("Enabled" or "Disabled"))
            {
                throw new InvalidDataException("ToleranceMode must be Enabled or Disabled.");
            }

            var expectedGap = ParseFinite(Value(ExpectedGapParameter), ExpectedGapParameter);
            var gapTolerance = ParseFiniteNonNegative(Value(GapToleranceParameter), GapToleranceParameter);
            var expectedFlush = ParseFinite(Value(ExpectedFlushParameter), ExpectedFlushParameter);
            var flushTolerance = ParseFiniteNonNegative(Value(FlushToleranceParameter), FlushToleranceParameter);
            if (minimumPairCount <= 0 || minimumSpan <= 0)
            {
                throw new InvalidDataException("MinimumValidPairCount and MinimumInlierScanlineSpan must be greater than zero.");
            }

            profile = new C3DPhysicalEdgeGapFlushProfile(
                sectionId,
                comparisonAxis,
                firstSurfaceSide,
                secondSurfaceSide,
                aggregation,
                minimumPairCount,
                coverage,
                maximumResidual,
                minimumInlierRatio,
                minimumSpan,
                ambiguityPolicy,
                ambiguityEpsilon,
                contactAllowed,
                toleranceMode == "Enabled",
                expectedGap,
                gapTolerance,
                expectedFlush,
                flushTolerance);
            message = string.Empty;
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidDataException or OverflowException)
        {
            message = exception.Message;
            return false;
        }
    }

    private static T ParseEnum<T>(string value, string name)
        where T : struct, Enum =>
        Enum.TryParse<T>(value, ignoreCase: false, out var parsed) && Enum.IsDefined(parsed)
            ? parsed
            : throw new InvalidDataException($"{name} has unsupported value '{value}'.");

    private static bool ParseBoolean(string value, string name) =>
        bool.TryParse(value, out var parsed)
            ? parsed
            : throw new InvalidDataException($"{name} must be True or False.");

    private static int ParseInteger(string value, string name) =>
        value == value.Trim()
        && !value.Contains(',', StringComparison.Ordinal)
        && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : throw new InvalidDataException($"{name} must be an explicit invariant integer.");

    private static double ParseFinite(string value, string name) =>
        value == value.Trim()
        && !value.Contains(',', StringComparison.Ordinal)
        && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
        && double.IsFinite(parsed)
            ? parsed
            : throw new InvalidDataException($"{name} must be an explicit invariant finite number.");

    private static double ParseFinitePositive(string value, string name)
    {
        var parsed = ParseFinite(value, name);
        return parsed > 0d
            ? parsed
            : throw new InvalidDataException($"{name} must be greater than zero.");
    }

    private static double ParseFiniteNonNegative(string value, string name)
    {
        var parsed = ParseFinite(value, name);
        return parsed >= 0d
            ? parsed
            : throw new InvalidDataException($"{name} must be non-negative.");
    }

    private static double ParseFiniteRatio(string value, string name)
    {
        var parsed = ParseFinite(value, name);
        return parsed is >= 0d and <= 1d
            ? parsed
            : throw new InvalidDataException($"{name} must be between zero and one inclusive.");
    }
}

public sealed record C3DPhysicalEdgeGapFlushInput(
    string StepId,
    string OutputEntityId,
    C3DLineFeature FirstLine,
    C3DLineFeature SecondLine,
    C3DHeightDifferenceEdgePointSet FirstEdge,
    C3DHeightDifferenceEdgePointSet SecondEdge,
    C3DPhysicalEdgeGapFlushProfile Profile);

/// <summary>
/// Immutable physical-edge result in the source-grid frame. The result keeps
/// source and evidence identities so a Run Record can explain exactly what
/// was measured; it does not claim calibrated physical metrology.
/// </summary>
public sealed class C3DPhysicalEdgeGapFlushResult
{
    public const string ContractVersion = "1.1";

    private C3DPhysicalEdgeGapFlushResult(
        string outputEntityId,
        string firstLineEntityId,
        string secondLineEntityId,
        string firstEdgePointSetEntityId,
        string secondEdgePointSetEntityId,
        string firstEdgeContentSha256,
        string secondEdgeContentSha256,
        string firstLineContentSha256,
        string secondLineContentSha256,
        string rootSourceEntityId,
        string rootSourceSha256,
        string unit,
        string frameId,
        C3DPhysicalEdgeGapFlushProfile profile,
        C3DHeightDifferencePolarity firstPolarity,
        C3DHeightDifferencePolarity secondPolarity,
        double signedGap,
        double gapMagnitude,
        double signedFlush,
        double flushMagnitude,
        double firstSurfaceSupport,
        double secondSurfaceSupport,
        int firstSurfaceSupportCount,
        int secondSurfaceSupportCount,
        int validPairCount,
        double coverageRatio,
        double firstInlierRatio,
        double secondInlierRatio,
        int firstInlierScanlineSpan,
        int secondInlierScanlineSpan,
        double firstResidualMaximum,
        double secondResidualMaximum,
        C3DPhysicalEdgeGapFlushMeasurementState measurementState,
        C3DPhysicalEdgeGapFlushToleranceState toleranceState,
        string topCandidateEvidence,
        string secondCandidateEvidence,
        string failureReason,
        string provenance,
        string contentSha256)
    {
        OutputEntityId = outputEntityId;
        FirstLineEntityId = firstLineEntityId;
        SecondLineEntityId = secondLineEntityId;
        FirstEdgePointSetEntityId = firstEdgePointSetEntityId;
        SecondEdgePointSetEntityId = secondEdgePointSetEntityId;
        FirstEdgeContentSha256 = firstEdgeContentSha256;
        SecondEdgeContentSha256 = secondEdgeContentSha256;
        FirstLineContentSha256 = firstLineContentSha256;
        SecondLineContentSha256 = secondLineContentSha256;
        RootSourceEntityId = rootSourceEntityId;
        RootSourceSha256 = rootSourceSha256;
        Unit = unit;
        FrameId = frameId;
        SectionId = profile.SectionId;
        ComparisonAxis = profile.ComparisonAxis;
        FirstPolarity = firstPolarity;
        SecondPolarity = secondPolarity;
        FirstSurfaceSide = profile.FirstSurfaceSide;
        SecondSurfaceSide = profile.SecondSurfaceSide;
        SurfaceAggregation = profile.SurfaceAggregation;
        MinimumValidPairCount = profile.MinimumValidPairCount;
        MinimumCoverageRatio = profile.MinimumCoverageRatio;
        MaximumResidual = profile.MaximumResidual;
        MinimumInlierRatio = profile.MinimumInlierRatio;
        MinimumInlierScanlineSpan = profile.MinimumInlierScanlineSpan;
        AmbiguityPolicy = profile.AmbiguityPolicy;
        AmbiguityEpsilon = profile.AmbiguityEpsilon;
        ContactAllowed = profile.ContactAllowed;
        ToleranceEnabled = profile.ToleranceEnabled;
        ExpectedGap = profile.ExpectedGap;
        GapTolerance = profile.GapTolerance;
        ExpectedFlush = profile.ExpectedFlush;
        FlushTolerance = profile.FlushTolerance;
        SignedGap = signedGap;
        GapMagnitude = gapMagnitude;
        SignedFlush = signedFlush;
        FlushMagnitude = flushMagnitude;
        FirstSurfaceSupport = firstSurfaceSupport;
        SecondSurfaceSupport = secondSurfaceSupport;
        FirstSurfaceSupportCount = firstSurfaceSupportCount;
        SecondSurfaceSupportCount = secondSurfaceSupportCount;
        ValidPairCount = validPairCount;
        CoverageRatio = coverageRatio;
        FirstInlierRatio = firstInlierRatio;
        SecondInlierRatio = secondInlierRatio;
        FirstInlierScanlineSpan = firstInlierScanlineSpan;
        SecondInlierScanlineSpan = secondInlierScanlineSpan;
        FirstResidualMaximum = firstResidualMaximum;
        SecondResidualMaximum = secondResidualMaximum;
        MeasurementState = measurementState;
        ToleranceState = toleranceState;
        TopCandidateEvidence = topCandidateEvidence;
        SecondCandidateEvidence = secondCandidateEvidence;
        FailureReason = failureReason;
        Provenance = provenance;
        ContentSha256 = contentSha256;
        EvidenceSummary = BuildEvidenceSummary(this);
    }

    public string OutputEntityId { get; }
    public string FirstLineEntityId { get; }
    public string SecondLineEntityId { get; }
    public string FirstEdgePointSetEntityId { get; }
    public string SecondEdgePointSetEntityId { get; }
    public string FirstEdgeContentSha256 { get; }
    public string SecondEdgeContentSha256 { get; }
    public string FirstLineContentSha256 { get; }
    public string SecondLineContentSha256 { get; }
    public string RootSourceEntityId { get; }
    public string RootSourceSha256 { get; }
    public string Unit { get; }
    public string FrameId { get; }
    public string SectionId { get; }
    public C3DHeightDifferenceComparisonAxis ComparisonAxis { get; }
    public C3DHeightDifferencePolarity FirstPolarity { get; }
    public C3DHeightDifferencePolarity SecondPolarity { get; }
    public C3DPhysicalEdgeSurfaceSide FirstSurfaceSide { get; }
    public C3DPhysicalEdgeSurfaceSide SecondSurfaceSide { get; }
    public C3DPhysicalEdgeSurfaceAggregation SurfaceAggregation { get; }
    public int MinimumValidPairCount { get; }
    public double MinimumCoverageRatio { get; }
    public double MaximumResidual { get; }
    public double MinimumInlierRatio { get; }
    public int MinimumInlierScanlineSpan { get; }
    public C3DPhysicalEdgeAmbiguityPolicy AmbiguityPolicy { get; }
    public double AmbiguityEpsilon { get; }
    public bool ContactAllowed { get; }
    public bool ToleranceEnabled { get; }
    public double ExpectedGap { get; }
    public double GapTolerance { get; }
    public double ExpectedFlush { get; }
    public double FlushTolerance { get; }
    public double SignedGap { get; }
    public double GapMagnitude { get; }
    public double SignedFlush { get; }
    public double FlushMagnitude { get; }
    public double FirstSurfaceSupport { get; }
    public double SecondSurfaceSupport { get; }
    public int FirstSurfaceSupportCount { get; }
    public int SecondSurfaceSupportCount { get; }
    public int ValidPairCount { get; }
    public double CoverageRatio { get; }
    public double FirstInlierRatio { get; }
    public double SecondInlierRatio { get; }
    public int FirstInlierScanlineSpan { get; }
    public int SecondInlierScanlineSpan { get; }
    public double FirstResidualMaximum { get; }
    public double SecondResidualMaximum { get; }
    public C3DPhysicalEdgeGapFlushMeasurementState MeasurementState { get; }
    public C3DPhysicalEdgeGapFlushToleranceState ToleranceState { get; }
    public string TopCandidateEvidence { get; }
    public string SecondCandidateEvidence { get; }
    public string FailureReason { get; }
    public string Provenance { get; }
    public string ContentSha256 { get; }
    public string EvidenceSummary { get; }

    public static C3DPhysicalEdgeGapFlushResult Create(
        string outputEntityId,
        C3DPhysicalEdgeGapFlushInput input,
        double signedGap,
        double signedFlush,
        double firstSurfaceSupport,
        double secondSurfaceSupport,
        int firstSurfaceSupportCount,
        int secondSurfaceSupportCount,
        int validPairCount,
        double coverageRatio,
        C3DPhysicalEdgeGapFlushMeasurementState measurementState,
        C3DPhysicalEdgeGapFlushToleranceState toleranceState,
        string topCandidateEvidence,
        string secondCandidateEvidence,
        string failureReason,
        string provenance)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputEntityId);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(input.FirstLine);
        ArgumentNullException.ThrowIfNull(input.SecondLine);
        ArgumentNullException.ThrowIfNull(input.FirstEdge);
        ArgumentNullException.ThrowIfNull(input.SecondEdge);
        ArgumentNullException.ThrowIfNull(input.Profile);
        ArgumentException.ThrowIfNullOrWhiteSpace(topCandidateEvidence);
        ArgumentException.ThrowIfNullOrWhiteSpace(secondCandidateEvidence);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);

        var firstLine = input.FirstLine;
        var secondLine = input.SecondLine;
        var firstEdge = input.FirstEdge;
        var secondEdge = input.SecondEdge;
        var hash = CalculateContentSha256(
            outputEntityId,
            firstLine,
            secondLine,
            firstEdge,
            secondEdge,
            input.Profile,
            signedGap,
            signedFlush,
            firstSurfaceSupport,
            secondSurfaceSupport,
            firstSurfaceSupportCount,
            secondSurfaceSupportCount,
            validPairCount,
            coverageRatio,
            measurementState,
            toleranceState,
            topCandidateEvidence,
            secondCandidateEvidence,
            failureReason,
            provenance);
        return new C3DPhysicalEdgeGapFlushResult(
            outputEntityId,
            firstLine.OutputEntityId,
            secondLine.OutputEntityId,
            firstEdge.OutputEntityId,
            secondEdge.OutputEntityId,
            firstEdge.ContentSha256,
            secondEdge.ContentSha256,
            firstLine.ContentSha256,
            secondLine.ContentSha256,
            firstLine.RootSourceEntityId,
            firstLine.RootSourceSha256,
            firstLine.Unit,
            firstLine.FrameId,
            input.Profile,
            firstEdge.Polarity,
            secondEdge.Polarity,
            signedGap,
            Math.Abs(signedGap),
            signedFlush,
            Math.Abs(signedFlush),
            firstSurfaceSupport,
            secondSurfaceSupport,
            firstSurfaceSupportCount,
            secondSurfaceSupportCount,
            validPairCount,
            coverageRatio,
            firstLine.Diagnostics.InlierRatio,
            secondLine.Diagnostics.InlierRatio,
            firstLine.Diagnostics.InlierScanlineSpan,
            secondLine.Diagnostics.InlierScanlineSpan,
            firstLine.Diagnostics.ResidualMaximum,
            secondLine.Diagnostics.ResidualMaximum,
            measurementState,
            toleranceState,
            topCandidateEvidence,
            secondCandidateEvidence,
            failureReason,
            provenance,
            hash);
    }

    private static string CalculateContentSha256(
        string outputEntityId,
        C3DLineFeature firstLine,
        C3DLineFeature secondLine,
        C3DHeightDifferenceEdgePointSet firstEdge,
        C3DHeightDifferenceEdgePointSet secondEdge,
        C3DPhysicalEdgeGapFlushProfile profile,
        double signedGap,
        double signedFlush,
        double firstSurfaceSupport,
        double secondSurfaceSupport,
        int firstSurfaceSupportCount,
        int secondSurfaceSupportCount,
        int validPairCount,
        double coverageRatio,
        C3DPhysicalEdgeGapFlushMeasurementState measurementState,
        C3DPhysicalEdgeGapFlushToleranceState toleranceState,
        string topCandidateEvidence,
        string secondCandidateEvidence,
        string failureReason,
        string provenance)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write("OpenVisionLab.C3DPhysicalEdgeGapFlushResult");
            writer.Write(ContractVersion);
            writer.Write(outputEntityId);
            WriteIdentity(writer, firstLine.OutputEntityId, firstLine.ContentSha256);
            WriteIdentity(writer, secondLine.OutputEntityId, secondLine.ContentSha256);
            WriteIdentity(writer, firstEdge.OutputEntityId, firstEdge.ContentSha256);
            WriteIdentity(writer, secondEdge.OutputEntityId, secondEdge.ContentSha256);
            writer.Write(firstLine.RootSourceEntityId);
            writer.Write(firstLine.RootSourceSha256.ToUpperInvariant());
            writer.Write(firstLine.Unit);
            writer.Write(firstLine.FrameId);
            writer.Write(profile.SectionId);
            writer.Write((int)profile.ComparisonAxis);
            writer.Write((int)firstEdge.Polarity);
            writer.Write((int)secondEdge.Polarity);
            writer.Write((int)profile.FirstSurfaceSide);
            writer.Write((int)profile.SecondSurfaceSide);
            writer.Write((int)profile.SurfaceAggregation);
            writer.Write(profile.MinimumValidPairCount);
            writer.Write(profile.MinimumCoverageRatio);
            writer.Write(profile.MaximumResidual);
            writer.Write(profile.MinimumInlierRatio);
            writer.Write(profile.MinimumInlierScanlineSpan);
            writer.Write((int)profile.AmbiguityPolicy);
            writer.Write(profile.AmbiguityEpsilon);
            writer.Write(profile.ContactAllowed);
            writer.Write(profile.ToleranceEnabled);
            writer.Write(profile.ExpectedGap);
            writer.Write(profile.GapTolerance);
            writer.Write(profile.ExpectedFlush);
            writer.Write(profile.FlushTolerance);
            writer.Write(signedGap);
            writer.Write(signedFlush);
            writer.Write(firstSurfaceSupport);
            writer.Write(secondSurfaceSupport);
            writer.Write(firstSurfaceSupportCount);
            writer.Write(secondSurfaceSupportCount);
            writer.Write(validPairCount);
            writer.Write(coverageRatio);
            writer.Write((int)measurementState);
            writer.Write((int)toleranceState);
            writer.Write(topCandidateEvidence);
            writer.Write(secondCandidateEvidence);
            writer.Write(failureReason);
            writer.Write(provenance);
        }

        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static void WriteIdentity(BinaryWriter writer, string entityId, string contentSha256)
    {
        writer.Write(entityId);
        writer.Write(contentSha256.ToUpperInvariant());
    }

    private static string BuildEvidenceSummary(C3DPhysicalEdgeGapFlushResult result) =>
        $"section={result.SectionId};firstLine={result.FirstLineEntityId};secondLine={result.SecondLineEntityId};firstEdge={result.FirstEdgePointSetEntityId};secondEdge={result.SecondEdgePointSetEntityId};root={result.RootSourceEntityId};sourceSha={result.RootSourceSha256};axis={result.ComparisonAxis};polarity={result.FirstPolarity}/{result.SecondPolarity};state={result.MeasurementState};tolerance={result.ToleranceState};gap={Format(result.SignedGap)};flush={Format(result.SignedFlush)};validPairs={result.ValidPairCount};coverage={Format(result.CoverageRatio)};support={Format(result.FirstSurfaceSupport)}/{Format(result.SecondSurfaceSupport)} ({result.FirstSurfaceSupportCount}/{result.SecondSurfaceSupportCount});ambiguity={result.AmbiguityPolicy};failure={result.FailureReason}";

    private static string Format(double value) =>
        double.IsFinite(value) ? value.ToString("G17", CultureInfo.InvariantCulture) : value.ToString(CultureInfo.InvariantCulture);
}
