using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text;
using System.Text.Json;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Uptime.Core;
using Uptime.Presentation.Controls;

namespace Uptime.Presentation.ViewModels;

public sealed record SessionRow(string DateText, string OutcomeText, IBrush OutcomeBrush, string DurationText);

public sealed record IncidentRow(
    string DateText,
    string EventSummary,
    string ConfidenceText,
    bool HasExplanation,
    string BugcheckName,
    string BugcheckHex,
    string Meaning,
    string Advice);

public sealed record SystemEventLogRow(
    string DateText,
    string EventIdText,
    string LevelText,
    IBrush LevelBrush,
    string Provider,
    string Message,
    int EventId);

public sealed record AppCrashRow(string DateText, string AppName, string KindText, IBrush KindBrush, string ModuleText);
public sealed record NetworkDropRow(string TimeText, string Target, string Reason);

public sealed partial class CommunityFixCard : ObservableObject
{
    public string Id { get; }
    public int? EventId { get; }
    public string EventIdBadge => EventId.HasValue ? $"ID {EventId.Value}" : "APP";
    public string Provider { get; }
    public string Title { get; }
    public string Steps { get; }
    public string Author { get; }
    public Bitmap? RoleIcon { get; }
    public bool HasRoleIcon => RoleIcon is not null;

    [ObservableProperty] private int _upvotes;
    [ObservableProperty] private bool _isVoted;

    public CommunityFixCard(CommunityFixItem item)
    {
        Id = item.Id;
        EventId = item.EventId;
        Provider = item.Provider;
        Title = item.Title;
        Steps = item.Steps;
        Author = item.Author;
        _upvotes = item.Upvotes;
        RoleIcon = LoadRoleIcon(item.AuthorRole);
    }

    private static Bitmap? LoadRoleIcon(string? role)
    {
        var uri = role?.Trim().ToLowerInvariant() switch
        {
            "owner" => "avares://Uptime/Assets/owner-icon.png",
            "moderator" or "developer" => "avares://Uptime/Assets/mod-icon.png",
            _ => null
        };
        if (uri is null) return null;
        try
        {
            using var s = AssetLoader.Open(new Uri(uri));
            return new Bitmap(s);
        }
        catch { return null; }
    }
}

public partial class StabilityViewModel : ObservableObject
{
    private static readonly IBrush Good = new SolidColorBrush(Color.Parse("#7BC4A8"));
    private static readonly IBrush Amber = new SolidColorBrush(Color.Parse("#E0A458"));
    private static readonly IBrush Orange = new SolidColorBrush(Color.Parse("#E8845A"));
    private static readonly IBrush Grey = new SolidColorBrush(Color.Parse("#6B717B"));

    private readonly IEventLogService _log;
    private readonly IAppCrashService? _appCrashes;
    private readonly ISystemIncidentLogService? _systemIncidentLogs;
    private readonly INetworkWatchdogService? _networkWatchdog;
    private readonly ICommunityFixesService? _communityFixes;
    private readonly DispatcherTimer _networkTimer = new() { Interval = TimeSpan.FromSeconds(10) };
    private StabilityReport? _report;
    private IReadOnlyList<AppCrashItem> _rawAppCrashes = Array.Empty<AppCrashItem>();
    private IReadOnlyList<SystemEventLogItem> _rawSystemLogs = Array.Empty<SystemEventLogItem>();
    private DateTime _loadedAt = DateTime.MinValue;
    private bool _isProbingNetwork;

    public Localizer L => Localizer.Instance;
    public ObservableCollection<SessionRow> Sessions { get; } = new();
    public ObservableCollection<IncidentRow> Incidents { get; } = new();
    public ObservableCollection<SystemEventLogRow> SystemEventLogs { get; } = new();
    public ObservableCollection<AppCrashRow> AppCrashes { get; } = new();
    public ObservableCollection<NetworkDropRow> NetworkDrops { get; } = new();
    public ObservableCollection<CommunityFixCard> CommunityFixes { get; } = new();

