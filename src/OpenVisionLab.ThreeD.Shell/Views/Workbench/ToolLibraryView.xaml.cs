using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

namespace OpenVisionLab.ThreeD.Shell.Views.Workbench;

public partial class ToolLibraryView : UserControl
{
    public ToolLibraryView()
    {
        InitializeComponent();
    }

    private void AllToolsList_MouseDoubleClick(object sender, MouseButtonEventArgs args)
    {
        if (FindAncestor<ButtonBase>(args.OriginalSource as DependencyObject) is not null
            || sender is not ListBox { SelectedItem: ToolWorkbenchToolItem tool }
            || DataContext is not ToolWorkbenchViewModel workbench
            || !workbench.AddSelectedToolCommand.CanExecute(tool))
        {
            return;
        }

        workbench.AddSelectedToolCommand.Execute(tool);
        args.Handled = true;
    }

    private static T? FindAncestor<T>(DependencyObject? source)
        where T : DependencyObject
    {
        while (source is not null)
        {
            if (source is T match)
            {
                return match;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return null;
    }
}
