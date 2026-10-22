# Combat revamp: implementation plan

Status: plan, not started. Design is in `docs/combat-revamp.md`. This file maps it onto the current code and orders the work. `CLAUDE.md` rule: refactors and feature changes ship as separate steps, and each step compiles on its own.

## What the code does today

- **One string for everything.** `Player.Combat.cs` has a single `COMBO` table of five `AttackProfile`s. Air attacks reuse it. Air behaviour is layered on top: `UpdateAirDescent`, `AirHitLinger`, the Slam drift, and the Sky Rhythm constants.
- **String length from the tree.** `ComboLength` is 3, then 4 with `FourthStrike`, then 5 with `FifthStrike`. `FourthStrike` is the grind and `FifthStrike` the slam.
- **Charged arts are hold-to-charge.** `Player.Skills.cs` holds Strike through a strike (`CHARGE_HOLD_TIME`, `CHARGE_FULL_TIME` 0.4 s, `CHARGE_MAX_TIME` 1.2 s), then releases. `ChargeableMove()` maps hit 1 to Rising Reap, hit 3 to Whirlwind, hit 5 to Cataclysm, and air to Meteor Slam.
- **Dash.** `EnterDash` / `UpdateDash` in `Player.cs`. `CanDash()` only allows grounded or during an attack. Dash can cancel into an attack from 60% of the dash. A dash in the first window grants a perfect dodge (`Player.Damage.cs`, `PERFECT_DODGE_WINDOW`).
- **Jump.** `Jump()` runs only from `UpdateLocomotion`, only when grounded. Nothing jumps out of a dash.
- **Skill save format.** `CombatSkills.Has` stores each skill by `ToString()` name in `SaveManager.PlayerStats.Skills`. `Info()` indexes `All` by enum value.

## Save and enum rules (affects every step that touches the tree)

- **Never rename an existing `CombatSkill` member.** Saves store the name. Renaming silently drops the skill for every existing save.
- **Append, don't insert.** `Info()` uses `All[(int)skill]`. New members go at the end of the enum, in the same position in `All`.
- **Retired skills stay in the enum.** Mark them retired (hidden from the tree, still loaded) instead of deleting them.

## Step 0: refactor, no behaviour change (optional)

Split `Player.Combat.cs` (681 lines) so the new work has somewhere to go:

- `Player.Strings.cs`: the `COMBO` table and `StrikeKind`, with the air string table added later.
- `Player.Air.cs`: `UpdateAirDescent` and the air-only constants.

Only moves. Compile. Play nothing; the change is structural.

## Step 1: charged hit (replaces hold-to-charge)

Scope: the charged third hit from `docs/combat-revamp.md`.

- Remove hold-to-charge from `Player.Skills.cs`: `EnterCharge`, `UpdateCharge`, `ReleaseCharge`, `ChargeFx`, and the charge constants.
- Add a windup on the third hit: when Strike is held through hit 3 it enters a `Windup` phase of 0.4 s.
- Release timing:
  - released at about 0.3 s into the windup: perfect, 100% damage;
  - released at any other time before 0.4 s: 90% damage;
  - still held at 0.4 s: fires at 90% automatically.
- Put the perfect window in one constant (`CHARGED_PERFECT_TIME = 0.3f`, `CHARGED_PERFECT_TOLERANCE`) so playtests can tune it.
- Charged damage multipliers live with the hit data (`AttackProfile.DamageMultiplier`), not in input code.

Tree impact in this step: the charged art on the held hit is whichever one the tree picked. Keep the current three charged arts mapped to hit 3 only, until Step 5 rewrites the tree.

## Step 2: air string split

Scope: air attacks become a 2-hit slice combo. Base only, no tree yet.

