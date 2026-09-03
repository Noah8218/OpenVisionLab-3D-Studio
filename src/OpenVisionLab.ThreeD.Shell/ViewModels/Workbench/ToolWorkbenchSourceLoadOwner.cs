using System.Globalization;
using System.IO;
using System.Threading;
using System.Windows.Input;
using OpenVisionLab;
using OpenVisionLab.Logging;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Owns source-load/import lifecycle state and commands. The Workbench facade
/// retains its public compatibility surface while Shell remains the WPF/data
/// bridge that performs the actual load.
/// </summary>
internal sealed class ToolWorkbenchSourceLoadOwner : IDisposable
{
    private readonly ThreeDLocalization localization;
    private readonly Action<string> notifyPropertyChanged;
    private readonly Action<string, string> appendLog;
    private readonly RelayCommand loadC3DSourceCommand;
    private readonly RelayCommand import3DDataCommand;
    private readonly RelayCommand cancelC3DSourceLoadCommand;
    private bool isSourceLoading;
    private double progressPercent;
    private string fileName = string.Empty;
    private string activeImportFormat = "C3D";
    private string viewerOnlyImportFileName = string.Empty;
    private string viewerOnlyImportFormat = string.Empty;
    private int disposalState;

    public ToolWorkbenchSourceLoadOwner(
        ThreeDLocalization localization,
        Action<string> notifyPropertyChanged,
        Action<string, string> appendLog)
    {
        this.localization = localization ?? throw new ArgumentNullException(nameof(localization));
        this.notifyPropertyChanged = notifyPropertyChanged ?? throw new ArgumentNullException(nameof(notifyPropertyChanged));
        this.appendLog = appendLog ?? throw new ArgumentNullException(nameof(appendLog));
        loadC3DSourceCommand = new RelayCommand(
            _ => LoadC3DSourceRequested?.Invoke(this, EventArgs.Empty),
            _ => !IsLoading);
        import3DDataCommand = new RelayCommand(
            _ => Import3DDataRequested?.Invoke(this, EventArgs.Empty),
            _ => !IsLoading);
        cancelC3DSourceLoadCommand = new RelayCommand(
            _ => CancelC3DSourceLoadRequested?.Invoke(this, EventArgs.Empty),
            _ => IsLoading);
        localization.PropertyChanged += OnLocalizationChanged;
    }

