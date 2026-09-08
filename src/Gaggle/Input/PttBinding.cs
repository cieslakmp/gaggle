using System.Text.Json.Serialization;
using System.Windows.Forms;
using Gaggle.Localisation;

namespace Gaggle.Input;

public enum PttSource
{
    Keyboard,
    Joystick,
}

/// <summary>
/// What the user holds to talk: either a keyboard key or a joystick button.
///
/// The two are not equivalent. A keyboard key is swallowed by the hook, so Condor
/// never sees it. A joystick button cannot be swallowed — Condor reads the device
/// directly — so binding a button the sim also uses will do both things at once.
/// </summary>
public sealed record PttBinding
{
    public PttSource Source { get; init; } = PttSource.Keyboard;

    public Keys Key { get; init; } = Keys.CapsLock;

    /// <summary>Zero-based winmm joystick id.</summary>
    public int JoystickId { get; init; }

    /// <summary>Zero-based button index.</summary>
    public int Button { get; init; }

    [JsonIgnore]
    public bool IsKeyboard => Source == PttSource.Keyboard;

    public static PttBinding FromKey(Keys key) => new() { Source = PttSource.Keyboard, Key = key };

    public static PttBinding FromButton(int joystickId, int button) => new()
    {
        Source = PttSource.Joystick,
        JoystickId = joystickId,
        Button = button,
    };

    /// <summary>
    /// Keys whose enum name is not what a user would call them. Keys.CapsLock, for
    /// instance, shares a value with Keys.Capital and prints as "Capital".
    /// </summary>
    private static readonly Dictionary<Keys, string> FriendlyNames = new()
    {
        [Keys.Capital] = "Caps Lock",
        [Keys.Back] = "Backspace",
        [Keys.Return] = "Enter",
        [Keys.Next] = "Page Down",
        [Keys.Prior] = "Page Up",
        [Keys.Menu] = "Alt",
        [Keys.ControlKey] = "Ctrl",
        [Keys.ShiftKey] = "Shift",
        [Keys.Oem3] = "`",
        [Keys.OemMinus] = "-",
        [Keys.Oemplus] = "=",
        [Keys.Scroll] = "Scroll Lock",
        [Keys.Pause] = "Pause",
        [Keys.Apps] = "Menu",
    };

    /// <summary>Label shown in the settings window and the tray tooltip.</summary>
    public string Describe() => Source switch
    {
        PttSource.Keyboard => DescribeKey(Key),
        PttSource.Joystick => Strings.Current.StickButton(JoystickId + 1, Button + 1),
        _ => Strings.Current.Unbound,
    };

    private static string DescribeKey(Keys key)
    {
        if (key == Keys.None)
        {
            return Strings.Current.Unbound;
        }

        return FriendlyNames.TryGetValue(key, out string? friendly) ? friendly : key.ToString();
    }
}
