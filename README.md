# Moon WorldEdit

A mod for [Moon Sweepers](https://store.steampowered.com/app/5335510): copy built structures and paste them somewhere
else, in the same world, another save or a crew world you've joined.

- `/wand`, then left click point A and right click point B, to select a box
- `/copy` everything built in the box (not capsules)
- `/rotate <degrees>` the copy
- `/paste` it at the nearest snap location under your aim (placed even where the game would refuse, and free)
- `/undo` the last paste

**Status:** in development. See [docs/design.md](docs/design.md).

Requires the patched BepInEx that [Moon Sweeper Mahjong](https://github.com/itsRevela/Moon-Sweeper-Mahjong) installs.

## Building

`dotnet build MoonWorldEdit.sln -c Release`; add `-p:DeployToGame=true` to copy the plugin into the game. The game
folder defaults to Steam's; override it with `-p:MoonSweepersDir=...` or the `MOONSWEEPERS_DIR` environment variable.
Tests: `dotnet test MoonWorldEdit.sln`.
