# Anomaly — Structural Pitfalls Register

> Tracked structural debt that will break later development if built upon. Ordered by blast radius. When a pitfall is resolved, move it to the Resolved section with a one-line note on how. Audited 2026-07-05; major refactor applied same day.

## P7 — Stringly-typed node paths (narrowed)

The silent `?? new Weapon()` fallback now fails loudly; missing `AnimationPlayer` warns. Remaining: resource-bar candidate chains in `InitializeEntity`, animation-name case-variant fallbacks (`"Attack_"`/`"attack_"`), enemy debug-label probing.

- **Containment**: standardize scene node and animation names, then delete fallback candidates. New lookups fail loudly instead of adding another candidate string.

## P9 — Test harness covers pure math only (narrowed)

`Tests/Anomaly.Tests` (xunit, in the solution) covers `DifficultyScalingSystem`, `PlayerStats`, and `WeaponStats` math. Tenacity knockback curve, vessel consecutive-hit scaling, and heal totals remain playtest-only — their math lives inside engine-coupled classes (`TenacitySystem`, `ResourceManager`).

- **Containment**: new balance math goes in engine-free static classes (see `DifficultyScalingSystem`) with tests. When touching `TenacitySystem`/`ResourceManager`, extract the pure calculations so they become testable.

## Resolved

- **P6 — Weapon Arcs are Node subclasses, not Resources** (2026-07-20): Arc tuning moved into `SoulWeaponArc` Resources (`[GlobalClass]`, `#Assets/Weapons/Arcs/*.tres`); `ScytheArc`/`SpearArc`/`HammerArc` deleted. `WeaponArc` is now presentation only (sprite, AnimationPlayer, hitbox) with an `[Export] SoulWeaponArc Data`. A new Arc costs one `.tres` plus a scene, no C# class. `WeaponAttackType` was lifted out of `WeaponArc` into its own file so the Resource does not depend on the Node. Fixed alongside: Arcs no longer write `SpecialHitInterval`/`HitCount` back into the shared `Weapon`; the dead `WeaponStatType.SpecialHitInterval` stat was removed (dual authority with `Weapon._specialHitInterval`); `SlotArc` frees the outgoing Arc instead of leaking a node per swap; `Weapon._Ready` slots the Arc authored in the scene instead of a bare `new ScytheArc()`; `Soul Hammer Arc.tscn` was wired to `SpearArc.cs` and is now correct.

- **P4 — Player.Instance singleton and stale-instance risk** (2026-07-20): removed the bare `Player.Instance` static field. `StateDisplay` now resolves the Player via `GetTree().Root.FindChild("Player", ...)` at `_Ready` (matching the `ResourcesUI`/`UI`/`Camera` pattern); `TenacitySystem` reads `_enemy.Player` (the Enemy's already-resolved field) instead of going through the static `Combat` helper. The `Combat` static class was deleted — its only live call site was `IsHeavyAttacking`, and the rest (`IsAttacking`, `IsChargingHeavy`, `GetHeavyDamageMultiplier`) was dead code.

- **P10 — Input polling lives in PlayerStateMachine, not PlayerInputBehavior** (2026-07-20): `PlayerInputBehavior` now polls all action input (dodge, attack, heal, heavy) each frame and exposes it as intent properties (`DodgeJustPressed`, `AttackJustPressed`, `HealJustPressed`, `HeavyPressed`, `HeavyJustReleased`); `PlayerStateMachine.ProcessInput` reads those instead of calling `Input` directly. Single input path.

- **P5 — Save system below design spec** (2026-07-07): replaced `SaveSystem` JSON with `#Scripts/Save/` — static `SaveManager` owning 5 versioned domain Resources (`PlayerSaveData`, `WeaponSaveData`, `ProgressionData`, `DifficultyData`, `WorldData`) written together as `.res` via `ResourceSaver`, with the `SavePoint` trigger model. Legacy JSON saves import once. Player/Weapon domains still persist live stat blocks (not upgrade counts) until design.md §3.10 base values are defined.

- **P1 — StateMachine god class** (2026-07-05): split into `StateMachine` (transition core, events, shared timers), `PlayerStateMachine` (input, heal, combos, heavy charge), `EnemyStateMachine` (chase AI, attack phases). `Entity.CreateStateMachine()` factory; `Player.StateMachine`/`Enemy.StateMachine` re-typed via shadowing. No scene changes needed. Remaining input-path issue tracked as P10.
- **P2 — String-keyed stats** (2026-07-05): `StatType` and `WeaponStatType` enums throughout; strings only at the save boundary (`ToString()` + legacy-key maps, old saves still load). `Xp`/`Vessel` alias removed.
- **P3 — Dual stat authority** (2026-07-05): precedence made explicit — an assigned `EntityStats` Resource is authoritative (now also sets `Health`); unassigned means exported values apply. Defaults aligned (`MaxHealth` 99999 → 100).
- **P8 — Frame-rate-dependent resource math** (2026-07-05): healing drain is deterministic (lerp from captured start values; total heal equals drained Health S). Stamina spending collapsed from three copy-pasted fallback blocks into `PlayerStateMachine.SpendAttackStamina()`.
