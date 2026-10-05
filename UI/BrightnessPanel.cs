using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Shell;
using BrightnessController.Helpers;
using BrightnessController.Monitors;
using BrightnessController.Native;
using BrightnessController.Settings;
using Brushes = System.Windows.Media.Brushes;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using Cursors = System.Windows.Input.Cursors;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using VerticalAlignment = System.Windows.VerticalAlignment;

namespace BrightnessController.UI;

/// <summary>
/// Seamless Windows 11 flyout matching the native Language & Quick Settings flyouts.
/// Built with hardware DirectComposition Acrylic blur, Segoe Fluent Icons, and fluent sliders.
/// Fully synchronized with Windows 11 theme, accent colors, and ColorPrevalence.
/// </summary>
public sealed class BrightnessPanel : Window, IDisposable
{
    private readonly MonitorManager _monitors;
    private readonly Dictionary<string, (FluentSlider bSlider, TextBlock valText)> _monitorControls = new();
    public event Action? SettingsRequested;

    public bool Visible => IsVisible;
    public IntPtr Handle => new WindowInteropHelper(this).EnsureHandle();

    public void UpdateBrightness(MonitorInfo mon, int percent)
    {
        if (_monitorControls.TryGetValue(mon.InternalName, out var ctrl))
        {
            ctrl.bSlider.SetValueDirect(percent);
            ctrl.valText.Text = $"{percent}%";
        }
    }

    // Typography
    public static string SystemFontName => _systemFontName ??= GetSystemFont();
    public static string TextFontName   => SystemFontName;
    private static string? _systemFontName;
    private static string GetSystemFont()
    {
        try
        {
            var font = new System.Windows.Media.FontFamily("Segoe UI Variable Text");
            if (font.Source == "Segoe UI Variable Text") return "Segoe UI Variable Text";
        }
        catch { }
        return "Segoe UI";
    }

    public static string IconFontName => _iconFontName ??= GetIconFont();
    private static string? _iconFontName;
    private static string GetIconFont()
    {
        try
        {
            var font = new System.Windows.Media.FontFamily("Segoe Fluent Icons");
            if (font.Source == "Segoe Fluent Icons") return "Segoe Fluent Icons";
        }
        catch { }
        return "Segoe MDL2 Assets";
    }

    // Win32 Interop for robust positioning and Z-Order
    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int x; public int y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [DllImport("user32.dll")]
    private static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

    private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    private const uint SWP_NOMOVE     = 0x0002;
    private const uint SWP_NOSIZE     = 0x0001;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_SHOWWINDOW = 0x0040;

    // Track active monitor layout to recalculate position if size changes dynamically
    private RECT _activeMonitorWork;
    private RECT _activeMonitorBounds;
    private int _detectedTaskbarTop;
    private double _activeScaleX = 1.0;
    private double _activeScaleY = 1.0;
    private int _lastAnchorX;

    public BrightnessPanel(MonitorManager monitors)
    {
        _monitors = monitors;

        WindowStyle           = WindowStyle.None;
        ResizeMode            = ResizeMode.NoResize;
        ShowInTaskbar         = false;
        Topmost               = true;
        SizeToContent         = SizeToContent.Height;
        Width                 = 330;
        Background            = Brushes.Transparent;
        WindowStartupLocation = WindowStartupLocation.Manual;

        WindowChrome.SetWindowChrome(this, new WindowChrome
        {
            CaptionHeight         = 0,
            CornerRadius          = new CornerRadius(0),
            GlassFrameThickness   = new Thickness(-1),
            UseAeroCaptionButtons = false
        });

        Deactivated += (_, _) => Hide();

        // Reposition dynamically if content height changes to guarantee it stays strictly above taskbar
        SizeChanged += (_, _) =>
        {
            if (IsVisible && _detectedTaskbarTop > 0 && ActualHeight > 0)
            {
                RepositionWindow();
            }
        };

        // Listen for live Windows theme changes
        ThemeHelper.ThemeChanged += OnThemeChanged;
    }

    public void Dispose()
    {
        ThemeHelper.ThemeChanged -= OnThemeChanged;
        Close();
    }

    public void BeginInvoke(Action action) => Dispatcher.BeginInvoke(action);

