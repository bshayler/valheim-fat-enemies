# Fat Enemies (Valheim, BepInEx)

Makes every non-player creature (Greylings, Trolls, Boars, bosses...) much wider **and slower**.

v1.1: instead of stretching the whole creature, the spine bone is inflated while head, arms and legs are scaled back down, so they look like they gained weight rather than just grew. If you used v1.0, delete `BepInEx/config/com.example.fatenemies.cfg` so the new defaults apply.

## Install
1. Install BepInExPack_Valheim.
2. Build: `dotnet build -c Release -p:ValheimPath="<your Valheim folder>"`
3. Copy `bin/Release/net462/FatEnemies.dll` to `Valheim/BepInEx/plugins/`.

## Config (`BepInEx/config/com.example.fatenemies.cfg`, created on first run)
- `BellySize` (default 1.7) – torso inflation. 1 = vanilla.
- `BodyWidth` (default 1.0) – optional whole-body stretch.
- `BodyHeight` (default 1.0)
- `SpeedMultiplier` (default 0.6) – movement speed (walk/run/swim/fly). 1 = vanilla.
- `AnimationSpeedMultiplier` (default 0.6) – animation playback speed; also slows attack wind-ups.
- `AffectTamed`, `AffectBosses`, `Enabled`

Note: scaling the root also scales hitboxes, so fat enemies are easier to hit
(and may get stuck in doorways). Visual/hitbox scaling is client-side; in
multiplayer everyone wanting to see it needs the mod.
