## Context (shared by all research briefs)

Moon Sweepers is a Unity 6 IL2CPP game (namespace `MoonCleaners`). We are designing a BepInEx 6 IL2CPP mod, "WorldEdit",
for these commands. The player types them in a command line the mod adds:
- `/wand`, then left click a surface (point A) and right click another (point B), selecting a box;
- `/copy`: every placed object in the box (foundations, walls, floors, roofs, machines, belts; NOT capsules), saved
  to a file so it can be pasted in another save or a crew world joined as a guest;
- `/rotate <degrees>`;
- `/paste`: snap to the nearest valid snap location under the player's aim, then FORCE-place everything relative to
  it (ignore placement checks, charge nothing);
- `/undo`: remove what the last paste added.

It must work when the player is a GUEST in someone else's crew world (the host does not have the mod).

## What you have (read-only: Read, Grep, Glob; you cannot run anything or edit files)

- `research/decompiled/*.c`: Ghidra decompilations of game methods, one per file, named like
  `MoonCleaners.MoonWorld$$TryBuildStructure.c`. These are IL2CPP builds: `param_1` is usually `this`, the last
  parameter is the `MethodInfo*`, and fields are reached by offsets. Calls and fields carry annotations (comments)
  naming the real methods and fields. Trust the annotations over Ghidra's guessed names.
- `research/cs/*.cs`: readable C# skeletons (Cpp2IL, signatures and fields with offsets, no bodies) for MoonWorld,
  MoonCrewSession, MoonDepotBuilder, MoonDepotSnapshot, MoonConveyorSnapshot, MoonDepotSave,
  MoonStructurePoseSave, MoonStructureAnchorSave, MoonDepotBuildDefinition, MoonFoundationModule, MoonConveyorPath,
  MoonConveyorSave and MoonDroppedCanister. Use the field offsets here to decode `param_1 + 0x..` accesses.
- `research/interop/*.cs`: the BepInEx interop view (what a mod can call): moonworld.cs, crewsession.cs,
  depotbuilder.cs, settings_screen.cs.

## How to answer

- Be concrete and cite evidence as `file:line` for every claim. Separate what the code shows from what you infer, and
  mark guesses as guesses.
- If a needed method is not decompiled, name it in a section "Methods to decompile next" (full name
  `MoonCleaners.Type$$Method`) and say why.
- Finish with a markdown report as your final answer: the findings, then a "Recommendation for the mod" section with
  the exact calls or state changes the mod should make, then the open questions. About 1,500 words at most.
