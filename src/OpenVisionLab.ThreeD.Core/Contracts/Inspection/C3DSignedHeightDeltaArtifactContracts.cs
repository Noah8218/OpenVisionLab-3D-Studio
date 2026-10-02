using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace OpenVisionLab.ThreeD.Core;

/// <summary>
/// Explicit recipe policy for the missing-safe signed current-minus-background
/// route. The ordinary C3D wire format cannot represent a finite zero, so this
/// route keeps its result as a content-addressed in-memory artifact instead of
/// silently converting zero to missing.
/// </summary>
public static class C3DSignedHeightDeltaRecipePolicy
{
    public const string ToolId = "signed-delta-background-subtraction";
    public const string SubtractionPolicyName = "CurrentMinusSavedBackground";
    public const string GridPolicyName = "ExactDimensionsOriginPitchUnitFrame";
    public const string MissingValuePolicyName = "MissingIfEitherInputMissing";
    public const string ZeroDeltaPolicyName = "PreserveFiniteZeroInSignedDeltaArtifact";

    public const string BackgroundPathParameterName = "BackgroundPath";
    public const string BackgroundEntityIdParameterName = "BackgroundEntityId";
    public const string BackgroundByteLengthParameterName = "BackgroundByteLength";
    public const string BackgroundContentSha256ParameterName = "BackgroundContentSha256";
    public const string BackgroundWidthParameterName = "BackgroundWidth";
    public const string BackgroundHeightParameterName = "BackgroundHeight";
    public const string BackgroundUnitParameterName = "BackgroundUnit";
    public const string BackgroundFrameIdParameterName = "BackgroundFrameId";
    public const string SubtractionPolicyParameterName = "SubtractionPolicy";
    public const string MissingValuePolicyParameterName = "MissingValuePolicy";
    public const string ZeroDeltaPolicyParameterName = "ZeroDeltaPolicy";

    public static readonly IReadOnlyList<string> ParameterNames =
    [
        BackgroundPathParameterName,
        BackgroundEntityIdParameterName,
        BackgroundByteLengthParameterName,
        BackgroundContentSha256ParameterName,
        BackgroundWidthParameterName,
        BackgroundHeightParameterName,
        BackgroundUnitParameterName,
        BackgroundFrameIdParameterName,
        SubtractionPolicyParameterName,
        MissingValuePolicyParameterName,
        ZeroDeltaPolicyParameterName
    ];

    public static bool TryRead(
        IReadOnlyList<ToolRecipeParameter>? parameters,
        out C3DSignedHeightDeltaReference reference,
        out string message)
    {
        reference = null!;
        if (parameters is null
            || parameters.Count != ParameterNames.Count
            || ParameterNames.Any(name => parameters.Count(parameter => parameter?.Name == name) != 1))
        {
            message = "Signed Delta Background Subtraction requires exactly one value for every recognized parameter.";
            return false;
        }

        string Value(string name) => parameters.Single(parameter => parameter.Name == name).Value;
        var path = Value(BackgroundPathParameterName);
        var entityId = Value(BackgroundEntityIdParameterName);
        var hash = Value(BackgroundContentSha256ParameterName);
        var unit = Value(BackgroundUnitParameterName);
        var frameId = Value(BackgroundFrameIdParameterName);
        if (string.IsNullOrWhiteSpace(path)
            || string.IsNullOrWhiteSpace(entityId)
            || !IsSha256(hash)
            || string.IsNullOrWhiteSpace(unit)
            || string.IsNullOrWhiteSpace(frameId)
            || !long.TryParse(Value(BackgroundByteLengthParameterName), NumberStyles.Integer, CultureInfo.InvariantCulture, out var byteLength)
            || byteLength <= 0
            || !int.TryParse(Value(BackgroundWidthParameterName), NumberStyles.Integer, CultureInfo.InvariantCulture, out var width)
            || width <= 0
            || !int.TryParse(Value(BackgroundHeightParameterName), NumberStyles.Integer, CultureInfo.InvariantCulture, out var height)
            || height <= 0
            || !string.Equals(Value(SubtractionPolicyParameterName), SubtractionPolicyName, StringComparison.Ordinal)
            || !string.Equals(Value(MissingValuePolicyParameterName), MissingValuePolicyName, StringComparison.Ordinal)
            || !string.Equals(Value(ZeroDeltaPolicyParameterName), ZeroDeltaPolicyName, StringComparison.Ordinal))
        {
            message = "Signed Delta Background Subtraction parameters do not match the approved missing-safe contract.";
            return false;
        }

        reference = new C3DSignedHeightDeltaReference(
            path.Trim(),
            entityId.Trim(),
            byteLength,
            hash.Trim().ToUpperInvariant(),
            width,
            height,
            unit.Trim(),
            frameId.Trim());
        message = "Signed Delta Background Subtraction parameters are valid.";
        return true;
    }

