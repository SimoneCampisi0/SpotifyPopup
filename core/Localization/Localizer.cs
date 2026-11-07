using System.ComponentModel;
using System.Globalization;
using System.Resources;

namespace SpotifyPopup;

// Only UI culture changes. JSON, shortcut syntax and elapsed time stay invariant.
public sealed class Localizer : INotifyPropertyChanged
{
    public static Localizer Current { get; } = new();
    public static readonly ResourceManager Resources = new("SpotifyPopup.Localization.Strings", typeof(Localizer).Assembly);
    private readonly CultureInfo systemCulture = CultureInfo.CurrentUICulture;
    public CultureInfo Culture { get; private set; } = CultureInfo.GetCultureInfo("en");
    public event PropertyChangedEventHandler? PropertyChanged;
    public string this[string key] => Get(key);
    public static string Normalize(string? language) => language is "en" or "it" ? language : "auto";
    public static string Resolve(string? language, CultureInfo system) => Normalize(language) switch
    {
        "en" => "en", "it" => "it", _ => system.TwoLetterISOLanguageName == "it" ? "it" : "en"
    };
    public static string Get(string key) => Resources.GetString(key, Current.Culture) ?? key;
    public void Apply(string? language)
    {
        var culture = CultureInfo.GetCultureInfo(Resolve(language, systemCulture));
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        if (Culture.Name == culture.Name) return;
        Culture = culture;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
    }
}
