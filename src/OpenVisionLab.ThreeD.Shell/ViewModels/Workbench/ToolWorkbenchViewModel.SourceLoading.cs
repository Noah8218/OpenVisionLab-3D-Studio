using System.Globalization;
using System.IO;
using System.Windows.Input;
using OpenVisionLab.Logging;
using OpenVisionLab.ThreeD.Viewer;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

public sealed partial class ToolWorkbenchViewModel
{
    private RelayCommand loadC3DSourceCommand = null!;
    private RelayCommand cancelC3DSourceLoadCommand = null!;
    private bool isC3DSourceLoading;
    private double c3DSourceLoadProgressPercent;
    private string c3DSourceLoadFileName = string.Empty;

    public event EventHandler? CancelC3DSourceLoadRequested;

    public ICommand CancelC3DSourceLoadCommand => cancelC3DSourceLoadCommand;

    public bool IsC3DSourceLoading => isC3DSourceLoading;

    public double C3DSourceLoadProgressPercent => c3DSourceLoadProgressPercent;

    public string C3DSourceLoadStatus => string.Format(
        CultureInfo.CurrentCulture,
        Localization.Loading3DMapFormat,
        c3DSourceLoadFileName,
        c3DSourceLoadProgressPercent);

    private void InitializeC3DSourceLoading()
    {
        loadC3DSourceCommand = new RelayCommand(
            _ => LoadC3DSourceRequested?.Invoke(this, EventArgs.Empty),
            _ => !isC3DSourceLoading);
        cancelC3DSourceLoadCommand = new RelayCommand(
            _ => CancelC3DSourceLoadRequested?.Invoke(this, EventArgs.Empty),
            _ => isC3DSourceLoading);
        LoadC3DSourceCommand = loadC3DSourceCommand;
        Localization.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(ThreeDLocalization.Loading3DMapFormat))
            {
                OnPropertyChanged(nameof(C3DSourceLoadStatus));
            }
        };
    }

    public void BeginC3DSourceLoad(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        c3DSourceLoadFileName = Path.GetFileName(path);
        c3DSourceLoadProgressPercent = 0.0;
        isC3DSourceLoading = true;
        NotifyC3DSourceLoadState();
        AppendLog("Source", $"C3D source load started: {c3DSourceLoadFileName}.");
        OVLog.Write(LogCategory.UI, LogLevel.Info, $"C3D source load started: {Path.GetFullPath(path)}");
    }

    public void ReportC3DSourceLoadProgress(double percent)
    {
        if (!isC3DSourceLoading)
        {
            return;
        }

        var normalized = Math.Clamp(percent, 0.0, 100.0);
        if (Math.Abs(c3DSourceLoadProgressPercent - normalized) < 0.1)
        {
            return;
        }

        c3DSourceLoadProgressPercent = normalized;
        OnPropertyChanged(nameof(C3DSourceLoadProgressPercent));
        OnPropertyChanged(nameof(C3DSourceLoadStatus));
    }

    public void CompleteC3DSourceLoad(string path, long elapsedMilliseconds)
    {
        EndC3DSourceLoad();
        AppendLog("Source", $"C3D source loaded: {Path.GetFileName(path)} ({elapsedMilliseconds} ms).");
        OVLog.Write(LogCategory.UI, LogLevel.Info, $"C3D source load completed in {elapsedMilliseconds} ms: {Path.GetFullPath(path)}");
    }

    public void CancelC3DSourceLoad(long elapsedMilliseconds)
    {
        var fileName = c3DSourceLoadFileName;
        EndC3DSourceLoad();
        AppendLog("Source", $"C3D source load cancelled; current source retained: {fileName} ({elapsedMilliseconds} ms).");
        OVLog.Write(LogCategory.UI, LogLevel.Warning, $"C3D source load cancelled after {elapsedMilliseconds} ms: {fileName}");
    }

    public void FailC3DSourceLoad(string path, long elapsedMilliseconds)
    {
        EndC3DSourceLoad();
        AppendLog("Source", $"C3D source load failed; current source retained: {Path.GetFileName(path)} ({elapsedMilliseconds} ms).");
        OVLog.Write(LogCategory.UI, LogLevel.Error, $"C3D source load failed after {elapsedMilliseconds} ms: {Path.GetFullPath(path)}");
    }

    private void EndC3DSourceLoad()
    {
        isC3DSourceLoading = false;
        NotifyC3DSourceLoadState();
    }

    private void NotifyC3DSourceLoadState()
    {
        OnPropertyChanged(nameof(IsC3DSourceLoading));
        OnPropertyChanged(nameof(C3DSourceLoadProgressPercent));
        OnPropertyChanged(nameof(C3DSourceLoadStatus));
        loadC3DSourceCommand.RaiseCanExecuteChanged();
        cancelC3DSourceLoadCommand.RaiseCanExecuteChanged();
    }
}
