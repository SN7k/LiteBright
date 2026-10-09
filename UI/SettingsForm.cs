using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shell;
using BrightnessController.Helpers;
using BrightnessController.Hotkeys;
using BrightnessController.Monitors;
using BrightnessController.Native;
using BrightnessController.Settings;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using ContextMenu = System.Windows.Controls.ContextMenu;
using Cursors = System.Windows.Input.Cursors;
using FontFamily = System.Windows.Media.FontFamily;
using MenuItem = System.Windows.Controls.MenuItem;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Image = System.Windows.Controls.Image;
using Keys = System.Windows.Forms.Keys;
using Orientation = System.Windows.Controls.Orientation;
using VerticalAlignment = System.Windows.VerticalAlignment;
using Colors = System.Windows.Media.Colors;
using DropShadowEffect = System.Windows.Media.Effects.DropShadowEffect;
using PlacementMode = System.Windows.Controls.Primitives.PlacementMode;
using Popup = System.Windows.Controls.Primitives.Popup;
using PopupAnimation = System.Windows.Controls.Primitives.PopupAnimation;
using ScrollBar = System.Windows.Controls.Primitives.ScrollBar;
using ScrollViewer = System.Windows.Controls.ScrollViewer;

namespace BrightnessController.UI;

/// <summary>
/// Modern Twinkle Tray / Windows Settings style navigation window.
/// Features a left navigation sidebar (General, Monitor Settings, DDC/CI Features,
/// Time Adjustments, Hotkeys & Shortcuts, Updates), custom seamless header,
/// and an ultra-slim Fluent scrollbar.
/// </summary>
public sealed class SettingsForm : Window
{
    public event Action<AppSettings>? SettingsSaved;
    public event EventHandler? FormClosed
    {
        add => Closed += value;
        remove => Closed -= value;
    }

    private static SettingsForm? _currentInstance;
    private bool _isClosed;
    public bool IsDisposed => _isClosed;

    private readonly MonitorManager _monitorManager;
    private readonly AppSettings _draft;
    public const string KofiUrl = "https://ko-fi.com/snkdevworks";
    public const string BuyMeACoffeeUrl = "https://buymeacoffee.com/sn7k";
    public const string PayPalUrl = "https://paypal.me/ShombhuKaran";
    public const string DonationUrl = KofiUrl;
    private readonly List<HotkeyBinder> _hotkeyBinders = new();

    private ContentControl _pageContainer = null!;
    private readonly List<SidebarNavItem> _navItems = new();
    private int _selectedPageIndex = 0;

    private Grid _rootGrid = null!;
    private Grid _dropdownOverlay = null!;
    private FrameworkElement? _currentDropdownAnchor;

    public SettingsForm(MonitorManager monitorManager)
    {
        _currentInstance = this;
        _monitorManager = monitorManager;
        _draft = Clone(SettingsManager.Current);

        Title                  = "LiteBright Settings";
        Width                  = 780;
        Height                 = 560;
        MinWidth               = 780;
        MaxWidth               = 780;
        MinHeight              = 560;
        MaxHeight              = 560;
        WindowStartupLocation  = WindowStartupLocation.CenterScreen;
        WindowStyle            = WindowStyle.None;
        ResizeMode             = ResizeMode.NoResize;
        Icon                   = GetAppIconSource();

        // Enable Windows 11 DWM rounded corners and shadows
        WindowChrome.SetWindowChrome(this, new WindowChrome
        {
            CaptionHeight         = 0,
            ResizeBorderThickness = new Thickness(0),
            CornerRadius          = new CornerRadius(8),
            GlassFrameThickness   = new Thickness(-1),
            UseAeroCaptionButtons = false
        });

        Closed += (_, _) =>
        {
            _isClosed = true;
            if (_currentInstance == this) _currentInstance = null;
        };

        BuildUI();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        ApplyDwmAttributes();
    }

