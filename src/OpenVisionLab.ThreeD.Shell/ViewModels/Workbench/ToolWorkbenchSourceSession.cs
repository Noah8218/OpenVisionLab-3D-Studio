namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

using System.IO;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;

internal sealed class ToolWorkbenchSourceSession : IDisposable
{
    private readonly object decodedSourceSync = new();
    private string decodedSourceKey = string.Empty;
    private Task<C3DHeightFieldSnapshot>? decodedSourceTask;
    private AsyncLoadCancellation? decodedSourceCancellation;
    private bool disposed;

    public ToolRecipeSelectionSourceBinding? SourceBinding { get; private set; }

    public ToolRecipeAcquisitionProvenance? SourceAcquisitionProvenance { get; private set; }

    public ToolRecipeSource? OpenedSourceIdentity { get; private set; }

    public IReadOnlyList<string> SourceIdentityErrors { get; private set; } = [];

    public bool SetSourceBinding(ToolRecipeSelectionSourceBinding? value)
    {
        if (SourceBinding == value)
        {
            return false;
        }

        SourceBinding = value;
        ClearDecodedSource();
        return true;
    }

    public async Task<C3DHeightFieldSnapshot> GetOrLoadDecodedSourceAsync(
        string path,
        string entityId,
        string unit,
        string frameId,
        CancellationToken cancellationToken)
    {
        var sourceBinding = SourceBinding;
        return await GetOrLoadDecodedSourceAsyncCore(
            path,
            entityId,
            unit,
            frameId,
            sourceBinding,
            cancellationToken,
            loadToken => Task.FromResult(
                LoadDecodedSource(
                    path,
                    entityId,
                    unit,
                    frameId,
                    sourceBinding,
                    loadToken))).ConfigureAwait(false);
    }

    internal Task<C3DHeightFieldSnapshot> GetOrLoadDecodedSourceAsync(
        string path,
        string entityId,
        string unit,
        string frameId,
        CancellationToken cancellationToken,
        Func<CancellationToken, Task<C3DHeightFieldSnapshot>> loadSourceAsync)
        => GetOrLoadDecodedSourceAsyncCore(
            path,
            entityId,
            unit,
            frameId,
            SourceBinding,
            cancellationToken,
            loadSourceAsync);

