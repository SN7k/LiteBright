using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BrightnessController.Settings;

public static class SettingsManager
{
    // ── File location ─────────────────────────────────────────────────────────

    private static readonly string SettingsDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "BrightnessController");

    private static readonly string SettingsFile =
        Path.Combine(SettingsDir, "settings.json");

    // ── JSON options ──────────────────────────────────────────────────────────

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented          = true,
        Converters             = { new JsonStringEnumConverter() },
        PropertyNameCaseInsensitive = true,
    };

    // ── Cached instance ───────────────────────────────────────────────────────

    private static AppSettings? _current;
    public  static AppSettings  Current => _current ??= Load();


    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsFile))
            {
                string json = File.ReadAllText(SettingsFile);
                var s = JsonSerializer.Deserialize<AppSettings>(json, JsonOpts);
                if (s != null)
                {
                    _current = s;
                    return s;
                }
            }
        }
        catch
        {
            // Corrupted file -> fall back to defaults silently.
        }
        _current = new AppSettings();
        return _current;
    }


    public static void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(SettingsDir);
            string json = JsonSerializer.Serialize(settings, JsonOpts);
            File.WriteAllText(SettingsFile, json);
            _current = settings;
        }
        catch
        {
            // Non-fatal; settings will revert on next launch.
        }
    }

    public static void Save() => Save(Current);
}
