# Refactoring

A refactor changes how code is organised without changing what the game does. Keep it separate from feature work, balance changes and bug fixes, so a regression can be traced to one kind of change.

## Before you start

- Say what is moving and why in one sentence, for example "entities load their visuals from a scene instead of building nodes in code".
- Find every caller. Use `Grep` for the class name, the enum value and any string paths (`res://...`), since Godot loads scenes and resources by path.
- Check saved data. If the refactor touches a type that is serialised, confirm that existing saves still load.
- Decide what the end state looks like before moving the first file, so the work has a finish line.

## Doing it

- Move one thing at a time and keep the game compiling after each step. Run `dotnet build` after every C# change.
- Keep the old path working until the last caller has moved, then remove it. Do not leave two ways of doing the same thing for longer than one step.
- Prefer moving behaviour unchanged over improving it on the way. If something is clearly wrong, note it and fix it in its own change.
- When a script has to match a scene, name the file after the class, as Godot requires.
- Keep the public surface stable where other systems depend on it. If a signature must change, update its callers in the same change.

## Godot specifics

- Scenes reference scripts and resources by `uid` and path. When you rename or move a file, update the `.tscn` and `.tres` references, and check that the `.uid` file moves with its script.
- Node paths are strings. Renaming a node in a scene breaks every `GetNode` that points at it, so search for the old name before renaming.
- `[Export]` fields are set in the editor. Moving a field can silently drop a value that was set on a scene, so note any exported values before changing them.
- Do not change a root node's position in a scene as part of a refactor. Positions are gameplay data for entities (see `CLAUDE.md`, "Entity scenes").

## Checking it

- The build must pass with no new warnings in the files you touched.
- Walk through the code path you changed, in the same order the game runs it: spawn, update, interact, save and load.
- Verify visually only when the user asks for it. Otherwise say what you checked and what you did not.
- Note in the summary anything you could not verify, such as an entity that needs a playtest.

## Summary to the user

- List the files moved or renamed, the callers updated, and anything left on the old path on purpose.
- State plainly what was verified and what was not.
