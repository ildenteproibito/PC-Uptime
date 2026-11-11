using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Uptime.Core;

namespace Uptime.Presentation.ViewModels;

public sealed record StartupAppRow(string Name, string Command, string ImpactText, IBrush ImpactBrush, string Location);

public partial class OperationsViewModel : ObservableObject
{
    private enum Pending { None, Restart, Shutdown, Uefi }

    private static readonly IBrush GreenBrush = new SolidColorBrush(Color.Parse("#7BC4A8"));
    private static readonly IBrush AmberBrush = new SolidColorBrush(Color.Parse("#E0A458"));
    private static readonly IBrush OrangeBrush = new SolidColorBrush(Color.Parse("#E8845A"));

    private readonly IPowerService _power;
    private readonly IStartupService _startup;
    private readonly IBatteryService? _battery;
    private readonly ISleepBlockerService? _sleepBlocker;
    private readonly IStartupAppsService? _startupApps;
    private readonly DispatcherTimer _tick = new() { Interval = TimeSpan.FromSeconds(1) };
    private Pending _pending = Pending.None;
    private DateTime? _timerEnd;

    public Localizer L => Localizer.Instance;
    public IReadOnlyList<string> TimerLabels { get; } = new[] { "0", "30 m", "60 m", "90 m", "120 m" };

    [ObservableProperty] private bool _isConfirming;
    [ObservableProperty] private string _confirmText = "";
    [ObservableProperty] private string _statusMessage = "";
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _fastStartupAvailable;
    [ObservableProperty] private bool _fastStartupEnabled;
    [ObservableProperty] private bool _runAtStartup;
    [ObservableProperty] private bool _timerActive;
    [ObservableProperty] private string _timerText = "";
    [ObservableProperty] private int _timerFilledTicks;

    // Battery
    [ObservableProperty] private bool _hasBattery;
    [ObservableProperty] private string _batteryStatusText = "";
    [ObservableProperty] private string _batteryHealthText = "—";
    [ObservableProperty] private string _batteryCyclesText = "—";
    [ObservableProperty] private string _batteryDetailsText = "";

    // Sleep Inspector
    [ObservableProperty] private bool _isSleepLoading;
    [ObservableProperty] private string _sleepStatusText = "";
    [ObservableProperty] private string _lastWakeText = "";
    [ObservableProperty] private string _formattedLastWake = "";
    [ObservableProperty] private bool _showAdminNotice;
    public ObservableCollection<SleepBlockerItem> SleepBlockers { get; } = new();

    // Startup Applications
    public ObservableCollection<StartupAppRow> StartupApps { get; } = new();

    public bool ShowFooterInfo => !IsConfirming;

    public OperationsViewModel(
        IPowerService power,
        IStartupService startup,
        IBatteryService? battery = null,
        ISleepBlockerService? sleepBlocker = null,
        IStartupAppsService? startupApps = null)
    {
        _power = power;
        _startup = startup;
        _battery = battery;
        _sleepBlocker = sleepBlocker;
        _startupApps = startupApps;

        Localizer.Instance.PropertyChanged += OnLanguageChanged;
        _tick.Tick += (_, _) => UpdateTimer();
        ReadPreferences();
        UpdateTimer();

        RefreshBattery();
        LoadStartupApps();
        _ = CheckSleepBlockers();
    }

