using System.Security.Cryptography;
using System.Text;

namespace OpenVisionLab.ThreeD.Core;

public enum C3DHeightBackgroundSubtractionMode
{
    CurrentMinusSavedBackground
}

/// <summary>
/// Immutable evidence for one exact-grid signed current-minus-background
/// subtraction. Source bytes are never embedded in the evidence.
/// </summary>
public sealed class C3DHeightBackgroundSubtractionEvidence
{
    public const string ContractVersion = "1.0";
    public const string SubtractionPolicyName = "CurrentMinusSavedBackground";
    public const string GridPolicyName = "ExactDimensionsOriginPitchUnitFrame";
    public const string MissingValuePolicyName = "MissingIfEitherInputMissing";
    public const string ZeroDeltaPolicyName = "RejectFiniteZeroForC3DEncoding";

    private C3DHeightBackgroundSubtractionEvidence(
        string stepId,
        string currentSourceEntityId,
        string currentSourceContentSha256,
        string currentRootSourceSha256,
        long currentByteLength,
        string backgroundEntityId,
        string backgroundContentSha256,
        string backgroundRootSourceSha256,
        long backgroundByteLength,
        string outputEntityId,
        string outputContentSha256,
        string outputRootSourceSha256,
        string unit,
        string frameId,
        int width,
        int height,
        C3DHeightBackgroundSubtractionMode mode,
        int currentValidSampleCount,
        int backgroundValidSampleCount,
        int pairedValidSampleCount,
        int missingEitherSampleCount,
        int zeroDeltaSampleCount,
        int positiveDeltaSampleCount,
        int negativeDeltaSampleCount,
        string provenance,
        string contentSha256)
    {
        StepId = stepId;
        CurrentSourceEntityId = currentSourceEntityId;
        CurrentSourceContentSha256 = currentSourceContentSha256;
        CurrentRootSourceSha256 = currentRootSourceSha256;
        CurrentByteLength = currentByteLength;
        BackgroundEntityId = backgroundEntityId;
        BackgroundContentSha256 = backgroundContentSha256;
        BackgroundRootSourceSha256 = backgroundRootSourceSha256;
        BackgroundByteLength = backgroundByteLength;
        OutputEntityId = outputEntityId;
        OutputContentSha256 = outputContentSha256;
        OutputRootSourceSha256 = outputRootSourceSha256;
        Unit = unit;
        FrameId = frameId;
        Width = width;
        Height = height;
        Mode = mode;
        CurrentValidSampleCount = currentValidSampleCount;
        BackgroundValidSampleCount = backgroundValidSampleCount;
        PairedValidSampleCount = pairedValidSampleCount;
        MissingEitherSampleCount = missingEitherSampleCount;
        ZeroDeltaSampleCount = zeroDeltaSampleCount;
        PositiveDeltaSampleCount = positiveDeltaSampleCount;
        NegativeDeltaSampleCount = negativeDeltaSampleCount;
        Provenance = provenance;
        ContentSha256 = contentSha256;
    }

    public string StepId { get; }
    public string CurrentSourceEntityId { get; }
    public string CurrentSourceContentSha256 { get; }
    public string CurrentRootSourceSha256 { get; }
    public long CurrentByteLength { get; }
    public string BackgroundEntityId { get; }
    public string BackgroundContentSha256 { get; }
    public string BackgroundRootSourceSha256 { get; }
    public long BackgroundByteLength { get; }
    public string OutputEntityId { get; }
    public string OutputContentSha256 { get; }
    public string OutputRootSourceSha256 { get; }
    public string Unit { get; }
    public string FrameId { get; }
    public int Width { get; }
    public int Height { get; }
    public C3DHeightBackgroundSubtractionMode Mode { get; }
    public string SubtractionPolicy => SubtractionPolicyName;
    public string GridPolicy => GridPolicyName;
    public string MissingValuePolicy => MissingValuePolicyName;
    public string ZeroDeltaPolicy => ZeroDeltaPolicyName;
    public int CurrentValidSampleCount { get; }
    public int BackgroundValidSampleCount { get; }
    public int PairedValidSampleCount { get; }
    public int MissingEitherSampleCount { get; }
    public int ZeroDeltaSampleCount { get; }
    public int PositiveDeltaSampleCount { get; }
    public int NegativeDeltaSampleCount { get; }
    public int OutputValidSampleCount => PairedValidSampleCount;
    public int OutputMissingSampleCount => MissingEitherSampleCount;
    public string Provenance { get; }
    public string ContentSha256 { get; }

