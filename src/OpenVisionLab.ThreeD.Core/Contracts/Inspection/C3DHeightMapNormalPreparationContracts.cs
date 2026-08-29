using System.Security.Cryptography;
using System.Text;

namespace OpenVisionLab.ThreeD.Core;

public enum C3DHeightMapNormalValidationState
{
    NotRequested,
    Passed,
    Failed
}

/// <summary>
/// One immutable row-major normal sample produced from a C3D height field.
/// Position uses X=grid column, Y=raw height, Z=grid row.
/// </summary>
public sealed record C3DHeightMapNormalSample(
    int Row,
    int Column,
    double PositionX,
    double PositionY,
    double PositionZ,
    double NormalX,
    double NormalY,
    double NormalZ,
    bool CentralColumnDerivative,
    bool CentralRowDerivative);

/// <summary>
/// Optional caller-authored validation normal. The SDK normalizes this value
/// for comparison; it never infers orientation from the source.
/// </summary>
public sealed record C3DHeightMapNormalValidationOptions(
    double ExpectedNormalX,
    double ExpectedNormalY,
    double ExpectedNormalZ,
    double MinimumAlignmentCosine = 0.999);

/// <summary>
/// Immutable source/result evidence for D-14 regular-height-map normal
/// preparation. No C3D bytes are overwritten; the output identity names the
/// derived normal artifact represented by this evidence.
/// </summary>
public sealed class C3DHeightMapNormalPreparationEvidence
{
    public const string ContractVersion = "1.0";
    public const string CoordinateConventionName =
        "X=grid-column,Y=raw-height,Z=grid-row";
    public const string DerivativePolicyName =
        "CentralInteriorOneSidedFiniteBoundary";
    public const string MissingPolicyName =
        "UnavailableNoFiniteNeighborNoInterpolation";
    public const string LineagePolicyName =
        "SeparateSourceDerivedNormalArtifact";

    private C3DHeightMapNormalPreparationEvidence(
        string stepId,
        string sourceEntityId,
        string sourceContentSha256,
        string sourceRootSourceSha256,
        long sourceByteLength,
        string outputEntityId,
        string outputContentSha256,
        string outputRootSourceSha256,
        string unit,
        string frameId,
        string coordinateConvention,
        int rowCount,
        int columnCount,
        int inputFiniteSampleCount,
        int calculatedNormalCount,
        int unavailableNormalCount,
        int centralDerivativeCount,
        int oneSidedDerivativeCount,
        int missingDerivativeCount,
        IReadOnlyList<C3DHeightMapNormalSample> samples,
        C3DHeightMapNormalValidationState validationState,
        double? expectedNormalX,
        double? expectedNormalY,
        double? expectedNormalZ,
        double? minimumAlignmentCosine,
        int validatedNormalCount,
        int consistentNormalCount,
        int reversedNormalCount,
        double? minimumAlignment,
        double? meanAlignment,
        double? maximumAngularErrorDegrees,
        string provenance,
        string contentSha256)
    {
        StepId = stepId;
        SourceEntityId = sourceEntityId;
        SourceContentSha256 = sourceContentSha256;
        SourceRootSourceSha256 = sourceRootSourceSha256;
        SourceByteLength = sourceByteLength;
        OutputEntityId = outputEntityId;
        OutputContentSha256 = outputContentSha256;
        OutputRootSourceSha256 = outputRootSourceSha256;
        Unit = unit;
        FrameId = frameId;
        CoordinateConvention = coordinateConvention;
        RowCount = rowCount;
        ColumnCount = columnCount;
        InputFiniteSampleCount = inputFiniteSampleCount;
        CalculatedNormalCount = calculatedNormalCount;
        UnavailableNormalCount = unavailableNormalCount;
        CentralDerivativeCount = centralDerivativeCount;
        OneSidedDerivativeCount = oneSidedDerivativeCount;
        MissingDerivativeCount = missingDerivativeCount;
        Samples = Array.AsReadOnly(samples.ToArray());
        ValidationState = validationState;
        ExpectedNormalX = expectedNormalX;
        ExpectedNormalY = expectedNormalY;
        ExpectedNormalZ = expectedNormalZ;
        MinimumAlignmentCosine = minimumAlignmentCosine;
        ValidatedNormalCount = validatedNormalCount;
        ConsistentNormalCount = consistentNormalCount;
        ReversedNormalCount = reversedNormalCount;
        MinimumAlignment = minimumAlignment;
        MeanAlignment = meanAlignment;
        MaximumAngularErrorDegrees = maximumAngularErrorDegrees;
        Provenance = provenance;
        ContentSha256 = contentSha256;
    }

