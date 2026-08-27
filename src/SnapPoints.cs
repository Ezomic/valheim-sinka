using UnityEngine;

namespace Sinka
{
    /// <summary>
    /// Gives every container piece snap points at the eight corners of its own footprint,
    /// so chests line up flush beside each other and stack cleanly on top. Fences get a
    /// ladder of points up each end instead, so a fence line can follow sloping ground -
    /// see UseLadder and AddLadder, and the credit to FenceSnap there.
    ///
    /// How the game snaps: Player.UpdatePlacementGhost calls FindClosestSnapPoints, which
    /// picks the globally closest pair of snap points - one on the ghost, one on a nearby
    /// placed piece - within 0.5m, then moves the ghost so the two coincide. Corners are
    /// used rather than face centres precisely because that rule is "make these two points
    /// the same point": a ghost's left corner landing on a placed chest's right corner is
    /// flush adjacency, and a ghost's bottom corner landing on a top corner is a stack.
    /// Mixing corners with face centres would let a chest snap half a chest out of line.
    ///
    /// A snap point is nothing but a child transform tagged "snappoint" - see
    /// Piece.GetSnapPoints, which is the whole of the game's side of this.
    /// </summary>
    internal static class SnapPoints
    {
        private const string Tag = "snappoint";

        /// <summary>
        /// The scene this has already been done to, rather than a plain bool.
        ///
        /// "Idempotent" here has to mean per world, not per process. Loading a second world -
        /// including logging out to the menu and back in - tears down ZNetScene and builds a
        /// new one, and a static bool would answer yes for the rest of the session while the
        /// new scene had never been touched. That is the failure recorded in CLAUDE.md that
        /// silently destroyed a built piece in another mod, and the fix there is the same as
        /// here: ask the world, do not answer from a field.
        ///
        /// This is cheap to get wrong in the safe direction. If prefab assets do keep the
        /// points across a reload, the second pass finds them all under "already had their
        /// own" and does nothing, and the log then says so plainly either way.
        /// </summary>
        private static ZNetScene _appliedTo;

        /// <summary>Idempotent, and safe to call every frame until it takes.</summary>
        public static bool Apply()
        {
            var scene = ZNetScene.instance;
            if (scene == null) return false;
            if (_appliedTo == scene) return true;

            // The buildable set is read off the piece tables, which live on items in
            // ObjectDB, so there is nothing to filter by until ObjectDB is up. Waiting is
            // safe: Update calls this every frame, and pieces are instantiated later.
            if (!Buildable.Ready()) return false;

            var touched = 0;
            var skipped = 0;
            var laddered = 0;
            var custom = 0;
            var oneWay = new System.Collections.Generic.List<string>();

            foreach (var prefab in scene.m_prefabs)
            {
                if (prefab == null) continue;
                if (!Eligible(prefab)) continue;

                // Something already gave it snap points - vanilla, or another mod. Adding
                // a second set on top would fight whatever placement it already has.
                if (HasSnapPoints(prefab.transform)) { skipped++; continue; }

                // One awkward prefab - an odd collider, a missing mesh - should cost that
                // chest its snap points, not every chest after it in the list.
                try
                {
                    var points = SinkaConfig.PointOverride(prefab.name);
                    var ladder = points == null && UseLadder(prefab);

                    var added = points != null
                        ? AddExact(prefab, points)
                        : ladder ? AddLadder(prefab) : AddCorners(prefab);

                    if (added)
                    {
                        touched++;
                        if (points != null) custom++;
                        else if (ladder) laddered++;
                    }
                }
                catch (System.Exception e)
                {
                    SinkaPlugin.Log.LogWarning(
                        "Could not snap " + prefab.name + ": " + e.Message);
                }

                if (!ReachableAsTarget(prefab)) oneWay.Add(prefab.name);
            }

            if (oneWay.Count > 0)
                SinkaPlugin.Log.LogWarning(
                    "These have snap points, but none of their colliders are on the piece or "
                    + "piece_nonsolid layer, which is where the game looks for something to "
                    + "snap to: " + string.Join(", ", oneWay.ToArray()) + ". Placing one still "
                    + "snaps it to its neighbours; what will not work is snapping anything to "
                    + "it once it is standing. That is the piece's own layer setup, not "
                    + "something this mod will change for it.");

            _appliedTo = scene;
            SinkaPlugin.Log.LogInfo(
                "Added snap points to " + touched + " piece(s): " + laddered
                + " fence ladder, " + custom + " from PointOverrides, "
                + (touched - laddered - custom) + " corners. " + skipped
                + " already had their own.");

            ReportMissingNames();
            return true;
        }

