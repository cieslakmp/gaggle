# CLAUDE.md

Gaggle is a Windows tray app: hold a push-to-talk key, speak, and the message is
transcribed locally by Whisper and typed into Condor Soaring Simulator's in-game chat.

C# on .NET 10, WinForms, x64 only. See [README.md](README.md) for user-facing docs.

## Commands

```bash
dotnet build -c Release
dotnet test -c Release
dotnet publish src/Gaggle/Gaggle.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish
```

The project builds with `TreatWarningsAsErrors`, so **a new warning is a broken
build**. Fix the cause; only suppress in `.editorconfig`, with a comment saying why.

Tests live in `tests/Gaggle.Tests` (xunit.v3). They cover the sanitiser, bindings and
their config migration, config load/save, download progress and the RMS gate — the
logic that can be checked without Condor, a microphone or a joystick. Anything
touching `Interop/` needs real hardware and is verified by hand.

`global.json` opts into Microsoft.Testing.Platform. The .NET 10 SDK refuses to run
tests through VSTest, and xunit.v3 hosts the new platform itself, so do not add
Microsoft.NET.Test.Sdk or xunit.runner.visualstudio back: they reintroduce the VSTest
targets and `dotnet test` fails outright.

## Releasing

Tagging `v*` triggers `.github/workflows/release.yml`, which builds, tests, packages
and opens a draft release. The tag must be annotated — its message becomes the release
notes — and `<Version>` in the csproj must match the tag, or the workflow fails on
purpose.

## Invariants

These look like arbitrary complexity and are not. Breaking any of them produces
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

**Joystick buttons cannot be swallowed.** A keyboard PTT key is hidden from Condor by
the hook. A joystick button is read by Condor directly from the device, so there is no
way to intercept it — the settings window warns about this rather than pretending
otherwise. Do not add code that claims to suppress a bound button.

**A non-English language needs a multilingual model.** Ask a `ggml-*.en.bin` build for
Polish and whisper.cpp silently *discards* both the language and the translate request:
no exception, and nothing logged at any level. It transcribes the audio phonetically as
English instead — "Lecę w prawo" comes back as "Les W. Pero W." — and `segment.Language`
still reports `en`. Verified against ggml-base.en.bin and ggml-small.en.bin: the output
is byte-for-byte identical whether translation was asked for or not, which is how you
can tell the parameters were ignored. `LoadModelAsync` refuses that pairing on purpose;
do not "simplify" the check away, because nothing downstream will report the problem.
`WithTranslate()` only ever goes *into* English, so the language list can never gain an
output language.

**Audio must be RMS-gated before transcription.** Fed near-silence, Whisper
confidently invents stock phrases from its training data — "Thank you.", "Thanks for
watching!". The multilingual builds reach for subtitle credits instead ("Subtitles by
the Amara.org community"), and translation carries those into English whatever was
spoken. Without the gate in `TranscribeAsync` those get broadcast to a live race.
`MessageSanitiser` is the second line of defence, not the first.

## Layout

| Path | Role |
|---|---|
| `Interop/` | Win32 P/Invoke, scan-code injection, the PTT hook, winmm joystick |
| `Input/` | Push-to-talk binding, joystick polling, the keyboard/joystick merge |
| `Audio/` | NAudio capture at 16 kHz mono — the format Whisper requires |
| `Speech/` | Whisper.net wrapper, language list, decoding options, ggml model download |
| `Text/` | Transcript sanitising before anything reaches chat |
| `Condor/` | Process/foreground detection and the chat macro |
| `Ui/` | Tray icon, state machine, review overlay, settings window |
| `tests/Gaggle.Tests/` | xunit.v3 suite for the hardware-free logic |

`Ui/TrayApplicationContext.cs` owns the state machine and is where the pieces meet:
`Idle → Recording → Transcribing → Review → Idle`.

## Conventions

P/Invoke declarations in `Interop/NativeMethods.cs` mirror Win32 names and signatures
exactly, including underscores and Hungarian parameter names. That is deliberate —
matching MSDN is worth more than matching .NET naming. The relevant analyser rules
are disabled for this reason.

Language and decoding settings are read fresh for every utterance rather than baked
into the loaded model, so switching language costs nothing. Only `WhisperModelFile`
forces a reload.

`Language` and `WhisperModelFile` are two halves of one decision, and both tray menus
maintain that: choosing English moves to an English build, choosing any-language moves
to a multilingual one, and picking an English-only model sets the language to English.
The menus therefore cannot produce the pairing `LoadModelAsync` refuses — that check now
only catches a hand-edited config. The tray offers two language choices, not five,
because with translation on `pl`, `de` and `es` all decode through the same path to
near-identical English; the per-language codes survive in config for pinning one when
detection picks wrong.

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