    private async Task<C3DHeightFieldSnapshot> GetOrLoadDecodedSourceAsyncCore(
        string path,
        string entityId,
        string unit,
        string frameId,
        ToolRecipeSelectionSourceBinding? sourceBinding,
        CancellationToken cancellationToken,
        Func<CancellationToken, Task<C3DHeightFieldSnapshot>> loadSourceAsync)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(entityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(unit);
        ArgumentException.ThrowIfNullOrWhiteSpace(frameId);
        ArgumentNullException.ThrowIfNull(loadSourceAsync);

        var fullPath = Path.GetFullPath(path);
        var file = new FileInfo(fullPath);
        var sourceKey = string.Join(
            "|",
            fullPath,
            file.Length,
            file.LastWriteTimeUtc.Ticks,
            sourceBinding?.ContentSha256 ?? string.Empty,
            entityId,
            unit,
            frameId);

        AsyncLoadCancellation? previousCancellation = null;
        Task<C3DHeightFieldSnapshot>? previousTask = null;
        AsyncLoadCancellation operation;
        Task<C3DHeightFieldSnapshot> sourceTask;
        lock (decodedSourceSync)
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(ToolWorkbenchSourceSession));
            }

            if (decodedSourceTask is null
                || decodedSourceCancellation is null
                || !string.Equals(decodedSourceKey, sourceKey, StringComparison.OrdinalIgnoreCase))
            {
                previousCancellation = decodedSourceCancellation;
                previousTask = decodedSourceTask;
                operation = new AsyncLoadCancellation();
                decodedSourceCancellation = operation;
                decodedSourceKey = sourceKey;
                decodedSourceTask = Task.Run(
                    () => loadSourceAsync(operation.Token),
                    operation.Token);
            }

            operation = decodedSourceCancellation!;
            sourceTask = decodedSourceTask!;
        }

        RetireDecodedSource(previousCancellation, previousTask);

        try
        {
            return await sourceTask.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch when (sourceTask.IsFaulted || sourceTask.IsCanceled)
        {
            var retireOperation = false;
            lock (decodedSourceSync)
            {
                if (ReferenceEquals(decodedSourceTask, sourceTask))
                {
                    decodedSourceTask = null;
                    decodedSourceKey = string.Empty;
                    decodedSourceCancellation = null;
                    retireOperation = true;
                }
            }

            if (retireOperation)
            {
                RetireDecodedSource(operation, sourceTask);
            }

            throw;
        }
    }

    public void ClearDecodedSource()
    {
        AsyncLoadCancellation? cancellation;
        Task<C3DHeightFieldSnapshot>? task;
        lock (decodedSourceSync)
        {
            cancellation = decodedSourceCancellation;
            task = decodedSourceTask;
            decodedSourceCancellation = null;
            decodedSourceTask = null;
            decodedSourceKey = string.Empty;
        }

        RetireDecodedSource(cancellation, task);
    }

    public void Dispose()
    {
        AsyncLoadCancellation? cancellation;
        Task<C3DHeightFieldSnapshot>? task;
        lock (decodedSourceSync)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            cancellation = decodedSourceCancellation;
            task = decodedSourceTask;
            decodedSourceCancellation = null;
            decodedSourceTask = null;
            decodedSourceKey = string.Empty;
        }

        RetireDecodedSource(cancellation, task);
        GC.SuppressFinalize(this);
    }

    public bool SetSourceAcquisitionProvenance(ToolRecipeAcquisitionProvenance? value)
    {
        if (SourceAcquisitionProvenance == value)
        {
            return false;
        }

        SourceAcquisitionProvenance = value;
        return true;
    }

    public void CaptureOpenedSourceIdentity(ToolRecipeSource source) => OpenedSourceIdentity = source;

    public void AcceptCurrentSourceIdentity() => OpenedSourceIdentity = null;

    public bool SetSourceIdentityErrors(IReadOnlyList<string> errors)
    {
        if (ReferenceEquals(SourceIdentityErrors, errors))
        {
            return false;
        }

        SourceIdentityErrors = errors;
        return true;
    }

    private C3DHeightFieldSnapshot LoadDecodedSource(
        string path,
        string entityId,
        string unit,
        string frameId,
        ToolRecipeSelectionSourceBinding? sourceBinding,
        CancellationToken cancellationToken)
    {
        var fullPath = Path.GetFullPath(path);
        if (sourceBinding is not { } binding
            || !string.Equals(binding.Format, "C3D", StringComparison.OrdinalIgnoreCase))
        {
            return C3DHeightFieldSnapshot.LoadIdentified(
                fullPath,
                entityId,
                unit,
                frameId,
                cancellationToken);
        }

        var expectedByteLength = checked(
            8L + (long)binding.GridWidth * binding.GridHeight * sizeof(float));
        return C3DHeightFieldSnapshot.LoadVerified(
            fullPath,
            entityId,
            unit,
            frameId,
            expectedByteLength,
            binding.ContentSha256,
            binding.GridWidth,
            binding.GridHeight,
            cancellationToken);
    }

    private static void RetireDecodedSource(
        AsyncLoadCancellation? cancellation,
        Task? task)
    {
        if (cancellation is null)
        {
            return;
        }

        cancellation.Cancel();
        if (task is null || task.IsCompleted)
        {
            cancellation.Dispose();
            return;
        }

        _ = task.ContinueWith(
            static (_, state) => ((AsyncLoadCancellation)state!).Dispose(),
            cancellation,
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }
}
