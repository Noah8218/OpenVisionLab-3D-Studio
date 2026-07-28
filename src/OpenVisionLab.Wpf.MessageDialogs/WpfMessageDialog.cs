using System;
using System.Windows;
using System.Windows.Threading;

namespace OpenVisionLab.Wpf.MessageDialogs
{
    public static class WpfMessageDialog
    {
        public static WpfMessageDialogResult Show(string message)
        {
            return Show(null, new WpfMessageDialogOptions { Message = message ?? string.Empty });
        }

        public static WpfMessageDialogResult Show(Window owner, string title, string message, WpfMessageDialogKind kind = WpfMessageDialogKind.Info)
        {
            return Show(owner, new WpfMessageDialogOptions
            {
                Title = title,
                Message = message ?? string.Empty,
                Kind = kind
            });
        }

        public static WpfMessageDialogResult ShowInfo(Window owner, string title, string message, string primaryButtonText = null)
        {
            return Show(owner, new WpfMessageDialogOptions
            {
                Title = title,
                Message = message ?? string.Empty,
                Kind = WpfMessageDialogKind.Info,
                Buttons = WpfMessageDialogButtons.OK,
                PrimaryButtonText = primaryButtonText
            });
        }

        public static WpfMessageDialogResult ShowWarning(Window owner, string title, string message, string primaryButtonText = null)
        {
            return Show(owner, new WpfMessageDialogOptions
            {
                Title = title,
                Message = message ?? string.Empty,
                Kind = WpfMessageDialogKind.Warning,
                Buttons = WpfMessageDialogButtons.OK,
                PrimaryButtonText = primaryButtonText
            });
        }

        public static WpfMessageDialogResult Confirm(Window owner, string title, string message, string primaryButtonText = null, string secondaryButtonText = null)
        {
            return Show(owner, new WpfMessageDialogOptions
            {
                Title = title,
                Message = message ?? string.Empty,
                Kind = WpfMessageDialogKind.Question,
                Buttons = WpfMessageDialogButtons.YesNo,
                PrimaryButtonText = primaryButtonText,
                SecondaryButtonText = secondaryButtonText
            });
        }

        public static WpfMessageDialogResult Show(Window owner, WpfMessageDialogOptions options)
        {
            Dispatcher dispatcher = owner?.Dispatcher ?? Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                return dispatcher.Invoke(() => ShowOnCurrentThread(owner, options));
            }

            return ShowOnCurrentThread(owner, options);
        }

        private static WpfMessageDialogResult ShowOnCurrentThread(Window owner, WpfMessageDialogOptions options)
        {
            options ??= new WpfMessageDialogOptions();
            var window = new WpfMessageDialogWindow(options);

            if (owner != null && owner.IsVisible)
            {
                window.Owner = owner;
                window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }
            else
            {
                window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }

            bool? shown = window.ShowDialog();
            return shown == true ? window.Result : WpfMessageDialogResult.None;
        }
    }
}
