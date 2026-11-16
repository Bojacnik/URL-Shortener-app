using System.Diagnostics.CodeAnalysis;
using Avalonia.Data;
using Avalonia.Markup.Xaml;

namespace UrlShortener.UI.Localization;

/// <summary>
/// XAML markup for a translated string that updates when the language changes:
/// <c>Text="{l:T Shorten.Button}"</c>.
/// </summary>
public sealed class T(string key) : MarkupExtension
{
    public string Key { get; } = key;

    // The binding reaches Loc's indexer by reflection; make sure trimming never removes it.
    [DynamicDependency("get_Item(System.String)", typeof(Loc))]
    public override object ProvideValue(IServiceProvider serviceProvider) =>
        new ReflectionBinding($"[{Key}]") { Source = Loc.Instance, Mode = BindingMode.OneWay };
}
