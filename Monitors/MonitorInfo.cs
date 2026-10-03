namespace BrightnessController.Monitors;

public sealed class MonitorInfo : IDisposable
{
    public string Name { get; set; } = string.Empty;

    public string DeviceName { get; set; } = string.Empty;

    public string InternalName { get; set; } = string.Empty;

    public int Index { get; set; }

    public bool IsInternal { get; set; }

    public string CommunicationMethod { get; set; } = "None";

    public bool IsCommunicationSupported { get; set; } = false;

    public string HdrStatus { get; set; } = "Unsupported";

    public int Brightness    { get; set; } = 100;
    public int MinBrightness { get; set; } = 0;
    public int MaxBrightness { get; set; } = 100;

    public int Contrast      { get; set; } = 50;
    public int MinContrast   { get; set; } = 0;
    public int MaxContrast   { get; set; } = 100;

    public IntPtr PhysicalHandle { get; set; } = IntPtr.Zero;

    internal Native.NativeMethods.PHYSICAL_MONITOR[]? PhysicalMonitorArray { get; set; }

    public int BrightnessPercent =>
        MaxBrightness > MinBrightness
            ? (int)Math.Round((Brightness - MinBrightness) * 100.0 / (MaxBrightness - MinBrightness))
            : Brightness;

    public int ContrastPercent =>
        MaxContrast > MinContrast
            ? (int)Math.Round((Contrast - MinContrast) * 100.0 / (MaxContrast - MinContrast))
            : Contrast;

    public override string ToString() => Name;

    public void Dispose()
    {
        if (PhysicalMonitorArray is { Length: > 0 })
        {
            Native.NativeMethods.DestroyPhysicalMonitors(
                (uint)PhysicalMonitorArray.Length, PhysicalMonitorArray);
            PhysicalMonitorArray = null;
        }
    }
}
