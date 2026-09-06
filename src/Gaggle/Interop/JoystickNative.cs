using System.Runtime.InteropServices;

namespace Gaggle.Interop;

/// <summary>
/// The legacy multimedia joystick API (winmm), used to read HOTAS and button-box
/// input without taking a DirectInput dependency.
///
/// Limits worth knowing: 32 buttons per device and 16 devices. Sticks that expose
/// more than 32 buttons will show only the first 32 here; that would need
/// DirectInput or Raw Input instead.
/// </summary>
internal static class JoystickNative
{
    public const uint JOYERR_NOERROR = 0;

    /// <summary>Ask joyGetPosEx for button state only; axes are irrelevant to us.</summary>
    public const uint JOY_RETURNBUTTONS = 0x00000080;

    public const int MaxDevices = 16;
    public const int MaxButtons = 32;

    [StructLayout(LayoutKind.Sequential)]
    public struct JOYINFOEX
    {
        public uint dwSize;
        public uint dwFlags;
        public uint dwXpos;
        public uint dwYpos;
        public uint dwZpos;
        public uint dwRpos;
        public uint dwUpos;
        public uint dwVpos;
        public uint dwButtons;
        public uint dwButtonNumber;
        public uint dwPOV;
        public uint dwReserved1;
        public uint dwReserved2;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct JOYCAPS
    {
        public ushort wMid;
        public ushort wPid;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szPname;

        public uint wXmin;
        public uint wXmax;
        public uint wYmin;
        public uint wYmax;
        public uint wZmin;
        public uint wZmax;
        public uint wNumButtons;
        public uint wPeriodMin;
        public uint wPeriodMax;
        public uint wRmin;
        public uint wRmax;
        public uint wUmin;
        public uint wUmax;
        public uint wVmin;
        public uint wVmax;
        public uint wCaps;
        public uint wMaxAxes;
        public uint wNumAxes;
        public uint wMaxButtons;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szRegKey;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szOEMVxD;
    }

    [DllImport("winmm.dll")]
    public static extern uint joyGetNumDevs();

    [DllImport("winmm.dll")]
    public static extern uint joyGetPosEx(uint uJoyID, ref JOYINFOEX pji);

    [DllImport("winmm.dll", CharSet = CharSet.Unicode)]
    public static extern uint joyGetDevCaps(UIntPtr uJoyID, ref JOYCAPS pjc, uint cbjc);
}
