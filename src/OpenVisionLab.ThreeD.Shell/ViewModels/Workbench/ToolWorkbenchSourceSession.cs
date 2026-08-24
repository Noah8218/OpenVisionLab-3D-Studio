namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

using System.IO;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;

internal sealed class ToolWorkbenchSourceSession
{
    private readonly object decodedSourceSync = new();
    private string decodedSourceKey = string.Empty;
    private Task<C3DHeightFieldSnapshot>? decodedSourceTask;

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
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(entityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(unit);
        ArgumentException.ThrowIfNullOrWhiteSpace(frameId);

        var fullPath = Path.GetFullPath(path);
        var file = new FileInfo(fullPath);
        var sourceBinding = SourceBinding;
        var sourceKey = string.Join(
            "|",
            fullPath,
            file.Length,
            file.LastWriteTimeUtc.Ticks,
            sourceBinding?.ContentSha256 ?? string.Empty,
            entityId,
            unit,
            frameId);

        Task<C3DHeightFieldSnapshot> sourceTask;
        lock (decodedSourceSync)
        {
            if (decodedSourceTask is null
                || !string.Equals(decodedSourceKey, sourceKey, StringComparison.OrdinalIgnoreCase))
            {
                decodedSourceKey = sourceKey;
                decodedSourceTask = Task.Run(() =>
                {
                    if (sourceBinding is not { } binding
                        || !string.Equals(binding.Format, "C3D", StringComparison.OrdinalIgnoreCase))
                    {
                        return C3DHeightFieldSnapshot.LoadIdentified(
                            fullPath,
                            entityId,
                            unit,
                            frameId);
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
                        binding.GridHeight);
                });
            }
            sourceTask = decodedSourceTask;
        }

        try
        {
            return await sourceTask.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch when (sourceTask.IsFaulted)
        {
            lock (decodedSourceSync)
            {
                if (ReferenceEquals(decodedSourceTask, sourceTask))
                {
                    decodedSourceTask = null;
                    decodedSourceKey = string.Empty;
                }
            }
            throw;
        }
    }

    public void ClearDecodedSource()
    {
        lock (decodedSourceSync)
        {
            decodedSourceTask = null;
            decodedSourceKey = string.Empty;
        }
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
}
