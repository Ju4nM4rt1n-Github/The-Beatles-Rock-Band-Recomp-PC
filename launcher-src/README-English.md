# The Beatles: Rock Band for PC — version 1.2.2

This release includes the responsive launcher, controller profiles, verified TBRB DLC importing, and the tested shared-PCM audio path. The DLC importer accepts supported TBRB packages with generic title ID **FFFE07D1** after validating their song catalog and matching assets; it does not convert songs made for another game.

Keep **BeatlesRockBandLauncher.exe.config** beside the launcher when updating. Long-path support requires .NET Framework 4.6.2 or newer.

The launcher has a **Dolby Digital (Experimental)** option. When enabled, the game encodes 5.1 audio as AC-3 and sends it over exclusive HDMI; if the game's rear channels are nearly silent, it derives rear ambience from the front and center channels. It also uses the full audio queue to better tolerate scheduling jitter. When disabled, the game uses stereo shared PCM and retains the selected audio queue setting. Dolby Atmos is not supported. The Dolby option is off for new settings and can be changed at any time in the launcher.

## Performance mode

The **Performance mode** switch applies the scene-transition shader behavior on the next game start. It is on by default. A newly encountered visual effect may appear a few frames later while its pipeline compiles. This setting does not change audio speed, note timing, resolution or control profiles.

## Graphics options

The launcher opens on a **Home** screen with the cover art, the **Play** button and shortcuts to each section. A left rail switches between Home and **Settings**, where **Graphics**, **Audio**, **Controllers** and **Game files & saves** are separate pages (Esc returns Home). Graphics has two tabs, *Display & frame rate* and *Image quality*, and includes **Low**, **Medium** and **High** presets, display resolution, internal resolution scale (100%–300%), anisotropic texture filtering (off–16×), optional FXAA, and frame-rate modes for VSync or 30, 60, 75, 120 and 144 FPS. Fixed frame-rate modes set the game's guest video refresh rate and enable VSync; actual presentation remains subject to your display and system performance. Audio includes experimental Dolby Digital; microphone, game audio delay and buffer settings remain in **Edit controls**. Changes are saved to `launcher-settings.json` and applied on the next game start. The internal scale increases the game's render-target size and can use substantially more GPU power and video memory; increase it gradually if performance or stability suffers.

Replace the complete application package together so the executable and its runtime DLLs match. Keep **launcher-settings.json**, **userdata** and **cache**, including installed DLC and saved games. The distributable ZIP excludes player settings, saves, imported DLC, caches, logs and disc data.

1. Extract the complete package to a writable folder.
2. Open **BeatlesRockBandLauncher.exe**.
3. Choose **default.xex** from your legally obtained, extracted copy of the disc. Keep **charnames.zbm** and the complete **gen** folder beside it, with its header and ten ARK files.
4. Choose your display settings and click **Play**. Accept song-cache creation if offered.

No XEX or disc data is included. Supported XEX SHA-256: A47CBAFBC7FC81E4E6CC9BD3CE317DB5EAA9E57FE46AA5EBE09E153C37CA898B.

## Import DLC

Click **Import DLC** at the bottom of the launcher, then **Choose DLC files…**. Select one or more original Xbox 360 LIVE, PIRS or CON packages for The Beatles: Rock Band; many have long names with no extension. Extract ZIP/RAR archives first and select the package files inside them, not the archive or the XEX.

The importer lists installed packages, skips identical packages even if renamed, and leaves the original files intact. It prepares each package separately before installation; a failed or cancelled extraction does not replace an existing package. Restart the game after importing and allow its song cache to update if prompted. Installed DLC stays in **userdata** and is not included in the distributable ZIP.

Official data-only DLC for title ID **454108B1** is accepted, as are generic-title **FFFE07D1** custom packages that pass the TBRB song checks above. Other game title IDs remain rejected. Title updates and packages containing new executable modules require separate support. The supplied Abbey Road, Rubber Soul, All You Need Is Love and Sgt. Pepper packages passed extraction and installation tests. The tester confirmed All You Need Is Love appears and plays with audio and notes; this does not validate every DLC or custom track.

