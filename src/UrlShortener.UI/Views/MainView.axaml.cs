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

        // Desktop: each time the window gets focus, look for a long link on the clipboard to offer.
        if (TopLevel.GetTopLevel(this) is Window window)
        {
            window.Activated -= OnWindowActivated;
            window.Activated += OnWindowActivated;
        }
    }

    private async void OnWindowActivated(object? sender, EventArgs e)
    {
        if (DataContext is ViewModels.MainViewModel vm)
            await vm.CheckClipboardAsync();
    }

    /// <summary>System bars in the app's background colour, with icons that stay readable.</summary>
    private void UpdateSystemBars()
    {
        if (_insets is null) return;
        var dark = ActualThemeVariant == ThemeVariant.Dark;
        _insets.SystemBarColor = dark ? Color.Parse("#0D0E13") : Color.Parse("#F5F6FA");
        // Icon colour (light icons on dark bars, dark icons on light bars) is set natively by the platform.
        App.SystemBarsHandler?.Invoke(dark);
    }
}
