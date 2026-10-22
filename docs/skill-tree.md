# Skill tree

Status: design. The tree has three main branches: **Rage**, **Dexterity** and **Corruption**. The Forge is separate (see below).

## Forge is not the tree

- **Forge (Maldor).** Upgrades the Scythe's damage and the Soul Arcs. Nothing else.
- **Skill tree.** Unlocks new combos and techniques for the Scythe, plus health, stamina, movement and Corruption bonuses.

Anything that changes damage or an Arc belongs to the Forge. Anything that adds a move, a state or a bonus belongs to the tree.

## Branches

### Rage: attack patterns

Everything associated with the attack patterns. Strings, charged arts, air combos and the dodge strike.

- 4th Strike, 5th Strike (existing, ground string)
- Rising Reap, Whirlwind, Cataclysm (existing, charged arts)
- Momentum, Echo (existing, damage and finisher on the string)
- Juggle, Meteor Slam (existing, air)
- Air branch: 2-hit slice base, 4-hit air string, airborne damage (new)
- Dodge strike (new, Rage because it is an attack)

### Dexterity: health, stamina, movement

Anything that changes health, stamina or movement.

- Sky Rhythm (existing: cuts air stamina cost and holds Kio aloft longer, so stamina and movement)
- Dodge jump, air dodge (new, movement)
- Any future health, stamina or movement bonuses

### Corruption: undefined bonuses

Bonuses from the Corruption side of the tree. Not yet defined.

- **Aetheriac sub-branch.** A small branch that changes the Aetheriac state (Y key, `docs/corrupted-powers.md`).
- The main Corruption bonuses are still open.

## Mapping the current members

| Member | Branch | Status |
| --- | --- | --- |
| 4th Strike | Rage | existing |
| 5th Strike | Rage | existing |
| Rising Reap | Rage | existing |
| Juggle | Rage | existing |
| Sky Rhythm | Dexterity | existing |
| Meteor Slam | Rage | existing |
| Momentum | Rage | existing |
| Echo | Rage | existing |
| Whirlwind | Rage | existing |
| Cataclysm | Rage | existing |
| Air branch members | Rage | new, appended |
| Dodge strike | Rage | new, appended |
| Dodge jump | Dexterity | new, appended |
| Air dodge | Dexterity | new, appended |
| Aetheriac sub-branch | Corruption | new, undefined |

Existing members are not moved, renamed or removed. The branch names are a layer on top of the save data, so saves keep working (see `docs/combat-revamp-plan.md`, save rules).

## Rules

- **Append only.** New members go at the end of the enum and of the table. Branch is a field on each member, not a reordering.
- **Each member changes an existing move, state or stat.** Avoid isolated extras.
- **Corruption is the stat and the branch.** The Corruption branch's bonuses scale the same Corruption stat as attunement at a bonfire (`docs/corrupted-powers.md`).

## Open questions

1. What are the Corruption branch's main bonuses? (Undefined.)
2. What is the Aetheriac sub-branch's size, and does it change the state or its cost?
3. Does Sky Rhythm stay in Dexterity, or move to Rage since it is an air-string skill?
