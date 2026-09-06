using Gaggle.Interop;
using Microsoft.Win32;

namespace Gaggle.Input;

/// <summary>
/// Polls attached joysticks for button state.
///
/// The winmm API has no event model, so this polls on a WinForms timer — cheap
/// enough at a few devices per tick, and it keeps every callback on the UI thread,
/// which is where the rest of the app expects to be.
/// </summary>
public sealed class JoystickWatcher : IDisposable
{
    /// <summary>Fast enough that a deliberate button press is never missed.</summary>
    private const int PollIntervalMs = 15;

    private readonly System.Windows.Forms.Timer _timer;
    private readonly uint[] _lastButtons = new uint[JoystickNative.MaxDevices];
    private readonly bool[] _present = new bool[JoystickNative.MaxDevices];

    public JoystickWatcher()
    {
        _timer = new System.Windows.Forms.Timer { Interval = PollIntervalMs };
        _timer.Tick += (_, _) => Poll();
    }

    /// <summary>The button being watched, or null to watch nothing.</summary>
    public PttBinding? Binding { get; set; }

    /// <summary>While true, the next button press is reported via <see cref="Captured"/>.</summary>
    public bool Capturing { get; set; }

    public event Action? Pressed;

    public event Action? Released;

    public event Action<int, int>? Captured;

    public void Start() => _timer.Start();

    public void Stop() => _timer.Stop();

    /// <summary>Attached devices, as (id, name, button count).</summary>
    public static IReadOnlyList<(int Id, string Name, int Buttons)> ListDevices()
    {
        var devices = new List<(int, string, int)>();
        uint slots = Math.Min(JoystickNative.joyGetNumDevs(), JoystickNative.MaxDevices);

        for (uint id = 0; id < slots; id++)
        {
            var info = new JoystickNative.JOYINFOEX
            {
                dwSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<JoystickNative.JOYINFOEX>(),
                dwFlags = JoystickNative.JOY_RETURNBUTTONS,
            };

            // joyGetNumDevs reports driver slots, not attached hardware; a successful
            // poll is the only reliable test that something is actually plugged in.
            if (JoystickNative.joyGetPosEx(id, ref info) != JoystickNative.JOYERR_NOERROR)
            {
                continue;
            }

            var caps = default(JoystickNative.JOYCAPS);
            string name = $"Joystick {id + 1}";
            int buttons = JoystickNative.MaxButtons;

            uint capsResult = JoystickNative.joyGetDevCaps(
                (UIntPtr)id,
                ref caps,
                (uint)System.Runtime.InteropServices.Marshal.SizeOf<JoystickNative.JOYCAPS>());

            if (capsResult == JoystickNative.JOYERR_NOERROR)
            {
                // szPname is the driver name ("Microsoft PC-joystick driver") and is
                // identical for every device, so prefer the registry's product name.
                name = LookupProductName(caps.wMid, caps.wPid)
                    ?? (string.IsNullOrWhiteSpace(caps.szPname) ? name : caps.szPname);

                buttons = (int)Math.Min(caps.wNumButtons, JoystickNative.MaxButtons);
            }

            devices.Add(((int)id, name, buttons));
        }

        return devices;
    }

    private const string OemKeyPath =
        @"System\CurrentControlSet\Control\MediaProperties\PrivateProperties\Joystick\OEM\";

    /// <summary>
    /// Resolves a device's real product name — "T.16000M" rather than "Microsoft
    /// PC-joystick driver" — from the OEM registry entry keyed by its USB vendor and
    /// product id. HKCU is checked first: that is where attached devices register,
    /// while HKLM mostly holds inbox driver defaults with no name at all.
    /// </summary>
    private static string? LookupProductName(ushort manufacturerId, ushort productId)
    {
        string oemKey = OemKeyPath + $"VID_{manufacturerId:X4}&PID_{productId:X4}";
        RegistryKey[] roots = [Registry.CurrentUser, Registry.LocalMachine];

        foreach (RegistryKey root in roots)
        {
            try
            {
                using RegistryKey? entry = root.OpenSubKey(oemKey);

                if (entry?.GetValue("OEMName") is string name && !string.IsNullOrWhiteSpace(name))
                {
                    return name.Trim();
                }
            }
            catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException)
            {
                // A nicer label is not worth failing enumeration over.
            }
        }

        return null;
    }

    private void Poll()
    {
        uint slots = Math.Min(JoystickNative.joyGetNumDevs(), JoystickNative.MaxDevices);

        for (uint id = 0; id < slots; id++)
        {
            // Only the bound device matters unless we are capturing a new binding.
            if (!Capturing && (Binding is null || Binding.Source != PttSource.Joystick || Binding.JoystickId != id))
            {
                continue;
            }

            var info = new JoystickNative.JOYINFOEX
            {
                dwSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<JoystickNative.JOYINFOEX>(),
                dwFlags = JoystickNative.JOY_RETURNBUTTONS,
            };

            if (JoystickNative.joyGetPosEx(id, ref info) != JoystickNative.JOYERR_NOERROR)
            {
                _present[id] = false;
                _lastButtons[id] = 0;
                continue;
            }

            uint previous = _present[id] ? _lastButtons[id] : info.dwButtons;
            _present[id] = true;
            _lastButtons[id] = info.dwButtons;

            uint newlyPressed = info.dwButtons & ~previous;
            uint newlyReleased = previous & ~info.dwButtons;

            if (Capturing)
            {
                if (newlyPressed != 0)
                {
                    Captured?.Invoke((int)id, LowestSetBit(newlyPressed));
                }

                continue;
            }

            if (Binding is null || Binding.Source != PttSource.Joystick || Binding.JoystickId != id)
            {
                continue;
            }

            uint mask = 1u << Binding.Button;

            if ((newlyPressed & mask) != 0)
            {
                Pressed?.Invoke();
            }
            else if ((newlyReleased & mask) != 0)
            {
                Released?.Invoke();
            }
        }
    }

    private static int LowestSetBit(uint value) => System.Numerics.BitOperations.TrailingZeroCount(value);

    public void Dispose()
    {
        _timer.Stop();
        _timer.Dispose();
    }
}