## Controls and devices

Open **Edit controls**, choose an **Instrument**, then a **Device**. Connect your Windows-compatible gamepad, guitar or drum controller first; **Refresh devices** scans again. The list also refreshes automatically. Devices requiring a receiver or driver must already be recognized by Windows. Direct MIDI input is not implemented.

Click an action, then press a key or controller button, hit a pad, use a D-pad direction, or move an axis. For analog whammy, release the bar before clicking **Whammy**, then move it through its range. **Escape** cancels controller capture. **×** clears an assignment. **Save** stores separate profiles for each instrument and device and preserves your existing guitar assignments. Changes apply the next time you start the game; restart to change instruments.

Controller presets are starting points: check and map the inputs for your particular controller. The selected device appears in-game as one guitar or one drum kit, according to your Instrument selection. This does not add simultaneous multiplayer instruments. If it is disconnected, reconnect it or select **Keyboard**. Up to 64 buttons, 16 axes and four D-pads per device are supported. Identical devices without a serial number or distinct device path may need their profiles reselected after reconnecting in a different order.

| Default keyboard input | Action |
| --- | --- |
| 1 / Space | Green / select |
| 2 / Backspace | Red / back |
| 3, 4, 5 | Yellow, blue, orange |
| Up / Down | Strum / navigate |
| Enter | Start / pause |
| Tab | Beatlemania / Star Power |
| W | Whammy, hold on sustained notes |
| S | Solo frets, hold in solo sections |
| Left / Right | Navigate menus |

Frets, strum and whammy work together. Keyboard whammy is digital; a mapped controller axis preserves its analog range. Beatlemania needs sufficient energy. Solo frets work in sections recognized by the game. Give the game window focus.

### Drums

| Default keyboard input | Action |
| --- | --- |
| D | Red pad / snare |
| F | Yellow pad |
| J | Blue pad |
| K | Green pad / select |
| Space | Kick pedal |
| 1 / 2 | Alternate select / back |
| Arrow keys | Navigate menus |
| Enter | Start / pause |
| Tab | Back button |

Hit pads directly; no strumming is needed. For Beatlemania, play the drum fill and hit the final green note. Pad and pedal inputs are digital; this version does not add velocity sensitivity or a separate pro-cymbal mode. Physical controllers and axis-based pedals must be mapped to the inputs reported by their Windows driver.

## Status

The launcher and editor are in English. Existing keyboard assignments, settings and saved games are preserved. Keyboard drum recognition, the drum chart, all four pads and the kick pedal were confirmed in-game by the tester. Software checks cover separate guitar/drum profiles, controller mapping and analog whammy. Layout previews cover 100%, 125%, 150% and 200% scaling and compact windows; this does not replace testing every monitor configuration. Physical-controller gameplay still needs hardware testing.

The 0.3.2 performance improvements remain active: cooperative GPU waits and offline network backoff outside its internal lock. Audio uses Windows XAudio2 with regularly spaced mixing callbacks. The tester confirmed improved smoothness with the 0.7.0 graphics configuration. Occasional frame-time spikes and possible manual audio/video calibration remain. This is an experimental port; microphone, online play, all songs and all instrument models are not validated.

Windows 10/11 x64, Direct3D 12 and .NET Framework 4.8 are required. Tested with i3-8100, 16 GB RAM and GTX 1050 Ti. Visual C++ runtime libraries are included. Saves, settings, shader cache and logs stay beside the launcher. ReXGlue and build tools are not needed to play.

The original submarine artwork, Aero palette and embedded DM Serif Display are retained. Font and SDL/ReXGlue licenses are included.

SDK: https://github.com/rexglue/rexglue-sdk (bd833a2). Reference: https://github.com/HossEz/fifa-world-cup-2014-recompiled.
