# Azeroth Survivors Co-op Browser

An unofficial Windows companion browser for Azeroth Survivors, with a dark-and-gold fantasy panel theme, a larger lobby list, filters, and class badges.

**Built with AI in mind:** this project was developed collaboratively with OpenAI Codex. AI assisted with implementation, protocol investigation, documentation, and testing. The source, build scripts, and maintenance notes are included so humans and AI coding assistants can inspect and improve it. AI assistance does not guarantee correctness; see the compatibility and testing limits below.

![Browser preview with synthetic example data](docs/preview.png)

## Features

- Browse public lobbies directly, without opening Co-op in the game.
- Filter by country, zone, difficulty, text, password, and available slots.
- Optional **Can Join** filter based on the hero selected in the running game.
- Class badges and roster details.
- Manual refresh or every 5, 10, 30, or 60 seconds.
- Copy a join code with the button or by double-clicking a room.
- Optional **Game view** source for the game's own lobby data.

Double-clicking **copies** the code; it does not automatically join. Paste the code into the game's **Join by code** flow. This tool does not enable cross-version multiplayer.

## Download and run

Download the executable from [Releases](https://github.com/supercalfrag/azeroth-survivors-coop-browser/releases). Windows x64 and .NET Framework 4.7.2 or later are recommended. No installer or administrator access is normally required; the executable is unsigned.

1. Open `AzerothCoopBrowser.exe`.
2. Leave **Source** set to **Direct service**.
3. Check the game version field matches the version you play. It detects a running game, or a game executable beside the browser; otherwise enter the version manually.
4. Refresh and set your filters.
5. Copy a room's code, then join inside the game.

Direct browsing works with the game closed. Hero eligibility requires the game running. **Can Join** hides rooms whose eligibility is unknown; uncheck it to see the returned list when hero reading is unavailable.

## Compatibility and limitations

This release is based on companion browser v7. The optional selected-hero reader was verified against **0.139.0**, SHA-256 `0E8E314DF4D78AB211B1BEF5C48D78D713ED64260894AF342F998D298D74D274`. It deliberately stops on other builds. Direct browsing can continue with the correct version field, provided the service protocol remains compatible. No claim of current or universal game-version support is made.

Normal-zone eligibility uses the selected class's unlocked zone count. Heroic eligibility requires that class's Normal clear plus the account's completed Heroic tiers, capped at V. Unsupported zones or data are marked unchecked.

The service has returned up to 100 rooms in testing; this is not a guarantee that all hosted games are listed. Listings may change before you join. A copied code does not reserve a slot. Initial hero scanning can take 15–30 seconds.

## Process access and privacy

- **Direct service** connects to `wss://wow.aethus.pl/coop/lobby/`, sending a protocol/version greeting and list requests. It does not host or join rooms, send chat, or upload hero progression.
- Optional selected-hero access queries and reads game memory only; it does not install a hook or modify saves.
- **Game view** is different: it installs a temporary, compatibility-checked renderer capture hook in game memory. The in-game lobby list must be open. Close the browser normally to restore the hook; avoid running multiple capture readers together.
- No trainer, save editor, personal saves, captured player lists, or credentials are included in this repository.

The game's service and protocol are operated by its developer and may change independently of this project. There is no official affiliation or endorsement.

## Build from source

On Windows with .NET Framework's C# compiler:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\test.ps1
```

The output is `dist\AzerothCoopBrowser.exe`. No NuGet dependencies or game files are required. Public builds generate original class-letter badges instead of redistributing extracted game textures.

## Validation

The original v7 was live-tested for direct listings with Co-op closed, hero selection changes, eligibility filtering, and refresh settings. That historical testing applies to the inspected build, not every newer update. Public-package checks cover compilation, protocol decoding with synthetic data, malformed responses, eligibility boundaries, and offline UI rendering. They do not join public rooms or prove compatibility with a newer game.

See [AI development notes](AI_DEVELOPMENT.md), [protocol notes](PROTOCOL.md), and [contributing](CONTRIBUTING.md).

## License and credits

Project-authored code is available under the [MIT License](LICENSE). Azeroth Survivors and Warcraft names and other third-party properties belong to their respective owners. This repository includes no game executables, archives, extracted textures, or saves. The theme is drawn in code and class badges are generated locally.
