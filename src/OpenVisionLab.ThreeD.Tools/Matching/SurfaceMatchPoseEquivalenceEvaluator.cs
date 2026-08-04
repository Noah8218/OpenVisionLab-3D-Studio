using Lib.ThreeD.FeatureExtraction;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Tools;

/// <summary>
/// Strict model/unit/frame adapter over Library-Noah pose-symmetry arithmetic.
/// </summary>
public static class SurfaceMatchPoseEquivalenceEvaluator
{
    public static SurfaceMatchPoseEquivalenceEvaluation Evaluate(
        SurfaceModelArtifact model,
        RigidPose3D referencePose,
        RigidPose3D candidatePose,
        double maximumTranslationDifference,
        double maximumRotationDifferenceDegrees)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(referencePose);
        ArgumentNullException.ThrowIfNull(candidatePose);
        var modelValidity = SurfaceModelArtifactValidator.Inspect(model);
        if (!modelValidity.IsValid)
        {
            throw new InvalidDataException(
                "Pose equivalence requires a valid SurfaceModel: "
                + string.Join(" ", modelValidity.Errors));
        }

        if (!referencePose.IsRigid(1e-9)
            || !candidatePose.IsRigid(1e-9)
            || referencePose.Unit != model.Unit
            || candidatePose.Unit != model.Unit
            || referencePose.SourceFrameId != model.FrameId
            || candidatePose.SourceFrameId != model.FrameId
            || referencePose.TargetFrameId
                != candidatePose.TargetFrameId)
        {
            throw new InvalidDataException(
                "Pose equivalence requires rigid poses with the model unit/source frame and one shared target frame.");
        }

        if (!double.IsFinite(maximumTranslationDifference)
            || maximumTranslationDifference < 0.0
            || !double.IsFinite(maximumRotationDifferenceDegrees)
            || maximumRotationDifferenceDegrees < 0.0
            || maximumRotationDifferenceDegrees > 180.0)
        {
            throw new InvalidDataException(
                "Pose equivalence limits require a non-negative translation difference and a rotation difference from zero through 180 degrees.");
        }

        var result = new RigidPoseSymmetryEquivalenceTool().Execute(
            LibraryNoahSurfaceMatching.Pose(referencePose),
            LibraryNoahSurfaceMatching.Pose(candidatePose),
            LibraryNoahSurfaceMatching.SymmetryEquivalenceOptions(
                model,
                maximumTranslationDifference,
                maximumRotationDifferenceDegrees));
        if (!result.Success)
        {
            throw new InvalidDataException(result.Message);
        }

        var declaration = model.Symmetry
                          ?? SurfaceModelSymmetryDeclaration.None;
        var evidence =
            $"semantics={SurfaceMatchPoseEquivalenceEvaluation.CurrentSemantics};"
            + $"symmetry={declaration.Kind}:{declaration.Axis}:{declaration.Order};"
            + $"operation={result.SymmetryOperationIndex}:{result.SymmetryOperationAngleDegrees:G17};"
            + $"translationDifference={result.TranslationDifference:G17}/{maximumTranslationDifference:G17};"
            + $"rotationDifferenceDegrees={result.RotationDifferenceDegrees:G17}/{maximumRotationDifferenceDegrees:G17};"
            + $"equivalent={result.Equivalent}";
        return new SurfaceMatchPoseEquivalenceEvaluation(
            SurfaceMatchPoseEquivalenceEvaluation.CurrentSemantics,
            model.ContentSha256,
            declaration.Kind,
            declaration.Axis,
            declaration.Order,
            model.Unit,
            model.FrameId,
            referencePose.TargetFrameId,
            maximumTranslationDifference,
            maximumRotationDifferenceDegrees,
            result.Equivalent,
            result.SymmetryOperationIndex,
            result.SymmetryOperationAngleDegrees,
            result.TranslationDifference,
            result.RotationDifferenceDegrees,
            evidence);
    }
}
