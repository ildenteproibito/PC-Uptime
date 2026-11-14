using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Uptime.Core;
using Uptime.Presentation.Services;

namespace Uptime.Presentation.ViewModels;

public sealed record AiChatMessage(bool IsUser, string Text, string Timestamp)
{
    public bool IsAssistant => !IsUser;
}

public partial class AssistantViewModel : ObservableObject
{
    private readonly IUptimeProvider _uptime;
    private readonly IIdleProvider _idle;
    private readonly IBatteryService? _battery;
    private readonly OperationsViewModel _operations;

    public Localizer L => Localizer.Instance;
    public ObservableCollection<AiChatMessage> Messages { get; } = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendMessageCommand))]
    private string _inputText = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendMessageCommand))]
    private bool _isBusy;

    [ObservableProperty] private bool _includeSystemContext = true;
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private bool _hasError;

    public AssistantViewModel(
        IUptimeProvider uptime,
        IIdleProvider idle,
        IBatteryService? battery,
        OperationsViewModel operations)
    {
        _uptime = uptime;
        _idle = idle;
        _battery = battery;
        _operations = operations;

        Localizer.Instance.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Localizer.Language))
            {
                OnPropertyChanged(nameof(L));
            }
        };

        AddWelcomeMessage();
    }

    private void AddWelcomeMessage()
    {
        Messages.Add(new AiChatMessage(false, L["AssistantWelcome"], DateTime.Now.ToString("HH:mm")));
    }

    private bool CanSendMessage() => !IsBusy && !string.IsNullOrWhiteSpace(InputText);

    [RelayCommand(CanExecute = nameof(CanSendMessage))]
    private async Task SendMessageAsync()
    {
        var prompt = InputText.Trim();
        if (string.IsNullOrWhiteSpace(prompt) || IsBusy) return;

        InputText = "";
        await SendPromptInternalAsync(prompt);
    }

    [RelayCommand]
    private async Task UsePromptChipAsync(string resourceKey)
    {
        if (IsBusy) return;
        var prompt = L[resourceKey];
        if (string.IsNullOrWhiteSpace(prompt)) return;
        await SendPromptInternalAsync(prompt);
    }

    [RelayCommand]
    private void ClearConversation()
    {
        if (IsBusy) return;
        Messages.Clear();
        StatusText = "";
        HasError = false;
        AddWelcomeMessage();
    }

    private async Task SendPromptInternalAsync(string prompt)
    {
        HasError = false;
        StatusText = L["AssistantThinking"];
        IsBusy = true;

        var history = Messages
            .Skip(1)
            .TakeLast(8)
            .Select(m => new AiChatTurn(m.IsUser ? "user" : "assistant", m.Text))
            .ToList();

        Messages.Add(new AiChatMessage(true, prompt, DateTime.Now.ToString("HH:mm")));

        try
        {
            var context = IncludeSystemContext ? BuildContextSnapshot() : null;
            var result = await UptimeAiService.AskAsync(prompt, history, L.Language, context);

            if (result.Success)
            {
                Messages.Add(new AiChatMessage(false, result.ReplyOrError, DateTime.Now.ToString("HH:mm")));
                StatusText = "";
            }
            else
            {
                HasError = true;
                StatusText = result.StatusCode switch
                {
                    503 => L["AssistantErrorNotConfigured"],
                    429 => L["AssistantErrorRateLimited"],
                    _ => L.Format("AssistantErrorGeneric", result.ReplyOrError)
                };
            }
        }
        catch (Exception ex)
        {
            HasError = true;
            StatusText = L.Format("AssistantErrorGeneric", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private AiSystemContext BuildContextSnapshot()
    {
        var up = _uptime.GetUptime();
        var idle = _idle.GetIdleTime();
        var bat = _battery?.GetBatteryStatus();
        var uptimeFormatted = up.TotalDays >= 1
            ? $"{(int)up.TotalDays}d {up.Hours}h {up.Minutes:00}m"
            : up.TotalHours >= 1
                ? $"{up.Hours}h {up.Minutes:00}m {up.Seconds:00}s"
                : $"{up.Minutes}m {up.Seconds:00}s";

        return new AiSystemContext(
            AppUpdateService.CurrentVersion,
            uptimeFormatted,
            Health.For(up).ToString(),
            RuntimeInformation.OSDescription,
            Environment.ProcessorCount,
            (long)idle.TotalSeconds,
            _operations.ActivePowerPlanName,
            _operations.IsKeepAwakeActuallyActive,
            bat?.HasBattery ?? false,
            bat?.Percent ?? 0,
            bat?.IsCharging ?? false);
    }
}
