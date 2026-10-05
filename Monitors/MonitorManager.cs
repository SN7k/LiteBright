using System.Runtime.InteropServices;
using BrightnessController.Native;

namespace BrightnessController.Monitors;

public sealed class MonitorManager : IDisposable
{
    private List<MonitorInfo> _monitors = new();
    private bool _disposed;

    public IReadOnlyList<MonitorInfo> Monitors => _monitors;


    public MonitorManager() { }

    public void Refresh(bool forceWmiRefresh = false)
    {
        DisposeMonitors();

        var newList = new List<MonitorInfo>();
        var (wmiAvailable, wmiRecords) = WmiMonitorHelper.GetWmiDataCached(forceWmiRefresh);
        int index = 0;

        NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero,
            (hMonitor, _, ref _, _) =>
            {
                var info = BuildMonitorInfo(hMonitor, index, wmiAvailable, wmiRecords);
                if (info != null)
                {
                    newList.Add(info);
                    index++;
                }
                return true;
            }, IntPtr.Zero);

        // Sort displays by system display order (Display 1 on top, then Display 2, etc.)
        newList.Sort((a, b) =>
        {
            if (!string.IsNullOrEmpty(a.DeviceName) && !string.IsNullOrEmpty(b.DeviceName))
                return string.Compare(a.DeviceName, b.DeviceName, StringComparison.OrdinalIgnoreCase);
            return a.Index.CompareTo(b.Index);
        });

        for (int i = 0; i < newList.Count; i++)
        {
            newList[i].Index = i;
        }

