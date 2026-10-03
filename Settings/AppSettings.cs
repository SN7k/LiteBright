using BrightnessController.Hotkeys;

namespace BrightnessController.Settings;

/// <summary>
/// Persistent application settings serialised to JSON in %AppData%.
/// All properties have safe defaults so a missing file still works.
/// </summary>
public sealed class AppSettings
{
    // ── General ───────────────────────────────────────────────────────────────
    public bool   StartWithWindows          { get; set; } = true;
    public bool   ApplyBrightnessAtStartup  { get; set; } = true;
    public string Language                  { get; set; } = "System language (default)";
    public string Theme                     { get; set; } = "System preferences (default)";
    public bool   AutoBrightness            { get; set; } = false;
    public bool   EnableContrastSlider      { get; set; } = false;
    public int    BrightnessStep            { get; set; } = 10;   // % per hotkey press


    // ── Hotkey bindings ───────────────────────────────────────────────────────
    // Stored as a list so it survives JSON round-trips cleanly.
    public List<HotkeyEntry> Hotkeys { get; set; } = new();

    // ── Per-monitor last-used brightness (optional restore on startup) ────────
    public Dictionary<string, int> LastBrightness { get; set; } = new();

    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Flat DTO used when serialising hotkey bindings to JSON.</summary>
    public sealed class HotkeyEntry
    {
        public HotkeyAction    Action    { get; set; }
        public HotkeyModifiers Modifiers { get; set; }
        public Keys            Key       { get; set; }

        public HotkeyDefinition ToDefinition() =>
            new() { Action = Action, Modifiers = Modifiers, Key = Key };
    }
}
