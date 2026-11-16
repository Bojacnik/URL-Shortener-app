using Avalonia.Controls;
using Avalonia.Controls.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;

namespace UrlShortener.UI.Views;

public partial class MainView : UserControl
{
    private IInsetsManager? _insets;

    public MainView()
    {
        InitializeComponent();
        ActualThemeVariantChanged += (_, _) => UpdateSystemBars();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        // Phones: Avalonia already keeps the view clear of the status and navigation bars (and the
        // keyboard, with adjustResize). Adding the safe-area padding on top of that doubled the gap
        // under the status bar, so the view doesn't pad for insets itself; it only colours the bars.
        _insets = TopLevel.GetTopLevel(this)?.InsetsManager;
        if (_insets is not null)
        {
            _insets.DisplayEdgeToEdgePreference = false;
            UpdateSystemBars();
        }

        // Focusing on a phone would pop the keyboard straight away; only do it on desktop.
        if (!OperatingSystem.IsAndroid() && !OperatingSystem.IsIOS())
            UrlBox.Focus();
    }

    /// <summary>System bars in the app's background colour, with icons that stay readable.</summary>
    private void UpdateSystemBars()
    {
        if (_insets is null) return;
        var dark = ActualThemeVariant == ThemeVariant.Dark;
        _insets.SystemBarColor = dark ? Color.Parse("#0D0E13") : Color.Parse("#F5F6FA");

        // SystemBarTheme only exists on some platforms' insets managers (Android); set it when present.
        // Light = light bar with dark icons, Dark = dark bar with light icons.
        if (_insets.GetType().GetProperty("SystemBarTheme") is { CanWrite: true } theme)
            theme.SetValue(_insets, dark ? SystemBarTheme.Dark : SystemBarTheme.Light);
    }
}
