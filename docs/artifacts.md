# Artifacts

Status: design, not implemented. Amulets are renamed to Artifacts. The rename is a refactor and ships as its own step, separate from the feature work below (see `CLAUDE.md`, Refactoring).

## What an artifact is

An artifact is worn in one of the four slots beside the status screen. It does two things:

1. **Stat boost.** Always on while worn. This is what amulets do today: each one bends one or two of Kio's numbers by a share of their base. Nothing changes here.
2. **Soul bonus.** A special effect that unlocks when the artifact's soul is retained. This is new.

## Soul bonus, the same principle as Corrupted Powers

Corrupted Powers (`docs/corrupted-powers.md`) are bound to a Soul Arc and unlock in order: the second Void Heart, Arc Level 5, then the soul. Artifacts follow the same pattern:

- **Each artifact has one bound soul.** The soul is the one that used the artifact. Only that soul's bonus belongs to that artifact.
- **Soul sources.** The soul is found as a corpse or a full boss fight, as in `docs/corrupted-powers.md`. A Disciple's artifact is found in its region, and the bonus drops from the Disciple.
- **Retaining the soul unlocks the bonus.** Once retained, the bonus stays unlocked in the save.
- **The bonus is an effect, not a number.** Stat boosts scale numbers. Soul bonuses change how a moment plays, for example a devastating blow on a dodge during a phase.

## Example: devastating blow on phase dodge

The idea from the design: dodging during a phase that meets the requirement unveils a devastating blow. In terms of the combat revamp (`docs/combat-revamp-plan.md`):

- Trigger: a dodge (Dash) that lands during the enemy's phase requirement. Use the existing perfect dodge window as the base, or a phase tag on the enemy.
- Effect: a single large strike, tuned as a dodge-strike variant (Step 3 of the combat plan).
- Limit: once per phase, so it stays a reward and doesn't become a loop.

Open detail: "each dodge on phase requirements" is read here as "a dodge that satisfies a phase requirement". If it means something else, the trigger changes but the structure doesn't.

## Data shape

Today `AmuletInfo` has `Id`, `Name`, `Line`, `Color` and `Stats`. Soul bonuses add:

- `SoulBonusId`: which bonus this is, a string key into a bonus table.
- A trigger and effect, defined in the bonus table, not per artifact.

Stat bonuses keep working as they do now. Soul bonuses read from the same progression save (`ProgressionData`), next to the artifact inventory.

## Rules

- **Stat boost:** while worn.
- **Soul bonus unlock:** permanent once the soul is retained.
- **Soul bonus active:** open question (see below). Recommendation: active only while worn, so the slot still matters.
- **Caps:** four slots, as now. No stacking limit planned yet.

## Rename plan

Refactor step, no behaviour change, compile after it:

- Classes: `Amulets` → `Artifacts`, `AmuletInfo` → `ArtifactInfo`, `AmuletStat` → `ArtifactStat`, `AmuletIcon` → `ArtifactIcon`, `AmuletWindow` → `ArtifactWindow`.
- Scenes and scripts: `#Scenes/AmuletWindow.tscn` → `#Scenes/ArtifactWindow.tscn`, with the script renamed to match (Godot needs the file name to match the class).
- Save fields: `ProgressionData.AmuletInventory` and `EquippedAmulets` are saved by name. Renaming them needs a load fallback that reads the old names, otherwise existing saves lose their artifacts.
- UI text: "AMULET SLOT 1" and "PRESS AN AMULET TO WEAR IT" become Artifact wording.
- Debug: `Amulets.GrantDebug` becomes `Artifacts.GrantDebug`. The five debug amulets stay as the debug list.

Call sites: `Player.cs`, `Player.Combat.cs`, `Player.Damage.cs`, `Player.Resources.cs`, `StatusMenu.cs`, and `ProgressionData.cs`.

## Open questions

1. **Soul bonus active:** only while the artifact is worn, or always once unlocked? (Recommendation: while worn.)
2. **Retention:** is the soul "retained" by finding it, or does the player do something with it (for example at a bonfire)?
3. **Phase dodge trigger:** does "phase requirement" mean the enemy's phase, a specific phase tag, or a Kio timing window?
4. **Soul source for artifacts:** the same corpse and boss pattern as Corrupted Powers, or only bosses?
5. **Save migration:** keep the old field names and map them, or rename and migrate on load? (Recommendation: migrate on load and keep one fallback.)
