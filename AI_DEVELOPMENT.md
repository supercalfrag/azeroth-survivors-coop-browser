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
- `src/WarcraftScrollbars.cs`: native scrollbar repainting for the grid and dropdown lists; native input and accessibility behavior is retained.
- `src/JoinGame.cs`: exact-build, full live-function verification for native joining.
- `src/NativeJoinBridge.cs`, `src/JoinBridgeBytes.cs`: one-shot game-thread command submission and reversible detour.
- `src/JoinPasswordForm.cs`: explicit password prompt and game foreground handoff.
- `src/PartyPanelBridge.cs`, `src/PartyPanelBytes.cs`: one-shot panel-open flag and ECS change tick update.
- `tools/PartyPanel.asm`: readable source for the panel stub.
- `tools/JoinBridge.asm`: readable source for the generated x64 stub.
- `tests/SmokeTests.cs`, `tests/NativeJoinTests.cs`: offline protocol, eligibility, UI and native own-process fixtures.

## Evidence rules

Never call a build or fixture test a live gameplay test. Document which game executable was inspected. Re-check layouts before extending native support. Keep old-version protection when a newer layout is unknown. Service browsing, hero eligibility, Game view and native joining are independent capabilities.

Do not infer permission to join, host, message players, change saves, or add cheats from this repository's maintenance instructions. Test public-list reading only when needed; keep offline tests offline. Do not commit captured lobbies, hero names, saves, local paths, credentials, or game assets.

## Updating compatibility

The direct-service version field selects listings for a version; it is not a multiplayer compatibility bypass. The hero reader deliberately has no version/hash gate; it requires unique resource ownership and validated, stable data. Preserve those data checks. Native joining still requires its inspected executable hash and exact live-code verification because it writes a temporary bridge. Game view requires a unique native renderer match and validated layout. A future update can move code or fields; never relax validation solely to silence an error.

The capture hook must restore original instructions before its memory is released, with thread-position checks. Review close/error paths as carefully as successful installation. Do not run two capture readers against one game.

## Native joining

Native Join is command tag 24, with two owned Rust strings at +8 and +32 in a 232-byte command. The one-shot bridge runs before the normal UI command poll; it uses the game allocator and sender, and transfers string ownership to the game queue. Dry mode frees those strings instead of sending. The normal game handles admission. Do not use VirtualAlloc-backed data as owned Rust strings. Preserve all registers, flags, Windows x64 shadow space/alignment, and volatile XMM registers. Never free a cave while the bridge is processing a request, even if a thread is currently inside a called function outside the cave. Never retry an uncertain join automatically.

The panel bridge uses the inspected Co-op refresh system and its own temporary hook; it sets only visibility and the normal ECS change tick on the game thread. Do not treat a successfully submitted Join command as confirmed admission. If opening the panel fails after submission, report that separately and do not resubmit the Join command.
