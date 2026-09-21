namespace Switcher;

/// <summary>
/// Standard US QWERTY and Russian JCUKEN (Windows) physical-key maps.
/// Used on Linux to render a word in the other layout and to type Unicode via XTest
/// (no ToUnicodeEx there). Matches what a typical RU/EN pair produces.
/// </summary>
internal static class KeyMap
{
    public static char ToChar(int langId, uint vk, bool shift, bool caps)
    {
        bool upper = shift ^ caps;
        if (vk >= 'A' && vk <= 'Z')
        {
            if (langId == Dictionaries.LangRu)
            {
                char ru = RuLetter((char)vk);
                return upper ? char.ToUpperInvariant(ru) : ru;
            }
            char la = char.ToLowerInvariant((char)vk);
            return upper ? char.ToUpperInvariant(la) : la;
        }
        return langId == Dictionaries.LangRu ? RuOem(vk, shift) : UsOem(vk, shift);
    }

    /// <summary>Low byte = VK, bit 8 = shift. 0xFFFF = cannot type.</summary>
    public static short VkScan(int langId, char ch)
    {
        char lower = char.ToLowerInvariant(ch);
        bool wantUpper = char.IsLetter(ch) && char.ToUpperInvariant(ch) == ch && ch != lower;
        if (langId == Dictionaries.LangRu)
        {
            for (char vk = 'A'; vk <= 'Z'; vk++)
                if (RuLetter(vk) == lower) return (short)(vk | (wantUpper ? 0x100 : 0));
        }
        else if (lower is >= 'a' and <= 'z')
            return (short)(char.ToUpperInvariant(lower) | (wantUpper ? 0x100 : 0));

        for (int pass = 0; pass < 2; pass++)
        {
            bool shift = pass == 1;
            foreach (uint vk in OemKeys)
            {
                char produced = langId == Dictionaries.LangRu ? RuOem(vk, shift) : UsOem(vk, shift);
                if (produced == ch) return (short)((int)vk | (shift ? 0x100 : 0));
            }
            for (uint d = '0'; d <= '9'; d++)
            {
                char produced = langId == Dictionaries.LangRu ? RuOem(d, shift) : UsOem(d, shift);
                if (produced == ch) return (short)((int)d | (shift ? 0x100 : 0));
            }
        }
        return -1;
    }

    private static readonly uint[] OemKeys =
    {
        Native.VK_OEM_1, Native.VK_OEM_2, Native.VK_OEM_3, Native.VK_OEM_4, Native.VK_OEM_5,
        Native.VK_OEM_6, Native.VK_OEM_7, Native.VK_OEM_COMMA, Native.VK_OEM_PERIOD,
        Native.VK_OEM_MINUS, Native.VK_OEM_PLUS, Native.VK_SPACE, Native.VK_OEM_102,
    };

    // Windows Russian layout, letters on A–Z
    private static char RuLetter(char vk) => vk switch
    {
        'Q' => 'й', 'W' => 'ц', 'E' => 'у', 'R' => 'к', 'T' => 'е', 'Y' => 'н',
        'U' => 'г', 'I' => 'ш', 'O' => 'щ', 'P' => 'з',
        'A' => 'ф', 'S' => 'ы', 'D' => 'в', 'F' => 'а', 'G' => 'п', 'H' => 'р',
        'J' => 'о', 'K' => 'л', 'L' => 'д',
        'Z' => 'я', 'X' => 'ч', 'C' => 'с', 'V' => 'м', 'B' => 'и', 'N' => 'т',
        'M' => 'ь',
        _ => '\0',
    };

    private static char UsOem(uint vk, bool shift) => vk switch
    {
        Native.VK_SPACE => ' ',
        >= '0' and <= '9' => shift ? ")!@#$%^&*("[((int)vk - '0')] : (char)vk,
        Native.VK_OEM_1 => shift ? ':' : ';',
        Native.VK_OEM_2 => shift ? '?' : '/',
        Native.VK_OEM_3 => shift ? '~' : '`',
        Native.VK_OEM_4 => shift ? '{' : '[',
        Native.VK_OEM_5 => shift ? '|' : '\\',
        Native.VK_OEM_6 => shift ? '}' : ']',
        Native.VK_OEM_7 => shift ? '"' : '\'',
        Native.VK_OEM_COMMA => shift ? '<' : ',',
        Native.VK_OEM_PERIOD => shift ? '>' : '.',
        Native.VK_OEM_MINUS => shift ? '_' : '-',
        Native.VK_OEM_PLUS => shift ? '+' : '=',
        Native.VK_OEM_102 => shift ? '|' : '\\',
        _ => '\0',
    };

    private static char RuOem(uint vk, bool shift) => vk switch
    {
        Native.VK_SPACE => ' ',
        >= '0' and <= '9' => shift ? ")!\"№;%:?*("[((int)vk - '0')] : (char)vk,
        Native.VK_OEM_1 => shift ? 'Ж' : 'ж',
        Native.VK_OEM_2 => shift ? ',' : '.',
        Native.VK_OEM_3 => shift ? 'Ё' : 'ё',
        Native.VK_OEM_4 => shift ? 'Х' : 'х',
        Native.VK_OEM_5 => shift ? '/' : '\\',
        Native.VK_OEM_6 => shift ? 'Ъ' : 'ъ',
        Native.VK_OEM_7 => shift ? 'Э' : 'э',
        Native.VK_OEM_COMMA => shift ? 'Б' : 'б',
        Native.VK_OEM_PERIOD => shift ? 'Ю' : 'ю',
        Native.VK_OEM_MINUS => shift ? '_' : '-',
        Native.VK_OEM_PLUS => shift ? '+' : '=',
        Native.VK_OEM_102 => shift ? '/' : '\\',
        _ => '\0',
    };

