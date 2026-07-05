# Anomaly — Structural Pitfalls Register

> Tracked structural debt that will break later development if built upon. Ordered by blast radius. When a pitfall is resolved, move it to the Resolved section with a one-line note on how. Audited 2026-07-05.

## P1 — StateMachine is a god class (highest blast radius)

`#Scripts/StateMachines/StateMachine.cs` contains player input polling, player state timers, heavy-charge logic, stamina spending, **and** enemy chase/attack AI in one 600-line class, branched on `Player`/`Enemy` casts. Every new entity category (Boss, NPC) and every new state multiplies the branching.

- **Breaks later**: Boss AI cannot be added without either bloating this class further or forking it; state transition rules are implicit in scattered `if` guards, so new states silently interact with old ones (e.g. `RequestStagger` can interrupt `HeavyCharging` with no cleanup of `HeavyChargeProgress`).
- **Containment**: new entity AI goes in entity-specific subclasses or behaviors, never new branches in `ProcessPlayerInput`/`ProcessEnemy`. Target: split into `PlayerStateMachine` / `EnemyStateMachine` subclasses; move input polling into `PlayerInputBehavior` (which already exists but is bypassed).

## P2 — String-keyed stats with no compile-time safety

`PlayerStats` is `Dictionary<string, Stat>` addressed by raw strings: `"Health"`, `"Health S"`, `"Stamina Regen"`, `"Vessel"`. `ResourceManager` exposes the same stat under two names (`Xp` and `Vessel` both map to `"Vessel"`). A typo compiles fine and silently returns `0f` (`GetCurrent` null-coalesces).

- **Breaks later**: every new meter (Corruption mechanics, Aetheriac State) adds more magic strings across 4+ files (PlayerStats ctor, ResourceManager property, events, UI). Silent-zero failures are the worst kind to debug mid-combat.
- **Containment**: design.md §3.10 already specifies `StatType` enums — introduce them before adding any new stat. Remove the `Xp`/`Vessel` alias while at it.

## P3 — Dual stat authority: EntityStats Resource vs. exported Entity fields

`Entity` exports `MaxHealth`, `Weight`, `Tenacity`, `CanBeKnockedBack`... and *also* takes an `EntityStats` Resource; `ApplyEntityStats()` silently overwrites the exported values at `_Ready` when `Stats` is set. Scene-tweaked values vanish at runtime with no warning. Defaults disagree wildly (`Entity.MaxHealth = 99999f`, `EntityStats.MaxHealth = 100f`; `Entity.Tenacity = 5f`, `EntityStats.Tenacity = 100f`).

- **Breaks later**: difficulty scaling (design.md §3.12 `ScaleStat()`) needs one authoritative base value to multiply; two sources guarantee balance bugs. Designers tuning in the editor cannot tell which knob is live.
- **Containment**: treat `EntityStats` as the only authority; when touching an entity, remove its duplicated exported stat fields rather than adding more.

## P4 — Singleton coupling and stale-instance risk

`Player.Instance` (bare public static field), `ResourceManager.Instance` (set in **constructor**), `StateMachine` finding the player via `GetTree().Root.FindChild("Player", true, false)`. `SaveSystem.SaveGame()` calls `ReloadCurrentScene()` — after which any held reference to the old Player/ResourceManager is a freed Godot object; static `Instance` fields are only rescued because the new player's `_Ready` happens to reassign them.

- **Breaks later**: multi-scene flow (Corrupted Void, respawn at Maldor, menus) will hit freed-instance crashes or silently-wrong state. The `ResourceManager.Instance != null ? ... : fallback` pattern is already duplicated three times in `StateMachine` because the lifetime is untrustworthy.
- **Containment**: never add a new `.Instance`; resolve the player through the scene tree once at `_Ready` of the consumer. Long-term: an autoload/service locator for genuinely global systems (Save, Difficulty), entity-scoped access for the rest.

## P5 — Save system cannot carry the designed progression

`SaveSystem` saves player position + two stat dictionaries as JSON, and **reloading the scene is a side effect of saving**. design.md §3.11 requires five domains (`PlayerStats`, `WeaponStats`, `ProgressionData`, `DifficultyData`, `WorldData`), `.res` Resources via `ResourceSaver`, save-point-based respawn, and trigger categories. None of that shape exists.

- **Breaks later**: Arc unlocks, difficulty level, killed Disciples, narrative flags have nowhere to live; retrofitting them into the flat JSON means versioning pain. The reload-on-save side effect will fight any future save trigger that must *not* reset the world (canonical events per §3.11).
- **Containment**: do not extend the JSON format. First new persistent feature should introduce the `SaveManager` + Resource-domain layout from design.md and migrate the two existing dictionaries into it.

## P6 — WeaponArc violates core architecture rules

`WeaponArc` is a `Node2D` with `[Export] Area2D Hitbox`, `[Export] Sprite2D Sprite`, `[Export] AnimationPlayer AnimationPlayer` — exported node references, the project's own hard "no exceptions" rule — and `_Ready` partially overwrites them via string lookups anyway. Arcs are code subclasses + scenes, not the designed `SoulWeaponArc` Resources, so every property access throws `InvalidOperationException` if `parentWeapon` isn't set (eight near-identical throwing properties).

- **Breaks later**: three more Arcs are planned (Sword, Dagger, Malice). Each currently costs a class + scene + animation wiring instead of a `.tres`. The efficiency model (90–100%/130%) has no data home.
- **Containment**: new Arcs should push shared data into a `SoulWeaponArc` Resource (per design.md §3.5) and keep only presentation in the scene. Do not add a ninth throwing property.

## P7 — Stringly-typed node paths and animation names

`InitializeEntity` probes `"Animation Player"` / `"AnimationPlayer"` / `"Animator"`; resource bars probe five path variants; `Player._Ready` does `GetNodeOrNull<Weapon>("WEAPON") ?? new Weapon()` — a misnamed node yields a **silently empty weapon**. Animation lookup tolerates case variants (`"Attack_..."` / `"attack_..."`), codifying asset naming inconsistency into permanent fallback chains.

- **Breaks later**: every renamed scene node or animation silently degrades (empty weapon, no health bar, idle fallback) instead of failing loudly. The fallback lists only grow.
- **Containment**: standardize scene node names and animation names (pick one casing), then delete fallbacks. New lookups fail loudly (`GD.PrintErr` + create, per design.md §2.7) instead of adding another candidate string.

## P8 — Frame-rate-sensitive and duplicated resource math

`ResourceManager.ProcessHealing` lerps `Health S`/`Vessel` toward zero using cumulative progress *and* adds `healAmount` scaled by both `consumptionProgress / HealConsumptionDuration` **and** `delta` — the healed total varies with frame rate. Stamina-spend logic (`TryUseStamina` + fallback `SetCurrent`) is copy-pasted three times in `StateMachine`. Vessel consecutive-hit scaling multiplies count each hit (`n * 1.3`) rather than the documented `x = n × 1.3` curve being verified anywhere.

- **Breaks later**: healing/Vessel balance tuning will chase ghosts across machines; the design.md §3.8 Potency model has no single place to land.
- **Containment**: route all resource mutation through one method per resource; make heal totals time-based, not progress×delta hybrids.

## P9 — No test or verification harness

No test project exists, and core math (knockback force, tenacity, vessel scaling, difficulty multipliers) is only verifiable by playing. Combined with P8, balance regressions are invisible.

- **Containment**: pure-math systems (`TenacitySystem` calculations, vessel scaling, difficulty stat multipliers) are plain C# — they can get an xunit project without touching Godot. Worth doing before the balance pass design.md promises.

## Resolved

*(none yet)*
