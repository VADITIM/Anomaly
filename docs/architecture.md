# Anomaly — As-Built Architecture

> This document describes what the code **actually does today**, including where it diverges from [design.md](design.md). Design intent lives in design.md; this file is the ground truth for building on the current codebase. Update it whenever a system changes shape. Last updated 2026-07-07 (save architecture + difficulty scaling + test harness).

## Layer Map

| Layer | Location | Contents |
|---|---|---|
| Entities | `#Scripts/Entities/` | `Entity` base (`CharacterBody2D`), `Player`, `Enemy` (+partials), `Prop` types, `StatType` enum |
| Behaviors | `#Scripts/Entities/Behaviors/` | `IEntityBehavior` implementations attached via `Entity.AddBehavior()` |
| State | `#Scripts/StateMachines/` | `StateMachine` base + `PlayerStateMachine` + `EnemyStateMachine` |
| Systems | `#Scripts/Systems/` | `ZAxisSystem`, `TenacitySystem`, `YSortSystem`, `DifficultyScalingSystem`, `ResourceManager`, `Keybinds` |
| Save | `#Scripts/Save/` | `SaveManager` (static authority) + domain Resources: `PlayerSaveData`, `WeaponSaveData`, `ProgressionData`, `DifficultyData`, `WorldData`; `SavePoint` enum |
| Tests | `Tests/` | `Anomaly.Tests` xunit project — pure-math coverage (difficulty tables, stat clamping/upgrade math); no engine-native calls |
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
- **Stat authority**: an assigned `EntityStats` Resource (property `Entity.EntityStats` — the plain `Stats` name belongs to `Player.Stats`, a `PlayerStats`) overrides scene-set exported values (including `Health`); unassigned means exported values apply. Enemy subclass constructors (`Bee`, `PracticeDummy`) form a third, lowest-precedence layer under scene exports. **Tenacity is authored on the runtime meter scale (~1–15), not design.md's 75–100** — weapons land ~0.2–1.2 tenacity damage per hit.
- Player resources live in `PlayerStats`, keyed by the `StatType` enum (`Stats.GetCurrent(StatType.Stamina)`). Weapon stats use `WeaponStatType`. Raw strings exist only at the save boundary (enum `ToString()` + legacy-key maps so old saves load).
- `ResourceManager` is an `IEntityBehavior` owned by `Player` (no static instance). UI reaches it via the Player node (see `ResourcesUI.DeferredSubscribe`). Healing drain is deterministic: values lerp from captured start values; total heal equals the drained Health S regardless of frame rate.
- Damage: `Hurtbox`/hitbox → `Entity.TakeDamage()` → virtual hooks. The player's `Weapon` owns full damage resolution (`ApplyDamage`: arc `DamageMultiplier` × combo/heavy × weakness (1.3 matched, 0.9–1.0 baseline via `Enemy.IsWeakTo`) × penetration-vs-armor); one damage application per entity per swing (`Weapon` dedupes body+Hurtbox overlap). Knockback/stagger flow through `KnockbackBehavior` (reads owner config live, stops exactly at duration end) and `TenacitySystem` (unified `ImpactBehavior` from design.md §3.4 does not exist yet). Enemies strike back: `EnemyStateMachine` drives `EnemyAttackPhase` (WindUp → Active at 50% → Recovery at 75%) and applies `Enemy.Damage` to the Player once per swing at mid-attack within `AttackRange × 1.25`.
- Death: enemy death (weapon or not) routes through `StateMachine.RequestDeath` → `OnDied` → `GrantDeathRewards` (rewards skip silently without a Player). Player death waits 2.5 s then `SaveManager.ReloadFromDisk()` — interim loop until the §3.13 SavePoint location map exists.
- Weapon Arc authority: `WeaponStats` defaults are the Scythe baseline (Damage 5, StaminaCost 51, TenacityDamage 10); Arc flavor applies through `WeaponArc` multipliers (`Damage/Tenacity/Penetration/StaminaCost`) at point of use and is never written back into `WeaponStats`.
- Saving: `SaveManager` (`#Scripts/Save/`) owns the single mutable instance of each save Resource and writes all five domains simultaneously as `.res` via `ResourceSaver` (design.md §3.11). Each domain carries a `Version` field. `SaveManager.Save(player, savePoint)` takes the Player explicitly (no `Player.Instance` use); `SavePoint.None` (canonical events) never moves the respawn point. Loading is lazy: `SaveManager.EnsureLoaded()` runs on first access (`Player._Ready` via `ApplyTo`, `Enemy` via level sampling); `ReloadFromDisk()` drops session instances and reloads the scene. Legacy JSON saves (`user://savegame.json`, envelope v2 and pre-envelope v1) are imported once when no `.res` domains exist.
- Difficulty: `DifficultyScalingSystem` is pure static math (no Godot deps, unit-tested). Each Enemy samples `EnemyLevel` once in `InitializeEnemy()` from `SaveManager.Difficulty.DifficultyLevel` and multiplies `Health`/`Damage` by the level multiplier; `EnemyLevel` is never saved. `SaveManager.RecordVoidHeartDestroyed()` is the write path that raises world difficulty (capped at 5) — no caller yet, Void Hearts are not in code.

