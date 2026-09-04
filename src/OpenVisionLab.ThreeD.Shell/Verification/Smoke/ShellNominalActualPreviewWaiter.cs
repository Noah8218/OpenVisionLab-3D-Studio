using OpenVisionLab.ThreeD.Viewer.ViewModels;

namespace OpenVisionLab.ThreeD.Shell.Verification.Smoke;

/// <summary>
/// Waits for Nominal/Actual Preview readiness without owning the Viewer or
/// touching WPF controls. The caller supplies the state snapshot and lifetime
/// token, which keeps this Smoke policy independently verifiable.
/// </summary>
internal sealed class ShellNominalActualPreviewWaiter
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100);
    private readonly Func<NominalActualComparisonState> readState;

    public ShellNominalActualPreviewWaiter(
        Func<NominalActualComparisonState> readState)
    {
        this.readState = readState ?? throw new ArgumentNullException(nameof(readState));
    }

    public async Task<bool> WaitAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        if (timeout < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (readState() == NominalActualComparisonState.PreviewRunning)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return false;
            }

            var remaining = deadline - DateTimeOffset.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                break;
            }

            try
            {
                await Task.Delay(
                    remaining < PollInterval ? remaining : PollInterval,
                    cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return false;
            }
        }

        return readState() is NominalActualComparisonState.PreviewReady
            or NominalActualComparisonState.Published;
    }
}
