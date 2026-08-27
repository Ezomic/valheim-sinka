using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sinka
{
    /// <summary>
    /// The set of prefabs that actually appear in a build menu.
    ///
    /// Having a Piece component is not the same as being buildable, and the gap is not small:
    /// matching on components alone gave snap points to 35 prefabs out of 46 that you can
    /// never place - 24 TreasureChest_* variants, nine pots, two loose loot chests. The cost
    /// is not the wasted transforms. Snap points work in both directions, so a chest carried
    /// into a crypt snapped itself to the loot chests already standing there, and a barrow's
    /// pottery became a snap target for a wall.
    ///
    /// The list is read off the game's own piece tables rather than guessed at: every
    /// buildable piece is in the PieceTable of some tool, reached through
    /// ItemDrop.m_itemData.m_shared.m_buildPieces. So the Hammer, the Hoe, the Cultivator and
    /// any modded tool with its own table all contribute, and nothing needs naming here.
    /// </summary>
    internal static class Buildable
    {
        /// <summary>
        /// Rebuilt per ObjectDB rather than cached in a static flag, for the same reason
        /// SnapPoints keys on the scene: a second world means a new ObjectDB, and answering
        /// from a field that outlived the old one is how this family of bug happens.
        /// </summary>
        private static ObjectDB _builtFrom;

        private static HashSet<string> _names;

        /// <summary>
        /// Waiting is normally the right answer, but not forever. If the tables never turn up,
        /// falling back to unfiltered is the lesser failure: too many pieces snapped is an
        /// annoyance, and a mod that silently does nothing at all is a bug report.
        /// </summary>
        private const int MaxWaitFrames = 900;

        private static int _waited;
        private static bool _gaveUp;

        public static bool Ready()
        {
            if (!SinkaConfig.BuildablePiecesOnly.Value) return true;
            if (Build()) return true;
            if (_gaveUp) return true;

            if (++_waited < MaxWaitFrames) return false;

            _gaveUp = true;
            SinkaPlugin.Log.LogWarning(
                "No piece tables found after " + MaxWaitFrames + " frames, so "
                + "BuildablePiecesOnly cannot be applied. Snapping everything with a Piece "
                + "component instead, which includes dungeon loot chests and pots.");

            return true;
        }

        public static bool Includes(GameObject prefab)
        {
            if (!SinkaConfig.BuildablePiecesOnly.Value) return true;

            // No set means Ready() gave up. Unfiltered, deliberately - see MaxWaitFrames.
            if (_names == null) return true;

            return _names.Contains(prefab.name);
        }

        private static bool Build()
        {
            var db = ObjectDB.instance;

            // The first ObjectDB.Awake of a session fires against a stub holding no items at
            // all, so an empty list means "not yet", not "nothing is buildable".
            if (db == null || db.m_items == null || db.m_items.Count == 0) return false;
            if (_builtFrom == db && _names != null) return true;

            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var tables = 0;

            foreach (var item in db.m_items)
            {
                if (item == null) continue;

                var drop = item.GetComponent<ItemDrop>();
                if (drop == null || drop.m_itemData == null || drop.m_itemData.m_shared == null)
                    continue;

                var table = drop.m_itemData.m_shared.m_buildPieces;
                if (table == null || table.m_pieces == null) continue;

                tables++;
                foreach (var piece in table.m_pieces)
                    if (piece != null) names.Add(piece.name);
            }

            if (tables == 0 || names.Count == 0) return false;

            _names = names;
            _builtFrom = db;

            // A mod that adds pieces to a table after this runs is not covered, and there is
            // no event to hang that off. PointOverrides names a prefab explicitly and skips
            // this check, which is the way out if it ever comes up.
            SinkaPlugin.Log.LogInfo(
                "Buildable set: " + names.Count + " piece(s) across " + tables + " piece table(s).");

            return true;
        }
    }
}
