# Dovetail

Chests and fences that line up. Place one, and the next snaps flush beside it or squarely
on top, with no nudging, no eyeballing, no gaps you only notice after you have built the wall.

Built against the installed game (0.221.12, Unity 6000.0.61, BepInEx 5.4.23.3, Harmony 2.9).
Single DLL, no asset bundle.

Named for chests because that is where it started; it now covers fences and stake walls
too. The name stays so existing configs keep working.

## How it works

A snap point in Valheim is nothing but a child transform tagged `snappoint`. Chests and
fences have none, which is exactly why they are fiddly to line up while walls and floors are
not. This mod measures each piece's own footprint at load and puts a point on all **eight
corners** of it, so a ghost's left corner landing on a placed piece's right corner is flush
adjacency and a bottom corner on a top corner is a clean stack. Fences are the exception and
get a ladder of points up each end, for reasons that come down to sloping ground.

Snapping makes the two points coincide, so the `Gap` setting pushes the corners *outward* by
half its value: each of the two pieces contributes half the space between them.

## Fences get a ladder instead

Eight corners give a fence exactly two heights to attach at: its base, and a full panel
up. Neither is any use for running a fence up a hill, which is most of what you do with a
fence. So a fence gets points up **both ends, at mid-depth, every `FenceLadderStep`
metres**, starting `FenceLadderBelow` metres under its own base so the next panel can step
down as well as up.

This idea is **MSchmoecker's**, from FenceSnap. It hand-places seven rungs 0.2m apart on
`wood_fence` plus one below the base. The difference here is that the rungs are derived
from the measured footprint rather than typed in, so a modded fence that nobody has
measured gets the same treatment from one config entry.

## What gets snapped

**The piece has to be something you can actually build** (`BuildablePiecesOnly`, on).
Having a `Piece` component is not the same as being buildable, and matching on components
alone handed points to 35 unplaceable prefabs, so a chest carried into a crypt snapped
itself to the loot chests standing there. The buildable set is read off the game's own piece
tables, so the Hammer, Hoe, Cultivator and any modded tool contribute and nothing needs
naming.

Then, three ways in, in descending order of confidence that snapping is wanted:

**Containers**, matched on components rather than names: anything with both a `Piece` and
a `Container`. Modded chests are covered without a list to maintain, and nothing rots when
a prefab is renamed. Ships are excluded; they hold cargo and are technically pieces, but
snapping a longship to a chest is not what anyone means by chaining storage.

**Fences**, matched by name, because nothing about a fence's components distinguishes it
from any other wall. The list is config rather than code, so a wrong or outdated entry is
something you fix without a build, and any configured name that matches no prefab is
**reported in the log at startup** rather than silently doing nothing.

**Everything else the developers never gave snap points to** (`SnapUnsnappedPieces`, off by
default). That set is mostly chests, fences and loose decoration, since walls, floors and
beams all ship with their own. It is off because it also catches chairs, banners and item
stands, where snapping tends to fight you rather than help.

Pieces that already have snap points of their own are always left alone.

Footprints are measured from collider data, and from only the geometry that will be standing
there rather than the damage states and destruction chunks inside the same prefab. `Verbose`
names every collider it measured from, which is how you find the culprit if a piece snaps at
the wrong distance.

Some pieces snap in one direction only, because their colliders sit on a layer the game's own
proximity search does not look at. Six vanilla prefabs are like this on a stock install and
Dovetail reports them at startup rather than rewriting somebody else's colliders.

## When derivation gets it wrong

One axis-aligned box cannot describe an L-shape or a piece whose geometry sits off centre,
and no amount of care will change that. `piece_dvergr_sharpstakes` measures
2.40 × 1.70 × 3.94 centred 0.35 off in x, so the corners of its box are nowhere near the
actual stakes. Both mods that came before this one ended up needing per-prefab data for the
same reason: FenceSnap hand-places its gate points, and ChestSnap moved to a YAML file of
them.

So `PointOverrides` takes exact points for named prefabs:

```
PointOverrides = piece_dvergr_sharpstakes: -0.5,0,2 | -0.5,0,-2 ; wooden_fence_1_gate: -2.4,0,0 | -2.4,1.17,0
```

Semicolons separate prefabs, a colon follows the name, pipes separate points, commas
separate one point's three coordinates. **Decimals must use a dot**, since a comma already
means something here. Naming a prefab is enough to get it snapped, so it does not also have
to be a container or a listed fence, and it skips the buildable filter as well: an explicit
name is more specific than any heuristic. `ExcludePrefabs` still wins, or it would not be an
escape hatch you could get back out of. `Gap` and the ladder do not apply, because a point
given by hand is used exactly as written. Names matching no prefab are reported at startup
like the fence list.

