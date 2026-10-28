# Changelog

All notable changes to this project are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

### Fixed
- The wand missed foundations: a piece only counted if its base point was within 1 m of the clicked height, so
  clicking on top of tall foundations left them out. A piece now counts when its 3D model overlaps the box by at least
  0.2 m (half its size for thin pieces such as floor tiles, so neighbours that only touch stay out). A belt counts when
  both ends are inside the box's outline, down to 4 m below it.

### Added
- Project set up: a Unity-free core library with tests, and a BepInEx IL2CPP plugin built against the game's interop,
  laid out like Moon Sweeper Mahjong, whose patched BepInEx it runs on.
- Surface geometry for the commands (`MoonWorldEdit.Core`): a frame on the Moon's surface with radial up
  (`SurfaceFrame`); the wand's box between points A and B (`SelectionBox`); and poses relative to an anchor, with
  rotation about the anchor's up (`Layout`). 21 tests.
- The clipboard core (`Copy`, `Selection`, `PastePlan`, `ClipboardFile`, `YawSolver`):
  - copies are stored relative to an anchor, keeping supports inside the copy;
  - pastes get new ids, relinked supports, and supports placed before what they hold;
  - the clipboard is versioned JSON that rejects records the game would drop;
  - a machine's yaw is solved from the game's own pose function.
- The command parser (`Command`) for /wand, /copy, /rotate <degrees>, /paste and /undo. 62 tests in all.
- The in-game plugin, first version:
  - a command line on the game's UI kit, opened with "/", which holds the game's controls off while typing;
  - /wand: left click sets point A, right click sets point B;
  - /copy: what's built in the box, saved to `BepInEx/config/MoonWorldEdit/clipboard.json`;
  - /rotate;
  - /paste: snaps to the game's own planner (`PlanStructure`), then adds every piece and belt the way the game does
    after its checks, so there are no checks and no charge;
  - /undo: the game's own removal, without a refund.

  In a crew world it starts the crew diff before adding, so the host and crewmates receive the paste, and it puts
  back anything the crew sync drops in its first moments. `FindObjectOfType` is stripped from the game, so the world
  is found with `FindAnyObjectByType`.
- The wand shows its selection (requested: it was hard to tell what the box held):
  - a glowing hologram box stretches from point A to wherever you aim, live, and stays put once point B is set;
  - every piece inside gets a green outline;
  - a running count shows how many pieces and belts are selected.

  Drawn with a copy of the builder's own ghost material.
- Research for the in-game part (`research/`): the game's building, crew sync and snapping methods decompiled, and
  briefs for read-only DeepSeek research agents.