        /// <summary>
        /// A piece whose colliders sit outside the mask the game searches with can only ever
        /// snap in one direction, so its points are half useful and it is worth saying which
        /// half.
        ///
        /// FindClosestSnapPoints reads the ghost's own points straight off the piece being
        /// placed, with no mask involved, and only the search for *nearby* pieces goes through
        /// Physics.OverlapSphereNonAlloc(..., s_pieceRayMask) - which is
        /// LayerMask.GetMask("piece", "piece_nonsolid"). So placing one of these snaps it to
        /// its neighbours perfectly well. What does not work is the reverse: once it is
        /// standing there, nothing else can find it to snap against.
        ///
        /// Both ChestSnap and FenceSnap carry a FixPiece that rewrites every collider onto
        /// the piece layer. This deliberately does not: moving another mod's colliders to a
        /// different layer changes what they collide with, which is far too large a side
        /// effect to apply silently to somebody else's content. Saying so is this mod's
        /// existing habit - the same as it does for a fence name that matches no prefab -
        /// and it leaves the fix with whoever owns the piece.
        ///
        /// Reported as one line rather than one per piece. Six vanilla prefabs trip this on a
        /// stock install (the three gifts and the three pots), and six warnings per startup
        /// about something only Iron Gate can fix is noise, not information.
        /// </summary>
        private static bool ReachableAsTarget(GameObject prefab)
        {
            var any = false;

            foreach (var collider in prefab.GetComponentsInChildren<Collider>(true))
            {
                if (collider.isTrigger) continue;

                any = true;
                if ((PieceMask & (1 << collider.gameObject.layer)) != 0) return true;
            }

            // Nothing solid at all is a different problem, and not this one.
            return !any;
        }

        private static int _pieceMask;

        private static int PieceMask
        {
            get
            {
                if (_pieceMask == 0) _pieceMask = LayerMask.GetMask("piece", "piece_nonsolid");
                return _pieceMask;
            }
        }

        /// <summary>Points given verbatim in config, with no gap and no ladder applied.</summary>
        private static bool AddExact(GameObject prefab, Vector3[] points)
        {
            for (var i = 0; i < points.Length; i++)
                Create(prefab, "snap_custom" + (i + 1), points[i]);

            if (SinkaConfig.Verbose.Value)
                SinkaPlugin.Log.LogInfo(
                    prefab.name + ": " + points.Length + " point(s) from PointOverrides");

            return true;
        }

        /// <summary>
        /// A configured fence name that matches no prefab does nothing at all, and does it
        /// silently - which is exactly how you spend an evening wondering why one fence
        /// still will not line up. Say so instead.
        /// </summary>
        private static void ReportMissingNames()
        {
            if (SinkaConfig.SnapFences.Value)
                Report("FencePrefabs", SinkaConfig.ConfiguredFences());

            Report("PointOverrides", SinkaConfig.ConfiguredOverrides());
        }

        private static void Report(string setting, System.Collections.Generic.IEnumerable<string> names)
        {
            var missing = new System.Collections.Generic.List<string>();
            foreach (var name in names)
                if (ZNetScene.instance.GetPrefab(name) == null) missing.Add(name);

            if (missing.Count == 0) return;

            SinkaPlugin.Log.LogWarning(
                setting + " names that match no prefab: " + string.Join(", ", missing.ToArray()));
        }

        /// <summary>
        /// Three ways in, in order of how confident we are that snapping is wanted.
        ///
        /// Containers are matched on components, not names - a modded chest is a Piece with
        /// a Container just as much as a vanilla one, so that covers them for free and does
        /// not rot when a prefab is renamed.
        ///
        /// Fences are not so lucky. Nothing distinguishes a fence from any other wall by
        /// component, so they need a name list; it lives in config rather than in code, and
        /// names that do not resolve are reported rather than silently skipped.
        ///
        /// The third way is the general one: any buildable piece the developers never gave
        /// snap points to. That set is mostly chests, fences and loose decoration, because
        /// walls, floors and beams all ship with their own. It is off by default because it
        /// also catches chairs and banners, where snapping is more nuisance than help.
        /// </summary>
        private static bool Eligible(GameObject prefab)
        {
            if (prefab.GetComponent<Piece>() == null) return false;

            // Ships carry cargo and are technically pieces. Snapping a longship to a chest
            // is not what anyone means by chaining storage.
            if (prefab.GetComponent<Ship>() != null) return false;

            // Exclusions win over everything, including an override - the setting says
            // "whatever else matches", and a list you cannot override is not an escape hatch.
            if (SinkaConfig.IsExcluded(prefab.name)) return false;

            // Naming a prefab in PointOverrides is a decision, so it does not have to qualify
            // some other way and it skips the buildable filter too - an explicit name is more
            // specific than any heuristic here, and someone naming a loot chest to snap their
            // own pieces against has said what they want. It still loses to an exclusion.
            var named = SinkaConfig.HasPointOverride(prefab.name);

            if (!named && !Buildable.Includes(prefab)) return false;

            if (named) return true;

            if (SinkaConfig.SnapContainers.Value && prefab.GetComponent<Container>() != null)
                return true;

            if (SinkaConfig.SnapFences.Value && SinkaConfig.IsFence(prefab.name))
                return true;

            return SinkaConfig.SnapUnsnappedPieces.Value;
        }

