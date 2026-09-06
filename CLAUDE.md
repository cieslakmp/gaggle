# CLAUDE.md

Gaggle is a Windows tray app: hold a push-to-talk key, speak, and the message is
transcribed locally by Whisper and typed into Condor Soaring Simulator's in-game chat.

C# on .NET 10, WinForms, x64 only. See [README.md](README.md) for user-facing docs.

## Commands

```bash
dotnet build src/Gaggle/Gaggle.csproj -c Release
dotnet publish src/Gaggle/Gaggle.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish
```

The project builds with `TreatWarningsAsErrors`, so **a new warning is a broken
build**. Fix the cause; only suppress in `.editorconfig`, with a comment saying why.

There is no test project yet. Changes to the speech path can be exercised by
transcribing a WAV through `WhisperTranscriber` and `MessageSanitiser` directly.

## Invariants

These four look like arbitrary complexity and are not. Breaking any of them produces
a build that compiles, runs, and silently does nothing useful.

**Keystrokes must be injected as scan codes.** Condor reads the keyboard through
DirectInput, which looks at hardware scan codes. Virtual-key `SendInput`,
`SendKeys`, and `PostMessage` all produce *nothing* in game — no error, no effect.
See `Interop/InputSender.cs`. Also check the `SendInput` return value: zero accepted
events means UIPI blocked us, which is what happens when Condor is elevated and
Gaggle is not.

**Keyboard hook handlers must return fast.** `PushToTalkHook` installs a
`WH_KEYBOARD_LL` hook. If a callback takes longer than the Windows
`LowLevelHooksTimeout` (300 ms default), Windows silently uninstalls the hook and
push-to-talk stops working with no error anywhere. Every handler defers real work
via `BeginInvokeOnUi`. Never do I/O, transcription, or `Thread.Sleep` inline in a
hook handler.

**The review overlay must never take focus.** If it activated, Condor would lose the
foreground, the injected keystrokes would land in the overlay instead of the game,
and a full-screen sim would likely minimise. Hence `WS_EX_NOACTIVATE` plus
`ShowWithoutActivation`, with confirm/cancel keys read by the global hook rather
than by the window. Do not add focusable controls to it.

**Audio must be RMS-gated before transcription.** Fed near-silence, Whisper
confidently invents stock phrases from its training data — "Thank you.", "Thanks for
watching!". Without the gate in `TranscribeAsync` those get broadcast to a live race.
`MessageSanitiser` is the second line of defence, not the first.

## Layout

| Path | Role |
|---|---|
| `Interop/` | Win32 P/Invoke, scan-code injection, the PTT hook |
| `Audio/` | NAudio capture at 16 kHz mono — the format Whisper requires |
| `Speech/` | Whisper.net wrapper and ggml model download |
| `Text/` | Transcript sanitising before anything reaches chat |
| `Condor/` | Process/foreground detection and the chat macro |
| `Ui/` | Tray icon, state machine, review overlay |

`Ui/TrayApplicationContext.cs` owns the state machine and is where the pieces meet:
`Idle → Recording → Transcribing → Review → Idle`.

## Conventions

P/Invoke declarations in `Interop/NativeMethods.cs` mirror Win32 names and signatures
exactly, including underscores and Hungarian parameter names. That is deliberate —
matching MSDN is worth more than matching .NET naming. The relevant analyser rules
are disabled for this reason.

Config lives in `%APPDATA%\Gaggle\config.json`, written on first run. `Keys` values
serialise by name, and note that `Keys.Enter` round-trips as `Return` — they are the
same underlying value.

## Watch out

This types into chat shared with other people in a live race. The rate limit
(`MinSecondsBetweenMessages`) and the review step (`ReviewBeforeSending`) exist for
that reason — do not remove either, or change their defaults, without being asked.

The full flow is verified working against a live Condor install, so the approach is
sound. If a change breaks it, suspect `ChatOpenDelayMs`, the process name, or
elevation before suspecting the injection approach itself.
