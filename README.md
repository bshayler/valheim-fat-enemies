# Fat Enemies (Valheim, BepInEx)

Makes every non-player creature (Greylings, Trolls, Boars, bosses...) much wider **and slower**.

## Install
1. Install BepInExPack_Valheim.
2. Build: `dotnet build -c Release -p:ValheimPath="<your Valheim folder>"`
3. Copy `bin/Release/net462/FatEnemies.dll` to `Valheim/BepInEx/plugins/`.

## Config (`BepInEx/config/com.example.fatenemies.cfg`, created on first run)
- `Width` (default 2.5) – how fat. 1 = vanilla.
- `Height` (default 1.0)
- `SpeedMultiplier` (default 0.6) – movement speed (walk/run/swim/fly). 1 = vanilla.
- `AffectTamed`, `AffectBosses`, `Enabled`

Note: scaling the root also scales hitboxes, so fat enemies are easier to hit
(and may get stuck in doorways). Visual/hitbox scaling is client-side; in
multiplayer everyone wanting to see it needs the mod.
