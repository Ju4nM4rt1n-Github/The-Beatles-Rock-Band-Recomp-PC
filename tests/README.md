# Regression tests

`Test-Launcher.ps1` builds an isolated portable copy from the native build. Pass `-GameBuildDirectory`, `-VsInstallDirectory` or `-CrtDirectory` when needed. Always use a fresh output folder; the suite writes synthetic settings and userdata beside its executables and must never run in a player's installation.

The suite covers keyboard/controller mapping, guitar/drum profiles, multiple DPI scales, scroll access, DLC UI, opaque painting and Performance mode migration/persistence/arguments. Generate synthetic packages with `python Make-DlcFixtures.py <fixture-folder>` and pass `-FixtureDirectory <fixture-folder>` to test extraction. Fixtures contain text, not commercial songs. Keep test paths short if .NET Framework's existing path-length limit causes an error.

`Test-SceneSubmission.ps1` extracts actual SDK method bodies and compiles worker/event doubles. It requires C++ tools and Python, but no game data or graphics window. `-Original -SdkSourceDirectory <clean-base-checkout>` expects to reproduce the old blocking path.

Build with `Build-Game.ps1 -AudioTests` for `windows-audio-tests.exe`. It includes the production driver and checks real 5.1 AC-3 frame encoding, IEC 61937 burst wrapping and local payload recovery/decoding, exclusive-format endpoint support, player-name argument parsing, COM initialization, existing STA/MTA callers, cross-thread lifetime, independent cookies, injected engine failure and reinitialization. It also reports registered Windows AC-3 encoder MFTs for diagnosis. The default PCM mode opens the default XAudio2 device without submitting sound and requires a usable playback device. Run with the matching runtime and Visual C++ DLLs; the portable runtime folder supplies them. A missing DLL can prevent the test from starting before it produces a log.

`windows-audio-tests.exe --probe-ac3-output` opens the default multimedia endpoint in exclusive AC-3 mode and emits encoded silence for two seconds. This briefly takes over the endpoint and may change the receiver's Dolby indicator; close other audio first.

`windows-audio-tests.exe --probe-ac3-tone` sends a low-level 5.1 test tone to each channel in sequence for less than a second per channel, then waits for the HDMI/ARC buffer to drain. Set the receiver volume low before running; the command temporarily takes exclusive control of the default multimedia endpoint.

`windows-audio-tests.exe --write-ac3-test` writes a synthetic six-channel AC-3 tone file to the current directory. Playing it through a separate player configured for bitstream passthrough can distinguish endpoint transport issues from AC-3 content issues.

The diagnostic game runtime accepts `--audio_dolby_digital=true` to exercise the experimental timer-driven path. It requests a 200 ms WASAPI buffer and maintains a continuous IEC 61937 stream, preserving partial 1536-frame AC-3 bursts across endpoint-buffer writes. When the game's native rear channels are below 5% of front/center-channel RMS, the experimental Dolby path derives rear ambience from the front and center channels; meaningful native surround is left unchanged. This fallback does not affect the stable PCM path. The SDK keeps speaker configuration in process state and logs `XAudioGetSpeakerConfig`, `XAudioSetSpeakerConfig` and `XAudioOverrideSpeakerConfig` calls. Keep the launcher option disabled and use isolated user-data/cache directories; do not copy this runtime into the stable PCM package until gameplay is audible and stable.

`windows-audio-tests.exe --fail-driver` intentionally injects an engine failure. It must exit nonzero at the fatal audio factory path and log XAudio2Create error 80004005, rather than return an invalid guest handle. This is an intentional failure test, not a normal launch mode.

These checks do not replace actual gameplay, physical controllers, listening tests or multi-monitor validation.

0.7.3 fixtures include verified generic-title TBRB customs and negative checks for foreign titles, other origins, missing assets, invalid MIDI and comment/string spoofs. The runner copies the startup .NET configuration before running each test executable. DlcLongPathTests exercises import, indexing, duplicates and cleanup beyond MAX_PATH. Build-Game.ps1 -DlcTests includes the native 20-check catalog parser test.
