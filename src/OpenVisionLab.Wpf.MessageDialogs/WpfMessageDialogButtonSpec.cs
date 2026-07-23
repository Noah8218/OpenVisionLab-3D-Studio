namespace OpenVisionLab.Wpf.MessageDialogs
{
    internal sealed class WpfMessageDialogButtonSpec
    {
        public WpfMessageDialogButtonSpec(string text, WpfMessageDialogResult result, bool isPrimary)
        {
            Text = text;
            Result = result;
            IsPrimary = isPrimary;
        }

        public string Text { get; }

        public WpfMessageDialogResult Result { get; }

        public bool IsPrimary { get; }
    }
}
