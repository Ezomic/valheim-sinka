# Sinka design notes

Why it works the way it does, and how it is built. None of this is needed to play; for that
see the [README](README.md).

## How it works

A snap point in Valheim is nothing but a child transform tagged `snappoint`, and that is the
entirety of the game's side of it, in `Piece.GetSnapPoints`. Chests and fences have none,
which is exactly why they are fiddly to line up, while walls and floors are not.

This mod measures each piece's own footprint at load and puts a snap point on all **eight
corners** of it. Fences are the exception and get a **ladder** of points up each end, for
reasons that come down to sloping ground; see below.

Corners rather than face centres, because of how the game actually snaps.
`Player.UpdatePlacementGhost` calls `FindClosestSnapPoints`, which picks the globally
closest pair of points, one on the ghost and one on a nearby placed piece, within 0.5m, and
then moves the ghost so **the two points become the same point**. Under that rule:

- a ghost's left corner landing on a placed piece's right corner is flush adjacency
- a ghost's bottom corner landing on a top corner is a clean stack
- for a long thin fence, the same rule chains panels end to end

Mixing in face centres would let a corner snap to a centre and put a piece half its own
length out of line, so the set is deliberately uniform.

Because snapping makes the two points coincide, the `Gap` setting pushes the corners
*outward* by half its value: each of the two pieces contributes half the space between
them. Insetting the points, which is the intuitive reading, would make them overlap by
exactly the same arithmetic.

## What the fence ladder costs

It does mean two different point layouts exist, which the uniform corner set was meant to
avoid: a fence rung and a chest corner can pair up and put the two half a piece out of
line. That is contained by fences being opt-in by name, and it is the same trade FenceSnap
made. Sloping ground is worth more than that edge. Set `FenceLadderStep = 0` to go back to
plain corners.

## Buildable is not the same as having a Piece

**the piece has to be something you can actually build**
(`BuildablePiecesOnly`, on). Having a `Piece` component is not the same as being buildable,
and the gap is not small. Matching on components alone gave snap points to 35 prefabs out
of 46 that you can never place: 24 `TreasureChest_*` variants, nine pots, two loose loot
chests. The cost was not the wasted transforms. Snap points work both ways, so a chest
carried into a crypt snapped itself to the loot chests already standing there, and a
barrow's pottery became a snap target for a wall.

The set is read off the game's own piece tables rather than guessed at. Every buildable
piece sits in the `PieceTable` of some tool, via `ItemDrop.m_itemData.m_shared.m_buildPieces`,
so the Hammer, Hoe, Cultivator and any modded tool with its own table all contribute and
nothing needs naming.

## Measuring a footprint

Footprints are read from collider *data* (`BoxCollider.center`/`size`, mesh bounds) rather
than from `Collider.bounds`. Prefabs sit inactive in `ZNetScene`, and the world-space bounds
of an inactive collider are not reliable; transforms work regardless of active state, so the
corners are carried across by hand instead.

The footprint is also measured from **only the geometry that will be standing there**. A
built piece carries its damage states and its destruction chunks inside the same prefab.
`WearNTear` holds `m_new`, `m_worn` and `m_broken` as separate subtrees, plus
`m_fragmentRoots`, and asking for every collider in the prefab returns all of them at
once. Measured that way `wood_fence` came out **2.72 × 2.30 × 0.85**, which is 0.7m wider
and 0.8m taller than the panel you actually place, and two chained fences would have stood
**0.72m apart**. So the search starts at `WearNTear.m_new` when there is one, skips
subtrees switched off via `activeSelf`, and skips colliders hanging off their own rigidbody
the way `WearNTear.SetupColliders` does. `Verbose` now names every collider it measured
from, which is how you find the culprit if a piece still snaps at the wrong distance.

## Pieces that only snap one way

`FindClosestSnapPoints` reads the ghost's own points straight off the piece you are placing,
with no layer check at all, but it finds *nearby* pieces with
`Physics.OverlapSphereNonAlloc(..., s_pieceRayMask)`, and that mask is
`LayerMask.GetMask("piece", "piece_nonsolid")`. A piece whose colliders sit on another layer
is invisible to that search.

So those pieces snap in one direction only. Placing one lines it up against its neighbours
exactly as it should; what fails is the reverse, where something else tries to snap to it
after it is standing. Six vanilla prefabs are like this on a stock install, the three Yule
gifts and the three pots, so this is reported as one line at startup rather than one per
piece.

Sinka says so in the log and leaves it there. Both ChestSnap and FenceSnap carry a
`FixPiece` that rewrites every collider onto the piece layer, and that is a real fix, but it
changes what those colliders collide with. That is too large a side effect to apply silently
to somebody else's content, and the piece belongs to whoever shipped it.

## What to check

1. Place a chest, then bring up a second. It should snap flush alongside, and stack when
   aimed above.
2. Place a wood fence and chain a second onto its end. Then chain one **up a slope**, which
   is what the ladder is for.
3. Same for sharp stakes.
4. **Check the measured fence in the log.** With `Verbose = true`, `wood_fence` should now
   report a footprint close to 2.0m wide rather than the 2.72m it reported before, which
   would have left a 0.72m gap between panels. If it still says 2.72, the inflation is not
   coming from the damage states and the collider lines underneath it will say what it is.
5. **Read the startup log** for a `FencePrefabs names that match no prefab` warning. The
   default list is inferred from the asset manifest, so an entry may need correcting.
6. **Tab** cycles snap points manually; the HUD names them, which is why they are named by
   position (`snap_top-front-left`) and a fence's rungs by height (`snap_left-y0.60`).
7. Set `Verbose = true` once and read the measured footprints if a piece snaps at the wrong
   distance. Each footprint is now followed by the colliders it was measured from.
