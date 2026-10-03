using System;
using System.Collections.Generic;
using System.Globalization;
using BepInEx.Configuration;
using UnityEngine;

namespace Sinka
{
    internal static class SinkaConfig
    {
        /// <summary>
        /// Vanilla fences and stake walls, which ship with no snap points of their own.
        ///
        /// A list is unavoidable here - unlike a chest, nothing about a fence's components
        /// tells you it is a fence rather than any other wall. Keeping it in config at
        /// least means a wrong or outdated entry is something you can fix without a build,
        /// and unresolved names are logged rather than quietly doing nothing.
        /// </summary>
        private const string DefaultFences =
            "wood_fence, piece_sharpstakes, piece_stakewall_blackwood, "
            + "piece_dvergr_sharpstakes, piece_dvergr_stake_wall";

        /// <summary>
        /// Every pole a torch can stand in, across the building tiers. Only the two wood ones
        /// have been played; the rest were read off the game's asset manifest and then checked
        /// against a running game, which is the check that matters - the manifest lists what is
        /// on disk rather than what the game loads. wood_logpole2 and wood_logpole4 came off it
        /// and resolve to nothing, and piece_dvergr_pole resolves but is in no build menu, so a
        /// player can never stand one up. All three are dropped rather than left to warn at
        /// every startup.
        /// </summary>
        private const string DefaultPoles =
            "wood_pole, wood_pole2, wood_pole_log, "
            + "darkwood_pole, darkwood_pole4, ashwood_pole_1m, ashwood_pole_2m";

        /// <summary>
        /// The wood torch keeps the 0.25 it was released with, since that is the one that has
        /// been looked at in game. The three iron standing torches leave the depth out and get
        /// one measured off their own fire, which is what a missing third field means: sink it
        /// as far as it can go without its flame reaching down into the pole. That comes to
        /// 0.32 for all three - their bowls sit between 0.671 and 0.823 above the pivot, so the
        /// bowl clears the pole with a little shaft to spare. On the wood torch the same sum
        /// gives 0.21 against the 0.25 picked by eye for it, and two answers agreeing within a
        /// centimetre is the reason to trust it on a torch nobody has stood on a pole yet.
        ///
        /// The mist demister is the exception and is typed: it carries no Fireplace at all, so
        /// there is no fire to measure. Its ball runs 0.283 to 0.515 under a mesh top of 0.549,
        /// which puts the whole ball above the pole at 0.27.
        /// </summary>
        private const string DefaultSockets =
            "piece_groundtorch_wood : " + DefaultPoles + " : 0.25 ; "
            + "piece_groundtorch, piece_groundtorch_blue, piece_groundtorch_green : "
            + DefaultPoles + " ; "
            + "piece_groundtorch_mist : " + DefaultPoles + " : 0.27";

        private const string DefaultCentreProps = "piece_brazierfloor01, piece_brazierfloor02";

        public static ConfigEntry<bool> SnapContainers;
        public static ConfigEntry<bool> SnapFences;
        public static ConfigEntry<bool> SnapUnsnappedPieces;
        public static ConfigEntry<bool> BuildablePiecesOnly;
        public static ConfigEntry<string> FencePrefabs;
        public static ConfigEntry<string> ExcludePrefabs;
        public static ConfigEntry<string> PointOverrides;
        public static ConfigEntry<float> Gap;
        public static ConfigEntry<string> GapOverrides;
        public static ConfigEntry<float> FenceLadderStep;
        public static ConfigEntry<float> FenceLadderBelow;
        public static ConfigEntry<bool> SnapSockets;
        public static ConfigEntry<string> Sockets;
        public static ConfigEntry<bool> CentreOnSurface;
        public static ConfigEntry<string> CentreProps;
        public static ConfigEntry<bool> StackContainers;
        public static ConfigEntry<bool> Verbose;