    /// <summary>X11/evdev keycode (X = linux + 8) for a Windows VK on a PC keyboard.</summary>
    public static uint XKeycode(uint vk) => vk switch
    {
        Native.VK_ESCAPE => 9,
        Native.VK_BACK => 22,
        Native.VK_TAB => 23,
        Native.VK_RETURN => 36,
        Native.VK_CONTROL or Native.VK_LCONTROL => 37,
        Native.VK_RCONTROL => 105,
        Native.VK_SHIFT or Native.VK_LSHIFT => 50,
        Native.VK_RSHIFT => 62,
        Native.VK_MENU or Native.VK_LMENU => 64,
        Native.VK_RMENU => 108,
        Native.VK_LWIN => 133,
        Native.VK_RWIN => 134,
        Native.VK_CAPITAL => 66,
        Native.VK_SPACE => 65,
        Native.VK_PAUSE => 127,
        Native.VK_DELETE => 119,
        Native.VK_INSERT => 118,
        Native.VK_HOME => 110,
        Native.VK_END => 115,
        Native.VK_PRIOR => 112,
        Native.VK_NEXT => 117,
        Native.VK_LEFT => 113,
        Native.VK_UP => 111,
        Native.VK_RIGHT => 114,
        Native.VK_DOWN => 116,
        Native.VK_OEM_1 => 47,
        Native.VK_OEM_2 => 61,
        Native.VK_OEM_3 => 49,
        Native.VK_OEM_4 => 34,
        Native.VK_OEM_5 => 51,
        Native.VK_OEM_6 => 35,
        Native.VK_OEM_7 => 48,
        Native.VK_OEM_COMMA => 59,
        Native.VK_OEM_PERIOD => 60,
        Native.VK_OEM_MINUS => 20,
        Native.VK_OEM_PLUS => 21,
        Native.VK_OEM_102 => 94,
        >= '1' and <= '9' => 10u + (vk - '1'),
        (uint)'0' => 19,
        >= 'A' and <= 'Z' => XLetter((char)vk),
        _ => FKey(vk),
    };

    private static uint FKey(uint vk)
    {
        if (vk >= Native.VK_F1 && vk <= Native.VK_F1 + 9) return 67u + (vk - Native.VK_F1);
        if (vk == Native.VK_F1 + 10) return 95;
        if (vk == Native.VK_F1 + 11) return 96;
        return 0;
    }

    private static uint XLetter(char vk) => vk switch
    {
        'Q' => 24, 'W' => 25, 'E' => 26, 'R' => 27, 'T' => 28, 'Y' => 29,
        'U' => 30, 'I' => 31, 'O' => 32, 'P' => 33,
        'A' => 38, 'S' => 39, 'D' => 40, 'F' => 41, 'G' => 42, 'H' => 43,
        'J' => 44, 'K' => 45, 'L' => 46,
        'Z' => 52, 'X' => 53, 'C' => 54, 'V' => 55, 'B' => 56, 'N' => 57,
        'M' => 58,
        _ => 0,
    };

    public static uint VkFromXKeycode(uint code) => code switch
    {
        9 => Native.VK_ESCAPE,
        22 => Native.VK_BACK,
        23 => Native.VK_TAB,
        36 => Native.VK_RETURN,
        37 => Native.VK_LCONTROL,
        105 => Native.VK_RCONTROL,
        50 => Native.VK_LSHIFT,
        62 => Native.VK_RSHIFT,
        64 => Native.VK_LMENU,
        108 => Native.VK_RMENU,
        133 => Native.VK_LWIN,
        134 => Native.VK_RWIN,
        66 => Native.VK_CAPITAL,
        65 => Native.VK_SPACE,
        127 => Native.VK_PAUSE,
        119 => Native.VK_DELETE,
        118 => Native.VK_INSERT,
        110 => Native.VK_HOME,
        115 => Native.VK_END,
        112 => Native.VK_PRIOR,
        117 => Native.VK_NEXT,
        113 => Native.VK_LEFT,
        111 => Native.VK_UP,
        114 => Native.VK_RIGHT,
        116 => Native.VK_DOWN,
        47 => Native.VK_OEM_1,
        61 => Native.VK_OEM_2,
        49 => Native.VK_OEM_3,
        34 => Native.VK_OEM_4,
        51 => Native.VK_OEM_5,
        35 => Native.VK_OEM_6,
        48 => Native.VK_OEM_7,
        59 => Native.VK_OEM_COMMA,
        60 => Native.VK_OEM_PERIOD,
        20 => Native.VK_OEM_MINUS,
        21 => Native.VK_OEM_PLUS,
        94 => Native.VK_OEM_102,
        >= 10 and <= 18 => (uint)('1' + (code - 10)),
        19 => (uint)'0',
        24 => 'Q', 25 => 'W', 26 => 'E', 27 => 'R', 28 => 'T', 29 => 'Y',
        30 => 'U', 31 => 'I', 32 => 'O', 33 => 'P',
        38 => 'A', 39 => 'S', 40 => 'D', 41 => 'F', 42 => 'G', 43 => 'H',
        44 => 'J', 45 => 'K', 46 => 'L',
        52 => 'Z', 53 => 'X', 54 => 'C', 55 => 'V', 56 => 'B', 57 => 'N',
        58 => 'M',
        >= 67 and <= 76 => Native.VK_F1 + (code - 67),
        95 => Native.VK_F1 + 10,
        96 => Native.VK_F1 + 11,
        _ => 0,
    };
}
