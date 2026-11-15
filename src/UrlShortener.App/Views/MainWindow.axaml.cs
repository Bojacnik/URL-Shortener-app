using Avalonia.Controls;
using Avalonia.Interactivity;

namespace UrlShortener.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        UrlBox.Focus();
    }
}