        public static void Bind(ConfigFile config)
        {
            SnapContainers = config.Bind("Snapping", "SnapContainers", true,
                "Snap anything buildable that holds items. Matched on components, so "
                + "modded chests are covered without naming them.");

            SnapFences = config.Bind("Snapping", "SnapFences", true,
                "Snap the fences and stake walls listed in FencePrefabs.");

            SnapUnsnappedPieces = config.Bind("Snapping", "SnapUnsnappedPieces", false,
                "Snap every buildable piece the game never gave snap points to. Catches "
                + "modded pieces for free, but also chairs, banners and item stands, where "
                + "snapping tends to fight you rather than help.");

            // Having a Piece component is not the same as being something you can build.
            // Matching on components alone caught 35 prefabs you can never place out of 46:
            // 24 TreasureChest_*, the pots, the loose loot chests. That is not just wasted
            // work - snap points face both ways, so a chest carried into a crypt snapped to
            // the loot chests standing there, and a barrow's pottery became a snap target
            // for your walls.
            BuildablePiecesOnly = config.Bind("Snapping", "BuildablePiecesOnly", true,
                "Only snap pieces that actually appear in a build menu. Off means anything "
                + "with a Piece component qualifies, which includes dungeon loot chests and "
                + "pots you cannot place and probably do not want to snap to.");

            FencePrefabs = config.Bind("Snapping", "FencePrefabs", DefaultFences,
                "Comma-separated prefab names treated as fences. Names that do not exist "
                + "are reported in the log at startup.");

            ExcludePrefabs = config.Bind("Snapping", "ExcludePrefabs", "",
                "Comma-separated prefab names to leave alone, whatever else matches.");

            // One axis-aligned box cannot describe an L-shape or an off-centre piece, and it
            // never will: piece_dvergr_sharpstakes measures 2.40 x 1.70 x 3.94 centred 0.35
            // off in x, so its box corners are nowhere near the actual stakes. Both of the
            // mods that came before this one ended up needing per-prefab data for exactly
            // this reason - FenceSnap hand-places its gate points, and ChestSnap moved to a
            // YAML file of them - so derivation covers the common case and this covers the
            // rest.
            PointOverrides = config.Bind("Snapping", "PointOverrides", "",
                "Exact snap points for named prefabs, replacing anything derived. Format:\n"
                + "#   prefab: x,y,z | x,y,z ; other_prefab: x,y,z\n"
                + "# Semicolons separate prefabs, a colon follows the name, pipes separate\n"
                + "# points, commas separate the three coordinates of one point. Naming a\n"
                + "# prefab here is enough to get it snapped - it does not also have to be a\n"
                + "# container or a listed fence. Decimals must use a dot, never a comma,\n"
                + "# because a comma already separates coordinates. Gap and the fence ladder\n"
                + "# do not apply: a point given here is used exactly as written.");

            Gap = config.Bind("Snapping", "Gap", 0f,
                "Metres of space left between two chained pieces. 0 places them flush. "
                + "Negative values are ignored - overlapping pieces just clip.");

            // One number for every piece means "chests flush" and "fences a hand apart" are
            // the same decision, and they are not: a chest row wants no seam and a stake wall
            // wants the stakes to read as separate. Same punctuation as PointOverrides, so
            // there is one syntax in this file rather than two.
            GapOverrides = config.Bind("Snapping", "GapOverrides", "",
                "Gap for named prefabs, overriding Gap for those and leaving the rest alone.\n"
                + "# Format:  prefab: metres ; other_prefab: metres\n"
                + "# Semicolons separate prefabs and a colon follows the name. Decimals must\n"
                + "# use a dot. Negative values are ignored, as with Gap, and a name that\n"
                + "# matches no prefab is reported in the log at startup. Points given by hand\n"
                + "# through PointOverrides are used exactly as written and take no gap at all.");

            // A fence follows the ground; a chest does not. Corners give a fence two
            // heights to attach at, its base and a full panel up, and neither is any use
            // for running a line up a hill - so a fence gets a ladder of points up each
            // end instead. The idea is MSchmoecker's FenceSnap, which hand-places seven
            // rungs 0.2m apart on wood_fence; this derives them from the footprint so it
            // works on a piece nobody has measured.
            FenceLadderStep = config.Bind("Snapping", "FenceLadderStep", 0.2f,
                "Vertical spacing of the snap points up each end of a fence, in metres. "
                + "Smaller follows sloping ground more closely and costs more points per "
                + "piece. 0 turns the ladder off and gives fences plain corners like a "
                + "chest.");

            FenceLadderBelow = config.Bind("Snapping", "FenceLadderBelow", 0.2f,
                "How far below its own base a fence's lowest rung sits, in metres. This "
                + "is what lets the next panel step down rather than only up.");

            // A torch on a post, rather than a torch on the ground. Unlike everything above,
            // this is a pairing and not a shape: the torch gets one point, and it snaps only
            // to the top of a listed pole. See Sockets for why it cannot be a plain point the
            // way a chest corner is.
            //
            // This replaced SnapTorchesToPoles, TorchPrefabs, PolePrefabs and TorchStickOut.
            // Those three keys could describe exactly one pairing: every listed torch went
            // into every listed pole at one shared depth, and the depth is the part that
            // cannot be shared, since a torch's length and the height of its own fire are its
            // own. Those four keys are left behind under [Torches] in a config file written by
            // an older version, where they do nothing; deleting them is tidiness, not repair.
            SnapSockets = config.Bind("Sockets", "SnapSockets", true,
                "Aim a piece listed in Sockets at the top of one of its targets and it snaps "
                + "down into it, centred, with only its head standing above the top - a torch "
                + "on a post. It snaps only while aimed at the target's top face, to that one "
                + "target and nothing else, and nothing ever snaps to the piece afterwards. "
                + "Hold the place-without-snapping key to set a torch on a pole top without "
                + "sinking it.");

            Sockets = config.Bind("Sockets", "Sockets", DefaultSockets,
                "What sinks into what, and how much of it still shows. Format:\n"
                + "#   piece, piece : target, target : metres showing ; next entry\n"
                + "# Semicolons separate entries, colons separate an entry's three fields, and\n"
                + "# commas separate names within a field. Decimals must use a dot.\n"
                + "#\n"
                + "# The third field is optional. Left out, the depth is measured off the piece\n"
                + "# itself: as deep as it can sink while the zone a burning torch spreads fire\n"
                + "# into stays clear of its own pole, which in the Ashlands is what stops the\n"
                + "# pole burning out from under it. A typed value below that floor is raised to\n"
                + "# it with a warning, and one past the piece's own length is capped, since a\n"
                + "# socket below its tip would leave it floating above the pole instead.\n"
                + "#\n"
                + "# A target needs snap points of its own for any of this to work, because the\n"
                + "# highest of them is what the piece lands on - which is why a pole of any\n"
                + "# length is covered without naming its length. The startup log names any\n"
                + "# target that has none, is in no build menu, or matches no prefab at all.");

            // Aim a brazier at a floor tile and it lands dead centre. Nothing in vanilla centres
            // anything: these props ship with no snap points, so every one is eyeballed. What they
            // land on is derived from the piece under the crosshair rather than listed, so a
            // modded floor or the lid of a chest works with nothing named; only the props are a
            // list, kept tight on purpose.
            CentreOnSurface = config.Bind("Centring", "CentreOnSurface", true,
                "Aim a piece listed in CentreProps at the top face of any piece and it lands "
                + "centred on that face. It centres only while the crosshair is on an upward "
                + "face level with the target's top, and nothing ever snaps to the prop "
                + "afterwards. Hold the place-without-snapping key to place it where you aim.");

            CentreProps = config.Bind("Centring", "CentreProps", DefaultCentreProps,
                "Comma-separated prefab names of the standing props that centre. Kept to the "
                + "two floor braziers rather than every fire in the game: the bonfire and "
                + "hearth names have not been confirmed against a running game, and a name "
                + "that matches no prefab is reported in the log at startup, so adding one "
                + "is safe to try.");

            // Not a snap point, and the only thing here that is not. Two vanilla rules refuse
            // a chest on a chest - the placement test reads the *aimed* piece's m_supports,
            // and the wear tick destroys a piece that ends up unsupported - so no arrangement
            // of points could have done this. See Stacking for both, and for why each is
            // answered as narrowly as it is.
            StackContainers = config.Bind("Stacking", "StackContainers", true,
                "Let a chest be placed on top of another chest, and stay there.\n"
                + "# A chest holds up a chest and nothing else: a wall, a beam or a torch on a\n"
                + "# chest is still refused, which is vanilla's answer rather than a limit of\n"
                + "# this setting.\n"
                + "#\n"
                + "# ON A SERVER, READ THIS FIRST. Whether a piece is supported is worked out\n"
                + "# by whichever player's game owns it, and ownership follows whoever is\n"
                + "# nearby. A player who does not have Sinka works it out the vanilla way,\n"
                + "# finds a chest standing on nothing, and DESTROYS IT WITH EVERYTHING IN IT.\n"
                + "# There is no fix for that from here, because the rule runs on their\n"
                + "# machine. A stack is safe in single player, and on a server where every\n"
                + "# player has this mod - a modpack that carries it, for instance. On a server\n"
                + "# where some players do not, turn this off and lose nothing but the feature.\n"
                + "#\n"
                + "# The build-integrity colours still show a stacked chest as unsupported.\n"
                + "# That is only the colour: the chest is exempt from the wear that colour\n"
                + "# usually warns about.");

            Verbose = config.Bind("Diagnostics", "Verbose", false,
                "Log the measured footprint of every piece that gets snap points, and the "
                + "colliders it was measured from.");
        }

