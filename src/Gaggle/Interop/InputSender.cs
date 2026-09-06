using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Gaggle.Interop;

/// <summary>
/// Injects keystrokes with SendInput using <b>scan codes</b>.
///
/// This is the single most important detail in the project: Condor reads the keyboard
/// through DirectInput, which looks at the hardware scan code and largely ignores
/// virtual-key-only injection. Sending VK codes (or using WinForms SendKeys, or
/// PostMessage) produces nothing at all in game.
/// </summary>
internal static class InputSender
{
    /// <summary>Virtual keys whose scan code must carry the extended-key flag.</summary>
    private static readonly HashSet<int> ExtendedKeys =
    [
        0x21, 0x22, 0x23, 0x24, // PageUp, PageDown, End, Home
        0x25, 0x26, 0x27, 0x28, // Left, Up, Right, Down
        0x2D, 0x2E,             // Insert, Delete
        0x5B, 0x5C, 0x5D,       // LWin, RWin, Apps
        0x6F,                   // Divide
        0x90,                   // NumLock
        0xA3,                   // RControl
        0xA5,                   // RMenu (AltGr)
    ];

    private const int VkShift = 0xA0;
    private const int VkControl = 0xA2;
    private const int VkAltGr = 0xA5;

    /// <summary>Keyboard layout of the thread owning the given window.</summary>
    public static IntPtr GetLayoutFor(IntPtr hwnd)
    {
        uint threadId = NativeMethods.GetWindowThreadProcessId(hwnd, out _);
        return NativeMethods.GetKeyboardLayout(threadId);
    }

    /// <summary>Returns false if Windows refused the injection — see <see cref="SendKey"/>.</summary>
    public static bool KeyDown(int virtualKey, IntPtr layout) => SendKey(virtualKey, layout, keyUp: false);

    /// <inheritdoc cref="KeyDown"/>
    public static bool KeyUp(int virtualKey, IntPtr layout) => SendKey(virtualKey, layout, keyUp: true);

    /// <inheritdoc cref="KeyDown"/>
    public static bool Tap(int virtualKey, IntPtr layout, int holdMs)
    {
        bool down = KeyDown(virtualKey, layout);
        Thread.Sleep(holdMs);
        bool up = KeyUp(virtualKey, layout);

        return down && up;
    }

    /// <summary>
    /// Returns false when SendInput accepted no events. In practice that means either
    /// the key has no scan code on this layout, or — far more likely — UIPI blocked
    /// us because the target process runs at a higher integrity level than we do.
    /// </summary>
    private static bool SendKey(int virtualKey, IntPtr layout, bool keyUp)
    {
        uint scan = NativeMethods.MapVirtualKeyEx((uint)virtualKey, NativeMethods.MAPVK_VK_TO_VSC, layout);
        if (scan == 0)
        {
            return false; // No key for this VK on this layout.
        }

        uint flags = NativeMethods.KEYEVENTF_SCANCODE;

        if (keyUp)
        {
            flags |= NativeMethods.KEYEVENTF_KEYUP;
        }

        if (ExtendedKeys.Contains(virtualKey))
        {
            flags |= NativeMethods.KEYEVENTF_EXTENDEDKEY;
        }

        var input = new NativeMethods.INPUT
        {
            type = NativeMethods.INPUT_KEYBOARD,
            U = new NativeMethods.InputUnion
            {
                ki = new NativeMethods.KEYBDINPUT
                {
                    wVk = 0,
                    wScan = (ushort)scan,
                    dwFlags = flags,
                    time = 0,
                    dwExtraInfo = NativeMethods.GaggleSignature,
                },
            },
        };

        uint accepted = NativeMethods.SendInput(1, [input], Marshal.SizeOf<NativeMethods.INPUT>());

        return accepted == 1;
    }

    /// <summary>
    /// Types one character, pressing whatever modifiers the layout needs for it.
    /// Returns false when the character has no key on this layout (an em dash, say) —
    /// sanitise text before calling rather than relying on this.
    /// </summary>
    public static bool TypeChar(char ch, IntPtr layout, int keyDelayMs)
    {
        short scanResult = NativeMethods.VkKeyScanEx(ch, layout);
        if (scanResult == -1)
        {
            return false;
        }

        int virtualKey = scanResult & 0xFF;
        int shiftState = (scanResult >> 8) & 0xFF;

        bool needShift = (shiftState & 1) != 0;
        bool needCtrl = (shiftState & 2) != 0;
        bool needAlt = (shiftState & 4) != 0;

        if (needShift)
        {
            KeyDown(VkShift, layout);
        }

        if (needCtrl)
        {
            KeyDown(VkControl, layout);
        }

        if (needAlt)
        {
            KeyDown(VkAltGr, layout);
        }

        bool typed = Tap(virtualKey, layout, Math.Max(1, keyDelayMs / 2));

        if (needAlt)
        {
            KeyUp(VkAltGr, layout);
        }

        if (needCtrl)
        {
            KeyUp(VkControl, layout);
        }

        if (needShift)
        {
            KeyUp(VkShift, layout);
        }

        return typed;
    }

    /// <summary>
    /// Releases any modifier the user is still physically holding.
    ///
    /// Without this, a push-to-talk key such as Ctrl+T leaves Ctrl down while we type,
    /// so Condor receives Ctrl+H, Ctrl+E, Ctrl+L instead of "hello".
    /// </summary>
    public static void ReleaseHeldModifiers(IntPtr layout)
    {
        int[] modifiers = [0xA0, 0xA1, 0xA2, 0xA3, 0xA4, 0xA5, 0x5B, 0x5C];

        foreach (int vk in modifiers)
        {
            if ((NativeMethods.GetAsyncKeyState(vk) & 0x8000) != 0)
            {
                KeyUp(vk, layout);
            }
        }
    }
}