    public string StepId { get; }
    public string SourceEntityId { get; }
    public string SourceContentSha256 { get; }
    public string SourceRootSourceSha256 { get; }
    public long SourceByteLength { get; }
    public string OutputEntityId { get; }
    public string OutputContentSha256 { get; }
    public string OutputRootSourceSha256 { get; }
    public string Unit { get; }
    public string FrameId { get; }
    public string CoordinateConvention { get; }
    public int RowCount { get; }
    public int ColumnCount { get; }
    public int InputFiniteSampleCount { get; }
    public int CalculatedNormalCount { get; }
    public int UnavailableNormalCount { get; }
    public int CentralDerivativeCount { get; }
    public int OneSidedDerivativeCount { get; }
    public int MissingDerivativeCount { get; }
    public IReadOnlyList<C3DHeightMapNormalSample> Samples { get; }
    public C3DHeightMapNormalValidationState ValidationState { get; }
    public double? ExpectedNormalX { get; }
    public double? ExpectedNormalY { get; }
    public double? ExpectedNormalZ { get; }
    public double? MinimumAlignmentCosine { get; }
    public int ValidatedNormalCount { get; }
    public int ConsistentNormalCount { get; }
    public int ReversedNormalCount { get; }
    public double? MinimumAlignment { get; }
    public double? MeanAlignment { get; }
    public double? MaximumAngularErrorDegrees { get; }
    public string DerivativePolicy => DerivativePolicyName;
    public string MissingPolicy => MissingPolicyName;
    public string LineagePolicy => LineagePolicyName;
    public string Provenance { get; }
    public string ContentSha256 { get; }

    public static C3DHeightMapNormalPreparationEvidence Create(
        string stepId,
        string sourceEntityId,
        string sourceContentSha256,
        string sourceRootSourceSha256,
        long sourceByteLength,
        string outputEntityId,
        string unit,
        string frameId,
        int rowCount,
        int columnCount,
        int inputFiniteSampleCount,
        int calculatedNormalCount,
        int unavailableNormalCount,
        int centralDerivativeCount,
        int oneSidedDerivativeCount,
        int missingDerivativeCount,
        IReadOnlyList<C3DHeightMapNormalSample> samples,
        C3DHeightMapNormalValidationState validationState,
        C3DHeightMapNormalValidationOptions? validation,
        int validatedNormalCount,
        int consistentNormalCount,
        int reversedNormalCount,
        double? minimumAlignment,
        double? meanAlignment,
        double? maximumAngularErrorDegrees,
        string provenance)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stepId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceEntityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputEntityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(unit);
        ArgumentException.ThrowIfNullOrWhiteSpace(frameId);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        if (string.Equals(sourceEntityId, outputEntityId, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "Normal-preparation source and output identities must be distinct.");
        }
        if (!IsSha256(sourceContentSha256)
            || !IsSha256(sourceRootSourceSha256))
        {
            throw new ArgumentException(
                "Normal-preparation evidence requires source SHA-256 identities.");
        }
        if (sourceByteLength <= 0
            || rowCount <= 0
            || columnCount <= 0
            || inputFiniteSampleCount <= 0
            || calculatedNormalCount <= 0
            || unavailableNormalCount < 0
            || calculatedNormalCount + unavailableNormalCount > inputFiniteSampleCount
            || centralDerivativeCount < 0
            || oneSidedDerivativeCount < 0
            || missingDerivativeCount < 0
            || centralDerivativeCount + oneSidedDerivativeCount + missingDerivativeCount
                != checked(inputFiniteSampleCount * 2))
        {
            throw new ArgumentException(
                "Normal-preparation evidence contains invalid dimensions, counts, or policies.");
        }
        ArgumentNullException.ThrowIfNull(samples);
        if (samples.Count != calculatedNormalCount)
        {
            throw new ArgumentException(
                "Normal-preparation sample evidence must match the calculated count.");
        }

