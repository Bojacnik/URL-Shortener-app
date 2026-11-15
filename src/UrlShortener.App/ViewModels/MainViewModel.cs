using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UrlShortener.App.Services;
using UrlShortener.Core;
using UrlShortener.Core.Providers;

namespace UrlShortener.App.ViewModels;

public enum StatusKind { None, Info, Success, Error }

public partial class MainViewModel : ObservableObject
{
    private readonly ShortenerService _shortener;
    private readonly AppStorage _storage;
    private readonly IPlatformServices _platform;
    private readonly List<HistoryItemViewModel> _allHistory = [];
    private bool _loadingSettings;

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

    public IReadOnlyList<IShortenerProvider> Providers { get; }
    public IReadOnlyList<string> Themes { get; } = ["System", "Light", "Dark"];
    public ObservableCollection<HistoryItemViewModel> History { get; } = [];

    // ---- input ----

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ShortenCommand))]
    [NotifyPropertyChangedFor(nameof(InputHint))]
    public partial string InputUrl { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AliasHint))]
    [NotifyCanExecuteChangedFor(nameof(ShortenCommand))]
    public partial bool UseAlias { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AliasHint))]
    [NotifyCanExecuteChangedFor(nameof(ShortenCommand))]
    public partial string Alias { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AliasHint), nameof(AliasSupported))]
    [NotifyCanExecuteChangedFor(nameof(ShortenCommand))]
    public partial IShortenerProvider SelectedProvider { get; set; }

    [ObservableProperty]
    public partial bool AutoFallback { get; set; } = true;

    [ObservableProperty]
    public partial bool CopyAutomatically { get; set; } = true;

    [ObservableProperty]
    public partial string Theme { get; set; } = "System";

    public bool AliasSupported => SelectedProvider.SupportsCustomAlias;

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
    [NotifyPropertyChangedFor(nameof(HasStatus), nameof(IsError), nameof(IsSuccess), nameof(IsInfo))]
    public partial StatusKind Status { get; set; }

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = "";

    public bool HasStatus => Status != StatusKind.None;
    public bool IsError => Status == StatusKind.Error;
    public bool IsSuccess => Status == StatusKind.Success;
    public bool IsInfo => Status == StatusKind.Info;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasResult))]
    [NotifyCanExecuteChangedFor(nameof(CopyResultCommand), nameof(OpenResultCommand))]
    public partial string? ShortUrl { get; set; }

    [ObservableProperty]
    public partial string? ResultLongUrl { get; set; }

    [ObservableProperty]
    public partial byte[]? QrCodePng { get; set; }

    public bool HasResult => ShortUrl is not null;

    [ObservableProperty]
    public partial string HistoryFilter { get; set; } = "";

    public bool HasHistory => History.Count > 0;
    public bool HistoryIsEmpty => _allHistory.Count == 0;

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

    partial void OnSelectedProviderChanged(IShortenerProvider value) => SaveSettings();
    partial void OnAutoFallbackChanged(bool value) => SaveSettings();
    partial void OnCopyAutomaticallyChanged(bool value) => SaveSettings();

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
        SetStatus(StatusKind.Info, $"Shortening with {SelectedProvider.DisplayName}…");
        try
        {
            var alias = UseAlias && AliasSupported ? Alias : null;
            var outcome = await _shortener.ShortenAsync(InputUrl, SelectedProvider.Id, alias, AutoFallback, cancellationToken);

            if (!outcome.IsSuccess)
            {
                SetStatus(outcome.Final.Failure == FailureKind.Cancelled ? StatusKind.None : StatusKind.Error,
                    outcome.Final.Error ?? "Something went wrong.");
                return;
            }

            var shortUrl = outcome.Final.ShortUrl!;
            ShortUrl = shortUrl;
            ResultLongUrl = outcome.LongUrl!.AbsoluteUri;
            QrCodePng = QrCodeGenerator.CreatePng(shortUrl);
            InputUrl = ResultLongUrl;

            var message = outcome.UsedFallback
                ? $"{SelectedProvider.DisplayName} wasn't available, so {_shortener.Find(outcome.Final.ProviderId)?.DisplayName} was used."
                : "Done!";
            if (CopyAutomatically)
            {
                await _platform.CopyTextAsync(shortUrl);
                message += " Copied to clipboard.";
            }
            SetStatus(StatusKind.Success, message);

            AddToHistory(new HistoryEntry(ResultLongUrl, shortUrl, outcome.Final.ProviderId, DateTimeOffset.Now));
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
            InputUrl = text.Trim();
    }

    [RelayCommand(CanExecute = nameof(HasResult))]
    private async Task CopyResultAsync()
    {
        if (ShortUrl is null) return;
        await _platform.CopyTextAsync(ShortUrl);
        SetStatus(StatusKind.Success, "Copied to clipboard.");
    }

    [RelayCommand(CanExecute = nameof(HasResult))]
    private Task OpenResultAsync() => ShortUrl is null ? Task.CompletedTask : OpenAsync(ShortUrl);

    [RelayCommand]
    private async Task CopyHistoryItemAsync(HistoryItemViewModel? item)
    {
        if (item is null) return;
        await _platform.CopyTextAsync(item.ShortUrl);
        SetStatus(StatusKind.Success, $"Copied {item.ShortUrl}");
    }

    [RelayCommand]
    private Task OpenHistoryItemAsync(HistoryItemViewModel? item) =>
        item is null ? Task.CompletedTask : OpenAsync(item.ShortUrl);

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
        _allHistory.Clear();
        RefreshHistoryView();
        PersistHistory();
    }

    [RelayCommand]
    private void ClearInput()
    {
        InputUrl = "";
        Alias = "";
        ShortUrl = null;
        ResultLongUrl = null;
        QrCodePng = null;
        SetStatus(StatusKind.None, "");
    }

    // ---- helpers ----

    private async Task OpenAsync(string url)
    {
        if (!await _platform.OpenUrlAsync(url))
            SetStatus(StatusKind.Error, "Couldn't open the browser.");
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
    }

    private void SetStatus(StatusKind kind, string message)
    {
        StatusMessage = message;
        Status = kind;
    }

    private sealed class NullPlatformServices : IPlatformServices
    {
        public Task CopyTextAsync(string text) => Task.CompletedTask;
        public Task<string?> GetClipboardTextAsync() => Task.FromResult<string?>(null);
        public Task<bool> OpenUrlAsync(string url) => Task.FromResult(false);
        public void SetTheme(string theme) { }
    }
}
