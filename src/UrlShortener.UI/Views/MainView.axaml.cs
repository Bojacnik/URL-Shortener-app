using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Platform;
using Avalonia.Interactivity;

namespace UrlShortener.UI.Views;

public partial class MainView : UserControl
{
    private IInsetsManager? _insets;
    private IInputPane? _inputPane;

    public MainView()
    {
        InitializeComponent();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        var top = TopLevel.GetTopLevel(this);

        // On phones, keep content clear of the status/navigation bars and the on-screen keyboard.
        _insets = top?.InsetsManager;
        if (_insets is not null)
        {
            _insets.SafeAreaChanged += OnSafeAreaChanged;
            ApplyInsets();
        }

        _inputPane = top?.InputPane;
        if (_inputPane is not null)
            _inputPane.StateChanged += OnInputPaneChanged;

        // Focusing on a phone would pop the keyboard straight away; only do it on desktop.
        if (!OperatingSystem.IsAndroid() && !OperatingSystem.IsIOS())
            UrlBox.Focus();
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        if (_insets is not null) _insets.SafeAreaChanged -= OnSafeAreaChanged;
        if (_inputPane is not null) _inputPane.StateChanged -= OnInputPaneChanged;
        base.OnUnloaded(e);
    }

    private void OnSafeAreaChanged(object? sender, SafeAreaChangedArgs e) => ApplyInsets();

    private void OnInputPaneChanged(object? sender, InputPaneStateEventArgs e) => ApplyInsets();

    private void ApplyInsets()
    {
        var safe = _insets?.SafeAreaPadding ?? default;
        var keyboard = _inputPane?.State == InputPaneState.Open ? _inputPane.OccludedRect.Height : 0;
        Root.Margin = new Thickness(safe.Left, safe.Top, safe.Right, Math.Max(safe.Bottom, keyboard));
    }
}
