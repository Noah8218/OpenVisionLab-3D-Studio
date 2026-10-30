using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Wpf.Ui.Controls;

namespace OpenVisionLab.ThreeD.Shell.Views.Shell;

public partial class PathInput : UserControl
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(nameof(Text), typeof(string), typeof(PathInput), new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
    public static readonly DependencyProperty BrowseCommandProperty = DependencyProperty.Register(nameof(BrowseCommand), typeof(ICommand), typeof(PathInput));
    public static readonly DependencyProperty BrowseTextProperty = DependencyProperty.Register(nameof(BrowseText), typeof(string), typeof(PathInput), new PropertyMetadata(string.Empty));
    public static readonly DependencyProperty BrowseSymbolProperty = DependencyProperty.Register(nameof(BrowseSymbol), typeof(SymbolRegular), typeof(PathInput), new PropertyMetadata(SymbolRegular.FolderOpen24));
    public static readonly DependencyProperty IsReadOnlyProperty = DependencyProperty.Register(nameof(IsReadOnly), typeof(bool), typeof(PathInput), new PropertyMetadata(false));
    public static readonly DependencyProperty InputAutomationIdProperty = DependencyProperty.Register(nameof(InputAutomationId), typeof(string), typeof(PathInput), new PropertyMetadata(string.Empty));
    public static readonly DependencyProperty BrowseAutomationIdProperty = DependencyProperty.Register(nameof(BrowseAutomationId), typeof(string), typeof(PathInput), new PropertyMetadata(string.Empty));

    public PathInput()
    {
        InitializeComponent();
        // 초기 접힘 상태의 Icon은 조상과 이름 범위를 찾지 못하므로 소유자를 직접 지정한다.
        ((SymbolIcon)BrowseButton.Icon!).SetBinding(SymbolIcon.SymbolProperty, new Binding(nameof(BrowseSymbol)) { Source = this });
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public ICommand? BrowseCommand
    {
        get => (ICommand?)GetValue(BrowseCommandProperty);
        set => SetValue(BrowseCommandProperty, value);
    }

    public string BrowseText
    {
        get => (string)GetValue(BrowseTextProperty);
        set => SetValue(BrowseTextProperty, value);
    }

    public SymbolRegular BrowseSymbol
    {
        get => (SymbolRegular)GetValue(BrowseSymbolProperty);
        set => SetValue(BrowseSymbolProperty, value);
    }

    public bool IsReadOnly
    {
        get => (bool)GetValue(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    public string InputAutomationId
    {
        get => (string)GetValue(InputAutomationIdProperty);
        set => SetValue(InputAutomationIdProperty, value);
    }

    public string BrowseAutomationId
    {
        get => (string)GetValue(BrowseAutomationIdProperty);
        set => SetValue(BrowseAutomationIdProperty, value);
    }
}
