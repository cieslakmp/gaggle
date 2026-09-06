using System.Windows.Forms;
using Gaggle.Interop;

namespace Gaggle.Input;

/// <summary>
/// Presents keyboard and joystick push-to-talk as one stream of press/release events,
/// so the rest of the app never has to care which device the user bound.
///
/// The keyboard hook stays installed regardless of the binding, because it also reads
/// the confirm and cancel keys while a review is open.
/// </summary>
internal sealed class PttController : IDisposable
{
    private readonly PushToTalkHook _hook;
    private readonly JoystickWatcher _joystick;
    private PttBinding _binding = PttBinding.FromKey(Keys.CapsLock);

    public PttController(PushToTalkHook hook, JoystickWatcher joystick)
    {
        _hook = hook;
        _joystick = joystick;

        _hook.TalkPressed += () => Pressed?.Invoke();
        _hook.TalkReleased += () => Released?.Invoke();
        _hook.KeyCaptured += key => Capture(PttBinding.FromKey(key));

        _joystick.Pressed += () => Pressed?.Invoke();
        _joystick.Released += () => Released?.Invoke();
        _joystick.Captured += (id, button) => Capture(PttBinding.FromButton(id, button));
    }

    public event Action? Pressed;

    public event Action? Released;

    /// <summary>Raised once, with the captured binding, after <see cref="BeginCapture"/>.</summary>
    public event Action<PttBinding>? Captured;

    public bool IsCapturing { get; private set; }

    public PttBinding Binding
    {
        get => _binding;
        set
        {
            _binding = value;
            Apply();
        }
    }

    public void Start()
    {
        Apply();
        _joystick.Start();
    }

    /// <summary>Listens for the next key or button and reports it as a binding.</summary>
    public void BeginCapture()
    {
        if (IsCapturing)
        {
            return;
        }

        IsCapturing = true;

        // Stop matching the current binding, so pressing it does not start a recording
        // while the user is trying to rebind it.
        _hook.TalkKey = Keys.None;
        _joystick.Binding = null;

        _hook.Capturing = true;
        _joystick.Capturing = true;
    }

    public void CancelCapture()
    {
        if (!IsCapturing)
        {
            return;
        }

        EndCapture();
        Apply();
    }

    private void Capture(PttBinding binding)
    {
        if (!IsCapturing)
        {
            return;
        }

        EndCapture();
        Captured?.Invoke(binding);
    }

    private void EndCapture()
    {
        IsCapturing = false;
        _hook.Capturing = false;
        _joystick.Capturing = false;
    }

    private void Apply()
    {
        _hook.TalkKey = _binding.IsKeyboard ? _binding.Key : Keys.None;
        _joystick.Binding = _binding.IsKeyboard ? null : _binding;
    }

    public void Dispose()
    {
        _joystick.Dispose();
        _hook.Dispose();
    }
}
