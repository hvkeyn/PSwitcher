#if !WINDOWS
using System.Runtime.InteropServices;
using System.Text;

namespace Switcher;

internal static partial class Native
{
    internal static IntPtr Display;      // queries + injection + grabs
    internal static IntPtr RecordDisplay; // XRecord control connection
    internal static readonly object XLock = new();
    internal static volatile int Injecting;
    private static nint _netWmPid;
    private static nint _root;
    private static IntPtr[] _layouts = Array.Empty<IntPtr>();
    private static readonly byte[] _keymap = new byte[32];

    public static bool TryConnect()
    {
        lock (XLock)
        {
            if (Display != IntPtr.Zero) return true;
            try
            {
                X11.XInitThreads();
                Display = X11.XOpenDisplay(IntPtr.Zero);
                RecordDisplay = X11.XOpenDisplay(IntPtr.Zero);
            }
            catch (DllNotFoundException ex)
            {
                Log.Write("X11 libraries missing (libX11.so.6 / libXtst.so.6): " + ex.Message);
                return false;
            }
            if (Display == IntPtr.Zero || RecordDisplay == IntPtr.Zero)
            {
                Log.Write("XOpenDisplay failed — is DISPLAY set? Native Wayland apps are not visible; use X11 or XWayland.");
                return false;
            }
            _root = X11.XDefaultRootWindow(Display);
            _netWmPid = X11.XInternAtom(Display, "_NET_WM_PID", 1);
            RefreshLayouts();
            return true;
        }
    }

    public static void Disconnect()
    {
        lock (XLock)
        {
            if (RecordDisplay != IntPtr.Zero) { X11.XCloseDisplay(RecordDisplay); RecordDisplay = IntPtr.Zero; }
            if (Display != IntPtr.Zero) { X11.XCloseDisplay(Display); Display = IntPtr.Zero; }
        }
    }

    public static IntPtr GetForegroundWindow()
    {
        if (Display == IntPtr.Zero) return IntPtr.Zero;
        lock (XLock)
        {
            X11.XGetInputFocus(Display, out nint focus, out _);
            if (focus == 0 || focus == 1) return IntPtr.Zero; // PointerRoot / None
            return (IntPtr)focus;
        }
    }