        private static bool HasSnapPoints(Transform root)
        {
            for (var i = 0; i < root.childCount; i++)
                if (root.GetChild(i).CompareTag(Tag)) return true;

            return false;
        }

        private static bool AddCorners(GameObject prefab)
        {
            if (!Footprint(prefab, out var bounds)) return false;

            // Snapping makes the two points the same point, so the gap between two chained
            // chests is twice however far the corners sit outside the box: chest A's right
            // point ends up on chest B's left point, and each contributed half the space.
            // Hence Gap/2, and hence pushing the corners out rather than pulling them in -
            // insetting them would make the chests overlap by the same arithmetic.
            var out2 = Mathf.Max(0f, SinkaConfig.Gap.Value) * 0.5f;
            var extents = bounds.extents + new Vector3(out2, out2, out2);

            foreach (var x in new[] { -1, 1 })
            foreach (var y in new[] { -1, 1 })
            foreach (var z in new[] { -1, 1 })
            {
                var corner = bounds.center + new Vector3(extents.x * x, extents.y * y, extents.z * z);

                // Named rather than numbered because the game prints this in the HUD when
                // you Tab through snap points manually.
                var name = (y < 0 ? "bottom" : "top") + "-"
                           + (z < 0 ? "back" : "front") + "-"
                           + (x < 0 ? "left" : "right");

                Create(prefab, "snap_" + name, corner);
            }

            if (SinkaConfig.Verbose.Value)
                SinkaPlugin.Log.LogInfo(
                    prefab.name + ": footprint " + bounds.size.ToString("F2")
                    + " centred " + bounds.center.ToString("F2"));

            return true;
        }

        // ------------------------------------------------------------------ fence ladder

        /// <summary>
        /// A tall piece with a small step would otherwise carry a hundred points. The cap is
        /// generous enough that no vanilla fence reaches it, and it is reported when it bites
        /// rather than quietly shortening the ladder.
        /// </summary>
        private const int MaxRungs = 24;

        /// <summary>
        /// Fences get the ladder, everything else gets corners.
        ///
        /// A chest sits on a floor and stacks at its own height, so rungs would only give it
        /// more ways to land wrong. A fence follows the ground, and corners give it exactly
        /// two heights to attach at - its base, and a full panel up - neither of which helps
        /// you run a line up a hill.
        /// </summary>
        private static bool UseLadder(GameObject prefab)
        {
            return SinkaConfig.SnapFences.Value
                   && SinkaConfig.FenceLadderStep.Value > 0f
                   && SinkaConfig.IsFence(prefab.name);
        }

