# Anomaly — As-Built Architecture

> This document describes what the code **actually does today**, including where it diverges from [design.md](design.md). Design intent lives in design.md; this file is the ground truth for building on the current codebase. Update it whenever a system changes shape.

## Layer Map

| Layer | Location | Contents |
|---|---|---|
| Entities | `#Scripts/Entities/` | `Entity` base (`CharacterBody2D`), `Player`, `Enemy` (+partials), `Prop` types |
| Behaviors | `#Scripts/Entities/Behaviors/` | `IEntityBehavior` implementations attached via `Entity.AddBehavior()` |
| State | `#Scripts/StateMachines/` | Single `StateMachine` node class handling both Player and Enemy logic |
| Systems | `#Scripts/Systems/` | `ZAxisSystem`, `TenacitySystem`, `YSortSystem`, `SaveSystem`, `ResourceManager`, `Keybinds` |
| Weapon | `#Scripts/Weapon/` | `Weapon` (+partials), `WeaponArc` base + Arc subclasses, `WeaponManager`, `WeaponStats` |
| UI | `#Scripts/UI/` | Resource bars, GUI, menus, damage numbers |
| Camera | `#Scripts/Camera/` | `CameraFeedback`, `CameraFocus`, Phantom Camera integration |

## How the Core Loop Actually Works

- `Entity._Ready()` runs: `ApplyEntityStats()` (copies `EntityStats` Resource values over exported fields) → `InitializeEntity.InitializeNodes()` (string-based node lookup) → `EnsureStateMachine()` (finds or creates a `StateMachine` child) → behavior `OnReady()`.
- Behaviors are plain C# classes (not Nodes) receiving `OnReady/OnProcess/OnPhysicsProcess/OnExitTree` callbacks from the owning Entity.
- `StateMachine` drives everything per-frame: player input polling (`ProcessPlayerInput`), player state timers, and enemy chase/attack AI are all inside this one class, branched on `Player`/`Enemy` casts.
- Damage: `Hurtbox` → `Entity.TakeDamage()` → virtual hooks (`CanTakeDamage`, `ApplyDamageModifiers`, `OnDamageTaken`, `OnDeath`). Knockback/stagger flow through `KnockbackBehavior`/`TenacityBehavior`/`TenacitySystem` separately — the unified `ImpactBehavior` from design.md §3.4 does not exist yet.
- Player resources (Health/Stamina/Corruption/Vessel/"Health S"/"Stamina S") live in `PlayerStats`, a **string-keyed dictionary** (`Stats.GetCurrent("Stamina")`), fronted by `ResourceManager` (an `IEntityBehavior` with a static `Instance`).
- Saving: `SaveSystem` serializes player position + `PlayerStats`/`WeaponStats` dictionaries to **JSON** at `user://savegame.json`, then reloads the current scene.

## Known Divergences from design.md

| design.md says | Code reality |
|---|---|
| Static signal registries (`PlayerSignals`, `EnemySignals`, ...) | Do not exist; C# `event Action` members on `StateMachine`/`ResourceManager` serve this role |
| `SoulWeaponArc` is a `Resource` (pure data, `.tres` authored) | `WeaponArc` is a `Node2D` with subclasses (`ScytheArc`, `HammerArc`, `SpearArc`) and scene files per Arc |
| Node references are never `[Export]`ed | `WeaponArc` exports `Hitbox`, `Sprite`, `AnimationPlayer` (partially overwritten in `_Ready`) |
| `EntityStats` Resource is the single stat authority | `Entity` also exports `Weight`/`Tenacity`/`MaxHealth`/etc.; the Resource silently overwrites exported values at `_Ready` |
| Save = Godot `Resource` instances via `ResourceSaver` (`.res`), 5 domains | Single JSON file, 2 domains (player + weapon), scene reload on save |
| `PlayerStats`/`WeaponStats` store upgrade **counts**, values derived at use | `PlayerStats` stores live Current/CurrentMax/TotalMax floats per string key |
| StatType enums referenced by Amulets/Arcs | Stats are addressed by raw strings (`"Health S"`, `"Stamina Regen"`) |
| Per-entity state machines / behavior separation | One `StateMachine` class contains player input, player states, and enemy AI |

When writing new code, follow design.md's target patterns (Resources for designer data, no exported node references, registries) — do not copy the legacy patterns above. When a task forces you to interact with a divergence, check [pitfalls.md](pitfalls.md) for the containment strategy.

## Integration Points for New Features

- **New enemy type**: subclass `Enemy` (partials if large), author an `EntityStats` `.tres`, scene under `#Scenes/Entities/Enemies/`, animations named `{Anim}_{U|D|S}` to match `Entity.GetAnimationCandidates`.
- **New behavior**: implement `IEntityBehavior`, attach in the owner's `_Ready` via `AddBehavior()`. Behaviors must not poll input directly (that is `PlayerInputBehavior`/`StateMachine` territory today).
- **New Weapon Arc**: currently requires a `WeaponArc` subclass + scene under `#Scenes/Weapons/`. Target state per design.md is Resource-driven — prefer moving Arc *data* into a Resource when touching this system.
- **New player resource/meter**: today requires a `PlayerStats` dictionary entry + `ResourceManager` property + events + UI bar wiring in `ResourcesUI`. All four places must agree on the string key.
- **New saved data**: extend `SaveSystem.SaveGame/ApplyLoadedData` and the owning class's `ToDictionary/LoadFromDictionary` pair.