    public static C3DHeightBackgroundSubtractionEvidence Create(
        string stepId,
        string currentSourceEntityId,
        string currentSourceContentSha256,
        string currentRootSourceSha256,
        long currentByteLength,
        string backgroundEntityId,
        string backgroundContentSha256,
        string backgroundRootSourceSha256,
        long backgroundByteLength,
        string outputEntityId,
        string outputContentSha256,
        string outputRootSourceSha256,
        string unit,
        string frameId,
        int width,
        int height,
        C3DHeightBackgroundSubtractionMode mode,
        int currentValidSampleCount,
        int backgroundValidSampleCount,
        int pairedValidSampleCount,
        int missingEitherSampleCount,
        int zeroDeltaSampleCount,
        int positiveDeltaSampleCount,
        int negativeDeltaSampleCount,
        string provenance)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stepId);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentSourceEntityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentSourceContentSha256);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentRootSourceSha256);
        ArgumentException.ThrowIfNullOrWhiteSpace(backgroundEntityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(backgroundContentSha256);
        ArgumentException.ThrowIfNullOrWhiteSpace(backgroundRootSourceSha256);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputEntityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputContentSha256);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputRootSourceSha256);
        ArgumentException.ThrowIfNullOrWhiteSpace(unit);
        ArgumentException.ThrowIfNullOrWhiteSpace(frameId);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        if (!IsSha256(currentSourceContentSha256)
            || !IsSha256(currentRootSourceSha256)
            || !IsSha256(backgroundContentSha256)
            || !IsSha256(backgroundRootSourceSha256)
            || !IsSha256(outputContentSha256)
            || !IsSha256(outputRootSourceSha256))
        {
            throw new ArgumentException("Background subtraction evidence requires SHA-256 identities.");
        }

        if (string.Equals(outputEntityId, currentSourceEntityId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(outputEntityId, backgroundEntityId, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Background subtraction output identity must differ from both inputs.");
        }

        if (currentByteLength <= 0 || backgroundByteLength <= 0
            || width <= 0 || height <= 0
            || !Enum.IsDefined(mode)
            || currentValidSampleCount < 1
            || backgroundValidSampleCount < 1
            || pairedValidSampleCount < 1
            || missingEitherSampleCount < 0
            || zeroDeltaSampleCount != 0
            || positiveDeltaSampleCount < 0
            || negativeDeltaSampleCount < 0)
        {
            throw new ArgumentException("Background subtraction evidence contains an invalid identity, grid, mode, count, or zero-delta state.");
        }

        var cellCount = checked(width * height);
        if (currentValidSampleCount > cellCount
            || backgroundValidSampleCount > cellCount
            || currentValidSampleCount + (cellCount - currentValidSampleCount) != cellCount
            || backgroundValidSampleCount + (cellCount - backgroundValidSampleCount) != cellCount
            || pairedValidSampleCount + missingEitherSampleCount != cellCount
            || positiveDeltaSampleCount + negativeDeltaSampleCount + zeroDeltaSampleCount != pairedValidSampleCount)
        {
            throw new ArgumentException("Background subtraction counts do not match the source grid.");
        }

        var contentSha256 = CalculateContentSha256(
            stepId,
            currentSourceEntityId,
            currentSourceContentSha256,
            currentRootSourceSha256,
            currentByteLength,
            backgroundEntityId,
            backgroundContentSha256,
            backgroundRootSourceSha256,
            backgroundByteLength,
            outputEntityId,
            outputContentSha256,
            outputRootSourceSha256,
            unit,
            frameId,
            width,
            height,
            mode,
            currentValidSampleCount,
            backgroundValidSampleCount,
            pairedValidSampleCount,
            missingEitherSampleCount,
            zeroDeltaSampleCount,
            positiveDeltaSampleCount,
            negativeDeltaSampleCount);
        return new C3DHeightBackgroundSubtractionEvidence(
            stepId,
            currentSourceEntityId,
            currentSourceContentSha256,
            currentRootSourceSha256,
            currentByteLength,
            backgroundEntityId,
            backgroundContentSha256,
            backgroundRootSourceSha256,
            backgroundByteLength,
            outputEntityId,
            outputContentSha256,
            outputRootSourceSha256,
            unit,
            frameId,
            width,
            height,
            mode,
            currentValidSampleCount,
            backgroundValidSampleCount,
            pairedValidSampleCount,
            missingEitherSampleCount,
            zeroDeltaSampleCount,
            positiveDeltaSampleCount,
            negativeDeltaSampleCount,
            provenance,
            contentSha256);
    }

    private static string CalculateContentSha256(
        string stepId,
        string currentSourceEntityId,
        string currentSourceContentSha256,
        string currentRootSourceSha256,
        long currentByteLength,
        string backgroundEntityId,
        string backgroundContentSha256,
        string backgroundRootSourceSha256,
        long backgroundByteLength,
        string outputEntityId,
        string outputContentSha256,
        string outputRootSourceSha256,
        string unit,
        string frameId,
        int width,
        int height,
        C3DHeightBackgroundSubtractionMode mode,
        int currentValidSampleCount,
        int backgroundValidSampleCount,
        int pairedValidSampleCount,
        int missingEitherSampleCount,
        int zeroDeltaSampleCount,
        int positiveDeltaSampleCount,
        int negativeDeltaSampleCount)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write("OpenVisionLab.C3DHeightBackgroundSubtractionEvidence");
        writer.Write(ContractVersion);
        writer.Write(stepId);
        writer.Write(currentSourceEntityId);
        writer.Write(currentSourceContentSha256.ToUpperInvariant());
        writer.Write(currentRootSourceSha256.ToUpperInvariant());
        writer.Write(currentByteLength);
        writer.Write(backgroundEntityId);
        writer.Write(backgroundContentSha256.ToUpperInvariant());
        writer.Write(backgroundRootSourceSha256.ToUpperInvariant());
        writer.Write(backgroundByteLength);
        writer.Write(outputEntityId);
        writer.Write(outputContentSha256.ToUpperInvariant());
        writer.Write(outputRootSourceSha256.ToUpperInvariant());
        writer.Write(unit);
        writer.Write(frameId);
        writer.Write(width);
        writer.Write(height);
        writer.Write((int)mode);
        writer.Write(SubtractionPolicyName);
        writer.Write(GridPolicyName);
        writer.Write(MissingValuePolicyName);
        writer.Write(ZeroDeltaPolicyName);
        writer.Write(currentValidSampleCount);
        writer.Write(backgroundValidSampleCount);
        writer.Write(pairedValidSampleCount);
        writer.Write(missingEitherSampleCount);
        writer.Write(zeroDeltaSampleCount);
        writer.Write(positiveDeltaSampleCount);
        writer.Write(negativeDeltaSampleCount);
        writer.Flush();
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static bool IsSha256(string value)
    {
        if (value.Length != 64)
        {
            return false;
        }

        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (!((character >= '0' && character <= '9')
                || (character >= 'A' && character <= 'F')
                || (character >= 'a' && character <= 'f')))
            {
                return false;
            }
        }

        return true;
    }
}