        /// <summary>
        /// Points up both ends of the piece, at mid-depth, every FenceLadderStep metres.
        ///
        /// Borrowed from MSchmoecker's FenceSnap, which hand-places seven rungs 0.2m apart
        /// on wood_fence and one below its base. Deriving the rungs from the measured
        /// footprint instead means a modded fence nobody has measured gets the same
        /// treatment from a config entry, which is the trade this mod makes everywhere.
        ///
        /// This does mix point layouts across pieces, which the corner set was uniform to
        /// avoid: a fence rung and a chest corner can now pair up and put the two half a
        /// piece out of line. It is contained by fences being opt-in by name, and it is the
        /// same call FenceSnap made - sloping ground is worth more than that edge.
        /// </summary>
        private static bool AddLadder(GameObject prefab)
        {
            if (!Footprint(prefab, out var bounds)) return false;

            // The fence runs along whichever horizontal axis is longer. wood_fence is wide
            // in x, piece_dvergr_sharpstakes is deep in z, and choosing wrong would put
            // every rung on the two faces a panel never joins along.
            var alongX = bounds.size.x >= bounds.size.z;

            // Same arithmetic as the corners: snapping makes the two points one point, so
            // each piece contributes half the gap.
            var reach = (alongX ? bounds.extents.x : bounds.extents.z)
                        + Mathf.Max(0f, SinkaConfig.Gap.Value) * 0.5f;

            var step = SinkaConfig.FenceLadderStep.Value;
            var bottom = bounds.min.y - Mathf.Max(0f, SinkaConfig.FenceLadderBelow.Value);

            var rungs = Mathf.FloorToInt((bounds.max.y - bottom) / step) + 1;
            var capped = rungs > MaxRungs;
            if (capped) rungs = MaxRungs;

            for (var i = 0; i < rungs; i++)
                AddRung(prefab, bounds, alongX, reach, bottom + i * step);

            // The top of the piece is what stacks one panel on another, so it is worth a
            // rung of its own when the step does not divide the height evenly.
            var highest = bottom + (rungs - 1) * step;
            if (!capped && bounds.max.y - highest > step * 0.25f)
                AddRung(prefab, bounds, alongX, reach, bounds.max.y);

            if (capped)
                SinkaPlugin.Log.LogWarning(
                    prefab.name + " is " + bounds.size.y.ToString("F2") + "m tall and "
                    + "FenceLadderStep is " + step + "m, which wants more than " + MaxRungs
                    + " rungs. The ladder stops at " + highest.ToString("F2")
                    + "m - raise the step to cover the whole piece.");

            if (SinkaConfig.Verbose.Value)
                SinkaPlugin.Log.LogInfo(
                    prefab.name + ": footprint " + bounds.size.ToString("F2")
                    + " centred " + bounds.center.ToString("F2")
                    + ", ladder of " + rungs + " along " + (alongX ? "x" : "z"));

            return true;
        }

        private static void AddRung(
            GameObject prefab, Bounds bounds, bool alongX, float reach, float y)
        {
            foreach (var side in new[] { -1f, 1f })
            {
                // Mid-depth on the axis the fence does not run along. A rung on a corner
                // would chain the panels diagonally offset by half their thickness.
                var position = alongX
                    ? new Vector3(bounds.center.x + reach * side, y, bounds.center.z)
                    : new Vector3(bounds.center.x, y, bounds.center.z + reach * side);

                var end = alongX
                    ? (side < 0f ? "left" : "right")
                    : (side < 0f ? "back" : "front");

                // Height in the name rather than a rung number, because the game prints it
                // in the HUD as you Tab through: on a slope "y0.60" tells you where you are
                // and "rung 3" does not.
                Create(prefab, "snap_" + end + "-y" + y.ToString("F2"), position);
            }
        }

        private static void Create(GameObject prefab, string name, Vector3 localPosition)
        {
            var point = new GameObject(name);
            point.tag = Tag;
            point.transform.SetParent(prefab.transform, false);
            point.transform.localPosition = localPosition;
        }

        // ------------------------------------------------------------------ footprint

        /// <summary>
        /// The piece's own box, in its local space.
        ///
        /// Colliders are read as data (BoxCollider.center/size, mesh bounds) rather than
        /// through Collider.bounds, because prefabs sit inactive in ZNetScene and the
        /// world-space bounds of an inactive collider are not reliable. Transforms work
        /// on inactive objects, so converting the corners by hand is safe where asking
        /// Unity for a world AABB is not.
        /// </summary>
        private static bool Footprint(GameObject prefab, out Bounds bounds)
        {
            bounds = default;
            var found = false;

            // Measure the piece as it will stand, not as the prefab is packaged. A built
            // piece carries its damage states and its destruction chunks in the same
            // prefab - WearNTear holds m_new, m_worn and m_broken as separate subtrees
            // plus m_fragmentRoots - and GetComponentsInChildren(true) sees all of them at
            // once. Measured that way, wood_fence came out 2.72 x 2.30 x 0.85, which is
            // most of a metre wider and three quarters of a metre taller than the panel
            // you actually place, so two chained fences stood 0.72m apart.
            //
            // Points are still parented to the prefab root, so root stays the space we
            // convert into; only the search starts lower down.
            var root = prefab.transform;
            var live = LiveGeometry(prefab);

            foreach (var collider in live.GetComponentsInChildren<Collider>(true))
            {
                if (Skip(collider, root)) continue;
                if (!LocalBounds(collider, out var local)) continue;

                var box = ToRoot(root, collider.transform, local);
                if (!found) { bounds = box; found = true; }
                else bounds.Encapsulate(box);

                // Which collider is responsible for an oversized box is the one thing you
                // want to know when a piece snaps at the wrong distance, and it used to
                // take a rip to find out.
                if (SinkaConfig.Verbose.Value)
                    SinkaPlugin.Log.LogInfo(
                        "    " + PathTo(collider.transform, root) + " " + box.size.ToString("F2"));
            }

            if (found) return true;

            // No usable collider: fall back to the mesh, which is also pure data.
            foreach (var filter in live.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;
                if (IsDisabled(filter.transform, root)) continue;

                var box = ToRoot(root, filter.transform, filter.sharedMesh.bounds);
                if (!found) { bounds = box; found = true; }
                else bounds.Encapsulate(box);
            }

            return found;
        }

