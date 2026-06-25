# Anomaly — Claude Code Guide

## 1. Project Overview

**Anomaly** is a 2D top-down Action-RPG set in an **Ontological Dark Fantasy** world, fusing visceral, high-stakes combat with a deeply unsettling psychological narrative. It is built in **Godot 4.x**, using **C#** for scripting and **GDShader** for visual effects.

|**Status**|**In Development**|
|---|---|
|**Genre**|**Dark Fantasy / Ontological Horror Action-RPG**|
|**Platform**|**Windows (currently)**|
|**Engine**|**Godot 4.x (C#, GDShader)**|

---

### 1.1 Premise

You play as **Kio**, an Elite Hunter of the **Black Scythe Organisation** — a complex entity formed from three souls bound to a single weapon by his decades-long allegiance to the Order. For forty years he has served with absolute discipline.

A mysterious voice sends Kio into the decaying world of **Akasa** to hunt down _Void Hearts_ and destroy the corrupted **Disciples of Valdis**, ostensibly to halt a spreading cataclysm known as **the Ruination**. The progression is designed to _feel_ heroic — while actually steering the world toward its end.

To grow strong enough to face the Disciples, Kio must absorb the very corruption he is hunting. This power behaves like a severe addiction: it grants immense combat efficiency and intoxicating highs, while steadily dissolving his humanity with every drop consumed.

> The horror in **Anomaly** is not jump-scares — it is **ontological**. The terror lies in realizing the rules of your world, your memories, and your own identity are actively unraveling and losing meaning. There was never a Hero, nor a Villain. The story is engineered to make its ending identifiable through gradual revelation, not exposition: Kio's arc concludes with him consuming and _becoming_ Valdis, ending the world through the very actions meant to save it — **The Ruination: Valdis' Final Shape.**

The game deliberately rejects traditional RPG and Soulslike bloat. There are no cluttered inventories and no generic elemental spell lists. Instead, story and gameplay are unified through a single **transformation system**, and enemy weaknesses are communicated visually (e.g. heavy armor implies a piercing weakness) rather than through positional mechanics like backstabs.

### 1.2 Core Combat Identity — The Rend & Soul Weapon Arcs

Kio never changes weapons — he never puts down his iconic Scythe. Instead, after defeating a Disciple he unlocks **The Rend**: the ability to violently warp his weapon's metaphysical blueprint. The Rend System is driven through UI interaction.

By slotting different **Soul Weapon Arcs** into the **Soul Stone** mounted on the Scythe, Kio forces the weapon to adopt the physics of an entirely different archetype:

- **Slashing**
- **Piercing**
- **Smashing**

The Scythe's physical shape never changes, but its properties shift completely — a _Hammer Arc_ makes it strike with bone-crushing weight; a _Dagger Arc_ makes its animation frames skip and vibrate for rapid-fire speed. Critically, a Weapon Arc never changes the Scythe's basic attack pattern — it instead applies its effects on an interval, unique to each Arc.

**Efficiency model:**

|Condition|Efficiency|
|---|---|
|Any Arc vs. any enemy (baseline)|90–100%|
|Correct Arc matched to enemy's visual weakness|**130%**|

No archetype ever "hard locks" against an enemy — the baseline guarantees viability with any build, while reading an enemy's weakness and matching the right Arc (e.g. Piercing into heavy armor) delivers the damage spike that drives the moment-to-moment gameplay loop.

Each Arc also carries its own **Special Move**, using a damage type distinct from its main combat flow — enabling fluid, tactical hybrid playstyles without ever swapping gear.

_Amulets_ retain the spirit of traditional armor systems, but radically simplified, built to enhance build diversity rather than gate it.

### 1.3 In Summary

Combat is brutal and precise. The pixel art is atmospheric and minimalist. The story challenges what the player believes is real. You are an elite executioner wielding a glitching, reality-tearing power — slowly losing your sanity to the very force that keeps you alive.

### 1.4 Tone

Grim, authoritative, clinical, high-consequence, and absolute. Subverts classic high-fantasy tropes by treating spiritual and magical phenomena as rigid, unforgiving laws of cosmic decay. Avoid soft fantasy descriptors ("magical," "cursed," "unholy") — use structural, metaphysical terminology instead.

---

### 1.5 Core Pillars

- **The Irreversibility of Decay** — The Ruination cannot be cleansed or undone, only absorbed. Progression is a mechanical descent disguised as an ascent; to grow stronger, Kio must consume the corruption actively unraveling his identity.
- **The Reality Matrix** — Weapons do not change their physical reality; they break their metaphysical blueprints via *The Rend* to manifest different physical properties at precision hit intervals.
- **Aggression as Stability** — Safety is an active pursuit; healing and mental focus do not stem from passive resources, but are violently extracted from living entities by maintaining flawless combat momentum.

### 1.6 Absolute Impossibilities

These are non-negotiable. No mechanic, narrative beat, or system may contradict them.

- **Decorruption is non-existent.** A soul can ONLY corrupt further. No purification paths exist once the descent begins.
- **True Heroes and Villains do not exist.** No factions fight for pure moral good — only incompatible truths.
- **Weapon Discarding is impossible.** Soul Bonds are permanent. Bound objects are physically locked to their partner entity and can never be thrown away or replaced — only structurally distorted.

---

## 2. Architecture

This section defines **how code is written** in Anomaly: conventions, casing, structural rules, and the patterns every script must follow regardless of which system it belongs to.

### 2.1 Naming & Casing Rules

| Category                                                          | Casing           |
| ----------------------------------------------------------------- | ---------------- |
| Primitives                                                        | camelCase        |
| `Vector2`/`Vector3`, collections, runtime data containers         | camelCase        |
| Godot types, gameplay systems, entities, nodes, framework classes | PascalCase       |
| Private fields (no underscore prefix)                             | camelCase        |
| Local variables                                                   | camelCase        |
| Public properties                                                 | PascalCase       |
| Constants                                                         | UPPER_SNAKE_CASE |

Do not simplify names. Always name each category fully — e.g. `StateMachine` instead of `SM` or similar abbreviations.

Avoid mixing naming styles within the same subsystem.

```csharp
// Correct
private float moveSpeed;
private bool canAttack;
private Vector2 movementDirection;
public StateMachine StateMachine;
public AnimationPlayer AnimationPlayer;

// Wrong
private float _moveSpeed;   // underscore prefix
public StateMachine SM;     // abbreviation
```

### 2.2 Comments

- Avoid comments whenever possible
- Never restate what the code already explains
- Prefer expressive class and method names
- Reserve comments for genuinely complex algorithms, architecture decisions, or mathematical reasoning

### 2.3 Null Checks

- Avoid unnecessary defensive null checks
- Prefer deterministic initialization and clear ownership guarantees
- Only validate null when a failure case is realistically possible

### 2.4 Godot Node Access

- Never export node references
- Resolve nodes through **Owner → Root → Create**
- Cache references during initialization — never re-resolve per frame

### 2.5 Export Attributes

Exports are reserved for **configuration values and Resource references** — never for node references. See [§2.6](https://claude.ai/chat/2a16c8f3-f9fd-49bd-8b14-a94eaa9593ee#26-data-authority--resources-vs-exports-vs-hardcoded-values) for which category a given value belongs in.

✅ **Valid — primitive config value:**

```csharp
[Export] public float moveSpeed { get; set; } = 100f;
```

✅ **Valid — Resource reference:**

```csharp
[Export] public SoulWeaponArc CurrentArc;
```

❌ **Invalid — node reference:**

```csharp
[Export] private Sprite2D sprite;
```

Node references must always be resolved at runtime. If `[Export]` appears on any Godot node type — `Node`, `Node2D`, `Sprite2D`, `AnimationPlayer`, `CharacterBody2D`, or any subclass — flag it immediately. No exceptions.

### 2.6 Data Authority — Resources vs. Exports vs. Hardcoded Values

Every exported value falls into exactly one of three categories. Picking the right one is mandatory, not stylistic.

|Category|Use for|Example|
|---|---|---|
|**Resource** (`[Export] public SomeResource data`)|Reusable, designer-authored **data** shared or swapped across many instances|`SoulWeaponArc`, `EntityStats`, `CameraFocusProfile`|
|**Exported value** (`[Export] public float x`)|A single tunable **primitive** scoped to one instance|`moveSpeed`, `weight`, `tenacity`|
|**Node reference**|Never exported — see [§2.5](https://claude.ai/chat/2a16c8f3-f9fd-49bd-8b14-a94eaa9593ee#25-export-attributes)|n/a|

**A value belongs in a `Resource` when at least one of these is true:**

- It needs to exist as a standalone **asset** in the editor (`.tres`), independent of any scene
- The same data is meant to be **shared or reused** across multiple entities/instances
- A designer should be able to create new variants **without touching code** (e.g. a new Soul Weapon Arc)

**A value stays a plain `[Export]` field when:**

- It's a single number/bool/enum scoped to one entity instance (e.g. `weight = 2f` on a specific Enemy)
- It has no standalone identity outside the entity that owns it

**Resources can absolutely describe runtime behavior — they just must never _hold_ mutable runtime state.** Behavior trees, ability definitions, AI graphs, and animation state data are all legitimate, often desirable, Resource-driven patterns: they are data that _describes_ behavior, authored once and read by many instances. What must stay out of a Resource is **live, per-instance, mutating data** — current stagger meter, current knockback velocity, the StateMachine's current node — because Godot caches and shares loaded Resources by path, so mutating one in place corrupts every other instance referencing that same asset. `ImpactBehavior` resolution and knockback/stagger _calculations_ remain code for this reason, not because Resources are unfit for behavior in general.

The litmus test: would two entities sharing this Resource ever need to hold _different_ current values for it at the same time? If yes, it's per-instance runtime state — keep it as a field on the entity/behavior, or duplicate the Resource explicitly. If the data is the same read-only blueprint for every instance until a designer changes it, a Resource is the right call.

> Resources loaded from disk are cached and shared by path in Godot unless explicitly duplicated. Treat every `Resource` as **read-only configuration data** at runtime; never mutate a loaded Resource instance in place.

### 2.7 Node Initialization Standard

Node references are **never exported**. Acquisition always follows this order:

1. Search **Owner**
2. Search **Scene Root**
3. **Log an error**, then create and attach to Owner

```csharp
var animationPlayer =
    FindOnOwner<AnimationPlayer>()
    ?? FindOnRoot<AnimationPlayer>()
    ?? LogMissingAndCreate<AnimationPlayer>();
```

### 2.8 Signal Architecture

Signals are connected **exclusively through code** — never wired in the editor.

Each entity category gets a static signal registry:

```csharp
PlayerSignals
EnemySignals
PropSignals
BossSignals
```

Signal registration happens during startup/initialization — never lazily on first use.

Each entity category gets its own static signal registry class. There is **no catch-all shared class**.

---

## 3. System Architecture

This section defines **how the Key Systems are designed**: their responsibilities, boundaries, and how they relate to one another. Code-level conventions (casing, exports, null checks) are covered in [§2 Architecture](https://claude.ai/chat/2a16c8f3-f9fd-49bd-8b14-a94eaa9593ee#2-architecture) and apply uniformly across every system below.

### 3.1 Core Design Philosophy

The project uses a **component-based Entity System** paired with **State Machines** for behavior management:

#### 3.1.1 Entity

Base class, all dynamic objects inherit from (Player, Enemy, NPCs).

#### 3.1.2 Behaviors

Modular systems attached to entities.

#### 3.1.3 State Machine

Controls state transitions (Idle, Moving, Chasing, Attacking, Dodging, Dead, etc.)

### 3.2 Entity System

**Location:** `#Scripts/Entities/`

`Entity.cs` is the base class for all dynamic game objects: **Player, Enemy, NPC, Boss, Prop**, and future dynamic entity types.

Every entity, by contract:

- Supports **Weight**
- Participates in the **knockback system**
- Participates in the future **Z Axis system**

Weight feeds both knockback calculations and gravity calculations. Z Axis simulation is a **core Entity responsibility**, never a behavior.

#### EntityStats Resource

Per-entity baseline stats (Weight, base Tenacity, future Z Axis values) are authored as a shared `EntityStats` **Resource**, rather than hardcoded per-script fields. This lets design iterate on balance entirely inside `.tres` assets, and lets multiple entities of the same archetype (e.g. several Enemy variants) reuse or override a common baseline.

```csharp
[GlobalClass]
public partial class EntityStats : Resource
{
    [Export] public float Weight = 1f;
    [Export] public bool UseKnockback = true;
    [Export] public bool UseTenacity = true;
    [Export] public float Tenacity = 100f;
}
```

```csharp
[Export] public EntityStats Stats;
```

The Entity reads from `Stats` at initialization; it never mutates the Resource instance at runtime.

#### Z Axis Development

Currently under active development. Future responsibilities include:

- Jumping
- Falling
- Airborne states
- Elevation
- Gravity
- Weight-influenced vertical movement

### 3.3 Behavior System

**Location:** `#Scripts/Entities/Behaviors/`

Modular behaviors are attached to entities via `AddBehavior()`.

> **Important:** Z Axis and Weight are _not_ behaviors. Core impact resolution stays at the entity level, not the behavior level.

### 3.4 Impact Architecture

A unified `ImpactBehavior` handles all incoming physical reactions:

- Knockback calculation
- Weight handling
- Tenacity handling
- Stagger handling
- Impact state transitions
- Knockback decay

Every entity supports **Weight**, which feeds into:

- Knockback resistance
- Future Z Axis gravity calculations
- Future airborne interactions

#### Knockback

Optional per entity:

```csharp
public bool useKnockback = true;
```

Entities with knockback disabled completely ignore displacement effects (e.g. **walls, large static structures, certain environmental objects**).

#### Tenacity

Optional per entity:

```csharp
public bool useTenacity = true;
public float tenacity = 100f;
```

Tenacity governs resistance to interruption and stagger. Entities with tenacity disabled skip all stagger resistance calculations (e.g. **crates, barrels, destructible props, environmental objects**).

#### Configuration Reference

These values now live in `EntityStats` Resources (see [§3.2](https://claude.ai/chat/2a16c8f3-f9fd-49bd-8b14-a94eaa9593ee#entitystats-resource)) rather than hardcoded fields — the table below shows the data each archetype's `.tres` asset carries:

|Entity|Weight|Knockback|Tenacity|
|---|---|---|---|
|Player|`1f`|✅ `true`|✅ `true` (`100f`)|
|Enemy|`2f`|✅ `true`|✅ `true` (`75f`)|
|Breakable Crate|`5f`|✅ `true`|❌ `false`|
|Wall|`9999f`|❌ `false`|❌ `false`|

#### Impact Flow

```text
Incoming Hit
  → Calculate Force
  → Apply Weight
  → Resolve Tenacity
  → Determine Stagger
  → Update StateMachine
  → Apply Knockback
```

#### Health/Damage Flow

1. Weapon attack triggers **Hurtbox**
2. Hurtbox calls `Entity.TakeDamage()`
3. Entity resolves the **Impact System**
4. **Weight** and **Tenacity** are evaluated
5. **StateMachine** determines stagger or knockback states
6. Movement displacement is applied

### 3.5 Soul Weapon Arc System (Resource-Driven)

**Soul Weapon Arcs** (Slashing, Piercing, Smashing — see [§1.2](https://claude.ai/chat/2a16c8f3-f9fd-49bd-8b14-a94eaa9593ee#12-core-combat-identity--the-rend--soul-weapon-arcs)) are authored as `SoulWeaponArc` **Resources**, not hardcoded combat logic. An Arc is pure data — designers can create, balance, and slot new Arcs as `.tres` assets without touching combat code.

```csharp
[GlobalClass]
public partial class SoulWeaponArc : Resource
{
    [Export] public string ArcName;
    [Export] public DamageType PrimaryDamageType;
    [Export] public float BaseEfficiency = 0.95f;
    [Export] public float MatchedEfficiency = 1.30f;
    [Export] public DamageType SpecialMoveDamageType;
    [Export] public PackedScene SpecialMoveEffect;
}
```

The Soul Stone holds a reference to the currently equipped Arc:

```csharp
[Export] public SoulWeaponArc CurrentArc;
```

Equipping a new Arc via **The Rend** is a single reassignment of `CurrentArc` — no branching combat code per archetype. Combat resolution reads `BaseEfficiency`/`MatchedEfficiency` from the Resource to determine the 90–100% vs. 130% damage outcome described in [§1.2](https://claude.ai/chat/2a16c8f3-f9fd-49bd-8b14-a94eaa9593ee#12-core-combat-identity--the-rend--soul-weapon-arcs).

> This is the clearest Resource use case in the project: Arcs have no behavior of their own, are meant to be authored and rebalanced by design, and are reused identically across every Scythe instance.

### Known Arcs

The unlock mechanic for each Arc is called **Rotshaping**, unlocked after defeating Rà'Meska (the first Disciple). The Scythe begins with its native Arc.

| Arc | Primary | Special Move | Special Type | Found |
|---|---|---|---|---|
| Soul Scythe Arc | Slashing | Throws the Scythe | Piercing | Default — Kio's native Arc |
| Soul Hammer Arc | Smashing | Ground-shattering wave | Slashing | `[UNDEFINED]` |
| Soul Sword Arc | Slashing | Heavy spin attack | Piercing | `[UNDEFINED]` |
| Soul Dagger Arc | Piercing | Whirling daggers from Scythe tip | Smashing | `[UNDEFINED]` |
| Soul Spear Arc | Piercing | Lunges forward, Smashes ground on impact | Smashing | `[UNDEFINED]` |
| Soul Malice Arc | Smashing | Scattering windburst, instantly breaks Tenacity | Slashing | `[UNDEFINED]` |

The player is bound to a single Arc until swapped at Maldor Arakhan.

### 3.6 Camera System

The camera is **dynamic** — it responds to actions and interactions happening in the game, rather than passively following the player.

#### CameraFeedback

Handles reactive feedback across the game. It drives camera shake on hitting entities and exposes a **tenacity shatter** response when an entity breaks stagger resistance.

#### CameraFocus

Handles camera guidance during combat. It achieves smooth transitions between a **target-locked** camera and a **player-locked** camera on demand.

The system determines which entity to focus using a combination of `cameraPriority` on each Entity subclass, the entity's current missing health, and the player's pointer position. Focus delegates to the entity with the highest calculated priority. Focus can be deactivated to re-center the player, or shifted to a different entity by moving the cursor over it.

This class also emits a **static signal** specifically for Entities that require spotlight focus. An Entity subscribing to this signal exposes a `CameraFocusProfile` **Resource** — focus settings are typically shared across many instances of the same Entity type (e.g. every Boss of a given category), so they're authored once as a `.tres` asset rather than duplicated per-instance.

```csharp
[GlobalClass]
public partial class CameraFocusProfile : Resource
{
    [Export] public bool UseCameraFocus = true;
    [Export] public float FocusDuration = 1.5f;
    [Export] public float FocusRadius = 250f;
}
```

```csharp
[Export] public CameraFocusProfile FocusProfile;
```

### 3.7 Resources UI

The Resources UI is an Entity UI component providing resource meters. Each Entity subclass has its own distinct display.

#### Player

| Meter | Purpose |
|---|---|
| Health Bar | HP meter. Healing contributed by the Vessel meter. |
| Stamina Bar | Stamina meter. Regenerates automatically after a short hard-cooldown. |
| Corruption Bar | Aetheriac State meter. Spending from it activates the Aetheriac State. |
| Vessel Bar | Healing meter. Fills on each hit; all direct healing resolves through this. |

#### Enemy

| Meter | Purpose |
|---|---|
| Health Bar | HP meter with stage arches. Stage count (0–4 arches) reflects `EnemyLevel`. Stage 6 ONLY appears in the Corrupted Void. |
| Tenacity Bar | Stagger meter beneath the Health bar. Fills as Tenacity is depleted; turns white at capacity, yellow during the open stagger window. |

#### Prop

Health Bar only.

#### Boss

`[UNDEFINED]`

---

### 3.8 Healing System

> For full design and mechanics, see `[[Healing]]` in the Obsidian Vault "Anomaly."

Healing is split into two distinct categories: *Direct* and *Indirect*. These categories do not share a modifier system.

> Direct Healing is extracted through violence. Indirect Healing is absorbed from the environment.

#### Vessel Bar

The Vessel Bar measures residual Soul energy Kio extracts from entities he strikes. His three-Soul construct draws sustenance from acts of violence — the same mechanism that makes him a threat to the Disciples is what keeps him alive.

**Fill:**

| Rule | Value |
|---|---|
| Bar maximum | `100` |
| Hit contribution | `+1` |
| Consecutive hit scaling (no damage taken) | `x = n × 1.3` |

Consecutive scaling resets the moment Kio takes damage. Consuming a Vessel heals Kio for the current **Potency** value.

**Potency** is the flat HP restored on Vessel consumption.

| Source | Effect |
|---|---|
| Base | `[UNDEFINED]` |
| Max Health stat increase (`+5 HP`) | `+0.5 Potency` |
| Amulet | Flat percentage of current Potency |

> Potency is never upgraded directly by a Stat value. It increases ONLY as a consequence of raising Max Health, or through Amulet modifiers.

Amulet modifiers are applied after all other sources. Arc modifiers never affect Potency.

#### Indirect Healing

Indirect Healing originates outside combat — no striking, no momentum, no internal resource required. Sources are environmental. `[UNDEFINED]`

It is not modified by Potency or any Stat. Amulets apply normally. Resolves at a fixed value determined by source.

#### Modifier Authority

| Modifier | Vessel Bar Potency | Aetheriac State | Indirect |
|---|---|---|---|
| Max Health stat | `+0.5` per `+5 HP` | no | no |
| Amulet (flat %) | yes | yes | yes |
| Direct stat upgrades | no | no | no |

---

### 3.9 Aetheriac State

The Aetheriac State is Kio's blood-lust form — introduced in Act I.III. It is entered by spending HP from the Corruption Bar (which functions as the Aetheriac meter). While active, each hit to a living Entity heals Kio directly for a percentage of damage dealt. No Vessel Bar involvement.

> The Aetheriac State does not grant survival. It makes survival contingent on total, uninterrupted destruction.

The State drains HP at an accelerating rate. This drain bypasses damage resolution entirely — it is not classified as damage. If Kio does not maintain attack momentum, the accelerating drain will kill him. He can exit the state voluntarily before that point.

Aetheriac State healing is not modified by Potency. Amulets apply normally.

Stage 6 enemy health bars appear ONLY in the Corrupted Void and are directly tied to this state's encounter design.

---

### 3.10 Stats

Stats govern the meta-/physical parameters of every Entity in Akasa. Split across three scopes:

| Scope | Authority | Savable | Mutable at Runtime |
|---|---|---|---|
| `EntityStats` | Base config for all dynamic objects | no | no |
| `PlayerStats` | Kio's permanent upgrade progression | yes | yes (single owned instance) |
| `WeaponStats` | Scythe upgrade progression | yes | yes (single owned instance) |

`PlayerStats` and `WeaponStats` are saved separately because their upgrade sources are separate. Kio upgrades himself at `[UNDEFINED]`. He upgrades his Scythe at Maldor Arakhan. These two progressions must never write to the same save slot.

#### EntityStats

Universal base config Resource for all dynamic objects. Every entity type authors its own `.tres` asset. Never mutated at runtime.

```csharp
[GlobalClass]
public partial class EntityStats : Resource
{
    [Export] public float MaxHealth = 100f;
    [Export] public float Weight = 1f;
    [Export] public bool UseKnockback = true;
    [Export] public bool UseTenacity = true;
    [Export] public float Tenacity = 100f;
}
```

| Archetype | MaxHealth | Weight | Knockback | Tenacity |
|---|---|---|---|---|
| Player | `100f` | `1f` | ✅ | ✅ `100f` |
| Enemy | `[UNDEFINED]` | `2f` | ✅ | ✅ `75f` |
| Breakable Crate | `[UNDEFINED]` | `5f` | ✅ | ❌ |
| Wall | — | `9999f` | ❌ | ❌ |

#### PlayerStats

Tracks Kio's permanent upgrade counts and last save point for respawn. Stores counts only — final values are derived at point of use.

| Stat | Formula |
|---|---|
| Effective Max Health | `EntityStats.MaxHealth + (MaxHealthUpgrades × 5f)` |
| Vessel Potency | `BasePotency + (MaxHealthUpgrades × 0.5f)` |

`BasePotency` is `[UNDEFINED]`. Amulet modifiers are applied on top at runtime and never written back.

#### WeaponStats

Tracks Scythe upgrade progression. Stores counts only.

| Stat | Formula |
|---|---|
| Effective Damage | `(BaseDamage + (DamageUpgrades × DamagePerUpgrade)) × Arc.BaseEfficiency` |
| Critical Chance | `BaseCriticalChance + (CriticalChanceUpgrades × CriticalChancePerUpgrade)` |
| Critical Multiplier | `BaseCriticalMultiplier + (CriticalMultiplierUpgrades × CriticalMultiplierPerUpgrade)` |

Base values and per-upgrade increments are `[UNDEFINED]`. Arc efficiency multiplies derived damage during combat resolution — not written back to `WeaponStats`.

#### StatType Enums

Allow `AmuletEffect` and `SoulWeaponArc` Resources to reference stat targets programmatically without branching. Amulets reference `PlayerStatType`. Arcs reference `WeaponStatType`. These systems do not cross-modify each other.

#### Modifier Authority

| Source | Targets | Applied | Saved |
|---|---|---|---|
| PlayerStats upgrade | `PlayerStatType` values | permanent | yes |
| Amulet | `PlayerStatType.HealingBonus` | runtime | no |
| WeaponStats upgrade | `WeaponStatType` values | permanent | yes |
| Soul Weapon Arc | `WeaponStatType` values | runtime | no |

---

### 3.11 Save System

The game saves automatically. No manual save exists. All save data is written as Godot `Resource` instances to `user://` via `ResourceSaver` (`.res` binary — no JSON). All Resources are written simultaneously on every save trigger — there is no partial save.

**Location:** `#Scripts/Save/`

#### Trigger Categories

| Trigger | Examples | Changes Respawn Point |
|---|---|---|
| Upgrade | Kio upgrades at `[UNDEFINED]`; upgrades at Maldor Arakhan | yes |
| Canonical event | Disciple defeated; Void Heart destroyed; boss defeated | no |
| Major NPC interaction | Significant quest interactions, first meetings, faction state changes `[UNDEFINED]` | no |

#### What Is Saved

| Domain | Resource |
|---|---|
| Player stat upgrades + respawn point | `PlayerStats` |
| Scythe upgrade counts | `WeaponStats` |
| Arc unlocks, equipped Arc, Amulets, current Act, narrative flags | `ProgressionData` |
| World difficulty level | `DifficultyData` |
| Disciples killed, Void Hearts destroyed, explored regions, NPC flags | `WorldData` |

#### SaveManager

Static utility class. Owns the single mutable instance of each save Resource for the session. All systems access save data through `SaveManager` properties — never by loading Resources directly.

`SaveManager.Load()` is called once at game start. `SaveManager.Save(savePoint)` is called by whichever system triggers the save event.

#### Respawn

On death, Kio respawns at the proximity of the location identified by `PlayerStats.LastSavePoint`. The respawn system resolves position from a scene-registered location map — positions are not stored in the save file.

| LastSavePoint | Respawn Location |
|---|---|
| `SavePoint.None` | `[UNDEFINED]` |
| `SavePoint.PlayerUpgrade` | Proximity of `[UNDEFINED]` upgrade location |
| `SavePoint.Maldor` | Proximity of Maldor Arakhan |

#### String ID Conventions

Save arrays use string IDs to reference Disciples, Void Hearts, regions, Arcs, and Amulets. ID format and naming conventions are `[UNDEFINED]`. A registry mapping IDs to their in-world objects is required. An ID that does not exist in the registry is silently ignored, not an error.

---

### 3.12 Difficulty Scaling

The Difficulty Scaling system governs how the world grows in threat as Kio grows in power. It operates on two levels: a world-level `DifficultyLevel` that persists across sessions, and a per-enemy `EnemyLevel` sampled at initialization that is never saved.

> The world does not reset. Each Void Heart destroyed permanently raises the threshold of what Kio will face.

**Location:** `#Scripts/Systems/DifficultyScalingSystem.cs`

`DifficultyLevel` is the single saved value driving the entire system. It increments by 1 each time a Void Heart is destroyed, capped at 5. It never decreases.

| DifficultyLevel | Trigger |
|---|---|
| `1` | Default — starting state |
| `2–4` | Each Void Heart destroyed (+1 per destruction) |
| `5` | 4th Void Heart destroyed — cap; unlocks Region 5 |

#### Enemy Level Sampling

Each Enemy samples its level once in `_Ready()` and retains it for its lifetime. The level is runtime state — not saved. Higher world difficulty shifts probability mass toward upper levels.

| World Level | L1 | L2 | L3 | L4 | L5 | L6 |
|---|---|---|---|---|---|---|
| 1 | 90% | 10% | — | — | — | — |
| 2 | 30% | 60% | 10% | — | — | — |
| 3 | 10% | 25% | 55% | 10% | — | — |
| 4 | 0% | 5% | 20% | 65% | 10% | — |
| 5 | 0% | 0% | 5% | 10% | 75% | 10% |

Each world difficulty level carries a 10% chance to spawn one enemy level above its tier. Level 6 is exclusive to World 5 and to the Corrupted Void — it is not a baseline expectation at any tier.

#### Stat Multipliers

The sampled `EnemyLevel` applies a flat multiplier to all scalable stats via `DifficultyScalingSystem.ScaleStat()`. Applied at initialization to `EntityStats` base values.

| Enemy Level | Stat Multiplier |
|---|---|
| 1 | `1.00` |
| 2 | `1.20` |
| 3 | `1.45` |
| 4 | `1.75` |
| 5 | `2.10` |
| 6 | `2.50` |

Multipliers are placeholders — balance pass required.

#### Visual Indicator

Enemy level is communicated through the health bar. The health bar asset swaps dynamically based on `EnemyLevel`, displaying 1–5 indicator arches above the bar. No numeric label. Asset definitions and signal routing are `[UNDEFINED]`.

#### Region 5

Optional region unlocked when `DifficultyLevel` reaches 5. The region and its unlock gate are `[UNDEFINED]`.

---

### 3.13 Death

#### Normal Death

Standard Soulslike death loop: Kio dies, leaves Souls at death location, and has the option to retrieve them on return.

#### Death During a Boss

On first death during a boss encounter, Kio is dragged by Valdis' forces into the **Corrupted Void** to reclaim his Soul and power. If Kio dies inside the Corrupted Void, he dies permanently for that run. If he survives, he revives at the spot he originally fell.

Inside the Corrupted Void the player can fight to revive — either by completing vertical-slice challenges or defeating waves of stronger foes.

---

### 3.14 Sacrifice

The playthrough introduces sacrifices and betrayals to add depth within a ruined, post-apocalyptic world. These events add individuality to each playthrough and reinforce the absence of pure moral good in Akasa.

Design is `[UNDEFINED]` — no faction or entity fights for an unambiguous right. Every sacrifice should present an incompatible truth, not a clean moral choice.

---

## 4. Current Focus

> Update this section as priorities shift.

- Z Axis system under active development
- `MovementPhysics`: unified class handling position knockback, tenacity stagger, and Z Axis gravity with weight as the shared variable
- Healing System: finalize data model and align mechanics with lore