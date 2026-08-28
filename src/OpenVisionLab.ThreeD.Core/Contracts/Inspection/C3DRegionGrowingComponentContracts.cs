using System.Security.Cryptography;
using System.Text;

namespace OpenVisionLab.ThreeD.Core;

public enum C3DRegionGrowingComponentMode
{
    SelectConnectedRegion
}

/// <summary>
/// Immutable evidence for projecting one validated G-11 connected region into
/// a separate same-grid height-field component. The contract stores identities
/// and counts only; source bytes remain outside the evidence.
/// </summary>
public sealed class C3DRegionGrowingComponentEvidence
{
    public const string ContractVersion = "1.0";
    public const string ProjectionPolicyName = "SelectedConnectedRegionCells";
    public const string MissingValuePolicyName = "PreserveExistingMissingSetOutsideComponent";
    public const string LineagePolicyName = "SeparateCurrentDerivedHeightField";
    public const string CoordinatePolicyName = "ExactSourceGrid";

    private C3DRegionGrowingComponentEvidence(
        string stepId,
        string sourceEntityId,
        string sourceContentSha256,
        string sourceRootSourceSha256,
        long sourceByteLength,
        string connectedRegionArtifactId,
        string connectedRegionContentSha256,
        string connectedRegionMaskContentSha256,
        int selectedRegionIndex,
        string connectivity,
        string outputEntityId,
        string outputContentSha256,
        string outputRootSourceSha256,
        string unit,
        string frameId,
        int gridWidth,
        int gridHeight,
        int selectedCellCount,
        int inputValidSampleCount,
        int inputMissingSampleCount,
        int retainedValidSampleCount,
        int reducedBackgroundSampleCount,
        C3DRegionGrowingComponentMode mode,
        string provenance,
        string contentSha256)
    {
        StepId = stepId;
        SourceEntityId = sourceEntityId;
        SourceContentSha256 = sourceContentSha256;
        SourceRootSourceSha256 = sourceRootSourceSha256;
        SourceByteLength = sourceByteLength;
        ConnectedRegionArtifactId = connectedRegionArtifactId;
        ConnectedRegionContentSha256 = connectedRegionContentSha256;
        ConnectedRegionMaskContentSha256 = connectedRegionMaskContentSha256;
        SelectedRegionIndex = selectedRegionIndex;
        Connectivity = connectivity;
        OutputEntityId = outputEntityId;
        OutputContentSha256 = outputContentSha256;
        OutputRootSourceSha256 = outputRootSourceSha256;
        Unit = unit;
        FrameId = frameId;
        GridWidth = gridWidth;
        GridHeight = gridHeight;
        SelectedCellCount = selectedCellCount;
        InputValidSampleCount = inputValidSampleCount;
        InputMissingSampleCount = inputMissingSampleCount;
        RetainedValidSampleCount = retainedValidSampleCount;
        ReducedBackgroundSampleCount = reducedBackgroundSampleCount;
        Mode = mode;
        Provenance = provenance;
        ContentSha256 = contentSha256;
    }

    public string StepId { get; }
    public string SourceEntityId { get; }
    public string SourceContentSha256 { get; }
    public string SourceRootSourceSha256 { get; }
    public long SourceByteLength { get; }
    public string ConnectedRegionArtifactId { get; }
    public string ConnectedRegionContentSha256 { get; }
    public string ConnectedRegionMaskContentSha256 { get; }
    public int SelectedRegionIndex { get; }
    public string Connectivity { get; }
    public string OutputEntityId { get; }
    public string OutputContentSha256 { get; }
    public string OutputRootSourceSha256 { get; }
    public string Unit { get; }
    public string FrameId { get; }
    public int GridWidth { get; }
    public int GridHeight { get; }
    public int SelectedCellCount { get; }
    public int InputValidSampleCount { get; }
    public int InputMissingSampleCount { get; }
    public int RetainedValidSampleCount { get; }
    public int ReducedBackgroundSampleCount { get; }
    public C3DRegionGrowingComponentMode Mode { get; }
    public string ProjectionPolicy => ProjectionPolicyName;
    public string MissingValuePolicy => MissingValuePolicyName;
    public string LineagePolicy => LineagePolicyName;
    public string CoordinatePolicy => CoordinatePolicyName;
    public int OutputValidSampleCount => RetainedValidSampleCount;
    public int OutputMissingSampleCount =>
        InputMissingSampleCount + ReducedBackgroundSampleCount;
    public bool HasFiniteComponent => RetainedValidSampleCount > 0;
    public string Provenance { get; }
    public string ContentSha256 { get; }