        /// <summary>
        /// The subtree that will actually be standing there once the piece is placed.
        ///
        /// Riding WearNTear's own m_new rather than guessing at child names: it is the
        /// field the game itself switches to when a piece is undamaged, so it is by
        /// definition the geometry you are looking at when you line two pieces up. Pieces
        /// without a WearNTear - and pieces whose m_new is the root itself - fall back to
        /// the whole prefab, which is what the old behaviour was for everything.
        /// </summary>
        private static Transform LiveGeometry(GameObject prefab)
        {
            var wear = prefab.GetComponent<WearNTear>();
            if (wear == null || wear.m_new == null || wear.m_new == prefab) return prefab.transform;

            return wear.m_new.transform;
        }

        private static bool Skip(Collider collider, Transform root)
        {
            // A trigger is not geometry.
            if (collider.isTrigger) return true;

            // Damage states and destruction chunks sit switched off until they are needed.
            if (IsDisabled(collider.transform, root)) return true;

            // A collider hanging off its own rigidbody is a loose fragment rather than part
            // of the piece; WearNTear.SetupColliders draws exactly the same line. The body
            // has to be below the root to count, because a piece whose own root carries one
            // would otherwise measure as nothing at all.
            var body = collider.attachedRigidbody;
            if (body != null && body.transform != root) return true;

            return false;
        }

        /// <summary>
        /// Prefabs sit inactive in ZNetScene, so activeInHierarchy is false for every
        /// object in one and tells you nothing. activeSelf is the per-object switch, and it
        /// is what the damage states are actually held behind, so ask that instead - all
        /// the way up to the root, since a whole disabled subtree only says so at its top.
        /// </summary>
        private static bool IsDisabled(Transform transform, Transform root)
        {
            for (var current = transform; current != null && current != root; current = current.parent)
                if (!current.gameObject.activeSelf) return true;

            return false;
        }

        private static string PathTo(Transform transform, Transform root)
        {
            var path = transform.name;
            for (var parent = transform.parent; parent != null && parent != root; parent = parent.parent)
                path = parent.name + "/" + path;

            return path;
        }

        private static bool LocalBounds(Collider collider, out Bounds local)
        {
            switch (collider)
            {
                case BoxCollider box:
                    local = new Bounds(box.center, box.size);
                    return true;

                case MeshCollider mesh when mesh.sharedMesh != null:
                    local = mesh.sharedMesh.bounds;
                    return true;

                case CapsuleCollider capsule:
                    var d = capsule.radius * 2f;
                    local = new Bounds(capsule.center, new Vector3(d, Mathf.Max(capsule.height, d), d));
                    return true;

                case SphereCollider sphere:
                    local = new Bounds(sphere.center, Vector3.one * sphere.radius * 2f);
                    return true;

                default:
                    local = default;
                    return false;
            }
        }

        /// <summary>
        /// Rewrites a child's local box into the root's space by carrying all eight corners
        /// across, so a rotated or scaled child still produces a box that contains it.
        /// </summary>
        private static Bounds ToRoot(Transform root, Transform child, Bounds local)
        {
            var centre = local.center;
            var extents = local.extents;

            var result = new Bounds(root.InverseTransformPoint(child.TransformPoint(centre)), Vector3.zero);

            for (var i = 0; i < 8; i++)
            {
                var corner = centre + new Vector3(
                    (i & 1) == 0 ? -extents.x : extents.x,
                    (i & 2) == 0 ? -extents.y : extents.y,
                    (i & 4) == 0 ? -extents.z : extents.z);

                result.Encapsulate(root.InverseTransformPoint(child.TransformPoint(corner)));
            }

            return result;
        }
    }
}
