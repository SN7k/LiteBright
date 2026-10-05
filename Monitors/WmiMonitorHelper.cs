using System.IO;
using System.Management;
using System.Text.Json;

namespace BrightnessController.Monitors;

public sealed class WmiCacheData
{
    public bool WmiAvailable { get; set; }
    public List<WmiMonitorRecord> Records { get; set; } = new();
}

public sealed class WmiMonitorRecord
{
    public string InstanceName { get; set; } = string.Empty;
    public string HardwareId   { get; set; } = string.Empty; // e.g. ACR051A, ACR0B70
    public string Key          { get; set; } = string.Empty; // e.g. 5&12d2be5&1&UID33536
    public string FriendlyName { get; set; } = string.Empty; // e.g. Acer V206HQLB, KA242Y E
    public string SerialNumber { get; set; } = string.Empty;
}

internal static class WmiMonitorHelper
{
    private const string WmiScope            = @"root\WMI";
    private const string BrightnessClass     = "WmiMonitorBrightness";
    private const string BrightnessMethodCls = "WmiMonitorBrightnessMethods";

    private static readonly string CachePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "BrightnessController", "wmi_cache.json");

    private static (bool wmiAvailable, List<WmiMonitorRecord> records)? _memoryCache;

    public static (bool wmiAvailable, List<WmiMonitorRecord> records) GetWmiDataCached(bool forceRefresh = false)
    {
        if (!forceRefresh && _memoryCache.HasValue)
        {
            return _memoryCache.Value;
        }

        if (!forceRefresh)
        {
            try
            {
                if (File.Exists(CachePath))
                {
                    string json = File.ReadAllText(CachePath);
                    var cached = JsonSerializer.Deserialize<WmiCacheData>(json);
                    if (cached != null)
                    {
                        _memoryCache = (cached.WmiAvailable, cached.Records);
                        return _memoryCache.Value;
                    }
                }
            }
            catch { }
        }

        // Query both WMI methods in parallel to cut latency in half
        var availTask = Task.Run(IsAvailable);
        var recsTask = Task.Run(GetWmiMonitors);
        Task.WaitAll(availTask, recsTask);

        bool avail = availTask.Result;
        var recs = recsTask.Result;
        _memoryCache = (avail, recs);

        try
        {
            string? dir = Path.GetDirectoryName(CachePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var data = new WmiCacheData { WmiAvailable = avail, Records = recs };
            File.WriteAllText(CachePath, JsonSerializer.Serialize(data));
        }
        catch { }

        return _memoryCache.Value;
    }

    public static List<WmiMonitorRecord> GetWmiMonitors()
    {
        var list = new List<WmiMonitorRecord>();
        try
        {
            using var searcher = new ManagementObjectSearcher(WmiScope, "SELECT * FROM WmiMonitorID");
            foreach (ManagementObject mo in searcher.Get())
            {
                using (mo)
                {
                    string instanceName = mo["InstanceName"]?.ToString() ?? string.Empty;
                    string[] parts = instanceName.Split('\\');
                    string hwid1 = parts.Length > 1 ? parts[1] : string.Empty;
                    string hwid2 = parts.Length > 2 ? parts[2].Split('_')[0] : string.Empty;

                    string friendlyName = ParseWmiString(mo["UserFriendlyName"]);
                    string serial = ParseWmiString(mo["SerialNumberID"]);

                    list.Add(new WmiMonitorRecord
                    {
                        InstanceName = instanceName,
                        HardwareId   = hwid1,
                        Key          = hwid2,
                        FriendlyName = friendlyName,
                        SerialNumber = serial
                    });
                }
            }
        }
        catch { /* root\WMI query failed or not supported */ }
        return list;
    }

    public static string ParseWmiString(object? obj)
    {
        if (obj is ushort[] uarr)
        {
            var chars = uarr.Where(c => c > 0).Select(c => (char)c).ToArray();
            return new string(chars).Trim();
        }
        if (obj is byte[] barr)
        {
            var chars = barr.Where(b => b > 0).Select(b => (char)b).ToArray();
            return new string(chars).Trim();
        }
        return obj?.ToString() ?? string.Empty;
    }

    public static int GetBrightness()
    {
        try
        {
            using var mc = new ManagementClass(WmiScope, BrightnessClass, null);
            foreach (ManagementObject mo in mc.GetInstances())
            {
                using (mo)
                {
                    var val = mo["CurrentBrightness"];
                    if (val != null)
                        return Convert.ToInt32(val);
                }
            }
        }
        catch { /* WMI not available (desktop PC) */ }
        return -1;
    }

    public static bool SetBrightness(int value, uint timeout = 1)
    {
        value = Math.Clamp(value, 0, 100);
        try
        {
            using var mc = new ManagementClass(WmiScope, BrightnessMethodCls, null);
            foreach (ManagementObject mo in mc.GetInstances())
            {
                using (mo)
                {
                    // WmiSetBrightness(Timeout, Brightness)
                    mo.InvokeMethod("WmiSetBrightness", new object[] { timeout, (byte)value });
                    return true;
                }
            }
        }
        catch { /* WMI not available */ }
        return false;
    }

    public static bool IsAvailable()
    {
        try
        {
            using var mc = new ManagementClass(WmiScope, BrightnessClass, null);
            foreach (ManagementObject mo in mc.GetInstances())
            {
                using (mo) return true;
            }
        }
        catch { }
        return false;
    }
}