        var previousRow = -1;
        var previousColumn = -1;
        foreach (var sample in samples)
        {
            if (sample is null
                || sample.Row < 0
                || sample.Row >= rowCount
                || sample.Column < 0
                || sample.Column >= columnCount
                || (sample.Row == previousRow && sample.Column <= previousColumn)
                || (sample.Row < previousRow))
            {
                throw new ArgumentException(
                    "Normal-preparation samples must be finite and row-major within the source grid.");
            }
            var values = new[]
            {
                sample.PositionX,
                sample.PositionY,
                sample.PositionZ,
                sample.NormalX,
                sample.NormalY,
                sample.NormalZ
            };
            if (values.Any(value => !double.IsFinite(value)))
            {
                throw new ArgumentException(
                    "Normal-preparation samples must contain finite positions and normals.");
            }
            var normalLength = Math.Sqrt(
                sample.NormalX * sample.NormalX
                + sample.NormalY * sample.NormalY
                + sample.NormalZ * sample.NormalZ);
            if (!double.IsFinite(normalLength)
                || Math.Abs(normalLength - 1.0) > 1e-9)
            {
                throw new ArgumentException(
                    "Normal-preparation samples must contain unit normals.");
            }
            previousRow = sample.Row;
            previousColumn = sample.Column;
        }

        double? expectedNormalX = validation is null ? null : validation.ExpectedNormalX;
        double? expectedNormalY = validation is null ? null : validation.ExpectedNormalY;
        double? expectedNormalZ = validation is null ? null : validation.ExpectedNormalZ;
        double? minimumAlignmentCosine = validation is null ? null : validation.MinimumAlignmentCosine;
        if (validation is null)
        {
            if (validationState != C3DHeightMapNormalValidationState.NotRequested
                || validatedNormalCount != 0
                || consistentNormalCount != 0
                || reversedNormalCount != 0
                || minimumAlignment is not null
                || meanAlignment is not null
                || maximumAngularErrorDegrees is not null)
            {
                throw new ArgumentException(
                    "Unrequested normal validation must not contain validation evidence.");
            }
        }
        else
        {
            var expectedLength = Math.Sqrt(
                expectedNormalX!.Value * expectedNormalX.Value
                + expectedNormalY!.Value * expectedNormalY.Value
                + expectedNormalZ!.Value * expectedNormalZ.Value);
            if (new[] { expectedNormalX.Value, expectedNormalY.Value, expectedNormalZ.Value }
                    .Any(value => !double.IsFinite(value))
                || !double.IsFinite(expectedLength)
                || expectedLength <= 0.0
                || !double.IsFinite(minimumAlignmentCosine!.Value)
                || minimumAlignmentCosine.Value < -1.0
                || minimumAlignmentCosine.Value > 1.0
                || validatedNormalCount != calculatedNormalCount
                || consistentNormalCount < 0
                || consistentNormalCount > validatedNormalCount
                || reversedNormalCount < 0
                || reversedNormalCount > validatedNormalCount
                || !minimumAlignment.HasValue
                || !meanAlignment.HasValue
                || !maximumAngularErrorDegrees.HasValue
                || !double.IsFinite(minimumAlignment.Value)
                || !double.IsFinite(meanAlignment.Value)
                || !double.IsFinite(maximumAngularErrorDegrees.Value)
                || validationState == C3DHeightMapNormalValidationState.NotRequested)
            {
                throw new ArgumentException(
                    "Requested normal validation evidence is invalid.");
            }
        }

