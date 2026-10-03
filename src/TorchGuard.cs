using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Sinka
{
    /// <summary>What a torch on a pole keeps the pole safe from. See SinkaConfig.ProtectFrom.</summary>
    internal enum Protection
    {
        All,
        Fire,
        Off
    }

    /// <summary>
    /// A pole with a torch snapped into it takes no damage from hits, or none from fire.
    ///
    /// **The protection belongs to the torch, so nothing is stored.** "Attached" is answered
    /// from the world each time the pole is hit: a paired torch stands in a pole when its
    /// socket point sits on the pole's top snap point, which is exactly where the snap put it.
    /// There is no flag on the pole or the torch to lose at logout, and taking the torch away
    /// takes the protection with it because the question is simply asked again and the answer
    /// is no. A torch merely set on top with the place-without-snapping key keeps its socket
    /// most of a metre above the pole's top, so it protects nothing, which is the rule that
    /// Sinka only acts on what it snapped.
    ///
    /// **The seam is WearNTear.ApplyDamage**, which every hit ends in: RPC_Damage calls it after
    /// resistances, tool tier and the private-area check, and the Cinder fire of the Ashlands
    /// arrives as a hit like any other. RPC_Damage returns early unless this client owns the
    /// piece, so the decision is made by the owner and a write to a pole someone else owns
    /// (which the game discards) is never relied on. The owner reads only positions and prefab
    /// names, and a torch within a tenth of a metre of the pole is always in the same zone.
    ///
    /// Only a call that carries a HitData is touched. ApplyDamage with none is the wear tick:
    /// ash, lava, a pole standing outside its biome and, above all, a piece that has lost its
    /// support. Shielding that would leave a pole hanging in the air for good, and a torch is
    /// not a reason for that.
    ///
    /// The torch itself is never protected: it is a target of nothing.
    /// </summary>
    internal static class TorchGuard
    {
        /// <summary>
        /// How far the torch's socket may sit from the pole's top point and still count. The
        /// snap makes them the same point, so this is room for float noise and nothing else.
        /// </summary>
        private const float Seated = 0.1f;

        /// <summary>
        /// Piece.GetAllPiecesInRadius measures from each piece's PIVOT, and the socket is not at
        /// the pivot: AddSocket puts it at (visual.max.y - stickOut) up the torch, so the pivot
        /// of a seated torch is that far BELOW the pole's top point. For the wood torch that is
        /// about 0.55 m, which a 0.5 m search never reached. 1.5 m clears the socket height of
        /// any standing torch with room to spare, and Seats then does the exact test.
        /// </summary>
        private const float Search = 1.5f;

        private static bool _failed;

        private static readonly List<Piece> Nearby = new List<Piece>();

        [HarmonyPrefix]
        [HarmonyPatch(typeof(WearNTear), nameof(WearNTear.ApplyDamage))]
        private static bool Damaged(WearNTear __instance, ref float damage, HitData hitData)
        {
            try
            {
                return Decide(__instance, ref damage, hitData);
            }
            catch (Exception e)
            {
                if (!_failed)
                {
                    _failed = true;
                    SinkaPlugin.Log.LogWarning("Torch protection failed, damage proceeds: " + e);
                }
                return true;
            }
        }

        private static bool Decide(WearNTear __instance, ref float damage, HitData hitData)
        {
            var mode = SinkaConfig.ProtectFrom.Value;
            if (mode == Protection.Off || hitData == null || damage <= 0f) return true;
            if (!Protected(__instance)) return true;

            if (mode == Protection.All) return false;

            if (hitData.m_hitType == HitData.HitType.CinderFire || hitData.m_hitType == HitData.HitType.Burning)
                return false;

            // The fire share is what is left in the hit after resistances, which is what
            // damage was summed from, so taking it off leaves the rest of the blow intact.
            damage = Mathf.Max(0f, damage - hitData.m_damage.m_fire);
            return damage > 0f;
        }

        private static bool Protected(WearNTear pole)
        {
            Piece piece;
            if (!pole.TryGetComponent(out piece)) return false;

            var name = PrefabName(pole.gameObject.name);
            if (!SinkaConfig.IsSocketTarget(name)) return false;

            var top = SocketPoints.TopPoint(pole.transform);
            if (top == null) return false;

            Nearby.Clear();
            Piece.GetAllPiecesInRadius(top.position, Search, Nearby);

            foreach (var other in Nearby)
            {
                if (other == piece) continue;
                if (Seats(other, name, top.position)) return true;
            }

            return false;
        }

        private static bool Seats(Piece torch, string poleName, Vector3 topPoint)
        {
            var socket = SinkaConfig.SocketFor(PrefabName(torch.gameObject.name));
            if (socket == null || !socket.Targets.Contains(poleName)) return false;

            ZNetView view;
            if (!torch.TryGetComponent(out view) || !view.IsValid()) return false;

            var point = torch.transform.Find(SocketPoints.SocketName);
            return point != null && (point.position - topPoint).sqrMagnitude <= Seated * Seated;
        }

        private static string PrefabName(string name)
        {
            var cut = name.IndexOf("(Clone)", StringComparison.Ordinal);
            return cut < 0 ? name : name.Substring(0, cut);
        }
    }
}
