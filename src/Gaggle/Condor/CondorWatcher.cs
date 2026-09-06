using System.Diagnostics;
using Gaggle.Interop;

namespace Gaggle.Condor;

/// <summary>
/// Tracks whether Condor is running and whether it currently owns the foreground.
///
/// Both matter: SendInput goes to whatever window has focus, so injecting while
/// Condor is not foreground would type the message into whatever else is on screen.
/// </summary>
public sealed class CondorWatcher : IDisposable
{
    private readonly System.Windows.Forms.Timer _timer;
    private string _processName;

    public CondorWatcher(string processName)
    {
        _processName = processName;

        _timer = new System.Windows.Forms.Timer { Interval = 2000 };
        _timer.Tick += (_, _) => Refresh();
    }

    /// <summary>True while at least one matching process exists.</summary>
    public bool IsRunning { get; private set; }

    public event EventHandler? StateChanged;

    public string ProcessName
    {
        get => _processName;
        set
        {
            _processName = value;
            Refresh();
        }
    }

    public void Start()
    {
        Refresh();
        _timer.Start();
    }

    public void Stop() => _timer.Stop();

    /// <summary>
    /// Returns the Condor window if it is currently in the foreground, else zero.
    /// </summary>
    public IntPtr GetForegroundCondorWindow()
    {
        IntPtr foreground = NativeMethods.GetForegroundWindow();
        if (foreground == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        _ = NativeMethods.GetWindowThreadProcessId(foreground, out uint foregroundPid);

        foreach (Process process in Process.GetProcessesByName(_processName))
        {
            using (process)
            {
                if ((uint)process.Id == foregroundPid)
                {
                    return foreground;
                }
            }
        }

        return IntPtr.Zero;
    }

    private void Refresh()
    {
        bool running;

        try
        {
            Process[] matches = Process.GetProcessesByName(_processName);
            running = matches.Length > 0;

            foreach (Process process in matches)
            {
                process.Dispose();
            }
        }
        catch (InvalidOperationException)
        {
            running = false;
        }

        if (running == IsRunning)
        {
            return;
        }

        IsRunning = running;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Dispose();
    }
}
