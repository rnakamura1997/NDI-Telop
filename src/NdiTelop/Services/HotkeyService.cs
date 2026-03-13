using System.Runtime.InteropServices;
using Avalonia.Threading;
using NdiTelop.Models;
using Serilog;

namespace NdiTelop.Services;

public enum HotkeyAction
{
    Preset1,
    Preset2,
    Preset3,
    Preset4,
    Preset5,
    ClearProgram
}

public sealed class HotkeyService : IDisposable
{
    private readonly Dictionary<int, HotkeyAction> _actionsById = new();
    private readonly Dictionary<HotkeyAction, string> _bindings = new();
    private int _nextId = 1;
    private IHotkeyPlatform? _platform;

    public event Action<HotkeyAction>? HotkeyPressed;

    public IReadOnlyDictionary<HotkeyAction, string> ActiveBindings => _bindings;

    public void ApplySettings(HotkeySettings settings)
    {
        Stop();

        _platform = CreatePlatform();
        if (_platform == null)
        {
            Log.Warning("Global hotkey is not supported on this platform yet.");
            return;
        }

        Register(settings.Preset1, HotkeyAction.Preset1);
        Register(settings.Preset2, HotkeyAction.Preset2);
        Register(settings.Preset3, HotkeyAction.Preset3);
        Register(settings.Preset4, HotkeyAction.Preset4);
        Register(settings.Preset5, HotkeyAction.Preset5);
        Register(settings.ClearProgram, HotkeyAction.ClearProgram);

        _platform.Start(OnHotkeyPressed);
    }

    public void Stop()
    {
        _platform?.Dispose();
        _platform = null;
        _actionsById.Clear();
        _bindings.Clear();
        _nextId = 1;
    }

    private void Register(string gestureText, HotkeyAction action)
    {
        if (_platform == null || !HotkeyGesture.TryParse(gestureText, out var gesture))
        {
            return;
        }

        var id = _nextId++;
        if (_platform.RegisterHotkey(id, gesture))
        {
            _actionsById[id] = action;
            _bindings[action] = gestureText;
        }
    }

    private void OnHotkeyPressed(int id)
    {
        if (!_actionsById.TryGetValue(id, out var action))
        {
            return;
        }

        Dispatcher.UIThread.Post(() => HotkeyPressed?.Invoke(action));
    }

    private static IHotkeyPlatform? CreatePlatform()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return new WindowsHotkeyPlatform();
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return new LinuxX11HotkeyPlatform();
        }

        return null;
    }

    public void Dispose() => Stop();
}

internal interface IHotkeyPlatform : IDisposable
{
    bool RegisterHotkey(int id, HotkeyGesture gesture);
    void Start(Action<int> onPressed);
}

[Flags]
internal enum HotkeyModifiers
{
    None = 0,
    Alt = 1,
    Control = 2,
    Shift = 4,
    Win = 8
}

internal readonly record struct HotkeyGesture(HotkeyModifiers Modifiers, uint Key)
{
    public static bool TryParse(string? raw, out HotkeyGesture gesture)
    {
        gesture = default;
        if (string.IsNullOrWhiteSpace(raw)) return false;

        var tokens = raw.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0) return false;

        HotkeyModifiers modifiers = HotkeyModifiers.None;
        uint key = 0;

        foreach (var token in tokens)
        {
            switch (token.ToLowerInvariant())
            {
                case "ctrl":
                case "control":
                    modifiers |= HotkeyModifiers.Control;
                    continue;
                case "alt":
                    modifiers |= HotkeyModifiers.Alt;
                    continue;
                case "shift":
                    modifiers |= HotkeyModifiers.Shift;
                    continue;
                case "win":
                case "meta":
                case "super":
                    modifiers |= HotkeyModifiers.Win;
                    continue;
            }

            key = ParseKey(token);
        }

        if (key == 0) return false;

        gesture = new HotkeyGesture(modifiers, key);
        return true;
    }

    private static uint ParseKey(string token)
    {
        if (token.Length == 1)
        {
            var ch = char.ToUpperInvariant(token[0]);
            if (char.IsLetterOrDigit(ch))
            {
                return ch;
            }
        }

        if (token.StartsWith('F') && int.TryParse(token.AsSpan(1), out var fKey) && fKey is >= 1 and <= 12)
        {
            return (uint)(0x70 + (fKey - 1));
        }

        return 0;
    }
}

