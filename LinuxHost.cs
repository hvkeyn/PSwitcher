#if !WINDOWS
using System.Diagnostics;

namespace Switcher;

/// <summary>Linux daemon: X11 hook, dictionaries, autostart desktop file. No WinForms tray.</summary>
internal static class LinuxHost
{
    private const string MutexName = "Switcher_SingleInstance";

    public static int Run(string[] args)
    {
        if (args.Length > 0 && args[0] is "--autostart" or "--install-autostart")
        { InstallAutostart(true); Console.WriteLine("autostart enabled: " + AutostartPath); return 0; }
        if (args.Length > 0 && args[0] is "--no-autostart" or "--uninstall-autostart")
        { InstallAutostart(false); Console.WriteLine("autostart disabled"); return 0; }

        using var mutex = new Mutex(true, MutexName, out bool created);
        if (!created)
        {
            Console.WriteLine("Switcher is already running.");
            return 0;
        }

        Settings.MigrateFromLayoutFix();
        var settings = Settings.Load();
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

        Native.GrabHotkey(Hotkey.Parse(settings.Hotkey).Vk);

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

        Console.WriteLine($"Switcher v{Version}  X11  Pause=переключить/отменить");
        Console.WriteLine("settings: " + Settings.FilePath);
        Console.WriteLine("Ctrl+C to quit.  --autostart / --no-autostart");
        Log.Write("Linux host ready");

        var done = new ManualResetEventSlim(false);
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; done.Set(); };
        AppDomain.CurrentDomain.ProcessExit += (_, _) => done.Set();
        done.Wait();

        Native.UngrabHotkey(Hotkey.Parse(settings.Hotkey).Vk);
        Native.Disconnect();
        return 0;
    }

    private static string Version => typeof(LinuxHost).Assembly.GetName().Version?.ToString(3) ?? "0.4.0";

    private static string AutostartPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "autostart", "switcher.desktop");

    public static void InstallAutostart(bool enable)
    {
        var path = AutostartPath;
        if (!enable) { if (File.Exists(path)) File.Delete(path); return; }
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var exe = Environment.ProcessPath ?? "Switcher";
        File.WriteAllText(path,
            "[Desktop Entry]\nType=Application\nName=Switcher\nComment=Layout switcher and typo fixer\n" +
            $"Exec=\"{exe}\"\nIcon=input-keyboard\nTerminal=false\nX-GNOME-Autostart-enabled=true\n");
    }

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
