using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Sinka
{
    /// <summary>
    /// A standing prop aimed at the top face of any piece lands centred on that piece's top: a
    /// brazier on a floor tile, a prop on the lid of a chest. Which props is the CentreProps
    /// setting; what they land on is derived, never named.
    ///
    /// Both ends of this pairing are missing a snap point. The prop gets one at the centre of
    /// its own base, added to its prefab like every other point in this mod. The target's point,
    /// the centre of its top face, is the new part, and it is not added to any prefab: every
    /// buildable piece would need one, and each would then sit in the general point set where a
    /// chest corner could pair with it and land half a piece out of line, which is the uniform
    /// corner rule DESIGN.md defends. It is one transform instead, parented to the piece under
    /// the crosshair only for the length of the search and tagged as a snap point only for
    /// that long, so no other ghost's search and no other call of GetSnapPoints ever sees it.
    /// Parented rather than loose because the vanilla placement code reads b.parent.position
    /// off the point it picked, and a point with no parent throws there.
    ///
    /// Gated on aim the way the torch socket is, and for the same reason. A postfix on
    /// PieceRayTest notes whether the ray is on an upward face level with the top of the piece's
    /// measured footprint, the static Piece.GetSnapPoints postfix cuts the candidates to that
    /// one point, and the prop's own base point is hidden from every list unless the prop is
    /// the ghost aiming at such a face. The keys that pick a point by hand put the chosen
    /// point on whatever the ray hit, so a base point left visible would also be offered
    /// when aiming at a wall.
    ///
    /// The centre is the middle of the target's footprint box, measured from the prefab by the
    /// same rules as the corners. A round or triangular top has a box centre that is not the
    /// middle of its face; that is the limit of deriving it from a box.
    /// </summary>
    internal static class CentrePoints
    {
        internal const string BaseName = "snap_centre-base";

        private const string Tag = "snappoint";

        /// <summary>
        /// A hit this far above or below the footprint's top still counts as its top face, the
        /// same room the socket gives a mesh that bevels its top.
        /// </summary>
        private const float TopTolerance = 0.1f;

        /// <summary>
        /// Added to the measured distance from the ghost's base point to the face centre. The
        /// search is cut to one point, so the radius can only ever reach that point; this is
        /// rounding room, not a catch area.
        /// </summary>
        private const float ReachSlack = 0.05f;

        private static readonly HashSet<string> PropPieceNames = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>Footprint of each target prefab, null for one that cannot be measured. Per scene.</summary>
        private static readonly Dictionary<string, Bounds?> Footprints =
            new Dictionary<string, Bounds?>(StringComparer.OrdinalIgnoreCase);

        private static Piece _aimedPiece;
        private static Vector3 _aimedCentre;

        private static Transform _point;
        private static bool _searching;
        private static int _ghostLayer = -1;

        internal static void Reset()
        {
            PropPieceNames.Clear();
            Footprints.Clear();
            _aimedPiece = null;
            Release();
        }

        internal static bool Wanted(GameObject prefab)
        {
            return SinkaConfig.CentreOnSurface.Value && SinkaConfig.IsCentreProp(prefab.name);
        }

        internal static bool AddBase(GameObject prefab)
        {
            if (!SnapPoints.Footprint(prefab, out var footprint)) return false;

            var local = new Vector3(footprint.center.x, footprint.min.y, footprint.center.z);
            SnapPoints.Create(prefab, BaseName, local);
            Register(prefab, local);
            return true;
        }

        /// <summary>The point is already on the prefab from an earlier scene; only the rule needs restoring.</summary>
        internal static bool Readopt(GameObject prefab)
        {
            var point = prefab.transform.Find(BaseName);
            if (point == null || !point.CompareTag(Tag)) return false;

            Register(prefab, point.localPosition);
            return true;
        }

        private static void Register(GameObject prefab, Vector3 local)
        {
            var piece = prefab.GetComponent<Piece>();
            if (piece != null && !string.IsNullOrEmpty(piece.m_name)) PropPieceNames.Add(piece.m_name);

            if (SinkaConfig.Verbose.Value)
                SinkaPlugin.Log.LogInfo(prefab.name + ": centre point at its base " + local.ToString("F2"));
        }

        internal static void ReportProps()
        {
            var unbuildable = new List<string>();

            foreach (var name in SinkaConfig.ConfiguredCentreProps())
            {
                var prefab = ZNetScene.instance.GetPrefab(name);
                if (prefab == null) continue; // already reported as matching no prefab

                if (!Buildable.Includes(prefab)) unbuildable.Add(name);
            }

            if (unbuildable.Count > 0)
                SinkaPlugin.Log.LogWarning(
                    "CentreProps pieces that are in no build menu: " + string.Join(", ", unbuildable.ToArray()));
        }

        private static string PrefabName(string name)
        {
            var cut = name.IndexOf("(Clone)", StringComparison.Ordinal);
            return cut < 0 ? name : name.Substring(0, cut);
        }

        private static bool Measure(string prefabName, out Bounds footprint)
        {
            Bounds? cached;
            if (!Footprints.TryGetValue(prefabName, out cached))
            {
                cached = null;
                var prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(prefabName) : null;

                try
                {
                    Bounds measured;
                    if (prefab != null && SnapPoints.Footprint(prefab, out measured)) cached = measured;
                }
                catch (Exception e)
                {
                    SinkaPlugin.Log.LogWarning("Could not measure " + prefabName + " to centre on: " + e.Message);
                }

                Footprints[prefabName] = cached;
            }

            footprint = cached ?? default(Bounds);
            return cached.HasValue;
        }

        private static void Release()
        {
            _searching = false;
            if (_point == null) return;

            _point.tag = "Untagged";
            _point.SetParent(null, false);
        }

        // ------------------------------------------------------------------ placement

        /// <summary>
        /// Notes the centre of the top face under the crosshair, or nothing. Runs before
        /// UpdatePlacementGhost gathers either list of snap points, so a frame's lists are all
        /// built from one answer.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Player), "PieceRayTest")]
        private static void Aimed(bool __result, ref Vector3 point, ref Vector3 normal, ref Piece piece)
        {
            _aimedPiece = null;

            if (PropPieceNames.Count == 0 || !__result || piece == null || normal.y < 0.8f) return;

            Bounds footprint;
            if (!Measure(PrefabName(piece.gameObject.name), out footprint)) return;

            var centre = piece.transform.TransformPoint(
                new Vector3(footprint.center.x, footprint.max.y, footprint.center.z));
            if (Mathf.Abs(point.y - centre.y) > TopTolerance) return;

            _aimedPiece = piece;
            _aimedCentre = centre;
        }

        /// <summary>
        /// Takes the base point off a prop's list of its own snap points unless it is the ghost
        /// in hand and aimed at a top face. One list feeds the automatic snap, the keys that
        /// pick a point by hand and every other ghost's search, so hiding it here keeps it out
        /// of all three.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Piece), nameof(Piece.GetSnapPoints), new[] { typeof(List<Transform>) })]
        private static void Listed(Piece __instance, List<Transform> points)
        {
            if (PropPieceNames.Count == 0 || points == null) return;
            if (!PropPieceNames.Contains(__instance.m_name)) return;

            if (_ghostLayer < 0) _ghostLayer = LayerMask.NameToLayer("ghost");
            if (_aimedPiece != null && __instance.gameObject.layer == _ghostLayer) return;

            var own = __instance.transform;
            for (var i = points.Count - 1; i >= 0; i--)
            {
                var point = points[i];
                if (point == null || point.parent != own) break;
                if (point.name == BaseName) points.RemoveAt(i);
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Player), "FindClosestSnapPoints")]
        private static void Searching(Transform ghost, ref float maxSnapDistance)
        {
            Release();

            if (_aimedPiece == null || ghost == null) return;

            Piece ghostPiece;
            if (!ghost.TryGetComponent(out ghostPiece) || !PropPieceNames.Contains(ghostPiece.m_name)) return;

            var basePoint = ghost.Find(BaseName);
            if (basePoint == null) return;

            if (_point == null)
            {
                var holder = new GameObject("snap_centre-top");
                holder.hideFlags = HideFlags.HideAndDontSave;
                _point = holder.transform;
            }

            _point.SetParent(_aimedPiece.transform, false);
            _point.position = _aimedCentre;
            _point.tag = Tag;
            _searching = true;

            var reach = Vector3.Distance(basePoint.position, _aimedCentre) + ReachSlack;
            if (reach > maxSnapDistance) maxSnapDistance = reach;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Player), "FindClosestSnapPoints")]
        private static void Searched()
        {
            Release();
        }

        /// <summary>
        /// For a prop aimed at a top face, the candidates are cut down to the face's centre.
        /// _searching is only set in that case, so every other placement leaves here on one
        /// check.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Piece), nameof(Piece.GetSnapPoints),
            new[] { typeof(Vector3), typeof(float), typeof(List<Transform>), typeof(List<Piece>) })]
        private static void Gathered(List<Transform> points)
        {
            if (!_searching || _point == null || points == null) return;

            for (var i = points.Count - 1; i >= 0; i--)
                if (points[i] != _point) points.RemoveAt(i);
        }
    }
}
