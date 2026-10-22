# Kio's weapon and offhand

Status: design discussion, not implemented. Open questions below need answers before planning.

## Direction

The Telekinetic Scythe is not its own feature. It is the basis of Kio's weapon. Kio interacts with the Scythe directly.

Kio can also hold an item in the left hand, such as a potion, and that item is part of the same system.

## Why this works for art

The Scythe attacks as its own sprite. That means Kio's arm animations can be separate states that respond to the attack state, so the arm art doesn't have to match every Scythe swing frame.

## Planned states

- The arm animations keep their current state machine, driven by the attack state.
- The same arm animations get a new `offhandEquipped` state.
- While `offhandEquipped` is active, the held item overlays the hand, positioned by direction.
- The sprite swaps by state: attack state and offhand state each pick their own frame set.

## Current code, for reference

- `TelekineticScythe.cs` is a standalone node. Its header says "Kio never holds the Scythe", it floats at the shoulder, manifests on the first strike, and vanishes after `LINGER_TIME`.
- `PlayerAnimator.cs` already builds a held weapon layer (`Kio_Weapon.png`, `PlayerWeapon.tres`), parked behind `WEAPON_LAYER_ENABLED = false`.
- Body and weapon sheets share one frame layout (14 × 13), and each has its own `AnimationPlayer` using the same clip name.

These parts fit the planned design. The floating presentation would change to held, and the parked layer is the likely place for the arm and item work.

## Open questions

1. Does the Scythe still float anywhere, such as thrown mode or out of combat, or does it become always held? If always held, the orbit, linger and withdraw logic can be removed.
2. Is the offhand one slot for any item, or specific items (potions Kio can drink)?
3. Are the arm animations on `Kio_Body.png` or `Kio_Weapon.png`? That decides which sheet gets the `offhandEquipped` variants.
