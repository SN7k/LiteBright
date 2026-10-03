using Microsoft.Win32;
using BrightnessController.Settings;

namespace BrightnessController.Helpers;

public static class ThemeHelper
{
    /// <summary>
    /// Event fired whenever the user changes Windows theme, accent color, or personalization settings.
    /// </summary>
    public static event Action? ThemeChanged;

    public static void NotifyThemeChanged() => ThemeChanged?.Invoke();

    static ThemeHelper()
    {
        try
        {
            SystemEvents.UserPreferenceChanged += (_, e) =>
            {
                if (e.Category == UserPreferenceCategory.General ||
                    e.Category == UserPreferenceCategory.Color ||
                    e.Category == UserPreferenceCategory.VisualStyle)
                {
                    ThemeChanged?.Invoke();
                }
            };
        }
        catch { }
    }

    /// <summary>
    /// True if Windows system shell (taskbar, flyouts) uses light theme.
    /// </summary>
    public static bool IsLightTheme
    {
        get
        {
            try
            {
                var userTheme = SettingsManager.Current?.Theme;
                if (string.Equals(userTheme, "Light", StringComparison.OrdinalIgnoreCase)) return true;
                if (string.Equals(userTheme, "Dark", StringComparison.OrdinalIgnoreCase)) return false;

                using var key = Registry.CurrentUser.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                // SystemUsesLightTheme governs the taskbar and shell flyouts in Windows 11
                if (key?.GetValue("SystemUsesLightTheme") is int sysLight)
                    return sysLight == 1;

                if (key?.GetValue("AppsUseLightTheme") is int appLight)
                    return appLight == 1;
            }
            catch { }
            return false;
        }
    }

    /// <summary>
    /// True if the user enabled "Show accent color on Start and taskbar" in Windows Settings.
    /// When true, system flyouts (like Language and Quick Settings) tint their Acrylic background with the accent color.
    /// </summary>
    public static bool IsColorPrevalence
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                if (key?.GetValue("ColorPrevalence") is int cp)
                    return cp == 1;

                using var dwmKey = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\DWM");
                if (dwmKey?.GetValue("ColorPrevalence") is int dwmCp)
                    return dwmCp == 1;
            }
            catch { }
            return false;
        }
    }

    /// <summary>
    /// The user's active Windows Accent Color.
    /// </summary>
    public static Color AccentColor
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\DWM");
                if (key?.GetValue("AccentColor") is int abgr)
                {
                    int r = (abgr >>  0) & 0xFF;
                    int g = (abgr >>  8) & 0xFF;
                    int b = (abgr >> 16) & 0xFF;
                    return Color.FromArgb(r, g, b);
                }

                if (key?.GetValue("ColorizationColor") is int argb)
                {
                    int r = (argb >> 16) & 0xFF;
                    int g = (argb >>  8) & 0xFF;
                    int b = (argb >>  0) & 0xFF;
                    return Color.FromArgb(r, g, b);
                }
            }
            catch { }
            return Color.FromArgb(0, 103, 192);
        }
    }

    /// <summary>
    /// Returns the exact palette for the flyout to match native Windows 11 flyouts.
    /// Uses genuine translucent Acrylic alpha levels (around 25-30% opacity) so the desktop wallpaper blurs through.
    /// If ColorPrevalence is enabled, tints with the accent color (matching the red flyout in Windows 11).
    /// </summary>
    public static (Color background, Color border, Color sliderFill, bool isAccentThemed) GetFlyoutPalette()
    {
        bool isLight = IsLightTheme;
        bool prevalence = IsColorPrevalence;
        Color accent = AccentColor;

        if (prevalence)
        {
            // Windows 11 Accent-Themed Flyout (e.g. Red, Blue, Purple, etc.)
            Color accentBase = GetAccentPaletteDark2() ?? (isLight
                ? Blend(accent, Color.FromArgb(245, 245, 248), 0.70f)
                : Blend(accent, Color.FromArgb(16, 16, 20), 0.40f));

            // Alpha 75 (~30% opacity) gives genuine frosted glass transparency with wallpaper bleed-through
            Color bg = Color.FromArgb(75, accentBase.R, accentBase.G, accentBase.B);
            Color border = Color.FromArgb(60,
                Math.Min(255, accent.R + 50),
                Math.Min(255, accent.G + 50),
                Math.Min(255, accent.B + 50));

            // When background is accent-colored, slider active fill uses crisp white for high contrast
            Color sliderFill = Color.FromArgb(255, 255, 255, 255);

            return (bg, border, sliderFill, true);
        }
        else
        {
            // Standard Neutral Windows 11 Acrylic
            if (isLight)
            {
                // Light mode translucent Acrylic (alpha 85 ~33% opacity)
                Color bg = Color.FromArgb(85, 246, 246, 248);
                Color border = Color.FromArgb(25, 0, 0, 0);
                Color sliderFill = accent;
                return (bg, border, sliderFill, false);
            }
            else
            {
                // Dark mode translucent Acrylic (alpha 70 ~27% opacity)
                // DirectComposition Acrylic blur lets wallpaper curves bleed through clearly
                Color bg = Color.FromArgb(70, 30, 30, 34);
                Color border = Color.FromArgb(35, 255, 255, 255);
                Color sliderFill = accent;
                return (bg, border, sliderFill, false);
            }
        }
    }

    /// <summary>
    /// Reads AccentDark2 from the Windows Explorer AccentPalette registry if present.
    /// This is the exact shade Windows uses for tinted Start and taskbar surfaces.
    /// </summary>
    private static Color? GetAccentPaletteDark2()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Explorer\Accent");
            if (key?.GetValue("AccentPalette") is byte[] pal && pal.Length >= 24)
            {
                // AccentDark2 is at offset 20..23 (R, G, B, A)
                return Color.FromArgb(pal[20], pal[21], pal[22]);
            }

            if (key?.GetValue("StartColorMenu") is int scm)
            {
                int r = (scm >>  0) & 0xFF;
                int g = (scm >>  8) & 0xFF;
                int b = (scm >> 16) & 0xFF;
                return Color.FromArgb(r, g, b);
            }
        }
        catch { }
        return null;
    }

    public static Color AccentColorLight => Blend(AccentColor, Color.White, 0.35f);
    public static Color AccentColorDark  => Blend(AccentColor, Color.Black, 0.25f);
    public static Color AccentMenuHover  => Blend(AccentColor, Color.White, 0.80f);

    public static Color Blend(Color a, Color b, float t)
        => Color.FromArgb(
            Math.Clamp((int)(a.R + (b.R - a.R) * t), 0, 255),
            Math.Clamp((int)(a.G + (b.G - a.G) * t), 0, 255),
            Math.Clamp((int)(a.B + (b.B - a.B) * t), 0, 255));
}
