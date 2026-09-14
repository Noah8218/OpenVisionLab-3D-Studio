namespace OpenVisionLab.ThreeD.Viewer.Rendering;

/// <summary>
/// Owns the managed interaction-LOD state machine and its verification
/// counters. Dispatcher timers and pointer/render predicates remain in the
/// WPF control; this type only decides state transitions.
/// </summary>
internal sealed class C3DInteractionLodState
{
    public C3DWireframeLodLevel Level { get; private set; } = C3DWireframeLodLevel.Precise;

    public bool IsActive => Level != C3DWireframeLodLevel.Precise;

    public int ActivationCount { get; private set; }

    public int MediumTransitionCount { get; private set; }

    public int RestoreCount { get; private set; }

    public int SourceApplyCount { get; private set; }

    public bool TryBegin(bool canUse)
    {
        if (!canUse)
        {
            return false;
        }

        if (!IsActive)
        {
            ActivationCount++;
        }

        Level = C3DWireframeLodLevel.Coarse;
        return true;
    }

    public bool AdvanceAfterInteraction()
    {
        if (!IsActive)
        {
            return false;
        }

        if (Level == C3DWireframeLodLevel.Coarse)
        {
            Level = C3DWireframeLodLevel.Medium;
            MediumTransitionCount++;
            return true;
        }

        Level = C3DWireframeLodLevel.Precise;
        RestoreCount++;
        return false;
    }

    public void Restore()
    {
        if (!IsActive)
        {
            return;
        }

        Level = C3DWireframeLodLevel.Precise;
        RestoreCount++;
    }

    public void ResetForSourceChange(bool sourceApplied)
    {
        Level = C3DWireframeLodLevel.Precise;
        if (sourceApplied)
        {
            SourceApplyCount++;
        }
    }

    public void Stop() => Level = C3DWireframeLodLevel.Precise;
}
