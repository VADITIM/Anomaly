# Pacing and backtracking

Status: draft. Target and budgets are starting points, to be checked against playtests.

## Target

- Total gameplay: **6–10 hours**.
- Mandatory path near the low end, optional content filling out to the high end.

## Length levers

- **Breadth of content.** Distinct encounters, bosses and rooms set the length. Walking slowly doesn't.
- **Optional content.** Extra souls, Arc levels and side fights fill the upper hours. The mandatory path has to stand on its own.
- **Backtracking that earns something.** Every return trip should unlock a path, a soul, a boss or a shortcut. Empty return trips are the main source of padding.
- **Cheap return.** Shortcuts and loops keep return trips short. Fast travel isn't planned, so routes are what matter.

## Time budget per region

Before building a region, estimate minutes for each room, fight, boss, dialogue and traversal, then sum:

- mandatory path
- optional content
- each return leg

Once the region has a playable layout, have someone run the mandatory route and an explore-everything route, then compare against the budget. Estimates are usually low for traversal and dying.

## Backtracking

Backtracking works without warp upgrades:

1. **One flag system.** Gates, doors, shortcuts and one-way drops all use string IDs saved in `WorldData`. `EnemyRoster` already keeps cleared state per slot.
2. **Sealed passages on the map.** A locked passage is marked on `MapData` with its requirement, so the player knows where to return.
3. **Return routes are different routes.** `LevelBlueprint` lays out loops, so the way back crosses different rooms. One-way drops force the long way round.
4. **Shortcut as reward.** A `ShortcutGate` opens from the far side. The shortcut back is the reward for finishing a loop.

## Corrupted Void and backtracking

The Corrupted Void is a separate map per region, not a mirror of the overworld. See `docs/corrupted-void.md` for its layout, spokes and rewards.

## Soul-curve tuning

The Arc upgrade curve must fill 20 levels across the 6–10 hour target.

- `ArcUpgrades.MAX_LEVEL` is 5 in code, and the per-level values are tuned for 5 levels. At 20 levels, `DAMAGE_PER_LEVEL` (4%) gives about 1.76x damage, and `COOLDOWN_CUT_PER_LEVEL` (6%) would reach zero.
- The soul cost curve, `150 + 120 * (level - 1)`, needs a pass for 20 levels.
- Track souls found per hour of play during playtests. Tune the curve to that, not to a timer.

## Checks

- Time each mandatory and explore-everything route against its budget.
- Walk each void spoke once. A spoke that clears in under a minute isn't earning its place.
- Count the rewards per spoke and per return leg. Every one should give something.
- Log total playtime in the save, so the 6–10 hour target is measured.
