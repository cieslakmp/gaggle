using Gaggle.Configuration;
using Gaggle.Interop;
using Gaggle.Ui;

namespace Gaggle.Condor;

public enum SendOutcome
{
    Sent,
    CondorNotRunning,
    CondorNotFocused,
    RateLimited,
    NothingToSend,
    Busy,
    UnsupportedCharacters,
    InjectionBlocked,
}

public readonly record struct SendResult(SendOutcome Outcome, string? Detail = null)
{
    public bool Success => Outcome == SendOutcome.Sent;

    public string Describe() => Outcome switch
    {
        SendOutcome.Sent => UiText.Current.SendSent,
        SendOutcome.CondorNotRunning => UiText.Current.SendCondorNotRunning,
        SendOutcome.CondorNotFocused => UiText.Current.SendCondorNotFocused,
        SendOutcome.RateLimited => UiText.Current.SendRateLimited,
        SendOutcome.NothingToSend => UiText.Current.SendNothingToSend,
        SendOutcome.Busy => UiText.Current.SendBusy,
        SendOutcome.UnsupportedCharacters => UiText.Current.SendUnsupportedCharacters(Detail ?? string.Empty),
        SendOutcome.InjectionBlocked => UiText.Current.SendInjectionBlocked,
        _ => UiText.Current.SendUnknown,
    };
}

/// <summary>
/// Performs the in-game chat macro: open the chat prompt, type, submit.
///
/// Runs off the UI thread because it deliberately sleeps between keystrokes, and
/// serialises itself so two messages can never interleave into one chat line.
/// </summary>
public sealed class ChatSender : IDisposable
{
    private readonly CondorWatcher _watcher;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private DateTime _lastSentUtc = DateTime.MinValue;

    public ChatSender(CondorWatcher watcher) => _watcher = watcher;

    public void Dispose() => _gate.Dispose();

    public Task<SendResult> SendAsync(string message, AppConfig config)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return Task.FromResult(new SendResult(SendOutcome.NothingToSend));
        }

        if (!_watcher.IsRunning)
        {
            return Task.FromResult(new SendResult(SendOutcome.CondorNotRunning));
        }

        TimeSpan sinceLast = DateTime.UtcNow - _lastSentUtc;
        if (sinceLast < TimeSpan.FromSeconds(config.MinSecondsBetweenMessages))
        {
            return Task.FromResult(new SendResult(SendOutcome.RateLimited));
        }

        if (!_gate.Wait(0))
        {
            return Task.FromResult(new SendResult(SendOutcome.Busy));
        }

        return Task.Run(() =>
        {
            try
            {
                return Type(message, config);
            }
            finally
            {
                _gate.Release();
            }
        });
    }

    private SendResult Type(string message, AppConfig config)
    {
        IntPtr window = _watcher.GetForegroundCondorWindow();
        if (window == IntPtr.Zero)
        {
            return new SendResult(SendOutcome.CondorNotFocused);
        }

        IntPtr layout = InputSender.GetLayoutFor(window);

        // The PTT or confirm key may still be physically held; typing with Ctrl or
        // Shift stuck down would send the wrong characters entirely.
        InputSender.ReleaseHeldModifiers(layout);
        Thread.Sleep(config.KeyDelayMs);

        // The first tap is the canary: if Windows refuses this one, every following
        // keystroke would be swallowed too, and we would silently do nothing.
        if (!InputSender.Tap((int)config.OpenChatKey, layout, config.KeyDelayMs / 2))
        {
            return new SendResult(SendOutcome.InjectionBlocked);
        }

        Thread.Sleep(config.ChatOpenDelayMs);

        var skipped = new List<char>();

        foreach (char ch in message)
        {
            if (!InputSender.TypeChar(ch, layout, config.KeyDelayMs))
            {
                skipped.Add(ch);
            }

            Thread.Sleep(config.KeyDelayMs);

            // Focus can be lost mid-message (alt-tab, a crash). Stop rather than
            // spraying the rest of the sentence into another application.
            if (_watcher.GetForegroundCondorWindow() == IntPtr.Zero)
            {
                return new SendResult(SendOutcome.CondorNotFocused);
            }
        }

        Thread.Sleep(config.BeforeSendDelayMs);
        InputSender.Tap((int)config.SendChatKey, layout, config.KeyDelayMs / 2);

        _lastSentUtc = DateTime.UtcNow;

        return skipped.Count > 0
            ? new SendResult(SendOutcome.UnsupportedCharacters, new string(skipped.Distinct().ToArray()))
            : new SendResult(SendOutcome.Sent);
    }
}