    public static IReadOnlyList<ToolRecipeParameter> ToParameters(C3DSignedHeightDeltaReference reference) =>
    [
        new(BackgroundPathParameterName, reference.Path),
        new(BackgroundEntityIdParameterName, reference.EntityId),
        new(BackgroundByteLengthParameterName, reference.ByteLength.ToString(CultureInfo.InvariantCulture)),
        new(BackgroundContentSha256ParameterName, reference.ContentSha256),
        new(BackgroundWidthParameterName, reference.Width.ToString(CultureInfo.InvariantCulture)),
        new(BackgroundHeightParameterName, reference.Height.ToString(CultureInfo.InvariantCulture)),
        new(BackgroundUnitParameterName, reference.Unit),
        new(BackgroundFrameIdParameterName, reference.FrameId),
        new(SubtractionPolicyParameterName, SubtractionPolicyName),
        new(MissingValuePolicyParameterName, MissingValuePolicyName),
        new(ZeroDeltaPolicyParameterName, ZeroDeltaPolicyName)
    ];

    private static bool IsSha256(string value) =>
        value.Length == 64 && value.All(Uri.IsHexDigit);
}

public sealed record C3DSignedHeightDeltaReference(
    string Path,
    string EntityId,
    long ByteLength,
    string ContentSha256,
    int Width,
    int Height,
    string Unit,
    string FrameId);

