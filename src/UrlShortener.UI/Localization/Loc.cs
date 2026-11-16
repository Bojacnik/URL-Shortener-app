using System.ComponentModel;
using System.Globalization;
using System.Text.Json;

namespace UrlShortener.UI.Localization;

/// <param name="Code">"system" or a culture code like "cs" or "pt-BR".</param>
/// <param name="NativeName">The language's name in that language, e.g. "Čeština".</param>
public sealed record LanguageInfo(string Code, string NativeName, bool IsRightToLeft = false);

/// <summary>
/// UI strings for all supported languages. The tables are JSON files in i18n/, built into the assembly.
/// XAML binds through the indexer (<c>{l:T Key}</c>); switching language refreshes every bound string.
/// Missing keys fall back to English, then to the key itself.
/// </summary>
public sealed class Loc : INotifyPropertyChanged
{
    public const string SystemCode = "system";
    public const string FallbackCode = "en";

    public static IReadOnlyList<LanguageInfo> Languages { get; } =
    [
        new("en", "English"),
        new("es", "Español"),
        new("cs", "Čeština"),
        new("de", "Deutsch"),
        new("fr", "Français"),
        new("it", "Italiano"),
        new("pt-BR", "Português (Brasil)"),
        new("pl", "Polski"),
        new("nl", "Nederlands"),
        new("ru", "Русский"),
        new("uk", "Українська"),
        new("tr", "Türkçe"),
        new("id", "Bahasa Indonesia"),
        new("ar", "العربية", IsRightToLeft: true),
        new("hi", "हिन्दी"),
        new("zh-Hans", "简体中文"),
        new("ja", "日本語"),
        new("ko", "한국어"),
    ];

    // Declared after Languages on purpose: static fields initialise in order, and the constructor reads Languages.
    public static Loc Instance { get; } = new();

    private readonly Dictionary<string, string> _fallback;
    private Dictionary<string, string> _strings;

    private Loc()
    {
        _fallback = Load(FallbackCode) ?? [];
        _strings = _fallback;
        Language = Languages[0];
        Culture = CultureInfo.GetCultureInfo(FallbackCode);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Raised after the language changes, for view models with computed text.</summary>
    public event EventHandler? LanguageChanged;

    public LanguageInfo Language { get; private set; }
    public CultureInfo Culture { get; private set; }
    public bool IsRightToLeft => Language.IsRightToLeft;

    public string this[string key] =>
        _strings.TryGetValue(key, out var value) || _fallback.TryGetValue(key, out value) ? value : key;

    public string Format(string key, params object?[] args) => string.Format(Culture, this[key], args);

    /// <summary>Switches language. "system" (or null/unknown) picks the closest match to the OS language.</summary>
    public void SetLanguage(string? code)
    {
        var language = Resolve(code, CultureInfo.CurrentUICulture);
        if (language == Language && _strings.Count > 0) return;

        _strings = Load(language.Code) ?? _fallback;
        Language = language;
        Culture = CultureInfo.GetCultureInfo(language.Code);

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Language)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsRightToLeft)));
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Picks a supported language: exact code, then the OS culture and its parents, then English.</summary>
    public static LanguageInfo Resolve(string? code, CultureInfo system)
    {
        if (!string.IsNullOrEmpty(code) && code != SystemCode && Find(code) is { } exact)
            return exact;

        if (Find(system.Name) is { } match) return match;
        return system.TwoLetterISOLanguageName switch
        {
            "zh" => Find("zh-Hans")!,   // zh-CN, zh-TW … → Simplified Chinese is the one we ship
            "pt" => Find("pt-BR")!,     // pt-PT, pt-AO … → Brazilian Portuguese
            var two => Find(two) ?? Languages[0],
        };
    }

    private static LanguageInfo? Find(string code) =>
        Languages.FirstOrDefault(l => string.Equals(l.Code, code, StringComparison.OrdinalIgnoreCase));

    internal static Dictionary<string, string>? Load(string code)
    {
        using var stream = typeof(Loc).Assembly.GetManifestResourceStream($"i18n.{code}.json");
        if (stream is null) return null;
        return JsonSerializer.Deserialize(stream, LocJsonContext.Default.DictionaryStringString);
    }
}

[System.Text.Json.Serialization.JsonSerializable(typeof(Dictionary<string, string>))]
internal partial class LocJsonContext : System.Text.Json.Serialization.JsonSerializerContext;