        // ------------------------------------------------------------------ lookups

        private static HashSet<string> _fences;
        private static HashSet<string> _excluded;

        public static bool IsFence(string prefabName)
        {
            if (_fences == null) _fences = Split(FencePrefabs.Value);
            return _fences.Contains(prefabName);
        }

        public static bool IsExcluded(string prefabName)
        {
            if (_excluded == null) _excluded = Split(ExcludePrefabs.Value);
            return _excluded.Count > 0 && _excluded.Contains(prefabName);
        }

        /// <summary>Configured fence names, so startup can report the ones that miss.</summary>
        public static IEnumerable<string> ConfiguredFences()
        {
            if (_fences == null) _fences = Split(FencePrefabs.Value);
            return _fences;
        }

        // ------------------------------------------------------------------ centring

        private static HashSet<string> _centreProps;

        public static bool IsCentreProp(string prefabName)
        {
            return ConfiguredCentreProps().Contains(prefabName);
        }

        /// <summary>Named props, so startup can report the ones that resolve to nothing.</summary>
        public static HashSet<string> ConfiguredCentreProps()
        {
            if (_centreProps == null) _centreProps = Split(CentreProps.Value);
            return _centreProps;
        }

        // ------------------------------------------------------------------ sockets

        /// <summary>
        /// One entry of Sockets, as it applies to one piece: what that piece may sink into,
        /// and how much of it is left showing.
        /// </summary>
        internal sealed class Socket
        {
            internal HashSet<string> Targets;

