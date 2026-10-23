# Anomaly — first build

2D top-down action RPG in Godot 4.7 (.NET 8, C#). This vertical slice is centred on Hollow Reach in Verdanya, the most broken land in Akasa. Corrupted Fractures tear out of it toward Èrmiosa, Azurean and Elaphrium. Glaciemera is reachable only through Èrmiosa. Each continent holds a Void Heart for Kio to rupture. The Voice calls that "cleansing".

## Run

1. Open the folder in Godot 4.7.2 (.NET build). Leave the Phantom Camera plugin enabled.
2. Build (`dotnet build`), then press Play. The main scene is `#Scenes/Main.tscn`.

Screenshot harness: `"Godot 4.7.2.exe" --path . -- --autopilot --capture=<dir>` plays a scripted tour, saves PNGs and quits. Add `--animcheck` to capture a frame strip of one attack per facing instead. Other modes: `--regioncheck` visits every continent and films the world map, `--enemycheck` walks Kio up to each new enemy type, `--cuecheck` films an enemy wind-up, `--fallcheck` drops Kio into a pit beside a cliff face, and `--combocheck` plays the five-strike string on the dummy (run it with `--write-movie <file>.avi --fixed-fps 60`, since screenshot stalls distort timing).

## Controls

| Action | Keyboard / mouse | Gamepad |
|---|---|---|
| Move / aim | WASD / mouse | L-stick / R-stick |
| Strike (5-hit combo) | LMB | X |
| Arc special | RMB | Y |
| Dash (i-frames, crosses small gaps) | Space | B |
| Jump (one height level) | Shift / C | A |
| Heal (Health Star) | Q | LB |
| Aetheriac State on/off | R | RB |
| Interact (Maldor, bonfires, gates) | E | D-pad up |
| Pause | Esc | Start |

## What's in

- **Height in a 2D plane.** The terrain uses a heightmap. Each entity has its own Z, so the world has ledges, cliffs, jumps and falls. Pits drop into the Void, and you respawn at your last safe ground. Elevated terrain draws in the actor Y-sort, so it hides whatever stands behind it.
- **Combat feel.** Input buffering, lunges, aim assist, hitstop, trauma-based shake, zoom punch, flashes and outlines, squash and stretch, sparks, crescent slash VFX, damage numbers (gold `!` = weakness hit), afterimages, and perfect dodge ("PHASE", with slow motion).
- **Enemy bars.** `EnemyResourceBar` follows the V/AS motion rules (`Motion` easings, real-time clocks, so hitstop never delays it). Damage cuts the bar on the hit frame and leaves a white slab that collapses after the string. A heal sweeps in behind a leading slab. A stagger breaks the Tenacity strip and sweeps the gold window in. Death shatters the bar into falling columns (`EnemyBarShard`). The dummy's bar shatters on a finishing hit and reassembles.
- **Telekinetic Scythe.** Kio never holds the Scythe (`TelekineticScythe`). It appears on Kio's first attack and floats at Kio's shoulder. Each swing pulls it back, then sweeps it around Kio in the attack direction. It stays while any aggroed enemy is within 220 px, and fades 3 s after combat ends.
- **Practice Dummy.** `TargetDummy` stands east of the start. It's rooted, harmless and can't die. It shows damage numbers, the health bar and Tenacity, and a readout of the current string's total damage and DPS. It's weak to Piercing, and it refills 3 s after the last hit.
- **Soul Weapon Arcs.** The `.tres` resources are in `#Resources/Arcs`: Scythe, Hammer and Spear. The combo has five strikes: forward sweep, backhand, a full spin, a lunging buzzsaw that grinds about five times, and a leaping overhead slam. The slam applies the Arc's interval effect. Each Arc has its own special: Scythe throw (boomerang), ground shatter wave, or lunge-smash. A strike that matches the target's weakness does 130% damage. Enemy projectiles can be struck back ("REND").
- **Impact system.** Knockback scales with weight. Filling an enemy's Tenacity breaks it into a stagger window, where it takes 1.5× damage.
- **Resources.** Health; stamina with a regen delay; the Vessel bar, which fills at `n × 1.3` per consecutive hit and pours into the Health Star (one heal) when full — while the star is charged the Vessel keeps filling to 100% and waits; and Corruption, which fuels the Aetheriac State (lifesteal, with HP drain that accelerates and can kill).
- **Enemies.** Rotling (predicted leaps, chains a second leap while healthy), Husk Hound (orbits, lunges at where Kio is heading, often double-lunges), Carrion Wasp (strafes, leads its spit, three-shot fan when wounded) and Hollow Brute (smash chains that grow as it weakens, shoulder charge when kited). Weaknesses: Smashing, Slashing, Piercing, Piercing. Health bars show level arches from `DifficultyScalingSystem`.
- **Continent enemies** (`SheetEnemy` draws side-view packs: one strip per clip, mirrored for facing):
  - **Gnawer** (Èrmiosa, Elaphrium): weaves in, bites and scurries back. **Nests** hide a pack under bones, and the bones rattle and squeak before the pack bursts out. The **Bloated** variant swells as it attacks and leaves a miasma pool when it dies.
  - **Bonewalker** (Èrmiosa): two-blow combo up close, leaping slam at range. When cut down it collapses into a glowing pile and stands again at half health, unless the pile is struck or the killing blow was Smashing. The **Rimebound** variant (Glaciemera) is slower and hard to flinch, its slam leaves a frost patch, and it stays down.
  - **Slime**: crawls, lashes, spits and hops back when crowded. Rimeslime (Glaciemera) spits ice that leaves a frost patch. Bogslime (Elaphrium) spits bile and splits into two Slimelets on death.
  - **Gloom Bat**: hangs asleep, and the sleeping shape is the tell. It wakes when Kio passes under, orbits, dive-bites, and screeches a knock-back ring when wounded or crowded.
  - **Shadeflit** (Hollow Reach, Azurean Verge): half-visible while it stalks. It vanishes in smoke, reappears behind Kio and runs a spear thrust straight through, then stands exposed.
  - Hazards (`GroundHazard`) show only a pulsing rim while they arm, then hurt anyone standing in them.
- **Void falls.** A body falling into a pit stays in the actor Y-sort and sinks straight down on screen, in front of the far cliff face. While falling it is clipped to the pit opening (`Entity._Draw` as a clip mask), so the ground in front of the pit swallows it as it drops, and it fades into the Void colour.
- **Dodge cue.** Every enemy wind-up shows a red diamond over its head that fills as four ticks close in (`AttackCue`). 0.18 s before the attack commits, the diamond bursts into a white glint and the enemy flashes, so a dash started on that signal lands in the perfect-dodge window. Follow-up strikes without a wind-up (Hound re-lunge, Rotling chained leap, Brute smash chain) fire the glint too.
- **Pack AI.** `EnemyDirector` lets two melee and two ranged enemies commit at once. The rest hold spread-out flank slots and rotate in. An aggroed enemy alerts others in sight. Enemies sidestep swings (per-type chance), counter out of flinches, and harden for 1.3 s after three quick flinches (amber outline) so they can't be stunlocked. They press harder while Kio heals or is out of stamina.
- **Resonance (leveling).** Kills grant Residue. Levelling up grants a point to attune at a bonfire into Vitality, Endurance, Might or Resolve. Each Resonance also quietly speeds up Corruption. The design is in the Obsidian vault at `Systems & Gameplay/Systems/Resonance.md`. Strikes now cost stamina, and base damage and stamina regen are lower; Resonance is how power comes back.
- **World.** Hollow Reach is the hub, with one Fracture span out of it in each direction. Each continent is built as a loop: Fracture landing with a bonfire, a fork, a gauntlet, a Void Heart, then a **shortcut gate** (`ShortcutGate`) that only lifts from the far side and leads back to the landing. Opened gates persist.
  - The Bonefields / Èrmiosa (east): the first real region. Watch-post with a Gnawer nest, Ossuary Field, Higher Empire sanctum, and the Burial Pits. The Rend is introduced here, within the first 1–2 hours of play.
  - Frostwatch / Glaciemera (north): reachable only through Èrmiosa. Wind Steppe, Rimebound Watch, Snowbound Pantheon, and the Frozen Ravine full of sleeping bats.
  - Aurelian Grove / Elaphrium (south): optional. The player can see it from the start, and it unlocks after the fourth Void Heart. The Great Gate is barred from inside, so the way in runs through the Golden Wood and the Fountain Court to the Altar of the First, and the gate is opened on the way back.
  - Azurean Verge (west): open from the hub, with shade islands. The final section, Valdis, is not here. It sits inside the Corrupted Void, locked behind four Void Hearts.
  - Every continent has a Soul Cache detour guarded by a bait ambush.
- **World loop.** Maldor's forge in Hollow Reach is the rest point: rest, swap Arcs, hone the Scythe. Bonfires hold the player-stat upgrades (Resonance, Vessel). Using a bonfire also makes it your respawn point; it doesn't heal or reset enemies. Dying leaves your souls at the death spot. Each Void Heart has two guard waves drawn from its continent's enemies, a rupture, and a difficulty increase. Saves go to `user://*.res` through `SaveManager`.
- **Options.** Pause → Options has Audio (Master, Ambience, Effects, Interface, mute when unfocused), Video (window mode and size, VSync, frame cap, brightness), Effects (screen shake, flashes, chromatic aberration, film grain, bloom), Gameplay (damage numbers, aim assist, control hints) and Controls (keyboard and mouse rebinding). Settings are saved to `user://settings.cfg`, separately from the save game.
- **Lighting and atmosphere.** Ambient light is low and placed lights carry the scene: flickering braziers, hanging soul lanterns, and magenta Void glows welling up from pit edges. `atmosphere.gdshader` adds drifting world-space fog and faint light shafts. The post pass adds mip-chain bloom, film grain and brightness. The Void has a far star layer and a slow pulse.
- **Ruination dressing.** `LevelBlueprint.Erode` bites ragged chunks out of every island rim (it never removes placed, paved or path-critical tiles). `RuinPainter` sinks broken paving, grit and cracks into the ground and darkens rims where Void veins crawl in. `RuinScatter` drops rubble, dead scrub and toppled relics (denser near the rims) and makes grit trickle off cliffs. Trees near the Void are dead, and the boundary forest is a third dead. `VoidDebris` floats torn-off islets with parallax in the abyss. Ash drifts over everything. Ruin and dead-tree sprites come from LPC packs; see `#Assets/Tilesets/Texture/LPC/README.md` for licenses and required credits.
- **Camera.** A PhantomCamera2D follows a rig: Kio, plus cursor look-ahead, plus a bias toward the `CameraFocus` target. Framing is resolution-independent. `ViewScale` derives zoom and UI scale from a fixed 480×270 design canvas, so window and viewport settings don't change what you see.

## Layout

- `#Scripts/Entities`: `Entity` base (Z axis, weight, knockback, `ImpactBehavior`), Player partials, enemies, props, Void Heart
- `#Scripts/World`: `WorldGrid` heightmap, `LevelBlueprint` (code-authored map), terrain painter and renderers
- `#Scripts/Camera`, `#Scripts/UI`, `#Scripts/VFX`, `#Scripts/Save`, `#Scripts/Signals` (static `PlayerSignals` / `EnemySignals` / `PropSignals` / `WorldSignals`)
- Player animation follows the former project's rig. `PlayerAnimator` builds a body `Sprite` and a `WEAPON` node, and each has its own AnimationPlayer. Both play the same clip (`Attack_E`, `Move_SW`, `Dodge_N`…) at the same speed, from libraries in `#Resources/Animations`. The two sheets share one frame layout (`Kio_Body.png`, `Kio_Weapon.png`; the weapon is split out per pixel), so they can't drift apart. Attack hit windows come from the clip's strike frame. The Arc finisher burst (`SoulArcEffect.tres`) has its own player.
- `#Assets`: copied from the former project. `Entities/Player/Kio.png` was extracted from `Player.aseprite` and recolored. `Audio/*.wav` is synthesized SFX from `#Tools/gen_audio.py` (numpy): layered, low-bodied, with baked stereo reverb. The SFX bus adds a short room reverb and a compressor.

## Known gaps

- Kio still uses the former project's placeholder mannequin, recolored. The held-weapon layer (`PlayerAnimator.WEAPON_LAYER_ENABLED`) and the Arc burst (`ARC_EFFECT_ENABLED`) are switched off while the Scythe is telekinetic.
- All three Arcs are unlocked from the start. In canon, Rotshaping unlocks after Rà'Meska.
- A dropped soul remnant lasts only for the current session. Broken props come back only on reload.
