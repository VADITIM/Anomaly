# Anomaly — As-Built Architecture

> This document describes what the code **actually does today**, including where it diverges from [design.md](design.md). Design intent lives in design.md; this file is the ground truth for building on the current codebase. Update it whenever a system changes shape. Last updated 2026-07-05 (post-refactor).

## Layer Map

| Layer | Location | Contents |
|---|---|---|
| Entities | `#Scripts/Entities/` | `Entity` base (`CharacterBody2D`), `Player`, `Enemy` (+partials), `Prop` types, `StatType` enum |
| Behaviors | `#Scripts/Entities/Behaviors/` | `IEntityBehavior` implementations attached via `Entity.AddBehavior()` |
| State | `#Scripts/StateMachines/` | `StateMachine` base + `PlayerStateMachine` + `EnemyStateMachine` |
| Systems | `#Scripts/Systems/` | `ZAxisSystem`, `TenacitySystem`, `YSortSystem`, `SaveSystem`, `ResourceManager`, `Keybinds` |
| Weapon | `#Scripts/Weapon/` | `Weapon` (+partials), `WeaponArc` base + Arc subclasses, `WeaponManager`, `WeaponStats`, `WeaponStatType` enum |
| UI | `#Scripts/UI/` | Resource bars, GUI, menus, damage numbers |
| Camera | `#Scripts/Camera/` | `CameraFeedback`, `CameraFocus`, Phantom Camera integration |

## State Machines

- `StateMachine` (base, concrete — used as-is by Props): transition core (`TransitionTo`, `CanTransitionTo`, locked-state rules), all `IsX` state queries, all events, shared stagger/knockback/attack timer fields, `RequestStagger/Knockback/Attack/Death/Revive`.
- `PlayerStateMachine`: action-input polling (`ProcessInput`), heal, dodge, heavy charge (`HeavyChargeProgress`), combo continuation, air attacks, stamina spending (single path: `SpendAttackStamina()` via the owning Player's `ResourceManager`).
- `EnemyStateMachine`: target acquisition, chase/attack AI (in `_PhysicsProcess`), `CurrentAttackPhase`.
- Wiring: no scene contains a StateMachine node. `Entity.EnsureStateMachine()` calls the virtual `CreateStateMachine()` factory; `Player`/`Enemy` override it and re-type their `StateMachine` property via `new`-shadowing. A new entity category gets its own subclass + factory override — never new branches in an existing state machine.

## How the Core Loop Works

- `Entity._Ready()`: `ApplyEntityStats()` → `InitializeEntity.InitializeNodes()` (warns loudly if no AnimationPlayer) → `EnsureStateMachine()` → `InitializeZAxis()` → behavior `OnReady()`.
- Behaviors are plain C# classes (not Nodes) receiving `OnReady/OnProcess/OnPhysicsProcess/OnExitTree` callbacks.
- **Stat authority**: an assigned `EntityStats` Resource overrides scene-set exported values (including `Health`); unassigned means exported values apply. Defaults agree between the two (`MaxHealth` 100).
- Player resources live in `PlayerStats`, keyed by the `StatType` enum (`Stats.GetCurrent(StatType.Stamina)`). Weapon stats use `WeaponStatType`. Raw strings exist only at the save boundary (enum `ToString()` + legacy-key maps so old saves load).
- `ResourceManager` is an `IEntityBehavior` owned by `Player` (no static instance). UI reaches it via the Player node (see `ResourcesUI.DeferredSubscribe`). Healing drain is deterministic: values lerp from captured start values; total heal equals the drained Health S regardless of frame rate.
- Damage: `Hurtbox`/hitbox → `Entity.TakeDamage()` → virtual hooks. Knockback/stagger still flow through `KnockbackBehavior`/`TenacityBehavior`/`TenacitySystem` (unified `ImpactBehavior` from design.md §3.4 does not exist yet).
- Saving: `SaveSystem` writes a versioned envelope `{version, domains: {Player, Weapon}}` to `user://savegame.json`. Saving has no side effects; loading reloads the scene and `Player._Ready` applies the data. Legacy (pre-envelope) saves still load.

## Known Divergences from design.md

| design.md says | Code reality |
|---|---|
| Static signal registries (`PlayerSignals`, `EnemySignals`, ...) | Do not exist; C# `event Action` members on `StateMachine`/`ResourceManager` serve this role |
| `SoulWeaponArc` is a `Resource` (pure data, `.tres` authored) | `WeaponArc` is a `Node2D` with subclasses (`ScytheArc`, `HammerArc`, `SpearArc`) and scene files per Arc (P6) |
| Save = Godot `Resource` instances via `ResourceSaver` (`.res`), 5 domains | Versioned JSON envelope, 2 domains (P5) |
| `PlayerStats`/`WeaponStats` store upgrade **counts**, values derived at use | Live Current/CurrentMax/TotalMax floats per `StatType` |
| Input handled by a dedicated input layer | Movement input in `PlayerInputBehavior`; action input polled inside `PlayerStateMachine` (P10) |

When writing new code, follow design.md's target patterns — do not copy legacy patterns. Check [pitfalls.md](pitfalls.md) for open items and their containment rules.

## Integration Points for New Features

- **New enemy type**: subclass `Enemy`, author an `EntityStats` `.tres` (authoritative when assigned), scene under `#Scenes/Entities/Enemies/`, animations named `{Anim}_{U|D|S}`. AI beyond chase/attack goes in an `EnemyStateMachine` subclass via `CreateStateMachine()` override — not branches in the shared class.
- **New entity category (Boss, NPC)**: own `StateMachine` subclass + `CreateStateMachine()` override + `new`-typed `StateMachine` property, mirroring Player/Enemy.
- **New behavior**: implement `IEntityBehavior`, attach in the owner's `_Ready` via `AddBehavior()`. Behaviors must not poll input directly.
- **New Weapon Arc**: currently a `WeaponArc` subclass + scene under `#Scenes/Weapons/` (child `Area2D` must be named `Hitbox Area`). Prefer moving Arc data into a `SoulWeaponArc` Resource when touching this (P6 containment).
- **New player stat/meter**: add a `StatType` member + `PlayerStats` constructor entry + `ResourceManager` property/event + UI bar wiring. The compiler now finds every site that must agree.
- **New saved data**: add a named domain to the `SaveSystem` envelope — never new flat keys. Bump `SaveVersion` when a domain's shape changes.
