using System.Threading;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Owns one ViewModel load cancellation source. Cancellation is immediate, but
/// disposal waits for the owning async load to reach its finally boundary.
/// </summary>
internal sealed class AsyncLoadCancellation : IDisposable
{
    private readonly object gate = new();
    private readonly CancellationTokenSource source = new();
    private bool disposed;

    public CancellationToken Token => source.Token;

    public void Cancel()
    {
        lock (gate)
        {
            if (!disposed)
            {
                source.Cancel();
            }
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            source.Dispose();
        }
    }
}
