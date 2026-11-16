using System.Collections.ObjectModel;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UrlShortener.Core;
using UrlShortener.UI.Localization;

namespace UrlShortener.UI.ViewModels;

public enum BatchStatus { Pending, Working, Done, Failed }

public sealed partial class BatchItemViewModel(string input) : ObservableObject
{
    public string Input { get; } = input;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPending), nameof(IsWorking), nameof(IsDone), nameof(IsFailed))]
    public partial BatchStatus Status { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShortDisplay))]
    public partial string? ShortUrl { get; set; }

    public string? LongUrl { get; set; }
    public string ShortDisplay => ShortUrl is null ? "" : HistoryItemViewModel.StripScheme(ShortUrl);

    internal LocalizedText? Error { get; set; }
    public string? ErrorText => Error?.ToString();

    public bool IsPending => Status == BatchStatus.Pending;
    public bool IsWorking => Status == BatchStatus.Working;
    public bool IsDone => Status == BatchStatus.Done;
    public bool IsFailed => Status == BatchStatus.Failed;

    public void Fail(LocalizedText error)
    {
        Error = error;
        Status = BatchStatus.Failed;
        OnPropertyChanged(nameof(ErrorText));
    }

    public void Refresh() => OnPropertyChanged(nameof(ErrorText));
}

/// <summary>Shortens many links in one go, one request at a time so the services don't throttle us.</summary>
public sealed partial class BatchViewModel(MainViewModel owner) : ObservableObject
{
    public const int MaxLinks = 50;

    /// <summary>Pause between requests, to stay well inside the services' rate limits. Tests set 0.</summary>
    internal TimeSpan Spacing { get; set; } = TimeSpan.FromMilliseconds(400);

