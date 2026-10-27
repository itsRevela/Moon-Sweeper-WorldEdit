# Moon WorldEdit: design

A BepInEx IL2CPP plugin for Moon Sweepers. It copies built structures from one place, or one world, and pastes them
in another. It runs on the patched BepInEx that Moon Sweeper Mahjong installs.

## Commands (the user's requirements)

| Command | What it does |
|---|---|
| `/wand` | Arms the wand: left click a surface sets point A, right click another sets point B. |
| `/copy` | Copies every placed object in the A–B box (foundations, walls, floors, roofs, machines, belts; not capsules) to the clipboard file. |
| `/rotate <degrees>` | Turns the copied layout about its anchor's up. Positive is clockwise seen from above, as in Unity. |
| `/paste` | Snaps the layout's anchor to the nearest snap location under the aim, then places everything relative to it. |
| `/undo` | Removes what the last paste added. This is the safety net for force-placing. |

Commands are typed in a command line the mod adds (press `/`); the game has no chat.

## Decisions (2026-10-27)

- **Force-place:** a paste skips the game's placement checks (clean ground, slope, overlaps, distance to flags and
  lunar sites). This is the point of the mod: the game refused a valid spot spanning sectors 70815, 70746 and 04460.
- **Free:** pasting charges nothing.
- **Crew worlds:** it must work for a guest in someone else's world, with the host not running the mod.
- **Clipboard:** a JSON file the mod keeps, so a copy carries across saves and joined worlds.

## Geometry (MoonWorldEdit.Core, tested)

- The Moon is a sphere centred on the world origin; the lounge tables sit about 2,000 m from it. "Up" is radial.
- `SurfaceFrame`: a local frame (right, up, forward) on the surface, with forward taken from a hint laid flat.
- `SelectionBox`: the box between A and B in a frame, stretched up and down by a margin. A piece is selected when its
  position, its base point, is inside. To include upper floors, click B high.
- `Layout`: poses relative to an anchor (`Relative`, `Place`) and `Rotate` about the anchor's up.

## Research (research/, game-derived files git-ignored)

- `research/decompiled/`: Ghidra decompiles of the building, crew sync and snapping methods.
- `research/briefs/` and `research/reports/`: questions for, and answers from, read-only DeepSeek research agents
  (`research/run_agent.sh`).
- Known so far: structures are `MoonDepotSnapshot` records (kind, pose and so on) and belts are
  `MoonConveyorSnapshot`. Crewmates sync structures as state (`WriteCrewStructures` and `ApplyCrewStructures`) using
  id blocks the host hands out (`MoonCrewSession.AssignIdBlock`). `TryBuildStructure`, `TryBuildDepot` and
  `TryBuildConveyor` do the checking and charging.