    private void ApplyDwmAttributes()
    {
        IntPtr hwnd = new WindowInteropHelper(this).EnsureHandle();
        bool isDark = !ThemeHelper.IsLightTheme;
        int dark = isDark ? 1 : 0;
        NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));

        int corner = NativeMethods.DWMWCP_ROUND;
        NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE, ref corner, sizeof(int));

        int mica = NativeMethods.DWMSBT_NONE;
        NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_SYSTEMBACKDROP_TYPE, ref mica, sizeof(int));

        int borderColor = 0x001B1919; // COLORREF 0x00BBGGRR matching #19191B
        NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_BORDER_COLOR, ref borderColor, sizeof(int));
    }

    private void BuildUI()
    {
        bool isDark = !ThemeHelper.IsLightTheme;
        var sysAccent = ThemeHelper.AccentColor;
        var accentCol = Color.FromArgb(255, sysAccent.R, sysAccent.G, sysAccent.B);
        var accentBrush = new SolidColorBrush(accentCol);

        var bgBrush = isDark
            ? new SolidColorBrush(Color.FromArgb(255, 25, 25, 27))  // #19191B
            : new SolidColorBrush(Color.FromArgb(255, 243, 243, 245));

        Background = bgBrush;

        var sidebarBg = isDark
            ? new SolidColorBrush(Color.FromArgb(255, 25, 25, 27))  // #19191B
            : new SolidColorBrush(Color.FromArgb(255, 236, 236, 239));

        var borderBrush = isDark
            ? new SolidColorBrush(Color.FromArgb(12, 255, 255, 255))
            : new SolidColorBrush(Color.FromArgb(16, 0, 0, 0));

        var primaryFg = isDark
            ? new SolidColorBrush(Color.FromArgb(255, 255, 255, 255))
            : new SolidColorBrush(Color.FromArgb(255, 26, 26, 26));

        var secondaryFg = isDark
            ? new SolidColorBrush(Color.FromArgb(200, 160, 160, 165))
            : new SolidColorBrush(Color.FromArgb(200, 96, 96, 100));

        string textFont = BrightnessPanel.TextFontName;
        string iconFont = BrightnessPanel.IconFontName;

        var rootGrid = new Grid();
        rootGrid.ClipToBounds = true;
        _rootGrid = rootGrid;
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });           // Custom Header
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Body

        // ── 1. Custom Titlebar Header (Seamless, logo on left, close on right) ──
        var titleBar = new Border
        {
            Height          = 36,
            Background      = Brushes.Transparent,
            BorderThickness = new Thickness(0)
        };

        titleBar.MouseLeftButtonDown += (_, e) =>
        {
            if (e.ButtonState == MouseButtonState.Pressed) DragMove();
        };

        var titleBarDock = new DockPanel();

        // Close button on right
        var closeBtn = new Border
        {
            Width        = 46,
            Height       = 36,
            Background   = Brushes.Transparent,
            Cursor       = Cursors.Hand
        };
        var closeGlyph = new TextBlock
        {
            Text              = "\uE711",
            FontFamily        = new FontFamily(iconFont),
            FontSize          = 10,
            Foreground        = secondaryFg,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Center
        };
        closeBtn.Child = closeGlyph;

        closeBtn.MouseEnter += (_, _) =>
        {
            closeBtn.Background   = new SolidColorBrush(Color.FromArgb(255, 232, 17, 35));
            closeGlyph.Foreground = Brushes.White;
        };
        closeBtn.MouseLeave += (_, _) =>
        {
            closeBtn.Background   = Brushes.Transparent;
            closeGlyph.Foreground = secondaryFg;
        };
        closeBtn.MouseLeftButtonDown += (_, e) => e.Handled = true;
        closeBtn.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            Close();
        };

        DockPanel.SetDock(closeBtn, Dock.Right);
        titleBarDock.Children.Add(closeBtn);

        // App Logo + Title on left
        var titleLeftStack = new StackPanel
        {
            Orientation       = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Margin            = new Thickness(14, 0, 0, 0)
        };

        var logoSource = GetAppIconSource();
        if (logoSource != null)
        {
            var logoImg = new Image
            {
                Source              = logoSource,
                Width               = 16,
                Height              = 16,
                VerticalAlignment   = VerticalAlignment.Center,
                Margin              = new Thickness(0, 0, 8, 0),
                SnapsToDevicePixels = true
            };
            RenderOptions.SetBitmapScalingMode(logoImg, BitmapScalingMode.HighQuality);
            titleLeftStack.Children.Add(logoImg);
        }

        var titleText = new TextBlock
        {
            Text              = "LiteBright Settings",
            FontFamily        = new FontFamily(textFont),
            FontSize          = 12.5,
            FontWeight        = FontWeights.SemiBold,
            Foreground        = primaryFg,
            VerticalAlignment = VerticalAlignment.Center
        };
        titleLeftStack.Children.Add(titleText);

        titleBarDock.Children.Add(titleLeftStack);
        titleBar.Child = titleBarDock;

        Grid.SetRow(titleBar, 0);
        rootGrid.Children.Add(titleBar);

        // ── 2. Main Body Grid (Sidebar on left, Content on right) ───────────────
        var bodyGrid = new Grid();
        bodyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(205, GridUnitType.Pixel) });
        bodyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // ── Sidebar ──
        var sidebarBorder = new Border
        {
            Background      = sidebarBg,
            BorderThickness = new Thickness(0)
        };

        var sidebarStack = new StackPanel { Margin = new Thickness(0, 10, 0, 10) };

        _navItems.Clear();

        AddNavItem(sidebarStack, 0, "\uE713", LocalizationManager.T("SETTINGS_SIDEBAR_GENERAL", "General"), isDark, accentCol, textFont, iconFont);
        AddNavItem(sidebarStack, 1, "\uE7F4", LocalizationManager.T("SETTINGS_SIDEBAR_MONITORS", "Monitor Settings"), isDark, accentCol, textFont, iconFont);
        AddNavItem(sidebarStack, 2, "\uE9E9", LocalizationManager.T("SETTINGS_SIDEBAR_FEATURES", "DDC/CI Features"), isDark, accentCol, textFont, iconFont);
        AddNavItem(sidebarStack, 3, "\uE92C", LocalizationManager.T("SETTINGS_SIDEBAR_HOTKEYS", "Hotkeys & Shortcuts"), isDark, accentCol, textFont, iconFont);

        var sidebarGrid = new Grid();
        sidebarGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        sidebarGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        Grid.SetRow(sidebarStack, 0);
        sidebarGrid.Children.Add(sidebarStack);

        // ── Donate Button pinned to bottom of sidebar ──
        var donateBtn = new Border
        {
            Height          = 36,
            CornerRadius    = new CornerRadius(5),
            Background      = isDark
                ? new SolidColorBrush(Color.FromArgb(18, 255, 255, 255))
                : new SolidColorBrush(Color.FromArgb(12, 0, 0, 0)),
            BorderBrush     = isDark
                ? new SolidColorBrush(Color.FromArgb(32, 255, 255, 255))
                : new SolidColorBrush(Color.FromArgb(24, 0, 0, 0)),
            BorderThickness = new Thickness(1),
            Cursor          = Cursors.Hand,
            Margin          = new Thickness(8, 0, 8, 12)
        };

        var donateDock = new DockPanel { LastChildFill = true, Margin = new Thickness(12, 0, 10, 0) };

        var heartIcon = new TextBlock
        {
            Text              = "\uEB52", // Segoe Fluent Icons / MDL2 Assets Heart
            FontFamily        = new FontFamily(iconFont),
            FontSize          = 14,
            Foreground        = new SolidColorBrush(Color.FromArgb(255, 255, 75, 110)), // Heart pink
            VerticalAlignment = VerticalAlignment.Center,
            Margin            = new Thickness(0, 0, 10, 0)
        };
        DockPanel.SetDock(heartIcon, Dock.Left);
        donateDock.Children.Add(heartIcon);

        var donateChevron = new TextBlock
        {
            Text              = "\uE70E", // ChevronUp indicating upward menu popup
            FontFamily        = new FontFamily(iconFont),
            FontSize          = 9,
            Foreground        = isDark
                ? new SolidColorBrush(Color.FromArgb(140, 255, 255, 255))
                : new SolidColorBrush(Color.FromArgb(140, 0, 0, 0)),
            VerticalAlignment = VerticalAlignment.Center,
            Margin            = new Thickness(4, 0, 0, 0)
        };
        DockPanel.SetDock(donateChevron, Dock.Right);
        donateDock.Children.Add(donateChevron);

        var donateText = new TextBlock
        {
            Text              = LocalizationManager.T("SETTINGS_DONATE", "Donate"),
            FontFamily        = new FontFamily(textFont),
            FontSize          = 13,
            FontWeight        = FontWeights.SemiBold,
            Foreground        = isDark ? Brushes.White : new SolidColorBrush(Color.FromArgb(255, 25, 25, 25)),
            VerticalAlignment = VerticalAlignment.Center
        };
        donateDock.Children.Add(donateText);
        donateBtn.Child = donateDock;

        donateBtn.MouseEnter += (_, _) =>
        {
            donateBtn.Background = isDark
                ? new SolidColorBrush(Color.FromArgb(36, 255, 255, 255))
                : new SolidColorBrush(Color.FromArgb(22, 0, 0, 0));
        };
        donateBtn.MouseLeave += (_, _) =>
        {
            donateBtn.Background = isDark
                ? new SolidColorBrush(Color.FromArgb(18, 255, 255, 255))
                : new SolidColorBrush(Color.FromArgb(12, 0, 0, 0));
        };
        donateBtn.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            ShowDonateMenu(donateBtn, isDark, textFont, iconFont);
        };

        Grid.SetRow(donateBtn, 1);
        sidebarGrid.Children.Add(donateBtn);

        sidebarBorder.Child = sidebarGrid;
        Grid.SetColumn(sidebarBorder, 0);
        bodyGrid.Children.Add(sidebarBorder);

        // ── Right Content Area (Full height, instant auto-save) ──
        _pageContainer = new ContentControl();
        Grid.SetColumn(_pageContainer, 1);
        bodyGrid.Children.Add(_pageContainer);

        Grid.SetRow(bodyGrid, 1);
        rootGrid.Children.Add(bodyGrid);

        // ── In-window dropdown overlay layer (RowSpan = 2, ZIndex = 9999) ──
        _dropdownOverlay = new Grid
        {
            Visibility          = Visibility.Collapsed,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment   = VerticalAlignment.Stretch
        };
        Grid.SetRow(_dropdownOverlay, 0);
        Grid.SetRowSpan(_dropdownOverlay, 2);
        System.Windows.Controls.Panel.SetZIndex(_dropdownOverlay, 9999);
        rootGrid.Children.Add(_dropdownOverlay);

        Content = rootGrid;

        SelectPage(_selectedPageIndex);
    }

    private void AddNavItem(StackPanel parent, int index, string glyph, string title,
        bool isDark, Color accentColor, string textFont, string iconFont)
    {
        var item = new SidebarNavItem(index, glyph, title, isDark, accentColor, textFont, iconFont, SelectPage);
        _navItems.Add(item);
        parent.Children.Add(item.Element);
    }

    public void SelectPage(int index)
    {
        CloseDropdownOverlay();
        _selectedPageIndex = index;
        foreach (var item in _navItems)
        {
            item.SetSelected(item.Index == index);
        }

        bool isDark = !ThemeHelper.IsLightTheme;
        var sysAccent = ThemeHelper.AccentColor;
        var accentCol = Color.FromArgb(255, sysAccent.R, sysAccent.G, sysAccent.B);

        UIElement page = index switch
        {
            0 => BuildGeneralPage(isDark, accentCol),
            1 => BuildMonitorSettingsPage(isDark, accentCol),
            2 => BuildDdcCiPage(isDark, accentCol),
            3 => BuildHotkeysPage(isDark, accentCol),
            _ => BuildGeneralPage(isDark, accentCol)
        };

        _pageContainer.Content = page;
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Page 1: General (Launch at startup, Apply brightness at startup, Language, Theme, Auto brightness, version)
    // ─────────────────────────────────────────────────────────────────────────────
    private UIElement BuildGeneralPage(bool isDark, Color accentCol)
    {
        var (scroll, stack) = CreatePageContainer(LocalizationManager.T("SETTINGS_GENERAL_TITLE", "General"), isDark);
        var cardBg = GetCardBg(isDark);
        var cardBorder = GetCardBorder(isDark);
        var divider = GetDivider(isDark);
        var primaryFg = GetPrimaryFg(isDark);
        var secondaryFg = GetSecondaryFg(isDark);
        string textFont = BrightnessPanel.TextFontName;
        string iconFont = BrightnessPanel.IconFontName;

        // Card 1: Startup & Brightness Behaviors
        var card1 = CreateCard(cardBg, cardBorder);
        var card1Stack = new StackPanel();

        var toggleStartup = new FluentToggleSwitch(_draft.StartWithWindows, accentCol, isDark, v => { _draft.StartWithWindows = v; AutoSave(); });
        card1Stack.Children.Add(MakeSettingRow(
            "\uE770", LocalizationManager.T("SETTINGS_GENERAL_STARTUP", "Launch at startup"),
            LocalizationManager.T("SETTINGS_GENERAL_STARTUP_DESC", "Launch LiteBright automatically when you sign into Windows"),
            toggleStartup, textFont, iconFont, primaryFg, secondaryFg));

        card1Stack.Children.Add(MakeDivider(divider));

        var toggleApplyStartup = new FluentToggleSwitch(_draft.ApplyBrightnessAtStartup, accentCol, isDark, v => { _draft.ApplyBrightnessAtStartup = v; AutoSave(); });
        card1Stack.Children.Add(MakeSettingRow(
            "\uE895", LocalizationManager.T("SETTINGS_GENERAL_BRIGHTNESS_STARTUP_TITLE", "Apply brightness at startup"),
            LocalizationManager.T("SETTINGS_GENERAL_BRIGHTNESS_STARTUP_DESC", "Restore the last known brightness for each display when LiteBright starts"),
            toggleApplyStartup, textFont, iconFont, primaryFg, secondaryFg));

        card1Stack.Children.Add(MakeDivider(divider));

        var toggleAuto = new FluentToggleSwitch(_draft.AutoBrightness, accentCol, isDark, v => { _draft.AutoBrightness = v; AutoSave(); });
        card1Stack.Children.Add(MakeSettingRow(
            "\uE706", LocalizationManager.T("SETTINGS_GENERAL_AUTOBRIGHT_TITLE", "Auto brightness"),
            LocalizationManager.T("SETTINGS_GENERAL_AUTOBRIGHT_DESC", "Automatically adjust brightness based on time or ambient light"),
            toggleAuto, textFont, iconFont, primaryFg, secondaryFg));

        card1.Child = card1Stack;
        stack.Children.Add(card1);

        // Card 2: Appearance & Preferences
        var card2 = CreateCard(cardBg, cardBorder);
        var card2Stack = new StackPanel();

        string selectedLangDisplay = LocalizationManager.GetLanguageDisplayName(_draft.Language);
        var langCombo = new FluentComboBox(
            LocalizationManager.AllWorldLanguages,
            selectedLangDisplay,
            isDark,
            accentCol,
            v =>
            {
                _draft.Language = v;
                AutoSave();
                LocalizationManager.SetLanguage(v);
                BuildUI();
            },
            controlWidth: 240);
        card2Stack.Children.Add(MakeSettingRow(
            "\uE774", LocalizationManager.T("SETTINGS_GENERAL_LANGUAGE_TITLE", "Language"),
            LocalizationManager.T("SETTINGS_GENERAL_LANGUAGE_DESC", "Select application display language"),
            langCombo, textFont, iconFont, primaryFg, secondaryFg));

        card2Stack.Children.Add(MakeDivider(divider));

        string themeSys = LocalizationManager.T("SETTINGS_GENERAL_THEME_SYSTEM", "System preferences (default)");
        string themeDark = LocalizationManager.T("SETTINGS_GENERAL_THEME_DARK", "Dark");
        string themeLight = LocalizationManager.T("SETTINGS_GENERAL_THEME_LIGHT", "Light");

        string currentThemeDisplay = _draft.Theme switch
        {
            "Dark" => themeDark,
            "Light" => themeLight,
            _ => themeSys
        };

        var themeCombo = new FluentComboBox(
            new[] { themeSys, themeDark, themeLight },
            currentThemeDisplay,
            isDark,
            accentCol,
            v =>
            {
                if (v == themeDark) _draft.Theme = "Dark";
                else if (v == themeLight) _draft.Theme = "Light";
                else _draft.Theme = "System preferences (default)";
                AutoSave();
                ThemeHelper.NotifyThemeChanged();
                ApplyDwmAttributes();
                BuildUI();
            },
            controlWidth: 240);
        card2Stack.Children.Add(MakeSettingRow(
            "\uE790", LocalizationManager.T("SETTINGS_GENERAL_THEME_TITLE", "Theme"),
            LocalizationManager.T("SETTINGS_GENERAL_THEME_DESC", "Choose between dark mode, light mode, or system default"),
            themeCombo, textFont, iconFont, primaryFg, secondaryFg));

        card2.Child = card2Stack;
        stack.Children.Add(card2);

        // Card 3: Version Info
        var card3 = CreateCard(cardBg, cardBorder);
        var card3Dock = new DockPanel { Margin = new Thickness(16, 12, 16, 12) };

        var infoIcon = new TextBlock
        {
            Text              = "\uE946",
            FontFamily        = new FontFamily(iconFont),
            FontSize          = 18,
            Foreground        = new SolidColorBrush(accentCol),
            VerticalAlignment = VerticalAlignment.Center,
            Margin            = new Thickness(0, 0, 14, 0)
        };
        DockPanel.SetDock(infoIcon, Dock.Left);
        card3Dock.Children.Add(infoIcon);

        var verStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var verTitle = new TextBlock
        {
            Text              = "LiteBright v1.2.0 (x64)",
            FontFamily        = new FontFamily(textFont),
            FontSize          = 13.5,
            FontWeight        = FontWeights.SemiBold,
            Foreground        = primaryFg
        };
        verStack.Children.Add(verTitle);

        var verSub = new TextBlock
        {
            Text              = "Latest release • Running on Windows 11 Fluent architecture",
            FontFamily        = new FontFamily(textFont),
            FontSize          = 11.5,
            Foreground        = secondaryFg,
            Margin            = new Thickness(0, 2, 0, 0)
        };
        verStack.Children.Add(verSub);
        card3Dock.Children.Add(verStack);

        card3.Child = card3Dock;
        stack.Children.Add(card3);

        return scroll;
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Page 2: Monitor Settings (All Displays details matching Twinkle Tray Screenshot 2)
    // ─────────────────────────────────────────────────────────────────────────────
    private UIElement BuildMonitorSettingsPage(bool isDark, Color accentCol)
    {
        var (scroll, stack) = CreatePageContainer(LocalizationManager.T("GENERIC_ALL_DISPLAYS", "All Displays"), isDark);
        var cardBg = GetCardBg(isDark);
        var cardBorder = GetCardBorder(isDark);
        var divider = GetDivider(isDark);
        var primaryFg = GetPrimaryFg(isDark);
        var secondaryFg = GetSecondaryFg(isDark);
        string textFont = BrightnessPanel.TextFontName;
        string iconFont = BrightnessPanel.IconFontName;

        var monitors = _monitorManager.Monitors;
        if (monitors.Count == 0)
        {
            stack.Children.Add(new TextBlock
            {
                Text       = LocalizationManager.T("GENERIC_NO_DISPLAYS", "No monitors currently detected."),
                FontFamily = new FontFamily(textFont),
                FontSize   = 13,
                Foreground = secondaryFg,
                Margin     = new Thickness(0, 10, 0, 0)
            });
            return scroll;
        }

        for (int i = 0; i < monitors.Count; i++)
        {
            var mon = monitors[i];
            string monLabel = mon.Name;

            var monCard = CreateCard(cardBg, cardBorder);
            var monStack = new StackPanel { Margin = new Thickness(16, 14, 16, 14) };

            // Header: Display icon + Display Title
            var headerDock = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };
            var monIcon = new TextBlock
            {
                Text              = "\uE7F4", // Monitor display glyph
                FontFamily        = new FontFamily(iconFont),
                FontSize          = 16,
                Foreground        = primaryFg,
                VerticalAlignment = VerticalAlignment.Center,
                Margin            = new Thickness(0, 0, 10, 0)
            };
            DockPanel.SetDock(monIcon, Dock.Left);
            headerDock.Children.Add(monIcon);

            var titleText = new TextBlock
            {
                Text              = monLabel,
                FontFamily        = new FontFamily(textFont),
                FontSize          = 14.5,
                FontWeight        = FontWeights.SemiBold,
                Foreground        = primaryFg,
                VerticalAlignment = VerticalAlignment.Center
            };
            headerDock.Children.Add(titleText);
            monStack.Children.Add(headerDock);

            // Detail Lines (Matching Twinkle Tray All Displays format exactly)
            monStack.Children.Add(MakeMonitorDetailRow(LocalizationManager.T("SETTINGS_MONITORS_DETAILS_NAME", "Name") + ":", mon.Name, textFont, primaryFg, secondaryFg));
            monStack.Children.Add(MakeMonitorDetailRow(LocalizationManager.T("SETTINGS_MONITORS_DETAILS_INTERNAL_NAME", "Internal Name") + ":", mon.InternalName, textFont, primaryFg, secondaryFg));

            monStack.Children.Add(MakeCommMethodRow(LocalizationManager.T("SETTINGS_MONITORS_DETAILS_COMMUNICATION", "Communication Method") + ":", mon.CommunicationMethod, textFont, primaryFg, secondaryFg));

            string currentBrightness = mon.IsCommunicationSupported
                ? $"{mon.BrightnessPercent}"
                : LocalizationManager.T("GENERIC_NOT_SUPPORTED", "Not supported");
            monStack.Children.Add(MakeMonitorDetailRow(LocalizationManager.T("SETTINGS_MONITORS_DETAILS_BRIGHTNESS", "Current Brightness") + ":", currentBrightness, textFont, primaryFg, secondaryFg));

            string maxBrightness = mon.IsCommunicationSupported
                ? $"{mon.MaxBrightness}"
                : LocalizationManager.T("GENERIC_NOT_SUPPORTED", "Not supported");
            monStack.Children.Add(MakeMonitorDetailRow(LocalizationManager.T("SETTINGS_MONITORS_DETAILS_MAX_BRIGHTNESS", "Max Brightness") + ":", maxBrightness, textFont, primaryFg, secondaryFg));

            string normBrightness = mon.IsCommunicationSupported
                ? $"{mon.MinBrightness} - {mon.MaxBrightness}"
                : LocalizationManager.T("GENERIC_NOT_SUPPORTED", "Not supported");
            monStack.Children.Add(MakeMonitorDetailRow(LocalizationManager.T("SETTINGS_MONITORS_DETAILS_BRIGHTNESS_NORMALIZATION", "Brightness Normalization") + ":", normBrightness, textFont, primaryFg, secondaryFg));

            string hdrStatus = mon.HdrStatus == "Active"
                ? LocalizationManager.T("GENERIC_ACTIVE", "Active")
                : mon.HdrStatus == "Supported"
                    ? LocalizationManager.T("GENERIC_SUPPORTED", "Supported")
                    : LocalizationManager.T("GENERIC_UNSUPPORTED", "Unsupported");
            monStack.Children.Add(MakeMonitorDetailRow(LocalizationManager.T("SETTINGS_MONITORS_DETAILS_HDR", "HDR") + ":", hdrStatus, textFont, primaryFg, secondaryFg));

            monCard.Child = monStack;
            stack.Children.Add(monCard);
        }

        // Refresh displays Windows 11 style action button
        var btnNormalBg = isDark
            ? new SolidColorBrush(Color.FromArgb(24, 255, 255, 255))
            : new SolidColorBrush(Color.FromArgb(245, 255, 255, 255));
        var btnHoverBg = isDark
            ? new SolidColorBrush(Color.FromArgb(36, 255, 255, 255))
            : new SolidColorBrush(Color.FromArgb(235, 235, 235, 240));
        var btnPressedBg = isDark
            ? new SolidColorBrush(Color.FromArgb(16, 255, 255, 255))
            : new SolidColorBrush(Color.FromArgb(220, 220, 220, 230));
        var btnBorderBrush = isDark
            ? new SolidColorBrush(Color.FromArgb(32, 255, 255, 255))
            : new SolidColorBrush(Color.FromArgb(40, 0, 0, 0));

        var btnRedetect = new Border
        {
            Background          = btnNormalBg,
            BorderBrush         = btnBorderBrush,
            BorderThickness     = new Thickness(1),
            CornerRadius        = new CornerRadius(4),
            Height              = 32,
            Padding             = new Thickness(14, 0, 14, 0),
            Cursor              = Cursors.Hand,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin              = new Thickness(0, 8, 0, 16)
        };

        var btnContent = new StackPanel
        {
            Orientation       = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };

        var refreshIcon = new TextBlock
        {
            Text              = "\uE72C", // Sync / Refresh glyph in Segoe Fluent Icons
            FontFamily        = new FontFamily(iconFont),
            FontSize          = 13,
            Foreground        = primaryFg,
            VerticalAlignment = VerticalAlignment.Center,
            Margin            = new Thickness(0, 0, 8, 0)
        };
        btnContent.Children.Add(refreshIcon);

        var redetectText = new TextBlock
        {
            Text              = LocalizationManager.T("GENERIC_REFRESH_DISPLAYS", "Refresh displays"),
            FontFamily        = new FontFamily(textFont),
            FontSize          = 13,
            Foreground        = primaryFg,
            VerticalAlignment = VerticalAlignment.Center
        };
        btnContent.Children.Add(redetectText);
        btnRedetect.Child = btnContent;

        btnRedetect.MouseEnter += (_, _) => btnRedetect.Background = btnHoverBg;
        btnRedetect.MouseLeave += (_, _) => btnRedetect.Background = btnNormalBg;
        btnRedetect.MouseLeftButtonDown += (_, _) => btnRedetect.Background = btnPressedBg;
        btnRedetect.MouseLeftButtonUp += (_, _) =>
        {
            btnRedetect.Background = btnHoverBg;
            _monitorManager.Refresh();
            SelectPage(1); // Reload display list
        };
        stack.Children.Add(btnRedetect);

        return scroll;
    }

    private static Grid MakeCommMethodRow(string label, string commMethod, string font, Brush primaryFg, Brush secondaryFg)
    {
        var grid = new Grid { Margin = new Thickness(0, 2, 0, 2) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170, GridUnitType.Pixel) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var lblText = new TextBlock
        {
            Text       = label,
            FontFamily = new FontFamily(font),
            FontSize   = 12,
            Foreground = secondaryFg
        };
        Grid.SetColumn(lblText, 0);
        grid.Children.Add(lblText);

        var valPanel = new StackPanel { Orientation = Orientation.Horizontal };
        var valText = new TextBlock
        {
            Text              = commMethod,
            FontFamily        = new FontFamily(font),
            FontSize          = 12,
            FontWeight        = FontWeights.SemiBold,
            Foreground        = primaryFg,
            VerticalAlignment = VerticalAlignment.Center
        };
        valPanel.Children.Add(valText);

        bool isNone = commMethod.Equals("None", StringComparison.OrdinalIgnoreCase);
        var iconText = new TextBlock
        {
            Text              = isNone ? "\uEB90" : "\uE73D",
            FontFamily        = new FontFamily(BrightnessPanel.IconFontName),
            FontSize          = 11,
            FontWeight        = FontWeights.Bold,
            Foreground        = isNone
                ? new SolidColorBrush(Color.FromArgb(255, 239, 68, 68))
                : new SolidColorBrush(Color.FromArgb(255, 52, 211, 153)),
            VerticalAlignment = VerticalAlignment.Center,
            Margin            = new Thickness(6, 0, 0, 0)
        };
        valPanel.Children.Add(iconText);

        Grid.SetColumn(valPanel, 1);
        grid.Children.Add(valPanel);
        return grid;
    }

    private static Grid MakeMonitorDetailRow(string label, string val, string font, Brush primaryFg, Brush secondaryFg)
    {
        var grid = new Grid { Margin = new Thickness(0, 2, 0, 2) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170, GridUnitType.Pixel) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var lblText = new TextBlock
        {
            Text       = label,
            FontFamily = new FontFamily(font),
            FontSize   = 12,
            Foreground = secondaryFg
        };
        Grid.SetColumn(lblText, 0);
        grid.Children.Add(lblText);

        var valText = new TextBlock
        {
            Text       = val,
            FontFamily = new FontFamily(font),
            FontSize   = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = primaryFg
        };
        Grid.SetColumn(valText, 1);
        grid.Children.Add(valText);

        return grid;
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Page 3: DDC/CI Features (Brightness step, Contrast toggle, Hardware sync)
    // ─────────────────────────────────────────────────────────────────────────────
    private UIElement BuildDdcCiPage(bool isDark, Color accentCol)
    {
        var (scroll, stack) = CreatePageContainer(LocalizationManager.T("SETTINGS_SIDEBAR_FEATURES", "DDC/CI Features"), isDark);
        var cardBg = GetCardBg(isDark);
        var cardBorder = GetCardBorder(isDark);
        var divider = GetDivider(isDark);
        var primaryFg = GetPrimaryFg(isDark);
        var secondaryFg = GetSecondaryFg(isDark);
        string textFont = BrightnessPanel.TextFontName;
        string iconFont = BrightnessPanel.IconFontName;

        // Card 1: Brightness Features
        var card1 = CreateCard(cardBg, cardBorder);
        var card1Stack = new StackPanel();

        int stepVal = Math.Clamp(_draft.BrightnessStep, 1, 25);
        var stepStepper = new FluentStepper(stepVal, 1, 25, isDark, accentCol, v => { _draft.BrightnessStep = v; AutoSave(); });
        card1Stack.Children.Add(MakeSettingRow(
            "\uE706", LocalizationManager.T("SETTINGS_MONITORS_RATE_TITLE", "Adjustment step size"),
            LocalizationManager.T("SETTINGS_HOTKEYS_LEVEL_DESC", "Percentage adjusted per mouse wheel notch, hotkey, and slider tap"),
            stepStepper, textFont, iconFont, primaryFg, secondaryFg));

        card1.Child = card1Stack;
        stack.Children.Add(card1);

        // Card 2: Contrast Features
        var card2 = CreateCard(cardBg, cardBorder);
        var card2Stack = new StackPanel();

        var toggleContrast = new FluentToggleSwitch(_draft.EnableContrastSlider, accentCol, isDark, v => { _draft.EnableContrastSlider = v; AutoSave(); });
        card2Stack.Children.Add(MakeSettingRow(
            "\uE793", LocalizationManager.T("PANEL_LABEL_CONTRAST", "Contrast") + " slider",
            "Display an optional contrast adjustment bar under each brightness slider",
            toggleContrast, textFont, iconFont, primaryFg, secondaryFg));

        card2.Child = card2Stack;
        stack.Children.Add(card2);

        // Card 3: Hardware Protocol
        var card3 = CreateCard(cardBg, cardBorder);
        var card3Stack = new StackPanel();

        var throttleCombo = new FluentComboBox(new[] { "Fast (25ms)", "Balanced (50ms)", "Stable (100ms)" }, "Balanced (50ms)", isDark, accentCol, _ => AutoSave(), controlWidth: 240);
        card3Stack.Children.Add(MakeSettingRow(
            "\uE9E9", "Hardware write throttle",
            "Delay between hardware I2C/DDC commands to ensure flicker-free communication",
            throttleCombo, textFont, iconFont, primaryFg, secondaryFg));

        card3.Child = card3Stack;
        stack.Children.Add(card3);

        return scroll;
    }
    // ─────────────────────────────────────────────────────────────────────────────
    // Page 4: Hotkeys & Shortcuts
    // ─────────────────────────────────────────────────────────────────────────────
    private UIElement BuildHotkeysPage(bool isDark, Color accentCol)
    {
        var (scroll, stack) = CreatePageContainer(LocalizationManager.T("SETTINGS_SIDEBAR_HOTKEYS", "Hotkeys & Shortcuts"), isDark);
        var cardBg = GetCardBg(isDark);
        var cardBorder = GetCardBorder(isDark);
        var divider = GetDivider(isDark);
        var primaryFg = GetPrimaryFg(isDark);
        var secondaryFg = GetSecondaryFg(isDark);
        string textFont = BrightnessPanel.TextFontName;
        string iconFont = BrightnessPanel.IconFontName;

        stack.Children.Add(new TextBlock
        {
            Text       = LocalizationManager.T("SETTINGS_HOTKEYS_DESC", "Click a shortcut box and press your desired keyboard combination to assign hotkeys."),
            FontFamily = new FontFamily(textFont),
            FontSize   = 12,
            Foreground = secondaryFg,
            Margin     = new Thickness(2, 0, 0, 10)
        });

        _hotkeyBinders.Clear();
        var monitors = _monitorManager.Monitors;
        for (int mi = 0; mi < Math.Min(monitors.Count, 4); mi++)
        {
            var mon = monitors[mi];
            string monLabel = mon.Name;
            int pi = monLabel.LastIndexOf(" (", StringComparison.Ordinal);
            if (pi > 0) monLabel = monLabel[..pi];

            var monCard = CreateCard(cardBg, cardBorder);
            var monStack = new StackPanel();

            // Monitor Header Row
            var monHeaderDock = new DockPanel { Margin = new Thickness(16, 10, 16, 8) };
            var monDisplayIcon = new TextBlock
            {
                Text              = "\uE7F4",
                FontFamily        = new FontFamily(iconFont),
                FontSize          = 14,
                Foreground        = primaryFg,
                VerticalAlignment = VerticalAlignment.Center,
                Margin            = new Thickness(0, 0, 10, 0)
            };
            DockPanel.SetDock(monDisplayIcon, Dock.Left);
            monHeaderDock.Children.Add(monDisplayIcon);

            var badgeBorder = new Border
            {
                Background      = isDark
                    ? new SolidColorBrush(Color.FromArgb(30, 52, 211, 153))
                    : new SolidColorBrush(Color.FromArgb(25, 16, 185, 129)),
                CornerRadius    = new CornerRadius(4),
                Padding         = new Thickness(8, 2, 8, 2),
                VerticalAlignment = VerticalAlignment.Center
            };
            var badgeText = new TextBlock
            {
                Text              = $"Display {mi + 1}",
                FontFamily        = new FontFamily(textFont),
                FontSize          = 11,
                FontWeight        = FontWeights.SemiBold,
                Foreground        = isDark
                    ? new SolidColorBrush(Color.FromArgb(235, 52, 211, 153))
                    : new SolidColorBrush(Color.FromArgb(235, 5, 150, 105))
            };
            badgeBorder.Child = badgeText;
            DockPanel.SetDock(badgeBorder, Dock.Right);
            monHeaderDock.Children.Add(badgeBorder);

            var monLabelText = new TextBlock
            {
                Text              = monLabel,
                FontFamily        = new FontFamily(textFont),
                FontSize          = 13,
                FontWeight        = FontWeights.SemiBold,
                Foreground        = primaryFg,
                VerticalAlignment = VerticalAlignment.Center
            };
            monHeaderDock.Children.Add(monLabelText);
            monStack.Children.Add(monHeaderDock);

            monStack.Children.Add(MakeDivider(divider));

            // Increase & Decrease rows
            foreach (bool isUp in new[] { true, false })
            {
                var action = (HotkeyAction)(mi * 2 + (isUp ? 0 : 1));
                var existing = _draft.Hotkeys
                    .FirstOrDefault(h => h.Action == action)
                    ?.ToDefinition() ?? new HotkeyDefinition { Action = action };

                var binder = new HotkeyBinder(action, existing, isUp, isDark, accentCol, textFont, iconFont, AutoSave);
                _hotkeyBinders.Add(binder);
                monStack.Children.Add(binder.Element);

                if (isUp) monStack.Children.Add(MakeDivider(divider));
            }

            monCard.Child = monStack;
            stack.Children.Add(monCard);
        }

        return scroll;
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // UI Helpers & Component Factories
    // ─────────────────────────────────────────────────────────────────────────────

    private (ScrollViewer, StackPanel) CreatePageContainer(string title, bool isDark)
    {
        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility   = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Padding                       = new Thickness(24, 14, 24, 14)
        };
        ApplySlimScrollbarStyle(scroll, isDark);

        var stack = new StackPanel();

        // Page Title
        var titleText = new TextBlock
        {
            Text       = title,
            FontFamily = new FontFamily(BrightnessPanel.TextFontName),
            FontSize   = 20,
            FontWeight = FontWeights.SemiBold,
            Foreground = GetPrimaryFg(isDark),
            Margin     = new Thickness(0, 0, 0, 14)
        };
        stack.Children.Add(titleText);

        scroll.Content = stack;
        return (scroll, stack);
    }

    private static Border CreateCard(Brush bg, Brush border) => new()
    {
        CornerRadius    = new CornerRadius(8),
        Background      = bg,
        BorderBrush     = border,
        BorderThickness = new Thickness(1),
        Margin          = new Thickness(0, 0, 0, 14)
    };

    private static Border MakeDivider(Brush dividerBrush) => new()
    {
        Height          = 1,
        Background      = dividerBrush,
        Margin          = new Thickness(50, 0, 16, 0)
    };

    private static Border MakeSettingRow(string iconGlyph, string title, string subtitle, FrameworkElement control,
        string textFont, string iconFont, Brush primaryFg, Brush secondaryFg)
    {
        var rowBorder = new Border
        {
            Background   = Brushes.Transparent,
            MinHeight    = 54,
            Padding      = new Thickness(16, 12, 16, 12),
            CornerRadius = new CornerRadius(6)
        };

        bool hasIcon = !string.IsNullOrEmpty(iconGlyph);
        var rowGrid = new Grid();
        if (hasIcon)
        {
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36, GridUnitType.Pixel) });
        }
        rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        if (hasIcon)
        {
            var rowIcon = new TextBlock
            {
                Text              = iconGlyph,
                FontFamily        = new FontFamily(iconFont),
                FontSize          = 16,
                Foreground        = primaryFg,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(rowIcon, 0);
            rowGrid.Children.Add(rowIcon);
        }

        var textStack = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin            = new Thickness(0, 0, 14, 0)
        };

        var titleBlock = new TextBlock
        {
            Text              = title,
            FontFamily        = new FontFamily(textFont),
            FontSize          = 13,
            FontWeight        = FontWeights.SemiBold,
            Foreground        = primaryFg
        };
        textStack.Children.Add(titleBlock);

        var subBlock = new TextBlock
        {
            Text              = subtitle,
            FontFamily        = new FontFamily(textFont),
            FontSize          = 11.5,
            Foreground        = secondaryFg,
            Margin            = new Thickness(0, 2, 0, 0),
            TextWrapping      = TextWrapping.Wrap
        };
        textStack.Children.Add(subBlock);

        Grid.SetColumn(textStack, hasIcon ? 1 : 0);
        rowGrid.Children.Add(textStack);

        control.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(control, hasIcon ? 2 : 1);
        rowGrid.Children.Add(control);

        rowBorder.Child = rowGrid;

        return rowBorder;
    }

    private static Brush GetCardBg(bool isDark) => isDark
        ? new SolidColorBrush(Color.FromArgb(255, 41, 41, 45))  // #29292D - separate elevated section color
        : new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));

    private static Brush GetCardBorder(bool isDark) => isDark
        ? new SolidColorBrush(Color.FromArgb(20, 255, 255, 255))
        : new SolidColorBrush(Color.FromArgb(16, 0, 0, 0));

    private static Brush GetPrimaryFg(bool isDark) => isDark
        ? new SolidColorBrush(Color.FromArgb(255, 255, 255, 255))
        : new SolidColorBrush(Color.FromArgb(255, 26, 26, 26));

    private static Brush GetSecondaryFg(bool isDark) => isDark
        ? new SolidColorBrush(Color.FromArgb(200, 160, 160, 165))
        : new SolidColorBrush(Color.FromArgb(200, 96, 96, 100));

    private static Brush GetDivider(bool isDark) => isDark
        ? new SolidColorBrush(Color.FromArgb(18, 255, 255, 255))
        : new SolidColorBrush(Color.FromArgb(14, 0, 0, 0));

    private static void ApplySlimScrollbarStyle(ScrollViewer scrollViewer, bool isDark)
    {
        string thumbColor = isDark ? "#4DFFFFFF" : "#44000000";
        string xaml = $@"
<ResourceDictionary xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
                    xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>
    <Style TargetType='ScrollBar'>
        <Setter Property='Width' Value='5'/>
        <Setter Property='MinWidth' Value='5'/>
        <Setter Property='Background' Value='Transparent'/>
        <Setter Property='Template'>
            <Setter.Value>
                <ControlTemplate TargetType='ScrollBar'>
                    <Grid Background='Transparent'>
                        <Track x:Name='PART_Track' IsDirectionReversed='true'>
                            <Track.Thumb>
                                <Thumb>
                                    <Thumb.Template>
                                        <ControlTemplate TargetType='Thumb'>
                                            <Border CornerRadius='2.5' 
                                                    Background='{thumbColor}' 
                                                    Margin='1,2,1,2'/>
                                        </ControlTemplate>
                                    </Thumb.Template>
                                </Thumb>
                            </Track.Thumb>
                        </Track>
                    </Grid>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>
</ResourceDictionary>";

        try
        {
            var resDict = (ResourceDictionary)XamlReader.Parse(xaml);
            scrollViewer.Resources.MergedDictionaries.Add(resDict);
        }
        catch { }
    }

    private static ImageSource? GetAppIconSource()
    {
        try
        {
            var asm = System.Reflection.Assembly.GetExecutingAssembly();
            using var stream = asm.GetManifestResourceStream("BrightnessController.public.icon.ico");
            if (stream != null)
            {
                var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                var frame = decoder.Frames.OrderBy(f => Math.Abs(f.PixelWidth - 32)).FirstOrDefault() ?? decoder.Frames[0];
                return frame;
            }
        }
        catch { }
        return null;
    }

    private static Color BlendColor(Color c1, Color c2, float amount)
    {
        byte r = (byte)(c1.R + (c2.R - c1.R) * amount);
        byte g = (byte)(c1.G + (c2.G - c1.G) * amount);
        byte b = (byte)(c1.B + (c2.B - c1.B) * amount);
        return Color.FromArgb(c1.A, r, g, b);
    }

    private void AutoSave()
    {
        if (_hotkeyBinders.Count > 0)
        {
            _draft.Hotkeys.Clear();
            foreach (var binder in _hotkeyBinders)
            {
                var def = binder.GetDefinition();
                if (def.IsValid)
                {
                    _draft.Hotkeys.Add(new AppSettings.HotkeyEntry
                    {
                        Action    = def.Action,
                        Modifiers = def.Modifiers,
                        Key       = def.Key
                    });
                }
            }
        }

        StartupManager.Apply(_draft.StartWithWindows);
        SettingsManager.Save(_draft);
        SettingsSaved?.Invoke(_draft);
    }

    private static AppSettings Clone(AppSettings src) => new()
    {
        StartWithWindows         = src.StartWithWindows,
        ApplyBrightnessAtStartup = src.ApplyBrightnessAtStartup,
        Language                 = src.Language,
        Theme                    = src.Theme,
        AutoBrightness           = src.AutoBrightness,
        EnableContrastSlider     = src.EnableContrastSlider,
        BrightnessStep           = src.BrightnessStep,
        Hotkeys                  = src.Hotkeys.Select(h => new AppSettings.HotkeyEntry
        {
            Action    = h.Action,
            Modifiers = h.Modifiers,
            Key       = h.Key
        }).ToList()
    };

    // ─────────────────────────────────────────────────────────────────────────────
    // Sub-components: SidebarNavItem, FluentComboBox, FluentToggleSwitch, FluentStepper, HotkeyBinder
    // ─────────────────────────────────────────────────────────────────────────────

    private sealed class SidebarNavItem
    {
        public int Index { get; }
        public Border Element { get; }

        private readonly Border _pill;
        private readonly TextBlock _glyphBlock;
        private readonly TextBlock _titleBlock;
        private readonly bool _isDark;
        private readonly Color _accentColor;
        private bool _isSelected;

        public SidebarNavItem(int index, string glyph, string title, bool isDark, Color accentColor,
            string textFont, string iconFont, Action<int> onSelect)
        {
            Index        = index;
            _isDark      = isDark;
            _accentColor = accentColor;

            Element = new Border
            {
                Height          = 36,
                CornerRadius    = new CornerRadius(5),
                Background      = Brushes.Transparent,
                Cursor          = Cursors.Hand,
                Margin          = new Thickness(8, 2, 8, 2)
            };

            var dock = new DockPanel();

            _pill = new Border
            {
                Width               = 3,
                Height              = 16,
                CornerRadius        = new CornerRadius(1.5),
                Background          = new SolidColorBrush(_accentColor),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment   = VerticalAlignment.Center,
                Margin              = new Thickness(0, 0, 8, 0),
                Visibility          = Visibility.Hidden
            };
            DockPanel.SetDock(_pill, Dock.Left);
            dock.Children.Add(_pill);

            _glyphBlock = new TextBlock
            {
                Text              = glyph,
                FontFamily        = new FontFamily(iconFont),
                FontSize          = 14,
                Foreground        = _isDark
                    ? new SolidColorBrush(Color.FromArgb(190, 255, 255, 255))
                    : new SolidColorBrush(Color.FromArgb(190, 0, 0, 0)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin            = new Thickness(0, 0, 10, 0)
            };
            DockPanel.SetDock(_glyphBlock, Dock.Left);
            dock.Children.Add(_glyphBlock);

            _titleBlock = new TextBlock
            {
                Text              = title,
                FontFamily        = new FontFamily(textFont),
                FontSize          = 12.5,
                Foreground        = _isDark
                    ? new SolidColorBrush(Color.FromArgb(200, 255, 255, 255))
                    : new SolidColorBrush(Color.FromArgb(200, 20, 20, 20)),
                VerticalAlignment = VerticalAlignment.Center
            };
            dock.Children.Add(_titleBlock);

            Element.Child = dock;

            Element.MouseEnter += (_, _) =>
            {
                if (!_isSelected)
                {
                    Element.Background = _isDark
                        ? new SolidColorBrush(Color.FromArgb(16, 255, 255, 255))
                        : new SolidColorBrush(Color.FromArgb(14, 0, 0, 0));
                }
            };
            Element.MouseLeave += (_, _) =>
            {
                if (!_isSelected) Element.Background = Brushes.Transparent;
            };

            Element.MouseLeftButtonUp += (_, _) => onSelect(Index);
        }

        public void SetSelected(bool selected)
        {
            _isSelected = selected;
            if (_isSelected)
            {
                Element.Background    = _isDark
                    ? new SolidColorBrush(Color.FromArgb(45, 255, 255, 255))
                    : new SolidColorBrush(Color.FromArgb(30, 0, 0, 0));
                _pill.Visibility      = Visibility.Visible;
                _titleBlock.Foreground = _isDark ? Brushes.White : Brushes.Black;
                _titleBlock.FontWeight = FontWeights.SemiBold;
                _glyphBlock.Foreground = _isDark ? Brushes.White : Brushes.Black;
            }
            else
            {
                Element.Background    = Brushes.Transparent;
                _pill.Visibility      = Visibility.Hidden;
                _titleBlock.Foreground = _isDark
                    ? new SolidColorBrush(Color.FromArgb(200, 255, 255, 255))
                    : new SolidColorBrush(Color.FromArgb(200, 20, 20, 20));
                _titleBlock.FontWeight = FontWeights.Normal;
                _glyphBlock.Foreground = _isDark
                    ? new SolidColorBrush(Color.FromArgb(190, 255, 255, 255))
                    : new SolidColorBrush(Color.FromArgb(190, 0, 0, 0));
            }
        }
    }

    public void ShowDropdownOverlay(
        FrameworkElement anchor,
        IEnumerable<string> items,
        string selectedItem,
        bool isDark,
        Color accentColor,
        Action<string> onSelected)
    {
        if (_dropdownOverlay == null || _rootGrid == null) return;

        if (_dropdownOverlay.Visibility == Visibility.Visible && _currentDropdownAnchor == anchor)
        {
            CloseDropdownOverlay();
            return;
        }

        _currentDropdownAnchor = anchor;
        _dropdownOverlay.Children.Clear();

        // 1. Transparent full-window backdrop to dismiss on click outside
        var backdrop = new Border
        {
            Background          = Brushes.Transparent,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment   = VerticalAlignment.Stretch
        };
        backdrop.MouseLeftButtonDown += (_, e) =>
        {
            e.Handled = true;
            CloseDropdownOverlay();
        };
        _dropdownOverlay.Children.Add(backdrop);

        // 2. Measure anchor position relative to _rootGrid
        System.Windows.Point pt;
        try
        {
            pt = anchor.TranslatePoint(new System.Windows.Point(0, 0), _rootGrid);
        }
        catch
        {
            return;
        }

        double anchorW = anchor.ActualWidth > 0 ? anchor.ActualWidth : anchor.Width;
        if (anchorW <= 0) anchorW = 240;
        double anchorH = anchor.ActualHeight > 0 ? anchor.ActualHeight : 32;

        double rootW = _rootGrid.ActualWidth > 0 ? _rootGrid.ActualWidth : Width;
        double rootH = _rootGrid.ActualHeight > 0 ? _rootGrid.ActualHeight : Height;

        double spaceBelow = rootH - (pt.Y + anchorH) - 10;
        double spaceAbove = pt.Y - 10;

        var itemList = items.ToList();
        double desiredHeight = Math.Min(itemList.Count * 33 + 12, 230);

        bool openUpward = spaceBelow < desiredHeight && spaceAbove > spaceBelow;
        double menuHeight = openUpward
            ? Math.Min(desiredHeight, Math.Max(100, spaceAbove))
            : Math.Min(desiredHeight, Math.Max(100, spaceBelow));

        double menuTop = openUpward
            ? (pt.Y - menuHeight - 3)
            : (pt.Y + anchorH + 3);

        // Strictly clamp within window boundaries
        if (menuTop < 6) menuTop = 6;
        if (menuTop + menuHeight > rootH - 6) menuTop = rootH - menuHeight - 6;

        double menuLeft = pt.X;
        if (menuLeft + anchorW > rootW - 8) menuLeft = rootW - anchorW - 8;
        if (menuLeft < 8) menuLeft = 8;

        // 3. Dropdown Menu Border
        var menuBorder = new Border
        {
            Width               = anchorW,
            Height              = menuHeight,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment   = VerticalAlignment.Top,
            Margin              = new Thickness(menuLeft, menuTop, 0, 0),
            Background          = isDark
                ? new SolidColorBrush(Color.FromArgb(255, 36, 36, 39))  // #242427
                : new SolidColorBrush(Color.FromArgb(255, 250, 250, 252)),
            BorderBrush         = isDark
                ? new SolidColorBrush(Color.FromArgb(255, 58, 58, 62))  // #3A3A3E
                : new SolidColorBrush(Color.FromArgb(255, 222, 222, 226)),
            BorderThickness     = new Thickness(1),
            CornerRadius        = new CornerRadius(8),
            Padding             = anchorW < 80 ? new Thickness(2, 4, 2, 4) : new Thickness(4, 5, 4, 5),
            Effect              = new DropShadowEffect
            {
                BlurRadius   = 12,
                ShadowDepth  = 3,
                Direction    = 270,
                Color        = Colors.Black,
                Opacity      = isDark ? 0.60 : 0.20
            }
        };

        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility   = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        ApplySlimScrollbarStyle(scroll, isDark);

        var stack = new StackPanel();
        var selBg = isDark
            ? new SolidColorBrush(Color.FromArgb(32, 255, 255, 255))
            : new SolidColorBrush(Color.FromArgb(20, 0, 0, 0));
        var hoverBg = isDark
            ? new SolidColorBrush(Color.FromArgb(20, 255, 255, 255))
            : new SolidColorBrush(Color.FromArgb(10, 0, 0, 0));

        Border? selectedBorder = null;

        foreach (var item in itemList)
        {
            bool isSel = string.Equals(item, selectedItem, StringComparison.OrdinalIgnoreCase);

            var itemBorder = new Border
            {
                Height       = 30,
                CornerRadius = new CornerRadius(5),
                Margin       = new Thickness(1, 1, 1, 1),
                Background   = isSel ? selBg : Brushes.Transparent,
                Cursor       = Cursors.Hand,
                ToolTip      = item
            };

            var itemDock = new DockPanel();

            if (anchorW >= 80)
            {
                var pillContainer = new Grid { Width = 14, VerticalAlignment = VerticalAlignment.Stretch };
                var pill = new Border
                {
                    Width               = 3,
                    Height              = 16,
                    CornerRadius        = new CornerRadius(1.5),
                    Background          = new SolidColorBrush(accentColor),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment   = VerticalAlignment.Center,
                    Visibility          = isSel ? Visibility.Visible : Visibility.Hidden
                };
                pillContainer.Children.Add(pill);
                DockPanel.SetDock(pillContainer, Dock.Left);
                itemDock.Children.Add(pillContainer);
            }

            var itemText = new TextBlock
            {
                Text              = item,
                FontFamily        = new FontFamily(BrightnessPanel.TextFontName),
                FontSize          = 12,
                FontWeight        = (isSel && anchorW < 80) ? FontWeights.SemiBold : FontWeights.Normal,
                Foreground        = isDark ? Brushes.White : Brushes.Black,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment     = anchorW < 80 ? TextAlignment.Center : TextAlignment.Left,
                Margin            = anchorW < 80 ? new Thickness(0, 0, 0, 0) : new Thickness(2, 0, 8, 0),
                TextTrimming      = TextTrimming.CharacterEllipsis
            };
            itemDock.Children.Add(itemText);
            itemBorder.Child = itemDock;

            if (isSel) selectedBorder = itemBorder;

            string captured = item;
            var curr = itemBorder;

            curr.MouseEnter += (_, _) =>
            {
                if (!string.Equals(captured, selectedItem, StringComparison.OrdinalIgnoreCase))
                    curr.Background = hoverBg;
            };
            curr.MouseLeave += (_, _) =>
            {
                if (!string.Equals(captured, selectedItem, StringComparison.OrdinalIgnoreCase))
                    curr.Background = Brushes.Transparent;
            };

            curr.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                CloseDropdownOverlay();
                onSelected(captured);
            };

            stack.Children.Add(itemBorder);
        }

        scroll.Content = stack;
        menuBorder.Child = scroll;
        _dropdownOverlay.Children.Add(menuBorder);
        _dropdownOverlay.Visibility = Visibility.Visible;

        // Auto-scroll to selected item
        if (selectedBorder != null)
        {
            _dropdownOverlay.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() =>
            {
                selectedBorder.BringIntoView();
            }));
        }
    }

    public void CloseDropdownOverlay()
    {
        _currentDropdownAnchor = null;
        if (_dropdownOverlay != null)
        {
            _dropdownOverlay.Children.Clear();
            _dropdownOverlay.Visibility = Visibility.Collapsed;
        }
    }

    public void ShowDonateMenu(FrameworkElement anchor, bool isDark, string textFont, string iconFont)
    {
        if (_currentDropdownAnchor == anchor)
        {
            CloseDropdownOverlay();
            return;
        }

        CloseDropdownOverlay();
        _currentDropdownAnchor = anchor;

        var backdrop = new Border
        {
            Background          = Brushes.Transparent,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment   = VerticalAlignment.Stretch
        };
        backdrop.MouseLeftButtonDown += (_, e) =>
        {
            e.Handled = true;
            CloseDropdownOverlay();
        };
        _dropdownOverlay.Children.Add(backdrop);

        System.Windows.Point pt;
        try
        {
            pt = anchor.TranslatePoint(new System.Windows.Point(0, 0), _rootGrid);
        }
        catch
        {
            return;
        }

        double menuWidth = 246;
        double menuHeight = 224;

        double rootW = _rootGrid.ActualWidth > 0 ? _rootGrid.ActualWidth : Width;
        double rootH = _rootGrid.ActualHeight > 0 ? _rootGrid.ActualHeight : Height;

        double menuLeft = pt.X;
        if (menuLeft + menuWidth > rootW - 8) menuLeft = rootW - menuWidth - 8;
        if (menuLeft < 8) menuLeft = 8;

        double menuTop = pt.Y - menuHeight - 6;
        if (menuTop < 8) menuTop = pt.Y + (anchor.ActualHeight > 0 ? anchor.ActualHeight : 36) + 6;

        var menuBorder = new Border
        {
            Width               = menuWidth,
            Height              = menuHeight,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment   = VerticalAlignment.Top,
            Margin              = new Thickness(menuLeft, menuTop, 0, 0),
            Background          = isDark
                ? new SolidColorBrush(Color.FromArgb(252, 34, 34, 38))
                : new SolidColorBrush(Color.FromArgb(252, 252, 252, 254)),
            BorderBrush         = isDark
                ? new SolidColorBrush(Color.FromArgb(255, 60, 60, 66))
                : new SolidColorBrush(Color.FromArgb(255, 218, 218, 224)),
            BorderThickness     = new Thickness(1),
            CornerRadius        = new CornerRadius(8),
            Padding             = new Thickness(6, 7, 6, 7),
            Effect              = new DropShadowEffect
            {
                BlurRadius  = 16,
                ShadowDepth = 3,
                Direction   = 270,
                Color       = Colors.Black,
                Opacity     = isDark ? 0.65 : 0.20
            }
        };

        var menuStack = new StackPanel();

        // Header: Heart icon + "Support LiteBright"
        var headerDock = new DockPanel { Margin = new Thickness(8, 4, 8, 7) };
        var heartIcon = new TextBlock
        {
            Text              = "\uEB52",
            FontFamily        = new FontFamily(iconFont),
            FontSize          = 13,
            Foreground        = new SolidColorBrush(Color.FromArgb(255, 255, 75, 110)),
            VerticalAlignment = VerticalAlignment.Center,
            Margin            = new Thickness(0, 0, 7, 0)
        };
        DockPanel.SetDock(heartIcon, Dock.Left);
        headerDock.Children.Add(heartIcon);

        var headerTitle = new TextBlock
        {
            Text              = LocalizationManager.T("SETTINGS_DONATE_TITLE", "Support LiteBright"),
            FontFamily        = new FontFamily(textFont),
            FontSize          = 12.5,
            FontWeight        = FontWeights.SemiBold,
            Foreground        = isDark ? Brushes.White : new SolidColorBrush(Color.FromArgb(255, 24, 24, 26)),
            VerticalAlignment = VerticalAlignment.Center
        };
        headerDock.Children.Add(headerTitle);
        menuStack.Children.Add(headerDock);

        // Divider
        menuStack.Children.Add(new Border
        {
            Height          = 1,
            Background      = isDark
                ? new SolidColorBrush(Color.FromArgb(28, 255, 255, 255))
                : new SolidColorBrush(Color.FromArgb(20, 0, 0, 0)),
            Margin          = new Thickness(4, 0, 4, 5)
        });

        // 1. Ko-fi
        menuStack.Children.Add(CreateDonateItem(
            badgeText: "☕",
            badgeBg: Color.FromArgb(40, 255, 94, 91),
            badgeFg: Color.FromArgb(255, 255, 94, 91),
            title: "Ko-fi",
            subtitle: "Tip or subscribe on Ko-fi",
            url: KofiUrl,
            isDark, textFont, iconFont));

        // 2. Buy Me a Coffee
        menuStack.Children.Add(CreateDonateItem(
            badgeText: "💛",
            badgeBg: Color.FromArgb(40, 245, 158, 11),
            badgeFg: Color.FromArgb(255, 245, 158, 11),
            title: "Buy Me a Coffee",
            subtitle: "Support via Buy Me a Coffee",
            url: BuyMeACoffeeUrl,
            isDark, textFont, iconFont));

        // 3. PayPal
        menuStack.Children.Add(CreateDonateItem(
            badgeText: "💳",
            badgeBg: Color.FromArgb(40, 0, 121, 193),
            badgeFg: Color.FromArgb(255, 0, 140, 220),
            title: "PayPal",
            subtitle: "Direct tip or donation",
            url: PayPalUrl,
            isDark, textFont, iconFont));

        menuBorder.Child = menuStack;
        _dropdownOverlay.Children.Add(menuBorder);
        _dropdownOverlay.Visibility = Visibility.Visible;
    }

    private UIElement CreateDonateItem(
        string badgeText,
        Color badgeBg,
        Color badgeFg,
        string title,
        string subtitle,
        string url,
        bool isDark,
        string textFont,
        string iconFont)
    {
        var itemBorder = new Border
        {
            Height          = 50,
            CornerRadius    = new CornerRadius(6),
            Margin          = new Thickness(2, 2, 2, 2),
            Background      = Brushes.Transparent,
            Cursor          = Cursors.Hand,
            Padding         = new Thickness(8, 0, 8, 0)
        };

        var dock = new DockPanel { LastChildFill = true };

        // Left Badge
        var badge = new Border
        {
            Width               = 30,
            Height              = 30,
            CornerRadius        = new CornerRadius(6),
            Background          = new SolidColorBrush(badgeBg),
            VerticalAlignment   = VerticalAlignment.Center,
            Margin              = new Thickness(0, 0, 10, 0)
        };
        var badgeTb = new TextBlock
        {
            Text                = badgeText,
            FontFamily          = new FontFamily("Segoe UI Emoji, Segoe UI"),
            FontSize            = 14,
            Foreground          = new SolidColorBrush(badgeFg),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Center
        };
        badge.Child = badgeTb;
        DockPanel.SetDock(badge, Dock.Left);
        dock.Children.Add(badge);

        // Right external link arrow (\uE8A7)
        var extIcon = new TextBlock
        {
            Text              = "\uE8A7",
            FontFamily        = new FontFamily(iconFont),
            FontSize          = 11,
            Foreground        = isDark
                ? new SolidColorBrush(Color.FromArgb(140, 255, 255, 255))
                : new SolidColorBrush(Color.FromArgb(140, 0, 0, 0)),
            VerticalAlignment = VerticalAlignment.Center,
            Margin            = new Thickness(8, 0, 0, 0)
        };
        DockPanel.SetDock(extIcon, Dock.Right);
        dock.Children.Add(extIcon);

        // Center: Title + Subtitle
        var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var titleTb = new TextBlock
        {
            Text         = title,
            FontFamily   = new FontFamily(textFont),
            FontSize     = 12.5,
            FontWeight   = FontWeights.SemiBold,
            Foreground   = isDark ? Brushes.White : new SolidColorBrush(Color.FromArgb(255, 24, 24, 26))
        };
        var subTb = new TextBlock
        {
            Text         = subtitle,
            FontFamily   = new FontFamily(textFont),
            FontSize     = 10.5,
            Foreground   = isDark
                ? new SolidColorBrush(Color.FromArgb(170, 200, 200, 205))
                : new SolidColorBrush(Color.FromArgb(170, 95, 95, 100)),
            Margin       = new Thickness(0, 1, 0, 0)
        };
        textStack.Children.Add(titleTb);
        textStack.Children.Add(subTb);
        dock.Children.Add(textStack);

        itemBorder.Child = dock;

        var hoverBg = isDark
            ? new SolidColorBrush(Color.FromArgb(32, 255, 255, 255))
            : new SolidColorBrush(Color.FromArgb(18, 0, 0, 0));

        itemBorder.MouseEnter += (_, _) => itemBorder.Background = hoverBg;
        itemBorder.MouseLeave += (_, _) => itemBorder.Background = Brushes.Transparent;

        itemBorder.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            CloseDropdownOverlay();
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName        = url,
                    UseShellExecute = true
                });
            }
            catch { }
        };

        return itemBorder;
    }

    private sealed class FluentComboBox : Border
    {
        private string _selectedItem;
        private readonly TextBlock _textBlock;

        public string SelectedItem => _selectedItem;

        public FluentComboBox(
            IEnumerable<string> items,
            string initialItem,
            bool isDark,
            Color accentColor,
            Action<string> onChanged,
            double controlWidth = 240,
            string iconGlyph = "\uE70D")
        {
            var itemList = items.ToList();
            string found = itemList.FirstOrDefault(i => string.Equals(i, initialItem, StringComparison.OrdinalIgnoreCase))
                        ?? itemList.FirstOrDefault(i => i.StartsWith(initialItem + " (", StringComparison.OrdinalIgnoreCase))
                        ?? itemList.FirstOrDefault() ?? "";
            _selectedItem = found;

            Width             = controlWidth;
            Height            = 32;
            BorderThickness   = new Thickness(1);
            BorderBrush       = isDark
                ? new SolidColorBrush(Color.FromArgb(38, 255, 255, 255))
                : new SolidColorBrush(Color.FromArgb(32, 0, 0, 0));
            CornerRadius      = new CornerRadius(4);
            Padding           = controlWidth < 80 ? new Thickness(8, 0, 6, 0) : new Thickness(12, 0, 10, 0);
            Background        = isDark
                ? new SolidColorBrush(Color.FromArgb(20, 255, 255, 255))
                : new SolidColorBrush(Color.FromArgb(14, 0, 0, 0));
            Cursor            = Cursors.Hand;
            VerticalAlignment = VerticalAlignment.Center;

            var dock = new DockPanel();

            var chevron = new TextBlock
            {
                Text              = iconGlyph,
                FontFamily        = new FontFamily(BrightnessPanel.IconFontName),
                FontSize          = iconGlyph == "\uE70D" ? 9 : 12,
                Foreground        = isDark
                    ? new SolidColorBrush(Color.FromArgb(180, 255, 255, 255))
                    : new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin            = controlWidth < 80 ? new Thickness(4, 0, 0, 0) : new Thickness(8, 0, 0, 0)
            };
            DockPanel.SetDock(chevron, Dock.Right);
            dock.Children.Add(chevron);

            _textBlock = new TextBlock
            {
                Text              = _selectedItem,
                FontFamily        = new FontFamily(BrightnessPanel.TextFontName),
                FontSize          = 12,
                Foreground        = isDark ? Brushes.White : Brushes.Black,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment     = controlWidth < 80 ? TextAlignment.Center : TextAlignment.Left,
                TextTrimming      = TextTrimming.CharacterEllipsis
            };
            dock.Children.Add(_textBlock);

            Child = dock;

            MouseEnter += (_, _) =>
            {
                Background = isDark
                    ? new SolidColorBrush(Color.FromArgb(32, 255, 255, 255))
                    : new SolidColorBrush(Color.FromArgb(24, 0, 0, 0));
            };
            MouseLeave += (_, _) =>
            {
                Background = isDark
                    ? new SolidColorBrush(Color.FromArgb(20, 255, 255, 255))
                    : new SolidColorBrush(Color.FromArgb(14, 0, 0, 0));
            };

            MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                _currentInstance?.ShowDropdownOverlay(
                    this,
                    itemList,
                    _selectedItem,
                    isDark,
                    accentColor,
                    chosen =>
                    {
                        _selectedItem = chosen;
                        _textBlock.Text = chosen;
                        onChanged(chosen);
                    });
            };
        }
    }

    private sealed class FluentToggleSwitch : Border
    {
        private bool _isChecked;
        private readonly Action<bool> _onChanged;
        private readonly Color _accentColor;
        private readonly bool _isDark;
        private readonly Border _thumb;
        private readonly Border _switchPill;
        private readonly TextBlock _statusText;

        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                _isChecked = value;
                UpdateVisuals();
                _onChanged?.Invoke(_isChecked);
            }
        }

        public FluentToggleSwitch(bool initialValue, Color accentColor, bool isDark, Action<bool> onChanged)
        {
            _isChecked   = initialValue;
            _accentColor = accentColor;
            _isDark      = isDark;
            _onChanged   = onChanged;

            Cursor = Cursors.Hand;
            Background = Brushes.Transparent;

            var dock = new DockPanel();

            _statusText = new TextBlock
            {
                Text              = _isChecked ? "On" : "Off",
                FontFamily        = new FontFamily(BrightnessPanel.TextFontName),
                FontSize          = 12,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment     = TextAlignment.Right,
                Width             = 24,
                Margin            = new Thickness(0, 0, 10, 0)
            };
            DockPanel.SetDock(_statusText, Dock.Left);
            dock.Children.Add(_statusText);

            _switchPill = new Border
            {
                Width             = 40,
                Height            = 20,
                CornerRadius      = new CornerRadius(10),
                BorderThickness   = new Thickness(1),
                VerticalAlignment = VerticalAlignment.Center
            };

            _thumb = new Border
            {
                Width        = 12,
                Height       = 12,
                CornerRadius = new CornerRadius(6)
            };
            _switchPill.Child = _thumb;
            dock.Children.Add(_switchPill);

            Child = dock;

            MouseLeftButtonUp += (_, _) => IsChecked = !IsChecked;

            MouseEnter += (_, _) =>
            {
                if (!_isChecked)
                {
                    _switchPill.BorderBrush = _isDark
                        ? new SolidColorBrush(Color.FromArgb(180, 255, 255, 255))
                        : new SolidColorBrush(Color.FromArgb(180, 0, 0, 0));
                }
            };
            MouseLeave += (_, _) => UpdateVisuals();

            UpdateVisuals();
        }

        private void UpdateVisuals()
        {
            _statusText.Text = _isChecked ? "On" : "Off";
            _statusText.Foreground = _isDark
                ? new SolidColorBrush(Color.FromArgb(220, 255, 255, 255))
                : new SolidColorBrush(Color.FromArgb(220, 24, 24, 24));

            if (_isChecked)
            {
                _switchPill.Background  = new SolidColorBrush(_accentColor);
                _switchPill.BorderBrush = new SolidColorBrush(_accentColor);
                _thumb.Background       = Brushes.White;
                _thumb.HorizontalAlignment = HorizontalAlignment.Right;
                _thumb.Margin           = new Thickness(0, 0, 3, 0);
            }
            else
            {
                _switchPill.Background  = _isDark
                    ? new SolidColorBrush(Color.FromArgb(20, 255, 255, 255))
                    : new SolidColorBrush(Color.FromArgb(15, 0, 0, 0));
                _switchPill.BorderBrush = _isDark
                    ? new SolidColorBrush(Color.FromArgb(120, 255, 255, 255))
                    : new SolidColorBrush(Color.FromArgb(120, 0, 0, 0));
                _thumb.Background       = _isDark
                    ? new SolidColorBrush(Color.FromArgb(220, 255, 255, 255))
                    : new SolidColorBrush(Color.FromArgb(200, 0, 0, 0));
                _thumb.HorizontalAlignment = HorizontalAlignment.Left;
                _thumb.Margin           = new Thickness(3, 0, 0, 0);
            }
        }
    }

    private sealed class FluentStepper : Border
    {
        private int _value;
        private readonly TextBlock _valText;

        public FluentStepper(int initialValue, int min, int max, bool isDark, Color accentColor, Action<int> onChanged)
        {
            _value = Math.Clamp(initialValue, min, max);

            var borderCol = isDark
                ? new SolidColorBrush(Color.FromArgb(28, 255, 255, 255))
                : new SolidColorBrush(Color.FromArgb(24, 0, 0, 0));

            var fgCol = isDark
                ? new SolidColorBrush(Color.FromArgb(255, 255, 255, 255))
                : new SolidColorBrush(Color.FromArgb(255, 24, 24, 24));

            BorderBrush     = borderCol;
            BorderThickness = new Thickness(1);
            CornerRadius    = new CornerRadius(4);
            Height          = 30;
            Background      = isDark
                ? new SolidColorBrush(Color.FromArgb(24, 255, 255, 255))
                : new SolidColorBrush(Color.FromArgb(14, 0, 0, 0));

            var stack = new StackPanel { Orientation = Orientation.Horizontal };

            _valText = new TextBlock
            {
                Text              = $"{_value}%",
                FontFamily        = new FontFamily(BrightnessPanel.TextFontName),
                FontSize          = 12.5,
                FontWeight        = FontWeights.SemiBold,
                Foreground        = fgCol,
                Width             = 42,
                TextAlignment     = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            var btnMinus = MakeStepperBtn("−", isDark, fgCol, () =>
            {
                if (_value > min)
                {
                    _value--;
                    _valText.Text = $"{_value}%";
                    onChanged(_value);
                }
            });
            stack.Children.Add(btnMinus);
            stack.Children.Add(_valText);

            var btnPlus = MakeStepperBtn("+", isDark, fgCol, () =>
            {
                if (_value < max)
                {
                    _value++;
                    _valText.Text = $"{_value}%";
                    onChanged(_value);
                }
            });
            stack.Children.Add(btnPlus);

            Child = stack;
        }

        private static Border MakeStepperBtn(string glyph, bool isDark, Brush fgCol, Action onClick)
        {
            var btn = new Border
            {
                Width        = 26,
                Height       = 26,
                CornerRadius = new CornerRadius(3),
                Background   = Brushes.Transparent,
                Cursor       = Cursors.Hand
            };

            var tb = new TextBlock
            {
                Text              = glyph,
                FontFamily        = new FontFamily("Segoe UI Variable Display"),
                FontSize          = 13.5,
                FontWeight        = FontWeights.Bold,
                Foreground        = fgCol,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center
            };
            btn.Child = tb;

            btn.MouseEnter += (_, _) =>
            {
                btn.Background = isDark
                    ? new SolidColorBrush(Color.FromArgb(30, 255, 255, 255))
                    : new SolidColorBrush(Color.FromArgb(20, 0, 0, 0));
            };
            btn.MouseLeave += (_, _) => btn.Background = Brushes.Transparent;
            btn.MouseLeftButtonUp += (_, _) => onClick();

            return btn;
        }
    }

    private sealed class HotkeyBinder
    {
        public FrameworkElement Element { get; }
        private HotkeyDefinition _definition;
        private readonly HotkeyAction _action;
        private readonly bool _isDark;
        private readonly Color _accentColor;
        private readonly Border _badgeBorder;
        private readonly TextBlock _badgeText;
        private readonly Border _clearBtn;
        private readonly Action? _onChanged;
        private bool _isRecording;

        public HotkeyDefinition GetDefinition() => _definition;

        public HotkeyBinder(HotkeyAction action, HotkeyDefinition definition, bool isUp,
            bool isDark, Color accentColor, string textFont, string iconFont, Action? onChanged = null)
        {
            _action      = action;
            _definition  = definition;
            _isDark      = isDark;
            _accentColor = accentColor;
            _onChanged   = onChanged;

            var dock = new DockPanel { Margin = new Thickness(14, 8, 14, 8) };

            var iconLabel = new TextBlock
            {
                Text              = isUp ? "\uE706" : "\uE708",
                FontFamily        = new FontFamily(iconFont),
                FontSize          = 13.5,
                Foreground        = isUp
                    ? new SolidColorBrush(Color.FromArgb(235, 52, 211, 153))
                    : new SolidColorBrush(Color.FromArgb(235, 248, 113, 113)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin            = new Thickness(0, 0, 10, 0)
            };
            DockPanel.SetDock(iconLabel, Dock.Left);
            dock.Children.Add(iconLabel);

            var titleText = new TextBlock
            {
                Text              = isUp ? LocalizationManager.T("SETTINGS_HOTKEYS_INCREASE", "Increase brightness") : LocalizationManager.T("SETTINGS_HOTKEYS_DECREASE", "Decrease brightness"),
                FontFamily        = new FontFamily(textFont),
                FontSize          = 12.5,
                Foreground        = isDark
                    ? new SolidColorBrush(Color.FromArgb(240, 240, 240, 245))
                    : new SolidColorBrush(Color.FromArgb(240, 26, 26, 28)),
                VerticalAlignment = VerticalAlignment.Center
            };
            dock.Children.Add(titleText);

            var rightStack = new StackPanel
            {
                Orientation       = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            _badgeBorder = new Border
            {
                Height          = 28,
                MinWidth        = 85,
                Padding         = new Thickness(10, 0, 10, 0),
                CornerRadius    = new CornerRadius(4),
                Cursor          = Cursors.Hand,
                Focusable       = true,
                Margin          = new Thickness(0, 0, 6, 0)
            };

            _badgeText = new TextBlock
            {
                Text              = GetShortcutDisplay(),
                FontFamily        = new FontFamily(textFont),
                FontSize          = 11.5,
                FontWeight        = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center
            };
            _badgeBorder.Child = _badgeText;

            _badgeBorder.MouseLeftButtonUp += (s, e) =>
            {
                _isRecording = true;
                _badgeBorder.Focus();
                UpdateBadgeStyle();
            };

            _badgeBorder.LostFocus += (s, e) =>
            {
                _isRecording = false;
                UpdateBadgeStyle();
            };

            _badgeBorder.PreviewKeyDown += (s, e) =>
            {
                if (!_isRecording) return;
                e.Handled = true;

                if (e.Key == Key.Escape)
                {
                    _isRecording = false;
                    UpdateBadgeStyle();
                    return;
                }

                HotkeyModifiers mods = HotkeyModifiers.None;
                if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)) mods |= HotkeyModifiers.Ctrl;
                if (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt)) mods |= HotkeyModifiers.Alt;
                if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)) mods |= HotkeyModifiers.Shift;
                if (Keyboard.IsKeyDown(Key.LWin) || Keyboard.IsKeyDown(Key.RWin)) mods |= HotkeyModifiers.Win;

                Key actualKey = e.Key == Key.System ? e.SystemKey : e.Key;

                if (actualKey is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
                    or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
                {
                    _badgeText.Text = "Press key...";
                    return;
                }

                int vk = KeyInterop.VirtualKeyFromKey(actualKey);
                var wfKey = (Keys)vk;

                _definition = new HotkeyDefinition
                {
                    Action    = _action,
                    Modifiers = mods,
                    Key       = wfKey
                };

                _isRecording = false;
                UpdateBadgeStyle();
                _onChanged?.Invoke();
            };

            rightStack.Children.Add(_badgeBorder);

            _clearBtn = new Border
            {
                Width           = 28,
                Height          = 28,
                CornerRadius    = new CornerRadius(4),
                Background      = Brushes.Transparent,
                Cursor          = Cursors.Hand,
                ToolTip         = "Clear shortcut"
            };
            var clearGlyph = new TextBlock
            {
                Text              = "\uE711",
                FontFamily        = new FontFamily(iconFont),
                FontSize          = 11,
                Foreground        = isDark
                    ? new SolidColorBrush(Color.FromArgb(170, 200, 200, 210))
                    : new SolidColorBrush(Color.FromArgb(170, 100, 100, 110)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center
            };
            _clearBtn.Child = clearGlyph;

            _clearBtn.MouseEnter += (_, _) =>
            {
                _clearBtn.Background = isDark
                    ? new SolidColorBrush(Color.FromArgb(30, 255, 255, 255))
                    : new SolidColorBrush(Color.FromArgb(20, 0, 0, 0));
            };
            _clearBtn.MouseLeave += (_, _) => _clearBtn.Background = Brushes.Transparent;
            _clearBtn.MouseLeftButtonUp += (_, _) =>
            {
                _definition = new HotkeyDefinition { Action = _action };
                UpdateBadgeStyle();
                _onChanged?.Invoke();
            };

            rightStack.Children.Add(_clearBtn);
            DockPanel.SetDock(rightStack, Dock.Right);
            dock.Children.Add(rightStack);

            Element = dock;
            UpdateBadgeStyle();
        }

        private string GetShortcutDisplay()
        {
            if (_isRecording) return LocalizationManager.T("SETTINGS_HOTKEYS_PRESS_KEYS_HINT", "Press keys...");
            return _definition.IsValid ? _definition.ToString() : "(none)";
        }

        private void UpdateBadgeStyle()
        {
            _badgeText.Text = GetShortcutDisplay();

            if (_isRecording)
            {
                _badgeBorder.Background  = _isDark
                    ? new SolidColorBrush(Color.FromArgb(40, 255, 255, 255))
                    : new SolidColorBrush(Color.FromArgb(25, 0, 0, 0));
                _badgeBorder.BorderBrush = new SolidColorBrush(_accentColor);
                _badgeBorder.BorderThickness = new Thickness(1.5);
                _badgeText.Foreground    = new SolidColorBrush(_accentColor);
            }
            else if (_definition.IsValid)
            {
                _badgeBorder.Background  = _isDark
                    ? new SolidColorBrush(Color.FromArgb(35, 255, 255, 255))
                    : new SolidColorBrush(Color.FromArgb(20, 0, 0, 0));
                _badgeBorder.BorderBrush = _isDark
                    ? new SolidColorBrush(Color.FromArgb(50, 255, 255, 255))
                    : new SolidColorBrush(Color.FromArgb(40, 0, 0, 0));
                _badgeBorder.BorderThickness = new Thickness(1);
                _badgeText.Foreground    = _isDark
                    ? new SolidColorBrush(Color.FromArgb(255, 255, 255, 255))
                    : new SolidColorBrush(Color.FromArgb(255, 24, 24, 24));
            }
            else
            {
                _badgeBorder.Background  = _isDark
                    ? new SolidColorBrush(Color.FromArgb(15, 255, 255, 255))
                    : new SolidColorBrush(Color.FromArgb(10, 0, 0, 0));
                _badgeBorder.BorderBrush = _isDark
                    ? new SolidColorBrush(Color.FromArgb(30, 255, 255, 255))
                    : new SolidColorBrush(Color.FromArgb(25, 0, 0, 0));
                _badgeBorder.BorderThickness = new Thickness(1);
                _badgeText.Foreground    = _isDark
                    ? new SolidColorBrush(Color.FromArgb(255, 160, 160, 165))
                    : new SolidColorBrush(Color.FromArgb(255, 140, 140, 150));
            }
        }
    }
}