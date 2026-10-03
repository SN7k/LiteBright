using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;
using BrightnessController.Settings;

namespace BrightnessController.Helpers;

/// <summary>
/// Comprehensive localization engine powered by Twinkle Tray translation datasets.
/// Supports all world cultures, system language auto-detection, and real-time UI localization.
/// </summary>
public static class LocalizationManager
{
    private static readonly Dictionary<string, Dictionary<string, string>> Translations = new(StringComparer.OrdinalIgnoreCase);
    private static Dictionary<string, string> _activeData = new(StringComparer.OrdinalIgnoreCase);
    private static Dictionary<string, string> _fallbackData = new(StringComparer.OrdinalIgnoreCase);

    public const string SystemDefaultLanguage = "System language (default)";

    public static event Action? LanguageChanged;

    /// <summary>
    /// All world languages (e.g. "English (United States)", "Spanish (Spain)", "French (France)", etc.)
    /// matching native Windows Settings / Region list, sorted alphabetically with System Default at the top.
    /// </summary>
    public static List<string> AllWorldLanguages { get; } = new();

    static LocalizationManager()
    {
        LoadAllTranslations();
        BuildWorldLanguageList();

        string initial = SettingsManager.Current?.Language ?? SystemDefaultLanguage;
        SetLanguage(initial, notify: false);
    }

    private static void LoadAllTranslations()
    {
        // 1. Try loading from Localization directory on disk
        string locDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Localization");
        if (Directory.Exists(locDir))
        {
            foreach (string file in Directory.GetFiles(locDir, "*.json"))
            {
                try
                {
                    string code = Path.GetFileNameWithoutExtension(file);
                    string json = File.ReadAllText(file, Encoding.UTF8);
                    var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                    if (dict != null)
                    {
                        Translations[code] = dict;
                    }
                }
                catch { }
            }
        }

        // 2. Also inspect embedded resources as fallback
        var asm = Assembly.GetExecutingAssembly();
        foreach (string res in asm.GetManifestResourceNames())
        {
            if (res.Contains(".Localization.", StringComparison.OrdinalIgnoreCase) && res.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    string filename = res.Substring(res.IndexOf(".Localization.", StringComparison.OrdinalIgnoreCase) + 14);
                    string code = filename.Substring(0, filename.Length - 5);
                    if (!Translations.ContainsKey(code))
                    {
                        using var stream = asm.GetManifestResourceStream(res);
                        if (stream != null)
                        {
                            using var reader = new StreamReader(stream, Encoding.UTF8);
                            string json = reader.ReadToEnd();
                            var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                            if (dict != null)
                            {
                                Translations[code] = dict;
                            }
                        }
                    }
                }
                catch { }
            }
        }

