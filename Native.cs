using System.Runtime.InteropServices;

namespace Switcher;

/// <summary>Virtual-key constants (Windows numbering) used on every OS as the internal key identity.</summary>
internal static partial class Native
{
    public const int VK_BACK = 0x08;
    public const int VK_TAB = 0x09;
    public const int VK_RETURN = 0x0D;
    public const int VK_SHIFT = 0x10;
    public const int VK_CONTROL = 0x11;
    public const int VK_MENU = 0x12;
    public const int VK_PAUSE = 0x13;
    public const int VK_CAPITAL = 0x14;
    public const int VK_ESCAPE = 0x1B;
    public const int VK_SPACE = 0x20;
    public const int VK_PRIOR = 0x21;
    public const int VK_NEXT = 0x22;
    public const int VK_END = 0x23;
    public const int VK_HOME = 0x24;
    public const int VK_LEFT = 0x25;
    public const int VK_UP = 0x26;
    public const int VK_RIGHT = 0x27;
    public const int VK_DOWN = 0x28;
    public const int VK_INSERT = 0x2D;
    public const int VK_DELETE = 0x2E;
    public const int VK_LWIN = 0x5B;
    public const int VK_RWIN = 0x5C;
    public const int VK_NUMPAD0 = 0x60;
    public const int VK_NUMPAD9 = 0x69;
    public const int VK_F1 = 0x70;
    public const int VK_F24 = 0x87;
    public const int VK_LSHIFT = 0xA0;
    public const int VK_RSHIFT = 0xA1;
    public const int VK_LCONTROL = 0xA2;
    public const int VK_RCONTROL = 0xA3;
    public const int VK_LMENU = 0xA4;
    public const int VK_RMENU = 0xA5;
    public const int VK_OEM_1 = 0xBA;
    public const int VK_OEM_PLUS = 0xBB;
    public const int VK_OEM_COMMA = 0xBC;
    public const int VK_OEM_MINUS = 0xBD;
    public const int VK_OEM_PERIOD = 0xBE;
    public const int VK_OEM_2 = 0xBF;
    public const int VK_OEM_3 = 0xC0;
    public const int VK_OEM_4 = 0xDB;
    public const int VK_OEM_5 = 0xDC;
    public const int VK_OEM_6 = 0xDD;
    public const int VK_OEM_7 = 0xDE;
    public const int VK_OEM_8 = 0xDF;
    public const int VK_OEM_102 = 0xE2;

    /// <summary>Windows low-level hooks can eat a key; XRecord cannot. Linux always lets the key through and rewrites afterwards.</summary>
    public static bool CanSwallowInput =>
#if WINDOWS
        true;
#else
        false;
#endif

    /// <summary>Language id (low word) of a keyboard layout handle.</summary>
    public static int LangId(IntPtr hkl) => (int)((long)hkl & 0xFFFF);
}
