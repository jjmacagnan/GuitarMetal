# Guitar Metal — Godot 4 + C#

[![🇧🇷 Português](https://img.shields.io/badge/lang-Português-green?style=flat-square)](README.md)
[![🇺🇸 English](https://img.shields.io/badge/lang-English-blue?style=flat-square)](README.en.md)

A rhythm game in the style of Guitar Hero, built from scratch with Godot 4.6 and C#. Supports Clone Hero (`.chart`) and Rock Band (`.mid`) chart formats, hold notes, difficulty selection, simultaneous gamepad and keyboard input, local leaderboard, and PT/EN internationalization.

> Project developed for the **Mobile Game Development** course, part of the **Mobile Devices Programming Specialization** offered by **UTFPR — Federal University of Technology of Paraná, Brazil**.

---

## Requirements

- Godot 4.6 (with C# / .NET support)
- .NET 8 SDK

> Audio files (`.ogg`, `.mp3`) and charts (`.chart` / `.mid`) are **not included** in the repository. Add them to the `Audio/` folder locally.

---

## Project Structure

```
res://
├── Scripts/
│   ├── GameManager.cs       ← Main controller (spawn, score, HUD, pause)
│   ├── Lane.cs              ← Lane logic (input, visuals, hold tracking)
│   ├── Note.cs              ← Note physics and visuals (tap and hold)
│   ├── SongChart.cs         ← Data structure + procedural generation
│   ├── ChartImporter.cs     ← .chart file parser (Clone Hero)
│   ├── MidiImporter.cs      ← .mid file parser (Rock Band)
│   ├── SongIniReader.cs     ← song.ini reader (name, artist, delay)
│   ├── GameData.cs          ← Static data shared between scenes
│   ├── LoadingScreen.cs     ← Loading state machine
│   ├── SongSelectMenu.cs    ← Song selection (Audio/ folder scan)
│   ├── DifficultySelect.cs  ← Difficulty selection
│   ├── MainMenu.cs          ← Main menu
│   ├── NameInput.cs         ← Virtual keyboard (gamepad-friendly)
│   ├── ResultsScreen.cs     ← Results screen
│   ├── Leaderboard.cs       ← Top 10 scores per song
│   ├── ScoreStorage.cs      ← Score persistence (JSON)
│   ├── KeybindingStorage.cs ← Custom keybinding persistence and application
│   ├── Locale.cs            ← PT/EN internationalization
│   ├── SettingsMenu.cs      ← Settings screen (key remapping)
│   └── Credits.cs           ← Credits and license screen
├── Scenes/
│   ├── MainMenu.tscn
│   ├── NameInput.tscn
│   ├── SongSelect.tscn
│   ├── DifficultySelect.tscn
│   ├── Loading.tscn
│   ├── Game.tscn
│   ├── Results.tscn
│   ├── Leaderboard.tscn
│   ├── Settings.tscn
│   └── Credits.tscn
├── Audio/               ← Place your .ogg/.mp3 and .chart/.mid files here (git-ignored)
├── LICENSE
└── project.godot
```

---

## Game Flow

```
MainMenu → NameInput → SongSelect → [DifficultySelect] → Loading → Game → Results
    ↕           ↕                                                           ↕
 Settings   Leaderboard                                                 MainMenu
                ↕
            Credits
```

---

## Controls

> The controls below are the **defaults**. All lane keys and buttons can be remapped in the **Settings** screen (Main Menu → Settings).

### Keyboard (default)

| Key   | Lane | Color  |
|-------|------|--------|
| A     | 0    | Green  |
| S     | 1    | Red    |
| J     | 2    | Yellow |
| K     | 3    | Blue   |
| L     | 4    | Orange |
| ESC   | —    | Pause  |

### Gamepad (default — Switch Pro / Xbox)

| Button      | Lane | Color  |
|-------------|------|--------|
| ZL / LT     | 0    | Green  |
| L / LB      | 1    | Red    |
| R / RB      | 2    | Yellow |
| ZR / RT     | 3    | Blue   |
| X (top)     | 4    | Orange |
| Start / +   | —    | Pause  |

Keyboard and gamepad work simultaneously. Menu navigation via D-pad + A (confirm) / B (back).

---

## Settings

Go to **Main Menu → Settings** to remap the keys for each lane.

- **Keyboard tab** — click a lane and press the desired key. `ESC` cancels.
- **Gamepad tab** — click a lane and press the desired button or trigger.
- **Reset to Defaults** — restores the original mapping.
- Settings are saved automatically when leaving the screen (`user://keybindings.cfg`).
- The control hints on the Main Menu and in-game HUD always reflect the active bindings.

---

## Scoring

| Timing  | Window   | Time   | Base points     |
|---------|----------|--------|-----------------|
| PERFECT | < 0.90u  | < 25ms | 100             |
| GREAT   | < 2.16u  | < 60ms | 75              |
| GOOD    | < 3.24u  | < 90ms | 50              |
| HOLD    | full hold | —     | 150             |
| MISS    | —        | —      | 0 + reset combo |

**Multipliers:**

| Combo | Multiplier |
|-------|------------|
| < 10  | 1x         |
| >= 10 | 2x         |
| >= 20 | 4x         |
| >= 30 | 8x         |

**Grades:** S >= 95% · A >= 85% · B >= 70% · C >= 55% · D < 55%

Timing windows are adjusted per difficulty: Easy (1.5×), Medium (1.2×), Hard (1.0×), Expert (0.85×).

---

## Synchronization

Notes are positioned directly by the audio clock (`GameData.SongTime`), not by frame-delta accumulation. This ensures perfect synchronization regardless of frame rate variations.

The `AudioLatencyOffset` field (Export on GameManager) allows manual latency compensation if needed.

---

## Adding Songs

### Clone Hero / Enchor format (recommended)

Create a subfolder inside `Audio/` with the following structure:

```
Audio/
└── Metallica - Master of Puppets/
    ├── notes.chart    ← note chart
    ├── song.ini       ← metadata (name, artist, delay)
    └── song.ogg       ← song audio
```

The game reads `song.ini` to display "Artist - Title" in the song selection list.

> 🎸 Find thousands of free charts at **[Enchor](https://www.enchor.us)** — the largest repository of Clone Hero-compatible songs.

### Rock Band format (MIDI)

Place the `.mid` file alongside the audio:

```
Audio/
└── SongName/
    ├── notes.mid      ← Rock Band format chart
    └── song.ogg
```

Supported MIDI difficulties (MIDI notes): Expert (96–100), Hard (84–88), Medium (72–76), Easy (60–64).

### Loose format (single file)

Place the audio and `.chart` in the `Audio/` folder with the same base name:

```
Audio/
├── MySong.ogg
└── MySong.chart
```

### Supported audio formats

| Format  | Supported |
|---------|-----------|
| `.ogg`  | ✅ Recommended |
| `.mp3`  | ✅ |
| `.wav`  | ✅ |
| `.opus` | ❌ Not supported by Godot 4 |

> **Tip:** Convert `.opus` to `.ogg` with `ffmpeg -i song.opus song.ogg`

### Fallback: procedural chart

If no `.chart` or `.mid` is found, the game auto-generates a chart based on the BPM and audio duration.

---

## Leaderboard

Scores are saved locally in `user://scores.json` (Godot's data folder on the operating system). The leaderboard shows the top 10 scores per song, with player name, score, grade, accuracy, and max combo. Scores for individual songs can be cleared.

---

## Languages

The game supports **Portuguese (BR)** and **English**. The language can be changed via the language button on the main menu. The preference is applied in real time, with no restart required.

---

## License

Distributed under the MIT License. See the [LICENSE](LICENSE) file for details.
