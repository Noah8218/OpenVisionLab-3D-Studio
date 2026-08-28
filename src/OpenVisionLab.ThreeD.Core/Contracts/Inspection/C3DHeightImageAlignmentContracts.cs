using System.Security.Cryptography;
using System.Text;

namespace OpenVisionLab.ThreeD.Core;

public enum C3DHeightImageAlignmentMode
{
    BorderTemplate,
    FeatureHomography
}

/// <summary>
/// Software-grid pose returned by the bounded F-08 height-image alignment route.
/// X is pixel column, Y is pixel row, and rotation is expressed in degrees in
/// the native height-image plane. It is not a calibrated physical pose.
/// </summary>
public sealed record C3DHeightImageAlignmentPose(
    double TranslationX,
    double TranslationY,
    double RotationDegrees,
    double Scale,
    double CenterX,
    double CenterY,
    double BoundingX,
    double BoundingY,
    double BoundingWidth,
    double BoundingHeight);

public sealed record C3DHeightImageAlignmentDiagnostics(
    int CandidateCount,
    double BestScorePercent,
    double SecondScorePercent,
    double ScoreMarginPercent,
    double SearchScoreMinimum,
    double AcceptanceScoreMinimumPercent,
    double MinimumCandidateMarginPercent,
    double AngleMinimumDegrees,
    double AngleMaximumDegrees,
    double AngleStepDegrees);

/// <summary>
/// Immutable identity-bearing evidence for one reference/moving height-image
/// alignment. Matching arithmetic remains in the OpenVisionLab Vision SDK;
/// this contract owns source identity, selection policy, result semantics, and
/// deterministic repeatability identity.
/// </summary>
public sealed class C3DHeightImageAlignmentArtifact
{
    public const string ContractVersion = "1.0";

    private C3DHeightImageAlignmentArtifact(
        string outputEntityId,
        string stepId,
        string referenceEntityId,
        string referenceContentSha256,
        string movingEntityId,
        string movingContentSha256,
        string unit,
        string frameId,
        string selectionId,
        ToolRecipeGridRectangle templateSelection,
        C3DHeightImageAlignmentMode mode,
        C3DHeightImageAlignmentPose pose,
        C3DHeightImageAlignmentDiagnostics diagnostics,
        string provenance,
        string contentSha256)
    {
        OutputEntityId = outputEntityId;
        StepId = stepId;
        ReferenceEntityId = referenceEntityId;
        ReferenceContentSha256 = referenceContentSha256;
        MovingEntityId = movingEntityId;
        MovingContentSha256 = movingContentSha256;
        Unit = unit;
        FrameId = frameId;
        SelectionId = selectionId;
        TemplateSelection = templateSelection;
        Mode = mode;
        Pose = pose;
        Diagnostics = diagnostics;
        Provenance = provenance;
        ContentSha256 = contentSha256;
    }

    public string OutputEntityId { get; }
    public string StepId { get; }
    public string ReferenceEntityId { get; }
    public string ReferenceContentSha256 { get; }
    public string MovingEntityId { get; }
    public string MovingContentSha256 { get; }
    public string Unit { get; }
    public string FrameId { get; }
    public string ScalarMeaning => "height-image-pixel-alignment";
    public string CoordinateMapping => "pixelX=column;pixelY=row;no-flip;one-source-cell-per-pixel";
    public string SelectionId { get; }
    public ToolRecipeGridRectangle TemplateSelection { get; }
    public C3DHeightImageAlignmentMode Mode { get; }
    public C3DHeightImageAlignmentPose Pose { get; }
    public C3DHeightImageAlignmentDiagnostics Diagnostics { get; }
    public string Provenance { get; }
    public string ContentSha256 { get; }