internal sealed class WindowsHotkeyPlatform : IHotkeyPlatform
{
    private Action<int>? _onPressed;
    private Thread? _thread;
    private uint _threadId;

    public bool RegisterHotkey(int id, HotkeyGesture gesture)
        => RegisterHotKey(IntPtr.Zero, id, (uint)gesture.Modifiers, gesture.Key);

    public void Start(Action<int> onPressed)
    {
        _onPressed = onPressed;
        _thread = new Thread(MessageLoop) { IsBackground = true, Name = "HotkeyWin32Loop" };
        _thread.Start();
    }

    private void MessageLoop()
    {
        _threadId = GetCurrentThreadId();
        while (GetMessage(out var msg, IntPtr.Zero, 0, 0))
        {
            if (msg.message == 0x0312)
            {
                _onPressed?.Invoke((int)msg.wParam);
            }
        }
    }

    public void Dispose()
    {
        if (_threadId != 0)
        {
            PostThreadMessage(_threadId, 0x0012, IntPtr.Zero, IntPtr.Zero);
        }

        _thread?.Join(TimeSpan.FromSeconds(1));
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool GetMessage(out WinMessage lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PostThreadMessage(uint idThread, uint msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct WinMessage
    {
        public IntPtr hWnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public int ptX;
        public int ptY;
    }
}

internal sealed class LinuxX11HotkeyPlatform : IHotkeyPlatform
{
    private IntPtr _display;
    private Action<int>? _onPressed;
    private readonly Dictionary<uint, int> _actionByKeycode = new();

    public bool RegisterHotkey(int id, HotkeyGesture gesture)
    {
        _display = _display == IntPtr.Zero ? XOpenDisplay(IntPtr.Zero) : _display;
        if (_display == IntPtr.Zero)
        {
            return false;
        }

        var root = XDefaultRootWindow(_display);
        var keysym = gesture.Key;
        var keycode = XKeysymToKeycode(_display, keysym);
        if (keycode == 0)
        {
            return false;
        }

        var modifiers = (uint)gesture.Modifiers;
        XGrabKey(_display, keycode, modifiers, root, true, 1, 1);
        XSelectInput(_display, root, 1);
        _actionByKeycode[keycode] = id;
        return true;
    }

    public void Start(Action<int> onPressed)
    {
        _onPressed = onPressed;
        if (_display == IntPtr.Zero)
        {
            return;
        }

        var thread = new Thread(EventLoop) { IsBackground = true, Name = "HotkeyX11Loop" };
        thread.Start();
    }

    private void EventLoop()
    {
        while (_display != IntPtr.Zero)
        {
            XNextEvent(_display, out var evt);
            if (evt.type == 2 && _actionByKeycode.TryGetValue((uint)evt.keycode, out var id))
            {
                _onPressed?.Invoke(id);
            }
        }
    }

    public void Dispose()
    {
        if (_display != IntPtr.Zero)
        {
            XCloseDisplay(_display);
            _display = IntPtr.Zero;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XKeyEvent
    {
        public int type;
        public IntPtr serial;
        public int sendEvent;
        public IntPtr display;
        public IntPtr window;
        public IntPtr root;
        public IntPtr subwindow;
        public IntPtr time;
        public int x;
        public int y;
        public int xRoot;
        public int yRoot;
        public uint state;
        public uint keycode;
        public int sameScreen;
    }

    [DllImport("libX11")]
    private static extern IntPtr XOpenDisplay(IntPtr display);

    [DllImport("libX11")]
    private static extern IntPtr XDefaultRootWindow(IntPtr display);

    [DllImport("libX11")]
    private static extern uint XKeysymToKeycode(IntPtr display, uint keysym);

    [DllImport("libX11")]
    private static extern int XGrabKey(IntPtr display, uint keycode, uint modifiers, IntPtr grabWindow, bool ownerEvents, int pointerMode, int keyboardMode);

    [DllImport("libX11")]
    private static extern int XSelectInput(IntPtr display, IntPtr window, long eventMask);

    [DllImport("libX11")]
    private static extern int XNextEvent(IntPtr display, out XKeyEvent xevent);

    [DllImport("libX11")]
    private static extern int XCloseDisplay(IntPtr display);
}
