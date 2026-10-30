using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Data;

namespace OpenVisionLab.ThreeD.Shell.Behaviors;

public static class WordBoundaryWrapBehavior
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.RegisterAttached(
        "Text", typeof(string), typeof(WordBoundaryWrapBehavior), new PropertyMetadata(string.Empty, OnTextChanged));

    public static string GetText(DependencyObject element) => (string?)element.GetValue(TextProperty) ?? string.Empty;
    public static void SetText(DependencyObject element, string value) => element.SetValue(TextProperty, value);

    private static void OnTextChanged(DependencyObject element, DependencyPropertyChangedEventArgs args)
    {
        if (element is not TextBlock textBlock) return;
        textBlock.SizeChanged -= OnSizeChanged;
        if (!string.IsNullOrEmpty(GetText(textBlock))) textBlock.SizeChanged += OnSizeChanged;
        AutomationProperties.SetName(textBlock, GetText(textBlock));
        Update(textBlock);
    }

    private static void OnSizeChanged(object sender, SizeChangedEventArgs args)
    {
        if (args.WidthChanged) Update((TextBlock)sender);
    }

    private static void Update(TextBlock textBlock)
    {
        var width = textBlock.ActualWidth - textBlock.Padding.Left - textBlock.Padding.Right;
        textBlock.Inlines.Clear();
        foreach (var part in Regex.Split(GetText(textBlock), @"(\s+)"))
        {
            if (part.Length == 0) continue;
            if (string.IsNullOrWhiteSpace(part)) textBlock.Inlines.Add(new Run(part));
            else
            {
                var word = new TextBlock
                {
                    Text = part, TextWrapping = TextWrapping.Wrap,
                    MaxWidth = width > 0 ? width : double.PositiveInfinity, IsHitTestVisible = false
                };
                foreach (var property in new[] { TextBlock.FontFamilyProperty, TextBlock.FontSizeProperty, TextBlock.FontWeightProperty, TextBlock.ForegroundProperty })
                    word.SetBinding(property, new Binding { Source = textBlock, Path = new PropertyPath(property) });
                textBlock.Inlines.Add(new InlineUIContainer(word) { BaselineAlignment = BaselineAlignment.Baseline });
            }
        }
    }
}
