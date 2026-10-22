# Refactor plan

Status: plan, not started. This is the handover for the next agent. It lists the refactor steps that prepare the design work, in order. Follow `docs/refactoring.md` for every step. Keep each step behaviour-neutral and separate from feature changes (`CLAUDE.md`, Refactoring).

## Ground rules

- **One step per change.** Each step compiles on its own: `dotnet build Anomaly.csproj -nologo -v q -clp:ErrorsOnly`.
- **Behaviour stays the same.** No balance, timing or input changes inside a refactor. Those are feature changes with their own commits.
- **Saves load.** Every serialised type or field keeps working for existing saves. Renames need a load fallback.
- **Never rename a saved skill.** `CombatSkill` members are stored by name. Append only.
- **Keep the root position of entity scenes.** Visual nudges go on children (`CLAUDE.md`, Entity scenes).
- **Stop hook.** The container needs `dotnet-sdk-8.0` installed (`apt-get install -y dotnet-sdk-8.0`). It isn't in the setup script yet, so check it on every new container.

## Where things are

| Area | File | Notes |
| --- | --- | --- |
| Combat strikes and strings | `#Scripts/Entities/Player/Player.Combat.cs` (681 lines) | `COMBO` table, `AttackProfile`, `EnterAttack`, `UpdateAttack`, air descent, grind |
| Charged arts | `#Scripts/Entities/Player/Player.Skills.cs` (405 lines) | `EnterCharge`, `UpdateCharge`, `ReleaseCharge`, `ChargeFx`, `CHARGE_*` constants, Rising Reap, Whirlwind, Cataclysm, Meteor Slam |
| Dash and jump | `#Scripts/Entities/Player/Player.cs` | `EnterDash`, `UpdateDash`, `CanDash`, `Jump`, `UpdateLocomotion` |
| Perfect dodge | `#Scripts/Entities/Player/Player.Damage.cs` | `PERFECT_DODGE_WINDOW`, `PerformPerfectDodge` |
| Skill tree data | `#Scripts/Systems/CombatSkills.cs` | `CombatSkill` enum, `CombatSkillInfo` record, `All` table, `Has`, `Learn`, `Unlockable` |
| Amulets | `#Scripts/Systems/Amulets.cs` | `AmuletStat`, `AmuletInfo`, `Amulets` class, five debug amulets |
| Amulet UI | `#Scripts/UI/AmuletIcon.cs`, `#Scripts/UI/Menus/AmuletWindow.cs`, `#Scenes/AmuletWindow.tscn` | window text reads "AMULET" |
| Amulet hooks | `Player.cs`, `Player.Combat.cs`, `Player.Damage.cs`, `Player.Resources.cs`, `StatusMenu.cs` | `Amulets.Multiplier` calls |
| Save data | `#Scripts/Save/ProgressionData.cs` | `AmuletInventory`, `EquippedAmulets` (saved by name) |
| Input | `#Scripts/Core/GameInput.cs` | `AETHERIAC` bound to `Key.J`? (check), `ATTACK` to `Key.J`, `CAMERA_UNLOCK` |
| Player animation | `#Scripts/Entities/Player/PlayerAnimator.cs` | `Kio_Body.png`, `Kio_Weapon.png`, `WEAPON_LAYER_ENABLED = false` |
| Scythe node | `#Scripts/Weapon/TelekineticScythe.cs` | floating orbit, `LINGER_TIME`, `ORBIT_RADIUS`, `STRIKES` table |

Confirm the `AETHERIAC` key before any input work. The design says Aetheriac moves to Y, but the file's current binding was not read in full.

## Steps

### Step 1: Amulets → Artifacts (rename only)

Scope: rename, no behaviour change. Design: `docs/artifacts.md`, "Rename plan".

- Classes: `Amulets` → `Artifacts`, `AmuletInfo` → `ArtifactInfo`, `AmuletStat` → `ArtifactStat`, `AmuletIcon` → `ArtifactIcon`, `AmuletWindow` → `ArtifactWindow`.
- Scene and scripts: `#Scenes/AmuletWindow.tscn` → `#Scenes/ArtifactWindow.tscn`. The script file must match the class name.
- Save fields: keep reading `AmuletInventory` and `EquippedAmulets`, and write the new names. Add one fallback on load so old saves keep their artifacts.
- UI text: "AMULET" → "ARTIFACT" in the window's labels only. Do not change the words "wear" and "slot" behaviour.
- Check: old save loads and shows the same artifacts equipped; compile.

Keep this step separate from any soul-bonus work.

### Step 2: split `Player.Combat.cs`

Scope: move code, no behaviour change. Design: `docs/combat-revamp-plan.md`, Step 0.

- `Player.Strings.cs`: `StrikeKind`, `AttackProfile`, the `COMBO` table, `ComboLength`, and the strike accessors (`CurrentAttack`, `StrikeTime`, `RecoverTime`, `CancelTime`).
- `Player.Air.cs`: `UpdateAirDescent`, air constants (`AIR_DESCENT_*`, `AIR_HIT_LINGER`), `airHitTimer`.
- Keep `EnterAttack`, `UpdateAttack`, `ExitAttack` in `Player.Combat.cs`.
- Check: no `const` or `static readonly` value changes; compile.

### Step 3: split charged arts out of `Player.Skills.cs`

Scope: move code, no behaviour change.

- `Player.Charge.cs`: `EnterCharge`, `UpdateCharge`, `ReleaseCharge`, `ChargeFx`, `ChargedMove`, `ChargeableMove`, `UpdateHold`, the charge constants.
- `Player.Skills.cs` keeps the per-art effects (Rising Reap, Whirlwind, Cataclysm, Meteor Slam, Echo, Momentum helpers) until they are needed separately.
- Check: no constant values change; compile.

