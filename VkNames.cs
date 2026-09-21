namespace Switcher;

/// <summary>Parse hotkey key names without System.Windows.Forms.Keys (Linux has no WinForms).</summary>
internal static class VkNames
{
    public static bool TryParse(string raw, out uint vk)
    {
        vk = 0;
        if (string.IsNullOrWhiteSpace(raw)) return false;
        var s = raw.Trim();
        if (s.Length == 1)
        {
            char c = char.ToUpperInvariant(s[0]);
            if (c is >= 'A' and <= 'Z' or >= '0' and <= '9') { vk = c; return true; }
        }
        vk = s.ToLowerInvariant() switch
        {
            "pause" or "break" => Native.VK_PAUSE,
            "space" => Native.VK_SPACE,
            "tab" => Native.VK_TAB,
            "enter" or "return" => Native.VK_RETURN,
            "esc" or "escape" => Native.VK_ESCAPE,
            "back" or "backspace" => Native.VK_BACK,
            "delete" or "del" => Native.VK_DELETE,
            "insert" or "ins" => Native.VK_INSERT,
            "home" => Native.VK_HOME,
            "end" => Native.VK_END,
            "pageup" or "prior" => Native.VK_PRIOR,
            "pagedown" or "next" => Native.VK_NEXT,
            "left" => Native.VK_LEFT,
            "right" => Native.VK_RIGHT,
            "up" => Native.VK_UP,
            "down" => Native.VK_DOWN,
            "caps" or "capslock" => Native.VK_CAPITAL,
            _ => 0,
        };
        if (vk != 0) return true;
        if (s.Length >= 2 && (s[0] == 'F' || s[0] == 'f') && int.TryParse(s.AsSpan(1), out int n) && n is >= 1 and <= 24)
        {
            vk = (uint)(Native.VK_F1 + n - 1);
            return true;
        }
        return false;
    }
}
