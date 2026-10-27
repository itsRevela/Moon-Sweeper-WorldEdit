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

## Research findings (agent reports A, B and C, 2026-10-27; details in research/reports/, kept local)

**Records.** A structure is a `MoonDepotSnapshot` in `world.depots` (keyed by id). Recreating one takes `kind`
(1..192), position (relative to `world.transform.position`), rotation and `style`, plus `support` (the id it stands on)
remapped to the new ids.
- Structural pieces store a quaternion (`placedPose` = true); machines store `yaw`.
- `stored` and `contributions` are machine and economy state and are not copied, so cargo isn't duplicated.

A belt is a `MoonConveyorSnapshot` in `world.conveyors`: start and end points and their directions. Its joins to
machines are purely geometric, so place machines first, then belts.

**Force-placed, free paste.** Do the insert that `TryBuildStructure` does after its checks, and nothing more:
1. Take `id = world.nextDepotId++`.
2. Build the snapshot.
3. Call `MoonDepotConstruction.Spawn(rec)`, then `depots.Add`.
4. Call `RefreshBeltsAt(PortPoints(rec))`, `PressDustUnder`, `RevealBuilt(ViewOf(id))`.
5. Set `progressDirty`, then call `SaveProgress()`.

Never call `ChargeBuild` or any `TryBuild*` method. `DevPlaceCatcher` (a developer tool) is the game's own example of
this. Belts: `conveyors.Add`, `ConveyorViews.Built`, `RevealBuilt`.

**/undo.** Use `RemoveStructureNow`'s teardown: the view, `depots.Remove`, `ReleaseSupport`, belt and joint refresh,
`DismantleView`, then save. For belts, `conveyors.Remove` and `Dismantled`. Never use `TryRemove*`: it refunds money
that was never spent.

**Crew worlds (works for a guest, host unmodded).** Structures sync as replicated state, not as requests:
- Every 1/15 s, `MoonCrewSession.UpdateWorld` sends `WriteCrewStructures`: a diff of `depots` and `conveyors` against
  hashes (`crewDepots`, `crewBelts`), covering additions and removals.
- Receivers apply it with no placement or money checks, only `ReadDepot` sanity checks: `id` ≥ 1, `kind` 1..192, an
  owner set, finite values, `style` in range.

A raw insert therefore reaches the host and everyone else, and the host's save keeps it after its next autosave. The
rules:
- Insert only while `world.crewTracking` is on. If it's off, call `BeginCrewTracking()` first, never afterwards.
- Take ids from `nextDepotId++`, which sits inside the host-assigned id block.
- Records must pass `ReadDepot`. Bad ones are dropped by peers and then deleted locally by the "truth" pass.
- The truth pass (64 hash buckets) can delete a record only one peer has. Records are protected once sent
  (`TouchTruth`). Check after about 1 s and re-insert once if one vanished.

**Snapping.** Use `world.PlanStructure(kind, aim, yaw, target)`: it plans a pose without building and covers
world-hex and square cells, concrete foundation edges and building-part edges. Use `world.SnapMachine` for machines
and `DepotPose` as the free fallback. Grid pieces turn in steps of `360 / mesh corners` (60° for hexes, 90° for
squares); pentagon cells are refused.

**Aim and input.**
- Aim with `builder.ReadInput().aim` (the walker's view ray) and the builder's raycast mask (all layers except 2,
  triggers ignored); `StructureAt(collider)` gives the target.
- The game reads mouse and keyboard devices directly (new Input System), so the wand reads clicks itself while the
  build tool is holstered.
- For the command line, mirror the game's own UI state: walker flags, unlocked cursor, and a neutral
  `builder.TestInput` so the builder sees no keys.