    public ObservableCollection<BatchItemViewModel> Results { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LinkCount), nameof(CountText), nameof(OverLimit))]
    [NotifyCanExecuteChangedFor(nameof(ShortenAllCommand))]
    public partial string Input { get; set; } = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ShortenAllCommand), nameof(CopyAllCommand), nameof(CopyTableCommand))]
    [NotifyPropertyChangedFor(nameof(ProgressText))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressText), nameof(ProgressPercent))]
    public partial int Completed { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressText), nameof(ProgressPercent))]
    public partial int Total { get; set; }

    public int LinkCount => Lines().Count;
    public bool OverLimit => LinkCount > MaxLinks;
    public string CountText => Loc.Instance.Format("Batch.Count", LinkCount);
    public string LimitText => Loc.Instance.Format("Batch.Limit", MaxLinks);
    public string ProgressText => Loc.Instance.Format("Batch.Progress", Math.Min(Completed + 1, Total), Total);
    public double ProgressPercent => Total == 0 ? 0 : 100.0 * Completed / Total;

    public int SucceededCount => Results.Count(r => r.IsDone);
    public int FailedCount => Results.Count(r => r.IsFailed);
    public bool HasResults => Results.Count > 0;
    public bool HasSucceeded => SucceededCount > 0;
    public string SummaryText => Loc.Instance.Format("Batch.Summary", SucceededCount, FailedCount);

    /// <summary>Non-empty lines; each may be plain text with a link in it. Duplicates are dropped.</summary>
    internal List<string> Lines() => Input
        .Split('\n')
        .Select(l => UrlValidator.ExtractUrl(l))
        .Where(l => l.Length > 0)
        .Distinct(StringComparer.Ordinal)
        .ToList();

    private bool CanShortenAll() => !IsBusy && LinkCount > 0;

    [RelayCommand(CanExecute = nameof(CanShortenAll), IncludeCancelCommand = true)]
    private async Task ShortenAllAsync(CancellationToken cancellationToken)
    {
        var lines = Lines().Take(MaxLinks).ToList();
        Results.Clear();
        foreach (var line in lines) Results.Add(new BatchItemViewModel(line));
        Total = Results.Count;
        Completed = 0;
        IsBusy = true;
        RaiseSummary();

        try
        {
            var provider = owner.SelectedProvider.Id;
            var first = true;
            foreach (var item in Results)
            {
                if (cancellationToken.IsCancellationRequested) break;
                item.Status = BatchStatus.Working;

                if (!UrlValidator.TryNormalize(item.Input, out var url, out UrlProblem problem))
                {
                    item.Fail(Messages.Url(problem));
                }
                else
                {
                    if (!first && Spacing > TimeSpan.Zero)
                        await Task.Delay(Spacing, cancellationToken).ContinueWith(_ => { }, TaskScheduler.Default);
                    first = false;
                    if (cancellationToken.IsCancellationRequested) { item.Status = BatchStatus.Pending; break; }

                    var outcome = await owner.Shortener.ShortenAsync(url.AbsoluteUri, provider, null, owner.AutoFallback, cancellationToken);
                    if (outcome.IsSuccess)
                    {
                        item.LongUrl = url.AbsoluteUri;
                        item.ShortUrl = outcome.Final.ShortUrl;
                        item.Status = BatchStatus.Done;
                        owner.AddToHistory(new HistoryEntry(url.AbsoluteUri, outcome.Final.ShortUrl!, outcome.Final.ProviderId, DateTimeOffset.Now));
                    }
                    else if (outcome.Final.Failure == FailureKind.Cancelled)
                    {
                        item.Status = BatchStatus.Pending;
                        break;
                    }
                    else
                    {
                        item.Fail(Messages.Failure(outcome.Final, owner.Shortener));
                    }
                }

                Completed++;
                RaiseSummary();
            }
        }
        finally
        {
            IsBusy = false;
            RaiseSummary();
        }
    }

    private bool CanCopy() => !IsBusy && HasSucceeded;

    [RelayCommand(CanExecute = nameof(CanCopy))]
    private async Task CopyAllAsync()
    {
        var shorts = Results.Where(r => r.IsDone).Select(r => r.ShortUrl!).ToList();
        await owner.Platform.CopyTextAsync(string.Join(Environment.NewLine, shorts));
        owner.ShowToast(Loc.Instance.Format("Toast.CopiedCount", shorts.Count));
    }

    /// <summary>Tab-separated "long ⇥ short" lines: pastes as two columns in Excel, LibreOffice or Google Sheets.</summary>
    [RelayCommand(CanExecute = nameof(CanCopy))]
    private async Task CopyTableAsync()
    {
        var sb = new StringBuilder();
        var count = 0;
        foreach (var r in Results.Where(r => r.IsDone))
        {
            sb.Append(r.LongUrl).Append('\t').Append(r.ShortUrl).Append(Environment.NewLine);
            count++;
        }
        await owner.Platform.CopyTextAsync(sb.ToString().TrimEnd());
        owner.ShowToast(Loc.Instance.Format("Toast.CopiedCount", count));
    }

    [RelayCommand]
    private Task CopyItemAsync(BatchItemViewModel? item) =>
        item?.ShortUrl is { } s ? CopyOneAsync(item, s) : Task.CompletedTask;

    private async Task CopyOneAsync(BatchItemViewModel item, string shortUrl)
    {
        await owner.Platform.CopyTextAsync(shortUrl);
        owner.ShowToast(Loc.Instance.Format("Toast.Copied", item.ShortDisplay));
    }

    [RelayCommand]
    private void Clear()
    {
        Input = "";
        Results.Clear();
        Total = 0;
        Completed = 0;
        RaiseSummary();
    }

    internal void RefreshText()
    {
        foreach (var r in Results) r.Refresh();
        OnPropertyChanged(string.Empty);
    }

    private void RaiseSummary()
    {
        OnPropertyChanged(nameof(SucceededCount));
        OnPropertyChanged(nameof(FailedCount));
        OnPropertyChanged(nameof(HasResults));
        OnPropertyChanged(nameof(HasSucceeded));
        OnPropertyChanged(nameof(SummaryText));
        CopyAllCommand.NotifyCanExecuteChanged();
        CopyTableCommand.NotifyCanExecuteChanged();
    }
}
