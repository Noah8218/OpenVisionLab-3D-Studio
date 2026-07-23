namespace OpenVisionLab.Wpf.MessageDialogs
{
    public sealed class WpfMessageDialogOptions
    {
        public string Title { get; set; } = "Message";

        public string Message { get; set; } = string.Empty;

        public string Details { get; set; } = string.Empty;

        public WpfMessageDialogKind Kind { get; set; } = WpfMessageDialogKind.Info;

        public WpfMessageDialogButtons Buttons { get; set; } = WpfMessageDialogButtons.OK;

        public WpfMessageDialogResult DefaultResult { get; set; } = WpfMessageDialogResult.None;

        public string PrimaryButtonText { get; set; }

        public string SecondaryButtonText { get; set; }

        public string TertiaryButtonText { get; set; }

        public bool TopMost { get; set; }

        public double MaxWidth { get; set; } = 560D;
    }
}
