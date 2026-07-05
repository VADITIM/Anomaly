---
name: feature-dev
description: Structured workflow for building a new Anomaly game feature or system (enemy, boss, Arc, behavior, meter, mechanic, progression element). Use whenever the user asks to add, design, or implement a gameplay feature or system, or says "let's build X". Walks design gate → data authority → as-built check → implementation → verification, so features land consistent with both lore and architecture.
---

# Anomaly Feature Development Workflow

Build the feature in the phases below, in order. Do not skip a gate because the feature "seems simple" — the gates exist because the codebase has tracked structural debt (docs/pitfalls.md) that simple features tend to step on.

## Phase 1 — Design Gate

1. **Lore fit**: run the `lore-anomaly` skill checks. The feature needs an ontological grounding, not just a mechanical one. If it violates an Absolute Impossibility (decorruption, true heroes/villains, weapon discarding), stop and redesign.
2. **Design contract**: check `docs/design.md` for the section governing this system (Arcs §3.5, Healing §3.8, Stats §3.10, Save §3.11, Difficulty §3.12...). If the relevant values are `[UNDEFINED]`, ask the user to define them rather than inventing — then record the answer in design.md as part of the change.
3. State in one short paragraph: what the feature does, which design.md section owns it, and what data it needs. Confirm with the user if anything is ambiguous.

## Phase 2 — Data Authority Decision

For every value the feature introduces, classify it (per `code-anomaly` §5):

- **Resource (`.tres`)** — designer-authored, shared/reusable, standalone identity (Arc data, stat baselines, focus profiles). Resources never hold mutable runtime state.
- **Exported primitive** — single instance-scoped tunable, PascalCase property.
- **Runtime field** — per-instance mutating state; lives on the entity/behavior, `_camelCase` private.
- **Saved data** — must go through the save layer; see the P5 pitfall before extending `SaveSystem`.

Write the classification down before writing code. Misclassification here is the most expensive mistake in this codebase.

## Phase 3 — As-Built Check

1. Read `docs/architecture.md` → "Integration Points for New Features" for the system you're touching.
2. Read `docs/pitfalls.md` and identify which open pitfalls your feature touches. For each: follow its **Containment** rule. New code must not deepen an open pitfall (no new magic stat strings, no new `.Instance` singletons, no new branches in `StateMachine.ProcessPlayerInput`/`ProcessEnemy`, no new exported node references, no JSON save extensions).
3. If the feature *requires* fixing part of a pitfall (e.g. first new saved field forces the SaveManager migration), say so up front and get the user's go-ahead for the larger scope.

## Phase 4 — Implementation

Order of work:

1. **Data first**: Resource classes / `.tres` assets / EntityStats entries.
2. **Logic second**: entity subclass, behavior (`IEntityBehavior` via `AddBehavior()`), or system class. Follow `code-anomaly` for naming (.NET conventions), node access (Owner → Root → log-and-create), and signal rules.
3. **Scene wiring third**: describe required `.tscn` changes for the user to make in the Godot editor (scenes are editor-owned; only edit `.tscn` text when explicitly asked). List exact node names and types — string lookups must match.
4. **UI last**, if the feature has a meter/display.

Keep each step compiling: `dotnet build Anomaly.sln` after every unit of work, not just at the end.

## Phase 5 — Verification & Documentation

1. Build passes with zero new warnings.
2. Walk the feature's runtime path aloud: what happens on `_Ready`, on the triggering input/event, on death/scene-reload (P4: does anything hold a stale reference after `ReloadCurrentScene`?).
3. Update the docs the feature invalidated:
   - `docs/architecture.md` — if the as-built shape changed.
   - `docs/design.md` — if an `[UNDEFINED]` got defined or a value changed.
   - `docs/pitfalls.md` — if the work resolved or extended a pitfall (move resolved ones to the Resolved section).
4. Summarize for the user: what was added, which design.md section it fulfills, what remains `[UNDEFINED]`, and what must be done manually in the Godot editor.