            /// <summary>
            /// Metres left standing above the target, or Measured when the entry left the
            /// field out and the piece's own fire decides instead.
            /// </summary>
            internal float StickOut;
        }

        /// <summary>A stick-out to measure off the prefab rather than take from config.</summary>
        internal const float Measured = -1f;

        private static Dictionary<string, Socket> _sockets;
        private static HashSet<string> _targets;

        public static Socket SocketFor(string prefabName)
        {
            Socket socket;
            return ParsedSockets().TryGetValue(prefabName, out socket) ? socket : null;
        }

        /// <summary>
        /// Whether anything at all sinks into this piece. The aim check runs on every
        /// placement ray, before the ghost in hand is known, so it asks the cheap question
        /// first and leaves "may *this* piece sink into it" to the search itself.
        /// </summary>
        public static bool IsSocketTarget(string prefabName)
        {
            return ConfiguredTargets().Contains(prefabName);
        }

        /// <summary>Named pieces, so startup can report the ones that resolve to nothing.</summary>
        public static IEnumerable<string> ConfiguredSockets()
        {
            return ParsedSockets().Keys;
        }

        public static HashSet<string> ConfiguredTargets()
        {
            if (_targets != null) return _targets;

            _targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var socket in ParsedSockets().Values)
                foreach (var target in socket.Targets)
                    _targets.Add(target);

            return _targets;
        }

        /// <summary>
        /// Parsed once and kept, keyed by the piece that sinks - one entry may name several,
        /// and they each get their own copy of the rule. Naming a piece twice is the later
        /// entry winning, which is the same rule a config file has everywhere else.
        ///
        /// A malformed entry is reported and dropped rather than throwing, like
        /// PointOverrides: getting one pairing wrong should cost that pairing, not the mod.
        /// </summary>
        private static Dictionary<string, Socket> ParsedSockets()
        {
            if (_sockets != null) return _sockets;

            _sockets = new Dictionary<string, Socket>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(Sockets.Value)) return _sockets;

            foreach (var entry in Sockets.Value.Split(';'))
            {
                var text = entry.Trim();
                if (text.Length == 0) continue;

                var fields = text.Split(':');
                if (fields.Length < 2 || fields.Length > 3)
                {
                    Warn("Sockets entry is not 'piece : target : metres' and was ignored: " + text);
                    continue;
                }

                var names = Split(fields[0]);
                var targets = Split(fields[1]);

                if (names.Count == 0 || targets.Count == 0)
                {
                    Warn("Sockets entry names no piece or no target and was ignored: " + text);
                    continue;
                }

                var stickOut = Measured;

                if (fields.Length == 3 && fields[2].Trim().Length > 0
                    && !float.TryParse(fields[2].Trim(), NumberStyles.Float,
                        CultureInfo.InvariantCulture, out stickOut))
                {
                    // The pairing is still usable without it, and measuring is what the field
                    // being absent already means, so this costs a typed depth rather than the
                    // whole entry.
                    Warn("Sockets: " + fields[0].Trim() + " has a depth that does not parse as "
                         + "a number, so it is measured off the piece instead: " + fields[2].Trim());
                    stickOut = Measured;
                }

                foreach (var name in names)
                    _sockets[name] = new Socket { Targets = targets, StickOut = stickOut };
            }

