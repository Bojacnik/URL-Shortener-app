using Avalonia.Controls;

namespace UrlShortener.UI.Views;

/// <summary>Desktop window; all content lives in <see cref="MainView"/>, which Android shows directly.</summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    public MainView MainView => View;
}
