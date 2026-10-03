# Sinka

Sinka adds snap points to the vanilla pieces that ship without any, mainly chests, fences
and stake walls. Place one and the next snaps flush beside it or squarely on top, the same
way walls and floors already do.

Named for the dovetail joint. It started as a chest mod and now covers fences too; the name
stayed so existing config files keep working.

## Features

- Eight snap points, one on each corner of a piece's own measured footprint.
- Fences and stake walls get a ladder of points up each end instead, so a fence line can
  follow sloping ground.
- A standing torch placed on a pole snaps down into it, centred, with only its head showing
  above the top. Which torches and which poles is a config list, and how much of the torch
  still shows is measured off its own fire when you do not say. While it stands there it
  also protects the pole from damage (`ProtectFrom`).
- `Gap` is per prefab where you want it to be, so chests can sit flush in the same world
  where stakes stand a hand apart.
- A chest can be set down on top of another chest, which vanilla refuses outright. Read the
  server warning under [Stacking chests](#stacking-chests) before using it on a server.
- Containers are matched by component, so modded chests are covered without naming them.
- Fences are matched by a config list you can extend.
- `PointOverrides` takes exact coordinates for a named prefab when the derived box is wrong.
- Optional: snap every buildable piece the game never gave points to.
- Pieces that already have snap points, vanilla or from another mod, are left alone.
- Nothing is added to prefabs you cannot build, so dungeon loot chests and pots stay
  unsnappable.

## How the snapping works

A snap point in Valheim is a child transform tagged `snappoint`, and that is all the game
needs. While you hold a placement ghost, `FindClosestSnapPoints` picks the closest pair of
points within 0.5m, one on the ghost and one on a placed piece nearby, then moves the ghost
so the two points become the same point.

That rule is why Sinka uses corners and nothing else. A ghost's left corner landing on a
placed chest's right corner is flush adjacency, and a bottom corner landing on a top corner
is a clean stack. Mixing corners with face centres would let a chest snap half its own
width out of line.

`Gap` pushes the corners outward by half its value, since both pieces contribute half the
space between them. `Gap = 0.1` leaves 10cm between two chained chests.

Footprints are measured at load from collider data, and only from the geometry that will
actually be standing there. A built piece carries its damage states and destruction chunks
in the same prefab, and measuring all of them at once inflates the box: `wood_fence` comes
out 2.72 x 2.30 x 0.85 that way against a panel of roughly 2.0 x 1.5, which would leave
chained fences standing 0.72m apart.

### The fence ladder

Eight corners give a fence two heights to attach at, its base and a full panel up. Neither
helps you run a fence up a hill, so a fence gets points up both ends instead, at mid-depth,
every `FenceLadderStep` metres, starting `FenceLadderBelow` metres under its own base so
the next panel can step down as well as up. The rungs run along whichever horizontal axis
of the piece is longer.

The ladder is capped at 24 rungs per piece. A tall piece with a small step hits that cap and
logs a warning saying where the ladder stopped.

### Torches on poles

Aim a standing torch at the top of a pole and it snaps down into the pole, centred on it,
with only its head standing above the top. Build the pole first; the torch goes on afterwards.

What pairs with what is the `Sockets` setting, one entry per line of pieces:

```
piece, piece : target, target : metres showing ; next entry
```

Semicolons separate entries, colons separate an entry's three fields, commas separate names
within a field. Out of the box that is the four standing torches and the mist demister, into
the seven poles that exist and can be built: wood, core wood, darkwood and ashwood. The
dvergr pole is a prefab you cannot place, so it is not among them.

The torch snaps only while the crosshair is on the top face of a pole it is paired with, and
only into that pole. Aimed at anything else, a floor, a wall, the side of a pole, the ground
beside one, a torch has no snap point at all and places exactly as it does without Sinka.
Nothing ever snaps to a torch. Hold the place-without-snapping key to set a torch on a pole
top without sinking it.

The snap has to slide the torch down most of its own length, and the game only looks half a
metre around the ghost for something to snap to. So for a torch aimed at a pole top, Sinka
widens that search to the distance it measures off the torch at load.

**How much shows is optional, and better left out.** A burning torch spreads fire into a zone
around its flame, and sunk deep enough that zone reaches into its own pole, which in the
Ashlands burns the pole out from under it. That gives every torch a floor of its own, measured
off its own fire, and an entry with no third field uses exactly that: as deep as this torch
can go and no further. The wood torch measures 0.21 that way against the 0.25 chosen by eye
for it, which is why the three iron ones are left to measure - they come out at 0.32, enough
for the bowl and a little shaft. A typed value below the floor is raised to it with a warning
in the log, and one past the torch's own length is capped, since a socket below its tip would
leave it floating above the pole.

The mist demister is typed at 0.27 instead, because it carries no fire to measure and its ball
wants clearing whole.

A target needs snap points of its own, because the highest of them is what the torch lands on.
That is what covers a pole of any length without naming its length, and the startup log names
any target that has none, is in no build menu, or matches no prefab at all.

**A torch protects the pole it stands in.** While a paired torch is snapped into a pole, hits
on that pole do nothing: a troll, a raider, an axe, the cinders of the Ashlands. Take the torch
away and the pole is as breakable as any other, because nothing is remembered on either piece.
Sinka asks the world each time the pole is hit, and a torch counts when its socket sits on the
pole's top point, which is where the snap puts it, so it survives a logout and a reload with no
state to lose. A torch set on top with the place-without-snapping key protects nothing, and the
torch itself is not protected.

`ProtectFrom` chooses how much. `All` (the default) ignores every hit on the pole, so only
taking it down by hand removes it. `Fire` ignores only fire: Ashlands cinders, burning and the
fire share of a mixed hit, while creatures and players still break the pole. `Off` turns the
protection off and leaves the pairing as it was. It covers every pole named in `Sockets`. It
never stops the wear that destroys a piece with nothing holding it up, so a torched pole still
falls if you remove what it stands on. The game's owner of the pole makes the call, so on a
server a player running without Sinka who happens to own the pole at that moment will not
honour it.

**Only the wood torch and the two wood poles have been played.** The other names come off the
game's asset manifest, which lists what is on disk rather than what the game loads, so one may
resolve to nothing - it is named in the log at startup if so, and costs nothing else.

### Picking a snap point by hand

Q and E cycle the ghost's snap point while you are holding a piece (`TabLeft` / `TabRight`
in the keybinds, so they follow a rebind), and the chosen point's name shows in the middle
of the screen. Sinka names its points by position for that reason:
`snap_top-front-left` for corners, `snap_left-y0.60` for a fence rung, `snap_into-pole` for a
torch, `snap_custom1` for a point you supplied through `PointOverrides`.

