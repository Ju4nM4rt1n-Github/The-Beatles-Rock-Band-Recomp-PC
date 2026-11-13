<img width="1100" height="500" alt="The Beatles: Rock Band PC recompilation banner" src="https://github.com/user-attachments/assets/38116ea6-d1b3-4f14-9774-4cec3e1f893a" />

# The Beatles: Rock Band — Native PC Port

<p align="center">
  An unofficial native Windows PC port of <strong>The Beatles: Rock Band</strong>, based on the original Xbox 360 version and the ReXGlue recompilation project.
</p>

<p align="center">
  <img src="https://img.shields.io/badge/Platform-Windows%20x64-blue?style=flat-square" />
  <img src="https://img.shields.io/badge/Graphics-Direct3D%2012-orange?style=flat-square" />
  <img src="https://img.shields.io/badge/Status-Active%20Development-success?style=flat-square" />
</p>

---

## About the Project

This project brings *The Beatles: Rock Band* to Windows PC through native recompilation, aiming to preserve the original gameplay experience while introducing PC-specific improvements.

It builds upon the original work by **Mercurialw0rld**, with additional development, compatibility improvements and features.

## Features

| Feature | Status |
|---|---|
| Guitar gameplay | Working |
| Bass gameplay | Working |
| Drum gameplay | Working |
| Microphone and vocal pitch detection | Working (Read known issues)| 
| Story Mode | Working |
| Quickplay | Working |
| Multiplayer | Working |
| Dolby Digital | Working (Read known issues) |

## Screenshots

<img width="1919" height="1078" alt="image" src="https://github.com/user-attachments/assets/1952ba01-1133-4e33-98f3-cea27f4a3671" />

<img width="1919" height="1079" alt="image" src="https://github.com/user-attachments/assets/4316e1d4-5f83-4f1c-aaf8-801a0ef2c170" />

<!-- Add actual in-game screenshots here once uploaded to the repository. -->

## System Requirements

| Component | Minimum | Recommended |
|---|---|---|
| Operating System | Windows 10/11 64-bit | Windows 11 64-bit |
| Processor | Intel Core i5 8th Gen / Ryzen 5 2600 | Intel Core i5 10th Gen / Ryzen 5 3600 |
| Memory | 8 GB RAM | 16 GB RAM |
| Graphics | GTX 1050 Ti / RX 570 | GTX 1660 / RTX 2060 / RX 6600 |
| Storage | Sufficient free disk space | SSD recommended |

Direct3D 12 compatibility is required. Performance may vary depending on hardware, resolution and the selected song.

To play with Dolby Digital, you must have a compatible audio system connected via HDMI.

## Installation

A legally obtained copy of the original game data is required.

No original game files, copyrighted music or proprietary assets are included in this repository.

1. Obtain the project build through the releases section.
2. Extract the application files.
3. Provide your own legally obtained game data.
4. Launch the application.
5. Configure graphics, audio and input settings.

For installation assistance, see the [installation video](https://www.youtube.com/watch?v=4QnA58zQAQA).

## Known Issues

<details>
<summary><strong>First-start crashes</strong></summary>

Random crashes may intermittently occur on the main menu (likely related to runtime shader compilation); waiting a few seconds or restarting the client typically resolves this.
We will continue running stability tests to mitigate and fix this behavior in future updates.
</details>

<details>
<summary><strong>Frame rate and gameplay speed</strong></summary>

The game currently has a timing issue above 60 FPS. Running at higher frame rates can cause gameplay to run at double speed and break certain animations.

**Workaround:** Limit the frame rate to 60 FPS or lower.
</details>

<details>
<summary><strong>Microphone latency</strong></summary>

Vocal input latency may be noticeable depending on the microphone and audio device being used. Thats because, translating low-level Xbox 360 audio/USB calls through Windows mixer buffers introduces unavoidable I/O latency. Since this relies on statically recompiled binaries, a root-level fix requires deep reverse engineering of the real-time DSP pipeline, **though we will work on minimizing this delay in future updates.**

**Workaround:** For the moment, i recommend playing Vocals on Easy with a low gain (you can change it in the options) so it's playable and not frustrating—at least for now.
</details>

<details>
<summary><strong>Dolby Digital</strong></summary>
The game automatically crashes when Dolby Digital is enabled if an active HDMI output supporting Dolby Audio / Atmos / Digital is not detected on the system.
  
**Workaround:** To fix this, set the audio output to PCM / Stereo by disabling the Dolby Digital option in the launcher before starting the game.
</details>

## Building from Source

Developers interested in compiling the project should start with:

- [BUILDING.md](BUILDING.md) — Build instructions.
- [VALIDATION.md](VALIDATION.md) — Validation and testing procedures.

Clone the repository:

```bash
git clone https://github.com/Ju4nM4rt1n-Github/The-Beatles-Rock-Band-Recomp-PC.git
cd The-Beatles-Rock-Band-Recomp-PC
```

Follow the instructions in `BUILDING.md` for the required dependencies and compilation process.

## Credits

This project is based on the original recompilation work by **[Mercurialw0rld](https://github.com/Mercurialw0rld)**.

Additional development and improvements are maintained by **Juanmaisbored**.

Special thanks to the original developers and contributors whose work made this project possible.

## Legal Disclaimer

This is an independent community project. It is not affiliated with, endorsed by or sponsored by Harmonix, MTV Games, Electronic Arts, Apple Corps Ltd. or The Beatles.

All trademarks and copyrighted materials belong to their respective owners.

Users must provide their own legally obtained copy of the original game.

## Contributing

Contributions, bug reports and suggestions are welcome.

Please open an [Issue](../../issues) before submitting major changes.

## License

See [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) and the `licenses/` directory for third-party licensing information.
