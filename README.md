# Gaggle

[![CI](https://github.com/cieslakmp/gaggle/actions/workflows/ci.yml/badge.svg)](https://github.com/cieslakmp/gaggle/actions/workflows/ci.yml)
[![Release](https://img.shields.io/github/v/release/cieslakmp/gaggle)](https://github.com/cieslakmp/gaggle/releases/latest)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

Push-to-talk voice chat for [Condor Soaring Simulator](https://www.condorsoaring.com/).

Hold a key or a joystick button, say your message, and Gaggle transcribes it locally
and types it into Condor's in-game chat — so you can talk to the gaggle without taking
a hand off the stick to type.

> A *gaggle* is the cluster of gliders sharing a thermal. That's who you're talking to.

Speak English, Polish, German or Spanish — anything that isn't English is translated
to English on the way into chat, so the gaggle can read it.

Speech recognition and translation both run entirely on your own machine. No API key,
no account, no cost, and no network round-trip in the middle of a race.

## Download

Grab the latest zip from [**Releases**](https://github.com/cieslakmp/gaggle/releases/latest)
and extract it somewhere permanent. Nothing to install: no .NET runtime, no
administrator rights.

Keep `Gaggle.exe` and the `runtimes` folder **together** — the executable alone cannot
load the speech model.

Needs Windows 10 or 11, 64-bit, plus room for the speech model it downloads on first
run: 141 MB for English, or 465 MB if you want it to understand other languages.

### Windows will warn you

Gaggle is not code-signed, so the first launch shows **"Windows protected your PC"**.
That is SmartScreen reacting to an unknown publisher, not a virus detection. Click
**More info**, then **Run anyway**.

Windows also stamps anything extracted from a downloaded zip as untrusted, and that
mark follows the files out of the archive. Clearing it on the zip before extracting
saves the argument:

```powershell
Unblock-File .\Gaggle-*-win-x64.zip
```

A signing certificate would remove the warning, but it is a recurring cost, so for now
the warning stays.

### Updating

Gaggle checks GitHub for a new release once a day and, when it finds one, offers it in
the tray menu with the release notes. Accepting downloads the new zip, checks it against
the SHA256 published with the release, and swaps it in — Gaggle closes and reopens by
itself, usually in a few seconds. Nothing in your folder is touched until the download
has been verified, and the update never asks for administrator rights.

Because the app downloads the zip directly, the SmartScreen warning above does *not*
come back for an updated copy.

Two things to know:

- **Installed somewhere you cannot write to** — `C:\Program Files`, say, or a read-only
  share — and Gaggle offers the releases page instead of updating itself. Extract it
  over the old copy by hand, as on first install.
- **Skipping a version** silences the daily check for that release only. **Check for
  updates…** in the tray menu ignores it, so nothing is ever lost for good.

To turn the daily check off entirely, set `CheckForUpdates` to `false` in
[the config file](#configuration). The tray menu item still works when you ask it to.

## How it works

```
hold PTT key  ──►  record mic  ──►  Whisper (local)  ──►  review overlay  ──►  Condor chat
   (hook)          (16 kHz WAV)      (offline STT)         (confirm/discard)     (SendInput)
```

1. A global low-level keyboard hook watches for the push-to-talk key and **swallows
   it**, so Condor never sees it and won't fire whatever it has bound there.
2. The microphone is captured at 16 kHz mono — the exact format Whisper wants.
3. On key release, [whisper.cpp](https://github.com/ggerganov/whisper.cpp) transcribes
   the clip locally, in about a second — translating it to English first if you were
   speaking something else.
4. The transcript appears in a small always-on-top overlay that **never takes focus**.
   Press Enter to send, Escape to discard.
5. Gaggle taps Backspace to open Condor's chat, types the message, and presses Enter.

## First run

Gaggle lives in the notification area — there is no main window.

1. Right-click the tray icon → **Speech model** → pick one. It downloads to
   `%APPDATA%\Gaggle`, with progress shown in the overlay and the tray tooltip.
2. Right-click → **Microphone** → pick your headset.
3. Right-click → **Push-to-talk…** to bind a key or a joystick button, unless the
   Caps Lock default suits you.
4. Right-click → **Speech language** if you speak something other than English. This
   needs a multilingual model — see below.
5. Start Condor. The tray icon turns blue when everything is ready.
6. Hold your push-to-talk control, speak, release.

Change your mind mid-sentence? **Keep holding.** Past the recording limit — 15 seconds
by default, 10 hands-free — the clip is thrown away instead of transcribed, and you get
the dropped tone. It is the only way to take a message back in VR, where the review
overlay cannot be seen.

## Push-to-talk binding

Right-click the tray icon → **Push-to-talk…**, press **Change**, then press the key or
joystick button you want. Whatever you press next becomes the binding.

Keyboard keys are swallowed by the hook, so Condor never sees them. **Joystick buttons
cannot be swallowed** — Condor reads the device directly — so pick a button the sim
does not already use, or it will do both things at once. The settings window says so
when a button is bound.

Joysticks are read through the legacy winmm API, which sees **32 buttons across 16
devices**. That covers ordinary HOTAS and button boxes; a device exposing more than 32
buttons would need DirectInput instead.

## Hands-free (VR)

In a headset the review overlay is a desktop window you cannot see, and the key that
answers it is a keyboard you cannot look at. For VR the review step does not add safety,
it removes the feature.

Right-click the tray icon → **Send without review (hands-free)**, or tick it in the
push-to-talk settings window. Hold the button, speak, release: the transcript goes
straight into chat. It asks once before turning on, and never asks again.

What changes when it is on:

- **Nothing reads the message before Condor does.** The silence gate and the
  hallucination filter are all that stand between Whisper and a live race chat. They
  catch the common cases and are not a substitute for you.
- **Recordings are cut shorter** — `HandsFreeMaxRecordingSeconds`, 10 by default rather
  than 15. Reaching the limit throws the recording away, so holding on is how you take a
  message back — the only way there is, with no overlay to answer. It ends once: a stuck
  button gives one abandoned recording, not one every ten seconds.
- The rate limit still applies, unchanged.

Turn on **Play audible cues** with it. Every other signal Gaggle has — the overlay, the
tray icon, balloon tips — is a desktop visual, so in a headset the tones are the only
feedback there is: a short tick when recording starts, a higher tone when a message
reaches chat, and a low one when it does not, whatever the reason. They work in review
mode too.

## Configuration

`%APPDATA%\Gaggle\config.json`, created on first run. Edit it from the tray menu
(**Open config file**), then **Reload config**. No rebuild needed.

| Setting | Default | Notes |
|---|---|---|
| `ProcessName` | `Condor` | Process to watch, without `.exe` |
| `OpenChatKey` | `Back` | Key that opens Condor's chat prompt |
| `SendChatKey` | `Return` | Key that submits the message |
| `PushToTalk` | Caps Lock | Key or joystick button held to record. Set it from the tray, not by hand |
| `ConfirmKey` / `CancelKey` | `Return` / `Escape` | Only active while a review is open |
| `ReviewBeforeSending` | `true` | `false` is hands-free — see [Hands-free](#hands-free-vr) |
| `AudibleFeedback` | `false` | Play a tone when recording starts, sends, or is dropped |
| `HandsFreeMaxRecordingSeconds` | `10` | Hold this long to cancel while hands-free, shorter than the one above |
| `KeyDelayMs` | `30` | Gap between injected keystrokes |
| `ChatOpenDelayMs` | `200` | Wait for the chat prompt to appear |
| `MinSecondsBetweenMessages` | `3` | Rate limit |
| `MaxRecordingSeconds` | `15` | Hold this long and the recording is thrown away |
| `SilenceThresholdRms` | `0.005` | Below this, audio is never transcribed |
| `WhisperModelFile` | `ggml-base.en.bin` | Model in `%APPDATA%\Gaggle` |
| `Language` | `en` | `en` or `auto` from the tray; `pl`/`de`/`es` by hand to pin one |
| `UiLanguage` | Windows display language | `English` or `Polish` — the language Gaggle's own menus are written in |
| `TranscriptionThreads` | `0` | `0` uses every hardware thread. Lower it if Condor stutters |
| `FastTranscription` | `true` | Trims Whisper's 30-second window to what you actually said |
| `MaxMessageLength` | `120` | Longest message typed into chat |
| `CheckForUpdates` | `true` | Daily check against the GitHub releases API. See [Updating](#updating) |
| `SkippedVersion` | unset | A release you chose to skip. Cleared by picking a newer one |

## Languages

Right-click the tray icon → **Speech language**. There are two choices, because there
are only two behaviours:

| Choice | What happens | Model |
|---|---|---|
| **English** | Transcribed as spoken | A dedicated English build — smaller and faster at English |
| **Any language → English** | Detected, then translated | A multilingual build |

Picking one settles the model too, so the two menus can never disagree. Switching to
English prefers an English build you already have and offers the same tier if you have
none; switching to *any language* does the same with the multilingual builds. Declining
the download leaves the language alone, so nothing breaks.

There is no per-language picker, and that is deliberate. With translation on, Whisper
decodes Polish, German and Spanish into English through the same path and produces
near-identical output whichever you name — so a menu of languages would imply a
decision that is not really being made. If detection ever picks wrong for you, you can
still pin a language by hand: set `Language` in the config file to `pl`, `de` or `es`.

| Model | Size | Use |
|---|---|---|
| Tiny / Base / Small **(English)** | 141 MB – 465 MB | The **English** choice |
| **Small (multilingual)** | ~465 MB | The realistic floor for **any language** |
| **Medium (multilingual)** | ~1.5 GB | Best accuracy, roughly 3× slower |

Multilingual costs nothing extra in download size at the same tier: `ggml-base.bin` and
`ggml-base.en.bin` are both 141 MB. The larger multilingual models are bigger because
they are bigger models, not because they are multilingual.

### Interface language

Right-click the tray icon → **App language** for **English** or **Polski**. The menus,
the settings window and the overlay switch straight away — no restart.

This is a different setting from **Speech language** above, and the two do not affect
each other. **Speech language** is what you say into the microphone; **App language** is
what Gaggle's own menus are written in. A Polish pilot who calls in English wants a
Polish interface *and* the English-only speech model, which is the faster pairing.

On a fresh install Gaggle follows your Windows display language, so Polish Windows
starts in Polish. Change it once and the choice is remembered (`UiLanguage` in the
config file).

### If transcription is slow

Transcription competes with Condor for the CPU, and the multilingual models are
heavier than the English ones. Two knobs, both in the config file:

- `FastTranscription` (on by default) trims Whisper's fixed 30-second analysis window
  down to the length you actually spoke. This is the big saving on short radio calls.
  Turn it off if transcripts start losing words.
- `TranscriptionThreads` defaults to every hardware thread on the machine. If a message
  causes a frame-rate hitch, set it to your physical core count or half your logical
  one — Condor needs cores too.

If it is still too slow, use Small rather than Medium.

## Etiquette

This types into a live race chat shared with other people. The rate limit is on by
default, and the review step exists because transcription is never perfect. Please
leave both on until you trust it.

[Hands-free](#hands-free-vr) turns the review step off, and is there because in VR
there is no way to answer it. On a monitor you can see the overlay, so the honest
answer is that you do not need it — and it is the other pilots in the chat who pay for
a mishearing you never got to read.

## Testing it

You do not need to be in a race, or even in Condor, to see what Gaggle would send.

Gaggle refuses to record unless the process it watches is running, so point it at
something else for a moment. Set `ProcessName` to `notepad` in the config file,
**Reload config** from the tray, and start Notepad. Hold push-to-talk, speak, and the
transcript appears in the review overlay exactly as it would in flight — press Escape
to discard it. That alone exercises capture, the silence gate, transcription and
translation.

Keep Notepad focused and press Enter instead, and the full macro types into Notepad so
you can read the message character for character. It arrives slightly mangled, because
the key that opens Condor's chat is Backspace and Notepad treats that as an edit. That
is the macro working, not a fault.

Set `ProcessName` back to `Condor` when you are done.

### What is worth reporting

Some of the behaviour below is measured and some is guessed, and the guesses are where
feedback helps most.

- **Translation quality.** Whisper's translate task is confirmed to *run* — the spoken
  language is honoured and English comes out — but it has never been checked against
  real speech, only synthesised audio. If Polish, German or Spanish arrives as
  nonsense, that is the most useful thing you can report. Say what you spoke and what
  appeared.
- **`FastTranscription`.** It makes transcription roughly 3× faster and can cost
  accuracy. If words go missing, or a sentence arrives only half-translated, set it to
  `false`, repeat the same phrase, and say whether that fixed it.
- **Frame-rate hitches** while a message transcribes, especially on Medium. Lower
  `TranscriptionThreads` and say whether it helped.
- **`MaxMessageLength`.** The 120 default is a guess, not Condor's measured limit. If a
  long message is cut short in game, the real number would settle it.
- **Anything the review overlay shows that you did not say.** The sanitiser drops known
  Whisper hallucinations, but the list only covers the ones seen so far.

**Report a bug…** and **Suggest an idea…** in the tray menu open the matching form on
GitHub with your version, speech model, language, microphone and Windows build already
filled in, so there is nothing to look up first. The forms also live at
[Issues](https://github.com/cieslakmp/gaggle/issues/new/choose) if you would rather not
go through the app.

A bug report is worth far more with your `config.json` pasted in. **About Gaggle** links
to the folder holding it, next to the version — which is in the tray tooltip too.

## Status

**Verified working end to end against a live Condor install** — push-to-talk capture,
local transcription, the review overlay, and scan-code injection into Condor's chat.

Speech-path timings on the reference machine: `ggml-base.en.bin` loads in ~330 ms and
transcribes a 6.7 s clip in ~1.1 s, with the sanitiser rejecting silence,
hallucinations and annotations. On `ggml-small.bin` (multilingual) a 7 s clip takes
~1.3 s with `FastTranscription` on, against ~3.9 s with it off.

Known unknowns, none of them blocking:

- **Translation quality is unverified.** The mechanism is confirmed — the spoken
  language is honoured and the output is English — but every check used synthesised
  audio, so how well real Polish, German or Spanish survives is genuinely unknown
- `FastTranscription` trades accuracy for speed and is on by default; one measured run
  came back less completely translated than the same clip with it off
- `MaxMessageLength` (120) is a conservative guess, not Condor's measured limit
- Timings are tuned on one machine; a slower system may need a larger `ChatOpenDelayMs`
- Tested with Condor 3

**Elevation is not needed.** Condor and Gaggle both run as a normal user, which is how
this was tested. Only if you deliberately run Condor as administrator must Gaggle be
elevated too — UIPI blocks input injection from a lower integrity level. Gaggle detects
that case: `SendInput` accepting zero events on the first keystroke is reported as
"Windows blocked the keystrokes" rather than failing silently.

---

# Building from source

Needs the [.NET 10 SDK](https://dotnet.microsoft.com/download). Windows only — the
project targets `net10.0-windows` and uses WinForms.

```bash
dotnet build -c Release
```

Self-contained build needing no .NET install on the target machine:

```bash
dotnet publish src/Gaggle/Gaggle.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish
```

Despite `PublishSingleFile`, this produces `Gaggle.exe` **plus a `runtimes/` folder**
holding whisper.cpp's native libraries, which Whisper.net probes for at load time.
Both must ship together.

## Tests

```bash
dotnet test -c Release
```

199 tests over the parts that can be checked without Condor, a microphone or a
joystick: the transcript sanitiser, push-to-talk bindings and their config migration,
config load and save, the cue tone buffers, download progress formatting, the RMS
silence gate, and the update checker — version comparison, release parsing, the daily throttle and the
checksum reader.

Input injection, the keyboard hook, joystick polling and the update swap itself are not
covered — they need real hardware, a running sim or a real install, and are verified by
hand.

Note the `global.json`: the .NET 10 SDK no longer runs tests through VSTest, so the
repo opts into Microsoft.Testing.Platform, which xunit.v3 hosts directly. Adding
`Microsoft.NET.Test.Sdk` or `xunit.runner.visualstudio` back would reintroduce the
VSTest targets and break `dotnet test` outright.

## Design notes

Five things that are easy to get wrong, and why the code looks the way it does:

**Scan codes, not virtual keys.** Condor reads the keyboard through DirectInput,
which looks at hardware scan codes. `SendKeys`, `PostMessage`, and virtual-key
`SendInput` all produce nothing in game. See `Interop/InputSender.cs`.

**A hook, not `RegisterHotKey`.** Push-to-talk needs the key *release*, which
`RegisterHotKey` never reports. The hook callback must return within ~300 ms or
Windows silently uninstalls it, so every handler defers its real work.

**The overlay must not take focus.** If it did, Condor would lose the foreground and
the keystrokes would land in the overlay instead — and a full-screen sim would likely
minimise. Hence `WS_EX_NOACTIVATE`, with confirm/cancel read by the hook.

**Whisper hallucinates on silence.** Fed an empty channel it confidently produces
"Thank you." or "Thanks for watching!". An RMS gate rejects quiet audio before
transcription, and a blocklist catches the rest. See `Text/MessageSanitiser.cs`.

**Joystick buttons cannot be suppressed.** Unlike a keyboard key, a bound button is
still seen by Condor. There is no interception point; the UI warns instead.

## Code style

`.editorconfig` holds the conventions, and the project builds with
`TreatWarningsAsErrors` plus `latest-recommended` analysers, so style and correctness
rules are enforced at build time rather than in review.

A handful of analyser rules are turned off in `.editorconfig`, each with a comment
saying why — mostly rules that fight P/Invoke code, where matching the Win32
signature exactly matters more than matching .NET naming conventions.

## Releasing

Tag and push. There is nothing to bump first:

```bash
git tag -a v0.6.0 -m "Gaggle 0.6.0" && git push origin v0.6.0
```

The release workflow builds, runs the tests, packages `Gaggle.exe` with its
`runtimes` folder, and opens a **draft** release using the annotated tag message as
the notes. Review it on the Releases page and publish when it reads right.

The version comes from the tag: the workflow passes it to the build and then reads it
back off the assembly, so a binary cannot ship stamped with a version that was never
released. `<Version>` in the csproj is a `0.0.0` placeholder, which is what a build
from source honestly is.

## Contributing

Pull requests are welcome — open an issue first for anything beyond a typo, because
some of what looks like missing polish is deliberate. [CONTRIBUTING.md](CONTRIBUTING.md)
has the process, how to build and test, and the invariants a change must not break.

## License

MIT — see [LICENSE](LICENSE).
