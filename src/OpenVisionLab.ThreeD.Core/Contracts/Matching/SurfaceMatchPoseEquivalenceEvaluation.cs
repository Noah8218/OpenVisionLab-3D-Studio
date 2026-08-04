namespace OpenVisionLab.ThreeD.Core;

/// <summary>
/// Deterministic evidence that two model-to-scene poses are or are not equal
/// under the saved SurfaceModel symmetry declaration.
/// </summary>
public sealed record SurfaceMatchPoseEquivalenceEvaluation(
    string Semantics,
    string ModelContentSha256,
    string SymmetryKind,
    string SymmetryAxis,
    int SymmetryOrder,
    string Unit,
    string SourceFrameId,
    string TargetFrameId,
    double MaximumTranslationDifference,
    double MaximumRotationDifferenceDegrees,
    bool Equivalent,
    int SymmetryOperationIndex,
    double SymmetryOperationAngleDegrees,
    double TranslationDifference,
    double RotationDifferenceDegrees,
    string Evidence)
{
    public const string CurrentSemantics =
        "model-declared-cyclic-rigid-pose-equivalence-v1";
}
