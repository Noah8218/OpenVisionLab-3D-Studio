using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using OpenVisionLab;

namespace OpenVisionLab.Wpf.MessageDialogs
{
    public partial class WpfMessageDialogControl : UserControl
    {
        private bool detailsVisible;

        public WpfMessageDialogControl()
        {
            InitializeComponent();
        }

        public event Action<WpfMessageDialogResult> DialogResultRequested;

        public WpfMessageDialogResult CancelResult { get; private set; } = WpfMessageDialogResult.OK;

        public void Configure(WpfMessageDialogOptions options)
        {
            options ??= new WpfMessageDialogOptions();
            TitleText.Text = string.IsNullOrWhiteSpace(options.Title)
                ? Text("MessageBox.Message", "메시지", "Message")
                : options.Title;
            MessageText.Text = options.Message ?? string.Empty;
            DetailsTextBox.Text = options.Details ?? string.Empty;
            DetailsContainer.Visibility = string.IsNullOrWhiteSpace(options.Details) ? Visibility.Collapsed : Visibility.Visible;
            DetailsTextBox.Visibility = Visibility.Collapsed;
            DetailsToggleButton.Content = Text("MessageBox.TechnicalDetails", "상세 정보", "Technical Details");
            AutomationProperties.SetName(DetailsToggleButton, DetailsToggleButton.Content.ToString());
            detailsVisible = false;

            ApplyKind(options.Kind);
            RebuildButtons(options);
        }

        private void RebuildButtons(WpfMessageDialogOptions options)
        {
            ButtonPanel.Children.Clear();
            CancelResult = ResolveCancelResult(options.Buttons);
            WpfMessageDialogResult defaultResult = ResolveDefaultResult(options);

            foreach (WpfMessageDialogButtonSpec spec in BuildButtons(options))
            {
                var button = new Button
                {
                    Content = spec.Text,
                    Style = (Style)FindResource(spec.IsPrimary ? "PrimaryDialogButtonStyle" : "SecondaryDialogButtonStyle"),
                    IsDefault = spec.Result == defaultResult
                };
                AutomationProperties.SetName(button, spec.Text);

                WpfMessageDialogResult result = spec.Result;
                button.Click += (_, _) => DialogResultRequested?.Invoke(result);
                ButtonPanel.Children.Add(button);
            }
        }

        private static IReadOnlyList<WpfMessageDialogButtonSpec> BuildButtons(WpfMessageDialogOptions options)
        {
            string primary = Pick(options.PrimaryButtonText, Text("MessageBox.OK", "확인", "OK"));
            string secondary = Pick(options.SecondaryButtonText, Text("MessageBox.Cancel", "취소", "Cancel"));
            string tertiary = Pick(options.TertiaryButtonText, Text("MessageBox.Cancel", "취소", "Cancel"));

            return options.Buttons switch
            {
                WpfMessageDialogButtons.OKCancel => new[]
                {
                    new WpfMessageDialogButtonSpec(primary, WpfMessageDialogResult.OK, true),
                    new WpfMessageDialogButtonSpec(secondary, WpfMessageDialogResult.Cancel, false)
                },
                WpfMessageDialogButtons.YesNo => new[]
                {
                    new WpfMessageDialogButtonSpec(Pick(options.PrimaryButtonText, Text("MessageBox.Yes", "예", "Yes")), WpfMessageDialogResult.Yes, true),
                    new WpfMessageDialogButtonSpec(Pick(options.SecondaryButtonText, Text("MessageBox.No", "아니오", "No")), WpfMessageDialogResult.No, false)
                },
                WpfMessageDialogButtons.YesNoCancel => new[]
                {
                    new WpfMessageDialogButtonSpec(Pick(options.PrimaryButtonText, Text("MessageBox.Yes", "예", "Yes")), WpfMessageDialogResult.Yes, true),
                    new WpfMessageDialogButtonSpec(Pick(options.SecondaryButtonText, Text("MessageBox.No", "아니오", "No")), WpfMessageDialogResult.No, false),
                    new WpfMessageDialogButtonSpec(tertiary, WpfMessageDialogResult.Cancel, false)
                },
                WpfMessageDialogButtons.RetryCancel => new[]
                {
                    new WpfMessageDialogButtonSpec(Pick(options.PrimaryButtonText, Text("MessageBox.Retry", "다시 시도", "Retry")), WpfMessageDialogResult.Retry, true),
                    new WpfMessageDialogButtonSpec(secondary, WpfMessageDialogResult.Cancel, false)
                },
                _ => new[]
                {
                    new WpfMessageDialogButtonSpec(primary, WpfMessageDialogResult.OK, true)
                }
            };
        }

