# Corrupted Void

Status: draft. Layout numbers are starting points, to be checked against playtime.

## What it is

The Corrupted Void is a separate map for each region. It is not a mirror of the overworld. It has its own layout, enemies and rewards, and it's reached from its region through a rupture or gate.

Each region's void is a smaller subregion. Together they form one connected Corrupted Void, which is the only fast travel between regions (see **Connected void**).

## Hollow Reach

Hollow Reach is the central hub of the overworld, in Verdanya. It's the most broken land in Akasa, so Void is everywhere in it: corruption, danger and pits to the Void. It connects to the other regions through Fractures.

## Shape

- **Centre: teleport anchors.** The anchors sit close together in the middle of the void, so moving between them is quick. This is the hub.
- **Spokes: content.** Routes expand outward from the hub. Each spoke has encounters, a reward and a way back to the centre, and it takes a few minutes to clear.
- **Outer rings are a little further out.** Anchors spread out a little, so the far spokes take longer to walk, but a return to the hub is still short.
- **Smaller than the overworld.** A void is sized to its time budget, not to the overworld footprint.

## Connected void

- **One region, many subregions.** Every region's void is a subregion of one large Corrupted Void. Players can cross from one region's void to another's.
- **Void rifts.** Once a region's Void Heart is destroyed, void rifts open inside that region's void. Rifts link to other regions' voids, so travel between regions is quick, but only from inside the void. They're the only fast travel in the game.
- **Valdis's final section is in the void.** It's locked behind four Void Hearts and sits inside the connected Corrupted Void, not on any continent.
- **Elaphrium unlocks after the fourth Void Heart,** whichever four the player has destroyed.

## Void Heart powers

- Each Void Heart grants Kio one power when it's destroyed. The set of powers is the same whichever order the hearts are cleared in.
- Some areas of a region are reached only with two, three or four Void Heart powers. Those gates are part of the region's layout, so the player can't reach them early.
- Powers are TBD. They're separate from the Arc powers in `docs/corrupted-powers.md`, which unlock at Arc Refinement 5 and from the soul.
- **Rifts are unlocked, not free.** A rift only links to regions whose Void Heart the player has destroyed. Until then, the route between regions runs through the overworld.

## Difficulty

- The Act I tutorial void is the easy one. After it, void enemies are harder than the overworld enemies of the same region, by default **1–2 world levels higher**. The void is meant to feel dangerous.
- Rewards scale with the extra danger, so the harder fights pay for themselves.

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
- **Teleport graph.** Anchors that are unlocked are saved as flags in `WorldData`. Teleporting only moves between unlocked anchors within one region void. Void rifts are separate flags, set by a region's Void Heart. They link region voids, so a rift is the only way to fast travel between regions.
- **Difficulty.** Void enemies use the overworld roster with a world-level offset of +1 to +2 after the Act I tutorial, so `EnemyRoster` needs a level offset per void.
- **Map.** `MapData` gets a void page per region, showing anchors and which spokes are open or sealed.
- **Enemies.** Void enemies are their own roster, so `EnemyRoster` streaming and cleared state work the same way as in the overworld.

## Checks

- Walk the hub-to-spoke-to-hub loop once for each spoke and time it. A spoke that clears in under a minute is too short to justify its place.
- If the teleports make most of the void trivial, move the anchors outward or lengthen the spokes.
- Count the rewards per spoke. Every spoke should give something.