    public static uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid)
    {
        pid = 0;
        if (Display == IntPtr.Zero || hWnd == IntPtr.Zero) return 0;
        lock (XLock)
        {
            nint w = (nint)hWnd;
            for (int hop = 0; hop < 16 && w != 0 && w != _root; hop++)
            {
                if (_netWmPid != 0 &&
                    X11.XGetWindowProperty(Display, w, _netWmPid, 0, 1, 0, 0 /*AnyPropertyType*/,
                        out _, out int fmt, out nuint nitems, out _, out IntPtr prop) == 0 && prop != IntPtr.Zero)
                {
                    if (fmt == 32 && nitems >= 1) pid = (uint)Marshal.ReadInt32(prop);
                    X11.XFree(prop);
                    if (pid != 0) return pid;
                }
                X11.XQueryTree(Display, w, out _, out nint parent, out IntPtr children, out _);
                if (children != IntPtr.Zero) X11.XFree(children);
                if (parent == 0 || parent == w) break;
                w = parent;
            }
        }
        return pid;
    }

    public static IntPtr GetKeyboardLayout(uint _)
    {
        if (Display == IntPtr.Zero) return Fake(0, Dictionaries.LangEn);
        lock (XLock)
        {
            var st = new X11.XkbStateRec();
            X11.XkbGetState(Display, X11.XkbUseCoreKbd, ref st);
            foreach (var h in _layouts)
                if (((long)h >> 16) == st.group) return h;
            return _layouts.Length > 0 ? _layouts[Math.Min(st.group, _layouts.Length - 1)] : Fake(st.group, Dictionaries.LangEn);
        }
    }

    public static int GetKeyboardLayoutList(int nBuff, IntPtr[]? lpList)
    {
        if (_layouts.Length == 0) RefreshLayouts();
        if (lpList == null) return _layouts.Length;
        int n = Math.Min(nBuff, _layouts.Length);
        Array.Copy(_layouts, lpList, n);
        return n;
    }

    internal static void RefreshLayouts()
    {
        var found = new List<IntPtr>();
        if (Display != IntPtr.Zero)
        {
            try
            {
                lock (XLock)
                {
                    IntPtr kb = X11.XkbGetKeyboard(Display, X11.XkbGroupNamesMask, X11.XkbUseCoreKbd);
                    if (kb != IntPtr.Zero)
                    {
                        X11.XkbGetNames(Display, X11.XkbGroupNamesMask, kb);
                        // XkbDescRec: first field Display*, then flags, device_spec, min/max keycode, ctrls*, server*, map*, indicators*, names*, compat*, geom*
                        // names is at a stable-enough offset on 64-bit: skip 8+4+2+1+1 + padding + 6 pointers before names? Too fragile.
                        // Fallback: assume group 0 = first US/EN, group 1 = RU if two groups, via setxkbmap -query.
                        X11.XkbFreeKeyboard(kb, 0, 1);
                    }
                }
            }
            catch { /* parsed below */ }
        }
        foreach (var (group, lang) in ParseSetxkbmap())
            found.Add(Fake(group, lang));
        if (found.Count == 0)
        {
            found.Add(Fake(0, Dictionaries.LangEn));
            found.Add(Fake(1, Dictionaries.LangRu));
        }
        _layouts = found.ToArray();
    }

    private static List<(int group, int lang)> ParseSetxkbmap()
    {
        var r = new List<(int, int)>();
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo("setxkbmap", "-query")
            {
                RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true,
            };
            using var p = System.Diagnostics.Process.Start(psi);
            string? output = p?.StandardOutput.ReadToEnd();
            p?.WaitForExit(1000);
            if (output == null) return r;
            foreach (var line in output.Split('\n'))
            {
                var t = line.Trim();
                if (!t.StartsWith("layout:", StringComparison.OrdinalIgnoreCase)) continue;
                var layouts = t.Split(':', 2)[1].Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < layouts.Length; i++)
                {
                    var name = layouts[i].ToLowerInvariant();
                    int lang = name.StartsWith("ru") || name.Contains("russian") ? Dictionaries.LangRu
                             : name.StartsWith("us") || name.StartsWith("gb") || name.Contains("en") ? Dictionaries.LangEn
                             : 0;
                    if (lang != 0) r.Add((i, lang));
                }
            }
        }
        catch { }
        return r;
    }

    internal static IntPtr Fake(int group, int lang) => (IntPtr)((group << 16) | lang);
    internal static int GroupOf(IntPtr hkl) => (int)((long)hkl >> 16);

    public static int ToUnicodeEx(uint wVirtKey, uint wScanCode, byte[] lpKeyState,
        StringBuilder pwszBuff, int cchBuff, uint wFlags, IntPtr dwhkl)
    {
        bool shift = lpKeyState is { Length: > Native.VK_SHIFT } && (lpKeyState[VK_SHIFT] & 0x80) != 0;
        bool caps = lpKeyState is { Length: > Native.VK_CAPITAL } && (lpKeyState[VK_CAPITAL] & 0x01) != 0;
        char ch = KeyMap.ToChar(LangId(dwhkl), wVirtKey, shift, caps);
        if (ch == '\0') return 0;
        pwszBuff.Clear();
        pwszBuff.Append(ch);
        return 1;
    }

    public static short VkKeyScanExW(char ch, IntPtr dwhkl) => KeyMap.VkScan(LangId(dwhkl), ch);

    public static uint MapVirtualKeyEx(uint uCode, uint uMapType, IntPtr dwhkl) => KeyMap.XKeycode(uCode);

    public static bool AttachConsole(int _) => true;

    public static bool IsDown(int vk)
    {
        if (Display == IntPtr.Zero) return false;
        lock (XLock)
        {
            X11.XQueryKeymap(Display, _keymap);
            return vk switch
            {
                VK_SHIFT => KeyDown(50) || KeyDown(62),
                VK_CONTROL => KeyDown(37) || KeyDown(105),
                VK_MENU => KeyDown(64) || KeyDown(108),
                _ => KeyDown(KeyMap.XKeycode((uint)vk)),
            };
        }
    }

    private static bool KeyDown(uint code) => code != 0 && (_keymap[code >> 3] & (1 << ((int)code & 7))) != 0;

    public static bool IsToggled(int vk)
    {
        if (vk != VK_CAPITAL || Display == IntPtr.Zero) return false;
        lock (XLock)
        {
            var st = new X11.XkbStateRec();
            X11.XkbGetState(Display, X11.XkbUseCoreKbd, ref st);
            return (st.locked_mods & X11.LockMask) != 0;
        }
    }

    public static void GrabHotkey(Hotkey hk)
    {
        uint code = KeyMap.XKeycode(hk.Vk);
        if (Display == IntPtr.Zero || code == 0) return;
        uint mods = 0;
        if (hk.Shift) mods |= X11.ShiftMask;
        if (hk.Ctrl) mods |= X11.ControlMask;
        if (hk.Alt) mods |= X11.Mod1Mask;
        if (hk.Win) mods |= X11.Mod4Mask;
        lock (XLock)
        {
            // also grab with CapsLock / NumLock so the key still works when those are on
            uint[] extra = { 0, X11.LockMask, X11.Mod2Mask, X11.LockMask | X11.Mod2Mask };
            foreach (var e in extra)
                X11.XGrabKey(Display, (int)code, mods | e, _root, 1, X11.GrabModeAsync, X11.GrabModeAsync);
            X11.XFlush(Display);
        }
    }

    public static void UngrabHotkey(uint vk)
    {
        uint code = KeyMap.XKeycode(vk);
        if (Display == IntPtr.Zero || code == 0) return;
        lock (XLock)
        {
            X11.XUngrabKey(Display, (int)code, X11.AnyModifier, _root);
            X11.XFlush(Display);
        }
    }
}
#endif