    public Action? RequestScrollToFixes { get; set; }

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _hasError;
    [ObservableProperty] private bool _hasNoIncidents;
    [ObservableProperty] private bool _hasNoAppCrashes = true;
    [ObservableProperty] private bool _hasNoNetworkDrops = true;
    [ObservableProperty] private string _fixesSearchQuery = "";
    [ObservableProperty] private bool _isFixesLoading;
    [ObservableProperty] private bool _hasNoFixes;
    [ObservableProperty] private bool _isFixSubmitOpen;
    [ObservableProperty] private bool _isFixSubmitting;
    [ObservableProperty] private string _newFixEventId = "";
    [ObservableProperty] private string _newFixProvider = "";
    [ObservableProperty] private string _newFixTitle = "";
    [ObservableProperty] private string _newFixSteps = "";
    [ObservableProperty] private string _newFixAuthor = "";
    [ObservableProperty] private string _fixesStatusText = "";
    [ObservableProperty] private string _networkStatusText = "";
    [ObservableProperty] private IBrush _networkStatusBrush = Grey;
    [ObservableProperty] private string _networkGatewayText = "";
    [ObservableProperty] private string _networkDropsCountText = "";
    [ObservableProperty] private string _scoreText = "—";
    [ObservableProperty] private string _confidenceText = "";
    [ObservableProperty] private IBrush _confidenceBrush = Grey;
    [ObservableProperty] private string _summaryText = "";
    [ObservableProperty] private string _longestText = "";
    [ObservableProperty] private IReadOnlyList<TimelineSegment> _segments = Array.Empty<TimelineSegment>();

