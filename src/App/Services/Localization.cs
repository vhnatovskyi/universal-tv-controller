using System.Collections;
using System.Globalization;
using System.Resources;

namespace BraviaRemote;

internal static class Localization
{
    private const string PreferenceKey = "app-language";
    private static readonly ResourceManager Strings = new("BraviaRemote.Resources.Localization.Strings", typeof(Localization).Assembly);
    private static CultureInfo culture = CultureInfo.GetCultureInfo("en");

    public static bool IsUkrainian => culture.Name == "uk";

    public static void Initialize(ResourceDictionary resources) =>
        Apply(resources, Preferences.Default.Get(PreferenceKey, "en") == "uk");

    public static void SetLanguage(ResourceDictionary resources, bool ukrainian)
    {
        Apply(resources, ukrainian);
        Preferences.Default.Set(PreferenceKey, culture.Name);
    }

    public static string Text(string key, params object[] arguments)
    {
        var value = Strings.GetString(key, culture) ?? key;
        return arguments.Length == 0 ? value : string.Format(culture, value, arguments);
    }

    private static void Apply(ResourceDictionary resources, bool ukrainian)
    {
        culture = CultureInfo.GetCultureInfo(ukrainian ? "uk" : "en");
        var neutral = Strings.GetResourceSet(CultureInfo.InvariantCulture, true, false)!;
        foreach (DictionaryEntry item in neutral)
        {
            var key = (string)item.Key;
            resources[$"Loc.{key}"] = Text(key);
        }
    }
}