        private static WpfMessageDialogResult ResolveDefaultResult(WpfMessageDialogOptions options)
        {
            if (options.DefaultResult != WpfMessageDialogResult.None)
            {
                return options.DefaultResult;
            }

            return BuildButtons(options).FirstOrDefault(button => button.IsPrimary)?.Result ?? WpfMessageDialogResult.OK;
        }

        private static WpfMessageDialogResult ResolveCancelResult(WpfMessageDialogButtons buttons)
        {
            return buttons switch
            {
                WpfMessageDialogButtons.OKCancel => WpfMessageDialogResult.Cancel,
                WpfMessageDialogButtons.YesNo => WpfMessageDialogResult.No,
                WpfMessageDialogButtons.YesNoCancel => WpfMessageDialogResult.Cancel,
                WpfMessageDialogButtons.RetryCancel => WpfMessageDialogResult.Cancel,
                _ => WpfMessageDialogResult.OK
            };
        }

        private static string Pick(string configured, string fallback)
        {
            return string.IsNullOrWhiteSpace(configured) ? fallback : configured;
        }

        private static string Text(string key, string korean, string english)
        {
            var translated = OpenVisionLanguageService.T(key);
            if (!string.Equals(translated, key, StringComparison.Ordinal))
            {
                return translated;
            }

            return OpenVisionLanguageService.CurrentLanguage == OpenVisionLanguage.English ? english : korean;
        }

        private void ApplyKind(WpfMessageDialogKind kind)
        {
            string glyph;
            Color color;

            switch (kind)
            {
                case WpfMessageDialogKind.Success:
                    glyph = "OK";
                    color = Color.FromRgb(34, 197, 94);
                    break;
                case WpfMessageDialogKind.Warning:
                    glyph = "!";
                    color = Color.FromRgb(245, 158, 11);
                    break;
                case WpfMessageDialogKind.Error:
                    glyph = "X";
                    color = Color.FromRgb(239, 68, 68);
                    break;
                case WpfMessageDialogKind.Question:
                    glyph = "?";
                    color = Color.FromRgb(139, 92, 246);
                    break;
                case WpfMessageDialogKind.Normal:
                    glyph = "...";
                    color = Color.FromRgb(100, 116, 139);
                    break;
                default:
                    glyph = "i";
                    color = Color.FromRgb(59, 130, 246);
                    break;
            }

            var brush = new SolidColorBrush(color);
            KindGlyph.Text = glyph;
            KindGlyph.FontSize = glyph.Length > 1 ? 14D : 20D;
            KindBadge.Background = brush;
            AccentBar.Background = brush;
        }

        private void OnDetailsToggleClicked(object sender, RoutedEventArgs e)
        {
            detailsVisible = !detailsVisible;
            DetailsTextBox.Visibility = detailsVisible ? Visibility.Visible : Visibility.Collapsed;
            DetailsToggleButton.Content = detailsVisible
                ? Text("MessageBox.HideDetails", "상세 숨기기", "Hide Details")
                : Text("MessageBox.TechnicalDetails", "상세 정보", "Technical Details");
            AutomationProperties.SetName(DetailsToggleButton, DetailsToggleButton.Content.ToString());
        }
    }
}
