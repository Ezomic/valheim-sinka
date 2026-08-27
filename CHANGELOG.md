# Changelog

Notable changes to Sinka. Format follows [Keep a Changelog](https://keepachangelog.com),
and the mod uses [semantic versioning](https://semver.org).

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