        if (Translations.TryGetValue("en", out var enData))
        {
            _fallbackData = enData;
        }
    }

    public record LanguageItem(string Code, string DisplayName);

    public static readonly List<LanguageItem> SupportedLanguages = new()
    {
        new("system", "System language (default)"),
        new("en", "English (United States)"),
        new("en-GB", "English (United Kingdom)"),
        new("es", "Español (Spanish)"),
        new("fr", "Français (French)"),
        new("de", "Deutsch (German)"),
        new("it", "Italiano (Italian)"),
        new("pt-BR", "Português - Brasil (Portuguese - Brazil)"),
        new("pt", "Português (Portuguese)"),
        new("ru", "Русский (Russian)"),
        new("zh_Hans", "中文 (简体) - Chinese (Simplified)"),
        new("zh-Hant", "中文 (繁體) - Chinese (Traditional)"),
        new("ja", "日本語 (Japanese)"),
        new("ko", "한국어 (Korean)"),
        new("hi", "हिन्दी (Hindi)"),
        new("bn", "বাংলা (Bengali)"),
        new("ar", "العربية (Arabic)"),
        new("nl", "Nederlands (Dutch)"),
        new("pl", "Polski (Polish)"),
        new("tr", "Türkçe (Turkish)"),
        new("uk", "Українська (Ukrainian)"),
        new("vi", "Tiếng Việt (Vietnamese)"),
        new("th", "ภาษาไทย (Thai)"),
        new("sv", "Svenska (Swedish)"),
        new("id", "Bahasa Indonesia (Indonesian)"),
        new("cs", "Čeština (Czech)"),
        new("el", "Ελληνικά (Greek)"),
        new("hu", "Magyar (Hungarian)"),
        new("ro", "Română (Romanian)"),
        new("sk", "Slovenčina (Slovak)"),
        new("fi", "Suomi (Finnish)"),
        new("nb", "Norsk bokmål (Norwegian Bokmål)"),
        new("he", "עברית (Hebrew)"),
        new("fa", "فارسی (Persian)"),
        new("ta", "தமிழ் (Tamil)"),
        new("az", "Azərbaycan dili (Azerbaijani)"),
        new("ckb", "کوردی (Kurdish)"),
        new("hr", "Hrvatski (Croatian)"),
        new("lt", "Lietuvių (Lithuanian)")
    };

    private static void BuildWorldLanguageList()
    {
        AllWorldLanguages.Clear();
        foreach (var lang in SupportedLanguages)
        {
            AllWorldLanguages.Add(lang.DisplayName);
        }
    }

    public static string GetLanguageDisplayName(string? codeOrName)
    {
        if (string.IsNullOrWhiteSpace(codeOrName) ||
            codeOrName.Equals("system", StringComparison.OrdinalIgnoreCase) ||
            codeOrName.Equals(SystemDefaultLanguage, StringComparison.OrdinalIgnoreCase))
        {
            return SystemDefaultLanguage;
        }

        var match = SupportedLanguages.FirstOrDefault(l =>
            string.Equals(l.Code, codeOrName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(l.DisplayName, codeOrName, StringComparison.OrdinalIgnoreCase));

        return match?.DisplayName ?? codeOrName;
    }

    public static void SetLanguage(string languageChoice, bool notify = true)
    {
        string targetCode = ResolveLanguageCode(languageChoice);

        if (Translations.TryGetValue(targetCode, out var dict))
        {
            _activeData = dict;
        }
        else if (Translations.TryGetValue("en", out var en))
        {
            _activeData = en;
        }

        if (notify)
        {
            LanguageChanged?.Invoke();
        }
    }

    private static string ResolveLanguageCode(string choice)
    {
        if (string.IsNullOrWhiteSpace(choice) ||
            choice.Equals(SystemDefaultLanguage, StringComparison.OrdinalIgnoreCase) ||
            choice.Equals("system", StringComparison.OrdinalIgnoreCase))
        {
            var sys = CultureInfo.CurrentUICulture;
            return MatchCultureToCode(sys);
        }

        // Direct matching code (e.g. "en", "es", "zh_Hans")
        if (Translations.ContainsKey(choice))
            return choice;

        // Match from SupportedLanguages
        var matched = SupportedLanguages.FirstOrDefault(l =>
            string.Equals(l.DisplayName, choice, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(l.Code, choice, StringComparison.OrdinalIgnoreCase) ||
            choice.StartsWith(l.DisplayName, StringComparison.OrdinalIgnoreCase));

        if (matched != null && Translations.ContainsKey(matched.Code))
            return matched.Code;

        // Substring / fuzzy match
        foreach (var l in SupportedLanguages)
        {
            if (l.DisplayName.Contains(choice, StringComparison.OrdinalIgnoreCase) ||
                choice.Contains(l.DisplayName, StringComparison.OrdinalIgnoreCase))
            {
                if (Translations.ContainsKey(l.Code))
                    return l.Code;
            }
        }

        return "en";
    }

    private static string MatchCultureToCode(CultureInfo c)
    {
        string name = c.Name; // e.g. "en-GB", "zh-CN", "pt-BR"

        if (name.Equals("zh-CN", StringComparison.OrdinalIgnoreCase) || name.StartsWith("zh-Hans", StringComparison.OrdinalIgnoreCase))
            return "zh_Hans";
        if (name.Equals("zh-TW", StringComparison.OrdinalIgnoreCase) || name.Equals("zh-HK", StringComparison.OrdinalIgnoreCase) || name.StartsWith("zh-Hant", StringComparison.OrdinalIgnoreCase))
            return "zh-Hant";
        if (name.Equals("en-GB", StringComparison.OrdinalIgnoreCase))
            return "en-GB";
        if (name.Equals("pt-BR", StringComparison.OrdinalIgnoreCase))
            return "pt-BR";

        // Try exact name match
        if (Translations.ContainsKey(name))
            return name;

        // Try two-letter ISO language name (e.g. "es", "fr", "de", "hi", "bn", "ja", "ko", etc.)
        string two = c.TwoLetterISOLanguageName;
        if (Translations.ContainsKey(two))
            return two;

        return "en";
    }

    /// <summary>
    /// Gets localized string for specified key, falling back to English or provided fallback.
    /// </summary>
    public static string T(string key, string fallback = "")
    {
        if (_activeData != null && _activeData.TryGetValue(key, out var val) && !string.IsNullOrEmpty(val))
            return val;

        if (_fallbackData != null && _fallbackData.TryGetValue(key, out var defVal) && !string.IsNullOrEmpty(defVal))
            return defVal;

        return string.IsNullOrEmpty(fallback) ? key : fallback;
    }
}