    private void OnThemeChanged()
    {
        Dispatcher.InvokeAsync(() =>
        {
            ApplyDwmAttributes();
            if (IsVisible)
            {
                BuildUI();
            }
        });
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var hwnd = new WindowInteropHelper(this).Handle;
        var source = HwndSource.FromHwnd(hwnd);
        if (source?.CompositionTarget != null)
        {
            source.CompositionTarget.BackgroundColor = System.Windows.Media.Colors.Transparent;
        }

        ApplyDwmAttributes();
        source?.AddHook(WndProc);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_SETTINGCHANGE = 0x001A;
        const int WM_THEMECHANGED   = 0x031A;
        const int WM_DWMCOLORIZATIONCOLORCHANGED = 0x0320;

        if (msg == WM_SETTINGCHANGE || msg == WM_THEMECHANGED || msg == WM_DWMCOLORIZATIONCOLORCHANGED)
        {
            OnThemeChanged();
        }
        return IntPtr.Zero;
    }

    private void ApplyDwmAttributes()
    {
        IntPtr hwnd = new WindowInteropHelper(this).EnsureHandle();
        bool isDark = !ThemeHelper.IsLightTheme;
        int dark = isDark ? 1 : 0;
        NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));

        int corner = NativeMethods.DWMWCP_ROUND;
        NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE, ref corner, sizeof(int));

        // Windows 11 DirectComposition Acrylic
        int backdrop = NativeMethods.DWMSBT_TRANSIENTWINDOW;
        NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_SYSTEMBACKDROP_TYPE, ref backdrop, sizeof(int));

        // Extend glass frame into the entire client area
        var margins = new NativeMethods.MARGINS { cxLeftWidth = -1, cxRightWidth = -1, cyTopHeight = -1, cyBottomHeight = -1 };
        NativeMethods.DwmExtendFrameIntoClientArea(hwnd, ref margins);

        // Always apply DWM Acrylic BlurBehind with exact alpha/color gradient
        // This guarantees deep frosted-glass blur and wallpaper bleed-through across all Windows 11 builds
        try
        {
            var palette = ThemeHelper.GetFlyoutPalette();
            int gradient = (palette.background.A << 24) |
                           (palette.background.B << 16) |
                           (palette.background.G << 8)  |
                           palette.background.R;

            var accent = new NativeMethods.AccentPolicy
            {
                AccentState   = NativeMethods.ACCENT_ENABLE_ACRYLICBLURBEHIND,
                GradientColor = gradient,
                AccentFlags   = 2
            };
            int accentStructSize = Marshal.SizeOf(accent);
            IntPtr accentPtr = Marshal.AllocHGlobal(accentStructSize);
            Marshal.StructureToPtr(accent, accentPtr, false);

            var data = new NativeMethods.WindowCompositionAttributeData
            {
                Attribute  = NativeMethods.WCA_ACCENT_POLICY,
                SizeOfData = accentStructSize,
                Data       = accentPtr
            };
            NativeMethods.SetWindowCompositionAttribute(hwnd, ref data);
            Marshal.FreeHGlobal(accentPtr);
        }
        catch { }
    }

    public void Prewarm()
    {
        BuildUI();
        ApplyDwmAttributes();
        if (Content is UIElement contentElement)
        {
            contentElement.Measure(new System.Windows.Size(Width, double.PositiveInfinity));
        }
    }

    public void RefreshMonitors()
    {
        BuildUI();
        if (IsVisible)
        {
            RepositionWindow();
        }
    }

    public void ShowAtTray(System.Drawing.Point anchor)
    {
        BuildUI();
        ApplyDwmAttributes();

        IntPtr hwnd = new WindowInteropHelper(this).EnsureHandle();
        var source = HwndSource.FromHwnd(hwnd);
        if (source?.CompositionTarget != null)
        {
            source.CompositionTarget.BackgroundColor = System.Windows.Media.Colors.Transparent;
        }

        _lastAnchorX = anchor.X;

        // 1. Get exact Monitor Work Area & Bounds via Win32
        POINT pt = new POINT { x = anchor.X, y = anchor.Y };
        IntPtr hMonitor = MonitorFromPoint(pt, 2 /* MONITOR_DEFAULTTONEAREST */);
        MONITORINFO mi = new MONITORINFO();
        mi.cbSize = Marshal.SizeOf(typeof(MONITORINFO));
        GetMonitorInfo(hMonitor, ref mi);

        _activeMonitorWork   = mi.rcWork;
        _activeMonitorBounds = mi.rcMonitor;

        // 2. Query Monitor DPI
        _activeScaleX = 1.0;
        _activeScaleY = 1.0;
        try
        {
            if (GetDpiForMonitor(hMonitor, 0 /* MDT_EFFECTIVE_DPI */, out uint dpiX, out uint dpiY) == 0)
            {
                _activeScaleX = dpiX / 96.0;
                _activeScaleY = dpiY / 96.0;
            }
        }
        catch
        {
            var dpi = VisualTreeHelper.GetDpi(this);
            _activeScaleX = dpi.DpiScaleX > 0 ? dpi.DpiScaleX : 1.0;
            _activeScaleY = dpi.DpiScaleY > 0 ? dpi.DpiScaleY : 1.0;
        }

        // 3. Detect Taskbar Top Edge
        int taskbarTop = mi.rcWork.Bottom;

        IntPtr hTaskbar = FindWindow("Shell_TrayWnd", null);
        if (hTaskbar != IntPtr.Zero && GetWindowRect(hTaskbar, out RECT tbRect))
        {
            if (tbRect.Top > mi.rcMonitor.Top && tbRect.Top < mi.rcMonitor.Bottom)
            {
                taskbarTop = Math.Min(taskbarTop, tbRect.Top);
            }
        }

        int standardTbPx = (int)Math.Round(48 * _activeScaleY);
        if (taskbarTop >= mi.rcMonitor.Bottom - 4)
        {
            taskbarTop = mi.rcMonitor.Bottom - standardTbPx;
        }

        _detectedTaskbarTop = taskbarTop;

        // 4. Measure content size accurately from the content root element
        if (Content is UIElement contentElement)
        {
            contentElement.Measure(new System.Windows.Size(Width, double.PositiveInfinity));
        }
        UpdateLayout();

        double measuredHeight = (Content as UIElement)?.DesiredSize.Height ?? 0;
        double dipHeight = measuredHeight > 50 ? measuredHeight : (ActualHeight > 50 ? ActualHeight : 240);

        int physWidth  = (int)Math.Round(Width * _activeScaleX);
        int physHeight = (int)Math.Round(dipHeight * _activeScaleY);

        // 5. Calculate position: 12 DIPs (scaled) strictly ABOVE the taskbar top
        int gapY = (int)Math.Round(12 * _activeScaleY);
        int gapX = (int)Math.Round(12 * _activeScaleX);

        int targetY = _detectedTaskbarTop - physHeight - gapY;
        if (targetY < mi.rcWork.Top + gapY)
            targetY = mi.rcWork.Top + gapY;

        int targetX = anchor.X - physWidth / 2;
        if (targetX + physWidth > mi.rcWork.Right - gapX)
            targetX = mi.rcWork.Right - physWidth - gapX;
        if (targetX < mi.rcWork.Left + gapX)
            targetX = mi.rcWork.Left + gapX;

        // Synchronize WPF coordinates
        Left = targetX / _activeScaleX;
        Top  = targetY / _activeScaleY;

        Show();
        Activate();

        // If actual rendered height differs after Show(), recalculate targetY to prevent any gap
        if (ActualHeight > 50 && Math.Abs(ActualHeight - dipHeight) >= 1)
        {
            dipHeight = ActualHeight;
            physHeight = (int)Math.Round(dipHeight * _activeScaleY);
            targetY = _detectedTaskbarTop - physHeight - gapY;
            if (targetY < mi.rcWork.Top + gapY)
                targetY = mi.rcWork.Top + gapY;

            Left = targetX / _activeScaleX;
            Top  = targetY / _activeScaleY;
        }

        // 6. Set HWND position and topmost Z-order via Win32 directly in physical pixels
        SetWindowPos(hwnd, HWND_TOPMOST, targetX, targetY, physWidth, physHeight, SWP_SHOWWINDOW);
        SetForegroundWindow(hwnd);
    }

    private void RepositionWindow()
    {
        IntPtr hwnd = new WindowInteropHelper(this).EnsureHandle();
        double currentDipHeight = ActualHeight > 50 ? ActualHeight : ((Content as UIElement)?.DesiredSize.Height ?? DesiredSize.Height);
        if (currentDipHeight <= 0) return;

        int physWidth  = (int)Math.Round(Width * _activeScaleX);
        int physHeight = (int)Math.Round(currentDipHeight * _activeScaleY);

        int gapY = (int)Math.Round(12 * _activeScaleY);
        int gapX = (int)Math.Round(12 * _activeScaleX);

        int targetY = _detectedTaskbarTop - physHeight - gapY;
        if (targetY < _activeMonitorWork.Top + gapY)
            targetY = _activeMonitorWork.Top + gapY;

        int targetX = _lastAnchorX - physWidth / 2;
        if (targetX + physWidth > _activeMonitorWork.Right - gapX)
            targetX = _activeMonitorWork.Right - physWidth - gapX;
        if (targetX < _activeMonitorWork.Left + gapX)
            targetX = _activeMonitorWork.Left + gapX;

        Left = targetX / _activeScaleX;
        Top  = targetY / _activeScaleY;

        SetWindowPos(hwnd, HWND_TOPMOST, targetX, targetY, physWidth, physHeight, SWP_SHOWWINDOW | SWP_NOACTIVATE);
    }

    private void BuildUI()
    {
        _monitorControls.Clear();
        bool isDark = !ThemeHelper.IsLightTheme;
        var palette = ThemeHelper.GetFlyoutPalette();

        var bgCol = Color.FromArgb(palette.background.A, palette.background.R, palette.background.G, palette.background.B);
        var borderCol = Color.FromArgb(palette.border.A, palette.border.R, palette.border.G, palette.border.B);
        var sliderFillCol = Color.FromArgb(palette.sliderFill.A, palette.sliderFill.R, palette.sliderFill.G, palette.sliderFill.B);

        var primaryFg = (isDark || palette.isAccentThemed)
            ? new SolidColorBrush(Color.FromArgb(255, 255, 255, 255))
            : new SolidColorBrush(Color.FromArgb(255, 24, 24, 24));

        var secondaryFg = (isDark || palette.isAccentThemed)
            ? new SolidColorBrush(Color.FromArgb(215, 230, 230, 235))
            : new SolidColorBrush(Color.FromArgb(215, 90, 90, 95));

        var iconFg = (isDark || palette.isAccentThemed)
            ? new SolidColorBrush(Color.FromArgb(240, 245, 245, 250))
            : new SolidColorBrush(Color.FromArgb(235, 75, 75, 80));

        // Single continuous Acrylic sheet matching Windows 11 Language & Quick Settings flyouts
        // Translucent with wallpaper blur bleed-through
        var root = new Border
        {
            CornerRadius    = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            BorderBrush     = new SolidColorBrush(borderCol),
            Background      = new SolidColorBrush(bgCol),
            Padding         = new Thickness(16, 14, 16, 12)
        };

        var mainStack = new StackPanel();

        // Monitors list
        bool showContrast = SettingsManager.Current.EnableContrastSlider;
        var mons = _monitors.Monitors;

        for (int i = 0; i < mons.Count; i++)
        {
            var mon = mons[i];
            var m = mon;
            bool isLast = i == mons.Count - 1;

            var monSection = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };

            // Monitor Name Header
            var headerDock = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
            var monIcon = new TextBlock
            {
                Text              = "\uE7F4", // Monitor display glyph (Segoe Fluent Icons)
                FontFamily        = new System.Windows.Media.FontFamily(IconFontName),
                FontSize          = 14,
                Foreground        = iconFg,
                VerticalAlignment = VerticalAlignment.Center,
                Margin            = new Thickness(0, 0, 8, 0)
            };
            DockPanel.SetDock(monIcon, Dock.Left);
            headerDock.Children.Add(monIcon);

            var monTitle = new TextBlock
            {
                Text              = CleanName(mon.Name),
                FontFamily        = new System.Windows.Media.FontFamily(TextFontName),
                FontSize          = 13,
                FontWeight        = FontWeights.SemiBold,
                Foreground        = primaryFg,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming      = TextTrimming.CharacterEllipsis
            };
            headerDock.Children.Add(monTitle);
            monSection.Children.Add(headerDock);

            // Brightness Row (matching native Windows 11 Quick Settings slider)
            var brightDock = new DockPanel { Margin = new Thickness(0, 3, 0, 3) };
            var sunIcon = new TextBlock
            {
                Text              = "\uE706", // Brightness sun glyph (Segoe Fluent Icons)
                FontFamily        = new System.Windows.Media.FontFamily(IconFontName),
                FontSize          = 14,
                Foreground        = iconFg,
                VerticalAlignment = VerticalAlignment.Center,
                Margin            = new Thickness(0, 0, 10, 0)
            };
            DockPanel.SetDock(sunIcon, Dock.Left);
            brightDock.Children.Add(sunIcon);

            var valText = new TextBlock
            {
                Text              = $"{mon.BrightnessPercent}%",
                FontFamily        = new System.Windows.Media.FontFamily(TextFontName),
                FontSize          = 12,
                FontWeight        = FontWeights.SemiBold,
                Foreground        = primaryFg,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment     = TextAlignment.Right,
                Width             = 38,
                Margin            = new Thickness(10, 0, 0, 0)
            };
            DockPanel.SetDock(valText, Dock.Right);
            brightDock.Children.Add(valText);

            var bSlider = new FluentSlider(mon.BrightnessPercent, sliderFillCol, palette.isAccentThemed, isDark, pct =>
            {
                valText.Text = $"{pct}%";
                _monitors.SetBrightness(m, pct);
            });

            if (!mon.IsCommunicationSupported)
            {
                valText.Visibility = Visibility.Collapsed;
                bSlider.IsEnabled = false;
                bSlider.Opacity = 0.4;
            }

            brightDock.Children.Add(bSlider);
            monSection.Children.Add(brightDock);

            // Enable live mouse wheel scrolling across the entire monitor card
            monSection.Background = Brushes.Transparent;
            monSection.MouseWheel += (s, e) =>
            {
                if (!m.IsCommunicationSupported) return;
                int step = SettingsManager.Current.BrightnessStep;
                int delta = e.Delta > 0 ? step : -step;
                bSlider.Value += delta;
                e.Handled = true;
            };

            _monitorControls[mon.InternalName] = (bSlider, valText);

            // Contrast Row (if enabled)
            if (showContrast)
            {
                var contrastDock = new DockPanel { Margin = new Thickness(0, 6, 0, 3) };
                var cIcon = new TextBlock
                {
                    Text              = "\uE793", // Contrast glyph
                    FontFamily        = new System.Windows.Media.FontFamily(IconFontName),
                    FontSize          = 14,
                    Foreground        = iconFg,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin            = new Thickness(0, 0, 10, 0)
                };
                DockPanel.SetDock(cIcon, Dock.Left);
                contrastDock.Children.Add(cIcon);

                int cInit = mon.IsInternal ? 50 : mon.ContrastPercent;
                var cValText = new TextBlock
                {
                    Text              = $"{cInit}%",
                    FontFamily        = new System.Windows.Media.FontFamily(TextFontName),
                    FontSize          = 12,
                    FontWeight        = FontWeights.SemiBold,
                    Foreground        = primaryFg,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextAlignment     = TextAlignment.Right,
                    Width             = 38,
                    Margin            = new Thickness(10, 0, 0, 0)
                };
                DockPanel.SetDock(cValText, Dock.Right);
                contrastDock.Children.Add(cValText);

                var cSlider = new FluentSlider(cInit, sliderFillCol, palette.isAccentThemed, isDark, pct =>
                {
                    cValText.Text = $"{pct}%";
                    if (!m.IsInternal) _monitors.SetContrast(m, pct);
                });
                contrastDock.Background = Brushes.Transparent;
                contrastDock.MouseWheel += (s, e) =>
                {
                    int step = SettingsManager.Current.BrightnessStep;
                    int delta = e.Delta > 0 ? step : -step;
                    cSlider.Value += delta;
                    e.Handled = true;
                };
                contrastDock.Children.Add(cSlider);
                monSection.Children.Add(contrastDock);
            }

            mainStack.Children.Add(monSection);

            // Subtle divider between multiple monitors
            if (!isLast)
            {
                var divider = new Border
                {
                    Height     = 1,
                    Background = palette.isAccentThemed
                        ? new SolidColorBrush(Color.FromArgb(32, 255, 255, 255))
                        : (isDark
                            ? new SolidColorBrush(Color.FromArgb(18, 255, 255, 255))
                            : new SolidColorBrush(Color.FromArgb(14, 0, 0, 0))),
                    Margin     = new Thickness(0, 4, 0, 10)
                };
                mainStack.Children.Add(divider);
            }
        }

        // Footer divider
        var footDivider = new Border
        {
            Height     = 1,
            Background = palette.isAccentThemed
                ? new SolidColorBrush(Color.FromArgb(32, 255, 255, 255))
                : (isDark
                    ? new SolidColorBrush(Color.FromArgb(18, 255, 255, 255))
                    : new SolidColorBrush(Color.FromArgb(14, 0, 0, 0))),
            Margin     = new Thickness(0, 4, 0, 8)
        };
        mainStack.Children.Add(footDivider);

        // Footer: LiteBright brand name and Settings button with hover pill
        var footerGrid = new Grid();
        footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var brandText = new TextBlock
        {
            Text              = "LiteBright",
            FontFamily        = new System.Windows.Media.FontFamily(TextFontName),
            FontSize          = 11.5,
            FontWeight        = FontWeights.SemiBold,
            Foreground        = secondaryFg,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(brandText, 0);
        footerGrid.Children.Add(brandText);

        var gearBtn = new Border
        {
            Width             = 28,
            Height            = 28,
            CornerRadius      = new CornerRadius(5),
            Background        = Brushes.Transparent,
            Cursor            = Cursors.Hand,
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip           = LocalizationManager.T("GENERIC_SETTINGS", "Settings")
        };

        var gearIcon = new TextBlock
        {
            Text                = "\uE713", // Settings gear glyph (Segoe Fluent Icons)
            FontFamily          = new System.Windows.Media.FontFamily(IconFontName),
            FontSize            = 13,
            Foreground          = secondaryFg,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Center
        };
        gearBtn.Child = gearIcon;

        gearBtn.MouseEnter += (_, _) =>
        {
            gearBtn.Background = palette.isAccentThemed
                ? new SolidColorBrush(Color.FromArgb(45, 255, 255, 255))
                : (isDark
                    ? new SolidColorBrush(Color.FromArgb(32, 255, 255, 255))
                    : new SolidColorBrush(Color.FromArgb(18, 0, 0, 0)));
            gearIcon.Foreground = primaryFg;
        };

        gearBtn.MouseLeave += (_, _) =>
        {
            gearBtn.Background = Brushes.Transparent;
            gearIcon.Foreground = secondaryFg;
        };

        gearBtn.MouseLeftButtonUp += (_, _) =>
        {
            Hide();
            SettingsRequested?.Invoke();
        };

        Grid.SetColumn(gearBtn, 1);
        footerGrid.Children.Add(gearBtn);

        mainStack.Children.Add(footerGrid);

        // Fallback: scrolling anywhere on the panel background adjusts the primary active display
        root.MouseWheel += (s, e) =>
        {
            if (e.Handled) return;
            var targetMon = mons.FirstOrDefault(x => x.IsCommunicationSupported);
            if (targetMon != null && _monitorControls.TryGetValue(targetMon.InternalName, out var ctrl))
            {
                int step = SettingsManager.Current.BrightnessStep;
                int delta = e.Delta > 0 ? step : -step;
                ctrl.bSlider.Value += delta;
                e.Handled = true;
            }
        };

        root.Child = mainStack;
        Content = root;
    }

    private static string CleanName(string s)
    {
        int p = s.IndexOf('(');
        return p > 0 ? s[..p].Trim() : s.Trim();
    }

    /// <summary>
    /// Native Windows 11 Fluent slider matching the Quick Settings volume/brightness control:
    /// 4px slender track, 18px circular thumb with dark border and soft drop shadow.
    /// </summary>
    internal sealed class FluentSlider : Canvas
    {
        private int _value;
        private bool _isDragging;
        private readonly Action<int> _onValueChanged;
        private readonly Border _trackBg;
        private readonly Border _trackFill;
        private readonly Border _thumb;

        const int TrackH       = 4;  // Windows 11 slider track is precisely 4px
        const double ThumbSize = 18; // Windows 11 circular thumb is 18px
        const double HalfThumb = ThumbSize / 2.0;

        public int Value
        {
            get => _value;
            set
            {
                int clamped = Math.Clamp(value, 0, 100);
                if (clamped == _value) return;
                _value = clamped;
                UpdateVisuals();
                _onValueChanged?.Invoke(_value);
            }
        }

        public void SetValueDirect(int value)
        {
            int clamped = Math.Clamp(value, 0, 100);
            if (clamped == _value) return;
            _value = clamped;
            UpdateVisuals();
        }

        public FluentSlider(int initialValue, Color fillColor, bool isAccentThemed, bool isDark, Action<int> onValueChanged)
        {
            _value = Math.Clamp(initialValue, 0, 100);
            _onValueChanged = onValueChanged;
            Height = 24; // Compact, perfectly centered hit-target
            Cursor = Cursors.Hand;
            ClipToBounds = false;

            Brush fillBrush;
            Brush trackBgBrush;
            Brush thumbBorderBrush;

            if (isAccentThemed)
            {
                // High contrast white controls over the accent-colored acrylic surface
                fillBrush        = Brushes.White;
                trackBgBrush     = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255));
                thumbBorderBrush = new SolidColorBrush(Color.FromArgb(80, 0, 0, 0));
            }
            else
            {
                fillBrush        = new SolidColorBrush(fillColor);
                trackBgBrush     = isDark
                    ? new SolidColorBrush(Color.FromArgb(50, 255, 255, 255))
                    : new SolidColorBrush(Color.FromArgb(35, 0, 0, 0));
                thumbBorderBrush = new SolidColorBrush(Color.FromArgb(65, 0, 0, 0));
            }

            _trackBg = new Border
            {
                Height       = TrackH,
                CornerRadius = new CornerRadius(TrackH / 2.0),
                Background   = trackBgBrush
            };

            _trackFill = new Border
            {
                Height       = TrackH,
                CornerRadius = new CornerRadius(TrackH / 2.0),
                Background   = fillBrush
            };

            // 18px circle matching native Windows 11 volume/brightness thumb
            _thumb = new Border
            {
                Width                 = ThumbSize,
                Height                = ThumbSize,
                CornerRadius          = new CornerRadius(HalfThumb),
                Background            = Brushes.White,
                BorderBrush           = thumbBorderBrush,
                BorderThickness       = new Thickness(1.5),
                RenderTransformOrigin = new System.Windows.Point(0.5, 0.5),
                Effect                = new DropShadowEffect
                {
                    Color       = Color.FromArgb(70, 0, 0, 0),
                    BlurRadius  = 4,
                    ShadowDepth = 1,
                    Direction   = 270
                }
            };

            Children.Add(_trackBg);
            Children.Add(_trackFill);
            Children.Add(_thumb);

            Loaded += (_, _) => UpdateVisuals();
            SizeChanged += (_, _) => UpdateVisuals();

            MouseLeftButtonDown += (s, e) =>
            {
                _isDragging = true;
                CaptureMouse();
                _thumb.RenderTransform = new ScaleTransform(1.12, 1.12);
                SetFromPosition(e.GetPosition(this).X);
            };

            MouseMove += (s, e) =>
            {
                if (_isDragging) SetFromPosition(e.GetPosition(this).X);
            };

            MouseLeftButtonUp += (s, e) =>
            {
                if (_isDragging)
                {
                    _isDragging = false;
                    ReleaseMouseCapture();
                    _thumb.RenderTransform = Transform.Identity;
                }
            };

            MouseWheel += (s, e) =>
            {
                int step = SettingsManager.Current.BrightnessStep;
                int delta = e.Delta > 0 ? step : -step;
                Value += delta;
                e.Handled = true;
            };

            MouseEnter += (_, _) =>
            {
                _thumb.RenderTransform = new ScaleTransform(1.11, 1.11);
            };

            MouseLeave += (_, _) =>
            {
                if (!_isDragging)
                    _thumb.RenderTransform = Transform.Identity;
            };
        }

        private void SetFromPosition(double x)
        {
            double availableWidth = ActualWidth - ThumbSize;
            if (availableWidth <= 0) return;
            double p = Math.Clamp((x - HalfThumb) / availableWidth, 0.0, 1.0);
            int newVal = (int)Math.Round(p * 100.0);
            if (newVal != _value)
            {
                _value = newVal;
                UpdateVisuals();
                _onValueChanged(_value);
            }
        }

        private void UpdateVisuals()
        {
            double w = ActualWidth;
            if (w <= 0) return;

            double trackY = (ActualHeight - TrackH) / 2.0;
            double availableWidth = Math.Max(0, w - ThumbSize);
            double fillWidth = (_value / 100.0) * availableWidth;
            double thumbX = fillWidth;
            double thumbY = (ActualHeight - ThumbSize) / 2.0;

            SetLeft(_trackBg, HalfThumb);
            SetTop(_trackBg, trackY);
            _trackBg.Width = availableWidth;

            SetLeft(_trackFill, HalfThumb);
            SetTop(_trackFill, trackY);
            _trackFill.Width = fillWidth;

            SetLeft(_thumb, thumbX);
            SetTop(_thumb, thumbY);
        }
    }
}