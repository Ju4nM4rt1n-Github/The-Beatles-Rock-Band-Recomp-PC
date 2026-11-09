The Beatles: Rock Band — Decompilation

This is the original proyect that i used to make this version: https://github.com/mercurialw0rld/thebeatlesrockband-pcport

¡Thank you to Mercurialw0rld for this decomp!

A complete, fully functional decompilation of **The Beatles: Rock Band**, reconstructed from the original game binary into readable, buildable source code.

The result is a working build with full feature parity: every instrument, every vocal input, and every game mode behaves exactly like the original release.

## Features

- **Full instrument support** — guitar, bass, and drums work flawlessly with accurate note detection, timing, and scoring.
- **Microphone support** — vocal input is fully functional, including pitch detection and harmony scoring for the three-part vocal sections.
- **Complete gameplay** — Story Mode, Quickplay, and multiplayer all run correctly with no missing features or known crashes.
- **Accurate behavior** — game logic, menus, and audio sync match the original behavior.
- **Clean, readable code** — organized, documented source that is easy to browse, build, and extend.

## Status

| Component         | Status     |
|-------------------|------------|
| Guitar / Bass     | ✅ Working |
| Drums             | ✅ Working |
| Vocals / Mic      | ✅ Working |
| Audio engine      | ✅ Working |
| Menus & UI        | ✅ Working |
| Full playthrough  | ✅ Working |

## Building

1. Clone the repository:
   ```bash
   git clone https://github.com/<your-username>/<repo-name>.git
   cd <repo-name>
   ```
2. Provide your own legally obtained copy of the game's data files (see `docs/SETUP.md`).
3. Follow the build instructions for your platform in `docs/BUILDING.md`.

## Legal Notice

This project contains **no copyrighted assets**. No game data, audio, charts, or artwork are included or distributed. You must supply your own legally obtained copy of the game to use this project.

This project is not affiliated with, endorsed by, or sponsored by Harmonix, MTV Games, Apple Corps Ltd., or any related rights holders. All trademarks belong to their respective owners.

## Contributing

Contributions are welcome. Please open an issue to discuss major changes before submitting a pull request.

## License

Game assets remain the property of their respective owners. Use an original copy to play the game.
