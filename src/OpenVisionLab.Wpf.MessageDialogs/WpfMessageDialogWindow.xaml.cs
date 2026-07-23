using System;
using System.Windows;
using System.Windows.Input;
using OpenVisionLab;

namespace OpenVisionLab.Wpf.MessageDialogs
{
    public partial class WpfMessageDialogWindow : Window
    {
        public WpfMessageDialogWindow(WpfMessageDialogOptions options)
        {
            InitializeComponent();

            options ??= new WpfMessageDialogOptions();
            Title = string.IsNullOrWhiteSpace(options.Title)
                ? OpenVisionLanguageService.T("MessageBox.Message")
                : options.Title;
            Topmost = options.TopMost;
            if (options.MaxWidth > 0D)
            {
                DialogControl.MaxWidth = options.MaxWidth;
            }

            DialogControl.Configure(options);
            DialogControl.DialogResultRequested += CloseWithResult;
        }

        public WpfMessageDialogResult Result { get; private set; } = WpfMessageDialogResult.None;

        private void CloseWithResult(WpfMessageDialogResult result)
        {
            Result = result;
            try
            {
                DialogResult = true;
            }
            catch (InvalidOperationException)
            {
            }

            Close();
        }

        private void OnWindowKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Escape)
            {
                return;
            }

            e.Handled = true;
            CloseWithResult(DialogControl.CancelResult);
        }
    }
}
