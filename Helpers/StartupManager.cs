using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;
using Windows.ApplicationModel;

namespace BrightnessController.Helpers;

public static class StartupManager
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "BrightnessController";
    public const string StartupTaskId = "LiteBrightStartupTask";

    private static string ExePath => Environment.ProcessPath
        ?? System.IO.Path.Combine(AppContext.BaseDirectory, "BrightnessController.exe");

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetCurrentPackageFullName(ref int packageFullNameLength, StringBuilder? packageFullName);

    public static bool IsPackaged()
    {
        try
        {
            int length = 0;
            int result = GetCurrentPackageFullName(ref length, null);
            return result != 15700; // APPMODEL_ERROR_NO_PACKAGE
        }
        catch
        {
            return false;
        }
    }

    public static bool IsEnabled()
    {
        if (IsPackaged())
        {
            try
            {
                var task = StartupTask.GetAsync(StartupTaskId).AsTask().GetAwaiter().GetResult();
                return task.State == StartupTaskState.Enabled;
            }
            catch
            {
                return true; // Default enabled in MSIX manifest
            }
        }

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: false);
            return key?.GetValue(ValueName) is string existing
                && existing.Equals($"\"{ExePath}\"", StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    public static void Enable()
    {
        if (IsPackaged())
        {
            try
            {
                // Clean up any stale registry key if it existed
                using var runKey = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
                runKey?.DeleteValue(ValueName, throwOnMissingValue: false);

                var task = StartupTask.GetAsync(StartupTaskId).AsTask().GetAwaiter().GetResult();
                if (task.State == StartupTaskState.Disabled)
                {
                    _ = task.RequestEnableAsync().AsTask().GetAwaiter().GetResult();
                }
            }
            catch { }
            return;
        }

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            key?.SetValue(ValueName, $"\"{ExePath}\"");

            // Disable Windows Explorer 10-30 second startup delay for user startup apps
            using var serializeKey = Registry.CurrentUser.CreateSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Explorer\Serialize");
            serializeKey?.SetValue("StartupDelayInMSec", 0, RegistryValueKind.DWord);
        }
        catch { }
    }

    public static void Disable()
    {
        if (IsPackaged())
        {
            try
            {
                var task = StartupTask.GetAsync(StartupTaskId).AsTask().GetAwaiter().GetResult();
                task.Disable();
            }
            catch { }
            return;
        }

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            key?.DeleteValue(ValueName, throwOnMissingValue: false);
        }
        catch { }
    }

    public static void Apply(bool enable)
    {
        if (enable) Enable();
        else        Disable();
    }
}
