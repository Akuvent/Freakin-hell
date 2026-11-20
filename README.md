# Freakin' Hell

2D arena fighter built in Godot 4.7 with C#. Local two-player combat, shifting platforms, and match flow. Card builds and online play are planned; the current build is a combat sandbox you can run from the editor.

## Requirements

- [Godot 4.7](https://godotengine.org/) with **.NET** / C# support
- [.NET SDK 8.0](https://dotnet.microsoft.com/download)

Open `project.godot` in the Godot editor, let it build the C# project, then press **Play** (F5). The main scene is `scenes/round/combat_sandbox.tscn`.

## Controls

| Action | Player 1 | Player 2 |
|--------|----------|----------|
| Move left | A | Left arrow |
| Move right | D | Right arrow |
| Jump | W | Up arrow |
| Light attack | Right Shift | Numpad 0 |
| Heavy attack | Space | Enter |

Heavy attacks cost attack charge (build it with light hits). Hold the attack button through the startup blink, release to strike.

### Debug

- **K**: force both players to lose a stock (double KO), useful for testing round and match resolution.

## How a match works

Tuning lives in `data/tuning/defaults.json`.

- **Stocks per round**: each player respawns at spawn until stocks reach 0 (default 3).
- **HP**: 100 per life. Damage from hits and from standing on virus platforms.
- **Round win**: when one player has no stocks left, the other wins the round.
- **Match win**: first to win enough rounds (default 2) wins the match; run score on screen updates via `GameManager`.
- **Ring out**: falling into the pit counts as a death and uses a stock.

After a match, the arena reloads and a new match starts with round wins reset. Run score (top labels) carries across matches until you reset it in code or add a menu later.

## Map rot

`RotController` reads the `map_rot` section of `defaults.json`. A round clock (tracked by `MatchManager`) drives the mode:

1. After **glitch_start_seconds**, platforms start flickering.
2. Later, platforms are chosen one at a time: they either **fall away** (collision off) or become **virus** platforms (green, damage over time while you stand on them).

Adjust timers and `virus_chance` in JSON to iterate faster during development.

## Combat notes

- **Light hits**: small damage, hitstop, camera shake, can grant jump charge and attack charge to the attacker.
- **Heavy hits**: higher damage, full-screen impact effect, slow motion briefly.
- **Aerial hits**: if the attacker has vertical velocity, damage is multiplied.

Fighter stats and attacks live in `data/characters/fighters.json`. Both players use the `default` entry unless you set `CharacterId` on a player instance. `HitData` is still the runtime object created when a strike goes active. Match and rot tuning stay in `data/tuning/defaults.json`.

## Project layout

```
scenes/          Main, combat sandbox, player, VFX
scripts/
  autoload/      GameManager, MatchManager, Sfx (audio stubs)
  characters/    Player
  combat/        HitData
  round/         RotController
  map/           WorldBoundary (pit)
  camera/        CameraShake
  vfx/           HitSpark
data/
  tuning/        defaults.json
  cards/         Card JSON (example only; not wired in game yet)
resources/       Shaders, UI label settings
```

## Audio

`Sfx` is an autoload with empty methods. Drop sounds under a path you choose and implement the stubs when you are ready.

## Planned direction

From the project description and data on disk:

- Card builds (`data/cards/`) affecting stats and triggers
- Full run flow: menu, draft/build between matches, end-of-run reset on `GameManager`
- Items (`items` section in `defaults.json`)
- Online multiplayer later

## License

Proprietary. [All Rights Reserved](LICENSE). Copyright (c) 2026 Cliffwake.