    public StabilityViewModel(
        IEventLogService log,
        IAppCrashService? appCrashes = null,
        ISystemIncidentLogService? systemIncidentLogs = null,
        INetworkWatchdogService? networkWatchdog = null,
        ICommunityFixesService? communityFixes = null)
    {
        _log = log;
        _appCrashes = appCrashes;
        _systemIncidentLogs = systemIncidentLogs ?? log as ISystemIncidentLogService;
        _networkWatchdog = networkWatchdog;
        _communityFixes = communityFixes;
        Localizer.Instance.PropertyChanged += OnLanguageChanged;
        if (_networkWatchdog is not null)
        {
            _networkTimer.Tick += async (_, _) => await ProbeNetworkAsync();
            _networkTimer.Start();
            _ = ProbeNetworkAsync();
        }
        if (_communityFixes is not null)
        {
            _ = SearchFixesAsync();
        }
    }

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(Localizer.Language)) return;
        OnPropertyChanged(nameof(L));
        if (_report is not null) Render(DateTime.Now);
        if (_networkWatchdog is not null) RenderNetworkSnapshot(_networkWatchdog.GetSnapshot());
    }

    [RelayCommand]
    private Task ProbeNetworkNowAsync() => ProbeNetworkAsync();

    private async Task ProbeNetworkAsync()
    {
        if (_networkWatchdog is null || _isProbingNetwork) return;
        _isProbingNetwork = true;
        try
        {
            var snap = await _networkWatchdog.ProbeAsync();
            RenderNetworkSnapshot(snap);
        }
        catch { }
        finally
        {
            _isProbingNetwork = false;
        }
    }

    private void RenderNetworkSnapshot(NetworkWatchdogSnapshot snap)
    {
        var l = Localizer.Instance;
        if (snap.CheckCount == 0)
        {
            NetworkStatusText = l["NetChecking"];
            NetworkStatusBrush = Grey;
        }
        else if (snap.IsOnline)
        {
            NetworkStatusText = snap.LastLatencyMs is { } ms ? $"{l["NetOnline"]} · {ms} ms" : l["NetOnline"];
            NetworkStatusBrush = Good;
        }
        else
        {
            NetworkStatusText = l["NetDrop"];
            NetworkStatusBrush = Orange;
        }

        NetworkGatewayText = l.Format("NetGateway", snap.Target);
        NetworkDropsCountText = l.Format("NetDropsCount", snap.DropCount);

        NetworkDrops.Clear();
        foreach (var d in snap.RecentDrops)
            NetworkDrops.Add(new NetworkDropRow(d.Time.ToString("HH:mm:ss"), d.Target, d.Reason));
        HasNoNetworkDrops = NetworkDrops.Count == 0;
    }

    [RelayCommand]
    public Task ReloadAsync() => LoadAsync(force: true);

    public async Task LoadAsync(bool force = false)
    {
        if (IsLoading) return;
        if (!force && _report is not null && DateTime.Now - _loadedAt < TimeSpan.FromMinutes(5)) return;

        IsLoading = true;
        HasError = false;
        try
        {
            var now = DateTime.Now;
            var events = await Task.Run(() => _log.ReadEvents(now.AddDays(-30)));
            _rawSystemLogs = _systemIncidentLogs is not null
                ? await Task.Run(() => _systemIncidentLogs.ReadRecentCriticalAndErrorEvents(now.AddDays(-30), 25))
                : Array.Empty<SystemEventLogItem>();
            _rawAppCrashes = _appCrashes is not null
                ? await Task.Run(() => _appCrashes.ReadRecentAppCrashes(now.AddDays(-7), 12))
                : Array.Empty<AppCrashItem>();
            _report = StabilityAnalyzer.Analyze(events, now);
            _loadedAt = now;
            Render(now);
        }
        catch (Exception ex)
        {
            AppDiagnostics.Write("Error", "Could not load Windows stability event history.", ex);
            HasError = true; // unreadable event log: visible error state, no fabricated data
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void Render(DateTime now)
    {
        if (_report is not { } r) return;
        var l = Localizer.Instance;
        var culture = l.Culture;

        ScoreText = r.ScorePercent is { } s ? $"{Math.Round(s):0}%" : "—";
        (ConfidenceText, ConfidenceBrush) = r.Confidence switch
        {
            DataConfidence.Verified => (l["ConfVerified"], Good),
            DataConfidence.LimitedSample => (l["ConfLimited"], Amber),
            _ => (l["ConfInsufficient"], Grey),
        };
        var known = r.CleanCount + r.UnexpectedCount;
        SummaryText = known > 0 ? l.Format("StabSummary", r.CleanCount, known) : l["StabSummaryNone"];
        LongestText = FormatDuration(r.Longest);

        Sessions.Clear();
        foreach (var s2 in r.Sessions.OrderByDescending(x => x.Start).Take(8))
        {
            var (text, brush) = s2.Outcome switch
            {
                SessionOutcome.Clean => (l["OutcomeClean"], Good),
                SessionOutcome.Unexpected => (l["OutcomeUnexpected"], Amber),
                SessionOutcome.InProgress => (l["OutcomeRunning"], Good),
                _ => (l["OutcomeUnknown"], Grey),
            };
            Sessions.Add(new SessionRow(s2.Start.ToString("d MMM, HH:mm", culture), text, brush,
                FormatDuration(s2.DurationAt(now))));
        }

        Incidents.Clear();
        foreach (var inc in r.Incidents.OrderByDescending(i => i.Time).Take(5))
        {
            var conf = inc.Confidence == IncidentConfidence.High ? l["ConfHigh"] : l["ConfMedium"];
            var dateStr = inc.Time.ToString("d MMM, HH:mm", culture);
            var evSummary = l.Format("IncidentLine", dateStr, string.Join(", ", inc.EventIds), conf);
            var exp = inc.BugcheckCode.HasValue
                ? BsodExplainer.Explain(inc.BugcheckCode.Value, l.Language)
                : inc.Explanation != null
                    ? BsodExplainer.Explain(inc.Explanation.Code, l.Language)
                    : null;
            var hasExp = exp != null;
            var name = exp?.Name ?? "";
            var hex = exp?.HexCode ?? "";
            var meaning = exp?.Meaning ?? "";
            var advice = exp?.Advice ?? "";

            Incidents.Add(new IncidentRow(dateStr, evSummary, conf, hasExp, name, hex, meaning, advice));
        }

        SystemEventLogs.Clear();
        foreach (var ev in _rawSystemLogs)
        {
            var isCrit = EventLogLocalizer.IsCriticalLevel(ev.Level);
            var brush = isCrit ? Orange : Amber;
            var levelText = EventLogLocalizer.LocalizeLevel(ev.Level, l.Language);
            var msg = EventLogLocalizer.LocalizeMessage(ev.EventId, ev.Provider, ev.Message, l.Language);
            SystemEventLogs.Add(new SystemEventLogRow(
                ev.Time.ToString("d MMM, HH:mm", culture),
                $"ID {ev.EventId}",
                levelText,
                brush,
                ev.Provider,
                msg,
                ev.EventId));
        }
        HasNoIncidents = SystemEventLogs.Count == 0;

        AppCrashes.Clear();
        foreach (var ac in _rawAppCrashes)
        {
            var isHang = ac.Kind == "Freeze";
            var kindText = isHang ? l["AppCrashTypeHang"] : l["AppCrashTypeCrash"];
            var brush = isHang ? Amber : Orange;
            var modText = string.IsNullOrWhiteSpace(ac.ModuleName) ? "" : l.Format("AppCrashModule", ac.ModuleName);
            AppCrashes.Add(new AppCrashRow(ac.Time.ToString("d MMM, HH:mm", culture), ac.AppName, kindText, brush, modText));
        }
        HasNoAppCrashes = AppCrashes.Count == 0;

        var windowStart = now.AddDays(-30);
        var window = TimeSpan.FromDays(30).TotalSeconds;
        var segs = new List<TimelineSegment>();
        foreach (var session in r.Sessions)
        {
            var from = session.Start < windowStart ? windowStart : session.Start;
            var to = session.End ?? now;
            if (to > now) to = now;
            if (to <= from) continue;
            IBrush brush = session.Outcome switch
            {
                SessionOutcome.Clean => Good,
                SessionOutcome.Unexpected => Orange,
                SessionOutcome.InProgress => Brushes.White,
                _ => Grey,
            };
            segs.Add(new TimelineSegment((from - windowStart).TotalSeconds / window, (to - from).TotalSeconds / window, brush));
        }
        Segments = segs;
        LeftLabelText = windowStart.ToString("d MMM", culture);
        RightLabelText = l["TimelineToday"];
    }

    [ObservableProperty] private string _leftLabelText = "";
    [ObservableProperty] private string _rightLabelText = "";

    public string ExportJson() => _report is null ? "{}" : JsonSerializer.Serialize(_report, new JsonSerializerOptions { WriteIndented = true });
    public string ExportCsv()
    {
        var l = Localizer.Instance;
        var sb = new StringBuilder($"{l["CsvStart"]},{l["CsvOutcome"]},{l["CsvDuration"]}\r\n");
        if (_report is not null)
            foreach (var session in _report.Sessions.OrderByDescending(x => x.Start))
                sb.Append('"').Append(session.Start.ToString("O")).Append("\",\"").Append(session.Outcome)
                    .Append("\",\"").Append(session.DurationAt(DateTime.Now).ToString()).Append("\"\r\n");
        return sb.ToString();
    }

    private static string FormatDuration(TimeSpan d)
    {
        var l = Localizer.Instance;
        if (d.TotalDays >= 1) return l.Format("DurationDH", (int)d.TotalDays, d.Hours);
        if (d.TotalHours >= 1) return l.Format("DurationHM", (int)d.TotalHours, d.Minutes);
        return l.Format("DurationM", Math.Max(0, (int)d.TotalMinutes));
    }

    [RelayCommand]
    private async Task SearchFixesAsync()
    {
        if (_communityFixes is null) return;
        IsFixesLoading = true;
        HasNoFixes = false;
        FixesStatusText = "";
        try
        {
            var raw = (FixesSearchQuery ?? "").Trim();
            int? eventId = int.TryParse(raw, out var parsed) ? parsed : null;
            var items = await _communityFixes.FetchFixesAsync(eventId: eventId, query: eventId.HasValue ? null : raw);
            CommunityFixes.Clear();
            foreach (var item in items) CommunityFixes.Add(new CommunityFixCard(item));
            HasNoFixes = CommunityFixes.Count == 0;
        }
        finally
        {
            IsFixesLoading = false;
        }
    }

    [RelayCommand]
    private async Task OpenFixForIncidentAsync(SystemEventLogRow? row)
    {
        if (row is null) return;
        FixesSearchQuery = row.EventId.ToString();
        NewFixEventId = row.EventId.ToString();
        NewFixProvider = row.Provider;
        RequestScrollToFixes?.Invoke();
        await SearchFixesAsync();
    }

    [RelayCommand]
    private async Task OpenFixForCrashAsync(AppCrashRow? row)
    {
        if (row is null) return;
        FixesSearchQuery = row.AppName;
        NewFixEventId = "";
        NewFixProvider = row.AppName;
        RequestScrollToFixes?.Invoke();
        await SearchFixesAsync();
    }

    [RelayCommand]
    private void ToggleFixSubmit()
    {
        IsFixSubmitOpen = !IsFixSubmitOpen;
        if (IsFixSubmitOpen) FixesStatusText = "";
    }

    [RelayCommand]
    private async Task SubmitFixAsync()
    {
        if (_communityFixes is null || IsFixSubmitting) return;
        var provider = (NewFixProvider ?? "").Trim();
        var title = (NewFixTitle ?? "").Trim();
        var steps = (NewFixSteps ?? "").Trim();
        if (provider.Length < 2 || title.Length < 5 || steps.Length < 12)
        {
            FixesStatusText = Localizer.Instance["FixValidation"];
            return;
        }

        int? eventId = int.TryParse((NewFixEventId ?? "").Trim(), out var parsed) ? parsed : null;
        IsFixSubmitting = true;
        FixesStatusText = "";
        try
        {
            var res = await _communityFixes.SubmitFixAsync(eventId, provider, title, steps, NewFixAuthor);
            if (!res.Ok)
            {
                FixesStatusText = res.Message;
                return;
            }

            FixesStatusText = Localizer.Instance["FixPublished"];
            NewFixTitle = "";
            NewFixSteps = "";
            IsFixSubmitOpen = false;
            if (res.CreatedFix is not null)
            {
                CommunityFixes.Insert(0, new CommunityFixCard(res.CreatedFix));
                HasNoFixes = false;
            }
            else
            {
                await SearchFixesAsync();
            }
        }
        finally
        {
            IsFixSubmitting = false;
        }
    }

    [RelayCommand]
    private async Task UpvoteFixAsync(CommunityFixCard? card)
    {
        if (_communityFixes is null || card is null || card.IsVoted) return;
        card.IsVoted = true;
        card.Upvotes += 1;
        var res = await _communityFixes.UpvoteFixAsync(card.Id);
        if (res.Ok && res.Upvotes.HasValue)
        {
            card.Upvotes = res.Upvotes.Value;
        }
        else if (res.AlreadyVoted)
        {
            card.Upvotes = Math.Max(0, card.Upvotes - 1);
        }
    }

    [RelayCommand]
    private async Task ReportFixAsync(CommunityFixCard? card)
    {
        if (_communityFixes is null || card is null) return;
        var res = await _communityFixes.ReportFixAsync(card.Id, $"Reported from Stability ({card.Provider})");
        FixesStatusText = res.Ok ? Localizer.Instance["FixReported"] : res.Message;
    }
}
