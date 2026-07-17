---
name: docs-sync
description: Compact the session's code/scene/design changes into the Anomaly docs (design.md, architecture.md, pitfalls.md, CLAUDE.md, ui-narrative.md). Use at the end of a work session, before a commit, or whenever the user says "update the docs", "sync the docs", or "document what we did". Diffs the working tree against the docs' claims and reconciles them so the docs never drift from the as-built code.
---

# Anomaly Docs Sync

Reconcile the docs with what actually changed. The docs are authority-ordered (CLAUDE.md § Docs & Skills): `design.md` = intent, `architecture.md` = as-built reality, `pitfalls.md` = tracked debt, `ui-narrative.md` = narrative UI spec. Each captures a different aspect of the same change — one change may touch several.

## Step 1 — Collect the change set

1. `git status` + `git diff` (staged and unstaged) against the last commit. If the session's work spans commits, diff from the last commit whose docs were in sync.
2. List every touched `.cs`, `.tscn`, `.tres`, `.gdshader`, and config file. Group by system (Entity, Stats, Save, UI, Weapon, StateMachine, Behaviors, Difficulty).
3. Include changes made *by the user in the editor* that the session discussed (scene rewires, Inspector assignments) — these are part of the change set even though no tool call made them.

## Step 2 — Classify each change against the docs

For every change, decide which docs it invalidates:

| Change kind | Doc to update |
|---|---|
| New/renamed class, behavior, system; changed wiring, node access, precedence | `architecture.md` (How the Core Loop Works / relevant section) |
| A design value defined (was `[UNDEFINED]`), a mechanic's rule changed, new meter/reward semantics | `design.md` (owning §; replace `[UNDEFINED]` in place) |
| Pitfall resolved, narrowed, extended, or newly discovered | `pitfalls.md` (move to Resolved with a one-line how, or edit the open entry) |
| New project-wide rule or footgun (e.g. shared scene sub-resources) | `CLAUDE.md` Don'ts — one line, only if it applies beyond a single system |
| Divergence created or removed between design intent and code | `architecture.md` § Known Divergences table |
| Narrative-scene UI, dialogue, NPC-state | `ui-narrative.md` |

Rules:
- **as-built beats aspiration**: `architecture.md` describes what the code does *now*, including in-progress migrations ("X.tres exists but is not yet assigned; constructors remain live authority").
- **design.md changes need the user**: never rewrite intent silently. If code contradicts design.md and the user hasn't ruled, record it in Known Divergences instead and flag it.
- **No duplication**: a fact lives in exactly one doc; others link to it by section reference.

## Step 3 — Verify docs' existing claims

Before writing, grep the docs for names touched this session (classes, node names, `.tres` paths, values). Any stale claim found — old class names, deleted exports, superseded values — is part of this sync even if today's change didn't cause it. Fix or flag it.

## Step 4 — Write the updates

- Edit surgically: change the sentences that are now false, add the minimum that makes the docs true. Match each doc's existing voice and formatting (tables stay tables, § numbering stays).
- Convert relative time ("today", "now") to absolute dates where the doc uses them (pitfalls.md audit notes).
- If the session defined a previously `[UNDEFINED]` value, replace the marker in `design.md` and note the defining decision in one clause.

## Step 5 — Report

Summarize to the user, per doc: what was updated and why, plus anything deliberately *not* documented (unconfirmed hypotheses, decisions still pending). List any contradictions found that need a user ruling. Do not commit unless asked.
