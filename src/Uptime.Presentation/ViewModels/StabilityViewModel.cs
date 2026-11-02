using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text;
using System.Text.Json;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Uptime.Core;
using Uptime.Presentation.Controls;

namespace Uptime.Presentation.ViewModels;

public sealed record SessionRow(string DateText, string OutcomeText, IBrush OutcomeBrush, string DurationText);

public partial class StabilityViewModel : ObservableObject
{
    private static readonly IBrush Good = new SolidColorBrush(Color.Parse("#7BC4A8"));
    private static readonly IBrush Amber = new SolidColorBrush(Color.Parse("#E0A458"));
    private static readonly IBrush Orange = new SolidColorBrush(Color.Parse("#E8845A"));
    private static readonly IBrush Grey = new SolidColorBrush(Color.Parse("#6B717B"));

    private readonly IEventLogService _log;
    private StabilityReport? _report;
    private DateTime _loadedAt = DateTime.MinValue;

    public Localizer L => Localizer.Instance;
    public ObservableCollection<SessionRow> Sessions { get; } = new();
    public ObservableCollection<string> Incidents { get; } = new();

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _hasError;
    [ObservableProperty] private bool _hasNoIncidents;
    [ObservableProperty] private string _scoreText = "—";
    [ObservableProperty] private string _confidenceText = "";
    [ObservableProperty] private IBrush _confidenceBrush = Grey;
    [ObservableProperty] private string _summaryText = "";
    [ObservableProperty] private string _longestText = "";
    [ObservableProperty] private IReadOnlyList<TimelineSegment> _segments = Array.Empty<TimelineSegment>();

    public StabilityViewModel(IEventLogService log)
    {
        _log = log;
        Localizer.Instance.PropertyChanged += OnLanguageChanged;
    }

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(Localizer.Language)) return;
        OnPropertyChanged(nameof(L));
        if (_report is not null) Render(DateTime.Now);
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
            _report = StabilityAnalyzer.Analyze(events, now);
            _loadedAt = now;
            Render(now);
        }
        catch (Exception ex)
        {
            AppDiagnostics.Write("Error", "Could not load Windows stability event history.", ex);
            HasError = true; // registro eventi illeggibile: stato di errore visibile, nessun dato inventato
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
            Incidents.Add(l.Format("IncidentLine", inc.Time.ToString("d MMM, HH:mm", culture),
                string.Join(", ", inc.EventIds), conf));
        }
        HasNoIncidents = Incidents.Count == 0;

        var windowStart = now.AddDays(-30);
        var window = TimeSpan.FromDays(30).TotalSeconds;
        var segs = new List<TimelineSegment>();
        foreach (var session in r.Sessions)
        {
            var from = session.Start < windowStart ? windowStart : session.Start;
            var to = session.End ?? now;
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

    public string ExportJson() => JsonSerializer.Serialize(_report, new JsonSerializerOptions { WriteIndented = true });
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
}
