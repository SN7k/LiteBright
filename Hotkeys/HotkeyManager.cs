using BrightnessController.Native;

namespace BrightnessController.Hotkeys;

public sealed class HotkeyManager : IDisposable
{

    public event Action<HotkeyDefinition>? HotkeyPressed;


    private readonly HotkeyWindow           _window;
    private readonly SynchronizationContext _uiContext;
    private readonly Dictionary<int, HotkeyDefinition> _registered = new();
    private bool _disposed;


    public HotkeyManager()
    {
        if (SynchronizationContext.Current == null)
        {
            SynchronizationContext.SetSynchronizationContext(new System.Windows.Forms.WindowsFormsSynchronizationContext());
        }
        _uiContext = SynchronizationContext.Current!;

        _window = new HotkeyWindow();
        _window.HotkeyReceived += OnHotkeyReceived;
    }


    public bool Register(HotkeyDefinition def)
    {
        if (!def.IsValid) return false;

        Unregister(def.Action);

        bool ok = NativeMethods.RegisterHotKey(
            _window.Handle,
            def.Id,
            (uint)def.Modifiers | NativeMethods.MOD_NOREPEAT,
            (uint)def.Key);

        if (ok)
            _registered[def.Id] = def;

        return ok;
    }

    public void Unregister(HotkeyAction action)
    {
        int id = (int)action + 1000;
        if (_registered.ContainsKey(id))
        {
            NativeMethods.UnregisterHotKey(_window.Handle, id);
            _registered.Remove(id);
        }
    }

    public void ApplyBindings(IEnumerable<HotkeyDefinition> definitions)
    {
        UnregisterAll();
        foreach (var def in definitions)
            Register(def);
    }

    public void UnregisterAll()
    {
        foreach (var id in _registered.Keys.ToList())
            NativeMethods.UnregisterHotKey(_window.Handle, id);
        _registered.Clear();
    }


    private void OnHotkeyReceived(int id)
    {
        if (!_registered.TryGetValue(id, out var def)) return;

        _uiContext.Post(_ => HotkeyPressed?.Invoke(def), null);
    }


    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        UnregisterAll();
        _window.DestroyHandle();
    }

    private sealed class HotkeyWindow : NativeWindow
    {
        public event Action<int>? HotkeyReceived;

        public HotkeyWindow()
        {
            var cp = new CreateParams
            {
                Parent = new IntPtr(-3)
            };
            CreateHandle(cp);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == NativeMethods.WM_HOTKEY)
            {
                int id = m.WParam.ToInt32();
                HotkeyReceived?.Invoke(id);
                return;
            }
            base.WndProc(ref m);
        }
    }
}
