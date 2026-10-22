# Corrupted Powers

Status: draft. Numbers are starting values for tuning, not final balance.

## Core rule: the soul decides

A Corrupted Power can only be acquired by the soul it is bound to. Each Soul Arc has one power, and only that arc's soul can teach it.

- **Refinement 5 unlocks the slot.** Refining the Arc to Level 5 opens its power slot.
- **Finding the soul unlocks the power.** The slot stays empty until the soul is found.
- **Soul sources.** A soul can be found as:
  - a corpse of an entity that used the weapon, or
  - a full boss fight. Disciples are true to the Disciples: a Disciple's Arc is found in its region, but the power drops from the Disciple.
- A soul can also be a fight, not only a pickup, so the power is earned rather than bought.

## Summary

Corrupted Powers are ability-focused powers that belong to Soul Arcs. Where an Arc is a physical weapon, its Corrupted Power is something Kio does with Corruption instead: a ranged or area effect, not another melee swing. Each Soul Arc has one Corrupted Power, and every power is activated with **R**.

## Unlock

Three gates, in order:

1. **Second Void Heart.** Rupturing the second Void Heart unlocks the Corrupted Powers system. No power is owned yet.
2. **Arc Level 5.** Refining a Soul Arc to Level 5 (`ArcUpgrades.MAX_LEVEL`) opens that Arc's power slot.
3. **The soul.** Finding the bound soul (see Core rule) grants the power. Each Arc is refined and unlocked on its own.

## Activation

- Key: **R**. Corrupted Powers take R.
- Aetheriac State moves to **Y** (`GameInput.AETHERIAC`).
- Cooldown: **50–70 s** per power, set per power in its Arc resource. Each power has its own cooldown; they don't share one.
- Cost: cooldown only, plus a small Stamina cost. No Corruption is spent.
- After Arc Level 5, the existing per-level cooldown cut (`COOLDOWN_CUT_PER_LEVEL`) applies to the power's cooldown.

## Corruption stat

- Corruption becomes an attunable stat, like Vitality, Might and Resolve. A point of Resonance can be attuned into it at a bonfire.
- Corruption attunement caps at **40** points.
- Leveling up does not grant Corruption. Level-ups grant points to attune, as they do now.
- Corrupted Powers scale with the Corruption stat. Starting values, per attuned point:
  - Power damage +3%
  - Power radius or duration +2%

## Powers

| Arc | Corrupted Power | Effect (draft) | Maldor's line (draft) |
| --- | --- | --- | --- |
| Soul Spear Arc | TBD | TBD | TBD |
| Soul Scythe Arc | TBD | TBD | TBD |
| Soul Hammer Arc | TBD | TBD | TBD |

Each power needs one line from Maldor that is unique to it. He speaks it when the power is granted, and it names what makes that power different from the others.

## Caps

- Corruption attunement: **40** points.
- Weapon cap: **40**.
- Soul Arc level cap: **20** upgrades. Level 5 is still the Corrupted Power gate.

## Pacing

- Target length: **6–10 hours** of gameplay.
- The Arc upgrade curve has to fill 20 levels across that time. Code currently has `ArcUpgrades.MAX_LEVEL = 5`, with `DAMAGE_PER_LEVEL` (4%) and `COOLDOWN_CUT_PER_LEVEL` (6%) tuned for 5 levels. At 20 levels those give about 1.76x damage and a cooldown cut that would reach zero, so they need retuning before the cap changes.
- The soul cost curve (`150 + 120 * (level - 1)`) also needs a pass for 20 levels.

## Open questions

1. None left from the decisions so far. Balance numbers above are for tuning.
