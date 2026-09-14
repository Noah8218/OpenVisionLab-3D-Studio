using System.IO;

namespace OpenVisionLab.ThreeD.Viewer.BinaryHost;

/// <summary>
/// Captures and stages the text contract emitted by a Viewer consumer smoke
/// callback. It owns artifact-file policy, not the Viewer or WPF lifecycle.
/// </summary>
internal sealed class ViewerConsumerContractCaptureCoordinator
{
    private readonly string? smokeContractPath;
    private readonly string reportDirectory;
    private readonly Action<string> reportLineSink;
    private readonly List<string> contractPaths = [];

    public ViewerConsumerContractCaptureCoordinator(
        string? smokeContractPath,
        string reportPath,
        Action<string> reportLineSink)
    {
        this.smokeContractPath = smokeContractPath;
        reportDirectory = Path.GetDirectoryName(reportPath)!;
        this.reportLineSink = reportLineSink;
    }

    public int ContractCount => contractPaths.Count;

    public async Task<string> CaptureAsync(string stage, Func<Task<bool>> capture)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stage);
        ArgumentNullException.ThrowIfNull(capture);

        if (smokeContractPath is null)
        {
            reportLineSink($"Contract|stage={stage}|captured=False|reason=no-smoke-contract-path");
            return string.Empty;
        }

        var captured = await capture();
        var content = File.Exists(smokeContractPath) ? File.ReadAllText(smokeContractPath) : string.Empty;
        var stagePath = Path.Combine(reportDirectory, $"viewer-consumer-{stage}-contract.txt");
        if (File.Exists(smokeContractPath))
        {
            File.Copy(smokeContractPath, stagePath, overwrite: true);
            contractPaths.Add(stagePath);
        }

        reportLineSink($"Contract|stage={stage}|captured={content.Length > 0}|smokeResult={captured}|path={stagePath}");
        return content;
    }
}
