using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using BrightnessController.Helpers;
using BrightnessController.Native;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using Cursors = System.Windows.Input.Cursors;
using FontFamily = System.Windows.Media.FontFamily;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using VerticalAlignment = System.Windows.VerticalAlignment;

namespace BrightnessController.UI;

/// <summary>
/// Modern Windows 11 / Twinkle Tray styled tray context menu flyout.
/// Features rounded corners, subtle drop shadow, hover pill highlights,
/// and localized actions (Refresh displays, Settings, Quit).
/// </summary>
public sealed class TrayContextMenu : Window
{
    private readonly Action _onRefreshDisplays;
    private readonly Action _onOpenSettings;
    private readonly Action _onQuit;

    private readonly Border _outerCard;
    private readonly StackPanel _itemsStack;

    public TrayContextMenu(
        Action onRefreshDisplays,
        Action onOpenSettings,
        Action onQuit)
    {
        _onRefreshDisplays = onRefreshDisplays;
        _onOpenSettings    = onOpenSettings;
        _onQuit            = onQuit;

        WindowStyle           = WindowStyle.None;
        ResizeMode            = ResizeMode.NoResize;
        ShowInTaskbar         = false;
        Topmost               = true;
        AllowsTransparency    = true;
        Background            = Brushes.Transparent;
        SizeToContent         = SizeToContent.WidthAndHeight;
        WindowStartupLocation = WindowStartupLocation.Manual;

        _outerCard = new Border
        {
            Margin       = new Thickness(10), // Padding for soft drop shadow
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            MinWidth     = 160,
            Padding      = new Thickness(4, 5, 4, 5)
        };

        _itemsStack = new StackPanel();
        _outerCard.Child = _itemsStack;
        Content = _outerCard;

        Deactivated += (_, _) => Hide();
    }

    public void BuildUI()
    {
        bool isDark = !ThemeHelper.IsLightTheme;

        _outerCard.Background = isDark
            ? new SolidColorBrush(Color.FromArgb(255, 36, 36, 39))  // #242427
            : new SolidColorBrush(Color.FromArgb(255, 250, 250, 252));

        _outerCard.BorderBrush = isDark
            ? new SolidColorBrush(Color.FromArgb(255, 58, 58, 62))  // #3A3A3E
            : new SolidColorBrush(Color.FromArgb(255, 222, 222, 226));

        _outerCard.Effect = new DropShadowEffect
        {
            BlurRadius   = 16,
            ShadowDepth  = 4,
            Direction    = 270,
            Color        = Colors.Black,
            Opacity      = isDark ? 0.55 : 0.18
        };

        _itemsStack.Children.Clear();

        // 1. Refresh displays
        string refreshText = LocalizationManager.T("GENERIC_REFRESH_DISPLAYS", "Refresh displays");
        _itemsStack.Children.Add(CreateMenuItem(refreshText, _onRefreshDisplays, isDark));

        // 2. Settings
        string settingsText = LocalizationManager.T("SETTINGS_NAV_SETTINGS", "Settings");
        _itemsStack.Children.Add(CreateMenuItem(settingsText, _onOpenSettings, isDark));

        // 3. Separator
        _itemsStack.Children.Add(CreateSeparator(isDark));

        // 4. Quit
        string quitText = LocalizationManager.T("GENERIC_QUIT", "Quit");
        _itemsStack.Children.Add(CreateMenuItem(quitText, _onQuit, isDark));
    }

    private Border CreateMenuItem(string text, Action onClick, bool isDark)
    {
        var itemBorder = new Border
        {
            Height       = 32,
            CornerRadius = new CornerRadius(5),
            Margin       = new Thickness(1, 1, 1, 1),
            Background   = Brushes.Transparent,
            Cursor       = Cursors.Hand
        };

        var textBlock = new TextBlock
        {
            Text              = text,
            FontFamily        = new FontFamily(BrightnessPanel.TextFontName),
            FontSize          = 12.5,
            Foreground        = isDark ? Brushes.White : new SolidColorBrush(Color.FromArgb(255, 25, 25, 25)),
            VerticalAlignment = VerticalAlignment.Center,
            Margin            = new Thickness(12, 0, 16, 0)
        };
        itemBorder.Child = textBlock;

        var hoverBrush = isDark
            ? new SolidColorBrush(Color.FromArgb(28, 255, 255, 255))
            : new SolidColorBrush(Color.FromArgb(16, 0, 0, 0));

        var pressedBrush = isDark
            ? new SolidColorBrush(Color.FromArgb(20, 255, 255, 255))
            : new SolidColorBrush(Color.FromArgb(24, 0, 0, 0));

        itemBorder.MouseEnter += (_, _) => itemBorder.Background = hoverBrush;
        itemBorder.MouseLeave += (_, _) => itemBorder.Background = Brushes.Transparent;
        itemBorder.MouseLeftButtonDown += (_, _) => itemBorder.Background = pressedBrush;
        itemBorder.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            Hide();
            onClick();
        };

        return itemBorder;
    }

    private static Border CreateSeparator(bool isDark)
    {
        return new Border
        {
            Height     = 1,
            Background = isDark
                ? new SolidColorBrush(Color.FromArgb(35, 255, 255, 255))
                : new SolidColorBrush(Color.FromArgb(20, 0, 0, 0)),
            Margin     = new Thickness(6, 4, 6, 4)
        };
    }

    public void ShowAt(System.Drawing.Point clickPt)
    {
        if (IsVisible)
        {
            Hide();
            return;
        }

        BuildUI();
        UpdateLayout();
        Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));

        double menuW = DesiredSize.Width > 0 ? DesiredSize.Width : 180;
        double menuH = DesiredSize.Height > 0 ? DesiredSize.Height : 140;

        var screen = System.Windows.Forms.Screen.FromPoint(clickPt);
        var work = screen.WorkingArea;

        var dpi = VisualTreeHelper.GetDpi(this);
        double dpiX = dpi.DpiScaleX > 0 ? dpi.DpiScaleX : 1.0;
        double dpiY = dpi.DpiScaleY > 0 ? dpi.DpiScaleY : 1.0;

        double clickX = clickPt.X / dpiX;
        double clickY = clickPt.Y / dpiY;

        double workLeft   = work.Left / dpiX;
        double workTop    = work.Top / dpiY;
        double workRight  = work.Right / dpiX;
        double workBottom = work.Bottom / dpiY;

        // Position horizontally: align right edge with cursor or center, clamp within bounds
        double left = clickX - menuW + 20;
        if (left < workLeft + 8) left = workLeft + 8;
        if (left + menuW > workRight - 8) left = workRight - menuW - 8;

        // Position vertically: above cursor by default, or below if near top
        double top = clickY - menuH + 4;
        if (top < workTop + 8)
        {
            top = clickY + 8;
        }
        if (top + menuH > workBottom - 8)
        {
            top = workBottom - menuH - 8;
        }

        Left = left;
        Top  = top;

        Show();
        Activate();
        Focus();

        IntPtr hwnd = new WindowInteropHelper(this).Handle;
        NativeMethods.SetForegroundWindow(hwnd);
    }
}
