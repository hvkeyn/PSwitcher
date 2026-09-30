namespace Switcher;

/// <summary>
/// Logon autostart. The switch lives in settings.json (<see cref="Settings.Autostart"/>);
/// this class only mirrors it onto the OS: HKCU Run on Windows, an XDG desktop file on Linux.
/// </summary>
internal static class OsAutostart
{
    public static string ExePath => Environment.ProcessPath ?? Environment.GetCommandLineArgs().FirstOrDefault() ?? "Switcher";

#if WINDOWS
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunName = "Switcher";

    /// <summary>A Run entry exists, even if it still points at an older copy of the exe.</summary>
    public static bool HasEntry()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKey, writable: false);
            var v = key?.GetValue(RunName) as string;
            return !string.IsNullOrWhiteSpace(v);
        }
        catch { return false; }
    }

    public static void Apply(bool enable)
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKey, writable: true)
                            ?? Microsoft.Win32.Registry.CurrentUser.CreateSubKey(RunKey)!;
            key.DeleteValue("LayoutFix", throwOnMissingValue: false);
            if (enable) key.SetValue(RunName, $"\"{ExePath}\"");
            else key.DeleteValue(RunName, throwOnMissingValue: false);
            Log.Write(enable ? "Autostart on (Windows Run)" : "Autostart off (Windows Run)");
        }
        catch (Exception ex) { Log.Write("Autostart failed: " + ex.Message); }
    }
#else
    public static string DesktopPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "autostart", "switcher.desktop");

    public static bool HasEntry() => File.Exists(DesktopPath);

    public static void Apply(bool enable)
    {
        try
        {
            var path = DesktopPath;
            if (!enable)
            {
                if (File.Exists(path)) File.Delete(path);
                Log.Write("Autostart off (XDG)");
                return;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var exe = ExePath.Replace("\"", "");
            File.WriteAllText(path,
                "[Desktop Entry]\n" +
                "Type=Application\n" +
                "Name=Switcher\n" +
                "Comment=Layout switcher and typo fixer\n" +
                $"Exec=\"{exe}\"\n" +
                "Icon=input-keyboard\n" +
                "Terminal=false\n" +
                "X-GNOME-Autostart-enabled=true\n" +
                "StartupNotify=false\n");
            Log.Write("Autostart on (XDG autostart)");
        }
        catch (Exception ex) { Log.Write("Autostart failed: " + ex.Message); }
    }
#endif
}
