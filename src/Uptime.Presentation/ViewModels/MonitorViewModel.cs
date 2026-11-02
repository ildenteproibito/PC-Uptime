using System.ComponentModel;
using System.Runtime.InteropServices;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Uptime.Core;

namespace Uptime.Presentation.ViewModels;

public partial class MonitorViewModel : ObservableObject, IDisposable
{
    private readonly IUptimeProvider _uptime;
    private readonly IIdleProvider _idle;
    private readonly SettingsViewModel _settings;
    private readonly DispatcherTimer _timer;
    private static readonly string RecordPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Uptime", "personal-record.txt");
    private double _personalBestSeconds;

    private static readonly IBrush Good = new SolidColorBrush(Color.Parse("#7BC4A8"));
    private static readonly IBrush Amber = new SolidColorBrush(Color.Parse("#E0A458"));
    private static readonly IBrush Orange = new SolidColorBrush(Color.Parse("#E8845A"));

    public Localizer L => Localizer.Instance;
    public string OsText { get; } = RuntimeInformation.OSDescription;
    public string ThreadsText { get; } = Environment.ProcessorCount.ToString();

    [ObservableProperty] private string _primaryText = "";
    [ObservableProperty] private string _secondaryText = "";
    [ObservableProperty] private string _bootText = "";
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private IBrush _statusBrush = Good;
    [ObservableProperty] private string _adviceText = "";
    [ObservableProperty] private string _idleText = "";
    [ObservableProperty] private string _startedText = "";
    [ObservableProperty] private int _filledTicks;
    [ObservableProperty] private IReadOnlyList<string> _rulerLabels = Array.Empty<string>();
    [ObservableProperty] private string _clipboardText = "";
    [ObservableProperty] private string _personalRecordText = "";
    [ObservableProperty] private string _hudThresholdText = "";

    public bool ShowSession => _settings.ShowSession;
    public bool ShowAdvice => _settings.ShowAdvice;
    public bool ShowSystem => _settings.ShowSystem;

    public MonitorViewModel(IUptimeProvider uptime, IIdleProvider idle, SettingsViewModel settings)
    {
        _uptime = uptime;
        _idle = idle;
        _settings = settings;
        try { _personalBestSeconds = double.Parse(File.ReadAllText(RecordPath), System.Globalization.CultureInfo.InvariantCulture); } catch (Exception e) when (e is IOException or FormatException or UnauthorizedAccessException) { }
        _settings.PropertyChanged += (_, _) => { OnPropertyChanged(nameof(ShowSession)); OnPropertyChanged(nameof(ShowAdvice)); OnPropertyChanged(nameof(ShowSystem)); };
        Localizer.Instance.PropertyChanged += OnLanguageChanged;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => Refresh();
        Refresh();
        _timer.Start();
    }

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(Localizer.Language)) return;
        OnPropertyChanged(nameof(L));
        Refresh();
    }

    private void Refresh()
    {
        var l = Localizer.Instance;
        var up = _uptime.GetUptime();
        if (up.TotalSeconds > _personalBestSeconds)
        {
            _personalBestSeconds = up.TotalSeconds;
            try { Directory.CreateDirectory(Path.GetDirectoryName(RecordPath)!); File.WriteAllText(RecordPath, _personalBestSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
        var p = UptimeParts.From(up);
        var boot = DateTime.Now - up;

        if (p.Days > 0)
        {
            PrimaryText = l.Plural(p.Days, "UnitDay");
            SecondaryText = $"{p.Hours:00} h  {p.Minutes:00} m  {p.Seconds:00} s";
        }
        else if (p.Hours > 0)
        {
            PrimaryText = $"{p.Hours} h";
            SecondaryText = $"{p.Minutes:00} m  {p.Seconds:00} s";
        }
        else
        {
            PrimaryText = $"{p.Minutes} m";
            SecondaryText = $"{p.Seconds:00} s";
        }

        var bootFormatted = boot.ToString("d MMM, HH:mm", l.Culture);
        BootText = l.Format("BootOn", bootFormatted);
        StartedText = bootFormatted;
        var best = TimeSpan.FromSeconds(_personalBestSeconds);
        PersonalRecordText = l.Format("PersonalRecord", (int)best.TotalDays, best.Hours);
        HudThresholdText = l.Format("RulerDay", 7);

        var level = Health.For(up);
        StatusText = l["Health" + level];
        StatusBrush = level switch { HealthLevel.Good => Good, HealthLevel.Attention => Amber, _ => Orange };
        AdviceText = level switch
        {
            HealthLevel.Good => l["AdviceGood"],
            HealthLevel.Attention => l.Format("AdviceAttention", Health.DaysUntilMaintenance(up)),
            _ => l["AdviceMaintenance"],
        };

        FilledTicks = (int)Math.Min(168, up.TotalHours);
        var labels = new List<string> { l["RulerStart"] };
        for (var d = 1; d <= 7; d++) labels.Add(l.Format("RulerDay", d));
        RulerLabels = labels;

        var idle = _idle.GetIdleTime();
        IdleText = $"{(int)idle.TotalMinutes} m {idle.Seconds:00} s";
        ClipboardText = l.Format("ClipboardText", $"{PrimaryText} {SecondaryText}", bootFormatted);
    }

    public void Dispose()
    {
        _timer.Stop();
        Localizer.Instance.PropertyChanged -= OnLanguageChanged;
    }
}
