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
- **Keep the old tree.** No existing member is retired or rewritten. Rising Reap, Juggle, Sky Rhythm, Meteor Slam, Momentum, Echo, Whirlwind and Cataclysm stay as they are. New arts are appended after `Cataclysm`, with new members at the end of the enum and `All`.

## Step 0: refactor, no behaviour change (optional)

Split `Player.Combat.cs` (681 lines) so the new work has somewhere to go:

- `Player.Strings.cs`: the `COMBO` table and `StrikeKind`, with the air string table added later.
- `Player.Air.cs`: `UpdateAirDescent` and the air-only constants.

Only moves. Compile. Play nothing; the change is structural.

## Step 1: charged arts get a timed windup

Scope: the charged art timing from `docs/combat-revamp.md`. The `COMBO` table does not change. Charged arts are branches off the combo path, not replacements for its hits.

- Keep the existing charged arts and their tree members: Rising Reap (hit 1), Whirlwind (hit 3), Cataclysm (hit 5), Meteor Slam (air).
- Replace the hold-to-charge timing in `Player.Skills.cs` (`EnterCharge`, `UpdateCharge`, `ReleaseCharge`, `ChargeFx`, `CHARGE_FULL_TIME`) with a fixed windup of 0.4 s per charged art. The charged art is not gated by how long Strike is held.
- Release timing:
  - released at about 0.3 s into the windup: perfect, 100% damage;
  - released at any other time before 0.4 s: 90% damage;
  - still held at 0.4 s: fires at 90% automatically.
- The 90% applies to the whole charged hit, not only its extra part.
- Put the perfect window in one constant (`CHARGED_PERFECT_TIME = 0.3f`, `CHARGED_PERFECT_TOLERANCE`) so playtests can tune it.
- Charged damage multipliers live with the hit data (`AttackProfile.DamageMultiplier`), not in input code.

## Step 2: air string split

Scope: air attacks become a 2-hit slice combo. Base only, no tree yet.

- In `EnterAttack`, when not grounded, use `AIR_COMBO` (two slices) and an air string counter, separate from the ground `comboStep`.
- Air attacks get a whole new implementation, not a patch on the ground string. The Slam drift, the grind-in-air behaviour and `UpdateAirDescent` are removed from the air path. Keep the ground behaviour unchanged.
- The air string exposes its state (hits so far, height, fall speed, momentum) on the player, so the future air-to-ground combo can read it.
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

- **Dodge jump:** a buffered Jump launches when the dash ends, carrying the dash's momentum. Gated by `CombatSkill` (new member, appended).
- **Air dodge:** `CanDash()` also allows dash while `Falling` when the skill is learned. One air dash per airtime; it resets on landing (`OnLanded`).
- Both reuse `EnterDash` and the existing jump code. No new movement state.

## Step 5: append the new tree members

Scope: new arts added to the existing tree. Nothing already in it is removed or moved.

| Area | Existing members | New members (appended) |
| --- | --- | --- |
| Ground combo | 4th Strike, 5th Strike, Momentum, Echo | none |
| Charged arts | Rising Reap, Whirlwind, Cataclysm | none; the timing change in Step 1 applies to these |
| Air | Juggle, Sky Rhythm, Meteor Slam | air 2-hit slice base, 4-hit air string, airborne damage |
| Dodge | none | dodge strike, dodge jump, air dodge |

- New members go at the end of `CombatSkill` and at the end of `All`, in the order listed above.
- `Requires` points at an existing member where the new art follows from it (for example, the dodge strike after 5th Strike), so the new arts are reachable without a new branch layout.
- The branch layout around the ring is decided during the Step 5 work, after the new members exist.

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

## Decisions

- **Combo table unchanged.** The hit combos stay as they are. Charged arts branch off the original combo path.
- **Tree is append-only.** Old members stay, new ones are appended. No retirement.
- **Charged damage.** Non-perfect release does 90% to the whole charged hit.
- **Perfect window.** Read as a band around 0.3 s, not an exact frame. The answer to question 2 was "yes", which this plan takes as the band. Confirm if it meant something else.
- **Dash into jump.** The jump launches at the end of the dash, not during it.
- **Dodge is provisional.** The dodge is too strong right now. It will be refactored and polished later, toward the Enter the Gungeon feel: a committed roll with clear i-frames and a recovery cost. The dodge steps (3, 4) ship as a base that will change, so keep their numbers in one place.
- **Air attacks are their own implementation.** Air attacks get a whole new implementation, with their own combos and their own branched combos in the tree. The 2-hit air slice is only the base of that branch.

## Future: air to ground

A later feature: a combo performed in the air converts the air momentum and state into a new ground combo. This is not in the current steps. It affects Step 2 only in that the air string needs to expose its state (height, momentum, hits so far) so a later landing combo can read it.

- **Air dodge resets on landing only.** One air dodge per jump. A landed hit does not refresh it. Revisit with the dodge rework.

## Open questions

1. **Air string in the tree.** Is Meteor Slam still the air charge in the new air branch, or does the air branch get its own charged art?
