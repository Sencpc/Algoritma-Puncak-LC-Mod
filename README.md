# Algoritma Puncak – Lethal Company AI Overhaul

> Experimental BepInEx mod that rebuilds Lethal Company’s enemy brains, encounter flow, and moon-specific pacing.

[![Made for Lethal Company](https://img.shields.io/badge/game-Lethal%20Company-ff4757.svg)](#) [![BepInEx 5](https://img.shields.io/badge/BepInEx-5.4%2B-blue.svg)](#)

## Contents

- [What’s in the Mod](#whats-in-the-mod)
- [Installing](#installing)
- [Building from Source](#building-from-source)
- [Roadmap](#roadmap)
- [Contributing](#contributing)

## What’s in the Mod

| Area                              | Highlights                                                                                                                            |
| --------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------- |
| **Behavior Tree Framework**       | Shared BTContext, blackboard system, NavMesh helpers, noise sensors, and logging to drive per-enemy playbooks.                        |
| **Baboon Hawk Overhaul**          | Pack leadership, nest defense, scavenging loops, synchronized scream/alert broadcasts, and flocking separation/alignment.             |
| **Sand Worm Heatmap AI**          | Footstep heat field, hotspot stalking vs. strike windows, camera-shake eruptions, and kill-radius validation.                         |
| **Eyeless Dog (MouthDog) Rework** | High/low priority sound memories, NavMesh interrupt charges, cooldown windows, and roaming when quiet.                                |
| **Hoarder Nest Memory**           | Nest state persists after theft; migration and combat branches key off stolen loot events.                                            |
| **Assurance 220 Director**        | Moon-specific spawn cap clamping (5–7 indoor / 3–5 outdoor), weighted encounter table, and verbose logs via `AssuranceSpawnDirector`. |
| **Sensor Suite Extensions**       | Player snapshots now capture sprint, drop, and voice cues; heatmap + blackboard hooks expose those signals to AI modules.             |

> Looking for encounter plans? See [`docs/Assurance220Plan.md`](docs/Assurance220Plan.md) for the curated spawn/heatmap blueprint that inspired the Assurance director.

## Installing

1. **Requirements**
   - Legit copy of _Lethal Company_ (latest patch).
   - BepInEx 5.4+ already deployed to the game directory.
2. **Download** the latest `Algoritma-Puncak.dll` from Releases (or build it yourself).
3. Drop the DLL into `Lethal Company/BepInEx/plugins/AlgoritmaPuncak/`.
4. Launch the game once to generate config; check the console/log for `[AlgoritmaPuncak]` messages confirming the Assurance director + AI controllers loaded.

## Building from Source

```bash
git clone https://github.com/Sencpc/Algoritma-Puncak-LC-Mod.git
cd Algoritma-Puncak-LC-Mod/Algoritma-Puncak
dotnet build Algoritma-Puncak.csproj
```

Copy the resulting `Algoritma-Puncak.dll` from `bin/Debug` (or `bin/Release`) to your BepInEx `plugins` folder.

## Roadmap

- Extend voice-triggered stimuli so Eyeless Dogs (and allies) react to live mic input, not only remote AudioSources.
- Add telemetry/visualizers for the heatmap + blackboard state to speed up encounter tuning.
- More moon directors (e.g., Vow, Rend) with bespoke spawn tables and hazard mixes.
- Extra AI overhauls: Thumper predictive pathing, Flowerman stare duels, Spider territorial traps with destructible webs.

See open issues for detailed task tracking and discussion.

## Contributing

PRs, fork rewrites, or design notes are welcome. Please:

1. Target the latest `main` branch.
2. Include before/after testing notes (`dotnet build`, in-game reproduction steps, etc.).
3. Keep log messages prefixed with `[AlgoritmaPuncak]` to simplify debugging.

You can also reach out via Discussions if you have encounter ideas or want to sync moon directors.
