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

Single-file self-contained executable:

```bash
dotnet publish src/Gaggle/Gaggle.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish
```

## First run

Gaggle lives in the notification area. On first launch:

1. Right-click the tray icon → **Speech model** → pick one. It downloads to
   `%APPDATA%\Gaggle`.
2. Right-click → **Microphone** → pick your headset.
3. Start Condor. The tray icon turns blue when it's ready.
4. Hold **Caps Lock** (the default PTT key), speak, release.

## Configuration

`%APPDATA%\Gaggle\config.json`, created on first run. Edit it from the tray menu
(**Open config file**), then **Reload config**.

| Setting | Default | Notes |
|---|---|---|
| `ProcessName` | `Condor` | Process to watch, without `.exe` |
| `OpenChatKey` | `Back` | Key that opens Condor's chat prompt |
| `SendChatKey` | `Enter` | Key that submits the message |
| `TalkKey` | `CapsLock` | Hold to record |
| `ConfirmKey` / `CancelKey` | `Enter` / `Escape` | Only active while a review is open |
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

## Not yet verified

Written against documented behaviour but not yet confirmed against a live install:

- Condor's actual chat message length limit (`MaxMessageLength` is a guess)
- Whether `ChatOpenDelayMs` is enough for the chat prompt on all systems
- Whether Condor 2 uses `Condor.exe` as the process name in all installs

If Condor is running elevated, Gaggle must be too — UIPI silently blocks input
injection from a lower integrity level.

## Etiquette

This types into a live race chat shared with other people. The rate limit is on by
default and the review step exists because transcription is never perfect. Please
leave both on until you trust it.

## License

MIT — see [LICENSE](LICENSE).