## Credit where it is due

This mod is written from scratch and shares no code with anything else, but it does not
pretend to have invented the idea. Several mods came first, and they fall into two groups:
the ones that also write snap points onto prefabs, which this cannot be run beside, and the
ones that solve the same problem some other way, which it can.

### The ones that add snap points

**FenceSnap**, by **MSchmoecker**. The ladder of points up each end of a fence is its idea,
and its hand-tuned numbers are also what caught a bug here: FenceSnap puts `wood_fence`
points at x = ±1.0, this mod's measured box said ±1.36, and one of those had to be wrong.
It was this one. Without a second opinion to compare against, that 0.72m gap would have
been found by building a fence.

**ChestSnap**, by **Frogger**. The original chest snapping mod, and the reason anyone knows
chests are worth snapping at all. Now at 0.1.1 and driven by a YAML file of snap point
data, so custom and modded containers are added by editing config rather than by waiting
for an update.

**Extra Snap Points Made Easy**, by **Searica**, is the broadest mod in this space at 2.0.5
and does far more than this one: manual snapping with keybinds to cycle points, grid
snapping, and points added by piece shape across beams, triangles, rectangles and roofs. If
you want the whole toolbox rather than chests and fences that line up, use it instead.

Do not run this alongside any of the three. It skips pieces that already have snap points,
so whichever registers first wins, which is a coin toss rather than a decision.

### The ones that work another way

**Snap Points Made Easy**, by **MathiasDecrock**, at 1.3.3. It cycles the points a piece
already carries, with separate keys for the ghost's point and the target's, so you pick the
one you want instead of aiming the mouse at it. It adds no points of its own, which means a
chest that has none stays exactly as unsnappable as it was. That also makes it the one mod
here that composes rather than competes: this puts the eight corners on, that picks between
them.

**PrecisePlacement**, originally by **Koosemose** and re-uploaded by **AcidWerks** at 1.1.1,
now marked deprecated. Free rotation about any axis, arrow-key nudging at a chosen step, and
copying a targeted piece's exact rotation and position onto the one you are holding. That
last one lines up a row of anything at all, snap points or not. It touches no prefab, so
there is nothing here for it to collide with.

## Building

```bash
dotnet build
```

Deploys to the repo-local `testprofile\`. Override with `-p:ProfileDir=...`, or build it
into the shared play profile with `valheim-own-profile\build-all.ps1`.


## No dependencies at all

Dovetail needs nothing but BepInEx. It does not use [Core](https://github.com/Ezomic/valheim-core)
and does not register with its version gate, so there is no handshake to fail and no other
mod it has to agree with. Install it on its own.

What that gives up is the gate itself. Nothing will tell you when two players are running
different builds of this, and it does add child transforms to shared prefabs, so a
disagreement passes unnoticed. Solo, none of that applies.

## Config

`BepInEx\config\ezomic.valheim.dovetail.cfg`

| Key | Default | What it does |
| --- | --- | --- |
| `SnapContainers` | `true` | Snap anything buildable that holds items |
| `SnapFences` | `true` | Snap the pieces named in `FencePrefabs` |
| `SnapUnsnappedPieces` | `false` | Snap every buildable piece with no snap points of its own |
| `BuildablePiecesOnly` | `true` | Only snap pieces that appear in a build menu |
| `PointOverrides` | | Exact points for named prefabs, replacing anything derived |
| `FencePrefabs` | see below | Comma-separated prefab names treated as fences |
| `ExcludePrefabs` | | Comma-separated names to leave alone whatever else matches |
| `Gap` | `0` | Metres left between chained pieces; `0` is flush |
| `FenceLadderStep` | `0.2` | Vertical spacing of a fence's rungs; `0` gives fences plain corners |
| `FenceLadderBelow` | `0.2` | How far under its own base a fence's lowest rung sits |
| `Verbose` | `false` | Log the measured footprint of every piece that gets points, and the colliders behind it |

`FencePrefabs` defaults to `wood_fence, piece_sharpstakes, piece_stakewall_blackwood,
piece_dvergr_sharpstakes, piece_dvergr_stake_wall`.

A value already written to the `.cfg` beats a new default in code. Change the `.cfg`, not
the source.

## Design notes

How the corner set is derived, why face centres were left out, what the measured footprint
had to exclude, and why the one-way pieces are reported rather than fixed:
[DESIGN.md](DESIGN.md).

## Author

Dovetail is an original mod by **Robbin Thijssen** (Thijssen Software).
Copyright (c) 2026 Robbin Thijssen. MIT licensed. See `LICENSE`.
