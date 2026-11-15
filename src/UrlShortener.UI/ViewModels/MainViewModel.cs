using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UrlShortener.Core;
using UrlShortener.Core.Providers;
using UrlShortener.UI.Services;

namespace UrlShortener.UI.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly ShortenerService _shortener;
    private readonly AppStorage _storage;
    private readonly IPlatformServices _platform;
    private readonly List<HistoryItemViewModel> _allHistory = [];
    private bool _loadingSettings;
    private int _toastVersion;
    private int _copiedVersion;

    public MainViewModel(ShortenerService shortener, AppStorage storage, IPlatformServices platform)
    {
        _shortener = shortener;
        _storage = storage;
        _platform = platform;
        Providers = shortener.Providers;
        _loadingSettings = true;
        SelectedProvider = Providers[0];
        _loadingSettings = false;
    }

    /// <summary>Design-time constructor for the XAML previewer.</summary>
    public MainViewModel() : this(
        ShortenerService.CreateDefault(new HttpClient()),
        new AppStorage(Path.Combine(Path.GetTempPath(), "UrlShortener.Design")),
        new NullPlatformServices())
    {
    }

    /// <summary>How long "Copied" and toasts stay up. Tests shorten this.</summary>
    internal TimeSpan FeedbackDuration { get; set; } = TimeSpan.FromSeconds(2.2);

    public IReadOnlyList<IShortenerProvider> Providers { get; }
    public IReadOnlyList<string> Themes { get; } = ["System", "Light", "Dark"];
    public ObservableCollection<HistoryItemViewModel> History { get; } = [];
    public bool CanShare => _platform.CanShare;
    public string Version { get; } =
        typeof(MainViewModel).Assembly.GetName().Version is { } v ? $"{v.Major}.{v.Minor}.{v.Build}" : "?";

    // ---- input ----

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ShortenCommand))]
    [NotifyPropertyChangedFor(nameof(InputHint), nameof(HasInput))]
    public partial string InputUrl { get; set; } = "";

    public bool HasInput => !string.IsNullOrEmpty(InputUrl);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AliasHint))]
    [NotifyCanExecuteChangedFor(nameof(ShortenCommand))]
    public partial bool UseAlias { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AliasHint))]
    [NotifyCanExecuteChangedFor(nameof(ShortenCommand))]
    public partial string Alias { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AliasHint), nameof(AliasSupported), nameof(AliasPrefix))]
    [NotifyCanExecuteChangedFor(nameof(ShortenCommand))]
    public partial IShortenerProvider SelectedProvider { get; set; }

    [ObservableProperty]
    public partial bool AutoFallback { get; set; } = true;

    [ObservableProperty]
    public partial bool CopyAutomatically { get; set; } = true;

    [ObservableProperty]
    public partial string Theme { get; set; } = "System";

    public bool AliasSupported => SelectedProvider.SupportsCustomAlias;
    public string AliasPrefix => SelectedProvider.DisplayName.ToLowerInvariant() + "/";

    /// <summary>Live hint under the URL box; null while the input is empty or fine.</summary>
    public string? InputHint =>
        string.IsNullOrWhiteSpace(InputUrl) ? null
        : UrlValidator.TryNormalize(InputUrl, out _, out var error) ? null
        : error;

    public string? AliasHint =>
        UseAlias && AliasSupported && !string.IsNullOrWhiteSpace(Alias) ? SelectedProvider.ValidateAlias(Alias.Trim()) : null;

    // ---- state / result ----

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ShortenCommand))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? ErrorMessage { get; set; }

    public bool HasError => ErrorMessage is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasResult), nameof(ShortDisplay))]
    [NotifyCanExecuteChangedFor(nameof(CopyResultCommand), nameof(OpenResultCommand), nameof(ShareResultCommand))]
    public partial string? ShortUrl { get; set; }

    public string ShortDisplay => ShortUrl is null ? "" : HistoryItemViewModel.StripScheme(ShortUrl);

    [ObservableProperty]
    public partial string? ResultLongUrl { get; set; }

    [ObservableProperty]
    public partial string? ResultNote { get; set; }

    [ObservableProperty]
    public partial byte[]? QrCodePng { get; set; }

    [ObservableProperty]
    public partial bool ShowQr { get; set; }

    public bool HasResult => ShortUrl is not null;

    /// <summary>Briefly true after the short link is copied; the Copy button turns into "Copied".</summary>
    [ObservableProperty]
    public partial bool JustCopied { get; set; }

    [ObservableProperty]
    public partial string ToastMessage { get; set; } = "";

    [ObservableProperty]
    public partial bool IsToastVisible { get; set; }

    [ObservableProperty]
    public partial string HistoryFilter { get; set; } = "";

    public bool HasHistory => History.Count > 0;
    public bool HistoryIsEmpty => _allHistory.Count == 0;
    public int HistoryCount => _allHistory.Count;
    public bool ShowHistorySearch => _allHistory.Count > 3;
    public bool NoSearchMatches => _allHistory.Count > 0 && History.Count == 0;

    // ---- lifecycle ----

    public async Task InitializeAsync()
    {
        var settings = await _storage.LoadSettingsAsync();
        _loadingSettings = true;
        SelectedProvider = _shortener.Find(settings.PreferredProviderId) ?? Providers[0];
        AutoFallback = settings.AutoFallback;
        CopyAutomatically = settings.CopyAutomatically;
        Theme = Themes.Contains(settings.Theme) ? settings.Theme : "System";
        _loadingSettings = false;
        _platform.SetTheme(Theme);

        var entries = await _storage.LoadHistoryAsync();
        _allHistory.Clear();
        _allHistory.AddRange(entries.OrderByDescending(e => e.CreatedAt).Select(e => new HistoryItemViewModel(e)));
        RefreshHistoryView();
    }

    /// <summary>Text shared into the app from another app (Android "Share → URL Shortener"): fill in and shorten right away.</summary>
    public async Task ReceiveSharedTextAsync(string text)
    {
        InputUrl = UrlValidator.ExtractUrl(text);
        UseAlias = false;
        if (ShortenCommand.CanExecute(null))
            await ShortenCommand.ExecuteAsync(null);
    }

    partial void OnSelectedProviderChanged(IShortenerProvider value) => SaveSettings();
    partial void OnAutoFallbackChanged(bool value) => SaveSettings();
    partial void OnCopyAutomaticallyChanged(bool value) => SaveSettings();
    partial void OnInputUrlChanged(string value) => ErrorMessage = null;
    partial void OnAliasChanged(string value) => ErrorMessage = null;

    partial void OnThemeChanged(string value)
    {
        _platform.SetTheme(value);
        SaveSettings();
    }

    partial void OnHistoryFilterChanged(string value) => RefreshHistoryView();

    private void SaveSettings()
    {
        if (_loadingSettings) return;
        _ = _storage.SaveSettingsAsync(new AppSettings
        {
            PreferredProviderId = SelectedProvider.Id,
            AutoFallback = AutoFallback,
            CopyAutomatically = CopyAutomatically,
            Theme = Theme,
        });
    }

    // ---- commands ----

    private bool CanShorten() => !IsBusy && !string.IsNullOrWhiteSpace(InputUrl) && InputHint is null && AliasHint is null;

    [RelayCommand(CanExecute = nameof(CanShorten), IncludeCancelCommand = true)]
    private async Task ShortenAsync(CancellationToken cancellationToken)
    {
        IsBusy = true;
        ErrorMessage = null;
        ShortUrl = null; // hide the old result so the new one animates in
        ShowQr = false;
        try
        {
            var alias = UseAlias && AliasSupported ? Alias : null;
            var outcome = await _shortener.ShortenAsync(InputUrl, SelectedProvider.Id, alias, AutoFallback, cancellationToken);

            if (!outcome.IsSuccess)
            {
                if (outcome.Final.Failure != FailureKind.Cancelled)
                    ErrorMessage = outcome.Final.Error ?? "Something went wrong.";
                return;
            }

            var shortUrl = outcome.Final.ShortUrl!;
            ResultLongUrl = outcome.LongUrl!.AbsoluteUri;
            ResultNote = outcome.UsedFallback
                ? $"{SelectedProvider.DisplayName} wasn't available — made with {_shortener.Find(outcome.Final.ProviderId)?.DisplayName}."
                : null;
            QrCodePng = QrCodeGenerator.CreatePng(shortUrl);
            ShortUrl = shortUrl;
            InputUrl = ResultLongUrl;

            AddToHistory(new HistoryEntry(ResultLongUrl, shortUrl, outcome.Final.ProviderId, DateTimeOffset.Now));

            if (CopyAutomatically)
            {
                await _platform.CopyTextAsync(shortUrl);
                FlashCopied();
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task PasteAsync()
    {
        var text = await _platform.GetClipboardTextAsync();
        if (!string.IsNullOrWhiteSpace(text))
            InputUrl = UrlValidator.ExtractUrl(text);
        else
            ShowToast("Clipboard is empty");
    }

    [RelayCommand(CanExecute = nameof(HasResult))]
    private async Task CopyResultAsync()
    {
        if (ShortUrl is null) return;
        await _platform.CopyTextAsync(ShortUrl);
        FlashCopied();
    }

    [RelayCommand(CanExecute = nameof(HasResult))]
    private Task OpenResultAsync() => ShortUrl is null ? Task.CompletedTask : OpenAsync(ShortUrl);

    private bool CanShareResult() => HasResult && CanShare;

    [RelayCommand(CanExecute = nameof(CanShareResult))]
    private void ShareResult()
    {
        if (ShortUrl is not null) _platform.Share(ShortUrl);
    }

    [RelayCommand]
    private void ToggleQr() => ShowQr = !ShowQr;

    [RelayCommand]
    private void ToggleAlias() => UseAlias = !UseAlias;

    [RelayCommand]
    private void SelectTheme(string theme) => Theme = theme;

    [RelayCommand]
    private async Task CopyHistoryItemAsync(HistoryItemViewModel? item)
    {
        if (item is null) return;
        await _platform.CopyTextAsync(item.ShortUrl);
        ShowToast($"Copied {item.ShortDisplay}");
    }

    [RelayCommand]
    private Task OpenHistoryItemAsync(HistoryItemViewModel? item) =>
        item is null ? Task.CompletedTask : OpenAsync(item.ShortUrl);

    [RelayCommand]
    private void ShareHistoryItem(HistoryItemViewModel? item)
    {
        if (item is not null && CanShare) _platform.Share(item.ShortUrl);
    }

    [RelayCommand]
    private void RemoveHistoryItem(HistoryItemViewModel? item)
    {
        if (item is null) return;
        _allHistory.Remove(item);
        RefreshHistoryView();
        PersistHistory();
    }

    [RelayCommand]
    private void ClearHistory()
    {
        var count = _allHistory.Count;
        _allHistory.Clear();
        HistoryFilter = "";
        RefreshHistoryView();
        PersistHistory();
        ShowToast(count == 1 ? "Removed 1 link" : $"Removed {count} links");
    }

    [RelayCommand]
    private void ClearInput()
    {
        InputUrl = "";
        Alias = "";
        ErrorMessage = null;
        ShortUrl = null;
        ResultLongUrl = null;
        ResultNote = null;
        QrCodePng = null;
        ShowQr = false;
    }

    // ---- helpers ----

    private async Task OpenAsync(string url)
    {
        if (!await _platform.OpenUrlAsync(url))
            ShowToast("Couldn't open the browser");
    }

    private async void FlashCopied()
    {
        var version = ++_copiedVersion;
        JustCopied = true;
        await Task.Delay(FeedbackDuration);
        if (version == _copiedVersion) JustCopied = false;
    }

    private async void ShowToast(string message)
    {
        var version = ++_toastVersion;
        ToastMessage = message;
        IsToastVisible = true;
        await Task.Delay(FeedbackDuration);
        if (version == _toastVersion) IsToastVisible = false;
    }

    private void AddToHistory(HistoryEntry entry)
    {
        _allHistory.RemoveAll(h => string.Equals(h.ShortUrl, entry.ShortUrl, StringComparison.OrdinalIgnoreCase));
        _allHistory.Insert(0, new HistoryItemViewModel(entry));
        if (_allHistory.Count > AppStorage.MaxHistory)
            _allHistory.RemoveRange(AppStorage.MaxHistory, _allHistory.Count - AppStorage.MaxHistory);
        RefreshHistoryView();
        PersistHistory();
    }

    private void PersistHistory() => _ = _storage.SaveHistoryAsync(_allHistory.Select(h => h.Entry).ToList());

    private void RefreshHistoryView()
    {
        History.Clear();
        foreach (var item in _allHistory.Where(h => h.Matches(HistoryFilter)))
            History.Add(item);
        OnPropertyChanged(nameof(HasHistory));
        OnPropertyChanged(nameof(HistoryIsEmpty));
        OnPropertyChanged(nameof(HistoryCount));
        OnPropertyChanged(nameof(ShowHistorySearch));
        OnPropertyChanged(nameof(NoSearchMatches));
    }

    private sealed class NullPlatformServices : IPlatformServices
    {
        public Task CopyTextAsync(string text) => Task.CompletedTask;
        public Task<string?> GetClipboardTextAsync() => Task.FromResult<string?>(null);
        public Task<bool> OpenUrlAsync(string url) => Task.FromResult(false);
        public void SetTheme(string theme) { }
        public bool CanShare => false;
        public void Share(string text) { }
    }
}
