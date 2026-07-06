# Anomaly — Claude Code Guide

2D top-down Action-RPG (Ontological Dark Fantasy). Godot 4.x Mono — C# (.NET 8) + GDShader.

## Commands

- Build: `dotnet build Anomaly.sln`
- Test: `dotnet test Tests/Anomaly.Tests.csproj` (pure-math coverage only)
- Run: through the Godot editor (no CLI run)

## Layout

- `#Scripts/` — all C# (Camera, Entities, Behaviors, StateMachines, Systems, Save, UI, Weapon)
- `Tests/` — xunit project for engine-free math (difficulty tables, stat math)
- `#Scenes/`, `#Assets/`, `#Shaders/` — Godot scenes, art, GDShader

## Docs & Skills (authority order)

- `docs/design.md` — design intent: lore, mechanics math, target architecture.
- `docs/architecture.md` — as-built reality, including known divergences from design.
- `docs/pitfalls.md` — tracked structural debt; check before building on Entity, StateMachine, Stats, Save, or Weapon systems.
- Any C# work MUST follow the `code-anomaly` skill (.NET naming, exports, node access, Resource rules).
- Any narrative/design work MUST pass the `lore-anomaly` skill.
- New gameplay features/systems go through the `feature-dev` skill workflow.

## Don'ts

- Never hand-edit Godot-managed files: `.godot/`, `*.import` (hook-enforced).
- Touch `.tscn`/`.tres` only when explicitly asked — they are editor-owned.
- Never `[Export]` a node reference — config primitives and Resources only.
- Never deepen an open pitfall: no new magic stat strings, `.Instance` singletons, StateMachine branches, or save writes that bypass `SaveManager`.
