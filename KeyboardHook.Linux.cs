#if !WINDOWS
using System.Runtime.InteropServices;

namespace Switcher;

/// <summary>XRecord keyboard/mouse observer. Cannot swallow keys (see Native.CanSwallowInput).</summary>
public sealed class KeyboardHook : IDisposable
{
    public Func<KeyEventArgs, bool>? KeyDown;
    public Action? MouseDown;
    public Action? Trigger;

    [ThreadStatic] public static bool InCallback;
    [ThreadStatic] public static bool InHardwareCallback;

    private nint _ctx;
    private Thread? _thread;
    private X11.XRecordCallback? _cb; // keep alive

    public void Install()
    {
        if (!Native.TryConnect())
            throw new InvalidOperationException("Cannot open X11 display. Install libx11-6 and libxtst6, and run under X11 or XWayland.");

        IntPtr range = X11.XRecordAllocRange();
        if (range == IntPtr.Zero) throw new InvalidOperationException("XRecordAllocRange failed");
        // gcc x86_64: device_events at offset 22 of XRecordRange
        Marshal.WriteByte(range, 22, X11.KeyPress);
        Marshal.WriteByte(range, 23, X11.ButtonPress);

        var clients = new nint[] { (nint)X11.XRecordAllClients };
        var ranges = new[] { range };
        lock (Native.XLock)
        {
            _ctx = X11.XRecordCreateContext(Native.RecordDisplay, 0, clients, 1, ranges, 1);
            X11.XSync(Native.RecordDisplay, 0);
        }
        if (_ctx == 0) throw new InvalidOperationException("XRecordCreateContext failed — does the X server support RECORD?");

        _cb = OnRecord;
        _thread = new Thread(RecordLoop) { IsBackground = true, Name = "Switcher XRecord" };
        _thread.Start();
    }

    private void RecordLoop()
    {
        try
        {
            // blocks until XRecordDisableContext on the other connection
            X11.XRecordEnableContext(Native.RecordDisplay, _ctx, _cb!, IntPtr.Zero);
        }
        catch (Exception ex) { Log.Write("XRecord loop: " + ex.Message); }
    }

    private void OnRecord(IntPtr closure, IntPtr raw)
    {
        if (raw == IntPtr.Zero) return;
        try
        {
            var rec = Marshal.PtrToStructure<X11.XRecordInterceptData>(raw);
            if (rec.category != X11.XRecordFromServer || rec.data == IntPtr.Zero || rec.data_len < 2)
                return;
            var ev = Marshal.PtrToStructure<X11.WireKeyEvent>(rec.data);
            int type = ev.type & 0x7F;
            if (type == X11.ButtonPress)
            {
                if (Native.Injecting == 0)
                {
                    try { MouseDown?.Invoke(); }
                    catch (Exception ex) { Log.Write("Mouse handler error: " + ex); }
                }
                return;
            }
            if (type != X11.KeyPress) return;

            uint vk = KeyMap.VkFromXKeycode(ev.detail);
            if (vk == 0) return;
            bool injected = Native.Injecting != 0;
            InCallback = true;
            InHardwareCallback = !injected;
            try
            {
                if (injected)
                {
                    // dummy trigger vk E8 is not used on Linux; deferred work runs immediately
                    return;
                }
                KeyDown?.Invoke(new KeyEventArgs(vk, ev.detail, injected, false));
            }
            catch (Exception ex) { Log.Write("Hook handler error: " + ex); }
            finally { InCallback = false; InHardwareCallback = false; }
        }
        finally
        {
            X11.XRecordFreeData(raw);
        }
    }

    public void Dispose()
    {
        if (_ctx != 0 && Native.Display != IntPtr.Zero)
        {
            lock (Native.XLock)
            {
                X11.XRecordDisableContext(Native.Display, _ctx);
                X11.XFlush(Native.Display);
                X11.XRecordFreeContext(Native.Display, _ctx);
            }
            _ctx = 0;
        }
        _thread?.Join(1000);
    }
}
#endif
