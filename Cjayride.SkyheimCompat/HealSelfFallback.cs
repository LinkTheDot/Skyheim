using System;
using HarmonyLib;
using UnityEngine;

namespace Cjayride.SkyheimCompat
{
    /// <summary>
    /// rune_heal_aoe hits once, from a physics overlap where it spawns. If that overlap misses the
    /// caster's collider, the caster never gets se_rune_heal. When the check finishes without hitting
    /// the local caster, give the caster the effect directly. Allies still depend on the overlap.
    /// </summary>
    [HarmonyPatch]
    internal static class HealSelfFallback
    {
        private const string HealAoe = "rune_heal_aoe";
        private static readonly int HealStatusHash = "se_rune_heal".GetStableHashCode();

        private static Aoe _checking;
        private static bool _ownerHit;

        private static bool IsHealAoe(Aoe aoe)
        {
            return aoe != null && aoe.gameObject.name.StartsWith(HealAoe, StringComparison.Ordinal);
        }

        [HarmonyPatch(typeof(Aoe), "CheckHits")]
        [HarmonyPrefix]
        private static void CheckHits_Prefix(Aoe __instance)
        {
            _checking = IsHealAoe(__instance) ? __instance : null;
            _ownerHit = false;
        }

        [HarmonyPatch(typeof(Character), nameof(Character.Damage))]
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static void Damage_Prefix(Character __instance, HitData hit)
        {
            if (_checking != null && hit != null && __instance == _checking.m_owner && hit.m_statusEffectHash == HealStatusHash)
            {
                _ownerHit = true;
            }
        }

        [HarmonyPatch(typeof(Aoe), "CheckHits")]
        [HarmonyFinalizer]
        private static void CheckHits_Finalizer(Aoe __instance)
        {
            Aoe aoe = _checking;
            bool ownerHit = _ownerHit;
            _checking = null;
            _ownerHit = false;

            if (aoe == null || aoe != __instance || ownerHit || !aoe.m_hitOwner)
            {
                return;
            }

            Player player = aoe.m_owner as Player;
            if (player == null || player != Player.m_localPlayer || player.IsDead() || player.IsDebugFlying())
            {
                return;
            }

            int hash = aoe.m_statusEffectHash != 0 ? aoe.m_statusEffectHash : HealStatusHash;
            if (hash != HealStatusHash)
            {
                return;
            }

            if (Vector3.Distance(player.GetCenterPoint(), aoe.transform.position) > aoe.m_radius + 2f)
            {
                return;
            }

            float skillLevel = aoe.m_skill != Skills.SkillType.None && aoe.m_skill != Skills.SkillType.All
                ? player.GetSkillLevel(aoe.m_skill)
                : 0f;

            SEMan seman = player.GetSEMan();
            seman.RemoveStatusEffect(hash, true);
            seman.AddStatusEffect(hash, true, aoe.m_level, skillLevel);
            HealDiagnostics.Log($"Heal area missed the caster; applied se_rune_heal directly (quality {aoe.m_level}, skill {skillLevel:0.#}).");
        }
    }
}
