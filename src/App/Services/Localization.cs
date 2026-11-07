using System.Collections;
using System.Globalization;
using System.Resources;

namespace UniversalTvRemote;

internal static class Localization
{
    private const string PreferenceKey = "app-language";
    private static readonly ResourceManager Strings = new("UniversalTvRemote.Resources.Localization.Strings", typeof(Localization).Assembly);
    private static CultureInfo culture = CultureInfo.GetCultureInfo("en");

    public static string Mode { get; private set; } = "system";
    public static bool IsUkrainian => culture.Name == "uk";

    public static void Initialize(ResourceDictionary resources) =>
        Apply(resources, Preferences.Default.Get(PreferenceKey, "system"));

    public static void SetLanguage(ResourceDictionary resources, string mode)
    {
        Apply(resources, mode);
        Preferences.Default.Set(PreferenceKey, Mode);
    }

    public static string Text(string key, params object[] arguments)
    {
        var value = Strings.GetString(key, culture) ?? key;
        return arguments.Length == 0 ? value : string.Format(culture, value, arguments);
    }

    public static string Date(DateTimeOffset date) => date.ToLocalTime().ToString("d", culture);

    private static void Apply(ResourceDictionary resources, string mode)
    {
        Mode = mode is "en" or "uk" ? mode : "system";
        var language = Mode == "system" ? CultureInfo.CurrentUICulture.TwoLetterISOLanguageName : Mode;
        culture = CultureInfo.GetCultureInfo(language == "uk" ? "uk" : "en");
        var neutral = Strings.GetResourceSet(CultureInfo.InvariantCulture, true, false)!;
        foreach (DictionaryEntry item in neutral)
        {
            var key = (string)item.Key;
            resources[$"Loc.{key}"] = Text(key);
        }
    }
}
