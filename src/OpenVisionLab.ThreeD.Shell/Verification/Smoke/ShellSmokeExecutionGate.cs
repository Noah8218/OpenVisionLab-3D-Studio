namespace OpenVisionLab.ThreeD.Shell.Verification.Smoke;

/// <summary>
/// Ensures a Window's command-line Shell Smoke loaded handler starts once.
/// The gate is intentionally WPF-neutral and has no reset operation: a closed
/// Window must not restart an automation sequence when it is loaded again.
/// </summary>
internal sealed class ShellSmokeExecutionGate
{
    private int entered;

    public bool TryEnter() =>
        Interlocked.CompareExchange(ref entered, 1, 0) == 0;
}
