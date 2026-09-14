using System.IO;
using OpenVisionLab.ThreeD.Viewer.Rendering;

namespace OpenVisionLab.ThreeD.Verification.Viewer;

internal static class ViewerInteractionLodStateVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        var fullReportPath = Path.GetFullPath(reportPath);
        var lines = new List<string>
        {
            "OpenVisionLab 3D Viewer interaction-LOD state verification",
            $"Generated: {DateTimeOffset.Now:O}"
        };
        var passed = 0;
        var total = 0;

        void Check(string name, bool condition, string detail)
        {
            total++;
            lines.Add($"{(condition ? "PASS" : "FAIL")} | {name} | {detail}");
            if (condition)
            {
                passed++;
            }
        }

        var state = new C3DInteractionLodState();
        Check(
            "initial state is precise",
            state.Level == C3DWireframeLodLevel.Precise
            && !state.IsActive
            && state.ActivationCount == 0
            && state.MediumTransitionCount == 0
            && state.RestoreCount == 0
            && state.SourceApplyCount == 0,
            Describe(state));

        Check(
            "unavailable interaction cannot activate",
            !state.TryBegin(canUse: false)
            && state.Level == C3DWireframeLodLevel.Precise
            && state.ActivationCount == 0,
            Describe(state));

        Check(
            "first activation enters coarse level",
            state.TryBegin(canUse: true)
            && state.Level == C3DWireframeLodLevel.Coarse
            && state.IsActive
            && state.ActivationCount == 1,
            Describe(state));

        Check(
            "repeated activation does not double count",
            state.TryBegin(canUse: true)
            && state.Level == C3DWireframeLodLevel.Coarse
            && state.ActivationCount == 1,
            Describe(state));

        Check(
            "coarse level advances to medium",
            state.AdvanceAfterInteraction()
            && state.Level == C3DWireframeLodLevel.Medium
            && state.MediumTransitionCount == 1,
            Describe(state));

        Check(
            "medium level restores to precise",
            !state.AdvanceAfterInteraction()
            && state.Level == C3DWireframeLodLevel.Precise
            && !state.IsActive
            && state.RestoreCount == 1,
            Describe(state));

        state.Restore();
        Check(
            "restore on precise is idempotent",
            state.RestoreCount == 1,
            Describe(state));

        state.TryBegin(canUse: true);
        state.Restore();
        Check(
            "explicit restore records an active restore",
            state.Level == C3DWireframeLodLevel.Precise
            && state.RestoreCount == 2,
            Describe(state));

        state.ResetForSourceChange(sourceApplied: false);
        Check(
            "source reset without apply clears active state",
            state.Level == C3DWireframeLodLevel.Precise
            && state.SourceApplyCount == 0,
            Describe(state));

        state.TryBegin(canUse: true);
        state.ResetForSourceChange(sourceApplied: true);
        Check(
            "source apply reset clears LOD and records source",
            state.Level == C3DWireframeLodLevel.Precise
            && !state.IsActive
            && state.SourceApplyCount == 1,
            Describe(state));

        state.Stop();
        Check(
            "stop is idempotent and does not alter counters",
            state.Level == C3DWireframeLodLevel.Precise
            && state.ActivationCount == 3
            && state.MediumTransitionCount == 1
            && state.RestoreCount == 2
            && state.SourceApplyCount == 1,
            Describe(state));

        summary = $"Viewer interaction-LOD state verification: {(passed == total ? "Pass" : "Fail")} ({passed}/{total} checks)";
        lines.Add(summary);
        Directory.CreateDirectory(Path.GetDirectoryName(fullReportPath)!);
        File.WriteAllLines(fullReportPath, lines);
        return passed == total;
    }

    private static string Describe(C3DInteractionLodState state) =>
        $"level={state.Level};active={state.IsActive};activations={state.ActivationCount};mediumTransitions={state.MediumTransitionCount};restores={state.RestoreCount};sourceApplies={state.SourceApplyCount}";
}
