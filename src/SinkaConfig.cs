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

        public static ConfigEntry<bool> SnapContainers;
        public static ConfigEntry<bool> SnapFences;
        public static ConfigEntry<bool> SnapUnsnappedPieces;
        public static ConfigEntry<bool> BuildablePiecesOnly;
        public static ConfigEntry<string> FencePrefabs;
        public static ConfigEntry<string> ExcludePrefabs;
        public static ConfigEntry<string> PointOverrides;
        public static ConfigEntry<float> Gap;
        public static ConfigEntry<float> FenceLadderStep;
        public static ConfigEntry<float> FenceLadderBelow;
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