    public static C3DHeightImageAlignmentArtifact Create(
        string outputEntityId,
        string stepId,
        string referenceEntityId,
        string referenceContentSha256,
        string movingEntityId,
        string movingContentSha256,
        string unit,
        string frameId,
        string selectionId,
        ToolRecipeGridRectangle templateSelection,
        C3DHeightImageAlignmentMode mode,
        C3DHeightImageAlignmentPose pose,
        C3DHeightImageAlignmentDiagnostics diagnostics,
        string provenance)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputEntityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(stepId);
        ArgumentException.ThrowIfNullOrWhiteSpace(referenceEntityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(referenceContentSha256);
        ArgumentException.ThrowIfNullOrWhiteSpace(movingEntityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(movingContentSha256);
        ArgumentException.ThrowIfNullOrWhiteSpace(unit);
        ArgumentException.ThrowIfNullOrWhiteSpace(frameId);
        ArgumentException.ThrowIfNullOrWhiteSpace(selectionId);
        ArgumentNullException.ThrowIfNull(templateSelection);
        ArgumentNullException.ThrowIfNull(pose);
        ArgumentNullException.ThrowIfNull(diagnostics);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode));
        }

        ValidateSelection(templateSelection);
        ValidatePose(pose);
        ValidateDiagnostics(diagnostics);

        var hash = CalculateContentSha256(
            outputEntityId,
            stepId,
            referenceEntityId,
            referenceContentSha256,
            movingEntityId,
            movingContentSha256,
            unit,
            frameId,
            selectionId,
            templateSelection,
            mode,
            pose,
            diagnostics);
        return new C3DHeightImageAlignmentArtifact(
            outputEntityId,
            stepId,
            referenceEntityId,
            referenceContentSha256,
            movingEntityId,
            movingContentSha256,
            unit,
            frameId,
            selectionId,
            templateSelection,
            mode,
            pose,
            diagnostics,
            provenance,
            hash);
    }

    private static void ValidateSelection(ToolRecipeGridRectangle selection)
    {
        if (selection.Row < 0
            || selection.Column < 0
            || selection.RowCount <= 0
            || selection.ColumnCount <= 0)
        {
            throw new ArgumentException("Height-image alignment template selection must be a positive source-owned rectangle.", nameof(selection));
        }
    }

    private static void ValidatePose(C3DHeightImageAlignmentPose pose)
    {
        var values = new[]
        {
            pose.TranslationX,
            pose.TranslationY,
            pose.RotationDegrees,
            pose.Scale,
            pose.CenterX,
            pose.CenterY,
            pose.BoundingX,
            pose.BoundingY,
            pose.BoundingWidth,
            pose.BoundingHeight
        };
        if (values.Any(value => !double.IsFinite(value)))
        {
            throw new ArgumentException("Height-image alignment pose values must be finite.", nameof(pose));
        }

        if (pose.Scale <= 0d || pose.BoundingWidth <= 0d || pose.BoundingHeight <= 0d)
        {
            throw new ArgumentException("Height-image alignment pose scale and bounds must be positive.", nameof(pose));
        }
    }

    private static void ValidateDiagnostics(C3DHeightImageAlignmentDiagnostics diagnostics)
    {
        var values = new[]
        {
            diagnostics.BestScorePercent,
            diagnostics.SecondScorePercent,
            diagnostics.ScoreMarginPercent,
            diagnostics.SearchScoreMinimum,
            diagnostics.AcceptanceScoreMinimumPercent,
            diagnostics.MinimumCandidateMarginPercent,
            diagnostics.AngleMinimumDegrees,
            diagnostics.AngleMaximumDegrees,
            diagnostics.AngleStepDegrees
        };
        if (values.Any(value => !double.IsFinite(value)))
        {
            throw new ArgumentException("Height-image alignment diagnostics must be finite.", nameof(diagnostics));
        }

        if (diagnostics.CandidateCount <= 0
            || diagnostics.BestScorePercent < 0d
            || diagnostics.SecondScorePercent < 0d
            || diagnostics.ScoreMarginPercent < 0d
            || diagnostics.SearchScoreMinimum < 0d
            || diagnostics.SearchScoreMinimum > 1d
            || diagnostics.AcceptanceScoreMinimumPercent < 0d
            || diagnostics.AcceptanceScoreMinimumPercent > 100d
            || diagnostics.MinimumCandidateMarginPercent < 0d
            || diagnostics.MinimumCandidateMarginPercent > 100d
            || diagnostics.AngleMinimumDegrees > diagnostics.AngleMaximumDegrees
            || diagnostics.AngleStepDegrees <= 0d)
        {
            throw new ArgumentException("Height-image alignment diagnostics contain an invalid range or threshold.", nameof(diagnostics));
        }
    }

    private static string CalculateContentSha256(
        string outputEntityId,
        string stepId,
        string referenceEntityId,
        string referenceContentSha256,
        string movingEntityId,
        string movingContentSha256,
        string unit,
        string frameId,
        string selectionId,
        ToolRecipeGridRectangle selection,
        C3DHeightImageAlignmentMode mode,
        C3DHeightImageAlignmentPose pose,
        C3DHeightImageAlignmentDiagnostics diagnostics)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write("OpenVisionLab.C3DHeightImageAlignmentArtifact");
            writer.Write(ContractVersion);
            writer.Write(outputEntityId);
            writer.Write(stepId);
            writer.Write(referenceEntityId);
            writer.Write(referenceContentSha256.ToUpperInvariant());
            writer.Write(movingEntityId);
            writer.Write(movingContentSha256.ToUpperInvariant());
            writer.Write(unit);
            writer.Write(frameId);
            writer.Write("pixelX=column;pixelY=row;no-flip;one-source-cell-per-pixel");
            writer.Write(selectionId);
            writer.Write(selection.Row);
            writer.Write(selection.Column);
            writer.Write(selection.RowCount);
            writer.Write(selection.ColumnCount);
            writer.Write((int)mode);
            writer.Write(pose.TranslationX);
            writer.Write(pose.TranslationY);
            writer.Write(pose.RotationDegrees);
            writer.Write(pose.Scale);
            writer.Write(pose.CenterX);
            writer.Write(pose.CenterY);
            writer.Write(pose.BoundingX);
            writer.Write(pose.BoundingY);
            writer.Write(pose.BoundingWidth);
            writer.Write(pose.BoundingHeight);
            writer.Write(diagnostics.CandidateCount);
            writer.Write(diagnostics.BestScorePercent);
            writer.Write(diagnostics.SecondScorePercent);
            writer.Write(diagnostics.ScoreMarginPercent);
            writer.Write(diagnostics.SearchScoreMinimum);
            writer.Write(diagnostics.AcceptanceScoreMinimumPercent);
            writer.Write(diagnostics.MinimumCandidateMarginPercent);
            writer.Write(diagnostics.AngleMinimumDegrees);
            writer.Write(diagnostics.AngleMaximumDegrees);
            writer.Write(diagnostics.AngleStepDegrees);
        }

        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }
}
