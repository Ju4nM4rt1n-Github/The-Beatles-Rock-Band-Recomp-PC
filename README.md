<img width="1100" height="500" alt="image" src="https://github.com/user-attachments/assets/38116ea6-d1b3-4f14-9774-4cec3e1f893a" />

# The Beatles: Rock Band — Decompilation

A complete, fully functional decompilation of **The Beatles: Rock Band**, reconstructed from the original game binary into readable, buildable source code.

The result is a working build with full feature parity: every instrument, every vocal input, and every game mode behaves exactly like the original release.

## Credits

This version is based on the original project by **Mercurialw0rld**: [thebeatlesrockband-pcport](https://github.com/mercurialw0rld/thebeatlesrockband-pcport). Thank you, Mercurialw0rld, for this decomp!

This version was made with *VibeCoding*. I'm not an expert, just a videogame fan, and this is my little contribution to all of us gamers.

## Features

- **Full instrument support**: guitar, bass, and drums work flawlessly with accurate note detection, timing, and scoring.
- **Microphone support**: vocal input is fully functional, including pitch detection and harmony scoring for the three-part vocal sections.
- **Complete gameplay**: Story Mode, Quickplay, and multiplayer all run correctly with no missing features or known crashes.
- **Accurate behavior**: game logic, menus, and audio sync match the original behavior.
- **Clean, readable code**: organized, documented source that is easy to browse, build, and extend.

## System Requirements

| Component | Minimum | Recommended |
|-----------|---------|-------------|
| **OS** | 64-bit Windows 10/11 | 64-bit Windows 11 |
| **Processor** | Intel Core i5 8th gen or AMD Ryzen 5 2600 | Intel Core i5 10th gen or AMD Ryzen 5 3600, or better |
| **RAM** | 8 GB | 16 GB |
| **Graphics** | NVIDIA GeForce GTX 1050 Ti / AMD Radeon RX 570 (Direct3D 12 compatible) | NVIDIA GeForce GTX 1660 / RTX 2060 or AMD Radeon RX 6600, or better |
| **Storage** | Disk with enough free space | SSD recommended |

> The port was tested on an RTX 3050 Laptop. The graphics cards listed are approximate examples; performance also depends on the processor, resolution, and song.

## Dolby Digital (Experimental)

To use it, you need:

- An HDMI audio output whose driver and device support Dolby Digital/AC-3 at 48 kHz.
- A TV or receiver that can receive AC-3 over HDMI and pass it on to the sound system. If the connection uses ARC, the TV must allow Dolby Digital passthrough.
- **Dolby Digital (Experimental)** enabled in the launcher.

## Status

| Component | Status |
|-----------|--------|
| Guitar / Bass | ✅ Working |
| Drums | ✅ Working |
| Vocals / Mic | ✅ Working |
| Audio engine | ✅ Working |
| Menus & UI | ✅ Working |
| Full playthrough | ✅ Working |

## Building

1. Clone the repository:
   ```bash
   git clone https://github.com/Ju4nM4rt1n-Github/The-Beatles-Rock-Band-Recomp-PC.git
   cd The-Beatles-Rock-Band-Recomp-PC
   ```
2. Provide your own legally obtained copy of the game's data files (see `docs/SETUP.md`).
3. Follow the build instructions for your platform in `docs/BUILDING.md`.

Need help installing it? Watch the [installation video](https://youtu.be/4QnA58zQAQA).

## Legal Notice

This project contains **no copyrighted assets**. No game data, audio, charts, or artwork are included or distributed. You must supply your own legally obtained copy of the game to use this project.

This project is not affiliated with, endorsed by, or sponsored by Harmonix, MTV Games, Apple Corps Ltd., or any related rights holders. All trademarks belong to their respective owners.

## Contributing

Contributions are welcome. Please open an issue to discuss major changes before submitting a pull request. If you have a suggestion, you can contact me at jmvihd2@gmail.com.

## License

Game assets remain the property of their respective owners. Please use an original copy of the game to play.
