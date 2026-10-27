# Brief A: how a structure or belt is created, removed, stored and enumerated

Read `research/briefs/common.md` first; it has the context, the files you have and the report format.

Questions:
1. Data model. What exactly is a placed structure (`MoonDepotSnapshot`)? Cover its fields (kind, position, rotation,
   style, owner, raised or support or stacking links, ids) and which of them would be needed to recreate it faithfully
   somewhere else. Do the same for a belt (`MoonConveyorSnapshot`, `MoonConveyorPath`): ends, ports and joins to
   machines. Where does MoonWorld keep them (collections, dictionaries by id)? How do structure ids get allocated?
2. Creation. Walk through `TryBuildStructure`, `TryBuildDepot`, `SeedPiece`, `DevPlaceCatcher`, `TryBuildConveyor` and
   `TryBuildConveyorRoute`. Separate the validation and charging (`ChargeBuild`) from the steps that actually create
   the record, register it, spawn its visuals and mark it for saving or crew sync. `DevPlaceCatcher` is a developer
   path that may place without checks; how does it differ?
3. Removal. Walk through `RemoveStructureNow`, `TryRemoveStructure` and `TryRemoveConveyor`. What does a clean removal
   need (views, refunds, crew sync, dependent pieces)?
4. Enumeration. How could the mod list every placed structure and belt with its pose (`TryGetStructure`,
   `TryGetConveyor`, the collections behind them)?

Recommendation for the mod: the minimal, safe sequence to (a) add a structure with a given kind, pose and style while
skipping validation and charging, so that it renders, saves and syncs like a normally built one; (b) the same for a
belt between pasted machines; (c) remove both for `/undo`. List the pitfalls: stale caches, power grids, support and
stacking links, ids.
