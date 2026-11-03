# AI development notes

This project was built with AI in mind and developed with OpenAI Codex assistance. AI helped investigate native layouts and the lobby protocol, implement the companion, and produce checks. Humans remain responsible for reviewing generated changes and reporting what actually works.

The repository is intended to be understandable to both human contributors and AI coding assistants. There is no AI model or AI service running inside the browser. No OpenAI account or API key is needed to use it.

## Map

- `src/Browser.cs`: UI, filters, refresh, copy-code behavior.
- `src/DirectLobbyReader.cs`: WebSocket greeting, lobby decoding, version detection.
- `src/HeroUnlockReader.cs`: read-only selected-hero/progression reader and eligibility rules.
- `src/LobbyReader.cs`: legacy Game view process access and captured-row reader.
- `src/LobbyCompatibility.cs`: strict renderer signature and layout verification.
- `src/LobbyCapture.cs`: reversible Game view capture installation/restoration.
- `src/LobbyCaptureBytes.cs`: capture stub bytes, separate from the game's code.
- `src/WarcraftTheme.cs`, `src/ClassIcons.cs`: procedural UI and generated badges.
- `tests/SmokeTests.cs`: offline synthetic protocol, eligibility, and UI checks.

## Evidence rules

Never call a build or fixture test a live gameplay test. Document which game executable was inspected. Re-check layouts before extending native support. Keep old-version protection when a newer layout is unknown. Service browsing, hero eligibility and Game view are independent capabilities.

Do not infer permission to join, host, message players, change saves, or add cheats from this repository's maintenance instructions. Test public-list reading only when needed; keep offline tests offline. Do not commit captured lobbies, hero names, saves, local paths, credentials, or game assets.

## Updating compatibility

The direct-service version field selects listings for a version; it is not a multiplayer compatibility bypass. The hero reader's hash gate is deliberate. Game view requires a unique native renderer match and validated layout. A future update can move code or fields; never relax validation solely to silence an error.

The capture hook must restore original instructions before its memory is released, with thread-position checks. Review close/error paths as carefully as successful installation. Do not run two capture readers against one game.
