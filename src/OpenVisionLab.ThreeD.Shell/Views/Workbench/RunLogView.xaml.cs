using System.Windows.Controls;

namespace OpenVisionLab.ThreeD.Shell.Views.Workbench;

using System;

public partial class RunLogView : UserControl, IDisposable
{
    public RunLogView()
    {
        InitializeComponent();
    }

    public void Dispose() => ApplicationLogPanel.Dispose();
}
