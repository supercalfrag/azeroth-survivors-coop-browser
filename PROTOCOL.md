# Lobby protocol notes

Observed protocol, not an official API specification. It may change.

Endpoint: `wss://wow.aethus.pl/coop/lobby/`.
Messages are binary; integers below are little-endian.

Initialize with byte 1, u32 protocol version 3, a u32-length-prefixed UTF-8 game version, u32 zero, byte 1. Request the list with byte 2. A list response starts with byte 20 and u32 row count.

Each row: four length-prefixed strings (code, name, description, host), u8 host class, zone-key string, u8 players, u8 password flag, u8 open flag, country string, u32 roster count, that many u8 class IDs, host-title string. After the rows come one u8 capacity per row, then one u8 Heroic tier per row.

The decoder checks lengths, strict UTF-8, flags, classes, codes, capacity, truncation and trailing bytes. The client caps a received message at 2 MiB and times requests out after 10 seconds. Refresh defaults to five seconds; manual, one-second, three-second and ten-second intervals are also offered. A busy request prevents overlapping refreshes.

Class IDs: 1 Warrior, 2 Paladin, 3 Hunter, 4 Rogue, 5 Priest, 7 Shaman, 8 Mage, 9 Warlock, 11 Druid. Unknown data should fail visibly rather than be guessed. This direct-service client requests lists only; no direct-service join/host/chat protocol is implemented. Browser v8 joins by submitting the normal UI command inside the running game, rather than implementing a second multiplayer client.

Observed list-size behavior: two live responses contained 100 rooms each and 106 distinct room codes across the pair. The client decoder permits 256; no supported pagination or requested-limit field was identified in the existing one-byte list request. This is observed behavior, not an official service-limit specification.
