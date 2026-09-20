# Changelog

Notable changes to Sinka. Format follows [Keep a Changelog](https://keepachangelog.com),
and the mod uses [semantic versioning](https://semver.org).

## [Unreleased]

### Added

- **A chest can be set down on top of another chest** (`StackContainers`, on). Vanilla refuses
  this outright and no snap point could ever have changed it, because two separate rules are in
  the way. `Player.UpdatePlacementGhost` marks the ghost invalid whenever the piece you are
  *aiming at* has `WearNTear.m_supports` off, which every chest does - the same reason a torch
  cannot go on a chest. And `WearNTear.UpdateWear` runs
  `if (m_noSupportWear) { UpdateSupport(); if (!HaveSupport()) num = 100f; }`, where 100 is the
  whole of the piece's health: a chest that ends up unsupported is destroyed with its contents.
  The field name reads backwards; it means "no support, wear".
- Both are answered as narrowly as they can be. The support flag is lent to the chest you are
  aiming at for exactly one frame, and only while another chest is in hand, so every other
  placement test still runs unchanged. And a chest standing on a chest is counted as supported,
  rather than made to carry real support, so **a chest never becomes load-bearing** - a wall, a
  beam or a torch on a chest is still refused, which is vanilla's answer and not a limit of
  this. The integrity colours still paint a stacked chest as unsupported, which is cosmetic.
- Tested in game, both halves: a chest places on top of another chest, and it is still there
  with its contents after leaving the area and coming back. That second half is the one worth
  proving - placement only needs the ghost to go green, while the wear tick is the path that
  would have destroyed the chest.
- **On a server this can destroy a chest.** Support is worked out by whichever player's game
  owns the piece, and ownership follows whoever is nearby, so a player without Sinka finds a
  chest standing on nothing and destroys it with everything in it. Nothing client-side can
  prevent that. Safe in single player and on a server where everyone has the mod; on a mixed
  server, turn it off. It ships on, with the warning next to the setting and in the README.

- **`Sockets`: what sinks into what, and how much of it shows.** One entry per line of pieces,
  `piece, piece : target, target : metres showing`, and it replaces `SnapTorchesToPoles`,
  `TorchPrefabs`, `PolePrefabs` and `TorchStickOut`. Those four could describe exactly one
  pairing - every listed torch into every listed pole at one shared depth - and depth is the
  part that cannot be shared, since a torch's length and the height of its own fire are its
  own. They are left behind under `[Torches]` in a config file an older version wrote, where
  they do nothing.
- **Every standing torch, into seven poles**, out of the box: the wood one, the three iron ones
  and the mist demister, into wood, core wood, darkwood and ashwood poles. 1.1.0
  shipped one torch and two poles. Only the wood torch and the two wood poles have been played;
  the rest of the names come off the game's asset manifest, which lists what is on disk rather
  than what the game loads, so one may resolve to nothing and be named in the log at startup.
- **A depth left out of an entry is measured off the piece**, not defaulted: as deep as it can
  sink while the zone its fire spreads into stays clear of its own pole. That floor was already
  being computed to catch a value set too low; it turns out to be the right number to use when
  nobody has chosen one. On the wood torch it measures 0.21 against the 0.25 picked by eye, and
  two independent answers agreeing within a centimetre is why the three iron torches are left
  to measure - they come out at 0.32, clearing a bowl that sits 0.671 to 0.823 above the pivot.
  A piece with no `Fireplace` borrows the wood torch's 0.25, which is why the mist demister is
  typed at 0.27 instead: it has no fire to measure and its ball runs 0.283 to 0.515 under a
  mesh top of 0.549.
- **A pairing is now per entry.** A torch aimed at a pole somebody paired with a *different*
  torch comes away with nothing, where one shared pole list could not tell the two apart.
- Tested in game: a standing iron torch sinks into a wood pole at its measured 0.32, bowl
  clear of the pole, and the pole tiers past wood take a torch the same way. The wood torch is
  unchanged at the 0.25 it released with.
- **`GapOverrides`, `Gap` per prefab.** One number for everything made "chests flush" and
  "stakes a hand apart" the same decision. Same punctuation as `PointOverrides`, and a prefab
  with no entry still uses `Gap`. Two pieces of the same kind meet at exactly that gap; two
  different kinds meet at the average of theirs, because each piece contributes half.
- The startup log now lists every socket and how far it ends up showing, which for a measured
  depth is written down nowhere else, and `GapOverrides` joins the names-that-match-no-prefab
  check.

### Fixed

- **Sharp stakes could not be chained at all**, and the reason was a footprint measured from
  the wrong thing. `piece_sharpstakes` keeps its colliders as direct children of the root and
  gives `New` nothing but an LODGroup and meshes, so the collider search - which starts at
  `WearNTear.m_new` to keep damage states out - came away empty and fell through to mesh
  bounds. Those include the stakes leaning out in +z, so the piece measured 2.67 deep against
  2.40 wide. Deeper than it is wide means the ladder ran along z, which put its rungs on the
  panel's front and back faces instead of its two ends, and no two panels could ever meet.
  `piece_dvergr_sharpstakes` did the same at 3.81 deep.
- The search now widens to the whole prefab before giving up on colliders, cutting out
  `m_worn`, `m_broken` and `m_fragmentRoots` by asking `WearNTear` for them rather than by
  where the search started. What makes that safe is a layer filter: only `piece` and
  `piece_nonsolid`, the layers the game's own snap search looks on, so a hitbox, a pathfinding
  blocker or an effect area cannot stand in for the piece. This piece's `HIT AREA` is an Aoe
  box a metre and a half behind it and is exactly the collider that would have replaced one
  wrong answer with another.
- A footprint that ends up measured from meshes now says so under `Verbose`. It printed
  nothing at all before, which is what made this take a rip to find: the log showed a wrong
  box with no collider lines above it and no explanation of where it came from.
- **Four pieces change how they chain**, because they were on that mesh fallback and are now
  measured from their colliders, which is what this has always meant to do. Read off the game
  at load: `piece_chest_blackmetal` 2.31 x 1.04 x 1.52 becomes 2.06 x 0.92 x 1.35,
  `piece_chest_grausten` 1.72 x 1.18 x 1.22 becomes 1.31 x 0.72 x 1.13, `piece_chest_warderobe`
  1.68 x 2.68 x 1.11 becomes 1.35 x 2.09 x 0.85, and `piece_sharpstakes` 2.40 x 1.56 x 2.67
  becomes 1.80 x 0.84 x 1.51. Each chains tighter than it did in 1.1.0. `piece_chest`,
  `piece_chest_wood`, `piece_chest_private` and `piece_chest_barrel` are untouched - they
  already measured from colliders. Checked in game afterwards: the tighter boxes chain
  without the meshes running into each other, which was the risk of trusting a collider that
  sits inside what you can see.
- Tested in game: `piece_sharpstakes` now measures 1.80 x 0.84 x 1.51 with a ladder of 6 along
  x, and two panels chain end to end. `piece_dvergr_sharpstakes` moved from mesh guesswork to
  its real collider box, 2.40 x 1.70 x 3.94 centred 0.35 off in x - the numbers this file has
  quoted since 1.0.0 - and keeps its ladder along z, which is the axis it genuinely runs on.

### Changed

- `TorchPoles` is now `SocketPoints`, since the mechanism is "a piece sinks into another
  piece" and only the config names torches and poles. No behaviour rides on the rename.

## [1.1.0] - 2026-09-15

### Added

- **Torches on poles.** A standing wood torch aimed at the top of a wood pole snaps down into
  it, centred, with its top `TorchStickOut` metres (0.25) standing above the pole. The torch
  snaps only while aimed at a pole's top face, and aimed anywhere else it has no snap point,
  so a torch on a floor is placed exactly as before. Because the snap slides the torch most of
  its own length, the game's half-metre snap search is widened for that one case to a reach
  measured off the torch at load. `TorchStickOut` never goes below the point where the torch's
  fire-spread zone would reach its own pole. New `[Torches]` section: `SnapTorchesToPoles`,
  `TorchPrefabs`, `PolePrefabs`, `TorchStickOut`. Tested in game on 1m and 2m poles.

## [1.0.1] - 2026-09-12

### Changed

- Rewritten README. Same mod, clearer documentation: what it does and how to install it come
  first, then configuration, multiplayer behaviour, compatibility and troubleshooting. Every
  config table was checked against the plugin's own Config.Bind calls, so the settings,
  sections and defaults listed are the ones actually bound. No code changed in this release.

## [1.0.0] - 2026-08-27

The 0.9.0 build, proven in play and given its release number - chests snap flush beside
and atop each other, fences ladder up hillsides, and world-spawned loot stays unsnappable.
Renamed from Dovetail on the way: Sinka is the dovetail joint itself in the Scandinavian
tongues, beside the pack's other Old Norse names. Nothing was published under the old
name, so nothing breaks.

## [Unreleased]

### Changed

- **Core is gone entirely.** Sinka no longer references Core, declares it as a soft
  dependency, or registers with its version gate. It was already optional; now it is absent.
  Nothing here has to agree with anything running elsewhere, which is what lets this be
  played and versioned on its own. The gate is what is given up: nothing reports two ends
  running different builds of it.

### Fixed

- **The measured footprint was too big, and fences paid for it.** A prefab carries its
  damage states and destruction chunks inside itself (`WearNTear.m_new`, `m_worn`,
  `m_broken`, `m_fragmentRoots`), and every collider in all of them was being measured as
  one box. `wood_fence` came out 2.72 × 2.30 × 0.85 against a panel roughly 2.0 × 1.5, so
  two chained fences would have stood **0.72m apart**, the exact thing this mod exists to
  prevent. The footprint now starts at `WearNTear.m_new`, skips subtrees switched off via
  `activeSelf`, and skips colliders on their own rigidbody the way `WearNTear.SetupColliders`
  does.
- Credit for catching it goes to **MSchmoecker's FenceSnap**, whose hand-placed
  `wood_fence` points sit at x = ±1.0 against the ±1.36 measured here. Two sources
  disagreeing is what made it findable without building a fence first.

### Added

- **Only buildable pieces are snapped** (`BuildablePiecesOnly`, on). Having a `Piece`
  component is not the same as being placeable: 35 of the 46 prefabs previously snapped were
  dungeon loot chests, pots and other things you cannot build. Snap points work both ways, so
  a chest carried into a crypt snapped to the loot chests standing there. The set is read off
  the game's own piece tables via `ItemDrop.m_itemData.m_shared.m_buildPieces`, so every tool
  including modded ones contributes and nothing needs naming.
- **`PointOverrides`, exact points for named prefabs.** One axis-aligned box cannot describe
  an L-shape or an off-centre piece, and `piece_dvergr_sharpstakes` is the proof at
  2.40 × 1.70 × 3.94 centred 0.35 off in x. Both earlier mods needed per-prefab data too:
  FenceSnap hand-places its gate points and ChestSnap keeps a YAML file of them. Naming a
  prefab is enough to get it snapped and skips the buildable filter, `ExcludePrefabs` still
  wins, and `Gap` and the ladder do not apply to a point given by hand.
- **A warning for pieces that can only snap one way.** `FindClosestSnapPoints` takes the
  ghost's own points straight off the piece being placed, but finds *neighbours* through
  `LayerMask.GetMask("piece", "piece_nonsolid")`. A piece whose colliders sit on another
  layer therefore snaps fine while you place it, and cannot be snapped to once it stands.
  Six vanilla prefabs are like this, the three gifts and the three pots, so it is one line
  at startup rather than one per piece. ChestSnap and FenceSnap both rewrite such colliders
  onto the piece layer; this deliberately does not, because that changes what they collide
  with and the piece belongs to whoever shipped it. It says so instead.
- **A ladder of snap points up each end of a fence**, so a fence line can follow sloping
  ground. Eight corners give a fence two heights to attach at, its base and a full panel up,
  and a hill needs the heights in between. Rungs run every `FenceLadderStep` metres
  (default 0.2) from `FenceLadderBelow` under its base (default 0.2) to its top, at
  mid-depth on both ends, along whichever horizontal axis is longer.
- This is **FenceSnap's idea**, taken deliberately. The difference is that the rungs come
  off the measured footprint rather than being typed in per prefab, so a modded fence is one
  config entry rather than a code change. `FenceLadderStep = 0` restores plain corners.
- It knowingly gives up the uniform point set: a fence rung and a chest corner can now pair
  and land half a piece out of line. Fences are opt-in by name, which contains it, and
  FenceSnap made the same call.
- `Verbose` now names every collider a footprint was measured from, not just the result.
  Finding which collider inflates a box previously took a rip.
- **Applying is now tracked per world rather than per process.** It keyed off a static bool,
  which answers yes for the rest of the session once set, while logging out to the menu and
  back in tears down `ZNetScene` and builds a new one. That is the failure CLAUDE.md records
  as having silently destroyed a built piece elsewhere, and the fix is the same: ask the
  world, do not answer from a field. If prefab assets do keep their points across a reload
  the second pass simply finds them under "already had their own", and the log now says
  which happened.

## [0.9.0] - 2026-08-16

Not released yet. Everything below is written, deployed and confirmed loading, but the
snapping has only been loaded and not yet played. The version stays under 1.0 until it has,
and 1.0.0 will be the release.

### Snapping

- **Chests and fences line up.** Place one and the next snaps flush beside it or squarely on
  top, with no nudging and no gaps you find after the wall is built.
- Each piece's own footprint is measured at load and given a snap point on **all eight
  corners** of it.
- **Corners rather than face centres**, and the set is deliberately uniform. The game snaps
  by making the closest pair of points *coincide*, so a corner meeting a corner is flush
  adjacency and a corner meeting a face centre would put a piece half its own length out of
  line.
- `Gap` pushes the corners **outward** by half its value, because each of the two pieces
  contributes half the space between them. Insetting them, which is the intuitive reading,
  makes pieces overlap by exactly the same arithmetic.

### What gets snapped

- **Containers**, matched on components rather than names: anything with both a `Piece` and
  a `Container`. Modded chests are covered without a list to maintain. Ships are excluded.
- **Fences**, matched by name, because nothing about a fence's components distinguishes it
  from any other wall. The list is config rather than code, and any configured name matching
  no prefab is **reported in the log at startup** rather than silently doing nothing.
- **Everything the developers never gave snap points to**, off by default. That set is mostly
  chests, fences and loose decoration, but it also catches chairs, banners and item stands,
  where snapping fights you rather than helps.
- Pieces that already have snap points of their own are always left alone.

### Correctness

- Footprints are read from collider **data** rather than from `Collider.bounds`. Prefabs sit
  inactive in `ZNetScene`, where world-space bounds have never been computed and read as
  zero, exactly when they are wanted.
- Loads on dedicated servers.
- **Core is optional.** Installed, it is used: the mod joins Core's version gate, which
  compares mod versions and build ids on connect and refuses a client that disagrees. That
  matters here because this adds child transforms to shared prefabs. Absent, nothing is
  degraded and the mod runs standalone, so installing Sinka no longer pulls Core in with
  it. A hard dependency would have been worse than no gate at all, since a missing hard
  dependency means the plugin never loads.

### Naming

Named for chests because that is where it started. It now covers fences and stake walls too,
and the name stays so existing configs keep working.
