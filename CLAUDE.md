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
their config migration, config load/save, download progress, the RMS gate and the update
checker — the logic that can be checked without Condor, a microphone or a joystick.
Anything touching `Interop/`, and the update swap itself, needs real hardware or a real
install and is verified by hand.

`global.json` opts into Microsoft.Testing.Platform. The .NET 10 SDK refuses to run
tests through VSTest, and xunit.v3 hosts the new platform itself, so do not add
Microsoft.NET.Test.Sdk or xunit.runner.visualstudio back: they reintroduce the VSTest
targets and `dotnet test` fails outright.

## Releasing

Tagging `v*` triggers `.github/workflows/release.yml`, which builds, tests, packages
and opens a draft release. It publishes the zip and a `.sha256` beside it; the in-app
updater will not install a release that has no checksum. The tag must be annotated — its
message becomes the release notes.

The tag is also where the version comes from: `release.yml` passes `-p:Version=<tag>` to
the build and publish, and `<Version>` in the csproj is a `0.0.0` placeholder. So a
release needs no commit of its own — which is the point, because a version-bump commit on
`main` fired a second, identical CI run for every release. Nothing enforces the two
agreeing any more because they cannot disagree, but the injection *is* silent when it
fails: a mistyped property leaves the placeholder in place and ships a zip stamped 0.0.0.
The "Check the build was stamped with the tag version" step reads it back off the built
assembly for that reason. Do not drop it.

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

**A cue tone must never be played synchronously.** `Console.Beep` blocks the calling
thread for the whole tone. Every cue fires from the UI thread, which is the thread the
`WH_KEYBOARD_LL` hook is installed on, so a 200 ms beep spends most of the 300 ms
`LowLevelHooksTimeout` budget and Windows uninstalls the hook with no error anywhere.
`Audio/CueTones.cs` uses `SoundPlayer`, which hands off to its own thread, and warms the
players off the UI thread at startup for the same reason. `SystemSounds` is wrong here
for a quieter reason: it plays nothing at all under the "No Sounds" scheme, which is
silence in the one mode where sound is the only feedback there is.

**Audio must be RMS-gated before transcription.** Fed near-silence, Whisper
confidently invents stock phrases from its training data — "Thank you.", "Thanks for
watching!". The multilingual builds reach for subtitle credits instead ("Subtitles by
the Amara.org community"), and translation carries those into English whatever was
spoken. Without the gate in `TranscribeAsync` those get broadcast to a live race.
`MessageSanitiser` is the second line of defence, not the first.

**The update path must survive its own failure.** `Update/UpdateInstaller.cs` replaces a
running executable, which Windows will not let a process do to itself. Three things hold
it together and all three look like fussiness. The install folder is located through
`Environment.ProcessPath` because `Assembly.Location` is *empty* under
`PublishSingleFile` — read it there and the updater silently targets nothing. The
downloaded zip is checked against the SHA256 published beside it *before* anything is
extracted, and nothing under the install folder is touched until that passes. And
`apply.cmd` waits for this process to exit and gives up rather than forcing it: a failed
update has to leave a working install behind, because the alternative is a pilot with no
app and a race starting. The release assets are matched by suffix (`-win-x64.zip`,
`-win-x64.zip.sha256`) rather than by rebuilding the file name, so renaming them in
`release.yml` breaks every copy already installed — and no test will catch it.

## Layout

| Path | Role |
|---|---|
| `Interop/` | Win32 P/Invoke, scan-code injection, the PTT hook, winmm joystick |
| `Input/` | Push-to-talk binding, joystick polling, the keyboard/joystick merge |
| `Audio/` | NAudio capture at 16 kHz mono — the format Whisper requires — and the cue tones |
| `Net/` | The one streaming download loop, shared by models and updates |
| `Speech/` | Whisper.net wrapper, language list, decoding options, ggml model download |
| `Text/` | Transcript sanitising before anything reaches chat |
| `Condor/` | Process/foreground detection and the chat macro |
| `Update/` | GitHub release lookup, version comparison, the self-replacing install |
| `Ui/` | Tray icon, state machine, review overlay, settings window |
| `IssueLink.cs` | Prefilled links to the GitHub issue forms in `.github/ISSUE_TEMPLATE` |
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

`ReloadConfig` in `Ui/TrayApplicationContext.cs` copies config fields across one by one.
A new `AppConfig` property that is not added there is silently dropped by "Reload config".

`UiLanguage` is the language the getting-started guide is written in, and is deliberately
not `Language`, which is what the pilot speaks to Whisper. They look like the same
decision and are not: `Language` defaults to English for everyone, including the Polish
pilots `UiLanguage` exists for. The tray keeps them apart by name — **Speech language**
against **Display language** — and both menus sit next to each other so the difference is
visible rather than remembered.

`Ui/UiText.cs` holds the interface and `Ui/OnboardingText.cs` the guide, both as one
record instance per language drawing on the same `UiLanguages` list. Members are
`required`, so adding a string fails the build until all four languages have one, and a
string with arguments is a `Func` rather than a format string, so a translation that
takes an argument and forgets to use it fails a test rather than shipping a sentence with
a hole in it. `UiTextTests` checks exactly that, plus that no language is quietly still
English. A `.resx` file would fall back silently on the first and not check the second.

`UiText.Current` is a field, not an auto-property: a property initialiser there runs
before the language tables below it and assigns null. Changing language rebuilds the tray
menu, because half its items are created inside `BuildMenu` — and the rebuild has to
remove the four items that outlive it before disposing the old menu, since disposing a
menu disposes everything in it. Both rebuild paths defer through `BeginInvokeOnUi`, as
they run from a click on the menu being replaced.

Config lives in `%APPDATA%\Gaggle\config.json`, written on first run. `Keys` values
serialise by name, and note that `Keys.Enter` round-trips as `Return` — they are the
same underlying value.

`IssueLink` prefills the GitHub issue forms by query parameter, which means the template
file names and the field ids in `.github/ISSUE_TEMPLATE/*.yml` are a contract with the
app. GitHub answers an unknown template, or a parameter matching no field id, with an
ordinary empty form — no error, nothing in the URL to say the prefill was dropped. Rename
a template or a field id and `IssueLinkTests` is the only thing that will notice.

## Watch out

This types into chat shared with other people in a live race. The rate limit
(`MinSecondsBetweenMessages`) and the review step (`ReviewBeforeSending`) exist for
that reason — do not remove either, or change their defaults, without being asked.

Hands-free is `ReviewBeforeSending: false`, and it is opt-in for VR, where the overlay
cannot be seen or answered. With it on the RMS gate and `MessageSanitiser` stop being the
second line of defence and become the only one, so anything that weakens them weighs more
than it looks. Three things follow, and none are obvious from the code:

- The watchdog (`HandsFreeMaxRecordingSeconds`) *sends* what it stops, it does not discard
  it. That is deliberate, and it is why the hands-free limit is shorter than
  `MaxRecordingSeconds`.
- `SendOutcome.UnsupportedCharacters` means the message **was** sent — `ChatSender` taps
  the send key before it reports the characters it could not type — so it earns the sent
  cue, not the dropped one.
- Toggling hands-free must call `DiscardPending()`. A transcript already awaiting Enter
  has the hook swallowing Enter and Escape, and would otherwise fire into chat on the next
  Enter pressed in Condor.

The full flow is verified working against a live Condor install, so the approach is
sound. If a change breaks it, suspect `ChatOpenDelayMs`, the process name, or
elevation before suspecting the injection approach itself.