## Known Divergences from design.md

| design.md says | Code reality |
|---|---|
| Static signal registries (`PlayerSignals`, `EnemySignals`, ...) | Do not exist; C# `event Action` members on `StateMachine`/`ResourceManager` serve this role |
| `SoulWeaponArc` is a `Resource` (pure data, `.tres` authored) | `WeaponArc` is a `Node2D` with subclasses (`ScytheArc`, `HammerArc`, `SpearArc`) and scene files per Arc (P6) |
| `PlayerStats`/`WeaponStats` store upgrade **counts**, values derived at use | Live Current/CurrentMax/TotalMax floats per `StatType`; the save layer persists these stat blocks (`PlayerSaveData.Stats`/`WeaponSaveData.Stats` dictionaries) until the `[UNDEFINED]` base/per-upgrade values in design.md §3.10 are defined |
| Respawn resolves position from a scene-registered location map | Player position is saved/restored directly; no location map exists yet |
| `SaveManager.Load()` called once at game start (autoload) | Lazy `EnsureLoaded()` from first consumer — an autoload service is still the target (P4) |
| Input handled by a dedicated input layer | Movement input in `PlayerInputBehavior`; action input polled inside `PlayerStateMachine` (P10) |

When writing new code, follow design.md's target patterns — do not copy legacy patterns. Check [pitfalls.md](pitfalls.md) for open items and their containment rules.

## Integration Points for New Features

- **New enemy type**: subclass `Enemy`, author an `EntityStats` `.tres` (authoritative when assigned), scene under `#Scenes/Entities/Enemies/`, animations named `{Anim}_{U|D|S}`. AI beyond chase/attack goes in an `EnemyStateMachine` subclass via `CreateStateMachine()` override — not branches in the shared class.
- **New entity category (Boss, NPC)**: own `StateMachine` subclass + `CreateStateMachine()` override + `new`-typed `StateMachine` property, mirroring Player/Enemy.
- **New behavior**: implement `IEntityBehavior`, attach in the owner's `_Ready` via `AddBehavior()`. Behaviors must not poll input directly.
- **New Weapon Arc**: currently a `WeaponArc` subclass + scene under `#Scenes/Weapons/` (child `Area2D` must be named `Hitbox Area`). Prefer moving Arc data into a `SoulWeaponArc` Resource when touching this (P6 containment).
- **New player stat/meter**: add a `StatType` member + `PlayerStats` constructor entry + `ResourceManager` property/event + UI bar wiring. The compiler now finds every site that must agree.
- **New saved data**: add a field to the owning domain Resource in `#Scripts/Save/` (or a new domain Resource + `SaveManager` property + write in `Save()`/load in `Load()`). Bump the domain's `Version` when its shape changes and migrate in `SaveManager`. Never load save Resources directly — always through `SaveManager`.
- **New pure-math system**: keep it free of Godot engine calls (System.Math, no `Godot.Collections`) and add coverage in `Tests/` — see `DifficultyScalingSystem` for the pattern.