    partial void OnIsConfirmingChanged(bool value) => OnPropertyChanged(nameof(ShowFooterInfo));

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(Localizer.Language)) return;
        OnPropertyChanged(nameof(L));
        UpdateTimer();
        RefreshBattery();
        LoadStartupApps();
        FormattedLastWake = string.IsNullOrWhiteSpace(LastWakeText) ? "" : L.Format("SleepLastWakeValue", LastWakeText);
        if (!string.IsNullOrWhiteSpace(StatusMessage)) StatusMessage = L["PowerFailed"];
        if (IsConfirming) ConfirmText = ConfirmFor(_pending);
    }

    private void ReadPreferences()
    {
        var fast = _power.IsFastStartupEnabled();
        FastStartupAvailable = fast.HasValue;
        FastStartupEnabled = fast ?? false;
        RunAtStartup = _startup.IsEnabled;
    }

    private string ConfirmFor(Pending p) => p switch
    {
        Pending.Restart => L["ConfirmRestart"],
        Pending.Shutdown => L["ConfirmShutdown"],
        Pending.Uefi => L["ConfirmUefi"],
        _ => "",
    };

    private void Ask(Pending p)
    {
        _pending = p;
        ConfirmText = ConfirmFor(p);
        StatusMessage = "";
        IsConfirming = true;
    }

    [RelayCommand] private void RequestRestart() => Ask(Pending.Restart);
    [RelayCommand] private void RequestShutdown() => Ask(Pending.Shutdown);
    [RelayCommand] private void RequestUefi() => Ask(Pending.Uefi);

    [RelayCommand]
    private void CancelPending()
    {
        _pending = Pending.None;
        IsConfirming = false;
    }

    [RelayCommand]
    private void ConfirmPending()
    {
        var action = _pending;
        CancelPending();
        Run(() =>
        {
            switch (action)
            {
                case Pending.Restart: _power.CleanReboot(); break;
                case Pending.Shutdown: _power.FullShutdown(); break;
                case Pending.Uefi: _power.RebootToUefi(); break;
            }
        });
    }

    [RelayCommand]
    private void StartTimer(string minutes)
    {
        if (!int.TryParse(minutes, out var m) || m <= 0) return;
        Run(() =>
        {
            if (_timerEnd is not null) _power.CancelShutdownTimer(); // single active timer at a time
            _power.StartShutdownTimer(m, L["ShutdownComment"]);
            _timerEnd = DateTime.Now.AddMinutes(m);
        });
        UpdateTimer();
    }

    [RelayCommand]
    private void CancelTimer()
    {
        Run(() => _power.CancelShutdownTimer());
        _timerEnd = null;
        UpdateTimer();
    }

    private void Run(Action action)
    {
        try
        {
            action();
            StatusMessage = "";
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            AppDiagnostics.Write("Error", "Power operation failed.", e);
            StatusMessage = L["PowerFailed"];
        }
    }

    private void UpdateTimer()
    {
        if (_timerEnd is { } end && end > DateTime.Now)
        {
            var remaining = end - DateTime.Now;
            TimerActive = true;
            TimerText = L.Format("TimerActive", end.ToString("HH:mm"), (int)Math.Ceiling(remaining.TotalMinutes));
            TimerFilledTicks = (int)Math.Min(60, remaining.TotalMinutes / 2);
            if (!_tick.IsEnabled) _tick.Start();
        }
        else
        {
            _timerEnd = null;
            TimerActive = false;
            TimerText = L["TimerNone"];
            TimerFilledTicks = 0;
            _tick.Stop();
        }
    }

    public async Task SetFastStartupAsync(bool desired)
    {
        IsBusy = true;
        FastStartupEnabled = desired; // optimistic update; re-aligned to actual state subsequently
        var ok = await Task.Run(() => _power.SetFastStartup(desired));
        FastStartupEnabled = _power.IsFastStartupEnabled() ?? false;
        StatusMessage = ok ? "" : L["PowerFailed"];
        IsBusy = false;
    }

    public void RefreshBattery()
    {
        if (_battery == null) return;
        var info = _battery.GetBatteryStatus();
        HasBattery = info.HasBattery;
        BatteryStatusText = info.PowerStatusText;
        if (info.HasBattery)
        {
            BatteryHealthText = info.HealthPercent.HasValue ? $"{info.HealthPercent.Value}%" : "—";
            BatteryCyclesText = info.CycleCount.HasValue ? info.CycleCount.Value.ToString() : "—";
            if (info.FullChargeCapacityMWh.HasValue && info.DesignCapacityMWh.HasValue && info.HealthPercent.HasValue)
            {
                BatteryDetailsText = L.Format("BatteryWearNotice", info.FullChargeCapacityMWh.Value, info.DesignCapacityMWh.Value, info.HealthPercent.Value);
            }
            else
            {
                BatteryDetailsText = $"{info.Percent}%";
            }
        }
        else
        {
            BatteryHealthText = "—";
            BatteryCyclesText = "—";
            BatteryDetailsText = L["BatteryDesktop"];
        }
    }

    [RelayCommand]
    public async Task CheckSleepBlockers()
    {
        if (_sleepBlocker == null || IsSleepLoading) return;
        IsSleepLoading = true;
        try
        {
            var res = await _sleepBlocker.CheckSleepBlockersAsync();
            SleepBlockers.Clear();
            foreach (var b in res.Blockers)
            {
                SleepBlockers.Add(b);
            }
            ShowAdminNotice = !res.IsElevated;
            LastWakeText = string.IsNullOrWhiteSpace(res.LastWakeReason) ? "—" : res.LastWakeReason;
            FormattedLastWake = string.IsNullOrWhiteSpace(res.LastWakeReason) ? "" : L.Format("SleepLastWakeValue", res.LastWakeReason);
            SleepStatusText = res.HasBlockers ? "" : L["SleepNoBlockers"];
        }
        catch (Exception ex)
        {
            AppDiagnostics.Write("Warning", "Sleep check failed.", ex);
            SleepStatusText = L["SleepNoBlockers"];
        }
        finally
        {
            IsSleepLoading = false;
        }
    }

    public void LoadStartupApps()
    {
        if (_startupApps == null) return;
        StartupApps.Clear();
        foreach (var app in _startupApps.GetStartupApps())
        {
            var (impactText, brush) = app.Impact switch
            {
                StartupImpactLevel.High => (L["StartupImpactHigh"], OrangeBrush),
                StartupImpactLevel.Medium => (L["StartupImpactMed"], AmberBrush),
                _ => (L["StartupImpactLow"], GreenBrush),
            };
            var locationText = L.Format("StartupOriginFormatted", app.Location);
            StartupApps.Add(new StartupAppRow(app.Name, app.Command, impactText, brush, locationText));
        }
    }

    public void SetRunAtStartup(bool desired)
    {
        RunAtStartup = desired;
        try
        {
            _startup.SetEnabled(desired);
            StatusMessage = "";
        }
        catch (Exception e) when (e is UnauthorizedAccessException or InvalidOperationException or System.Security.SecurityException)
        {
            AppDiagnostics.Write("Error", "Could not change Run at startup preference.", e);
            StatusMessage = L["PowerFailed"];
        }
        RunAtStartup = _startup.IsEnabled;
    }
}
