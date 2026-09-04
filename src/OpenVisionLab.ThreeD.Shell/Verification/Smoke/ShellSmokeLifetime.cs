namespace OpenVisionLab.ThreeD.Shell.Verification.Smoke;

/// <summary>
/// Owns the cancellation boundary for asynchronous Shell Smoke work.
///
/// The MainWindow remains the owner of the Window lifecycle. This small
/// boundary only turns close/dispose into a token and a non-throwing delay so a
/// Smoke continuation cannot keep waiting after the Window has gone away.
/// </summary>
internal sealed class ShellSmokeLifetime : IDisposable
{
    private readonly CancellationTokenSource cancellation = new();
    private readonly CancellationToken token;
    private int disposalState;

    public ShellSmokeLifetime()
    {
        token = cancellation.Token;
    }

    public CancellationToken Token => token;

    public bool IsActive =>
        Volatile.Read(ref disposalState) == 0
        && !token.IsCancellationRequested;

    public void Cancel()
    {
        if (Volatile.Read(ref disposalState) != 0)
        {
            return;
        }

        cancellation.Cancel();
    }

    public async Task<bool> DelayAsync(TimeSpan delay)
    {
        if (delay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(delay));
        }
        if (!IsActive)
        {
            return false;
        }

        try
        {
            await Task.Delay(delay, token);
            return IsActive;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            return false;
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposalState, 1) != 0)
        {
            return;
        }

        cancellation.Cancel();
        cancellation.Dispose();
    }
}
