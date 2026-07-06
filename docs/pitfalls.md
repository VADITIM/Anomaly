# Anomaly — Structural Pitfalls Register

> Tracked structural debt that will break later development if built upon. Ordered by blast radius. When a pitfall is resolved, move it to the Resolved section with a one-line note on how. Audited 2026-07-05; major refactor applied same day.

## P4 — Player.Instance singleton and stale-instance risk (narrowed)

`ResourceManager.Instance` was removed (2026-07-05); `Player.Instance` (bare public static field) remains, used by UI, SaveSystem, Enemy rewards, and Weapon hit handlers. Scene reload on load re-runs `Player._Ready` which reassigns it, but any scene without a Player leaves a freed reference behind.

- **Breaks later**: multi-scene flow (menus, Corrupted Void) can hit freed-instance access.
- **Containment**: never add a new `.Instance`; new consumers resolve the player once at `_Ready` (see `ResourcesUI` for the pattern). Long-term: an autoload service for genuinely global systems.

## P5 — Save system below design spec (narrowed)

Now versioned (`{version, domains}` envelope), reload-on-save side effect removed, legacy saves still load. Still JSON with 2 domains (Player, Weapon) instead of the designed 5 Resource domains (`ProgressionData`, `DifficultyData`, `WorldData` missing) and no save-point/trigger model.

- **Containment**: add new persistent data as a new named domain in the envelope — never new flat keys. The `.res`/`ResourceSaver` migration from design.md §3.11 is still the target.

## P6 — Weapon Arcs are Node subclasses, not Resources (narrowed)

The exported-node-reference violation and the eight throwing properties were fixed (2026-07-05). Arcs remain `WeaponArc` Node subclasses + scenes rather than the designed `SoulWeaponArc` Resources, so each new Arc still costs a class + scene, and the efficiency model (90–100%/130%) has no data home.

- **Containment**: when adding the next Arc, move shared data (durations, type, multipliers) into a `SoulWeaponArc` Resource per design.md §3.5 and keep only presentation in the scene.

## P7 — Stringly-typed node paths (narrowed)

The silent `?? new Weapon()` fallback now fails loudly; missing `AnimationPlayer` warns. Remaining: resource-bar candidate chains in `InitializeEntity`, animation-name case-variant fallbacks (`"Attack_"`/`"attack_"`), enemy debug-label probing.

- **Containment**: standardize scene node and animation names, then delete fallback candidates. New lookups fail loudly instead of adding another candidate string.

## P9 — No test or verification harness

No test project; core math (tenacity knockback curve, vessel scaling, heal totals) is only verifiable by playing.

- **Containment**: pure-math systems are plain C# — an xunit project can cover them without touching Godot. Worth doing before the balance pass design.md promises.

## P10 — Input polling lives in PlayerStateMachine, not PlayerInputBehavior

Movement input goes through `PlayerInputBehavior`, but action input (attack, dodge, heal, heavy) is polled inside `PlayerStateMachine.ProcessInput`. Two input paths; rebinding/replays/AI-driven players would have to touch both.

- **Containment**: new actions poll in one place only. Target: `PlayerInputBehavior` emits intents, `PlayerStateMachine` consumes them.

## Resolved

- **P1 — StateMachine god class** (2026-07-05): split into `StateMachine` (transition core, events, shared timers), `PlayerStateMachine` (input, heal, combos, heavy charge), `EnemyStateMachine` (chase AI, attack phases). `Entity.CreateStateMachine()` factory; `Player.StateMachine`/`Enemy.StateMachine` re-typed via shadowing. No scene changes needed. Remaining input-path issue tracked as P10.
- **P2 — String-keyed stats** (2026-07-05): `StatType` and `WeaponStatType` enums throughout; strings only at the save boundary (`ToString()` + legacy-key maps, old saves still load). `Xp`/`Vessel` alias removed.
- **P3 — Dual stat authority** (2026-07-05): precedence made explicit — an assigned `EntityStats` Resource is authoritative (now also sets `Health`); unassigned means exported values apply. Defaults aligned (`MaxHealth` 99999 → 100).
- **P8 — Frame-rate-dependent resource math** (2026-07-05): healing drain is deterministic (lerp from captured start values; total heal equals drained Health S). Stamina spending collapsed from three copy-pasted fallback blocks into `PlayerStateMachine.SpendAttackStamina()`.
