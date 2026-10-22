---
name: anomaly-level-design
description: Souls-like level, map and world structure for Anomaly, the top-down Godot Souls-like. Use this whenever the work touches tilesets, TileSet/TileMapLayer setup, terrain or autotile rules, rooms, areas, regions, dungeons, maps, world layout, worldbuilding, checkpoints, shortcuts, secrets, traps, enemy or item placement, or procedural/level generation for Anomaly, even if the user only says "make a new area", "add tiles for X" or "lay out this region". It loads the Level Design notes from the Obsidian vault, applies them, and reviews the result against the Level Design Checklist. Skip it only when the user explicitly says not to use it.
---

# Anomaly Level Design

Anomaly is a top-down Souls-like. Its areas should feel like Souls-like spaces: interlocking loops, shortcuts that open from one side, fake routes and dead ends that pay off, traps that are fair on a second look, and progression that keeps rewarding detours. This skill keeps every tileset, map and world decision tied to one shared design reference, so areas don't drift into plain corridors or generic dungeon rooms.

The user wants this used for all tileset and worldbuilding work unless they say otherwise in that request. If they do, drop it for that task and say so in one line.

## 1. Load the design reference

The reference is a set of 7 notes in the Obsidian vault `C:\Users\vn\Documents\Obsidian\Anomaly`. Find them by searching the vault for `Level Design - Overview.md`. They may be in `Level Design/` or inside an existing design folder.

**If the notes exist**, read the Overview and then only the notes this task needs:

| Task | Read |
|---|---|
| Tileset, tile art needs, terrain/autotile rules | Top-Down Specifics, Level Features Catalog |
| Laying out a room or area | World Structure, Level Features Catalog, Pacing and Reward |
| Region or world map, gating, progression order | World Structure, Pacing and Reward |
| Procedural or hybrid generation code | Generation Guide, World Structure |
| Reviewing an existing area | Level Design Checklist (plus whatever it points to) |

**If the notes don't exist yet**, tell the user and offer to build them with the research workflow in [references/research-prompt.md](references/research-prompt.md). It's a long web-research run that writes 7 new notes, so get a yes first. Until the notes exist, apply the principles in section 2 and say that you're working without the vault reference.

## 2. Apply it

Base every decision on the notes and cite the note you relied on (for example "per [[World Structure]]: checkpoint spacing"). When the notes don't cover a case, these fallback principles apply:

- **Graph before geometry.** Sketch the area as nodes (rooms), edges (paths) and locks/keys (abilities, keys, bosses, knowledge) before placing tiles. Every area needs at least one loop back to its checkpoint, usually a shortcut opened from the far side.
- **Every detour pays.** A dead end holds an item, lore, a view of a later area, or a shortcut. Fake routes should teach something or tempt with a visible reward.
- **Traps are telegraphed.** A trap, ambush or mimic needs a readable tell that a careful player can spot. It's fair on a second look, never pure gotcha.
- **Top-down readability.** Height, depth, drops, ledges, one-way doors and illusory walls must be readable from above. That takes tile contrast, edge and shadow tiles, occlusion and sight lines. Plan the tiles these features need, not only the floor and walls.
- **Freshness per area.** Each area introduces at least one new mechanic, enemy mix, or twist on an earlier one, and revisited areas change.

For **tileset work**, list the tiles each level feature needs (shortcut doors open and closed, breakable or illusory walls, ledge and drop edges, trap tells, checkpoint markers, foreground occluders), along with the terrain and physics layers that support them. That way the art covers the design, not only the look.

For **lore and canon** (what a region is, who lives there, what an item means), also use the `anomaly-lore-consistency` skill. This skill covers structure; that one covers canon.

## 3. Check before finishing

Run the new or changed area against the **Level Design Checklist** note, or the section 2 principles if the notes don't exist yet. Report the result as a short pass/fail list, giving a reason for each fail.

If the work produced a new reusable design decision (a convention, a tile rule, a generation rule), offer to add it to the matching vault note. Never edit existing vault notes without the user's OK.

## Running the game

To see a layout in-game, use `tools/autopilot.ps1` as the project's CLAUDE.md describes. Never launch Godot directly.
