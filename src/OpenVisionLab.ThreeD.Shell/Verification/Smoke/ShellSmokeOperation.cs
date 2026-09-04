namespace OpenVisionLab.ThreeD.Shell.Verification.Smoke;

/// <summary>
/// Owns one command-line Shell Smoke operation from admission through close.
/// The operation composes the existing cancellation and one-shot gate owners
/// so MainWindow has one lifetime identity to check and dispose.
/// </summary>
internal sealed class ShellSmokeOperation : IDisposable
{
    private readonly ShellSmokeLifetime lifetime = new();
    private readonly ShellSmokeExecutionGate executionGate = new();
    private int disposalState;

    public CancellationToken Token => lifetime.Token;

    public bool IsActive =>
        Volatile.Read(ref disposalState) == 0
        && lifetime.IsActive;

    public bool TryEnter()
    {
        if (!IsActive || !executionGate.TryEnter())
        {
            return false;
        }

        return IsActive;
    }

    public Task<bool> DelayAsync(TimeSpan delay) => lifetime.DelayAsync(delay);

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposalState, 1) != 0)
        {
            return;
        }

        lifetime.Dispose();
        GC.SuppressFinalize(this);
    }
}