## Stacking chests

Vanilla refuses a chest on a chest, and no snap point could ever have changed that. Two rules
are in the way, both in the game's own code:

- **Placing.** The placement ghost is invalid whenever the piece you are *aiming at* has
  `WearNTear.m_supports` off. Every chest ships it off, which is the same reason a torch
  cannot go on a chest either.
- **Staying.** The wear tick reads `if (m_noSupportWear) { UpdateSupport(); if (!HaveSupport())
  num = 100f; }`, and that 100 is the whole of the piece's health. A chest that ends up
  unsupported is destroyed with everything inside it. Chests on the ground are safe because
  terrain hands out full support.

`StackContainers` answers both, and each as narrowly as it can be. The support flag is turned
on for one frame on the chest you are aiming at, and only while another chest is in hand; and
a chest standing on a chest is counted as supported so the wear tick leaves it alone. A chest
does not become load-bearing: a wall, a beam or a torch on a chest is still refused.

The build-integrity colours still show a stacked chest as unsupported. That is only the
colour - the chest is exempt from the wear it usually warns about.

### On a server, read this first

Whether a piece is supported is worked out by whichever player's game owns it, and ownership
follows whoever is nearby. **A player who does not have Sinka works it out the vanilla way,
finds a chest standing on nothing, and destroys it with everything in it.**

