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

    private string _language = "it";
    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? LanguageChanged;

    public IReadOnlyCollection<string> Languages => Strings.Tables.Keys.ToArray();

    public string Language
    {
        get => _language;
        set
        {
            if (!Strings.Tables.ContainsKey(value) || value == _language) return;
            _language = value;
            var culture = Culture;
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
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
            var table = Strings.Tables[_language];
            if (!table.TryGetValue(key, out var value) && !Strings.Tables["en"].TryGetValue(key, out value))
                return $"[{key}]";
            return key.StartsWith("Lbl", StringComparison.Ordinal) ? value.ToUpper(Culture) : value;
        }
    }

    public string Format(string key, params object[] args) => string.Format(Culture, this[key], args);

    public string Plural(int n, string baseKey) => $"{n} {this[n == 1 ? baseKey + "One" : baseKey + "Other"]}";

    public void ToggleNext()
    {
        var langs = Strings.Tables.Keys.ToList();
        Language = langs[(langs.IndexOf(_language) + 1) % langs.Count];
    }

    public void SetLanguage(string language) => Language = language;
}
