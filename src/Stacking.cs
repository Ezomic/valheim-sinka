using HarmonyLib;
using UnityEngine;

namespace Sinka
{
    /// <summary>
    /// A chest can be set down on another chest.
    ///
    /// This is the one thing in this mod that is not a snap point, and it is here because no
    /// snap point could ever have done it. Two separate vanilla rules stand in the way, and
    /// both were read out of the decompiled game rather than guessed at.
    ///
    /// **Placing.** Player.UpdatePlacementGhost ends with a long run of validity tests, and one
    /// of them is `if ((bool)wearNTear && !wearNTear.m_supports) m_placementStatus = Invalid`.
    /// That `wearNTear` belongs to the piece the placement ray **hit**, not to the piece in
    /// hand, so it reads "you may not build on a piece that does not support things". Every
    /// chest ships `m_supports = false`, which is why a chest on a chest is refused before
    /// snapping is even considered - and why a torch on a chest is refused too.
    ///
    /// **Staying.** Getting past that alone would be worse than useless. WearNTear.UpdateWear
    /// runs `if (m_noSupportWear) { UpdateSupport(); if (!HaveSupport()) num = 100f; }`, and
    /// that 100 is a percentage of the piece's own health: a chest that ends up unsupported is
    /// **destroyed, with everything inside it**. The field name reads backwards - it means "no
    /// support, wear", not "no support wear". Chests on the ground are safe because a collider
    /// with no WearNTear of its own, terrain included, hands out max support.
    ///
    /// So both rules are answered, and each as narrowly as it can be: the support flag is
    /// turned on for exactly one frame on the chest you are aiming at while holding another
    /// chest, and the support test is answered true for a chest standing on a chest. Neither
    /// one makes a chest load-bearing for anything else. A wall, a beam or a torch on a chest
    /// is still refused, which is vanilla's answer and not a limitation of this.
    ///
    /// **What this cannot do.** Support is computed by whoever owns the chest's ZDO, and
    /// ownership follows whoever is nearby. A player without Sinka who comes near a stack
    /// computes it the vanilla way, finds no support, and destroys the top chest and its
    /// contents. There is no client-side fix for that: the rule runs on their machine, from
    /// their copy of the prefab. On a pack server where everyone has the mod it does not
    /// arise; on a mixed server it is a real risk, and the config says so next to the switch.
    /// </summary>
    internal static class Stacking
    {
        /// <summary>
        /// How far below its own pivot to look for the chest underneath. A chest's pivot sits
        /// at its base - piece_chest_wood measures 0.74 tall centred 0.37 up - so this is a
        /// probe just under the floor of the piece rather than a search around it.
        /// </summary>
        private const float ProbeDepth = 0.12f;

        /// <summary>
        /// Half-width of that probe. Wide enough that a chest set down a little off centre
        /// still finds the one under it, narrow enough that it cannot reach a chest standing
        /// alongside: the smallest vanilla chest is 0.92 across, so half of that is the limit
        /// worth approaching.
        /// </summary>
        private const float ProbeHalfWidth = 0.35f;

        private static readonly Collider[] Probe = new Collider[16];

        /// <summary>
        /// The chest whose support flag is currently turned on, and its real value. Kept so it
        /// can be put back: this is somebody else's component and it is borrowed, not changed.
        /// </summary>
        private static WearNTear _lent;

        private static bool _lentWas;

        private static int _pieceMask;

        private static int PieceMask
        {
            get
            {
                if (_pieceMask == 0) _pieceMask = LayerMask.GetMask("piece", "piece_nonsolid");
                return _pieceMask;
            }
        }

        internal static void Reset()
        {
            Restore();
        }

        private static void Restore()
        {
            if (_lent == null) { _lent = null; return; }

            _lent.m_supports = _lentWas;
            _lent = null;
        }

        /// <summary>
        /// A piece that holds items and can be built - the same test the snap points use, so a
        /// modded chest is covered without naming it, and a ship is not a chest however much
        /// cargo it carries.
        /// </summary>
        private static bool IsContainer(GameObject piece)
        {
            if (piece == null) return false;

            return piece.GetComponentInChildren<Container>() != null
                   && piece.GetComponent<Ship>() == null;
        }

        private static string _ghostName;
        private static bool _ghostIsContainer;

