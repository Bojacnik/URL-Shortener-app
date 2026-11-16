using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UrlShortener.Core;
using UrlShortener.Core.Providers;
using UrlShortener.UI.Localization;
using UrlShortener.UI.Services;

namespace UrlShortener.UI.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly AppStorage _storage;
    private readonly List<HistoryItemViewModel> _allHistory = [];
    private bool _loadingSettings;
    private int _toastVersion;
    private int _copiedVersion;
    private LocalizedText? _error;
    private LocalizedText? _resultNote;

    public MainViewModel(ShortenerService shortener, AppStorage storage, IPlatformServices platform, LinkExpander? expander = null)
    {
        Shortener = shortener;
        _storage = storage;
        Platform = platform;
        Providers = shortener.Providers;

        ModeChoices = [new(AppMode.Shorten, "Mode.Shorten"), new(AppMode.Batch, "Mode.Batch"), new(AppMode.Expand, "Mode.Expand")];
        ThemeChoices = [new("System", "Theme.System"), new("Light", "Theme.Light"), new("Dark", "Theme.Dark")];
        LanguageChoices =
        [
            new(Loc.SystemCode, "Settings.SystemLanguage"),
            .. Loc.Languages.Select(l => new Choice<string>(l.Code, "", l.NativeName)),
        ];

        Batch = new BatchViewModel(this);
        Expand = new ExpandViewModel(this, expander ?? new LinkExpander(LinkExpander.CreateHttpClient()));

        _loadingSettings = true;
        SelectedProvider = Providers[0];
        _loadingSettings = false;

        // Weak, so a view model that's no longer used can be collected even though Loc lives forever.
        var weak = new WeakReference<MainViewModel>(this);
        EventHandler? handler = null;
        handler = (_, _) =>
        {
            if (weak.TryGetTarget(out var vm)) vm.OnLanguageChanged();
            else Loc.Instance.LanguageChanged -= handler;
        };
        Loc.Instance.LanguageChanged += handler;
    }

    /// <summary>Design-time constructor for the XAML previewer.</summary>
    public MainViewModel() : this(
        ShortenerService.CreateDefault(new HttpClient()),
        new AppStorage(Path.Combine(Path.GetTempPath(), "UrlShortener.Design")),
        new NullPlatformServices())
    {
    }

    internal ShortenerService Shortener { get; }
    internal IPlatformServices Platform { get; }

    /// <summary>How long "Copied" and toasts stay up. Tests shorten this.</summary>
    internal TimeSpan FeedbackDuration { get; set; } = TimeSpan.FromSeconds(2.2);

    public BatchViewModel Batch { get; }
    public ExpandViewModel Expand { get; }

    public IReadOnlyList<IShortenerProvider> Providers { get; }
    public IReadOnlyList<Choice<AppMode>> ModeChoices { get; }
    public IReadOnlyList<Choice<string>> ThemeChoices { get; }
    public IReadOnlyList<Choice<string>> LanguageChoices { get; }
    public ObservableCollection<HistoryItemViewModel> History { get; } = [];

    public bool CanShare => Platform.CanShare;
    public bool IsDesktop => !OperatingSystem.IsAndroid() && !OperatingSystem.IsIOS();
    public FlowDirection FlowDirection => Loc.Instance.IsRightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

    public string Version { get; } =
        typeof(MainViewModel).Assembly.GetName().Version is { } v ? $"{v.Major}.{v.Minor}.{v.Build}" : "?";

    public string AboutText => Loc.Instance.Format("Settings.About", Version);

    // ---- mode ----

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsShortenMode), nameof(IsBatchMode), nameof(IsExpandMode), nameof(SelectedModeChoice))]
    public partial AppMode Mode { get; set; }

    public bool IsShortenMode => Mode == AppMode.Shorten;
    public bool IsBatchMode => Mode == AppMode.Batch;
    public bool IsExpandMode => Mode == AppMode.Expand;

    public Choice<AppMode> SelectedModeChoice
    {
        get => ModeChoices.First(c => c.Value == Mode);
        set { if (value is not null) Mode = value.Value; }
    }

    // ---- input ----

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ShortenCommand))]
    [NotifyPropertyChangedFor(nameof(InputHint), nameof(HasInput), nameof(InputLooksShort))]
    public partial string InputUrl { get; set; } = "";

    public bool HasInput => !string.IsNullOrEmpty(InputUrl);

    /// <summary>The input is already a short link (bit.ly, t.co …), so offer to expand it instead.</summary>
    public bool InputLooksShort => InputHint is null && ShortLinks.IsKnownShortLink(InputUrl) && !HasResult;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AliasHint))]
    [NotifyCanExecuteChangedFor(nameof(ShortenCommand))]
    public partial bool UseAlias { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AliasHint))]
    [NotifyCanExecuteChangedFor(nameof(ShortenCommand))]
    public partial string Alias { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AliasHint), nameof(AliasSupported), nameof(AliasPrefix), nameof(SelectedProviderDescription))]
    [NotifyCanExecuteChangedFor(nameof(ShortenCommand))]
    public partial IShortenerProvider SelectedProvider { get; set; }

    public string SelectedProviderDescription => Loc.Instance[$"Service.{SelectedProvider.Id}"];

    [ObservableProperty]
    public partial bool AutoFallback { get; set; } = true;

    [ObservableProperty]
    public partial bool CopyAutomatically { get; set; } = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedThemeChoice))]
    public partial string Theme { get; set; } = "System";

    public Choice<string> SelectedThemeChoice
    {
        get => ThemeChoices.FirstOrDefault(c => c.Value == Theme) ?? ThemeChoices[0];
        set { if (value is not null) Theme = value.Value; }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedLanguageChoice))]
    public partial string Language { get; set; } = Loc.SystemCode;

    public Choice<string> SelectedLanguageChoice
    {
        get => LanguageChoices.FirstOrDefault(c => c.Value == Language) ?? LanguageChoices[0];
        set { if (value is not null) Language = value.Value; }
    }

    public bool AliasSupported => SelectedProvider.SupportsCustomAlias;
    public string AliasPrefix => SelectedProvider.DisplayName.ToLowerInvariant() + "/";

    /// <summary>Live hint under the URL box; null while the input is empty or fine.</summary>
    public string? InputHint =>
        string.IsNullOrWhiteSpace(InputUrl) ? null
        : UrlValidator.TryNormalize(InputUrl, out _, out UrlProblem problem) ? null
        : Messages.Url(problem).ToString();

    public string? AliasHint =>
        UseAlias && AliasSupported && !string.IsNullOrWhiteSpace(Alias) && SelectedProvider.CheckAlias(Alias.Trim()) is { } problem
            ? Messages.Alias(SelectedProvider, problem).ToString()
            : null;

    // ---- state / result ----

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ShortenCommand))]
    public partial bool IsBusy { get; set; }

    public string? ErrorMessage => _error?.ToString();
    public bool HasError => _error is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasResult), nameof(ShortDisplay), nameof(InputLooksShort))]
    [NotifyCanExecuteChangedFor(nameof(CopyResultCommand), nameof(OpenResultCommand), nameof(ShareResultCommand))]
    public partial string? ShortUrl { get; set; }

    public string ShortDisplay => ShortUrl is null ? "" : HistoryItemViewModel.StripScheme(ShortUrl);

    [ObservableProperty]
    public partial string? ResultLongUrl { get; set; }

    public string? ResultNote => _resultNote?.ToString();

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
        SelectedProvider = Shortener.Find(settings.PreferredProviderId) ?? Providers[0];
        AutoFallback = settings.AutoFallback;
        CopyAutomatically = settings.CopyAutomatically;
        Theme = ThemeChoices.Any(c => c.Value == settings.Theme) ? settings.Theme : "System";
        Language = LanguageChoices.Any(c => c.Value == settings.Language) ? settings.Language : Loc.SystemCode;
        _loadingSettings = false;
        Platform.SetTheme(Theme);
        Loc.Instance.SetLanguage(Language);

        var entries = await _storage.LoadHistoryAsync();
        _allHistory.Clear();
        _allHistory.AddRange(entries.OrderByDescending(e => e.CreatedAt).Select(e => new HistoryItemViewModel(e)));
        RefreshHistoryView();
    }

    /// <summary>
    /// Text shared into the app from another app (Android "Share → URL Shortener").
    /// A short link gets expanded; anything else gets shortened right away.
    /// </summary>
    public async Task ReceiveSharedTextAsync(string text)
    {
        var url = UrlValidator.ExtractUrl(text);
        if (ShortLinks.IsKnownShortLink(url))
        {
            Mode = AppMode.Expand;
            Expand.Input = url;
            if (Expand.ExpandCommand.CanExecute(null))
                await Expand.ExpandCommand.ExecuteAsync(null);
            return;
        }

        Mode = AppMode.Shorten;
        InputUrl = url;
        UseAlias = false;
        if (ShortenCommand.CanExecute(null))
            await ShortenCommand.ExecuteAsync(null);
    }

    partial void OnSelectedProviderChanged(IShortenerProvider value) => SaveSettings();
    partial void OnAutoFallbackChanged(bool value) => SaveSettings();
    partial void OnCopyAutomaticallyChanged(bool value) => SaveSettings();
    partial void OnInputUrlChanged(string value) => SetError(null);
    partial void OnAliasChanged(string value) => SetError(null);

    partial void OnThemeChanged(string value)
    {
        Platform.SetTheme(value);
        SaveSettings();
    }

    partial void OnLanguageChanged(string value)
    {
        if (_loadingSettings) return;
        Loc.Instance.SetLanguage(value);
        SaveSettings();
    }

    partial void OnHistoryFilterChanged(string value) => RefreshHistoryView();

    private void OnLanguageChanged()
    {
        foreach (var c in ModeChoices) c.Refresh();
        foreach (var c in ThemeChoices) c.Refresh();
        LanguageChoices[0].Refresh();
        foreach (var item in _allHistory) item.Refresh();
        Batch.RefreshText();
        Expand.RefreshText();
        // Everything computed from translated strings (hints, errors, about text, direction …).
        OnPropertyChanged(string.Empty);
    }

    private void SaveSettings()
    {
        if (_loadingSettings) return;
        _ = _storage.SaveSettingsAsync(new AppSettings
        {
            PreferredProviderId = SelectedProvider.Id,
            AutoFallback = AutoFallback,
            CopyAutomatically = CopyAutomatically,
            Theme = Theme,
            Language = Language,
        });
    }

    // ---- commands ----

    private bool CanShorten() => !IsBusy && !string.IsNullOrWhiteSpace(InputUrl) && InputHint is null && AliasHint is null;

    [RelayCommand(CanExecute = nameof(CanShorten), IncludeCancelCommand = true)]
    private async Task ShortenAsync(CancellationToken cancellationToken)
    {
        IsBusy = true;
        SetError(null);
        ShortUrl = null; // hide the old result so the new one animates in
        ShowQr = false;
        try
        {
            var alias = UseAlias && AliasSupported ? Alias : null;
            var outcome = await Shortener.ShortenAsync(InputUrl, SelectedProvider.Id, alias, AutoFallback, cancellationToken);

            if (!outcome.IsSuccess)
            {
                if (outcome.Final.Failure != FailureKind.Cancelled)
                    SetError(Messages.Failure(outcome.Final, Shortener));
                return;
            }

            var shortUrl = outcome.Final.ShortUrl!;
            ResultLongUrl = outcome.LongUrl!.AbsoluteUri;
            _resultNote = outcome.UsedFallback
                ? new LocalizedText("Result.Fallback", SelectedProvider.DisplayName, Shortener.Find(outcome.Final.ProviderId)?.DisplayName)
                : null;
            OnPropertyChanged(nameof(ResultNote));
            QrCodePng = QrCodeGenerator.CreatePng(shortUrl);
            ShortUrl = shortUrl;
            InputUrl = ResultLongUrl;

            AddToHistory(new HistoryEntry(ResultLongUrl, shortUrl, outcome.Final.ProviderId, DateTimeOffset.Now));

            if (CopyAutomatically)
            {
                await Platform.CopyTextAsync(shortUrl);
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
        var text = await Platform.GetClipboardTextAsync();
        if (!string.IsNullOrWhiteSpace(text))
            InputUrl = UrlValidator.ExtractUrl(text);
        else
            ShowToast(Loc.Instance["Toast.ClipboardEmpty"]);
    }

    /// <summary>The input is a short link: switch to Expand and follow it.</summary>
    [RelayCommand]
    private async Task ExpandInputAsync()
    {
        Mode = AppMode.Expand;
        Expand.Input = InputUrl;
        if (Expand.ExpandCommand.CanExecute(null))
            await Expand.ExpandCommand.ExecuteAsync(null);
    }

    [RelayCommand(CanExecute = nameof(HasResult))]
    private async Task CopyResultAsync()
    {
        if (ShortUrl is null) return;
        await Platform.CopyTextAsync(ShortUrl);
        FlashCopied();
    }

    [RelayCommand(CanExecute = nameof(HasResult))]
    private Task OpenResultAsync() => ShortUrl is null ? Task.CompletedTask : OpenUrlAsync(ShortUrl);

    private bool CanShareResult() => HasResult && CanShare;

    [RelayCommand(CanExecute = nameof(CanShareResult))]
    private void ShareResult()
    {
        if (ShortUrl is not null) Platform.Share(ShortUrl);
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
        await Platform.CopyTextAsync(item.ShortUrl);
        ShowToast(Loc.Instance.Format("Toast.Copied", item.ShortDisplay));
    }

    [RelayCommand]
    private Task OpenHistoryItemAsync(HistoryItemViewModel? item) =>
        item is null ? Task.CompletedTask : OpenUrlAsync(item.ShortUrl);

    [RelayCommand]
    private void ShareHistoryItem(HistoryItemViewModel? item)
    {
        if (item is not null && CanShare) Platform.Share(item.ShortUrl);
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
        ShowToast(Loc.Instance.Format("Toast.Removed", count));
    }

    [RelayCommand]
    private void ClearInput()
    {
        InputUrl = "";
        Alias = "";
        SetError(null);
        ShortUrl = null;
        ResultLongUrl = null;
        _resultNote = null;
        OnPropertyChanged(nameof(ResultNote));
        QrCodePng = null;
        ShowQr = false;
    }

    // ---- shared with the Batch and Expand view models ----

    internal async Task OpenUrlAsync(string url)
    {
        if (!await Platform.OpenUrlAsync(url))
            ShowToast(Loc.Instance["Toast.CantOpen"]);
    }

    internal async void ShowToast(string message)
    {
        var version = ++_toastVersion;
        ToastMessage = message;
        IsToastVisible = true;
        await Task.Delay(FeedbackDuration);
        if (version == _toastVersion) IsToastVisible = false;
    }

    internal void AddToHistory(HistoryEntry entry)
    {
        _allHistory.RemoveAll(h => string.Equals(h.ShortUrl, entry.ShortUrl, StringComparison.OrdinalIgnoreCase));
        _allHistory.Insert(0, new HistoryItemViewModel(entry));
        if (_allHistory.Count > AppStorage.MaxHistory)
            _allHistory.RemoveRange(AppStorage.MaxHistory, _allHistory.Count - AppStorage.MaxHistory);
        RefreshHistoryView();
        PersistHistory();
    }

    /// <summary>"Shorten this" from the Expand view: take its destination over to the Shorten view.</summary>
    internal void StartShortening(string url)
    {
        Mode = AppMode.Shorten;
        ClearInput();
        InputUrl = url;
    }

    // ---- helpers ----

    private void SetError(LocalizedText? error)
    {
        _error = error;
        OnPropertyChanged(nameof(ErrorMessage));
        OnPropertyChanged(nameof(HasError));
    }

    private async void FlashCopied()
    {
        var version = ++_copiedVersion;
        JustCopied = true;
        await Task.Delay(FeedbackDuration);
        if (version == _copiedVersion) JustCopied = false;
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

    internal sealed class NullPlatformServices : IPlatformServices
    {
        public Task CopyTextAsync(string text) => Task.CompletedTask;
        public Task<string?> GetClipboardTextAsync() => Task.FromResult<string?>(null);
        public Task<bool> OpenUrlAsync(string url) => Task.FromResult(false);
        public void SetTheme(string theme) { }
        public bool CanShare => false;
        public void Share(string text) { }
    }
}
