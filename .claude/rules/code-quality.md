# Code Quality

## Anti-defaults (counter common Claude tendencies)

- No premature abstractions. Three similar lines beats a helper used once.
- Don't add features or improvements beyond what was asked.
- Don't refactor adjacent code while fixing a bug.
- No dead code or commented-out blocks. Git has history.
- WHY comments, never WHAT. If code needs a "what" comment, rename instead.
- API docs at module boundaries only, not every internal function.

## Naming

Standard .NET conventions, enforced via the `code-anomaly` skill (authoritative for all C#). Highlights:

- Files and classes: PascalCase (`StateMachine.cs`); partial-class split by concern (`Player.Damage.cs`).
- Private/protected fields: `_camelCase`. Locals and parameters: camelCase. Public members: PascalCase. Constants: PascalCase (never UPPER_SNAKE).
- Interfaces: `I` prefix. No abbreviations: `StateMachine`, never `SM`. Booleans: `Is` / `Has` / `Can` prefix.
- Legacy bare-camelCase fields exist; migrate names only in classes you already touch, whole class at a time.

## Code Markers

`TODO(author): desc (#issue)` for planned work. `FIXME(author): desc (#issue)` for known bugs. `HACK(author): desc (#issue)` for ugly workarounds (explain the proper fix). `NOTE: desc` for non-obvious context. Owner and issue link required. Never `XXX`, `TEMP`, `REMOVEME`.

## File Organization

- One class per file; large entities split into partial classes by concern (`Enemy.Combat.cs`, `Enemy.Tenacity.cs`).
- Function order: public API first, then helpers in call order.
