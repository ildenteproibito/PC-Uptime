using System.ComponentModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Uptime.Core;

namespace Uptime.Presentation.ViewModels;

public partial class OperationsViewModel : ObservableObject
{
    private enum Pending { None, Restart, Shutdown, Uefi }

    private readonly IPowerService _power;
    private readonly IStartupService _startup;
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

    public bool ShowFooterInfo => !IsConfirming;

    public OperationsViewModel(IPowerService power, IStartupService startup)
    {
        _power = power;
        _startup = startup;
        Localizer.Instance.PropertyChanged += OnLanguageChanged;
        _tick.Tick += (_, _) => UpdateTimer();
        ReadPreferences();
        UpdateTimer();
    }

    partial void OnIsConfirmingChanged(bool value) => OnPropertyChanged(nameof(ShowFooterInfo));

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(Localizer.Language)) return;
        OnPropertyChanged(nameof(L));
        UpdateTimer();
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
            if (_timerEnd is not null) _power.CancelShutdownTimer(); // un solo timer alla volta
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
        FastStartupEnabled = desired; // ottimistico; poi riallineato allo stato reale
        var ok = await Task.Run(() => _power.SetFastStartup(desired));
        FastStartupEnabled = _power.IsFastStartupEnabled() ?? false;
        StatusMessage = ok ? "" : L["PowerFailed"];
        IsBusy = false;
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
            AppDiagnostics.Write("Error", "Could not change Fast Startup preference.", e);
            StatusMessage = L["PowerFailed"];
        }
        RunAtStartup = _startup.IsEnabled;
    }
}
