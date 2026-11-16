using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;

namespace UrlShortener.UI.Views;

public partial class MainView : UserControl
{
    private static readonly Thickness ContentMargin = new(18, 14, 18, 40);
    private static readonly Thickness ToastMargin = new(20, 0, 20, 28);

    private IInsetsManager? _insets;
    private IInputPane? _inputPane;

    public MainView()
    {
        InitializeComponent();
        ActualThemeVariantChanged += (_, _) => UpdateSystemBars();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        var top = TopLevel.GetTopLevel(this);

        // Phones: draw edge to edge, so the background and glow continue behind the status and
        // navigation bars, then pad the content ourselves, exactly once, by the safe area.
        _insets = top?.InsetsManager;
        if (_insets is not null)
        {
            _insets.DisplayEdgeToEdgePreference = true;
            _insets.SafeAreaChanged += OnSafeAreaChanged;
            UpdateSystemBars();
        }

        _inputPane = top?.InputPane;
        if (_inputPane is not null)
            _inputPane.StateChanged += OnInputPaneChanged;

        ApplyInsets();

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
        // Only pad for the system bars when we really are drawing behind them; otherwise the OS has
        // already moved the window below them, and padding again leaves an empty band at the top.
        var safe = _insets is { DisplaysEdgeToEdge: true } ? _insets.SafeAreaPadding : default;
        var keyboard = _inputPane?.State == InputPaneState.Open ? _inputPane.OccludedRect.Height : 0;

        // The scroll area stops at the status bar and above the keyboard …
        Scroller.Margin = new Thickness(safe.Left, safe.Top, safe.Right, keyboard);
        // … while the last item can still scroll clear of the gesture/navigation bar.
        ContentStack.Margin = ContentMargin + new Thickness(0, 0, 0, keyboard > 0 ? 0 : safe.Bottom);
        Toast.Margin = ToastMargin + new Thickness(0, 0, 0, Math.Max(safe.Bottom, keyboard));
    }

    /// <summary>Status-bar icons: dark on the light theme, light on the dark theme.</summary>
    private void UpdateSystemBars()
    {
        if (_insets is null) return;
        var dark = ActualThemeVariant == ThemeVariant.Dark;
        _insets.SystemBarColor = Colors.Transparent;

        // SystemBarTheme only exists on some platforms' insets managers (Android); set it when present.
        if (_insets.GetType().GetProperty("SystemBarTheme") is { CanWrite: true } theme)
            theme.SetValue(_insets, dark ? SystemBarTheme.Dark : SystemBarTheme.Light);
    }
}