- In `EnterAttack`, when not grounded, use `AIR_COMBO` (two slices) and an air string counter, separate from the ground `comboStep`.
- Remove the Slam drift, the grind-in-air behaviour and `UpdateAirDescent` from the air path. Keep the ground behaviour unchanged.
- Confirm how the `Falling` state enters attacks today (`Player.cs` around `EnterFalling`) before changing it. Air attack entry is the first thing to check.
- Stamina per air hit: start from the ground values, tune later.

## Step 3: dodge strike

Scope: a single strike during a dash.

- In `UpdateDash`, a buffered Attack after 60% of the dash starts a `DodgeStrike` profile instead of the ground string. This is not a string hit, so it doesn't advance `comboStep`.
- It is a normal hit with its own `AttackProfile`. Its damage sits between the 1st hit and the perfect dodge reward, to be tuned.
- Keep the perfect dodge separate. The dodge strike doesn't use or consume `perfectDodgeAvailable`.
- Stamina: the dodge strike costs less than a dash.

## Step 4: dodge into jump and air dodge

Scope: the first two tree items in the dodge branch, implemented with a base version and the tree controlling them.

- **Dodge jump:** a buffered Jump during the last part of a dash launches a jump with the dash's momentum. Gated by `CombatSkill` (new member, appended).
- **Air dodge:** `CanDash()` also allows dash while `Falling` when the skill is learned. One air dash per airtime; it resets on landing (`OnLanded`).
- Both reuse `EnterDash` and the existing jump code. No new movement state.

## Step 5: skill tree rewrite

Scope: the tree from `docs/combat-revamp.md`. Four branches around the ring:

| Branch | Base | Notes |
| --- | --- | --- |
| Ground combo | 3 hits | 4th and 5th hits keep their current enum members |
| Charged hit | timed hit 3 | wraps Step 1; the held-hit art choice is open |
| Air | 2-hit slice | new members for air string length and airborne damage |
| Dodge | dodge strike | new members for dodge strike upgrades, dodge jump and air dodge |

Existing members that don't fit (`RisingReap`, `Juggle`, `SkyRhythm`, `MeteorSlam`, `Momentum`, `Echo`, `Whirlwind`, `Cataclysm`) are retired, not deleted, per the save rules above. Any player who learned one keeps the skill in their save, and it's hidden from the tree.

## Dependencies and order

1. Step 0 (optional) makes Steps 2–4 smaller.
2. Step 1 is independent of the air work.
3. Step 2 before Step 5 (the air branch needs the air string).
4. Step 3 before the dodge branch in Step 5.
5. Step 4 before the dodge branch in Step 5.
6. Step 5 last, since it needs every new member to exist.

## Checks per step

- `dotnet build Anomaly.csproj -nologo -v q -clp:ErrorsOnly` after each step.
- Load an existing save after Step 5 and confirm every previously learned skill still shows or is retired, and none is dropped.
- Timing constants (windup, perfect window, dodge threshold) are in one place each, so playtests can tune them without a code search.
- No visual check unless asked. `tools/autopilot.ps1` is only for explicit requests.

## Open questions

1. **Held hit art.** Only hit 3 can be charged in the new design. The tree has three charged arts (Rising Reap on hit 1, Whirlwind on hit 3, Cataclysm on hit 5). Which of them survive, and does the held hit choose between arts, or does the tree pick one?
2. **Perfect window.** The windup is 0.4 s and the perfect release is at 0.3 s. Is the tolerance exact, or a band (for example ±2–3 frames)?
3. **Charged damage on the perfect path.** Is the 90% for non-perfect releases applied to the whole charged hit or only its extra part?
4. **Air string in the tree.** Does the air string grow past two hits only through the tree (the design says up to 4), and does Meteor Slam survive as the air charge?
5. **Dash into jump.** Is the jump launched during the dash, or only after it ends?
6. **Air dodge reset.** Does landing reset it only, or does a hit or a Sky Rhythm-style hang also reset it?
7. **Enum cleanup.** Retire the old members, or keep them visible as tree nodes that do nothing? (Recommendation: retire.)