        (double? X, double? Y, double? Z) normalizedExpected;
        if (validation is null)
        {
            normalizedExpected = (null, null, null);
        }
        else
        {
            var expectedLength = Math.Sqrt(
                expectedNormalX!.Value * expectedNormalX.Value
                + expectedNormalY!.Value * expectedNormalY.Value
                + expectedNormalZ!.Value * expectedNormalZ.Value);
            normalizedExpected = (
                expectedNormalX.Value / expectedLength,
                expectedNormalY!.Value / expectedLength,
                expectedNormalZ!.Value / expectedLength);
        }
        var outputContentSha256 = CalculateOutputContentSha256(
            sourceEntityId,
            sourceContentSha256,
            outputEntityId,
            unit,
            frameId,
            rowCount,
            columnCount,
            inputFiniteSampleCount,
            calculatedNormalCount,
            unavailableNormalCount,
            centralDerivativeCount,
            oneSidedDerivativeCount,
            missingDerivativeCount,
            samples,
            normalizedExpected,
            minimumAlignmentCosine,
            validationState,
            validatedNormalCount,
            consistentNormalCount,
            reversedNormalCount,
            minimumAlignment,
            meanAlignment,
            maximumAngularErrorDegrees);
        var outputRootSourceSha256 = sourceRootSourceSha256.ToUpperInvariant();
        var contentSha256 = CalculateContentSha256(
            stepId,
            sourceEntityId,
            sourceContentSha256,
            sourceRootSourceSha256,
            sourceByteLength,
            outputEntityId,
            outputContentSha256,
            outputRootSourceSha256,
            unit,
            frameId,
            rowCount,
            columnCount,
            inputFiniteSampleCount,
            calculatedNormalCount,
            unavailableNormalCount,
            centralDerivativeCount,
            oneSidedDerivativeCount,
            missingDerivativeCount,
            samples,
            validationState,
            normalizedExpected,
            minimumAlignmentCosine,
            validatedNormalCount,
            consistentNormalCount,
            reversedNormalCount,
            minimumAlignment,
            meanAlignment,
            maximumAngularErrorDegrees,
            provenance);
        return new C3DHeightMapNormalPreparationEvidence(
            stepId,
            sourceEntityId,
            sourceContentSha256.ToUpperInvariant(),
            outputRootSourceSha256,
            sourceByteLength,
            outputEntityId,
            outputContentSha256,
            outputRootSourceSha256,
            unit,
            frameId,
            CoordinateConventionName,
            rowCount,
            columnCount,
            inputFiniteSampleCount,
            calculatedNormalCount,
            unavailableNormalCount,
            centralDerivativeCount,
            oneSidedDerivativeCount,
            missingDerivativeCount,
            samples,
            validationState,
            normalizedExpected.X,
            normalizedExpected.Y,
            normalizedExpected.Z,
            minimumAlignmentCosine,
            validatedNormalCount,
            consistentNormalCount,
            reversedNormalCount,
            minimumAlignment,
            meanAlignment,
            maximumAngularErrorDegrees,
            provenance,
            contentSha256);
    }

    private static string CalculateOutputContentSha256(
        string sourceEntityId,
        string sourceContentSha256,
        string outputEntityId,
        string unit,
        string frameId,
        int rowCount,
        int columnCount,
        int inputFiniteSampleCount,
        int calculatedNormalCount,
        int unavailableNormalCount,
        int centralDerivativeCount,
        int oneSidedDerivativeCount,
        int missingDerivativeCount,
        IReadOnlyList<C3DHeightMapNormalSample> samples,
        (double? X, double? Y, double? Z) expectedNormal,
        double? minimumAlignmentCosine,
        C3DHeightMapNormalValidationState validationState,
        int validatedNormalCount,
        int consistentNormalCount,
        int reversedNormalCount,
        double? minimumAlignment,
        double? meanAlignment,
        double? maximumAngularErrorDegrees)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write("OpenVisionLab.C3DHeightMapNormalArtifact");
        writer.Write(ContractVersion);
        writer.Write(sourceEntityId);
        writer.Write(sourceContentSha256.ToUpperInvariant());
        writer.Write(outputEntityId);
        writer.Write(unit);
        writer.Write(frameId);
        writer.Write(CoordinateConventionName);
        writer.Write(DerivativePolicyName);
        writer.Write(MissingPolicyName);
        writer.Write(rowCount);
        writer.Write(columnCount);
        writer.Write(inputFiniteSampleCount);
        writer.Write(calculatedNormalCount);
        writer.Write(unavailableNormalCount);
        writer.Write(centralDerivativeCount);
        writer.Write(oneSidedDerivativeCount);
        writer.Write(missingDerivativeCount);
        WriteNullable(writer, expectedNormal.X);
        WriteNullable(writer, expectedNormal.Y);
        WriteNullable(writer, expectedNormal.Z);
        WriteNullable(writer, minimumAlignmentCosine);
        writer.Write((int)validationState);
        writer.Write(validatedNormalCount);
        writer.Write(consistentNormalCount);
        writer.Write(reversedNormalCount);
        WriteNullable(writer, minimumAlignment);
        WriteNullable(writer, meanAlignment);
        WriteNullable(writer, maximumAngularErrorDegrees);
        writer.Write(samples.Count);
        foreach (var sample in samples)
        {
            WriteSample(writer, sample);
        }

        writer.Flush();
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static string CalculateContentSha256(
        string stepId,
        string sourceEntityId,
        string sourceContentSha256,
        string sourceRootSourceSha256,
        long sourceByteLength,
        string outputEntityId,
        string outputContentSha256,
        string outputRootSourceSha256,
        string unit,
        string frameId,
        int rowCount,
        int columnCount,
        int inputFiniteSampleCount,
        int calculatedNormalCount,
        int unavailableNormalCount,
        int centralDerivativeCount,
        int oneSidedDerivativeCount,
        int missingDerivativeCount,
        IReadOnlyList<C3DHeightMapNormalSample> samples,
        C3DHeightMapNormalValidationState validationState,
        (double? X, double? Y, double? Z) expectedNormal,
        double? minimumAlignmentCosine,
        int validatedNormalCount,
        int consistentNormalCount,
        int reversedNormalCount,
        double? minimumAlignment,
        double? meanAlignment,
        double? maximumAngularErrorDegrees,
        string provenance)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write("OpenVisionLab.C3DHeightMapNormalPreparationEvidence");
        writer.Write(ContractVersion);
        writer.Write(stepId);
        writer.Write(sourceEntityId);
        writer.Write(sourceContentSha256.ToUpperInvariant());
        writer.Write(sourceRootSourceSha256.ToUpperInvariant());
        writer.Write(sourceByteLength);
        writer.Write(outputEntityId);
        writer.Write(outputContentSha256);
        writer.Write(outputRootSourceSha256);
        writer.Write(unit);
        writer.Write(frameId);
        writer.Write(CoordinateConventionName);
        writer.Write(DerivativePolicyName);
        writer.Write(MissingPolicyName);
        writer.Write(LineagePolicyName);
        writer.Write(rowCount);
        writer.Write(columnCount);
        writer.Write(inputFiniteSampleCount);
        writer.Write(calculatedNormalCount);
        writer.Write(unavailableNormalCount);
        writer.Write(centralDerivativeCount);
        writer.Write(oneSidedDerivativeCount);
        writer.Write(missingDerivativeCount);
        writer.Write((int)validationState);
        WriteNullable(writer, expectedNormal.X);
        WriteNullable(writer, expectedNormal.Y);
        WriteNullable(writer, expectedNormal.Z);
        WriteNullable(writer, minimumAlignmentCosine);
        writer.Write(validatedNormalCount);
        writer.Write(consistentNormalCount);
        writer.Write(reversedNormalCount);
        WriteNullable(writer, minimumAlignment);
        WriteNullable(writer, meanAlignment);
        WriteNullable(writer, maximumAngularErrorDegrees);
        writer.Write(samples.Count);
        foreach (var sample in samples)
        {
            WriteSample(writer, sample);
        }

        writer.Write(provenance);
        writer.Flush();
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static void WriteSample(BinaryWriter writer, C3DHeightMapNormalSample sample)
    {
        writer.Write(sample.Row);
        writer.Write(sample.Column);
        writer.Write(sample.PositionX);
        writer.Write(sample.PositionY);
        writer.Write(sample.PositionZ);
        writer.Write(sample.NormalX);
        writer.Write(sample.NormalY);
        writer.Write(sample.NormalZ);
        writer.Write(sample.CentralColumnDerivative);
        writer.Write(sample.CentralRowDerivative);
    }

    private static void WriteNullable(BinaryWriter writer, double? value)
    {
        writer.Write(value.HasValue);
        if (value.HasValue)
        {
            writer.Write(value.Value);
        }
    }

    private static bool IsSha256(string value) =>
        value is not null
        && value.Length == 64
        && value.All(Uri.IsHexDigit);
}