There is no fix for that from here, because the rule runs on their machine and from their copy
of the prefab. A stack is safe in single player, and on a server where every player has this
mod - through a modpack that carries it, for instance. On a server where some players do not,
set `StackContainers = false` and you lose nothing but the feature.

## What gets snapped

A piece has to be something you can actually build. The buildable set is read off the game's
own piece tables, so the Hammer, Hoe, Cultivator and any modded tool with its own table all
contribute and nothing needs listing. Turning `BuildablePiecesOnly` off drops that filter
and snaps anything with a `Piece` component, which includes dungeon loot chests and pots.

Past that filter there are three ways in:

- **Containers** (`SnapContainers`, on): anything with both a `Piece` and a `Container`.
  Ships are excluded.
- **Fences** (`SnapFences`, on): the prefab names in `FencePrefabs`. Nothing about a fence's
  components distinguishes it from any other wall, so this one needs names.
- **Everything else with no points of its own** (`SnapUnsnappedPieces`, off): mostly chests,
  fences and loose decoration, since walls, floors and beams ship with their own. It also
  catches chairs, banners and item stands, where snapping tends to get in the way.

`ExcludePrefabs` wins over all of it, including `PointOverrides`. Prefab names are matched
case-insensitively everywhere.

## Installation

1. Install [BepInEx 5.4.2350](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/).
   BepInEx 5 only, not 6.
