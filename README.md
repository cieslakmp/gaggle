# Gaggle

Push-to-talk voice chat for [Condor Soaring Simulator](https://www.condorsoaring.com/).

Hold a key, say your message, and Gaggle transcribes it locally and types it into
Condor's in-game chat — so you can talk to the gaggle without taking a hand off the
stick to type.

> A *gaggle* is the cluster of gliders sharing a thermal. That's who you're talking to.

## How it works

```
hold PTT key  ──►  record mic  ──►  Whisper (local)  ──►  review overlay  ──►  Condor chat
   (hook)          (16 kHz WAV)      (offline STT)         (confirm/discard)     (SendInput)
```

1. A global low-level keyboard hook watches for the push-to-talk key and **swallows
   it**, so Condor never sees it and won't fire whatever it has bound there.
2. The microphone is captured at 16 kHz mono — the exact format Whisper wants.
3. On key release, [whisper.cpp](https://github.com/ggerganov/whisper.cpp) transcribes
   the clip locally. No API key, no cost, no network round-trip mid-race.
4. The transcript appears in a small always-on-top overlay that **never takes focus**.
   Press Enter to send, Escape to discard.
5. Gaggle taps Backspace to open Condor's chat, types the message, and presses Enter.

Everything runs offline.

## Requirements

- Windows 10/11 x64
- [.NET 10 SDK](https://dotnet.microsoft.com/download) to build
- A Whisper model — downloaded on first run from the tray menu (~148 MB for the default)

## Build

```bash
dotnet build src/Gaggle/Gaggle.csproj -c Release
```

Self-contained build needing no .NET install on the target machine:

```bash
dotnet publish src/Gaggle/Gaggle.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish
```

This produces `Gaggle.exe` (~118 MB) **plus a `runtimes/` folder** holding whisper.cpp's
native libraries, which Whisper.net probes for at load time. Ship both — the exe alone
will fail when it tries to load the speech model.

## First run

Gaggle lives in the notification area. On first launch:

1. Right-click the tray icon → **Speech model** → pick one. It downloads to
   `%APPDATA%\Gaggle`, with progress shown in the overlay and the tray tooltip.
2. Right-click → **Microphone** → pick your headset.
3. Start Condor. The tray icon turns blue when it's ready.
4. Right-click → **Push-to-talk…** to bind a key or a joystick button, if you want
   something other than the Caps Lock default.
5. Hold the push-to-talk control, speak, release.

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

## Configuration

`%APPDATA%\Gaggle\config.json`, created on first run. Edit it from the tray menu
(**Open config file**), then **Reload config**.

| Setting | Default | Notes |
|---|---|---|
| `ProcessName` | `Condor` | Process to watch, without `.exe` |
| `OpenChatKey` | `Back` | Key that opens Condor's chat prompt |
| `SendChatKey` | `Return` | Key that submits the message |
| `PushToTalk` | Caps Lock | Key or joystick button held to record. Set it from the tray, not by hand |
| `ConfirmKey` / `CancelKey` | `Return` / `Escape` | Only active while a review is open |
| `ReviewBeforeSending` | `true` | Set `false` for hands-free instant send |
| `KeyDelayMs` | `30` | Gap between injected keystrokes |
| `ChatOpenDelayMs` | `200` | Wait for the chat prompt to appear |
| `MinSecondsBetweenMessages` | `3` | Rate limit |
| `MaxRecordingSeconds` | `15` | Recording is abandoned past this |
| `SilenceThresholdRms` | `0.005` | Below this, audio is never transcribed |
| `WhisperModelFile` | `ggml-base.en.bin` | Model in `%APPDATA%\Gaggle` |
| `Language` | `en` | Or `auto` |
| `MaxMessageLength` | `120` | Longest message typed into chat |

## Design notes

Four things that are easy to get wrong, and why the code looks the way it does:

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

## Status

**Verified working end to end against a live Condor install** — push-to-talk capture,
local transcription, the review overlay, and scan-code injection into Condor's chat.

Speech-path timings on the reference machine: `ggml-base.en.bin` loads in ~330 ms and
transcribes a 6.7 s clip in ~1.1 s, with the sanitiser rejecting silence,
hallucinations and annotations.

Known unknowns, none of them blocking:

- `MaxMessageLength` (120) is a conservative guess, not Condor's measured limit
- Timings are tuned on one machine; a slower system may need a larger `ChatOpenDelayMs`
- Tested with Condor 2 and an English model only

**Elevation is not needed.** Condor and Gaggle both run as a normal user, which is how
this was tested. Only if you deliberately run Condor as administrator must Gaggle be
elevated too — UIPI blocks input injection from a lower integrity level. Gaggle detects
that case: `SendInput` accepting zero events on the first keystroke is reported as
"Windows blocked the keystrokes" rather than failing silently.

## Code style

`.editorconfig` holds the conventions, and the project builds with
`TreatWarningsAsErrors` plus `latest-recommended` analysers, so style and correctness
rules are enforced at build time rather than in review.

A handful of analyser rules are turned off in `.editorconfig`, each with a comment
saying why — mostly rules that fight P/Invoke code, where matching the Win32
signature exactly matters more than matching .NET naming conventions.

## Etiquette

This types into a live race chat shared with other people. The rate limit is on by
default and the review step exists because transcription is never perfect. Please
leave both on until you trust it.

## License

MIT — see [LICENSE](LICENSE).
