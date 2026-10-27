# Brief C: snap locations, aiming, and mouse and keyboard input

Read `research/briefs/common.md` first; it has the context, the files you have and the report format.

Files: `MoonWorld$$PlanStructure`, `PlanFitted`, `PlanWorldHex`, `PlanBuildingPart`, `SnapConcrete`, `WorldHexCell`,
`FitWorldHex`, `WorldHexFrame`, `HexCellsAround` and `FootprintPlane`, and `MoonDepotBuilder$$ReadInput`, `PickAimed`,
`PlacementPose`, `UpdateFoundationPlacement`, `UpdateMachineSnap`, `BeginPlacement`, `ShowPlan`, `ToggleBuildMode` and
`Update`.

The Moon is a sphere about 2,000 m in radius, so "up" is the radial direction. Its surface is divided into a hex grid
with 12 pentagon cells (world hexes); square foundations also exist.

Questions:
1. Snapping. What snap locations does the builder offer: world-hex cells, a square grid, edges of existing
   foundations, machine snap points? How is the snapped pose (position and rotation) computed from the aim point and
   yaw? Which methods could the mod call to get "the nearest valid snap pose" for an anchor piece of a given kind at a
   given aim, without building anything?
2. Orientation. How is yaw defined relative to the local frame, for squares and for hexes? What rotation steps make
   sense: 90° for squares, 60° for hexes?
3. Aiming. How does the builder find the surface the player aims at (raycast from which camera, which layer mask, max
   distance)? The mod needs the same thing for the wand's points A and B and the paste anchor.
4. Input. How does `MoonDepotBuilder` read clicks and keys (`ReadInput`, `BuildInput`)? Which input system: the new
   Input System's actions, or `Input.GetMouseButton`? How could the mod (a) take left and right clicks for the wand
   without the game also acting on them (building, brushing, picking up), and (b) block the game's keyboard controls
   while the player types in the mod's command line? Look for anything the game already uses to suspend input while
   its own UI is open.

Recommendation for the mod: how to compute paste snapping, which game methods to reuse, and the input-capture approach,
with the evidence.
