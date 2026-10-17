using System.Windows;
using System.Windows.Controls;

namespace OpenVisionLab.ThreeD.Shell.Views.Workbench;

/// <summary>Global saved-results presentation; DataContext is inherited from the Shell ResultsWorkspaceView.</summary>
public partial class ResultsHistoryView : UserControl
{
    public ResultsHistoryView()
    {
        InitializeComponent();
        Loaded += (_, _) => ApplyLayout();
        SizeChanged += (_, _) => ApplyLayout();
    }

    private void ApplyLayout() =>
        HistoryColumn.Width = new GridLength((Window.GetWindow(this)?.ActualWidth ?? ActualWidth) > 1500 ? 320 : 270);

    internal bool HasOperatorSummaryAndCorrectionRoute => !string.IsNullOrWhiteSpace(ResultsSavedDecision.Text)
        && ResultsFixInTeachButton.IsTabStop && ResultsFixInTeachButton.Focusable
        && !string.IsNullOrWhiteSpace(ResultsFixInTeachButton.Content?.ToString());
}
