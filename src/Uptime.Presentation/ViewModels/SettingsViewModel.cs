using System.ComponentModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Uptime.Core;

namespace Uptime.Presentation.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private static readonly string FilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Uptime", "settings.json");
    public Localizer L => Localizer.Instance;
    public bool IsItalian => L.Language == "it";
    public bool IsEnglish => L.Language == "en";
    public double LanguageIndicatorOffset => IsEnglish ? 100 : 0;
    [ObservableProperty] private bool _showSession = true;
    [ObservableProperty] private bool _showAdvice = true;
    [ObservableProperty] private bool _showSystem = true;
    [ObservableProperty] private bool _nightReminder;
    [ObservableProperty] private string _selectedPreset = "Default";
    [ObservableProperty] private string _diagnosticsStatus = "";

    public SettingsViewModel()
    {
        L.PropertyChanged += OnLocalizerChanged;
        try
        {
            var saved = JsonSerializer.Deserialize<SettingsData>(File.ReadAllText(FilePath));
            if (saved is null) return;
            ShowSession = saved.ShowSession; ShowAdvice = saved.ShowAdvice; ShowSystem = saved.ShowSystem;
            NightReminder = saved.NightReminder; SelectedPreset = saved.Preset;
            if (!string.IsNullOrWhiteSpace(saved.Language)) L.Language = saved.Language;
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException) { }
    }

    private void OnLocalizerChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(Localizer.Language)) return;
        OnPropertyChanged(nameof(IsItalian));
        OnPropertyChanged(nameof(IsEnglish));
        OnPropertyChanged(nameof(LanguageIndicatorOffset));
        OnPropertyChanged(nameof(L));
        Save();
    }

    partial void OnShowSessionChanged(bool value) => Save();
    partial void OnShowAdviceChanged(bool value) => Save();
    partial void OnShowSystemChanged(bool value) => Save();
    partial void OnNightReminderChanged(bool value) => Save();
    partial void OnSelectedPresetChanged(string value) => Save();

    [RelayCommand]
    private void SetLanguage(string code)
    {
        if (code is "it" or "en") L.Language = code;
    }

    [RelayCommand] private void ApplyPreset(string name)
    {
        SelectedPreset = name;
        (ShowSession, ShowAdvice, ShowSystem) = name switch
        {
            "Compact" => (true, false, false),
            "Focus" => (false, true, false),
            _ => (true, true, true),
        };
        Save();
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            var tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(new SettingsData(ShowSession, ShowAdvice, ShowSystem, NightReminder, SelectedPreset, L.Language)));
            File.Move(tmp, FilePath, true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }
    private sealed record SettingsData(bool ShowSession, bool ShowAdvice, bool ShowSystem, bool NightReminder, string Preset, string? Language = null);
}