/// <summary>
/// Content-addressed signed delta. Finite zero is a valid measured delta;
/// only NaN represents a missing/non-evaluated cell in this artifact.
/// </summary>
public sealed record C3DSignedHeightDeltaArtifact(
    string SchemaVersion,
    string StepId,
    string OutputEntityId,
    string CurrentSourceEntityId,
    string CurrentSourceContentSha256,
    string CurrentRootSourceSha256,
    long CurrentByteLength,
    string BackgroundEntityId,
    string BackgroundContentSha256,
    string BackgroundRootSourceSha256,
    long BackgroundByteLength,
    string OutputContentSha256,
    string OutputRootSourceSha256,
    string Unit,
    string FrameId,
    int Width,
    int Height,
    int GridOriginColumn,
    int GridOriginRow,
    string SubtractionPolicy,
    string GridPolicy,
    string MissingValuePolicy,
    string ZeroDeltaPolicy,
    int CurrentValidSampleCount,
    int BackgroundValidSampleCount,
    int PairedValidSampleCount,
    int MissingEitherSampleCount,
    int ZeroDeltaSampleCount,
    int PositiveDeltaSampleCount,
    int NegativeDeltaSampleCount,
    IReadOnlyList<double> Values,
    string Provenance,
    string ContentSha256)
{
    public const string CurrentSchemaVersion = "1.0";

    public int OutputValidSampleCount => PairedValidSampleCount;
    public int OutputMissingSampleCount => MissingEitherSampleCount;

    public static C3DSignedHeightDeltaArtifact Create(
        string stepId,
        string outputEntityId,
        string currentSourceEntityId,
        string currentSourceContentSha256,
        string currentRootSourceSha256,
        long currentByteLength,
        string backgroundEntityId,
        string backgroundContentSha256,
        string backgroundRootSourceSha256,
        long backgroundByteLength,
        string unit,
        string frameId,
        int width,
        int height,
        int gridOriginColumn,
        int gridOriginRow,
        IReadOnlyList<double> values,
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
        ArgumentException.ThrowIfNullOrWhiteSpace(outputEntityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentSourceEntityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentSourceContentSha256);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentRootSourceSha256);
        ArgumentException.ThrowIfNullOrWhiteSpace(backgroundEntityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(backgroundContentSha256);
        ArgumentException.ThrowIfNullOrWhiteSpace(backgroundRootSourceSha256);
        ArgumentException.ThrowIfNullOrWhiteSpace(unit);
        ArgumentException.ThrowIfNullOrWhiteSpace(frameId);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        ArgumentNullException.ThrowIfNull(values);
        if (!IsSha256(currentSourceContentSha256)
            || !IsSha256(currentRootSourceSha256)
            || !IsSha256(backgroundContentSha256)
            || !IsSha256(backgroundRootSourceSha256)
            || outputEntityId.Equals(currentSourceEntityId, StringComparison.OrdinalIgnoreCase)
            || outputEntityId.Equals(backgroundEntityId, StringComparison.OrdinalIgnoreCase)
            || currentByteLength <= 0
            || backgroundByteLength <= 0
            || width <= 0
            || height <= 0
            || values.Count != checked(width * height)
            || currentValidSampleCount < 1
            || backgroundValidSampleCount < 1
            || pairedValidSampleCount < 0
            || missingEitherSampleCount < 0
            || zeroDeltaSampleCount < 0
            || positiveDeltaSampleCount < 0
            || negativeDeltaSampleCount < 0)
        {
            throw new ArgumentException("Signed delta artifact contains an invalid identity, grid, or count.");
        }

        var copiedValues = values.ToArray();
        var artifact = new C3DSignedHeightDeltaArtifact(
            CurrentSchemaVersion,
            stepId.Trim(),
            outputEntityId.Trim(),
            currentSourceEntityId.Trim(),
            currentSourceContentSha256.Trim().ToUpperInvariant(),
            currentRootSourceSha256.Trim().ToUpperInvariant(),
            currentByteLength,
            backgroundEntityId.Trim(),
            backgroundContentSha256.Trim().ToUpperInvariant(),
            backgroundRootSourceSha256.Trim().ToUpperInvariant(),
            backgroundByteLength,
            CalculateValuesSha256(copiedValues),
            currentRootSourceSha256.Trim().ToUpperInvariant(),
            unit.Trim(),
            frameId.Trim(),
            width,
            height,
            gridOriginColumn,
            gridOriginRow,
            C3DSignedHeightDeltaRecipePolicy.SubtractionPolicyName,
            C3DSignedHeightDeltaRecipePolicy.GridPolicyName,
            C3DSignedHeightDeltaRecipePolicy.MissingValuePolicyName,
            C3DSignedHeightDeltaRecipePolicy.ZeroDeltaPolicyName,
            currentValidSampleCount,
            backgroundValidSampleCount,
            pairedValidSampleCount,
            missingEitherSampleCount,
            zeroDeltaSampleCount,
            positiveDeltaSampleCount,
            negativeDeltaSampleCount,
            copiedValues,
            provenance.Trim(),
            string.Empty);
        artifact = artifact with { ContentSha256 = CalculateContentSha256(artifact) };
        if (!artifact.TryValidate(out var message))
        {
            throw new InvalidDataException(message);
        }

        return artifact;
    }

    public bool TryValidate(out string message)
    {
        var cellCount = Width > 0 && Height > 0 ? (long)Width * Height : -1;
        var finiteCount = 0;
        var missingCount = 0;
        var zeroCount = 0;
        var positiveCount = 0;
        var negativeCount = 0;
        if (Values is not null)
        {
            foreach (var value in Values)
            {
                if (double.IsNaN(value) || !double.IsFinite(value))
                {
                    missingCount++;
                }
                else if (value == 0d)
                {
                    finiteCount++;
                    zeroCount++;
                }
                else if (value > 0d)
                {
                    finiteCount++;
                    positiveCount++;
                }
                else
                {
                    finiteCount++;
                    negativeCount++;
                }
            }
        }
        var valid = SchemaVersion == CurrentSchemaVersion
            && !string.IsNullOrWhiteSpace(StepId)
            && !string.IsNullOrWhiteSpace(OutputEntityId)
            && !string.IsNullOrWhiteSpace(CurrentSourceEntityId)
            && !string.IsNullOrWhiteSpace(BackgroundEntityId)
            && !OutputEntityId.Equals(CurrentSourceEntityId, StringComparison.OrdinalIgnoreCase)
            && !OutputEntityId.Equals(BackgroundEntityId, StringComparison.OrdinalIgnoreCase)
            && IsSha256(CurrentSourceContentSha256)
            && IsSha256(CurrentRootSourceSha256)
            && IsSha256(BackgroundContentSha256)
            && IsSha256(BackgroundRootSourceSha256)
            && IsSha256(OutputContentSha256)
            && IsSha256(OutputRootSourceSha256)
            && CurrentByteLength > 0
            && BackgroundByteLength > 0
            && cellCount > 0
            && Values is not null
            && Values.Count == cellCount
            && CurrentValidSampleCount is > 0 and <= int.MaxValue
            && BackgroundValidSampleCount is > 0 and <= int.MaxValue
            && PairedValidSampleCount >= 0
            && MissingEitherSampleCount >= 0
            && CurrentValidSampleCount <= cellCount
            && BackgroundValidSampleCount <= cellCount
            && PairedValidSampleCount + MissingEitherSampleCount == cellCount
            && missingCount == MissingEitherSampleCount
            && finiteCount == PairedValidSampleCount
            && zeroCount == ZeroDeltaSampleCount
            && positiveCount == PositiveDeltaSampleCount
            && negativeCount == NegativeDeltaSampleCount
            && ZeroDeltaSampleCount + PositiveDeltaSampleCount + NegativeDeltaSampleCount == PairedValidSampleCount
            && string.Equals(SubtractionPolicy, C3DSignedHeightDeltaRecipePolicy.SubtractionPolicyName, StringComparison.Ordinal)
            && string.Equals(GridPolicy, C3DSignedHeightDeltaRecipePolicy.GridPolicyName, StringComparison.Ordinal)
            && string.Equals(MissingValuePolicy, C3DSignedHeightDeltaRecipePolicy.MissingValuePolicyName, StringComparison.Ordinal)
            && string.Equals(ZeroDeltaPolicy, C3DSignedHeightDeltaRecipePolicy.ZeroDeltaPolicyName, StringComparison.Ordinal)
            && string.Equals(OutputContentSha256, CalculateValuesSha256(Values), StringComparison.OrdinalIgnoreCase)
            && string.Equals(ContentSha256, CalculateContentSha256(this), StringComparison.OrdinalIgnoreCase);
        message = valid ? "Signed delta artifact is valid." : "Signed delta artifact is incomplete or internally inconsistent.";
        return valid;
    }

    public static string CalculateValuesSha256(IReadOnlyList<double> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write("OpenVisionLab.C3DSignedHeightDeltaValues");
        writer.Write(values.Count);
        foreach (var value in values)
        {
            writer.Write(double.IsNaN(value) ? long.MinValue : BitConverter.DoubleToInt64Bits(value));
        }
        writer.Flush();
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    public static string CalculateContentSha256(C3DSignedHeightDeltaArtifact artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write("OpenVisionLab.C3DSignedHeightDeltaArtifact");
        writer.Write(artifact.SchemaVersion ?? string.Empty);
        writer.Write(artifact.StepId ?? string.Empty);
        writer.Write(artifact.OutputEntityId ?? string.Empty);
        writer.Write(artifact.CurrentSourceEntityId ?? string.Empty);
        writer.Write((artifact.CurrentSourceContentSha256 ?? string.Empty).ToUpperInvariant());
        writer.Write((artifact.CurrentRootSourceSha256 ?? string.Empty).ToUpperInvariant());
        writer.Write(artifact.CurrentByteLength);
        writer.Write(artifact.BackgroundEntityId ?? string.Empty);
        writer.Write((artifact.BackgroundContentSha256 ?? string.Empty).ToUpperInvariant());
        writer.Write((artifact.BackgroundRootSourceSha256 ?? string.Empty).ToUpperInvariant());
        writer.Write(artifact.BackgroundByteLength);
        writer.Write((artifact.OutputContentSha256 ?? string.Empty).ToUpperInvariant());
        writer.Write((artifact.OutputRootSourceSha256 ?? string.Empty).ToUpperInvariant());
        writer.Write(artifact.Unit ?? string.Empty);
        writer.Write(artifact.FrameId ?? string.Empty);
        writer.Write(artifact.Width);
        writer.Write(artifact.Height);
        writer.Write(artifact.GridOriginColumn);
        writer.Write(artifact.GridOriginRow);
        writer.Write(artifact.SubtractionPolicy ?? string.Empty);
        writer.Write(artifact.GridPolicy ?? string.Empty);
        writer.Write(artifact.MissingValuePolicy ?? string.Empty);
        writer.Write(artifact.ZeroDeltaPolicy ?? string.Empty);
        writer.Write(artifact.CurrentValidSampleCount);
        writer.Write(artifact.BackgroundValidSampleCount);
        writer.Write(artifact.PairedValidSampleCount);
        writer.Write(artifact.MissingEitherSampleCount);
        writer.Write(artifact.ZeroDeltaSampleCount);
        writer.Write(artifact.PositiveDeltaSampleCount);
        writer.Write(artifact.NegativeDeltaSampleCount);
        writer.Write(artifact.Provenance ?? string.Empty);
        writer.Write(CalculateValuesSha256(artifact.Values ?? []));
        writer.Flush();
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static bool IsSha256(string? value) =>
        value is not null && value.Length == 64 && value.All(Uri.IsHexDigit);
}
