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

    // The binding reaches Loc's indexer by reflection; DynamicDependency keeps it when trimming,
    // which is exactly what the trimmer's warning is about.
    [DynamicDependency("get_Item(System.String)", typeof(Loc))]
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Loc's indexer is preserved by the DynamicDependency above.")]
    public override object ProvideValue(IServiceProvider serviceProvider) =>
        new ReflectionBinding($"[{Key}]") { Source = Loc.Instance, Mode = BindingMode.OneWay };
}
