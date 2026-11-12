<img width="1100" height="500" alt="The Beatles: Rock Band PC recompilation banner" src="https://github.com/user-attachments/assets/38116ea6-d1b3-4f14-9774-4cec3e1f893a" />
The Beatles: Rock Band — Decompilation

A decompilation of The Beatles: Rock Band, reconstructed from the original game binary into readable, buildable source code, running natively on Windows x64 (Direct3D 12).

Guitar, bass, drums and vocals work, and the game can be played from start to finish. A few features are still experimental; see Known Issues.

Credits

This version is based on the original project by Mercurialw0rld: thebeatlesrockband-pcport. Thank you, Mercurialw0rld, for this decomp!

This version was made with VibeCoding. I'm not an expert, just a videogame fan, and this is my little contribution to all of us gamers.

Features
Instrument support: guitar, bass, and drums with note detection, timing, and scoring.
Microphone support: vocal input with pitch detection and harmony scoring for the three-part vocal sections.
Gameplay: Story Mode, Quickplay, and multiplayer.
Graphics options (V1.2.2): resolutions up to 2560x1440, Low/Medium/High presets, internal scaler, anisotropic filtering, VSync, and anti-aliasing.
Dolby Digital 5.1 over HDMI (experimental), enabled from the launcher.
Readable source code: easy to browse, build, and extend.
System Requirements
Component	Minimum	Recommended
OS	64-bit Windows 10/11	64-bit Windows 11
Processor	Intel Core i5 8th gen or AMD Ryzen 5 2600	Intel Core i5 10th gen or AMD Ryzen 5 3600, or better
RAM	8 GB	16 GB
Graphics	NVIDIA GeForce GTX 1050 Ti / AMD Radeon RX 570 (Direct3D 12 compatible)	NVIDIA GeForce GTX 1660 / RTX 2060 or AMD Radeon RX 6600, or better
Storage	Disk with enough free space	SSD recommended

The port was tested on an RTX 3050 Laptop. The graphics cards listed are approximate examples; performance also depends on the processor, resolution, and song.

Installation (players)

You need your own legally obtained copy of the game's data. No game files are included in this repository or in the releases.

Download the latest portable .zip from the Releases page and extract it to a folder of your choice.
<!-- CONFIRM: describe exactly how the user provides the game data (disc/XEX extraction, which folder it goes in). -->Provide your own copy of the game's data files, placing them where the launcher expects them.
Run The Beatles Rock Band Launcher to configure graphics, audio and input.
Click Play (or run The Beatles Rock Band Play) to start the game.

Need help? Watch the installation video.

Dolby Digital (Experimental)

To use it, you need:

An HDMI audio output whose driver and device support Dolby Digital/AC-3 at 48 kHz.
A TV or receiver that can receive AC-3 over HDMI and pass it on to the sound system. If the connection uses ARC, the TV must allow Dolby Digital passthrough.
Dolby Digital (Experimental) enabled in the launcher.
Status
Component	Status
Guitar / Bass	✅ Working
Drums	✅ Working
Vocals / Mic	✅ Working (latency can be noticeable, see Known Issues)
Audio	✅ Working
Dolby Digital 5.1 (HDMI)	🧪 Experimental
Menus & UI	✅ Working
Full playthrough	✅ Working
Known Issues
Frame rate: the launcher accepts refresh rates up to 144 Hz, but above 75 FPS the game runs at double speed and some animations break. Cap it at 75 FPS or lower.
Microphone latency: vocal input can have noticeable latency on some microphones and audio devices. Improving this is planned.

Found something else? Please open an issue and include your Windows version, GPU, and the version of the port you are using.

Building
Clone the repository:
   git clone https://github.com/Ju4nM4rt1n-Github/The-Beatles-Rock-Band-Recomp-PC.git
   cd The-Beatles-Rock-Band-Recomp-PC
Provide your own legally obtained copy of the game's data files.
Follow the steps in BUILDING.md.
Legal Notice

This project contains no copyrighted assets. No game data, audio, charts, or artwork are included or distributed. You must supply your own legally obtained copy of the game to use this project.

This project is not affiliated with, endorsed by, or sponsored by Harmonix, MTV Games, Apple Corps Ltd., or any related rights holders. All trademarks belong to their respective owners.

Contributing

Contributions are welcome. Please open an issue to discuss major changes before submitting a pull request.

License

Game assets remain the property of their respective owners. Please use an original copy of the game to play.