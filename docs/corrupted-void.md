# Corrupted Void

Status: draft. Layout numbers are starting points, to be checked against playtime.

## What it is

The Corrupted Void is a separate map for each region. It is not a mirror of the overworld. It has its own layout, enemies and rewards, and it's reached from its region through a rupture or gate.

## Shape

- **Centre: teleport anchors.** The anchors sit close together in the middle of the void, so moving between them is quick. This is the hub.
- **Spokes: content.** Routes expand outward from the hub. Each spoke has encounters, a reward and a way back to the centre, and it takes a few minutes to clear.
- **Outer rings are a little further out.** Anchors spread out a little, so the far spokes take longer to walk, but a return to the hub is still short.
- **Smaller than the overworld.** A void is sized to its time budget, not to the overworld footprint.

## Playtime

- The void has to be more than travel. Every spoke is a route with something to do, and the hub is a place to rest, not the reward.
- Starting budget per region void: roughly 15–25 minutes for a full clear, mandatory and optional together. Check this against the 6–10 hour target in `docs/corrupted-powers.md`.
- Spokes unlock progressively. The inner spokes are open on the first visit. The outer rings need a condition: a Corrupted Power, a Corruption threshold, or a soul. That gives a reason to come back.

## Rewards

- Souls for Soul Arc upgrades (Arc levels up to 20, see `docs/corrupted-powers.md`).
- Corruption attunement points, so the void feeds the Corruption stat.
- Bound souls for Corrupted Powers, placed at the end of certain spokes.
- A shortcut back to the overworld on the far side of a loop.

## Implementation notes

- **Scenes.** One scene per void, `#Scenes/Void/<Region>Void.tscn`. Anchors are entity scenes (`#Scenes/Entities/VoidAnchor.tscn`), and the scene rule in `CLAUDE.md` applies.
- **Placement.** `LevelBlueprint` decides where anchors and spokes go, using the same `PlacementKind` pattern as the overworld.
- **Teleport graph.** Anchors that are unlocked are saved as flags in `WorldData`. Teleporting only moves between unlocked anchors. This is the only fast travel in the void.
- **Map.** `MapData` gets a void page per region, showing anchors and which spokes are open or sealed.
- **Enemies.** Void enemies are their own roster, so `EnemyRoster` streaming and cleared state work the same way as in the overworld.

## Checks

- Walk the hub-to-spoke-to-hub loop once for each spoke and time it. A spoke that clears in under a minute is too short to justify its place.
- If the teleports make most of the void trivial, move the anchors outward or lengthen the spokes.
- Count the rewards per spoke. Every spoke should give something.