2. Install Sinka from [Thunderstore](https://thunderstore.io/c/valheim/p/Ezomic/Sinka/) with
   a mod manager, or drop `Sinka.dll` into `BepInEx\plugins\Sinka\`.

No other dependencies. Sinka does not use Longhouse Core.

Snapping happens on the client while you place a piece, so only the players who want it need
it. A dedicated server gains nothing from having it installed, and loses nothing either.

## Configuration

`BepInEx\config\ezomic.valheim.sinka.cfg`, written on first run.

| Key | Default | What it does |
| --- | --- | --- |
| `SnapContainers` | `true` | Snap anything buildable that holds items |
| `SnapFences` | `true` | Snap the pieces named in `FencePrefabs` |
| `SnapUnsnappedPieces` | `false` | Snap every buildable piece with no snap points of its own |
| `BuildablePiecesOnly` | `true` | Only snap pieces that appear in a build menu |
| `FencePrefabs` | see below | Comma-separated prefab names treated as fences |
| `ExcludePrefabs` | empty | Comma-separated prefab names to leave alone, whatever else matches |
| `PointOverrides` | empty | Exact points for named prefabs, replacing anything derived |
| `Gap` | `0` | Metres left between two chained pieces. `0` is flush; negative values are ignored |
| `GapOverrides` | empty | `Gap` for named prefabs, leaving the rest on `Gap` |
| `FenceLadderStep` | `0.2` | Vertical spacing of a fence's rungs, in metres. `0` gives fences plain corners |
| `FenceLadderBelow` | `0.2` | How far under its own base a fence's lowest rung sits, in metres |
| `SnapSockets` | `true` | Sink a piece listed in `Sockets` into the top of one of its targets |
| `Sockets` | see below | What sinks into what, and how much of it shows |
| `ProtectFrom` | `All` | What a snapped torch keeps its pole safe from: `All`, `Fire` or `Off` |
| `StackContainers` | `true` | Let a chest be placed on another chest, and stay there. Read the server warning |
| `Verbose` | `false` | Log the measured footprint of every piece that gets points, and the colliders behind it |

`SnapSockets`, `Sockets` and `ProtectFrom` are in the `[Sockets]` section, `StackContainers` in `[Stacking]`,
`Verbose` in `[Diagnostics]`, and everything else in `[Snapping]`.

`FencePrefabs` defaults to `wood_fence, piece_sharpstakes, piece_stakewall_blackwood,
piece_dvergr_sharpstakes, piece_dvergr_stake_wall`. Names that match no prefab are listed in
the log at startup.

BepInEx writes every setting to the `.cfg` on first run, and the saved value beats a new
default in a later version. If a setting looks like it is being ignored, edit the `.cfg`.

**Upgrading from 1.1.0:** `Sockets` replaces `SnapTorchesToPoles`, `TorchPrefabs`,
`PolePrefabs` and `TorchStickOut`. Those four are left behind under a `[Torches]` section in
a config file an older version wrote, where they now do nothing. Deleting that section is
tidiness rather than repair, and a torch depth you had tuned needs writing into the `Sockets`
entry to keep it.

### Sockets

```
Sockets = piece_groundtorch_wood : wood_pole, wood_pole2 : 0.25 ; piece_groundtorch, piece_groundtorch_blue : wood_pole
```

Semicolons separate entries, colons separate an entry's three fields, and commas separate
names within a field. The third field is metres of the piece left showing above its target,
and leaving it out measures the deepest this piece can sink without its own fire reaching the
pole. Decimals must use a dot. A malformed entry is reported in the log and dropped, and the
rest of the config still loads; a depth that does not parse costs only the depth, and that
entry measures instead.

Naming a piece twice is the later entry winning. See [Torches on poles](#torches-on-poles)
for what the pairing actually does and what the defaults are.

### GapOverrides

```
GapOverrides = piece_chest_wood: 0 ; piece_sharpstakes: 0.15
```

One `Gap` for everything makes "chests flush" and "stakes a hand apart" the same decision.
Semicolons separate prefabs and a colon follows the name, as in `PointOverrides`. A prefab
with no entry uses `Gap`, negative values are ignored, and a name that matches no prefab is
reported in the log at startup.

Because snapping makes two points coincide, each piece contributes half the space between
them - so two *different* pieces chained together meet at the average of their two gaps.

### PointOverrides

One axis-aligned box cannot describe an L-shape or a piece whose geometry sits off centre.
`piece_dvergr_sharpstakes` measures 2.40 x 1.70 x 3.94 centred 0.35 off in x, so the corners
of its box are nowhere near the actual stakes. `PointOverrides` replaces the derived points
with exact ones:

```
PointOverrides = piece_dvergr_sharpstakes: -0.5,0,2 | -0.5,0,-2 ; wooden_fence_1_gate: -2.4,0,0 | -2.4,1.17,0
```

Semicolons separate prefabs, a colon follows the prefab name, pipes separate points, and
commas separate the three local coordinates of one point. Decimals must use a dot, because a
comma already means something here.

Naming a prefab is enough to get it snapped: it does not also have to be a container or a
listed fence, and it skips the buildable filter. `Gap` and the fence ladder do not apply,
since a point given by hand is used exactly as written. A malformed entry is reported in the
log and dropped, and the rest of the config still loads.

## Multiplayer

Snap points are added to prefabs in your own game, nothing is sent over the network, and
world data is unaffected. A player without Sinka sees the pieces exactly where you placed
them, they just have more work to do lining up their own.

**`StackContainers` is the exception, and it is not a small one.** Support is worked out by
whoever owns a piece, ownership follows whoever is nearby, and a player without Sinka
destroys a stacked chest and its contents. See [Stacking chests](#stacking-chests). Everything
else here stays entirely on your own machine, apart from `ProtectFrom`: a pole's damage is
decided by whoever owns it, so the protection holds only while a player running Sinka does.

Sinka does not register with Longhouse Core's version check, so nothing tells you when two
players are running different builds of it. In practice that only shows up as one player
finding a piece harder to align than the other did.

## Compatibility

Sinka skips any piece that already has snap points, so it will not fight another mod for the
same prefab, but whichever one registers first wins and the order is not something you
control. Do not run it alongside other mods that add snap points to prefabs:

- **FenceSnap** by MSchmoecker
- **ChestSnap** by Frogger
- **Extra Snap Points Made Easy** by Searica

Extra Snap Points Made Easy is much broader than this mod: manual point cycling with
keybinds, grid snapping, and points derived per piece shape across beams, triangles,
rectangles and roofs. If you want the whole toolbox rather than chests and fences that line
up, use that instead.

These work alongside Sinka, because they do not touch prefabs:

- **Snap Points Made Easy** by MathiasDecrock cycles the points a piece already has, with
  separate keys for the ghost's point and the target's. It adds none of its own, so Sinka
  supplies the points and it picks between them.
- **PrecisePlacement**, originally by Koosemose and re-uploaded by AcidWerks, now marked
  deprecated. Free rotation, arrow-key nudging, and copying a targeted piece's rotation and
  position onto the one you are holding.

## Troubleshooting

**A piece snaps at the wrong distance.** Set `Verbose = true` and restart. Every piece that
gets points logs its measured footprint followed by the colliders it was measured from, so
you can see which collider is inflating the box. If the box cannot describe the piece, give
it exact points through `PointOverrides`.

**A fence name in the config does nothing.** Check the startup log for a
`FencePrefabs names that match no prefab` warning. The same check runs on `PointOverrides`,
`GapOverrides` and both halves of `Sockets`, and a listed target with no snap points or no
place in a build menu gets a warning of its own.

**A torch will not snap into a pole.** The pole has to be a target of that torch's own
`Sockets` entry - a pole paired with a different torch is not enough. The startup log lists
every socket and how far it ends up showing; with `Verbose = true` it also names each socket's
height and how far it reaches.

**A torch sinks too far, or not far enough.** Give its entry a third field. With none, the
depth is the deepest its own fire allows, which is the safe answer rather than the pretty one.

**A piece snaps while I place it, but nothing will snap to it afterwards.** The game finds
nearby pieces with a search limited to the `piece` and `piece_nonsolid` layers, and a piece
whose colliders sit elsewhere is invisible to it. Sinka lists these in one warning at
startup. Rewriting another mod's or the game's colliders onto a different layer changes what
they collide with, so Sinka reports it rather than fixing it.

**"No piece tables found after 900 frames".** The buildable filter could not be built, so
Sinka fell back to snapping anything with a `Piece` component, loot chests and pots
included. Usually means another mod delayed or replaced ObjectDB.

**Snap points did not appear at all.** They are added to prefabs when the scene loads, so
a config change needs a restart to the main menu at minimum. Check the startup line reading
`Added snap points to N piece(s)`.

## Bug reports

Post in the [Discord](https://discord.gg/hJzAVaZ5wb) or open an issue at
[github.com/Ezomic/valheim-sinka](https://github.com/Ezomic/valheim-sinka). Useful to
include:

- `BepInEx\LogOutput.log`, ideally with `Verbose = true`.
- Your `ezomic.valheim.sinka.cfg`.
- The prefab name of the piece involved. The log lines from `Verbose` give you these.
- Whether you were in single player or on a server, and any other snapping or building mods
  installed.

## Discord

The [Discord](https://discord.gg/hJzAVaZ5wb) is where mod information, updates, support, bug
reports and compatibility questions go.

There's also a small EU server running the Longhouse pack if you want somewhere to play.
Details are in the Discord.

## Building

```bash
dotnet build
```

Targets net462 and references the game's managed DLLs from the default Steam path. Output
deploys to the repo-local `testprofile\`; override with `-p:ProfileDir=...`, or build it into
the shared play profile with `own-profile\build-all.ps1`.

## Design notes

How the corner set is derived, why face centres were left out, what the measured footprint
has to exclude, and why the one-way pieces are reported rather than fixed: [DESIGN.md](DESIGN.md).

Credit where it is due: the fence ladder is MSchmoecker's idea, from FenceSnap. Sinka derives
its rungs from the measured footprint instead of hand-placing them, which is how a modded
fence gets the same treatment from one config entry. FenceSnap's hand-tuned numbers are also
what caught a measuring bug here, since it puts `wood_fence` points at x = 1.0 where Sinka's
inflated box said 1.36.

## Author

Sinka is an original mod by Robbin Thijssen (Thijssen Software). Copyright (c) 2026 Robbin
Thijssen. MIT licensed, see `LICENSE`.

## Part of Longhouse

Sinka ships in the [Longhouse](https://thunderstore.io/c/valheim/p/Ezomic/Longhouse/) modpack,
which pins the exact versions of the Ezomic mods used on the Ezomic setup. It behaves exactly
the same installed on its own.
