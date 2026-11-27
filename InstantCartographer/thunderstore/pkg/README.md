# Instant Cartographer

A BepInEx mod for Valheim. Open your map and every Cartography Table syncs both ways — no need to walk back and press E:
- **Read** (the table's "show others" side): data stored in tables is merged into your map.
- **Write** (the "place your markings" side): your exploration and pins are published to the tables.

## Install
Drop `InstantCartographer.dll` into `BepInEx/plugins/` (requires BepInExPack Valheim).

## Config (`BepInEx/config/com.bshayler.instantcartographer.cfg`)
- `Enabled` – on/off.
- `ReadTables` / `WriteTables` – turn either direction off.
- `MaxDistance` – only sync tables within N metres (0 = unlimited).
- `CooldownSeconds` – minimum time between syncs.
- `ShowMessage` – toast when data was merged.

## Notes
- Single-player and hosting: every table in the world is read, however far away.
- Joining a dedicated server: the client only knows about tables near you, so only those are read.
- Writing to tables far outside your loaded area only happens in single-player or when hosting.

## Build
```
dotnet build -p:ValheimPath="C:\Program Files (x86)\Steam\steamapps\common\Valheim" -c Release
```
