using System.Windows.Input;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Compatibility bindings for source-load/import lifecycle state. Policy and
/// command lifetime live in <see cref="ToolWorkbenchSourceLoadOwner"/>.
/// </summary>
public sealed partial class ToolWorkbenchViewModel
{
    public event EventHandler? CancelC3DSourceLoadRequested
    {
        add => sourceLoadOwner.CancelC3DSourceLoadRequested += value;
        remove => sourceLoadOwner.CancelC3DSourceLoadRequested -= value;
    }

    public event EventHandler? Import3DDataRequested
    {
        add => sourceLoadOwner.Import3DDataRequested += value;
        remove => sourceLoadOwner.Import3DDataRequested -= value;
    }

    public ICommand CancelC3DSourceLoadCommand => sourceLoadOwner.CancelC3DSourceLoadCommand;
    public ICommand Import3DDataCommand => sourceLoadOwner.Import3DDataCommand;

    public bool IsC3DSourceLoading => sourceLoadOwner.IsLoading;

    public double C3DSourceLoadProgressPercent => sourceLoadOwner.ProgressPercent;

    public string C3DSourceLoadStatus => sourceLoadOwner.LoadStatus;

    public bool HasViewerOnlyImport => sourceLoadOwner.HasViewerOnlyImport;

    public string ViewerOnlyImportSummary => sourceLoadOwner.ViewerOnlyImportSummary;

    public void BeginC3DSourceLoad(string path) => sourceLoadOwner.BeginC3DSourceLoad(path);

    public void Begin3DDataImport(string path, string format) =>
        sourceLoadOwner.Begin3DDataImport(path, format);

    public void ReportC3DSourceLoadProgress(double percent) =>
        sourceLoadOwner.ReportC3DSourceLoadProgress(percent);

    public void CompleteC3DSourceLoad(string path, long elapsedMilliseconds) =>
        sourceLoadOwner.CompleteC3DSourceLoad(path, elapsedMilliseconds);

    public void CancelC3DSourceLoad(long elapsedMilliseconds) =>
        sourceLoadOwner.CancelC3DSourceLoad(elapsedMilliseconds);

    public void FailC3DSourceLoad(string path, long elapsedMilliseconds) =>
        sourceLoadOwner.FailC3DSourceLoad(path, elapsedMilliseconds);

    public void CompleteViewerOnlyImport(string path, string format, long elapsedMilliseconds) =>
        sourceLoadOwner.CompleteViewerOnlyImport(path, format, elapsedMilliseconds);
}
