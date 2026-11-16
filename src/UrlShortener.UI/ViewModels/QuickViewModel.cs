using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UrlShortener.Core;
using UrlShortener.UI.Localization;
using UrlShortener.UI.Services;

namespace UrlShortener.UI.ViewModels;

public enum QuickState { Working, Done, Failed }

/// <summary>
/// "UrlShortener --quick": a small pop-up that shortens the link on the clipboard, puts the short link
/// back on the clipboard, then closes itself. Meant to be bound to a keyboard shortcut in the OS.
/// </summary>
public sealed partial class QuickViewModel(ShortenerService shortener, AppStorage storage, IPlatformServices platform) : ObservableObject
{
    internal TimeSpan DoneDelay { get; set; } = TimeSpan.FromSeconds(1.6);
    internal TimeSpan FailDelay { get; set; } = TimeSpan.FromSeconds(4);

    public event EventHandler? Finished;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsWorking), nameof(IsDone), nameof(IsFailed))]
    public partial QuickState State { get; set; }

    [ObservableProperty]
    public partial string Title { get; set; } = "";

    [ObservableProperty]
    public partial string Detail { get; set; } = "";

    public bool IsWorking => State == QuickState.Working;
    public bool IsDone => State == QuickState.Done;
    public bool IsFailed => State == QuickState.Failed;
    public string? ShortUrl { get; private set; }

    public async Task RunAsync()
    {
        var settings = await storage.LoadSettingsAsync();
        Loc.Instance.SetLanguage(settings.Language);
        platform.SetTheme(settings.Theme);

        State = QuickState.Working;
        Title = Loc.Instance["Quick.Working"];

        var text = await platform.GetClipboardTextAsync();
        var candidate = UrlValidator.ExtractUrl(text);
        if (!UrlValidator.TryNormalize(candidate, out var url, out UrlProblem problem))
        {
            Finish(QuickState.Failed, Loc.Instance["Quick.NoLink"],
                string.IsNullOrWhiteSpace(text) ? "" : Messages.Url(problem).ToString());
            await CloseLaterAsync(FailDelay);
            return;
        }

        var outcome = await shortener.ShortenAsync(url.AbsoluteUri, settings.PreferredProviderId, null, settings.AutoFallback);
        if (!outcome.IsSuccess)
        {
            Finish(QuickState.Failed, Loc.Instance["Quick.Failed"], Messages.Failure(outcome.Final, shortener).ToString());
            await CloseLaterAsync(FailDelay);
            return;
        }

        ShortUrl = outcome.Final.ShortUrl!;
        await platform.CopyTextAsync(ShortUrl);

        var history = await storage.LoadHistoryAsync();
        history.RemoveAll(h => string.Equals(h.ShortUrl, ShortUrl, StringComparison.OrdinalIgnoreCase));
        history.Insert(0, new HistoryEntry(url.AbsoluteUri, ShortUrl, outcome.Final.ProviderId, DateTimeOffset.Now));
        await storage.SaveHistoryAsync(history);

        Finish(QuickState.Done, Loc.Instance["Quick.Done"], HistoryItemViewModel.StripScheme(ShortUrl));
        await CloseLaterAsync(DoneDelay);
    }

    [RelayCommand]
    private void Close() => Finished?.Invoke(this, EventArgs.Empty);

    private void Finish(QuickState state, string title, string detail)
    {
        Title = title;
        Detail = detail;
        State = state;
    }

    private async Task CloseLaterAsync(TimeSpan delay)
    {
        await Task.Delay(delay);
        Finished?.Invoke(this, EventArgs.Empty);
    }
}
