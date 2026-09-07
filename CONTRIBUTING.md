# Contributing to Gaggle

Gaggle types into chat shared with other pilots during a live race. That single fact
shapes most of what follows: a change that is merely untidy costs nothing, and a change
that broadcasts the wrong thing at the wrong moment costs somebody their race.

## The most useful thing you can do is report

You do not need to write code to help. **Report a bug…** and **Suggest an idea…** in the
tray menu open a GitHub form already filled in with your version, speech model, language,
microphone and Windows build. [What is worth reporting](README.md#what-is-worth-reporting)
lists the behaviour that is currently guessed rather than measured — that is where a
report is worth the most.

## Before you write code

**Open an issue first** for anything beyond a typo. Some of what looks like missing
polish is deliberate, and the reasoning lives in [CLAUDE.md](CLAUDE.md) rather than in the
code. Better to find that out before you have written the patch than after.

## The process

You will not be able to push a branch to this repository — fork it. Then:

1. Branch from `main`.
2. Make the change, with tests where the logic can be tested without hardware.
3. Open a pull request against `main`.
4. CI must pass, and the maintainer reviews and merges. There is one maintainer, so
   expect this to take days rather than hours.

## Building and testing

```bash
dotnet build -c Release
dotnet test -c Release
```

The project builds with `TreatWarningsAsErrors`, so **a new warning is a broken build**.
Fix the cause rather than the symptom; if a rule genuinely does not apply, suppress it in
`.editorconfig` with a comment saying why.

If `dotnet test` reports zero tests on your machine, run the test executable directly —
`tests/Gaggle.Tests/bin/Release/net10.0-windows/Gaggle.Tests.exe`. CI runs `dotnet test`
and finds them, so a local zero is an SDK quirk rather than a missing suite.

Tests cover the logic that works without Condor, a microphone or a joystick: the
sanitiser, bindings and their migration, config, downloads, the RMS gate, the update
checker and the issue links. Anything under `Interop/`, and the update swap itself, is
verified by hand against a real install — say so in the PR if your change touches those,
and what you did to check it.

## What will get a pull request sent back

[CLAUDE.md](CLAUDE.md) documents a set of invariants that look like arbitrary complexity
and are not. Each one, if broken, produces a build that compiles, runs, and silently does
nothing useful. The short version:

- **Keystrokes go in as scan codes.** Condor reads the keyboard through DirectInput.
  Virtual-key `SendInput`, `SendKeys` and `PostMessage` produce nothing in game — no
  error, no effect.
- **Keyboard hook handlers return immediately.** Over the Windows hook timeout and the
  hook is silently uninstalled, taking push-to-talk with it. Defer everything.
- **The review overlay never takes focus.** If it did, Condor would lose the foreground
  and the keystrokes would land in the overlay.
- **Audio is RMS-gated before transcription.** Fed near-silence, Whisper invents stock
  phrases from its training data, and those would go out to a live race.
- **A non-English language needs a multilingual model.** Ask an `.en` build for Polish and
  whisper.cpp discards the request without a word and transcribes phonetically.

Read the file before changing anything in `Interop/`, `Speech/` or `Update/`.

Some things are deliberately absent, and a PR adding them will be declined:

- Removing the rate limit (`MinSecondsBetweenMessages`) or the review step
  (`ReviewBeforeSending`), or changing either default. Both exist because of the shared
  chat. Review can be turned off per user — that is hands-free, and it is for VR, where
  the overlay cannot be seen. Making it the default for everyone is a different thing.
- Code that claims to suppress a bound joystick button. Condor reads the device directly;
  it cannot be done, and pretending otherwise is worse than the warning in the settings
  window.

## Style

Match the surrounding code. Two habits are worth naming:

- **Comments say why, not what.** The repository is full of explanations of non-obvious
  behaviour, and that is the point — most of it was expensive to learn.
- **P/Invoke mirrors Win32 exactly**, underscores and Hungarian names included. Matching
  MSDN is worth more than matching .NET naming, and the analyser rules are disabled to
  allow it.

## Releases

The maintainer tags and publishes releases. Please do not bump `<Version>` in a pull
request — the release workflow checks the tag against it, and a bump that arrives early
just has to be undone.
