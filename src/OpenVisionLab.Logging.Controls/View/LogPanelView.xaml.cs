using OpenVisionLab.Logging.Controls.ViewModel;
using System.Collections.Specialized;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace OpenVisionLab.Logging.Controls.View
{
    public partial class LogPanelView : UserControl, IDisposable
    {
        private readonly LogPanelViewModel viewModel;
        private bool isAttached;
        private bool isDisposed;

        public LogPanelView()
        {
            InitializeComponent();
            viewModel = new LogPanelViewModel();
            viewModel.Suspend();
            DataContext = viewModel;
            Loaded += LogPanelView_Loaded;
            Unloaded += LogPanelView_Unloaded;
        }

        private void LogPanelView_Loaded(object sender, RoutedEventArgs e)
        {
            if (isDisposed || isAttached) return;
            isAttached = true;
            viewModel.FilteredLogs.CollectionChanged += FilteredLogs_CollectionChanged;
            SizeChanged += LogPanelView_SizeChanged;
            viewModel.Resume();
            viewModel.IsCompactLayout = ActualHeight < 190 || ActualWidth < 720;
        }

        private void DetachViewObservers()
        {
            if (!isAttached) return;
            isAttached = false;
            viewModel.FilteredLogs.CollectionChanged -= FilteredLogs_CollectionChanged;
            SizeChanged -= LogPanelView_SizeChanged;
        }

        private void LogPanelView_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            viewModel.IsCompactLayout = ActualHeight < 190 || ActualWidth < 720;
        }

        private void FilteredLogs_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (isDisposed || !isAttached || !viewModel.AutoScroll || LogList.Items.Count == 0)
            {
                return;
            }

            Dispatcher.BeginInvoke(new System.Action(() =>
            {
                if (isDisposed || !isAttached || !viewModel.AutoScroll || LogList.Items.Count == 0)
                {
                    return;
                }

                object lastItem = LogList.Items[LogList.Items.Count - 1];
                LogList.ScrollIntoView(lastItem);
            }), DispatcherPriority.Background);
        }

        private void LogPanelView_Unloaded(object sender, RoutedEventArgs e)
        {
            if (isDisposed) return;
            DetachViewObservers();
            viewModel.Suspend();
        }

        public void Dispose()
        {
            if (isDisposed) return;
            isDisposed = true;
            DetachViewObservers();
            Loaded -= LogPanelView_Loaded;
            Unloaded -= LogPanelView_Unloaded;
            viewModel.Dispose();
        }
    }
}
