# Research workflow: build the Level Design reference

Run this only after the user has agreed. It builds the 7 vault notes that the skill reads.

<role>
You are a senior level designer and game-design researcher who specializes in Souls-like world structure and top-down action RPGs. You write practical design references that a solo developer can build levels from.
</role>

<context>
- Project: "Anomaly", a top-down action game in Godot 4 at C:\Users\vn\source\Godot\Anomaly. Its core idea is a top-down Souls-like.
- Design notes live in the Obsidian vault at C:\Users\vn\Documents\Obsidian\Anomaly.
- Goal: a general reference sheet on how to structure and generate levels, maps and worlds. Progression must stay rewarding and never feel stale. The sheet must cover the signature Souls-like layout features: fake or decoy routes, dead ends that pay off, traps and ambushes, shortcuts and locked doors that open from one side, interconnected loops back to checkpoints, hidden walls, optional areas, vertical layering, and environmental storytelling.
</context>

<task>
1. Research how Souls-like games build their worlds. At minimum cover Dark Souls 1 (the Lordran interconnection), Dark Souls 3, Bloodborne, Sekiro, Elden Ring (open world combined with legacy dungeons), and Hollow Knight as the 2D Metroidvania counterpart.
2. Research top-down games that are close to a top-down Souls-like and record how each one adapts these ideas to a fixed overhead camera. Starting candidates, which you must verify and extend: Death's Door, Tunic, Hyper Light Drifter, Eldest Souls, Unsighted, Ender Lilies (side-view, as a contrast). Add procedural or roguelike references (Hades, Enter the Gungeon, Children of Morta) only for the generation section.
3. Pull out the principles and turn them into a design sheet that can be acted on.
</task>

<research_rules>
- Use WebSearch and WebFetch. Prefer, in this order: developer talks, interviews and postmortems (including GDC); level-design analyses (for example Game Maker's Toolkit, Mark Brown's Boss Keys, Matthewmatosis, Design Doc); well-sourced wikis.
- Cite a source URL for every factual claim about a specific game. Write [uncertain] next to anything you cannot confirm. Never invent quotes, statistics or developer statements.
- Mark clearly which statements are sourced fact, which are your inference, and which are recommendations for Anomaly.
- Record every key observation as: what it does → why it works → how Anomaly can use it.
- You may use subagents for the two research tracks (Souls-likes, top-down games). Do the synthesis yourself.
- Stop researching once each listed game has at least 2 sourced structural observations. Do not run open-ended searching or collect trivia.
</research_rules>

<output_format>
Write Markdown notes into C:\Users\vn\Documents\Obsidian\Anomaly. Before writing, look at the vault's folder layout and put the notes in the closest fitting design folder. If there is none, create `Level Design/`. Use Obsidian [[wikilinks]] between the notes.

Notes (these exact filenames, because the skill looks them up by name):
1. `Level Design - Overview` is the index. It holds a one-paragraph thesis and links to every other note.
2. `World Structure` covers hub vs. interconnected vs. open-world layouts, critical path vs. optional space, gating (ability, key, boss, knowledge), checkpoint (bonfire) placement and spacing, loop-back shortcuts, and how vertical layering works in a top-down view (stacked floors, cliffs, below-ground layers, occlusion).
3. `Level Features Catalog` is a table with the columns: Feature | Purpose for the player | How it is built | Top-down adaptation | Tiles/assets needed | Pitfalls | Example (game + source). It must include fake routes, rewarding dead ends, traps and ambushes, one-way shortcuts, illusory walls, mimics, environmental hints, optional bosses, NPC questlines tied to places, and item placement as storytelling.
4. `Pacing and Reward` covers the tension/release rhythm, reward cadence (consumable vs. permanent upgrades vs. lore), avoiding staleness (new mechanics or enemy mixes per area, revisiting areas changed), and risk vs. reward on detours.
5. `Top-Down Specifics` covers camera framing, readability of height and depth, tileset implications (edge, shadow, occluder and trap-tell tiles), hiding secrets when the view is overhead, sight lines, and lessons from each top-down reference game.
6. `Generation Guide` is a step-by-step method to design or procedurally generate an area: the world graph first (nodes, locks and keys, loops), then room templates, then encounter and trap placement rules, then validation checks. Include a generic data model (graph node/edge fields, room tags) that can be implemented in Godot. Clearly separate handcrafted, hybrid and fully procedural approaches, with the trade-offs for a Souls-like.
7. `Level Design Checklist` is a single-page pass/fail checklist for reviewing any new area.

Every note starts with a 2–3 line summary and ends with a `## Sources` list.
</output_format>

<constraints>
- Only create new files inside the Obsidian vault folder above. NEVER modify, overwrite or delete existing notes, and NEVER touch the Godot project folder.
- Do not write code into the game project. The data model in the Generation Guide is a design spec, not an implementation.
- Keep each note under about 1,500 words. Prefer tables and bullet points to prose.
- Stop and ask before doing any of these: creating more than 8 files, writing outside the chosen vault folder, or continuing when the vault is inaccessible or one of the 7 filenames already exists in the target folder.
</constraints>

<done_when>
- All 7 notes exist and each one has a Sources section.
- Every [[wikilink]] resolves to one of the 7 notes.
- Each note is under about 1,500 words.
- Every game named in the task appears with at least 2 sourced observations, or is explicitly marked as not verifiable.
- Confirm all of the above by listing the files and their word counts after writing.
- Finally, reply with: the folder path, the list of files created, the 5 most important principles (one line each), and any claims you marked [uncertain].
</done_when>
