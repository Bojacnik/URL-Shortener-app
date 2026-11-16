using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UrlShortener.Core;
using UrlShortener.UI.Localization;

namespace UrlShortener.UI.ViewModels;

/// <summary>"Where does this short link go?" — follows redirects without opening the page.</summary>
public sealed partial class ExpandViewModel(MainViewModel owner, LinkExpander expander) : ObservableObject
{
    private ExpandResult? _result;
    private LocalizedText? _error;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExpandCommand))]
    [NotifyPropertyChangedFor(nameof(InputHint), nameof(HasInput))]
    public partial string Input { get; set; } = "";

    public bool HasInput => !string.IsNullOrEmpty(Input);

    public string? InputHint =>
        string.IsNullOrWhiteSpace(Input) ? null
        : UrlValidator.TryNormalize(Input, out _, out UrlProblem problem) ? null
        : Messages.Url(problem).ToString();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExpandCommand))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial bool ShowPath { get; set; }

    [ObservableProperty]
    public partial bool JustCopied { get; set; }

    public bool HasResult => _result is { IsSuccess: true };
    public string? ErrorMessage => _error?.ToString();
    public bool HasError => _error is not null;

    public string FinalUrl => _result?.Final.AbsoluteUri ?? "";

    /// <summary>The destination's site, shown big so it's obvious where you'd end up.</summary>
    public string FinalHost => _result is null ? "" : StripWww(_result.Final.IdnHost == _result.Final.Host ? _result.Final.Host : _result.Final.IdnHost);

    public IReadOnlyList<string> Path => _result?.Chain.Select(u => u.AbsoluteUri).ToList() ?? [];
    public bool HasPath => (_result?.Redirects ?? 0) > 0;

    public string RedirectsText => _result is null ? ""
        : _result.Redirects == 0 ? Loc.Instance["Expand.Direct"]
        : Loc.Instance.Format("Expand.Redirects", _result.Redirects);

    /// <summary>Why following stopped early, if it did.</summary>
    public string? WarningText => _result?.Stop switch
    {
        ExpandStop.PrivateAddress => Loc.Instance["Expand.Private"],
        ExpandStop.TooManyRedirects => Loc.Instance.Format("Expand.TooMany", LinkExpander.MaxRedirects),
        ExpandStop.Loop => Loc.Instance["Expand.Loop"],
        _ => null,
    };

    public bool HasWarning => WarningText is not null;

    private bool CanExpand() => !IsBusy && !string.IsNullOrWhiteSpace(Input) && InputHint is null;

    [RelayCommand(CanExecute = nameof(CanExpand), IncludeCancelCommand = true)]
    private async Task ExpandAsync(CancellationToken cancellationToken)
    {
        if (!UrlValidator.TryNormalize(Input, out var url, out UrlProblem _)) return;
        IsBusy = true;
        SetResult(null, null);
        ShowPath = false;
        try
        {
            var result = await expander.ExpandAsync(url, cancellationToken);
            SetResult(result, result.IsSuccess ? null : new LocalizedText("Expand.Failed"));
        }
        catch (OperationCanceledException)
        {
            // Stopped by the user; nothing to show.
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CopyAsync()
    {
        if (!HasResult) return;
        await owner.Platform.CopyTextAsync(FinalUrl);
        JustCopied = true;
        await Task.Delay(owner.FeedbackDuration);
        JustCopied = false;
    }

    [RelayCommand]
    private Task OpenAsync() => HasResult ? owner.OpenUrlAsync(FinalUrl) : Task.CompletedTask;

    [RelayCommand]
    private void ShortenThis()
    {
        if (HasResult) owner.StartShortening(FinalUrl);
    }

    [RelayCommand]
    private void TogglePath() => ShowPath = !ShowPath;

    [RelayCommand]
    private async Task PasteAsync()
    {
        var text = await owner.Platform.GetClipboardTextAsync();
        if (!string.IsNullOrWhiteSpace(text)) Input = UrlValidator.ExtractUrl(text);
        else owner.ShowToast(Loc.Instance["Toast.ClipboardEmpty"]);
    }

    [RelayCommand]
    private void Clear()
    {
        Input = "";
        SetResult(null, null);
    }

    internal void RefreshText() => OnPropertyChanged(string.Empty);

    partial void OnInputChanged(string value)
    {
        if (_error is not null) SetResult(_result, null);
    }

    private void SetResult(ExpandResult? result, LocalizedText? error)
    {
        _result = result is { IsSuccess: true } ? result : null;
        _error = error;
        OnPropertyChanged(string.Empty);
    }

    private static string StripWww(string host) =>
        host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? host[4..] : host;
}
