using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Sinka
{
    /// <summary>
    /// A standing torch placed on a pole snaps down into it, centred on the pole, with only its
    /// head standing above the top: a torch on a post.
    ///
    /// Everything else in this mod is a shape - corners, rungs - that pairs with whatever is
    /// nearby. This is a pairing, and it needs two things a plain snap point cannot give it.
    ///
    /// **Reach.** FindClosestSnapPoints only looks 0.5m from wherever the ghost is already
    /// resting, and a ghost aimed at a pole's top face rests *on* it. The wood torch's socket has
    /// to sit near its top (1.41m of torch, 0.2m showing), so the socket starts out a long way
    /// above the pole's top point, and the snap it needs is the torch sliding down most of its
    /// own length. How far depends on the torch's collider, which a rip does not print: its
    /// pivot is 0.65m above the bottom of the shaft, and whether the ghost rests on its pivot or
    /// on its shaft's tip is a difference of 0.65m. So the distance is measured from the prefab
    /// at load, and the search radius is widened to it - for a torch ghost only.
    ///
    /// **Partners.** A wider radius on an ordinary point would snap a torch into the corner of
    /// every floor it was carried near, with its head at ankle height. So a torch ghost is shown
    /// only the top point of a listed pole, and every other ghost is shown no torch socket at
    /// all. The torch loses nothing by it - it had no snap points before - and a pole snapping
    /// its top onto a standing torch would bury the pole to bring its top down to the torch's
    /// head, which is not a post anybody wants.
    ///
    /// Both halves ride the vanilla search rather than replacing it: a prefix raises the radius
    /// and a postfix trims the list of candidates, and the game still picks the closest pair and
    /// moves the ghost itself. Holding the place-without-snapping key skips the search, and so
    /// all of this, exactly as it skips every other snap.
    /// </summary>
    internal static class TorchPoles
    {
        /// <summary>The game prints this in the middle of the screen when the point is picked by hand.</summary>
        internal const string SocketName = "snap_into-pole";

        private const string Tag = "snappoint";

        /// <summary>
        /// The search is widened by this much on top of the socket's own height, so aiming
        /// anywhere on a pole's top face still catches its centre point. The wood pole's mesh is
        /// 0.40m square, which puts the corner of its top face 0.28m from the middle.
        /// </summary>
        private const float FaceSlack = 0.3f;

        /// <summary>The game's own radius, which a torch's reach never drops below.</summary>
        private const float VanillaReach = 0.5f;

        /// <summary>
        /// Search radius per torch prefab, by name, since the placement ghost is named after its
        /// prefab. Being in here is also what marks a ghost as a torch. Cleared per scene with the
        /// rest of the mod's work, and rebuilt by Apply.
        /// </summary>
        private static readonly Dictionary<string, float> Reach =
            new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// The ghost the current search is for. The static Piece.GetSnapPoints that gathers the
        /// candidates is not told, and its only caller is FindClosestSnapPoints, which is, so the
        /// prefix leaves it here and the postfix takes it away again.
        /// </summary>
        private static Transform _ghost;

        internal static void Reset()
        {
            Reach.Clear();
        }

        internal static bool Wanted(GameObject prefab)
        {
            return SinkaConfig.SnapTorchesToPoles.Value && SinkaConfig.IsTorch(prefab.name);
        }

        /// <summary>
        /// The socket sits on the torch's own axis, TorchStickOut below the top of what you can
        /// see, so a socket laid on a pole's top point leaves exactly that much standing out.
        /// </summary>
        internal static bool AddSocket(GameObject prefab)
        {
            if (!SnapPoints.Footprint(prefab, out var footprint)) return false;

            var visual = Visual(prefab, footprint);

            // Past the torch's own length the socket would sit below its tip, and the torch
            // would float above the pole instead of standing in it.
            var stickOut = Mathf.Clamp(SinkaConfig.TorchStickOut.Value, 0f, visual.size.y);

            // The footprint's centre rather than the pivot: the colliders are the shaft, and
            // centred means the shaft on the pole's axis.
            var socket = new Vector3(footprint.center.x, visual.max.y - stickOut, footprint.center.z);

            SnapPoints.Create(prefab, SocketName, socket);
            Register(prefab, footprint, socket);

            return true;
        }

        /// <summary>The socket is already on the prefab from an earlier scene; only the rules need restoring.</summary>
        internal static bool Readopt(GameObject prefab)
        {
            var socket = prefab.transform.Find(SocketName);
            if (socket == null || !socket.CompareTag(Tag)) return false;
            if (!SnapPoints.Footprint(prefab, out var footprint)) return false;

            Register(prefab, footprint, socket.localPosition);
            return true;
        }

        private static void Register(GameObject prefab, Bounds footprint, Vector3 socket)
        {
            // A ghost aimed at a pole's top face is lowered until its colliders touch the hit -
            // the bottom of its footprint. So that is where the socket starts out relative to
            // the pole's top point, and the distance the snap has to cover.
            var rest = Mathf.Abs(socket.y - footprint.min.y);
            var reach = Mathf.Max(VanillaReach, rest + FaceSlack);

            Reach[prefab.name] = reach;

            if (SinkaConfig.Verbose.Value)
                SinkaPlugin.Log.LogInfo(
                    prefab.name + ": torch socket at " + socket.ToString("F2") + ", footprint "
                    + footprint.size.ToString("F2") + " from y " + footprint.min.y.ToString("F2")
                    + ", snaps to a pole top within " + reach.ToString("F2") + "m");
        }

        /// <summary>
        /// What you see, rather than what collides: the head a player means by "sticking out" is
        /// mesh, and a collider may stop short of it. Same live-geometry rules as the footprint,
        /// so a damage state or a destruction chunk does not stretch it. Falls back to the
        /// footprint for a torch with no readable mesh.
        /// </summary>
        private static Bounds Visual(GameObject prefab, Bounds footprint)
        {
            var root = prefab.transform;
            var found = false;
            var bounds = footprint;

            foreach (var filter in SnapPoints.LiveGeometry(prefab).GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;
                if (SnapPoints.IsDisabled(filter.transform, root)) continue;

                var box = SnapPoints.ToRoot(root, filter.transform, filter.sharedMesh.bounds);
                if (!found) { bounds = box; found = true; }
                else bounds.Encapsulate(box);
            }

            return bounds;
        }

        /// <summary>
        /// A listed pole with no snap points of its own gives a torch nothing to snap to, and one
        /// that is not in a build menu is a name that will never be standing anywhere. Both look
        /// exactly like the feature not working, so both are said out loud.
        /// </summary>
        internal static void ReportPoles()
        {
            var bare = new List<string>();
            var unbuildable = new List<string>();

            foreach (var name in SinkaConfig.ConfiguredPoles())
            {
                var prefab = ZNetScene.instance.GetPrefab(name);
                if (prefab == null) continue; // already reported as matching no prefab

                if (!HasPoints(prefab.transform)) bare.Add(name);
                if (!Buildable.Includes(prefab)) unbuildable.Add(name);
            }

            if (bare.Count > 0)
                SinkaPlugin.Log.LogWarning(
                    "PolePrefabs names with no snap points of their own, so no torch can snap to "
                    + "them: " + string.Join(", ", bare.ToArray()));

            if (unbuildable.Count > 0)
                SinkaPlugin.Log.LogWarning(
                    "PolePrefabs names that are in no build menu: "
                    + string.Join(", ", unbuildable.ToArray()));
        }

        private static bool HasPoints(Transform root)
        {
            for (var i = 0; i < root.childCount; i++)
                if (root.GetChild(i).CompareTag(Tag)) return true;

            return false;
        }

        // ------------------------------------------------------------------ placement

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Player), "FindClosestSnapPoints")]
        private static void Searching(Transform ghost, ref float maxSnapDistance)
        {
            _ghost = ghost;
            if (ghost == null) return;

            float reach;
            if (Reach.TryGetValue(ghost.name, out reach) && reach > maxSnapDistance)
                maxSnapDistance = reach;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Player), "FindClosestSnapPoints")]
        private static void Searched()
        {
            _ghost = null;
        }

        /// <summary>
        /// Trims the candidates FindClosestSnapPoints is about to choose from. This runs every
        /// frame a piece is held, over every point within 10m, so the common case - no torch has
        /// a socket this scene - leaves after one count.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Piece), nameof(Piece.GetSnapPoints),
            new[] { typeof(Vector3), typeof(float), typeof(List<Transform>), typeof(List<Piece>) })]
        private static void Gathered(List<Transform> points)
        {
            if (Reach.Count == 0 || _ghost == null || points == null) return;

            var torch = Reach.ContainsKey(_ghost.name);

            for (var i = points.Count - 1; i >= 0; i--)
            {
                var point = points[i];
                if (point == null) continue;

                var keep = torch ? IsPoleTop(point) : point.name != SocketName;
                if (!keep) points.RemoveAt(i);
            }
        }

        /// <summary>
        /// The pole's highest point, whatever the game named it. Pieces only ever turn about the
        /// vertical, so the highest local point is the highest in the world too, and a pole of any
        /// length is covered without knowing how long it is.
        /// </summary>
        private static bool IsPoleTop(Transform point)
        {
            var piece = point.parent;
            if (piece == null || !SinkaConfig.IsPole(PrefabName(piece.name))) return false;

            for (var i = 0; i < piece.childCount; i++)
            {
                var other = piece.GetChild(i);
                if (other == point || !other.CompareTag(Tag)) continue;
                if (other.localPosition.y > point.localPosition.y + 0.001f) return false;
            }

            return true;
        }

        /// <summary>A placed piece is its prefab's name with "(Clone)" on the end; the ghost is not.</summary>
        private static string PrefabName(string name)
        {
            var cut = name.IndexOf("(Clone)", StringComparison.Ordinal);
            return cut < 0 ? name : name.Substring(0, cut);
        }
    }
}
