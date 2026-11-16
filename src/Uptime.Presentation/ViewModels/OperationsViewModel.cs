using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Uptime.Core;

namespace Uptime.Presentation.ViewModels;

public sealed record StartupAppRow(string Name, string Command, string ImpactText, IBrush ImpactBrush, string Location);

public partial class OperationsViewModel : ObservableObject, IDisposable
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
    private readonly IKeepAwakeService? _keepAwake;
    private readonly IPowerPlanService? _powerPlan;
    private readonly IWorkspaceRestoreService? _workspaceRestore;
    private readonly IIdleProvider? _idle;
    private readonly SettingsViewModel? _settings;
    private readonly IMemoryService? _memory;
    private readonly DispatcherTimer _tick = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer _autopilotTick = new() { Interval = TimeSpan.FromSeconds(5) };
    private Pending _pending = Pending.None;
    private DateTime? _timerEnd;
    private DateTime? _keepAwakeEnd;
    private PowerPlanKind? _preAutopilotPlan;

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

    // RAM Working-Set Trim
    [ObservableProperty] private string _memorySummaryText = "—";
    [ObservableProperty] private string _memoryPercentText = "—";
    [ObservableProperty] private string _memoryTrimResultText = "";
    [ObservableProperty] private bool _isTrimmingMemory;

    // Smart Caffeine (Keep-Awake)
    [ObservableProperty] private bool _isKeepAwakeManual;
    [ObservableProperty] private bool _isKeepAwakeActuallyActive;
    [ObservableProperty] private string _keepAwakeStatusText = "";

    public string KeepAwakeProcessName
    {
        get => _settings?.KeepAwakeProcessName ?? "";
        set
        {
            if (_settings == null || _settings.KeepAwakeProcessName == value) return;
            _settings.KeepAwakeProcessName = value;
            OnPropertyChanged();
            EvaluateAutomation();
        }
    }

    public bool KeepAwakeBatteryGuard
    {
        get => _settings?.KeepAwakeBatteryGuard ?? true;
        set
        {
            if (_settings == null || _settings.KeepAwakeBatteryGuard == value) return;
            _settings.KeepAwakeBatteryGuard = value;
            OnPropertyChanged();
            EvaluateAutomation();
        }
    }

    // Power Plan Autopilot
    [ObservableProperty] private PowerPlanKind _activePowerPlan;
    [ObservableProperty] private string _activePowerPlanText = "";
    public string ActivePowerPlanName => _powerPlan?.GetActivePlanName() ?? ActivePowerPlan.ToString();
    public bool IsPowerPlanBalanced => ActivePowerPlan == PowerPlanKind.Balanced;
    public bool IsPowerPlanHighPerf => ActivePowerPlan == PowerPlanKind.HighPerformance;
    public bool IsPowerPlanSaver => ActivePowerPlan == PowerPlanKind.PowerSaver;

    partial void OnActivePowerPlanChanged(PowerPlanKind value)
    {
        OnPropertyChanged(nameof(IsPowerPlanBalanced));
        OnPropertyChanged(nameof(IsPowerPlanHighPerf));
        OnPropertyChanged(nameof(IsPowerPlanSaver));
    }

    public bool PowerPlanAutopilot
    {
        get => _settings?.PowerPlanAutopilot ?? false;
        set
        {
            if (_settings == null || _settings.PowerPlanAutopilot == value) return;
            _settings.PowerPlanAutopilot = value;
            OnPropertyChanged();
            if (!value && _preAutopilotPlan is { } prev)
            {
                _powerPlan?.SetActivePlan(prev);
                _preAutopilotPlan = null;
                RefreshPowerPlan();
            }
        }
    }

    public bool RestoreWorkspaceOnReboot
    {
        get => _settings?.RestoreWorkspaceOnReboot ?? false;
        set
        {
            if (_settings == null || _settings.RestoreWorkspaceOnReboot == value) return;
            _settings.RestoreWorkspaceOnReboot = value;
            OnPropertyChanged();
        }
    }

    public bool ShowFooterInfo => !IsConfirming;

    public OperationsViewModel(
        IPowerService power,
        IStartupService startup,
        IBatteryService? battery = null,
        ISleepBlockerService? sleepBlocker = null,
        IStartupAppsService? startupApps = null,
        IKeepAwakeService? keepAwake = null,
        IPowerPlanService? powerPlan = null,
        IWorkspaceRestoreService? workspaceRestore = null,
        IIdleProvider? idle = null,
        SettingsViewModel? settings = null,
        IMemoryService? memory = null)
    {
        _power = power;
        _startup = startup;
        _battery = battery;
        _sleepBlocker = sleepBlocker;
        _startupApps = startupApps;
        _keepAwake = keepAwake;
        _powerPlan = powerPlan;
        _workspaceRestore = workspaceRestore;
        _idle = idle;
        _settings = settings;
        _memory = memory;

        Localizer.Instance.PropertyChanged += OnLanguageChanged;
        _tick.Tick += (_, _) => UpdateTimer();
        _autopilotTick.Tick += (_, _) =>
        {
            EvaluateAutomation();
            RefreshMemory();
        };
        try { ReadPreferences(); } catch (Exception ex) { AppDiagnostics.Write("Warning", "ReadPreferences failed on startup.", ex); }
        UpdateTimer();

        try { RefreshBattery(); } catch (Exception ex) { AppDiagnostics.Write("Warning", "RefreshBattery failed on startup.", ex); }
        try { LoadStartupApps(); } catch (Exception ex) { AppDiagnostics.Write("Warning", "LoadStartupApps failed on startup.", ex); }
        try { RefreshPowerPlan(); } catch (Exception ex) { AppDiagnostics.Write("Warning", "RefreshPowerPlan failed on startup.", ex); }
        try { RefreshMemory(); } catch (Exception ex) { AppDiagnostics.Write("Warning", "RefreshMemory failed on startup.", ex); }
        try { EvaluateAutomation(); } catch (Exception ex) { AppDiagnostics.Write("Warning", "EvaluateAutomation failed on startup.", ex); }
        _autopilotTick.Start();
        _ = CheckSleepBlockers();
    }

    partial void OnIsConfirmingChanged(bool value) => OnPropertyChanged(nameof(ShowFooterInfo));

    partial void OnIsKeepAwakeManualChanged(bool value)
    {
        if (!value) _keepAwakeEnd = null;
        EvaluateAutomation();
    }

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(Localizer.Language)) return;
        OnPropertyChanged(nameof(L));
        UpdateTimer();
        RefreshBattery();
        LoadStartupApps();
        RefreshPowerPlan();
        EvaluateAutomation();
        FormattedLastWake = string.IsNullOrWhiteSpace(LastWakeText) ? "" : L.Format("SleepLastWakeValue", LastWakeText);
        if (!IsSleepLoading && SleepBlockers.Count == 0) SleepStatusText = L["SleepNoBlockers"];
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
                case Pending.Restart:
                    if (RestoreWorkspaceOnReboot) _workspaceRestore?.SaveWorkspaceSnapshot();
                    _power.CleanReboot();
                    break;
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
            if (_timerEnd is not null)
            {
                _power.CancelShutdownTimer(); // single active timer at a time
                _timerEnd = null;
            }
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
        if (IsBusy) return;
        IsBusy = true;
        FastStartupEnabled = desired; // optimistic update; re-aligned to actual state subsequently
        try
        {
            var (ok, actual) = await Task.Run(() =>
            {
                var setOk = _power.SetFastStartup(desired);
                return (setOk, _power.IsFastStartupEnabled() ?? false);
            });
            FastStartupEnabled = actual;
            StatusMessage = ok ? "" : L["PowerFailed"];
        }
        catch (Exception ex)
        {
            AppDiagnostics.Write("Error", "SetFastStartupAsync failed.", ex);
            StatusMessage = L["PowerFailed"];
        }
        finally
        {
            IsBusy = false;
        }
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

    [RelayCommand]
    private void StartKeepAwakeTimer(string minutes)
    {
        if (!int.TryParse(minutes, out var m) || m <= 0)
        {
            _keepAwakeEnd = null;
            IsKeepAwakeManual = true;
        }
        else
        {
            _keepAwakeEnd = DateTime.Now.AddMinutes(m);
            IsKeepAwakeManual = true;
        }
        EvaluateAutomation();
    }

    [RelayCommand]
    private void SetPowerPlan(string planName)
    {
        if (_powerPlan == null) return;
        if (Enum.TryParse<PowerPlanKind>(planName, out var plan))
        {
            _preAutopilotPlan = null;
            _powerPlan.SetActivePlan(plan);
            RefreshPowerPlan();
        }
    }

    private void RefreshPowerPlan()
    {
        if (_powerPlan == null) return;
        var plan = _powerPlan.GetActivePlan();
        ActivePowerPlan = plan;
        var customName = _powerPlan.GetActivePlanName();
        ActivePowerPlanText = plan switch
        {
            PowerPlanKind.Balanced => L["PowerPlanBalanced"],
            PowerPlanKind.HighPerformance => L["PowerPlanHighPerf"],
            PowerPlanKind.PowerSaver => L["PowerPlanSaver"],
            _ => !string.IsNullOrWhiteSpace(customName) && customName != PowerPlanKind.Unknown.ToString()
                ? customName
                : L["PowerPlanUnknown"]
        };
    }

    private bool _evaluatingAutomation;

    private void EvaluateAutomation()
    {
        if (_evaluatingAutomation) return;
        _evaluatingAutomation = true;
        try
        {
            // 1. Smart Caffeine (Keep-Awake)
            if (_keepAwake != null)
        {
            if (_keepAwakeEnd is { } end && DateTime.Now >= end)
            {
                _keepAwakeEnd = null;
                IsKeepAwakeManual = false;
            }

            var processActive = !string.IsNullOrWhiteSpace(KeepAwakeProcessName) &&
                                _keepAwake.IsProcessRunning(KeepAwakeProcessName);
            var desired = IsKeepAwakeManual || processActive;

            var pausedByBattery = false;
            if (desired && KeepAwakeBatteryGuard && _battery != null)
            {
                var bat = _battery.GetBatteryStatus();
                if (bat.HasBattery && !bat.IsCharging && bat.Percent is > 0 and < 20)
                {
                    pausedByBattery = true;
                    desired = false;
                }
            }

            _keepAwake.SetAwake(desired, keepDisplayOn: true);
            IsKeepAwakeActuallyActive = _keepAwake.IsActive;

            if (pausedByBattery)
            {
                KeepAwakeStatusText = L["KeepAwakePausedBattery"];
            }
            else if (!desired)
            {
                KeepAwakeStatusText = L["KeepAwakeOff"];
            }
            else if (_keepAwakeEnd is { } activeEnd)
            {
                var rem = Math.Max(1, (int)Math.Ceiling((activeEnd - DateTime.Now).TotalMinutes));
                KeepAwakeStatusText = L.Format("KeepAwakeActiveFor", rem);
            }
            else if (processActive && !IsKeepAwakeManual)
            {
                KeepAwakeStatusText = L.Format("KeepAwakeActiveApp", KeepAwakeProcessName.Trim());
            }
            else
            {
                KeepAwakeStatusText = L["KeepAwakeActive"];
            }
        }

        // 2. Power Plan Autopilot
        if (_powerPlan != null)
        {
            if (PowerPlanAutopilot && _idle != null)
            {
                var idle = _idle.GetIdleTime();
                if (idle >= TimeSpan.FromMinutes(5))
                {
                    var current = _powerPlan.GetActivePlan();
                    if (current != PowerPlanKind.PowerSaver && current != PowerPlanKind.Unknown)
                    {
                        _preAutopilotPlan = current;
                        _powerPlan.SetActivePlan(PowerPlanKind.PowerSaver);
                    }
                }
                else if (idle < TimeSpan.FromSeconds(15) && _preAutopilotPlan is { } prev)
                {
                    _powerPlan.SetActivePlan(prev);
                    _preAutopilotPlan = null;
                }
            }
            RefreshPowerPlan();
        }
        }
        finally
        {
            _evaluatingAutomation = false;
        }
    }

    private void RefreshMemory()
    {
        if (_memory is null) return;
        try
        {
            var info = _memory.GetStatus();
            MemoryPercentText = $"{info.UsedPercent}%";
            MemorySummaryText = $"{info.UsedGb:F1} / {info.TotalGb:F1} GB";
        }
        catch { }
    }

    public Action<long>? OnMemoryTrimmed { get; set; }

    [RelayCommand]
    private async Task TrimMemoryAsync()
    {
        if (_memory is null || IsTrimmingMemory) return;
        IsTrimmingMemory = true;
        MemoryTrimResultText = L["RamTrimming"];
        try
        {
            var res = await Task.Run(() => _memory.TrimBackgroundWorkingSets());
            MemoryPercentText = $"{res.After.UsedPercent}%";
            MemorySummaryText = $"{res.After.UsedGb:F1} / {res.After.TotalGb:F1} GB";
            MemoryTrimResultText = L.Format("RamTrimResult", res.FreedMb, res.TrimmedProcessCount);
            OnMemoryTrimmed?.Invoke(res.FreedMb);
        }
        catch (Exception ex)
        {
            AppDiagnostics.Write("Warning", "Failed to trim RAM working sets.", ex);
            MemoryTrimResultText = "";
        }
        finally
        {
            IsTrimmingMemory = false;
        }
    }

    public void Dispose()
    {
        _tick.Stop();
        _autopilotTick.Stop();
        Localizer.Instance.PropertyChanged -= OnLanguageChanged;
    }
}
