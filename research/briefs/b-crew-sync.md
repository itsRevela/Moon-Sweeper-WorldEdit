# Brief B: how built structures and belts sync between crewmates, and who decides

Read `research/briefs/common.md` first; it has the context, the files you have and the report format.

Files: `MoonWorld$$WriteCrewStructures`, `ApplyCrewStructures`, `ReadDepot`, `Place`, `SamePlace`,
`BeginCrewTracking`, `EndCrewTracking`, `WriteCrewBelts`, `ApplyCrewBelts`, `BeginCrewBelts`, `EndCrewBelts`,
`ApplyTruthDepots` and `ApplyTruthBelts`, and `MoonCrewSession$$AssignIdBlock`, `Handle`, `UpdateWorld`,
`HostAction` and `SendBelts`. `research/interop/crewsession.cs` lists the message kinds (enum `Kind`: Structures,
Belts, TruthDigest, TruthWanted, TruthPart and others).

Questions:
1. When a GUEST builds a structure, what is sent, to whom, and when? Is it a request the host can refuse, or state the
   host just applies? Does the host re-check placement or money?
2. What does `WriteCrewStructures` send: a diff since `BeginCrewTracking`, or everything? How does it decide which
   structures are "new"? Would a structure added directly to the world's collections, not through
   `TryBuildStructure`, be picked up and sent? If not, what must the mod call or set to have it sent?
3. Ids: what does `AssignIdBlock` do? How does a guest get ids for new structures, and what happens on a clash?
4. Removal: how does a guest's removal reach the others?
5. The "truth" system (`TruthDigest`, `TruthWanted`, `TruthPart`, `ApplyTruthDepots`, `ApplyTruthBelts`): is the host's
   world authoritative, and could it later DELETE structures that only the guest has? Under what conditions?
6. Persistence: does the host's save keep a guest-built structure? Who writes the save?

Recommendation for the mod: what a guest-side `/paste` (force-placed, free) and `/undo` must do so that the host and
every other crewmate see the pieces, and the host's save keeps them, with the host NOT running the mod. If that is
impossible or risky, say exactly why, and what the closest workable approach is (for example, the host also running
the mod).
