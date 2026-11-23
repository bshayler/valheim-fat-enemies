# Fat Enemies

A [BepInEx](https://github.com/BepInEx/BepInEx) mod for **Valheim** that makes every enemy look like it has been skipping leg day, and meals.

Greydwarves, Trolls, boars, bosses: anything that isn't a player gets a big round belly, wider arms and a comically small head. Fat creatures also move and animate slower, so they waddle.

## Features

- **Round, not just big.** The spine bone is inflated while the head is scaled back down, so enemies look like they gained weight instead of simply growing.
- **Wider overall.** An optional whole-body stretch adds to the effect.
- **Slower.** Movement speed and animation speed are both reduced (this also slows attack wind-ups, so you get more time to dodge).
- **Configurable.** Every value is tunable in a config file, and you can leave tamed animals and bosses alone.
- Works with leveled (starred) creatures.

## Install

1. Install **BepInExPack_Valheim** from [Thunderstore](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/), either with a mod manager such as r2modman or manually into your Valheim folder. Launch the game once so the `BepInEx` folders are created.
2. Download [`dist/FatEnemies-dropin.zip`](dist/FatEnemies-dropin.zip) and unzip it.
3. Copy `FatEnemies.dll` into `Valheim/BepInEx/plugins/`. (If you drag the whole `BepInEx` folder over your install, choose **merge**, never replace.)
4. Launch Valheim. A config file appears at `BepInEx/config/com.example.fatenemies.cfg`.

With r2modman, put the DLL in the profile's `BepInEx/plugins` folder instead (Settings, then Browse profile folder).

## Configuration

Edit `BepInEx/config/com.example.fatenemies.cfg` with the game closed.

| Section | Setting | Default | What it does |
|---|---|---|---|
| General | `Enabled` | `true` | Turns the mod on or off. |
| General | `AffectTamed` | `false` | Also fatten tamed creatures. |
| General | `AffectBosses` | `true` | Also fatten bosses. |
| Fatness | `BellySize` | `2.2` | Torso inflation. 1 is normal, 3 is absurd. |
| Fatness | `FatLimbs` | `true` | Arms inflate with the belly. Only the head stays normal size. Set `false` for thin arms. |
| Fatness | `LimbSize` | `1.8` | Thickness of every arm and leg, scaled across the limb rather than along it, so limbs get fat without getting longer. |
| Fatness | `BodyWidth` | `1.4` | Whole-body sideways and front-to-back stretch. |
| Fatness | `BodyHeight` | `0.9` | Whole-body height multiplier. Below 1 looks squatter. |
| Slowness | `SpeedMultiplier` | `0.6` | Walk, run, swim and fly speed. 1 is vanilla. |
| Slowness | `AnimationSpeedMultiplier` | `0.6` | Animation playback speed. 1 is vanilla. |

If you update from an older version, delete the old `.cfg` so the new defaults apply.

## How it works

Harmony patches `Character.Awake` and `Character.SetLevel` and tags each non-player creature with a small component. Every frame (after the Animator runs) that component:

1. Scales the creature's topmost "spine" bone, falling back to "hips" or "pelvis", by `BellySize`.
2. Thickens every arm and leg bone across its width (`LimbSize`).
3. Counter-scales the head so it keeps its normal size.
4. Scales the animator's speed by `AnimationSpeedMultiplier`.

The creature's walk, run, swim and fly speeds are scaled once by `SpeedMultiplier`. Re-applying never compounds.

Bones are found by name, so an unusual skeleton may not match. The log reports what was found for each creature type, for example `Troll: belly bone 'Spine', 1 counter-scaled`, or `no spine/hips bone found, belly effect skipped` if nothing matched.

## Known limitations

- `BodyWidth` also widens hitboxes, so fat enemies are easier to hit (and may get stuck in doorways).
- The effect is client-side. In multiplayer, everyone who wants to see it needs the mod.
- The look depends on each creature's skeleton and skin weighting, so some models will look better than others.

## Build from source

You need the .NET SDK and a Valheim install with BepInEx.

```bash
dotnet build -c Release -p:ValheimPath="D:\SteamLibrary\steamapps\common\Valheim"
```

The output is `bin/Release/net462/FatEnemies.dll`. The source is a single file, [`FatEnemies.cs`](FatEnemies.cs).

## Troubleshooting

Send `BepInEx/LogOutput.log` when reporting a problem. Look for the line `Loading [Fat Enemies ...]` to confirm the plugin loaded, and for lines starting with `[Error` or `[Warning`.

## Disclaimer

Unofficial fan mod, not affiliated with Iron Gate or Coffee Stain. Valheim updates can change internal names and break the mod.
