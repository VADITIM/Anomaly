# Anomaly

## Level design

- For any work on tilesets, maps, rooms, areas, regions, world layout, worldbuilding, or level generation, load the `anomaly-level-design` skill first. Skip it only when the user explicitly says not to use it.

## UI scenes

- Every UI menu or element needs a scene counterpart in `#Scenes/`. Build it in code first, then convert the code into a `.tscn` for the user to edit and polish, and have the script load the scene instead of building the nodes.
- The scene must look in the editor as it does in game: real fonts, colours, sample portraits, text and rows, with runtime-only samples hidden or freed in `_Ready`. Author it on the 480x270 design canvas and set the root `CanvasLayer` scale to 4 so it fills the 1920x1080 editor frame (`ViewScale.Fit` overrides it at runtime).
- Custom-drawn controls used in scenes (`Divider`, `DiamondMark`) live in their own file, named after the class, with `[Tool]` and `[Export]` fields. Godot can only attach a script from a file with the matching name.
- Example: `#Scenes/NarrationBox.tscn` with `#Scripts/UI/NarrationBox.cs`.

## Entity scenes

- Every entity type gets an editable scene in `#Scenes/Entities/<Name>.tscn`, with its script on the root (`Bonfire.tscn` with `Bonfire.cs`). The script loads the scene and does not build the visual or collision nodes in code. Cache each `PackedScene` once; `EnemyRoster` instantiates enemies repeatedly.
- The root position stays the ground footprint that `Entity` relies on for depth sorting, collision and elevation. Visual nudges go on child nodes (body, sprite, shape offsets), never on the root.
- `LevelBlueprint` still decides where each entity goes and what kind it is. The scene only defines how that kind looks and what it is made of.
- Scene-first applies to new and migrated entities. When you touch an entity that is still built in code, migrate it when the change makes that cheap, otherwise leave it and say so.
- Follow `docs/refactoring.md` for any move like this.

## Refactoring

- Refactors change structure without changing behaviour. Keep them separate from feature or balance changes, and follow `docs/refactoring.md`.

## Compiling

- Always compile after changing C# code: `dotnet build Anomaly.csproj -nologo -v q -clp:ErrorsOnly`. A `Stop` hook in `.claude/settings.json` runs this after each response; fix any errors it reports before finishing.

## Running the game for tests

- Never verify changes visually (autopilot runs, screenshots) unless the user explicitly asks for it. Compiling is enough by default.

- Never launch Godot directly for test runs. Use `tools/autopilot.ps1`. It moves the game window to another virtual desktop (so the user never sees it) and never takes focus from the user:
  ```
  powershell -NoProfile -ExecutionPolicy Bypass -File tools/autopilot.ps1 -Mode <autopilot-mode> [-Capture <dir>] [-TimeoutSeconds 60]
  ```
  It writes `log.txt` and the screenshots into `-Capture`.
- It runs Godot with `--audio-driver Dummy`, so test runs are silent. Keep it that way.
- How it avoids focus:
  - Starts Godot through WMI, so the game is not a child of the foreground app.
  - Sets `display/window/size/no_focus` through a temporary `override.cfg`, which is removed once the window exists.
  - Moves the window to a virtual desktop other than the current one using the `VirtualDesktop` PowerShell module (`C:\Users\vn\dependencies\PSModules\VirtualDesktop`). Needs at least two desktops; one is created if missing. Screenshots come from the viewport texture, so the game renders fine off-screen.
  - Shows the window with `SW_SHOWNOACTIVATE`.
- If a stale `override.cfg` is ever left in the project root, delete it. It would stop the user's own runs from taking focus.
