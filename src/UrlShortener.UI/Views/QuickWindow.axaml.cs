using Avalonia.Controls;
using Avalonia.Input;
using UrlShortener.UI.Localization;
using UrlShortener.UI.ViewModels;

namespace UrlShortener.UI.Views;

/// <summary>The pop-up for "UrlShortener --quick". Runs once it has focus, then closes itself.</summary>
public partial class QuickWindow : Window
{
    private bool _started;

    public QuickWindow()
    {
        InitializeComponent();
        // Wayland only lets the focused window read the clipboard, so start once we're activated.
        Activated += OnFirstActivated;
    }

    private async void OnFirstActivated(object? sender, EventArgs e)
    {
        if (_started || DataContext is not QuickViewModel vm) return;
        _started = true;
        FlowDirection = Loc.Instance.IsRightToLeft ? Avalonia.Media.FlowDirection.RightToLeft : Avalonia.Media.FlowDirection.LeftToRight;
        vm.Finished += (_, _) => Close();
        await vm.RunAsync();
    }

    private void OnCardPressed(object? sender, PointerPressedEventArgs e) =>
        (DataContext as QuickViewModel)?.CloseCommand.Execute(null);
}