    public event EventHandler? LoadC3DSourceRequested;
    public event EventHandler? CancelC3DSourceLoadRequested;
    public event EventHandler? Import3DDataRequested;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposalState, 1) != 0)
        {
            return;
        }

        localization.PropertyChanged -= OnLocalizationChanged;
    }

    public ICommand LoadC3DSourceCommand => loadC3DSourceCommand;
    public ICommand Import3DDataCommand => import3DDataCommand;
    public ICommand CancelC3DSourceLoadCommand => cancelC3DSourceLoadCommand;

    public bool IsLoading => isSourceLoading;
    public double ProgressPercent => progressPercent;
    public string LoadStatus => string.Format(
        CultureInfo.CurrentCulture,
        localization.Loading3DDataFormat,
        fileName,
        progressPercent);
    public bool HasViewerOnlyImport => !string.IsNullOrEmpty(viewerOnlyImportFormat);
    public string ViewerOnlyImportSummary => HasViewerOnlyImport
        ? string.Format(
            CultureInfo.CurrentCulture,
            localization.ViewerOnlyImportSummaryFormat,
            viewerOnlyImportFormat,
            viewerOnlyImportFileName)
        : string.Empty;

    public void BeginC3DSourceLoad(string path) => Begin3DDataImport(path, "C3D");

    public void Begin3DDataImport(string path, string format)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(format);
        fileName = Path.GetFileName(path);
        activeImportFormat = format;
        progressPercent = 0.0;
        isSourceLoading = true;
        NotifyLoadState();
        appendLog("Source", $"{format} import started: {fileName}.");
        OVLog.Write(
            LogCategory.UI,
            LogLevel.Info,
            $"{format} import started: {Path.GetFullPath(path)}");
    }

    public void ReportC3DSourceLoadProgress(double percent)
    {
        if (!isSourceLoading)
        {
            return;
        }

        var normalized = Math.Clamp(percent, 0.0, 100.0);
        if (Math.Abs(progressPercent - normalized) < 0.1)
        {
            return;
        }

        progressPercent = normalized;
        notifyPropertyChanged(nameof(ProgressPercent));
        notifyPropertyChanged(nameof(LoadStatus));
    }

    public void CompleteC3DSourceLoad(string path, long elapsedMilliseconds)
    {
        ClearViewerOnlyImport();
        progressPercent = 100.0;
        EndLoad();
        appendLog("Source", $"C3D source loaded: {Path.GetFileName(path)} ({elapsedMilliseconds} ms).");
        OVLog.Write(
            LogCategory.UI,
            LogLevel.Info,
            $"C3D source load completed in {elapsedMilliseconds} ms: {Path.GetFullPath(path)}");
    }

    public void CancelC3DSourceLoad(long elapsedMilliseconds)
    {
        var currentFileName = fileName;
        var format = activeImportFormat;
        EndLoad();
        appendLog(
            "Source",
            $"{format} import cancelled; current source retained: {currentFileName} ({elapsedMilliseconds} ms).");
        OVLog.Write(
            LogCategory.UI,
            LogLevel.Warning,
            $"{format} import cancelled after {elapsedMilliseconds} ms: {currentFileName}");
    }

    public void FailC3DSourceLoad(string path, long elapsedMilliseconds)
    {
        var format = activeImportFormat;
        EndLoad();
        appendLog(
            "Source",
            $"{format} import failed; current source retained: {Path.GetFileName(path)} ({elapsedMilliseconds} ms).");
        OVLog.Write(
            LogCategory.UI,
            LogLevel.Error,
            $"{format} import failed after {elapsedMilliseconds} ms: {Path.GetFullPath(path)}");
    }

    public void CompleteViewerOnlyImport(string path, string format, long elapsedMilliseconds)
    {
        viewerOnlyImportFileName = Path.GetFileName(path);
        viewerOnlyImportFormat = format;
        EndLoad();
        notifyPropertyChanged(nameof(HasViewerOnlyImport));
        notifyPropertyChanged(nameof(ViewerOnlyImportSummary));
        appendLog(
            "Source",
            $"{format} imported for Viewer only; recipe source unchanged: {viewerOnlyImportFileName} ({elapsedMilliseconds} ms).");
        OVLog.Write(
            LogCategory.UI,
            LogLevel.Info,
            $"{format} Viewer-only import completed in {elapsedMilliseconds} ms: {Path.GetFullPath(path)}");
    }

    private void ClearViewerOnlyImport()
    {
        if (!HasViewerOnlyImport)
        {
            return;
        }

        viewerOnlyImportFileName = string.Empty;
        viewerOnlyImportFormat = string.Empty;
        notifyPropertyChanged(nameof(HasViewerOnlyImport));
        notifyPropertyChanged(nameof(ViewerOnlyImportSummary));
    }

    private void EndLoad()
    {
        isSourceLoading = false;
        NotifyLoadState();
    }

    private void NotifyLoadState()
    {
        notifyPropertyChanged(nameof(IsLoading));
        notifyPropertyChanged(nameof(ProgressPercent));
        notifyPropertyChanged(nameof(LoadStatus));
        loadC3DSourceCommand.RaiseCanExecuteChanged();
        import3DDataCommand.RaiseCanExecuteChanged();
        cancelC3DSourceLoadCommand.RaiseCanExecuteChanged();
    }

    private void OnLocalizationChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        if (string.IsNullOrEmpty(args.PropertyName)
            || args.PropertyName == nameof(ThreeDLocalization.Loading3DDataFormat)
            || args.PropertyName == nameof(ThreeDLocalization.ViewerOnlyImportSummaryFormat))
        {
            notifyPropertyChanged(nameof(LoadStatus));
            notifyPropertyChanged(nameof(ViewerOnlyImportSummary));
        }
    }
}
