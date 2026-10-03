using System.IO;
using System.Runtime.InteropServices;

namespace BrightnessController;

internal static class Program
{
    private const string MutexName = "BrightnessController_SingleInstance_Mutex";

    [STAThread]
    static void Main(string[] args)
    {
        if (args.Contains("--dump-monitors"))
        {
            var mgr = new Monitors.MonitorManager();
            mgr.Refresh();
            var sb = new System.Text.StringBuilder();
            foreach (var m in mgr.Monitors)
            {
                sb.AppendLine($"Name: {m.Name}");
                sb.AppendLine($"Internal Name: {m.InternalName}");
                sb.AppendLine($"Communication Method: {m.CommunicationMethod}");
                sb.AppendLine($"Current Brightness: {(m.IsCommunicationSupported ? $"{m.BrightnessPercent}" : "Not supported")}");
                sb.AppendLine($"Max Brightness: {(m.IsCommunicationSupported ? $"{m.MaxBrightness}" : "Not supported")}");
                sb.AppendLine($"Brightness Normalization: {(m.IsCommunicationSupported ? $"{m.MinBrightness} - {m.MaxBrightness}" : "Not supported")}");
                sb.AppendLine($"HDR: {m.HdrStatus}");
                sb.AppendLine();
                sb.AppendLine("x");
                sb.AppendLine();
            }
            File.WriteAllText("dump_monitors.txt", sb.ToString());
            Console.WriteLine(sb.ToString());
            return;
        }

        if (args.Contains("--render-monitors"))
        {
            var mgr = new Monitors.MonitorManager();
            mgr.Refresh();
            var form = new UI.SettingsForm(mgr);
            form.Show();
            form.SelectPage(1); // Monitor Settings
            form.UpdateLayout();
            var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(780, 560, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
            rtb.Render(form);
            using (var fs = File.Create("page_monitors.png"))
            {
                var enc = new System.Windows.Media.Imaging.PngBitmapEncoder();
                enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));
                enc.Save(fs);
            }
            form.Close();
            Console.WriteLine("Rendered page_monitors.png successfully.");
            return;
        }

        if (args.Contains("--render-general"))
        {
            var mgr = new Monitors.MonitorManager();
            mgr.Refresh();
            var form = new UI.SettingsForm(mgr);
            form.Show();
            form.SelectPage(0); // General
            form.UpdateLayout();
            var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(780, 560, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
            rtb.Render(form);
            using (var fs = File.Create("page_general.png"))
            {
                var enc = new System.Windows.Media.Imaging.PngBitmapEncoder();
                enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));
                enc.Save(fs);
            }
            form.Close();
            Console.WriteLine("Rendered page_general.png successfully.");
            return;
        }


        if (args.Contains("--render-context-menu"))
        {
            var menu = new UI.TrayContextMenu(() => { }, () => { }, () => { });
            menu.BuildUI();
            menu.Show();
            menu.UpdateLayout();
            menu.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
            int w = (int)Math.Ceiling(menu.DesiredSize.Width > 0 ? menu.DesiredSize.Width : 200);
            int h = (int)Math.Ceiling(menu.DesiredSize.Height > 0 ? menu.DesiredSize.Height : 160);
            var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(w, h, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
            rtb.Render(menu);
            using (var fs = File.Create("context_menu.png"))
            {
                var enc = new System.Windows.Media.Imaging.PngBitmapEncoder();
                enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));
                enc.Save(fs);
            }
            menu.Close();
            Console.WriteLine("Rendered context_menu.png successfully.");
            return;
        }

        using var mutex = new Mutex(initiallyOwned: true, MutexName,
            out bool createdNew);
        if (!createdNew)
        {
            MessageBox.Show(
                "Brightness Controller is already running.\n" +
                "Look for the sun icon in the system tray (^ arrow).",
                "Already Running",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        if (SynchronizationContext.Current == null)
        {
            SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
        }

        Application.ThreadException += (_, e) =>
            HandleUnhandledException(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            HandleUnhandledException(e.ExceptionObject as Exception);

        bool openSettings = args.Contains("--settings") || args.Contains("-s");
        Application.Run(new TrayApplicationContext(openSettings));

        // Keep mutex alive for entire session
        mutex.ReleaseMutex();
    }

    private static void HandleUnhandledException(Exception? ex)
    {
        if (ex == null) return;
#if DEBUG
        MessageBox.Show(ex.ToString(), "Unhandled Error",
            MessageBoxButtons.OK, MessageBoxIcon.Error);
#else
        try
        {
            string dir  = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "BrightnessController");
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "error.log"),
                $"[{DateTime.Now:u}] {ex}\n\n");
        }
        catch { }
#endif
    }
}
