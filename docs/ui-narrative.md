# Narrative Scene UI — Design Guidelines

Authority order: `design.md` (intent) → this file (narrative UI spec) → `architecture.md` (as-built). Cross-check `pitfalls.md` before touching Save or Entity code.

**Primary visual reference:** Persona 5 — [Game UI Database entry](https://www.gameuidatabase.com/gameData.php?id=72). We reference specifically the **Dialogue Choice** and **Dialogue & Speech** screen categories, not the full P5 UI language.

---

## 1. Visual Style

Anomaly is pixel art, but narrative UI is a deliberate stylistic break: high-contrast, graphic, angular — inspired by Persona 5's dialogue presentation — layered over the pixel-art world.

### 1.1 Speech Boxes

- White fill, thick black outline, angular/slanted quadrilateral shapes — **not** rounded rectangles.
- **Less jagged than P5**: P5 boxes have aggressive torn/spiked edges; ours use clean slanted cuts (2–3 angled edges per box), no spikes or tears. Think "sharp parallelogram", not "explosion".
- Speaker text: black on white. High contrast is non-negotiable for readability over the game world.
- A small angular tail points from the box toward the speaker.
- Speaker name tag: separate small slanted ribbon attached to the box's top-left, dark fill with light text (inverse of the box).

### 1.2 Dialogue Choices

- Choices are a vertical stack of slanted white banners fanning out from the player character's side of the screen (P5 pattern: staggered arrows pointing at the speaker).
- 2–4 options max on screen. Selected option: enlarged/offset with inverted colors (black fill, white text) or a red accent edge.
- Choices appear layered over a darkened/desaturated world, keeping the scene visible behind.

### 1.3 Portraits

- The game world stays pixel art; **portraits are higher-fidelity drawn art**, cut out with an angular border matching the speech-box language.
- Portrait sits on the speaker's screen side, partially overlapping the speech box edge (P5-style overlap, not a framed avatar slot).
- Portraits are planned for entities generally — NPCs, bosses, and Kio each get portrait sets. Support multiple expressions per character from day one (see §2.2).
- Palette: monochrome/desaturated with the character's single accent color, consistent with the Ontological Dark Fantasy tone — avoid P5's red unless the scene demands it.

### 1.4 Motion

- Boxes and choices slide/snap in with short (≤150 ms) angular motion; no fades or bounces.
- Text reveals per-character (typewriter) with an input to instantly complete; a second input advances.
- Never block the game longer than the dialogue itself: entering/leaving narrative mode is one transition each.

---

## 2. Architecture

Goals: scalable (hundreds of conversations), manageable (writers edit data, not code), and save-correct (choices persist through `SaveManager` only).

### 2.1 Data model — dialogue as Resources

Dialogue content is **data, not code**. Per project rules (Resource-vs-Export authority in `code-anomaly`), conversations are Godot `Resource` types under `#Scripts/UI/Narrative/` (scripts) + `#Assets/Dialogue/` (`.tres` content):

- `DialogueGraph` (Resource): one conversation. Holds a list of `DialogueNode`s and an entry node id.
- `DialogueNode` (Resource): speaker id, text, portrait/expression id, then either a next-node id or a list of `DialogueChoice`s.
- `DialogueChoice` (Resource): choice text, target node id, optional **condition** (flag that must be set/unset) and optional **effect** (flags to set — see §2.3).

Node references inside a graph are string ids resolved at load; a missing id is a logged error at load time, not a silent skip (dialogue authoring errors must be loud).

### 2.2 Runtime

- `DialogueManager` (single scene-level node, **not** an `.Instance` singleton — pitfall rule): takes a `DialogueGraph`, walks nodes, raises signals (`NodeEntered`, `ChoicesPresented`, `DialogueEnded`). It contains zero rendering.
- `NarrativeUI` (CanvasLayer scene): subscribes to `DialogueManager` signals and renders boxes/choices/portraits per §1. Swappable without touching dialogue logic.
- Portraits: a `PortraitSet` Resource per entity mapping expression ids → textures. `DialogueNode` stores `(speakerId, expressionId)`; the UI resolves through a registry, same string-id + registry pattern as the save system (design.md §3.13).
- Entities that can talk expose a `DialogueGraph` reference (a Resource `[Export]` — allowed; node refs are not).

### 2.3 Progression & NPC state — flags through SaveManager

Best practice for "choice changed an NPC" is **flag-based, not transcript-based**: never save which dialogue node you're on; save the *consequences* as named boolean/int flags.

- All narrative state lives in `WorldData` (NPC flags already exist there) and `ProgressionData` (narrative flags), accessed **only** via `SaveManager` — never a new save path (pitfall rule).
- Flag ids are strings following the save-system id conventions (design.md §3.13): format `[UNDEFINED]` until the id registry is defined — suggested `npc.<name>.<flag>` / `story.<act>.<flag>`. Unknown flags read as unset, matching the registry's silent-ignore rule.
- A `DialogueChoice` effect sets flags via `SaveManager` write methods (add e.g. `SaveManager.SetNarrativeFlag(id)`); the next auto-save persists them ("Major NPC interaction" is already a listed save trigger, design.md §3.11).
- NPC variation is then pure reads: an NPC picks its `DialogueGraph` (or a graph picks its entry node) by checking flags. No dialogue history is ever serialized.
- Mid-conversation state is **never saved**. Dialogue is atomic: quitting mid-conversation replays it from the start with the pre-conversation flags.

### 2.4 Scaling rules

- One `.tres` graph per conversation; NPCs with evolving relationships get one graph per stage, selected by flags — not one mega-graph with internal branches on every flag.
- Conditions/effects are data on choices/nodes, not C# branches inside `DialogueManager`. The manager must never contain `if (npcName == ...)` logic (StateMachine-branch pitfall applies in spirit).
- Localizable from the start: node text goes through Godot `tr()` keys once localization begins — `[UNDEFINED]` until then, but don't bake formatting into strings.

---

## 3. Open / `[UNDEFINED]`

- Flag id registry format (blocked on save-system id conventions, design.md §3.13).
- Whether choices can have non-flag effects (items, stat changes) — route through existing systems if so, never directly from dialogue data.
- Portrait art pipeline (resolution, expression set per character).
- Timed choices / social-stat-gated choices (P5 has them; no design intent stated yet — do not build speculatively).