This step makes Step 4 a single-file change.

### Step 4: charged windup (feature, not refactor)

Scope: behaviour change. Keep it in its own commit and label it as a feature. Design: `docs/combat-revamp-plan.md`, Step 1 and "Decisions".

- Replace hold-to-charge timing with a fixed 0.4 s windup per charged art.
- Perfect band around 0.3 s, one constant (`CHARGED_PERFECT_TIME`, `CHARGED_PERFECT_TOLERANCE`).
- Non-perfect release does 90% to the whole charged hit. A full hold fires at 90%.
- The `COMBO` table does not change.

### Step 5: skill tree branch field (data, no behaviour change)

Scope: add the branch field without changing any member. Design: `docs/skill-tree.md`.

- Add `Branch` values for Rage, Dexterity and Corruption, and map each existing member to one (table in `docs/skill-tree.md`).
- Keep the existing `Branch` int and ring layout working until the tree UI reads the new field. Remove the old field only after the last caller moves.
- Do not rename or remove any member. Do not reorder `All`.
- Check: a save with every existing skill loads and shows the same learned set.

### Step 6: air string (feature)

Scope: new air implementation, separate from the ground string. Design: `docs/combat-revamp-plan.md`, Step 2.

- `AIR_COMBO` (two slices) with its own counter.
- Remove the Slam drift and air grind from the air path. Keep the ground behaviour.
- The air string exposes hits so far, height, fall speed and momentum on the player, for the later air-to-ground combo.
- Confirm how `Falling` enters attacks first (`Player.cs`, `EnterFalling`).

### Step 7: dodge strike and dodge movement (feature)

Scope: design: `docs/combat-revamp-plan.md`, Steps 3 and 4.

- Dodge strike from `UpdateDash` after 60% of the dash. It does not advance `comboStep` and does not use `perfectDodgeAvailable`.
- Dodge jump: a buffered jump launches at the end of the dash, carrying momentum.
- Air dodge: one per jump, reset on landing only (`OnLanded`).
- Keep the numbers in one place. The dodge is provisional and will be reworked toward a committed roll with i-frames.

### Step 8: tree members (feature)

Scope: append new members to `CombatSkill` and `All`, in the order of `docs/combat-revamp-plan.md`, Step 5. No existing member is changed.

- Rage: air 2-hit slice base, 4-hit air string, airborne damage, dodge strike.
- Dexterity: dodge jump, air dodge.
- Corruption: Aetheriac sub-branch, main bonuses (undefined, leave them out until designed).

### Step 9: artifact soul bonuses (feature)

Scope: design: `docs/artifacts.md`. Only after Step 1.

- Add `SoulBonusId` to `ArtifactInfo` and a bonus table keyed by it.
- Stat boosts stay unchanged.
- Open questions in `docs/artifacts.md` must be answered first (active while worn, retention, trigger, soul source, save migration).

### Step 10: Corrupted Powers (feature)

Scope: design: `docs/corrupted-powers.md`. Only after the Arc levels and Corruption stat are defined.

- R key for powers; Aetheriac to Y (`GameInput.AETHERIAC`). Confirm the current binding first.
- Power slot per Arc, unlocked at Arc Level 5, bound soul, cooldown 50–70 s per power.
- Corruption becomes an attunable stat, cap 40. Level-ups don't grant Corruption.
- The Powers table in the doc is still TBD for Spear, Scythe and Hammer.

### Step 11: Void and pacing (later)

Scope: `docs/corrupted-void.md` and `docs/pacing.md`. Do not start until the core combat and progression are stable.

- `#Scenes/Void/<Region>Void.tscn`, `VoidAnchor` entity scene.
- Void spokes, teleport graph in `WorldData`, void page on `MapData`.
- Arc curve retuning for 20 levels (`ArcUpgrades.MAX_LEVEL` is 5 in code).

## Dependencies

1. Step 1 is independent. It can go first.
2. Step 2 and Step 3 are independent of each other. Both should come before Step 6.
3. Step 4 needs Step 3.
4. Step 5 can go any time after Step 1.
5. Step 6 needs Step 2.
6. Step 7 needs Step 6 for the dodge branch's air dodge reset (landing), and Step 4 for the perfect dodge check.
7. Step 8 needs Steps 5, 6 and 7.
8. Step 9 needs Step 1 and open answers in `docs/artifacts.md`.
9. Step 10 needs Steps 5 and 8, and the Arc level and Corruption design.
10. Step 11 needs everything above.

## Open decisions that block steps

- **Air branch Meteor Slam.** Is Meteor Slam still the air charge, or does the air branch get its own charged art? (Blocks Step 6.)
- **Sky Rhythm branch.** Dexterity or Rage? (Blocks Step 8 mapping.)
- **Aetheriac key.** Confirm Y is free and the current binding before Step 10.
- **Artifact soul bonus.** Active while worn or always once unlocked; retention; trigger; soul source; save migration. (Blocks Step 9.)
- **Corruption branch bonuses.** Undefined. (Blocks Step 8 Corruption members.)

## Verification per step

- Build passes with no new warnings in the files touched.
- Save load check for any step that touches serialised data (Steps 1, 5, 8, 9, 10).
- Code path walk in the same order the game runs it: spawn, update, interact, save and load.
- No visual check unless the user asks. `tools/autopilot.ps1` is only for explicit requests. Never launch Godot directly.

## Summary for the user, per step

- Files moved or renamed, callers updated, anything left on the old path on purpose.
- What was verified, and what was not (playtest-only items).
