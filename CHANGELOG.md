# Changelog

All notable changes to this project are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

### Fixed
- The wand missed foundations: a piece only counted if its base point was within 1 m of the clicked height, so
  clicking on top of tall foundations left them out. A piece now counts when its 3D model overlaps the box by at least
  0.2 m (half its size for thin pieces such as floor tiles, so neighbours that only touch stay out). A belt counts when
  both ends are inside the box's outline, down to 4 m below it.

- The command line didn't show at all. It hung off BepInEx's hidden manager object, whose UI never draws; it now has
  its own scene object. Then its text didn't draw: the UI kit hides any line taller than its box, and the game's
  fonts have tall lines. Each row is now one line that may overflow its box.
- The hologram failed with "The builder has no preview material". The builder only creates its ghost material once
  build mode has been used, so the hologram falls back to the game's URP shader with a glow.

### Changed
- The command line is now a console (requested): a dark see-through strip in the top-left corner with a "> " prompt,
  the last 5 messages under it like a log (while closed, only those from the last 8 seconds), and the wand's live
  count.

### Added
- `/paste-relative` (requested): pastes the copy where it was relative to you when you copied it, turned with your
  facing, with no snapping. `/copy` now records your position and facing, and the clipboard file is version 2
  (version 1 files still read, but need a fresh `/copy` for this). `/paste` and `/paste-relative` say how far from you
  the paste landed. 9 tests (80 in all).
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
