using System.Security.Cryptography;
using System.Text;

namespace OpenVisionLab.ThreeD.Core;

public enum C3DHeightThresholdBackgroundRemovalMode
{
    KeepAtOrAboveThreshold,
    KeepAtOrBelowThreshold
}

/// <summary>
/// Immutable evidence for one same-grid raw-height threshold projection.
/// It records policy and lineage, but never stores source bytes or mutates the
/// source field.
/// </summary>
public sealed class C3DHeightThresholdBackgroundRemovalEvidence
{
    public const string ContractVersion = "1.0";
    public const string ComparisonPolicyName = "InclusiveFiniteSamplePredicate";
    public const string MissingValuePolicyName = "PreserveExistingMissing";
    public const string BackgroundPolicyName = "FailPredicateToMissing";

    private C3DHeightThresholdBackgroundRemovalEvidence(
        string stepId,
        string sourceEntityId,
        string sourceContentSha256,
        string rootSourceSha256,
        string outputEntityId,
        string outputContentSha256,
        string unit,
        string frameId,
        int width,
        int height,
        double threshold,
        C3DHeightThresholdBackgroundRemovalMode mode,
        int inputValidSampleCount,
        int inputMissingSampleCount,
        int retainedValidSampleCount,
        int removedBackgroundSampleCount,
        string provenance,
        string contentSha256)
    {
        StepId = stepId;
        SourceEntityId = sourceEntityId;
        SourceContentSha256 = sourceContentSha256;
        RootSourceSha256 = rootSourceSha256;
        OutputEntityId = outputEntityId;
        OutputContentSha256 = outputContentSha256;
        Unit = unit;
        FrameId = frameId;
        Width = width;
        Height = height;
        Threshold = threshold;
        Mode = mode;
        InputValidSampleCount = inputValidSampleCount;
        InputMissingSampleCount = inputMissingSampleCount;
        RetainedValidSampleCount = retainedValidSampleCount;
        RemovedBackgroundSampleCount = removedBackgroundSampleCount;
        Provenance = provenance;
        ContentSha256 = contentSha256;
    }

    public string StepId { get; }
    public string SourceEntityId { get; }
    public string SourceContentSha256 { get; }
    public string RootSourceSha256 { get; }
    public string OutputEntityId { get; }
    public string OutputContentSha256 { get; }
    public string Unit { get; }
    public string FrameId { get; }
    public int Width { get; }
    public int Height { get; }
    public double Threshold { get; }
    public C3DHeightThresholdBackgroundRemovalMode Mode { get; }
    public string ComparisonPolicy => ComparisonPolicyName;
    public string MissingValuePolicy => MissingValuePolicyName;
    public string BackgroundPolicy => BackgroundPolicyName;
    public int InputValidSampleCount { get; }
    public int InputMissingSampleCount { get; }
    public int RetainedValidSampleCount { get; }
    public int RemovedBackgroundSampleCount { get; }
    public int OutputValidSampleCount => RetainedValidSampleCount;
    public int OutputMissingSampleCount =>
        InputMissingSampleCount + RemovedBackgroundSampleCount;
    public bool HasForeground => RetainedValidSampleCount > 0;
    public string Provenance { get; }
    public string ContentSha256 { get; }

    public static C3DHeightThresholdBackgroundRemovalEvidence Create(
        string stepId,
        string sourceEntityId,
        string sourceContentSha256,
        string rootSourceSha256,
        string outputEntityId,
        string outputContentSha256,
        string unit,
        string frameId,
        int width,
        int height,
        double threshold,
        C3DHeightThresholdBackgroundRemovalMode mode,
        int inputValidSampleCount,
        int inputMissingSampleCount,
        int retainedValidSampleCount,
        int removedBackgroundSampleCount,
        string provenance)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stepId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceEntityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceContentSha256);
        ArgumentException.ThrowIfNullOrWhiteSpace(rootSourceSha256);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputEntityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputContentSha256);
        ArgumentException.ThrowIfNullOrWhiteSpace(unit);
        ArgumentException.ThrowIfNullOrWhiteSpace(frameId);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        if (string.Equals(sourceEntityId, outputEntityId, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Height threshold background-removal output identity must differ from the source.");
        }
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Height threshold background-removal dimensions must be positive.");
        }
        if (!double.IsFinite(threshold)
            || !Enum.IsDefined(mode)
            || inputValidSampleCount < 1
            || inputMissingSampleCount < 0
            || retainedValidSampleCount < 0
            || removedBackgroundSampleCount < 0)
        {
            throw new ArgumentException("Height threshold background-removal evidence contains an invalid threshold, mode, or count.");
        }

        var cellCount = checked(width * height);
        if (inputValidSampleCount + inputMissingSampleCount != cellCount
            || retainedValidSampleCount + removedBackgroundSampleCount != inputValidSampleCount)
        {
            throw new ArgumentException("Height threshold background-removal counts do not match the source grid.");
        }

        var contentSha256 = CalculateContentSha256(
            stepId,
            sourceEntityId,
            sourceContentSha256,
            rootSourceSha256,
            outputEntityId,
            outputContentSha256,
            unit,
            frameId,
            width,
            height,
            threshold,
            mode,
            inputValidSampleCount,
            inputMissingSampleCount,
            retainedValidSampleCount,
            removedBackgroundSampleCount);
        return new C3DHeightThresholdBackgroundRemovalEvidence(
            stepId,
            sourceEntityId,
            sourceContentSha256,
            rootSourceSha256,
            outputEntityId,
            outputContentSha256,
            unit,
            frameId,
            width,
            height,
            threshold,
            mode,
            inputValidSampleCount,
            inputMissingSampleCount,
            retainedValidSampleCount,
            removedBackgroundSampleCount,
            provenance,
            contentSha256);
    }

    private static string CalculateContentSha256(
        string stepId,
        string sourceEntityId,
        string sourceContentSha256,
        string rootSourceSha256,
        string outputEntityId,
        string outputContentSha256,
        string unit,
        string frameId,
        int width,
        int height,
        double threshold,
        C3DHeightThresholdBackgroundRemovalMode mode,
        int inputValidSampleCount,
        int inputMissingSampleCount,
        int retainedValidSampleCount,
        int removedBackgroundSampleCount)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write("OpenVisionLab.C3DHeightThresholdBackgroundRemovalEvidence");
        writer.Write(ContractVersion);
        writer.Write(stepId);
        writer.Write(sourceEntityId);
        writer.Write(sourceContentSha256.ToUpperInvariant());
        writer.Write(rootSourceSha256.ToUpperInvariant());
        writer.Write(outputEntityId);
        writer.Write(outputContentSha256.ToUpperInvariant());
        writer.Write(unit);
        writer.Write(frameId);
        writer.Write(width);
        writer.Write(height);
        writer.Write(threshold);
        writer.Write((int)mode);
        writer.Write(ComparisonPolicyName);
        writer.Write(MissingValuePolicyName);
        writer.Write(BackgroundPolicyName);
        writer.Write(inputValidSampleCount);
        writer.Write(inputMissingSampleCount);
        writer.Write(retainedValidSampleCount);
        writer.Write(removedBackgroundSampleCount);
        writer.Flush();
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }
}
