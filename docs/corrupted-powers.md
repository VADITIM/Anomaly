# Corrupted Powers

Status: draft. Numbers are starting values for tuning, not final balance.

## Summary

Corrupted Powers are ability-focused powers that belong to Soul Arcs. Where an Arc is a physical weapon, its Corrupted Power is something Kio does with Corruption instead: a ranged or area effect, not another melee swing. Each Soul Arc has one Corrupted Power, and every power is activated with **R**.

## Unlock

Two gates, in order:

1. **Second Void Heart.** Rupturing the second Void Heart unlocks the Corrupted Powers system. No power is owned yet.
2. **Arc Level 5.** Refining a Soul Arc to Level 5 (`ArcUpgrades.MAX_LEVEL`) grants that Arc's Corrupted Power. Each Arc is refined and unlocks on its own.

## Activation

- Key: **R**. This is the same key the Aetheriac State uses today (`GameInput.AETHERIAC`). See open question 1.
- Cooldown: **50–70 s** per power. Each power has its own cooldown; they don't share one.
- Cooldown is set per power in its Arc resource, within the 50–70 s band.

## Corruption stat

- Corruption becomes an attunable stat, like Vitality, Might and Resolve. A point of Resonance can be attuned into it at a bonfire.
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

## Open questions

1. **R is taken.** The Aetheriac State uses R. Either the Aetheriac key moves, or the powers share R and the game chooses by state. Decide before implementing.
2. **Cost.** Does activating a power spend Corruption, Stamina, or nothing (cooldown only)?
3. **Arc cooldown cut.** Does the existing per-level cooldown cut (`COOLDOWN_CUT_PER_LEVEL`) also apply to the Corrupted Power's cooldown?
4. **Corruption attunement cap.** What is the maximum number of points, and does it match the 40-point Aspect fill used in the status menu?
