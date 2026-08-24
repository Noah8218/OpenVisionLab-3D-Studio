using System.Globalization;
using System.IO;
using System.Windows.Input;
using OpenVisionLab.Logging;
using OpenVisionLab.ThreeD.Viewer;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

public sealed partial class ToolWorkbenchViewModel
{
    private RelayCommand loadC3DSourceCommand = null!;
    private RelayCommand import3DDataCommand = null!;
    private RelayCommand cancelC3DSourceLoadCommand = null!;
    private bool isC3DSourceLoading;
    private double c3DSourceLoadProgressPercent;
    private string c3DSourceLoadFileName = string.Empty;
    private string activeImportFormat = "C3D";
    private string viewerOnlyImportFileName = string.Empty;
    private string viewerOnlyImportFormat = string.Empty;

    public event EventHandler? CancelC3DSourceLoadRequested;
    public event EventHandler? Import3DDataRequested;

    public ICommand CancelC3DSourceLoadCommand => cancelC3DSourceLoadCommand;
    public ICommand Import3DDataCommand => import3DDataCommand;

    public bool IsC3DSourceLoading => isC3DSourceLoading;

    public double C3DSourceLoadProgressPercent => c3DSourceLoadProgressPercent;

    public string C3DSourceLoadStatus => string.Format(
        CultureInfo.CurrentCulture,
        Localization.Loading3DDataFormat,
        c3DSourceLoadFileName,
        c3DSourceLoadProgressPercent);

    public bool HasViewerOnlyImport => !string.IsNullOrEmpty(viewerOnlyImportFormat);

    public string ViewerOnlyImportSummary => HasViewerOnlyImport
        ? string.Format(
            CultureInfo.CurrentCulture,
            Localization.ViewerOnlyImportSummaryFormat,
            viewerOnlyImportFormat,
            viewerOnlyImportFileName)
        : string.Empty;

    private void InitializeC3DSourceLoading()
    {
        loadC3DSourceCommand = new RelayCommand(
            _ => LoadC3DSourceRequested?.Invoke(this, EventArgs.Empty),
            _ => !isC3DSourceLoading);
        import3DDataCommand = new RelayCommand(
            _ => Import3DDataRequested?.Invoke(this, EventArgs.Empty),
            _ => !isC3DSourceLoading);
        cancelC3DSourceLoadCommand = new RelayCommand(
            _ => CancelC3DSourceLoadRequested?.Invoke(this, EventArgs.Empty),
            _ => isC3DSourceLoading);
        LoadC3DSourceCommand = loadC3DSourceCommand;
        Localization.PropertyChanged += (_, args) =>
        {
            if (string.IsNullOrEmpty(args.PropertyName)
                || args.PropertyName == nameof(ThreeDLocalization.Loading3DDataFormat)
                || args.PropertyName == nameof(ThreeDLocalization.ViewerOnlyImportSummaryFormat))
            {
                OnPropertyChanged(nameof(C3DSourceLoadStatus));
                OnPropertyChanged(nameof(ViewerOnlyImportSummary));
            }
        };
    }

    public void BeginC3DSourceLoad(string path)
        => Begin3DDataImport(path, "C3D");

    public void Begin3DDataImport(string path, string format)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(format);
        c3DSourceLoadFileName = Path.GetFileName(path);
        activeImportFormat = format;
        c3DSourceLoadProgressPercent = 0.0;
        isC3DSourceLoading = true;
        NotifyC3DSourceLoadState();
        AppendLog("Source", $"{format} import started: {c3DSourceLoadFileName}.");
        OVLog.Write(LogCategory.UI, LogLevel.Info, $"{format} import started: {Path.GetFullPath(path)}");
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
        ClearViewerOnlyImport();
        EndC3DSourceLoad();
        AppendLog("Source", $"C3D source loaded: {Path.GetFileName(path)} ({elapsedMilliseconds} ms).");
        OVLog.Write(LogCategory.UI, LogLevel.Info, $"C3D source load completed in {elapsedMilliseconds} ms: {Path.GetFullPath(path)}");
    }

    public void CancelC3DSourceLoad(long elapsedMilliseconds)
    {
        var fileName = c3DSourceLoadFileName;
        var format = activeImportFormat;
        EndC3DSourceLoad();
        AppendLog("Source", $"{format} import cancelled; current source retained: {fileName} ({elapsedMilliseconds} ms).");
        OVLog.Write(LogCategory.UI, LogLevel.Warning, $"{format} import cancelled after {elapsedMilliseconds} ms: {fileName}");
    }

    public void FailC3DSourceLoad(string path, long elapsedMilliseconds)
    {
        var format = activeImportFormat;
        EndC3DSourceLoad();
        AppendLog("Source", $"{format} import failed; current source retained: {Path.GetFileName(path)} ({elapsedMilliseconds} ms).");
        OVLog.Write(LogCategory.UI, LogLevel.Error, $"{format} import failed after {elapsedMilliseconds} ms: {Path.GetFullPath(path)}");
    }

    public void CompleteViewerOnlyImport(string path, string format, long elapsedMilliseconds)
    {
        viewerOnlyImportFileName = Path.GetFileName(path);
        viewerOnlyImportFormat = format;
        EndC3DSourceLoad();
        OnPropertyChanged(nameof(HasViewerOnlyImport));
        OnPropertyChanged(nameof(ViewerOnlyImportSummary));
        AppendLog("Source", $"{format} imported for Viewer only; recipe source unchanged: {viewerOnlyImportFileName} ({elapsedMilliseconds} ms).");
        OVLog.Write(LogCategory.UI, LogLevel.Info, $"{format} Viewer-only import completed in {elapsedMilliseconds} ms: {Path.GetFullPath(path)}");
    }

    private void ClearViewerOnlyImport()
    {
        if (!HasViewerOnlyImport)
        {
            return;
        }

        viewerOnlyImportFileName = string.Empty;
        viewerOnlyImportFormat = string.Empty;
        OnPropertyChanged(nameof(HasViewerOnlyImport));
        OnPropertyChanged(nameof(ViewerOnlyImportSummary));
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
        import3DDataCommand.RaiseCanExecuteChanged();
        cancelC3DSourceLoadCommand.RaiseCanExecuteChanged();
    }
}
