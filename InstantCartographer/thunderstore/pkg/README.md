# Instant Cartographer

A BepInEx mod for Valheim. Open your map and every Cartography Table is read for you —
no need to walk back to a table and press E. Exploration and pins stored in the tables are merged into your map.

## Install
Drop `InstantCartographer.dll` into `BepInEx/plugins/` (requires BepInExPack Valheim).

## Config (`BepInEx/config/com.bshayler.instantcartographer.cfg`)
- `Enabled` – on/off.
- `MaxDistance` – only sync tables within N metres (0 = unlimited).
- `CooldownSeconds` – minimum time between syncs.
- `ShowMessage` – toast when data was merged.

## Notes
- Single-player and hosting: every table in the world is read, however far away.
- Joining a dedicated server: the client only knows about tables near you, so only those are read.
- This is read-only. You still *write* your map to a table the normal way (interact with it).

## Build
```
dotnet build -p:ValheimPath="C:\Program Files (x86)\Steam\steamapps\common\Valheim" -c Release
```