            return _sockets;
        }

        // ------------------------------------------------------------------ gap

        private static Dictionary<string, float> _gaps;

        /// <summary>The gap for one prefab: its own if it has one, otherwise the shared Gap.</summary>
        public static float GapFor(string prefabName)
        {
            float gap;
            return Gaps().TryGetValue(prefabName, out gap) ? gap : Gap.Value;
        }

        /// <summary>Named prefabs, so startup can report the ones that resolve to nothing.</summary>
        public static IEnumerable<string> ConfiguredGaps()
        {
            return Gaps().Keys;
        }

        private static Dictionary<string, float> Gaps()
        {
            if (_gaps != null) return _gaps;

            _gaps = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(GapOverrides.Value)) return _gaps;

            foreach (var entry in GapOverrides.Value.Split(';'))
            {
                var text = entry.Trim();
                if (text.Length == 0) continue;

                var colon = text.IndexOf(':');
                if (colon <= 0)
                {
                    Warn("GapOverrides entry has no 'prefab:' part and was ignored: " + text);
                    continue;
                }

                var name = text.Substring(0, colon).Trim();

                float gap;
                if (name.Length == 0
                    || !float.TryParse(text.Substring(colon + 1).Trim(), NumberStyles.Float,
                        CultureInfo.InvariantCulture, out gap))
                {
                    Warn("GapOverrides: " + name + " has a gap that does not parse as a number "
                         + "and was ignored: " + text);
                    continue;
                }

                _gaps[name] = gap;
            }

            return _gaps;
        }

        // ------------------------------------------------------------------ overrides

        private static Dictionary<string, Vector3[]> _overrides;

        public static bool HasPointOverride(string prefabName)
        {
            return Overrides().Count > 0 && Overrides().ContainsKey(prefabName);
        }

        public static Vector3[] PointOverride(string prefabName)
        {
            Vector3[] points;
            return Overrides().TryGetValue(prefabName, out points) ? points : null;
        }

        /// <summary>Named prefabs, so startup can report the ones that resolve to nothing.</summary>
        public static IEnumerable<string> ConfiguredOverrides()
        {
            return Overrides().Keys;
        }

        /// <summary>
        /// Parsed once and kept. A malformed entry is reported and dropped rather than
        /// throwing: getting one prefab's coordinates wrong should cost that prefab its
        /// points, not stop the mod loading.
        /// </summary>
        private static Dictionary<string, Vector3[]> Overrides()
        {
            if (_overrides != null) return _overrides;

            _overrides = new Dictionary<string, Vector3[]>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(PointOverrides.Value)) return _overrides;

            foreach (var entry in PointOverrides.Value.Split(';'))
            {
                var text = entry.Trim();
                if (text.Length == 0) continue;

                var colon = text.IndexOf(':');
                if (colon <= 0)
                {
                    Warn("PointOverrides entry has no 'prefab:' part and was ignored: " + text);
                    continue;
                }

                var name = text.Substring(0, colon).Trim();
                var points = ParsePoints(text.Substring(colon + 1), name);

                if (name.Length == 0 || points == null) continue;

                _overrides[name] = points;
            }

            return _overrides;
        }

        private static Vector3[] ParsePoints(string text, string prefabName)
        {
            var points = new List<Vector3>();

            foreach (var chunk in text.Split('|'))
            {
                var part = chunk.Trim();
                if (part.Length == 0) continue;

                var coords = part.Split(',');
                if (coords.Length != 3)
                {
                    Warn("PointOverrides: " + prefabName + " has a point that is not three "
                         + "numbers and was ignored: " + part);
                    continue;
                }

                float x, y, z;
                // Invariant, deliberately. This machine's culture reads a comma as the
                // decimal separator, which would turn "0.5" into 5 and a comma-separated
                // triple into nonsense. The config comment says to use dots for the same
                // reason.
                if (!float.TryParse(coords[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out x)
                    || !float.TryParse(coords[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out y)
                    || !float.TryParse(coords[2].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out z))
                {
                    Warn("PointOverrides: " + prefabName + " has a point that does not parse "
                         + "as numbers and was ignored: " + part);
                    continue;
                }

                points.Add(new Vector3(x, y, z));
            }

            if (points.Count != 0) return points.ToArray();

            Warn("PointOverrides: " + prefabName + " listed no usable points, so it is "
                 + "snapped the derived way instead.");
            return null;
        }

        private static void Warn(string message)
        {
            if (SinkaPlugin.Log != null) SinkaPlugin.Log.LogWarning(message);
        }

        private static HashSet<string> Split(string value)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(value)) return set;

            foreach (var entry in value.Split(','))
            {
                var name = entry.Trim();
                if (name.Length > 0) set.Add(name);
            }

            return set;
        }
    }
}
