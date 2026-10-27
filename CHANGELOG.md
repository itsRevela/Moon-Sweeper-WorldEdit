# Changelog

All notable changes to this project are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

### Added
- Project set up: a Unity-free core library with tests, and a BepInEx IL2CPP plugin built against the game's interop,
  laid out like Moon Sweeper Mahjong, whose patched BepInEx it runs on.
- Surface geometry for the commands (`MoonWorldEdit.Core`): a frame on the Moon's surface with radial up
  (`SurfaceFrame`); the wand's box between points A and B (`SelectionBox`); and poses relative to an anchor, with
  rotation about the anchor's up (`Layout`). 21 tests.
- Research for the in-game part (`research/`): the game's building, crew sync and snapping methods decompiled, and
  briefs for read-only DeepSeek research agents.