        _monitors = newList;
    }

    public bool SetBrightness(MonitorInfo mon, int percent)
    {
        percent = Math.Clamp(percent, 0, 100);

        if (mon.IsInternal)
        {
            bool ok = WmiMonitorHelper.SetBrightness(percent);
            if (ok) mon.Brightness = percent;
            return ok;
        }
        else
        {
            uint raw = DdcCiHelper.PercentToRaw(percent,
                (uint)mon.MinBrightness, (uint)mon.MaxBrightness);
            bool ok = DdcCiHelper.SetBrightness(mon.PhysicalHandle, raw);
            if (ok) mon.Brightness = (int)raw;
            return ok;
        }
    }

    public bool StepBrightness(MonitorInfo mon, int stepPercent)
    {
        int current = mon.IsInternal
            ? mon.Brightness
            : mon.BrightnessPercent;
        return SetBrightness(mon, current + stepPercent);
    }


    public bool SetContrast(MonitorInfo mon, int percent)
    {
        if (mon.IsInternal) return false;

        percent = Math.Clamp(percent, 0, 100);
        uint raw = DdcCiHelper.PercentToRaw(percent,
            (uint)mon.MinContrast, (uint)mon.MaxContrast);
        bool ok = DdcCiHelper.SetContrast(mon.PhysicalHandle, raw);
        if (ok) mon.Contrast = (int)raw;
        return ok;
    }


    private static MonitorInfo? BuildMonitorInfo(IntPtr hMonitor, int index, bool wmiAvailable, List<WmiMonitorRecord> wmiRecords)
    {
        var mi = new NativeMethods.MONITORINFOEX
        {
            cbSize = (uint)Marshal.SizeOf<NativeMethods.MONITORINFOEX>()
        };
        if (!NativeMethods.GetMonitorInfo(hMonitor, ref mi))
            return null;

        string gdiDevice = mi.szDevice.TrimEnd('\0');

        // Retrieve Hardware ID and driver description via EnumDisplayDevices
        string hwId = string.Empty;
        string driverDesc = string.Empty;
        var ddMon = new NativeMethods.DISPLAY_DEVICE { cb = (uint)Marshal.SizeOf<NativeMethods.DISPLAY_DEVICE>() };
        if (NativeMethods.EnumDisplayDevices(gdiDevice, 0, ref ddMon, 0))
        {
            driverDesc = ddMon.DeviceString?.Trim() ?? string.Empty;
            if (!string.IsNullOrEmpty(ddMon.DeviceID))
            {
                // e.g. "MONITOR\ACR0B70\{4d36e96e-e325-11ce-bfc1-08002be10318}\0001"
                string[] parts = ddMon.DeviceID.Split(new[] { '\\', '#' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 1)
                {
                    hwId = parts[1].Trim();
                }
            }
        }

        // Match with WmiMonitorID for real manufacturer model name (e.g. "KA242Y E", "Acer V206HQLB")
        WmiMonitorRecord? wmiMatch = null;
        if (!string.IsNullOrEmpty(hwId))
        {
            wmiMatch = wmiRecords.FirstOrDefault(r => string.Equals(r.HardwareId, hwId, StringComparison.OrdinalIgnoreCase));
        }
        if (wmiMatch == null && index < wmiRecords.Count)
        {
            wmiMatch = wmiRecords[index];
            if (string.IsNullOrEmpty(hwId)) hwId = wmiMatch.HardwareId;
        }

        string friendlyModelName = string.Empty;
        if (!string.IsNullOrWhiteSpace(wmiMatch?.FriendlyName))
            friendlyModelName = wmiMatch.FriendlyName;
        else if (!string.IsNullOrWhiteSpace(driverDesc) && !driverDesc.Equals("Generic PnP Monitor", StringComparison.OrdinalIgnoreCase))
            friendlyModelName = driverDesc;

        string internalName = !string.IsNullOrWhiteSpace(hwId)
            ? hwId
            : (!string.IsNullOrWhiteSpace(wmiMatch?.HardwareId) ? wmiMatch.HardwareId : gdiDevice);

        bool isPrimary = (mi.dwFlags & NativeMethods.MONITORINFOF_PRIMARY) != 0;
        bool isInternal = isPrimary && wmiAvailable;

        if (isInternal)
        {
            int brightness = WmiMonitorHelper.GetBrightness();
            if (brightness < 0) brightness = 100;

            if (string.IsNullOrEmpty(friendlyModelName))
                friendlyModelName = "Built-in Display";

            return new MonitorInfo
            {
                Index                    = index,
                Name                     = friendlyModelName,
                DeviceName               = gdiDevice,
                InternalName             = internalName,
                IsInternal               = true,
                CommunicationMethod      = "WMI",
                IsCommunicationSupported = true,
                Brightness               = brightness,
                MinBrightness            = 0,
                MaxBrightness            = 100,
                Contrast                 = 50,
                MinContrast              = 0,
                MaxContrast              = 100,
                HdrStatus                = "Unsupported",
            };
        }
        else
        {
            var physArr = DdcCiHelper.GetPhysicalMonitors(hMonitor);
            if (physArr.Length == 0)
            {
                if (string.IsNullOrEmpty(friendlyModelName))
                    friendlyModelName = $"Monitor {index + 1}";

                return new MonitorInfo
                {
                    Index                    = index,
                    Name                     = friendlyModelName,
                    DeviceName               = gdiDevice,
                    InternalName             = internalName,
                    IsInternal               = false,
                    CommunicationMethod      = "None",
                    IsCommunicationSupported = false,
                    Brightness               = 0,
                    MinBrightness            = 0,
                    MaxBrightness            = 100,
                    HdrStatus                = "Unsupported",
                };
            }

            IntPtr hPhysical = physArr[0].hPhysicalMonitor;
            string physDesc = physArr[0].szPhysicalMonitorDescription.TrimEnd('\0');

            if (string.IsNullOrEmpty(friendlyModelName))
            {
                friendlyModelName = !string.IsNullOrEmpty(physDesc) && !physDesc.Equals("Generic PnP Monitor", StringComparison.OrdinalIgnoreCase)
                    ? physDesc
                    : (string.IsNullOrEmpty(driverDesc) ? $"Monitor {index + 1}" : driverDesc);
            }

            // Probe Brightness via High-Level DDC/CI (dxva2 GetMonitorBrightness)
            uint min = 0, cur = 0, max = 0;
            bool brightOk = DdcCiHelper.TryGetBrightness(hPhysical, out min, out cur, out max);

            // Probe Contrast via High-Level DDC/CI
            uint cMin = 0, cCur = 50, cMax = 100;
            bool contrastOk = DdcCiHelper.TryGetContrast(hPhysical, out cMin, out cCur, out cMax);

            bool commSupported = brightOk && (max > min);
            string commMethod = commSupported ? "DDC/CI (HL)" : "None";

            return new MonitorInfo
            {
                Index                    = index,
                Name                     = friendlyModelName,
                DeviceName               = gdiDevice,
                InternalName             = internalName,
                IsInternal               = false,
                PhysicalHandle           = hPhysical,
                PhysicalMonitorArray     = physArr,
                CommunicationMethod      = commMethod,
                IsCommunicationSupported = commSupported,
                Brightness               = commSupported ? (int)cur : 0,
                MinBrightness            = commSupported ? (int)min : 0,
                MaxBrightness            = commSupported ? (int)max : 100,
                Contrast                 = contrastOk ? (int)cCur : 50,
                MinContrast              = contrastOk ? (int)cMin : 0,
                MaxContrast              = contrastOk ? (int)cMax : 100,
                HdrStatus                = "Unsupported",
            };
        }
    }

    private void DisposeMonitors()
    {
        foreach (var m in _monitors)
            m.Dispose();
        _monitors.Clear();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        DisposeMonitors();
    }
}
