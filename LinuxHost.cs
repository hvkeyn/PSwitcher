#if !WINDOWS
using System.Diagnostics;

namespace Switcher;

/// <summary>Linux daemon: X11 hook, dictionaries, autostart desktop file. No WinForms tray.</summary>
internal static class LinuxHost
{
    private const string MutexName = "Switcher_SingleInstance";
    private static FileSystemWatcher? _settingsWatch;
    private static int _reloadGen;

    public static int Run(string[] args)
    {
        if (args.Length > 0 && args[0] is "--autostart" or "--install-autostart")
            return SetAutostartFlag(true);
        if (args.Length > 0 && args[0] is "--no-autostart" or "--uninstall-autostart")
            return SetAutostartFlag(false);

        using var mutex = new Mutex(true, MutexName, out bool created);
        if (!created)
        {
            Console.WriteLine("Switcher is already running.");
            return 0;
        }

        Settings.MigrateFromLayoutFix();
        var settings = Settings.Load();
        OsAutostart.Apply(settings.Autostart);
        var rules = new Rules();
        var dicts = new Dictionaries();
        var freq = new Frequencies();
        var speller = new SpellFixer(dicts, freq, rules);
        using var engine = new Engine(settings, rules, dicts, freq, speller);
        engine.SuggestWord += word =>
        {
            Log.Write($"offer personal word: {word}");
            Notify($"Switcher: «{word}» отменяли {Rules.UndosToSuggest} раза. Добавьте в {Rules.WordsPath}");
        };

        try { engine.Start(); }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            Log.Write("start failed: " + ex);
            return 1;
        }

        engine.ReloadHotkey();
        WatchSettings(settings, engine);

        try
        {
            dicts.Load(); freq.Load();
            Log.Write("Frequencies loaded");
            speller.WarmUp();
            engine.Ready = true;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Не удалось загрузить словари: " + ex.Message);
            Log.Write("Dictionary load failed: " + ex);
            return 1;
        }

        Console.WriteLine($"Switcher v{Version}  X11  hotkey={settings.Hotkey}  autostart={(settings.Autostart ? "on" : "off")}");
        Console.WriteLine("settings: " + Settings.FilePath);
        Console.WriteLine("Правки settings.json подхватываются на лету. Ctrl+C — выход.");
        Console.WriteLine("Автозапуск: \"Autostart\": true в settings.json, или --autostart / --no-autostart");
        Log.Write("Linux host ready");

        var done = new ManualResetEventSlim(false);
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; done.Set(); };
        AppDomain.CurrentDomain.ProcessExit += (_, _) => done.Set();
        done.Wait();

        Native.UngrabHotkey(Hotkey.Parse(settings.Hotkey).Vk);
        Native.Disconnect();
        return 0;
    }

    private static int SetAutostartFlag(bool enable)
    {
        var s = Settings.Load();
        s.Autostart = enable;
        s.Save();
        OsAutostart.Apply(enable);
        Console.WriteLine(enable ? "autostart enabled: " + OsAutostart.DesktopPath : "autostart disabled");
        return 0;
    }

    /// <summary>Editor and the Windows settings window write the same json. On Linux there is no tray, so reread the file.</summary>
    private static void WatchSettings(Settings live, Engine engine)
    {
        try
        {
            Directory.CreateDirectory(Settings.Dir);
            _settingsWatch = new FileSystemWatcher(Settings.Dir, "settings.json")
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
            };
            void Kick(object? _, FileSystemEventArgs __)
            {
                int gen = Interlocked.Increment(ref _reloadGen);
                Task.Delay(400).ContinueWith(_ =>
                {
                    if (gen != Volatile.Read(ref _reloadGen)) return;
                    var fresh = Settings.TryRead();
                    if (fresh == null) return;
                    bool hotkey = !string.Equals(live.Hotkey, fresh.Hotkey, StringComparison.OrdinalIgnoreCase);
                    bool auto = live.Autostart != fresh.Autostart;
                    live.CopyRuntimeFrom(fresh);
                    if (hotkey) engine.ReloadHotkey();
                    if (auto) OsAutostart.Apply(live.Autostart);
                    Log.Write("settings reloaded");
                });
            }
            _settingsWatch.Changed += Kick;
            _settingsWatch.Created += Kick;
            _settingsWatch.Renamed += Kick;
            _settingsWatch.EnableRaisingEvents = true;
        }
        catch (Exception ex) { Log.Write("settings watch failed: " + ex.Message); }
    }

    private static string Version => typeof(LinuxHost).Assembly.GetName().Version?.ToString(3) ?? "0.5.0";

    private static void Notify(string text)
    {
        try
        {
            Process.Start(new ProcessStartInfo("notify-send")
            {
                ArgumentList = { "-a", "Switcher", text },
                UseShellExecute = false,
            });
        }
        catch { /* notify-send is optional */ }
    }
}
#endif
