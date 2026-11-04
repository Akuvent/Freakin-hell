# Project layout

Godot project root (`res://`). Lowercase folders; PascalCase for `.tscn` / typed resources.

```
freakin-hell/
├── project.godot
├── icon.svg
├── docs/                      Design + this file
├── itch-assets/               Marketing / store art (not loaded in-game)
│
├── scenes/                    Instanced scenes only
│   ├── main.tscn              Entry (boot → app flow)
│   ├── app/                   Boot, main menu, settings
│   ├── match/                 Match root, between-round flow
│   ├── round/                 One playable round (arena + spawns)
│   ├── characters/            Fighter scenes
│   ├── stages/                Maps / tile chunks
│   ├── ui/                    HUD, card pick, pause
│   ├── vfx/                   Hit sparks, glitch overlays
│   └── camera/                Split / follow rigs (if scene-based)
│
├── scripts/                   GDScript
│   ├── autoload/              Singletons (game, audio, net later)
│   ├── core/                  Constants, math, shared helpers
│   ├── characters/
│   ├── combat/                Hit data, hurtboxes, hitstop
│   ├── match/                 Stocks, round win, best-of
│   ├── map/                   Rot, boundaries, spawn points
│   ├── mobs/
│   ├── items/
│   ├── cards/                 Card effects + registry
│   ├── ui/
│   ├── camera/
│   └── net/                   Multiplayer (stub until milestone)
│
├── resources/                 .tres, .res, shaders
│   ├── combat/
│   ├── characters/
│   ├── cards/
│   ├── ui/
│   ├── themes/
│   └── shaders/
│
├── data/                      JSON / CSV (cards, pools, tuning)
│   ├── cards/
│   ├── pools/
│   └── tuning/
│
├── art/                       Source art (import into Godot)
│   ├── concepts/              Style experiments
│   ├── characters/
│   ├── stages/
│   ├── ui/
│   └── vfx/
│
├── audio/
│   ├── sfx/
│   └── music/
│
├── addons/                    Third-party plugins
└── tests/                     Dev test scenes / harness
    └── scenes/
```

## Conventions

- **Scenes** wire nodes; **scripts** hold behavior; **resources** hold tunable data Godot edits well.
- **data/** holds card JSON and balance tables the game loads at runtime (mod-friendly later).
- **art/concepts/** is for parallel visual directions; promote winners into **art/characters** etc.
Ported from `Documents/platformer-fighter` (minimal combat slice):

- `scripts/characters/player.gd`, `scripts/combat/hit_data.gd`
- `scripts/autoload/game_manager.gd`, `scripts/camera/camera_shake.gd`, `scripts/map/world_boundary.gd`
- `resources/shaders/impact_outline.gdshader`, `scenes/vfx/hit_spark.tscn`
- Eight prototype pose PNGs under `art/characters/prototype/poses/` (replace when vector art lands)

Not ported: tilemaps, UI chrome, sfx banks, dummy enemy, export presets.

Run target: `scenes/round/combat_sandbox.tscn` (also reachable via `scenes/main.tscn`).

