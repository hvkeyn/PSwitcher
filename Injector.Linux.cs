#if !WINDOWS
namespace Switcher;

/// <summary>XTest typing + XkbLockGroup. Unicode is typed as physical keys in the target layout.</summary>
public static class Injector
{
    public static readonly IntPtr Signature = (IntPtr)0x4C46_4958;
    public static readonly IntPtr TriggerSignature = (IntPtr)0x4C46_5452;

    public static void SendTrigger()
    {
        // No dummy key: XRecord does not block the input thread. Deferred work can run now.
    }

    public static void SwitchLayout(IntPtr hwnd, IntPtr hkl)
    {
        if (hkl == IntPtr.Zero || Native.Display == IntPtr.Zero) return;
        int group = Native.GroupOf(hkl);
        lock (Native.XLock)
        {
            X11.XkbLockGroup(Native.Display, X11.XkbUseCoreKbd, (uint)group);
            X11.XSync(Native.Display, 0);
        }
    }

    public static void Replace(int backspaces, string text, int trailingVk = 0)
    {
        if (Native.Display == IntPtr.Zero) return;
        Interlocked.Increment(ref Native.Injecting);
        try
        {
            lock (Native.XLock)
            {
                var held = HeldModifiers();
                foreach (var kc in held) Fake(kc, false);
                for (int i = 0; i < backspaces; i++) Fake(22, true, false); // BackSpace
                foreach (var ch in text)
                {
                    if (ch == '\r') continue;
                    if (ch == '\n') { Fake(36, true, false); continue; }
                    if (ch == '\t') { Fake(23, true, false); continue; }
                    TypeChar(ch);
                }
                if (trailingVk != 0)
                {
                    uint kc = KeyMap.XKeycode((uint)trailingVk);
                    if (kc != 0) Fake(kc, true, false);
                }
                foreach (var kc in held) Fake(kc, true);
                X11.XSync(Native.Display, 0);
            }
        }
        finally { Interlocked.Decrement(ref Native.Injecting); }
    }

    public static void PressKey(int vk)
    {
        uint kc = KeyMap.XKeycode((uint)vk);
        if (kc == 0 || Native.Display == IntPtr.Zero) return;
        Interlocked.Increment(ref Native.Injecting);
        try
        {
            lock (Native.XLock)
            {
                Fake(kc, true, false);
                X11.XSync(Native.Display, 0);
            }
        }
        finally { Interlocked.Decrement(ref Native.Injecting); }
    }

    public static void SendToggleHotkey()
    {
        if (Native.Display == IntPtr.Zero) return;
        var all = Layouts.Installed();
        if (all.Length == 0) return;
        var cur = Layouts.Current(Native.GetForegroundWindow());
        int i = Array.IndexOf(all, cur);
        var next = all[(i + 1) % all.Length];
        SwitchLayout(IntPtr.Zero, next);
    }

    public static IntPtr FocusWindow(IntPtr hwnd) => hwnd;

    private static void TypeChar(char ch)
    {
        var hkl = Layouts.Current(Native.GetForegroundWindow());
        short scan = Native.VkKeyScanExW(ch, hkl);
        if (scan == -1)
        {
            // try the other layout's physical keys after locking that group
            var other = Layouts.Other(hkl);
            if (other != IntPtr.Zero)
            {
                scan = Native.VkKeyScanExW(ch, other);
                if (scan != -1) X11.XkbLockGroup(Native.Display, X11.XkbUseCoreKbd, (uint)Native.GroupOf(other));
            }
        }
        if (scan == -1) return;
        uint vk = (uint)(scan & 0xFF);
        bool shift = (scan & 0x100) != 0;
        uint kc = KeyMap.XKeycode(vk);
        if (kc == 0) return;
        if (shift) Fake(50, true);
        Fake(kc, true, false);
        if (shift) Fake(50, false);
    }

    private static void Fake(uint keycode, bool down, bool upToo = true)
    {
        X11.XTestFakeKeyEvent(Native.Display, keycode, down ? 1 : 0, 0);
        if (upToo && down) X11.XTestFakeKeyEvent(Native.Display, keycode, 0, 0);
    }

    private static List<uint> HeldModifiers()
    {
        var held = new List<uint>();
        var map = new byte[32];
        X11.XQueryKeymap(Native.Display, map);
        foreach (uint kc in new uint[] { 37, 105, 64, 108, 133, 134, 50, 62 })
            if ((map[kc >> 3] & (1 << ((int)kc & 7))) != 0) held.Add(kc);
        return held;
    }
}
#endif
