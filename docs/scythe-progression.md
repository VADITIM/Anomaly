# Scythe progression

Status: design, not implemented. The Scythe is Kio's only weapon. Progression comes from how the Scythe is used, not from new weapons.

## Lore

Kio uses only the Scythe. He bends it with Corruption, beyond what the blade can do alone. Every new capability is the same blade doing something new, never a different weapon.

## Five levers

1. **Tree depth on the same string.** The skill tree's Rage branch adds the 4th and 5th strikes and the charged arts (Rising Reap, Whirlwind, Cataclysm). Each one changes how the existing string is used. The string shape doesn't change. The tree's Dexterity and Corruption branches change health, stamina, movement and Corruption (`docs/skill-tree.md`).
2. **Outside techniques, the Corrupted Powers.** Powers are the variety that lies outside the blade's normal reach. They are unlocked by soul and cooldown-based, and they don't need new strike sets (`docs/corrupted-powers.md`).
3. **Context changes the same weapon.** Air, dodge and the charged windup change how the Scythe behaves depending on state. Air is its own implementation with its own combos and branches, and a later air-to-ground combo will read the air state (`docs/combat-revamp-plan.md`).
4. **Arcs as manifestations.** An Arc changes how the Scythe manifests: colour, impact, a finisher. The string shape stays the same. The current code already works this way: the Arc rewrites the interval strike's impact.
5. **Stats and artifacts.** Corruption attunement, worn artifacts and stat bonuses change damage, reach and bonuses, so the same moves feel different as Kio grows (`docs/artifacts.md`, `docs/corrupted-powers.md`). The Forge also upgrades the Scythe's damage and the Arcs. It is separate from the tree.

## Rules

- **Every new capability reshapes a move the player already uses.** A tree member should change the feel of an existing strike, state or power. Isolated extras that don't touch existing play should be avoided.
- **The tree carries the sense of getting better.** Progression comes from the tree, Corruption and artifacts together, not from a new weapon.
- **No new strike sets per upgrade.** Strike profiles are data on the existing string, plus the air and dodge branches already planned.
- **Arcs change manifestation, not the moveset.** A new Arc adds a finisher or impact look, not a new string.

## Open questions

1. Which existing strike should each new tree member touch? (Suggested: 4th and 5th strikes for the string, air for airborne, dodge for the dodge strike.)
2. Does an Arc change any strike data (for example, the interval strike's damage type), or only its look and finisher?
3. Are there any moves a player should be able to lose or swap later, or does progression only add?

## Links

- `docs/combat-revamp.md`, `docs/combat-revamp-plan.md`: combo, charged, air and dodge work.
- `docs/corrupted-powers.md`: outside techniques and Arc levels.
- `docs/artifacts.md`: stat boosts and soul bonuses.
- `docs/skill-tree.md`: the three branches and how the members map to them.
- `docs/kio-weapon-offhand.md`: how the Scythe is held.
