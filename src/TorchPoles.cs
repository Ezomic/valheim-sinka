using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Sinka
{
    /// <summary>
    /// A standing torch aimed at the top of a pole snaps down into it, centred on the pole, with
    /// only its head standing above the top: a torch on a post.
    ///
    /// Everything else in this mod is a shape - corners, rungs - that pairs with whatever is
    /// nearby. This is a pairing, and a plain snap point cannot give it three things.
    ///
    /// **Reach.** FindClosestSnapPoints only looks 0.5m from wherever the ghost is already
    /// resting, and a ghost aimed at a pole's top face rests *on* it. The wood torch's socket has
    /// to sit near its top (1.41m of torch, 0.25m showing), so the socket starts out a long way
    /// above the pole's top point, and the snap it needs is the torch sliding down most of its
    /// own length. How far depends on the torch's collider, which a rip does not print: its
    /// pivot is 0.65m above the bottom of the shaft, and whether the ghost rests on its pivot or
    /// on its shaft's tip is a difference of 0.65m. So the distance is measured from the prefab
    /// at load, and the search radius is widened to it.
    ///
    /// **Aim.** The search is a sphere around the socket, and widened to over a metre it caught
    /// far more than the pole in the crosshair. The first build of this pulled a torch set on a
    /// floor down through it whenever a pole stood under the floor's corner, jumped a torch on
    /// the ground two metres up into a nearby wall post, and picked the taller of two neighbouring
    /// posts over the one aimed at. A review against the decompiled placement code found all
    /// three before it was ever played. So the catch is no longer a distance at all: the placement
    /// ray has to have hit the top face of a listed pole, and that pole's top point is then the
    /// only thing the torch is offered. Aimed anywhere else, the torch has no snap point.
    ///
    /// **Hiding the socket.** That last part matters for more than the automatic snap. The keys
    /// that pick a snap point by hand put the chosen point on whatever the ray hit, so a socket
    /// left visible on a torch aimed at a floor would bury it there with its head at ankle height.
    /// And a pole snapping its top onto a standing torch would bury the pole to bring its top down
    /// to the torch's head. So the socket exists, as far as the game can tell, only on a torch
    /// ghost aimed at a pole top, and never on a torch already standing.
    ///
    /// All of it rides the vanilla search rather than replacing it: the game still gathers the
    /// points, picks the pair and moves the ghost, and these patches only change the radius and
    /// what is on the lists. Holding the place-without-snapping key skips the search, and so all
    /// of this, exactly as it skips every other snap.
    /// </summary>
    internal static class TorchPoles
    {
        /// <summary>The game prints this in the middle of the screen when the point is picked by hand.</summary>
        internal const string SocketName = "snap_into-pole";

        private const string Tag = "snappoint";

        /// <summary>
        /// Added to the socket's height to make the search radius, so aiming anywhere on a pole's
        /// top face still reaches its centre point. The wood pole's mesh is 0.40m square, which
        /// puts the corner of its top face 0.28m from the middle. Only the aimed pole's top is
        /// ever a candidate, so a generous number here cannot catch anything else.
        /// </summary>
        private const float FaceSlack = 0.3f;

        /// <summary>The game's own radius, which a torch's reach never drops below.</summary>
        private const float VanillaReach = 0.5f;

        /// <summary>
        /// A hit this far below or above the pole's top point still counts as its top face. The
        /// face and the point are the same height on a vanilla pole; this is only room for a mesh
        /// that bevels its top.
        /// </summary>
        private const float TopTolerance = 0.1f;

        /// <summary>
        /// Clearance kept between the bottom of a torch's fire-spread capsule and the top of the
        /// pole it stands in. See StickOutFloor.
        /// </summary>
        private const float IgniteMargin = 0.05f;

        /// <summary>
        /// Search radius per torch prefab, by name, since the placement ghost is named after its
        /// prefab. Cleared per scene with the rest of the mod's work, and rebuilt by Apply.
        /// </summary>
        private static readonly Dictionary<string, float> Reach =
            new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Piece.m_name of every torch carrying a socket. The per-piece snap point postfix runs
        /// for every piece within 10m of a held ghost, every frame, and m_name is a plain field -
        /// asking a GameObject its name allocates a new string each time.
        /// </summary>
        private static readonly HashSet<string> TorchPieceNames = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>
        /// The top point of the listed pole the placement ray hit this frame, or null. Set by the
        /// PieceRayTest postfix, which UpdatePlacementGhost calls before it gathers either list of
        /// snap points, so both lists in a frame are built from the same answer.
        /// </summary>
        private static Transform _aimedTop;

        /// <summary>
        /// The ghost the current search is for. The static Piece.GetSnapPoints that gathers the
        /// candidates is not told, and its only caller is FindClosestSnapPoints, which is, so the
        /// prefix leaves it here and the postfix takes it away again.
        /// </summary>
        private static Transform _ghost;

        private static int _ghostLayer = -1;

        internal static void Reset()
        {
            Reach.Clear();
            TorchPieceNames.Clear();
            _aimedTop = null;
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
            var wanted = SinkaConfig.TorchStickOut.Value;
            var floor = StickOutFloor(prefab, visual);

            // Past the torch's own length the socket would sit below its tip, and the torch
            // would float above the pole instead of standing in it. That ceiling wins over the
            // fire floor: a torch too short to clear its own fire zone is a modded torch nobody
            // measured, and floating it would be worse.
            var stickOut = Mathf.Min(Mathf.Max(wanted, floor, 0f), visual.size.y);

            if (stickOut > wanted + 0.001f)
                SinkaPlugin.Log.LogWarning(
                    prefab.name + ": TorchStickOut " + wanted.ToString("F3") + " would put the "
                    + "torch's fire-spread zone inside its own pole, so " + stickOut.ToString("F3")
                    + " is used. Below that the torch can set the pole alight in the Ashlands.");

            // The footprint's centre rather than the pivot: the colliders are the shaft, and
            // centred means the shaft on the pole's axis.
            var socket = new Vector3(footprint.center.x, visual.max.y - stickOut, footprint.center.z);

            SnapPoints.Create(prefab, SocketName, socket);
            Register(prefab, footprint, socket);

            return true;
        }

        /// <summary>
        /// The least a torch may show and keep its fire out of its own pole.
        ///
        /// A burning Fireplace looks for things to set alight in a capsule around its flame -
        /// Fireplace.UpdateIgnite, an OverlapCapsule from transform.position + m_igniteCapsuleStart
        /// to + m_igniteCapsuleEnd - and it skips only its own colliders. It fires only where
        /// Cinder says fire can spread, which in practice is the Ashlands and worlds with the fire
        /// hazard modifier. The wood torch's capsule runs from 0.65 to 1.00 above its pivot with
        /// a 0.05 radius, so its lowest point is 0.60, and its visible top is 0.76. Sunk further
        /// than that, the pole it stands in is inside the capsule and burns out from under it.
        /// Measured off the prefab rather than written down, so a modded torch gets its own floor.
        /// </summary>
        private static float StickOutFloor(GameObject prefab, Bounds visual)
        {
            var fire = prefab.GetComponent<Fireplace>();
            if (fire == null || fire.m_igniteCapsuleRadius <= 0f) return 0f;

            var lowest = Mathf.Min(fire.m_igniteCapsuleStart.y, fire.m_igniteCapsuleEnd.y)
                         - fire.m_igniteCapsuleRadius;

            // The capsule is in world space from the pivot and turns with nothing, and pieces
            // only ever turn about the vertical, so its height in the prefab is its height placed.
            return visual.max.y - lowest + IgniteMargin;
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

            var piece = prefab.GetComponent<Piece>();
            if (piece != null && !string.IsNullOrEmpty(piece.m_name)) TorchPieceNames.Add(piece.m_name);

            if (SinkaConfig.Verbose.Value)
                SinkaPlugin.Log.LogInfo(
                    prefab.name + ": torch socket at " + socket.ToString("F2") + ", footprint "
                    + footprint.size.ToString("F2") + " from y " + footprint.min.y.ToString("F2")
                    + ", reaches " + reach.ToString("F2") + "m to the top of the pole aimed at");
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

                if (TopPoint(prefab.transform) == null) bare.Add(name);
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

        /// <summary>
        /// A piece's highest snap point, whatever the game named it. Pieces only ever turn about
        /// the vertical, so the highest local point is the highest in the world too, and a pole of
        /// any length is covered without knowing how long it is.
        /// </summary>
        private static Transform TopPoint(Transform piece)
        {
            Transform top = null;

            for (var i = 0; i < piece.childCount; i++)
            {
                var child = piece.GetChild(i);
                if (!child.CompareTag(Tag)) continue;
                if (top == null || child.localPosition.y > top.localPosition.y) top = child;
            }

            return top;
        }

        /// <summary>A placed piece is its prefab's name with "(Clone)" on the end; the ghost is not.</summary>
        private static string PrefabName(string name)
        {
            var cut = name.IndexOf("(Clone)", StringComparison.Ordinal);
            return cut < 0 ? name : name.Substring(0, cut);
        }

        // ------------------------------------------------------------------ placement

        /// <summary>
        /// Whether the placement ray is on the top face of a listed pole: a hit on the pole, facing
        /// up, level with the pole's top point. The side of a pole is not enough - the wood torch
        /// refuses any surface steeper than m_notOnTiltingSurface allows, and the game decides
        /// that from this same hit before any snapping, so a torch snapped from the side would
        /// sit in the pole looking right and stay red.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Player), "PieceRayTest")]
        private static void Aimed(bool __result, ref Vector3 point, ref Vector3 normal, ref Piece piece)
        {
            _aimedTop = null;

            if (Reach.Count == 0 || !__result || piece == null || normal.y < 0.8f) return;
            if (!SinkaConfig.IsPole(PrefabName(piece.gameObject.name))) return;

            var top = TopPoint(piece.transform);
            if (top != null && Mathf.Abs(point.y - top.position.y) <= TopTolerance) _aimedTop = top;
        }

        /// <summary>
        /// Takes the socket off a torch's list of its own snap points unless it is the ghost in
        /// hand and aimed at a pole top. This one list feeds the automatic snap, the keys that
        /// pick a point by hand, and every other ghost's search for something to snap to, so
        /// hiding it here is what keeps the socket out of all three.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Piece), nameof(Piece.GetSnapPoints), new[] { typeof(List<Transform>) })]
        private static void Listed(Piece __instance, List<Transform> points)
        {
            if (TorchPieceNames.Count == 0 || points == null) return;
            if (!TorchPieceNames.Contains(__instance.m_name)) return;

            if (_ghostLayer < 0) _ghostLayer = LayerMask.NameToLayer("ghost");
            if (_aimedTop != null && __instance.gameObject.layer == _ghostLayer) return;

            // This call appended the piece's own points at the end of a list that may already
            // hold other pieces' points, so only those are looked at.
            var own = __instance.transform;
            for (var i = points.Count - 1; i >= 0; i--)
            {
                var point = points[i];
                if (point == null || point.parent != own) break;
                if (point.name == SocketName) points.RemoveAt(i);
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Player), "FindClosestSnapPoints")]
        private static void Searching(Transform ghost, ref float maxSnapDistance)
        {
            _ghost = null;
            if (_aimedTop == null || ghost == null) return;

            float reach;
            if (!Reach.TryGetValue(ghost.name, out reach)) return;

            _ghost = ghost;
            if (reach > maxSnapDistance) maxSnapDistance = reach;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Player), "FindClosestSnapPoints")]
        private static void Searched()
        {
            _ghost = null;
        }

        /// <summary>
        /// For a torch ghost aimed at a pole top, the candidates FindClosestSnapPoints chooses from
        /// are cut down to that one point. _ghost is only set in that case, so every other
        /// placement leaves here on one null check.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Piece), nameof(Piece.GetSnapPoints),
            new[] { typeof(Vector3), typeof(float), typeof(List<Transform>), typeof(List<Piece>) })]
        private static void Gathered(List<Transform> points)
        {
            if (_ghost == null || _aimedTop == null || points == null) return;

            for (var i = points.Count - 1; i >= 0; i--)
                if (points[i] != _aimedTop) points.RemoveAt(i);
        }
    }
}
