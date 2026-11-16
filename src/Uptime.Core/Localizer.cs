using System.ComponentModel;
using System.Globalization;

namespace Uptime.Core;

/// <summary>Runtime localization and language switching. Keys starting with "Lbl*" are rendered uppercase.</summary>
public sealed class Localizer : INotifyPropertyChanged
{
    public static Localizer Instance { get; } = new();

    private Localizer()
    {
        var culture = CultureInfo.GetCultureInfo(_language);
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }

    private volatile string _language = "it";
    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? LanguageChanged;

    public IReadOnlyCollection<string> Languages => Strings.Tables.Keys.ToArray();

    public string Language
    {
        get => _language;
        set
        {
            if (string.IsNullOrWhiteSpace(value) || !Strings.Tables.ContainsKey(value) || value == _language) return;
            _language = value;
            var culture = CultureInfo.GetCultureInfo(value);
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Language)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
            LanguageChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public CultureInfo Culture => CultureInfo.GetCultureInfo(_language);

    public string this[string key]
    {
        get
        {
            var lang = _language;
            var table = Strings.Tables[lang];
            if (!table.TryGetValue(key, out var value) && !Strings.Tables["en"].TryGetValue(key, out value))
                return $"[{key}]";
            return key.StartsWith("Lbl", StringComparison.Ordinal)
                ? value.ToUpper(CultureInfo.GetCultureInfo(lang))
                : value;
        }
    }

    public string Format(string key, params object[] args)
    {
        var lang = _language;
        var table = Strings.Tables[lang];
        if (!table.TryGetValue(key, out var value) && !Strings.Tables["en"].TryGetValue(key, out value))
            return $"[{key}]";
        var text = key.StartsWith("Lbl", StringComparison.Ordinal)
            ? value.ToUpper(CultureInfo.GetCultureInfo(lang))
            : value;
        return string.Format(CultureInfo.GetCultureInfo(lang), text, args);
    }

    public string Plural(int n, string baseKey) => $"{n} {this[n == 1 ? baseKey + "One" : baseKey + "Other"]}";

    public void ToggleNext()
    {
        var langs = Strings.Tables.Keys.ToList();
        Language = langs[(langs.IndexOf(_language) + 1) % langs.Count];
    }

    public void SetLanguage(string language) => Language = language;
}
