using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Gaggle.Interop;

/// <summary>
/// Global low-level keyboard hook driving push-to-talk.
///
/// A hook rather than RegisterHotKey, because PTT needs the key <i>release</i>, which
/// RegisterHotKey never reports. The hook also lets us swallow the key so Condor never
/// sees it — otherwise the PTT key would also fire whatever the sim has bound to it.
///
/// The callback runs on the thread that installed the hook (our UI thread) and must
/// return within the Windows LowLevelHooksTimeout (300 ms by default) or Windows
/// silently uninstalls the hook. Handlers must therefore do no blocking work.
/// </summary>
internal sealed class PushToTalkHook : IDisposable
{
    private readonly NativeMethods.LowLevelKeyboardProc _proc;
    private IntPtr _hook = IntPtr.Zero;
    private bool _talkKeyDown;

    public PushToTalkHook()
    {
        // Held in a field so the GC cannot collect the delegate while Windows holds it.
        _proc = HookCallback;
    }

    /// <summary>Key held to record. Always swallowed while the hook is installed.</summary>
    public Keys TalkKey { get; set; } = Keys.CapsLock;

    /// <summary>Sends the pending transcript. Swallowed only while a review is open.</summary>
    public Keys ConfirmKey { get; set; } = Keys.Enter;

    /// <summary>Discards the pending transcript. Swallowed only while a review is open.</summary>
    public Keys CancelKey { get; set; } = Keys.Escape;

    /// <summary>Set by the app while a transcript is waiting to be confirmed.</summary>
    public bool ReviewPending { get; set; }

    public event Action? TalkPressed;

    public event Action? TalkReleased;

    public event Action? Confirmed;

    public event Action? Cancelled;

    public bool IsInstalled => _hook != IntPtr.Zero;

    public void Install()
    {
        if (IsInstalled)
        {
            return;
        }

        IntPtr module = NativeMethods.GetModuleHandle(null);
        _hook = NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, _proc, module, 0);

        if (_hook == IntPtr.Zero)
        {
            throw new InvalidOperationException(
                $"Could not install the keyboard hook (Win32 error {Marshal.GetLastWin32Error()}).");
        }
    }

    public void Uninstall()
    {
        if (!IsInstalled)
        {
            return;
        }

        NativeMethods.UnhookWindowsHookEx(_hook);
        _hook = IntPtr.Zero;
        _talkKeyDown = false;
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode != NativeMethods.HC_ACTION)
        {
            return NativeMethods.CallNextHookEx(_hook, nCode, wParam, lParam);
        }

        var info = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);

        // Never react to our own injected keystrokes, or typing a message would
        // retrigger the hook and loop.
        bool injected = (info.flags & NativeMethods.LLKHF_INJECTED) != 0
            || info.dwExtraInfo == NativeMethods.GaggleSignature;

        if (injected)
        {
            return NativeMethods.CallNextHookEx(_hook, nCode, wParam, lParam);
        }

        int message = (int)wParam;
        bool isDown = message is NativeMethods.WM_KEYDOWN or NativeMethods.WM_SYSKEYDOWN;
        bool isUp = message is NativeMethods.WM_KEYUP or NativeMethods.WM_SYSKEYUP;
        var key = (Keys)info.vkCode;

        if (key == TalkKey)
        {
            if (isDown && !_talkKeyDown)
            {
                _talkKeyDown = true;
                TalkPressed?.Invoke();
            }
            else if (isUp && _talkKeyDown)
            {
                _talkKeyDown = false;
                TalkReleased?.Invoke();
            }

            return 1; // Swallow: Condor must not see the PTT key.
        }

        if (ReviewPending && isDown)
        {
            if (key == ConfirmKey)
            {
                Confirmed?.Invoke();
                return 1;
            }

            if (key == CancelKey)
            {
                Cancelled?.Invoke();
                return 1;
            }
        }

        return NativeMethods.CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    public void Dispose() => Uninstall();
}
