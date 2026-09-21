#if !WINDOWS
using System.Runtime.InteropServices;

namespace Switcher;

/// <summary>Minimal X11 / XTest / XRecord / XKB bindings (64-bit Linux).</summary>
internal static class X11
{
    public const int KeyPress = 2;
    public const int KeyRelease = 3;
    public const int ButtonPress = 4;
    public const int GrabModeAsync = 1;
    public const int RevertToParent = 2;
    public const uint CurrentTime = 0;
    public const uint ShiftMask = 1 << 0;
    public const uint LockMask = 1 << 1;
    public const uint ControlMask = 1 << 2;
    public const uint Mod1Mask = 1 << 3;
    public const uint Mod2Mask = 1 << 4;
    public const uint Mod4Mask = 1 << 6;
    public const uint AnyModifier = 1u << 15;
    public const uint XkbUseCoreKbd = 0x0100;
    public const uint XkbGroupNamesMask = 1 << 12;
    public const uint XRecordAllClients = 3;
    public const int XRecordFromServer = 0;
    public const nint None = 0;

    [DllImport("libX11.so.6")] public static extern int XInitThreads();
    [DllImport("libX11.so.6")] public static extern IntPtr XOpenDisplay(IntPtr display);
    [DllImport("libX11.so.6")] public static extern int XCloseDisplay(IntPtr display);
    [DllImport("libX11.so.6")] public static extern int XDefaultScreen(IntPtr display);
    [DllImport("libX11.so.6")] public static extern nint XRootWindow(IntPtr display, int screen);
    [DllImport("libX11.so.6")] public static extern int XFlush(IntPtr display);
    [DllImport("libX11.so.6")] public static extern int XSync(IntPtr display, int discard);
    [DllImport("libX11.so.6")] public static extern int XQueryKeymap(IntPtr display, byte[] keys);
    [DllImport("libX11.so.6")] public static extern int XGetInputFocus(IntPtr display, out nint focus, out int revert);
    [DllImport("libX11.so.6")] public static extern nint XDefaultRootWindow(IntPtr display);
    [DllImport("libX11.so.6")] public static extern nint XInternAtom(IntPtr display, [MarshalAs(UnmanagedType.LPUTF8Str)] string name, int onlyIfExists);
    [DllImport("libX11.so.6")] public static extern int XFree(IntPtr data);
    [DllImport("libX11.so.6")] public static extern IntPtr XGetAtomName(IntPtr display, nint atom);
    [DllImport("libX11.so.6")] public static extern int XQueryTree(IntPtr display, nint w, out nint root, out nint parent, out IntPtr children, out uint nchildren);
    [DllImport("libX11.so.6")] public static extern int XGrabKey(IntPtr display, int keycode, uint modifiers, nint grabWindow, int ownerEvents, int pointerMode, int keyboardMode);
    [DllImport("libX11.so.6")] public static extern int XUngrabKey(IntPtr display, int keycode, uint modifiers, nint grabWindow);

    [DllImport("libX11.so.6")]
    public static extern int XGetWindowProperty(IntPtr display, nint w, nint property, nint offset, nint length,
        int delete, nint reqType, out nint actualType, out int actualFormat, out nuint nitems, out nuint bytesAfter, out IntPtr prop);

    [DllImport("libX11.so.6")] public static extern int XkbLockGroup(IntPtr display, uint deviceSpec, uint group);
    [DllImport("libX11.so.6")] public static extern int XkbGetState(IntPtr display, uint deviceSpec, ref XkbStateRec state);
    [DllImport("libX11.so.6")] public static extern IntPtr XkbGetKeyboard(IntPtr display, uint which, uint deviceSpec);
    [DllImport("libX11.so.6")] public static extern int XkbGetNames(IntPtr display, uint which, IntPtr xkb);
    [DllImport("libX11.so.6")] public static extern void XkbFreeKeyboard(IntPtr xkb, uint which, int freeAll);

    [DllImport("libXtst.so.6")] public static extern int XTestFakeKeyEvent(IntPtr display, uint keycode, int is_press, nuint delay);

    [DllImport("libXtst.so.6")] public static extern IntPtr XRecordAllocRange();
    [DllImport("libXtst.so.6")] public static extern nint XRecordCreateContext(IntPtr display, int datum_flags, nint[] clients, int nclients, IntPtr[] ranges, int nranges);
    [DllImport("libXtst.so.6")] public static extern int XRecordEnableContext(IntPtr display, nint context, XRecordCallback callback, IntPtr closure);
    [DllImport("libXtst.so.6")] public static extern int XRecordDisableContext(IntPtr display, nint context);
    [DllImport("libXtst.so.6")] public static extern int XRecordFreeContext(IntPtr display, nint context);
    [DllImport("libXtst.so.6")] public static extern void XRecordFreeData(IntPtr data);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void XRecordCallback(IntPtr closure, IntPtr recorded_data);

    [StructLayout(LayoutKind.Sequential)]
    public struct XkbStateRec
    {
        public byte group, locked_group, base_group, latched_group;
        public byte mods, base_mods, latched_mods, locked_mods;
        public byte compat_state;
        public byte grab_mods, compat_grab_mods, lookup_mods, compat_lookup_mods;
        public byte ptr_buttons;
    }

    // XRecordRange: we only fill deviceEvents (bytes 2-3 of the 4th pair). Layout from X11/extensions/record.h
    [StructLayout(LayoutKind.Sequential)]
    public struct XRecordRange
    {
        public ushort coreRequestsFirst, coreRequestsLast;
        public ushort coreRepliesFirst, coreRepliesLast;
        public byte extRequestsMajorFirst, extRequestsMajorLast;
        public ushort extRequestsMinorFirst, extRequestsMinorLast;
        public byte extRepliesMajorFirst, extRepliesMajorLast;
        public ushort extRepliesMinorFirst, extRepliesMinorLast;
        public byte deliveredEventsFirst, deliveredEventsLast;
        public byte deviceEventsFirst, deviceEventsLast;
        public byte errorsFirst, errorsLast;
        public byte clientStarted, clientDied;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct XRecordInterceptData
    {
        public nuint id_base;
        public uint server_time;
        public uint pad;
        public nuint client_seq;
        public int category;
        public int client_swapped;
        public IntPtr data;
        public nuint data_len;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct WireKeyEvent
    {
        public byte type, detail;
        public ushort sequence;
        public uint time, root, eventWin, child;
        public short rootX, rootY, eventX, eventY;
        public ushort state;
        public byte sameScreen, pad;
    }
}
#endif