    public static C3DRegionGrowingComponentEvidence Create(
        string stepId,
        string sourceEntityId,
        string sourceContentSha256,
        string sourceRootSourceSha256,
        long sourceByteLength,
        string connectedRegionArtifactId,
        string connectedRegionContentSha256,
        string connectedRegionMaskContentSha256,
        int selectedRegionIndex,
        string connectivity,
        string outputEntityId,
        string outputContentSha256,
        string outputRootSourceSha256,
        string unit,
        string frameId,
        int gridWidth,
        int gridHeight,
        int selectedCellCount,
        int inputValidSampleCount,
        int inputMissingSampleCount,
        int retainedValidSampleCount,
        int reducedBackgroundSampleCount,
        C3DRegionGrowingComponentMode mode,
        string provenance)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stepId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceEntityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectedRegionArtifactId);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectivity);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputEntityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(unit);
        ArgumentException.ThrowIfNullOrWhiteSpace(frameId);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        if (!IsSha256(sourceContentSha256)
            || !IsSha256(sourceRootSourceSha256)
            || !IsSha256(connectedRegionContentSha256)
            || !IsSha256(connectedRegionMaskContentSha256)
            || !IsSha256(outputContentSha256)
            || !IsSha256(outputRootSourceSha256))
        {
            throw new ArgumentException(
                "Region-growing component evidence requires SHA-256 identities.");
        }

        if (sourceByteLength <= 0
            || selectedRegionIndex < 0
            || !Enum.IsDefined(mode)
            || !string.Equals(
                outputRootSourceSha256,
                sourceRootSourceSha256,
                StringComparison.OrdinalIgnoreCase)
            || string.Equals(sourceEntityId, outputEntityId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(connectedRegionArtifactId, outputEntityId, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "Region-growing component evidence contains an invalid identity, lineage, index, or mode.");
        }

        if (!string.Equals(connectivity, C3DConnectedRegionArtifact.FourConnectivity, StringComparison.Ordinal)
            && !string.Equals(connectivity, C3DConnectedRegionArtifact.EightConnectivity, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Region-growing component evidence connectivity must be Four or Eight.");
        }

        if (gridWidth <= 0
            || gridHeight <= 0
            || selectedCellCount <= 0
            || inputValidSampleCount < 1
            || inputMissingSampleCount < 0
            || retainedValidSampleCount < 0
            || reducedBackgroundSampleCount < 0)
        {
            throw new ArgumentException(
                "Region-growing component evidence contains an invalid grid or count.");
        }

        var cellCount = checked(gridWidth * gridHeight);
        if (selectedCellCount > cellCount
            || inputValidSampleCount + inputMissingSampleCount != cellCount
            || retainedValidSampleCount + reducedBackgroundSampleCount != inputValidSampleCount
            || retainedValidSampleCount > selectedCellCount)
        {
            throw new ArgumentException(
                "Region-growing component evidence counts do not match the source grid.");
        }

        var normalizedSourceContent = sourceContentSha256.ToUpperInvariant();
        var normalizedSourceRoot = sourceRootSourceSha256.ToUpperInvariant();
        var normalizedRegionContent = connectedRegionContentSha256.ToUpperInvariant();
        var normalizedMaskContent = connectedRegionMaskContentSha256.ToUpperInvariant();
        var normalizedOutputContent = outputContentSha256.ToUpperInvariant();
        var normalizedOutputRoot = outputRootSourceSha256.ToUpperInvariant();
        var contentSha256 = CalculateContentSha256(
            stepId,
            sourceEntityId,
            normalizedSourceContent,
            normalizedSourceRoot,
            sourceByteLength,
            connectedRegionArtifactId,
            normalizedRegionContent,
            normalizedMaskContent,
            selectedRegionIndex,
            connectivity,
            outputEntityId,
            normalizedOutputContent,
            normalizedOutputRoot,
            unit,
            frameId,
            gridWidth,
            gridHeight,
            selectedCellCount,
            inputValidSampleCount,
            inputMissingSampleCount,
            retainedValidSampleCount,
            reducedBackgroundSampleCount,
            mode);
        return new C3DRegionGrowingComponentEvidence(
            stepId,
            sourceEntityId,
            normalizedSourceContent,
            normalizedSourceRoot,
            sourceByteLength,
            connectedRegionArtifactId,
            normalizedRegionContent,
            normalizedMaskContent,
            selectedRegionIndex,
            connectivity,
            outputEntityId,
            normalizedOutputContent,
            normalizedOutputRoot,
            unit,
            frameId,
            gridWidth,
            gridHeight,
            selectedCellCount,
            inputValidSampleCount,
            inputMissingSampleCount,
            retainedValidSampleCount,
            reducedBackgroundSampleCount,
            mode,
            provenance,
            contentSha256);
    }

    private static string CalculateContentSha256(
        string stepId,
        string sourceEntityId,
        string sourceContentSha256,
        string sourceRootSourceSha256,
        long sourceByteLength,
        string connectedRegionArtifactId,
        string connectedRegionContentSha256,
        string connectedRegionMaskContentSha256,
        int selectedRegionIndex,
        string connectivity,
        string outputEntityId,
        string outputContentSha256,
        string outputRootSourceSha256,
        string unit,
        string frameId,
        int gridWidth,
        int gridHeight,
        int selectedCellCount,
        int inputValidSampleCount,
        int inputMissingSampleCount,
        int retainedValidSampleCount,
        int reducedBackgroundSampleCount,
        C3DRegionGrowingComponentMode mode)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write("OpenVisionLab.C3DRegionGrowingComponentEvidence");
        writer.Write(ContractVersion);
        writer.Write(stepId);
        writer.Write(sourceEntityId);
        writer.Write(sourceContentSha256);
        writer.Write(sourceRootSourceSha256);
        writer.Write(sourceByteLength);
        writer.Write(connectedRegionArtifactId);
        writer.Write(connectedRegionContentSha256);
        writer.Write(connectedRegionMaskContentSha256);
        writer.Write(selectedRegionIndex);
        writer.Write(connectivity);
        writer.Write(outputEntityId);
        writer.Write(outputContentSha256);
        writer.Write(outputRootSourceSha256);
        writer.Write(unit);
        writer.Write(frameId);
        writer.Write(gridWidth);
        writer.Write(gridHeight);
        writer.Write(selectedCellCount);
        writer.Write(inputValidSampleCount);
        writer.Write(inputMissingSampleCount);
        writer.Write(retainedValidSampleCount);
        writer.Write(reducedBackgroundSampleCount);
        writer.Write((int)mode);
        writer.Write(ProjectionPolicyName);
        writer.Write(MissingValuePolicyName);
        writer.Write(LineagePolicyName);
        writer.Write(CoordinatePolicyName);
        writer.Flush();
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static bool IsSha256(string? value) =>
        value is not null
        && value.Length == 64
        && value.All(character => character is >= '0' and <= '9'
            or >= 'A' and <= 'F'
            or >= 'a' and <= 'f');
}
