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

**The asset name carries no version, and that is load-bearing twice over.**
`Gaggle-win-x64.zip` is what makes
`releases/latest/download/Gaggle-win-x64.zip` resolve — GitHub matches that path against
the newest release by exact asset name, so a version in there would break the download
page's button and every link already shared. The updater matches the `-win-x64.zip`
suffix, so it survives a rename but not a change of suffix. And never publish a second
asset ending in that suffix: the updater takes the last match it sees, and which one that
is depends on the order the API happens to return them in.

The tag is also where the version comes from: `release.yml` passes `-p:Version=<tag>` to
the build and publish, and `<Version>` in the csproj is a `0.0.0` placeholder. So a
release needs no commit of its own — which is the point, because a version-bump commit on
`main` fired a second, identical CI run for every release. Nothing enforces the two
agreeing any more because they cannot disagree, but the injection *is* silent when it
fails: a mistyped property leaves the placeholder in place and ships a zip stamped 0.0.0.
The "Check the build was stamped with the tag version" step reads it back off the built
assembly for that reason. Do not drop it.

**Write the tag message with `--cleanup=verbatim`.** `git tag -a -F notes.md` applies the
same cleanup a commit message gets, which **strips every line starting with `#`** as a
comment. Markdown headings are exactly that, so `## What changed` disappears from the tag,
from the release notes built out of it, and from the published release — with nothing
anywhere to say a line was dropped. Verified: it ate a `###` heading out of the v0.7.0
notes, and the release looked perfectly fine without it.

```bash
git tag -a v0.7.0 --cleanup=verbatim -F notes.md
git tag -l v0.7.0 --format='%(contents:body)'   # read it back before pushing
```

**Fixing a draft means deleting the release, not just the tag.** Re-pushing a tag over an
existing draft does not rebuild it; `release.yml` only runs on a new tag. So a correction
is `gh release delete <tag> --cleanup-tag`, then tag and push again — which destroys the
draft someone may be reading, and takes it with them if they published it in the meantime.
Say so before you do it, or edit the draft's notes in place with `gh release edit` and
leave the tag alone.

`docs/index.html` is the download page, served by GitHub Pages from `/docs` on `main` at
<https://cieslakmp.github.io/gaggle/>. It is the link handed to people who do not live on
GitHub, and it needs nothing done to it at release time: the button points at
`releases/latest/download/Gaggle-win-x64.zip`, which GitHub resolves against the newest
release, and the version line is filled in from the releases API with the static link as
the fallback. Editing it is a normal commit; there is no build step and no framework.

It carries English and Polish the same way the app does — both in the file, next to each
other, so they cannot drift. Mark each with `data-en` / `data-pl`; the CSS only ever
*removes* display, never sets it, so a `<p>` stays a block and a `<span>` stays inline
when it comes back. **English is what shows with no script**, because the rule keying on
`:root:not([data-lang="pl"])` matches until the switcher sets the attribute — so a blocked
script costs the Polish text, never the page. Detection walks `navigator.languages` in
order rather than asking whether Polish appears anywhere in it: `["en-GB", "pl"]` is an
English speaker who also reads Polish, and that is a normal way for a Polish pilot to set
up Windows. `?lang=pl` overrides everything, so a Polish link can be shared directly.

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

**Translations must be compiled into the one assembly.** `release.yml` packages
`publish/Gaggle.exe` and `publish/runtimes` and nothing else, and `UpdateInstaller`
validates only those two paths. A satellite `pl\Gaggle.resources.dll` would therefore be
dropped from every release zip and every in-app update — and a missing satellite does not
throw, it falls back to the neutral language. That is why `src/Gaggle/Localisation` is a
C# table of literal pairs rather than a set of `.resx` files, and why nothing here may
grow a satellite assembly without the packaging step and the updater's asset check
changing with it.

**Anything downloaded is verified before it is used, and bounded while it arrives.** A
ggml model is parsed by whisper.cpp in native code, so an unverified one is a
memory-safety surface rather than merely a wrong file — which is why every
`ModelInstaller.ModelChoice` carries the SHA256 and the exact byte count Hugging Face
publishes, and why `DownloadAsync` takes the `ModelChoice` rather than a file name: there
is no overload that can fetch a model without a digest to check it against. **A new model
added without its hash will not build, and one added with the wrong hash will download in
full and then always be rejected**, so take both from
`https://huggingface.co/api/models/ggerganov/whisper.cpp?blobs=true` rather than from a
local copy. The size is a hard cap, not a hint; it must match what the CDN serves or the
download aborts. The same reasoning bounds the update package and every text response:
the checksum cannot be checked until the whole file has landed, so without a cap a host
that never stops sending fills the disk first.

**A URL from anywhere but a constant goes through `Ui.Shell`.** `UseShellExecute` picks a
handler by scheme, so a string that is not the http(s) link it looks like is a program
launch. Only a release's `html_url` is remote today, but the check lives in one place
because the next one will not announce itself. Use `OpenPath` for the config file and the
data folder; `OpenUrl` refuses anything that is not http(s).

**An asset name from GitHub is not a file name.** `Path.Combine` returns a rooted string
unchanged and follows `..\` out of the folder you meant, so `ReleaseInfo.PackageName` goes
through `UpdateInstaller.PackageFileName` before it touches the disk. `HardeningTests`
pins that, including the Startup-folder case that turns a download into a program that
runs at next logon.

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
| `Localisation/` | Every user-facing string, English and Polish side by side |
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

Every user-facing string goes in `Localisation/Strings.*.cs`, in both languages, reached
as `Strings.Current.Whatever`. Formatted text is a *method* rather than a format string,
so a placeholder cannot exist in one language and not the other. `StringsTests` walks the
table by reflection, so a member written in English and left untranslated fails the build
rather than quietly showing English to a Polish pilot — which is the whole point, because
nothing at runtime would ever say so. A third language means one more argument to `Pick`
and one more `UiLanguage`.

Some text is English on purpose and must stay that way: `IssueLink.DescribeEnvironment`
(maintainers read those issues), `MessageSanitiser.Hallucinations` (a blocklist of
Whisper's English output — translating it disables the gate), `PttBinding.FriendlyNames`
(a Polish keyboard is still labelled "Caps Lock"), and the endonyms in
`SpokenLanguage.Available`.

`UiLanguage` and `Language` are unrelated settings that both read as "language". The tray
names them "App language" and "Speech language" for that reason; do not shorten either
back to "Language". Changing `UiLanguage` rebuilds the whole tray menu, which is why the
menu's items are created inside `BuildMenu` and why the rebuild is deferred through
`BeginInvokeOnUi` — a `ContextMenuStrip` disposes its own items, and disposing a drop-down
from inside its own `Click` handler pulls the control out from under the click.

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

- The watchdog **discards** what it stops. Holding past the limit is the cancel gesture —
  the only one hands-free has, because there is no overlay to read and no Escape to aim at
  it. It used to transcribe and send instead; do not put that back without also giving
  hands-free some other way to abandon a message.
- `SendOutcome.UnsupportedCharacters` means the message **was** sent — `ChatSender` taps
  the send key before it reports the characters it could not type — so it earns the sent
  cue, not the dropped one.
- Toggling hands-free must call `DiscardPending()`. A transcript already awaiting Enter
  has the hook swallowing Enter and Escape, and would otherwise fire into chat on the next
  Enter pressed in Condor.

The full flow is verified working against a live Condor install, so the approach is
sound. If a change breaks it, suspect `ChatOpenDelayMs`, the process name, or
elevation before suspecting the injection approach itself.
