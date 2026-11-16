using CommunityToolkit.Mvvm.ComponentModel;
using UrlShortener.Core;
using UrlShortener.Core.Providers;
using UrlShortener.UI.Localization;

namespace UrlShortener.UI.ViewModels;

public enum AppMode { Shorten, Batch, Expand }

/// <summary>An option in a picker (mode, theme, language) whose label follows the UI language.</summary>
public abstract class Choice(string labelKey, string? fixedLabel) : ObservableObject
{
    /// <summary>Translated label, or a fixed one (language names are always shown in their own language).</summary>
    public string Label => fixedLabel ?? Loc.Instance[labelKey];

    public void Refresh() => OnPropertyChanged(nameof(Label));

    public override string ToString() => Label;
}

public sealed class Choice<T>(T value, string labelKey, string? fixedLabel = null) : Choice(labelKey, fixedLabel)
{
    public T Value { get; } = value;
}

/// <summary>Text that's translated when it's shown, so it changes with the language.</summary>
public sealed class LocalizedText(string key, params object?[] args)
{
    public string Key { get; } = key;
    public override string ToString() => args.Length == 0 ? Loc.Instance[Key] : Loc.Instance.Format(Key, args);
}

/// <summary>Turns the Core's error codes into translated messages.</summary>
public static class Messages
{
    public static LocalizedText Url(UrlProblem problem) => new($"Url.{problem}");

    public static LocalizedText Alias(IShortenerProvider provider, AliasProblem problem) => problem switch
    {
        AliasProblem.NotSupported => new("Alias.NotSupported", provider.DisplayName),
        AliasProblem.Length => new("Alias.Length", provider.DisplayName, provider.AliasRule!.MinLength, provider.AliasRule.MaxLength),
        _ => new(provider.AliasRule?.AllowsDash == true ? "Alias.CharactersDash" : "Alias.Characters"),
    };

    public static LocalizedText Failure(ShortenResult result, ShortenerService service)
    {
        var name = service.Find(result.ProviderId)?.DisplayName ?? result.ProviderId;
        return result.Failure switch
        {
            FailureKind.RateLimited => new("Error.RateLimited", name),
            FailureKind.ServiceError => new("Error.Service", name),
            FailureKind.Network => new("Error.Network", name),
            FailureKind.InvalidUrl => new("Error.Rejected", name),
            FailureKind.AliasRejected => new("Error.AliasTaken", name),
            _ => new("Error.Generic"),
        };
    }
}
