# Anomaly — Structural Pitfalls Register

> Tracked structural debt that will break later development if built upon. Ordered by blast radius. When a pitfall is resolved, move it to the Resolved section with a one-line note on how. Audited 2026-07-05; major refactor applied same day.

## P4 — Player.Instance singleton and stale-instance risk (narrowed)

`ResourceManager.Instance` was removed (2026-07-05); `SaveManager` takes the Player as a parameter (2026-07-07); `Weapon` resolves its owning entity from the tree, Enemy rewards use the Enemy's own resolved `Player` field, and the static `Player.CanMove/CanAttack/IsPaused` wrappers were deleted (2026-07-09). `Player.Instance` (bare public static field) remains, used by UI and the static `Combat` helpers. Scene reload on load re-runs `Player._Ready` which reassigns it, but any scene without a Player leaves a freed reference behind.

- **Breaks later**: multi-scene flow (menus, Corrupted Void) can hit freed-instance access.
- **Containment**: never add a new `.Instance`; new consumers resolve the player once at `_Ready` (see `ResourcesUI` for the pattern). Long-term: an autoload service for genuinely global systems.

## P6 — Weapon Arcs are Node subclasses, not Resources (narrowed)

The exported-node-reference violation and the eight throwing properties were fixed (2026-07-05). Arcs no longer write into persistent `WeaponStats` — flavor lives in per-arc multipliers, and the efficiency model (90–100% baseline / 130% matched) is live in `Weapon.GetWeaknessMultiplier` (2026-07-09). Arcs remain `WeaponArc` Node subclasses + scenes rather than the designed `SoulWeaponArc` Resources, so each new Arc still costs a class + scene and the multiplier values are code, not data.

- **Containment**: when adding the next Arc, move shared data (durations, type, multipliers) into a `SoulWeaponArc` Resource per design.md §3.5 and keep only presentation in the scene.

## P7 — Stringly-typed node paths (narrowed)

The silent `?? new Weapon()` fallback now fails loudly; missing `AnimationPlayer` warns. Remaining: resource-bar candidate chains in `InitializeEntity`, animation-name case-variant fallbacks (`"Attack_"`/`"attack_"`), enemy debug-label probing.

- **Containment**: standardize scene node and animation names, then delete fallback candidates. New lookups fail loudly instead of adding another candidate string.

## P9 — Test harness covers pure math only (narrowed)

`Tests/Anomaly.Tests` (xunit, in the solution) covers `DifficultyScalingSystem`, `PlayerStats`, and `WeaponStats` math. Tenacity knockback curve, vessel consecutive-hit scaling, and heal totals remain playtest-only — their math lives inside engine-coupled classes (`TenacitySystem`, `ResourceManager`).

- **Containment**: new balance math goes in engine-free static classes (see `DifficultyScalingSystem`) with tests. When touching `TenacitySystem`/`ResourceManager`, extract the pure calculations so they become testable.

## P10 — Input polling lives in PlayerStateMachine, not PlayerInputBehavior

Movement input goes through `PlayerInputBehavior`, but action input (attack, dodge, heal, heavy) is polled inside `PlayerStateMachine.ProcessInput`. Two input paths; rebinding/replays/AI-driven players would have to touch both.

- **Containment**: new actions poll in one place only. Target: `PlayerInputBehavior` emits intents, `PlayerStateMachine` consumes them.

## Resolved

- **P5 — Save system below design spec** (2026-07-07): replaced `SaveSystem` JSON with `#Scripts/Save/` — static `SaveManager` owning 5 versioned domain Resources (`PlayerSaveData`, `WeaponSaveData`, `ProgressionData`, `DifficultyData`, `WorldData`) written together as `.res` via `ResourceSaver`, with the `SavePoint` trigger model. Legacy JSON saves import once. Player/Weapon domains still persist live stat blocks (not upgrade counts) until design.md §3.10 base values are defined.

- **P1 — StateMachine god class** (2026-07-05): split into `StateMachine` (transition core, events, shared timers), `PlayerStateMachine` (input, heal, combos, heavy charge), `EnemyStateMachine` (chase AI, attack phases). `Entity.CreateStateMachine()` factory; `Player.StateMachine`/`Enemy.StateMachine` re-typed via shadowing. No scene changes needed. Remaining input-path issue tracked as P10.
- **P2 — String-keyed stats** (2026-07-05): `StatType` and `WeaponStatType` enums throughout; strings only at the save boundary (`ToString()` + legacy-key maps, old saves still load). `Xp`/`Vessel` alias removed.
- **P3 — Dual stat authority** (2026-07-05): precedence made explicit — an assigned `EntityStats` Resource is authoritative (now also sets `Health`); unassigned means exported values apply. Defaults aligned (`MaxHealth` 99999 → 100).
- **P8 — Frame-rate-dependent resource math** (2026-07-05): healing drain is deterministic (lerp from captured start values; total heal equals drained Health S). Stamina spending collapsed from three copy-pasted fallback blocks into `PlayerStateMachine.SpendAttackStamina()`.