        /// <summary>
        /// Whether the piece in hand is a chest, asked of the prefab rather than of the ghost.
        ///
        /// SetupPlacementGhost tears a list of components off the copy it makes - Joint,
        /// Rigidbody, TerrainModifier, GuidePoint and more - and Container is not on that list
        /// today. Reading the answer off the prefab instead means it stays right if that list
        /// ever grows, and the ghost carries its prefab's name for exactly this kind of lookup.
        /// Cached against that name, because this runs on every frame a piece is in hand.
        /// </summary>
        private static bool GhostIsContainer(GameObject ghost)
        {
            var name = ghost.name;
            if (name == _ghostName) return _ghostIsContainer;

            _ghostName = name;

            var scene = ZNetScene.instance;
            var prefab = scene == null ? null : scene.GetPrefab(name);

            // No prefab under that name is a ghost of something this mod cannot look up, so
            // fall back to the ghost itself rather than answering no.
            _ghostIsContainer = IsContainer(prefab == null ? ghost : prefab);

            return _ghostIsContainer;
        }

        // ------------------------------------------------------------------ placing

        /// <summary>
        /// Lends the aimed chest a support flag while another chest is in hand, so the one
        /// validity test that refuses the placement reads true and every other test still runs
        /// exactly as it did. Runs inside UpdatePlacementGhost, before that test; the postfix
        /// below hands the flag back immediately after.
        ///
        /// A prefix on UpdatePlacementGhost could not do this - which piece is under the
        /// crosshair is not known until the ray is cast, and the ray is cast inside the method.
        /// Rewriting the test itself would mean a transpiler through a 900-line vanilla method
        /// for one boolean, which is a far worse trade than borrowing a field for a frame.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Player), "PieceRayTest")]
        private static void Aimed(Player __instance, bool __result, ref Piece piece, GameObject ___m_placementGhost)
        {
            // Whatever was lent last frame goes back first, so a ray test from somewhere other
            // than the placement ghost can never leave a chest holding a flag it did not have.
            Restore();

            if (!SinkaConfig.StackContainers.Value) return;
            if (!__result || piece == null || ___m_placementGhost == null) return;
            if (__instance != Player.m_localPlayer) return;

            if (!GhostIsContainer(___m_placementGhost)) return;
            if (!IsContainer(piece.gameObject)) return;

            var wear = piece.GetComponent<WearNTear>();
            if (wear == null || wear.m_supports) return;

            _lent = wear;
            _lentWas = wear.m_supports;
            wear.m_supports = true;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Player), "UpdatePlacementGhost")]
        private static void Placed()
        {
            Restore();
        }

        // ------------------------------------------------------------------ staying

        /// <summary>
        /// A chest standing on a chest counts as supported, so the wear tick does not decide it
        /// is floating and take its whole health.
        ///
        /// Answered here rather than by making support flow from the chest below, because
        /// support is a network: a chest that genuinely supports would also hold up a wall and
        /// a roof, and would carry its own load down through the stack. This says only "this
        /// particular piece is not floating", which is the claim that is actually true - a
        /// chest set on a chest is resting on something solid.
        ///
        /// The integrity colours still show the top chest as unsupported, because those read
        /// m_support and nothing here writes it. That is cosmetic and left alone.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(WearNTear), "HaveSupport")]
        private static void Supported(WearNTear __instance, ref bool __result)
        {
            if (__result || !SinkaConfig.StackContainers.Value) return;
            if (!IsContainer(__instance.gameObject)) return;

            __result = RestsOnContainer(__instance.transform);
        }

        /// <summary>
        /// Whether another container sits directly underneath. A box probe just below the
        /// piece's own pivot, on the layers the game keeps pieces on, rather than a raycast:
        /// a chest's base is flat and wide, and a single downward ray from the pivot can miss
        /// the chest below entirely when the two are a hand's width out of line.
        /// </summary>
        private static bool RestsOnContainer(Transform chest)
        {
            var centre = chest.position + Vector3.down * ProbeDepth;
            var half = new Vector3(ProbeHalfWidth, ProbeDepth, ProbeHalfWidth);

            var count = Physics.OverlapBoxNonAlloc(
                centre, half, Probe, Quaternion.identity, PieceMask);

            for (var i = 0; i < count; i++)
            {
                var found = Probe[i];
                if (found == null) continue;

                var piece = found.GetComponentInParent<Piece>();
                if (piece == null || piece.transform == chest) continue;

                if (IsContainer(piece.gameObject)) return true;
            }

            return false;
        }
    }
}
