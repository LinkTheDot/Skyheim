using System;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace Cjayride.SkyheimCompat
{
    /// <summary>
    /// Skyheim ships both runes as areas: rune_heal_aoe is 5 m, rune_warmth_aoe is 30 m.
    /// Both hit the caster, allies, and same-faction characters. Hits use Aoe.m_radius.
    /// </summary>
    internal static class RuneAuraRadius
    {
        private const string HealAoe = "rune_heal_aoe";
        private const string WarmthAoe = "rune_warmth_aoe";

        private static ConfigEntry<float> _healRadius;
        private static ConfigEntry<float> _warmthRadius;

        internal static void Bind(ConfigFile config)
        {
            _healRadius = config.Bind(
                "Rune healing",
                "Heal Radius",
                5f,
                new ConfigDescription(
                    "Meters the Rune of Healing reaches around the caster. Skyheim's prefab is 5.",
                    new AcceptableValueRange<float>(1f, 50f)));
            _warmthRadius = config.Bind(
                "Rune warmth",
                "Warmth Radius",
                30f,
                new ConfigDescription(
                    "Meters the Rune of Warmth reaches around the caster. Skyheim's prefab is 30, and it is an area by design.",
                    new AcceptableValueRange<float>(1f, 50f)));
        }

        [HarmonyPatch(typeof(Aoe), nameof(Aoe.Setup))]
        private static class Aoe_Setup_Radius_Patch
        {
            private static void Postfix(Aoe __instance)
            {
                if (__instance == null)
                {
                    return;
                }

                string name = __instance.gameObject.name;
                if (name.StartsWith(HealAoe, StringComparison.Ordinal) && _healRadius != null)
                {
                    __instance.m_radius = _healRadius.Value;
                }
                else if (name.StartsWith(WarmthAoe, StringComparison.Ordinal) && _warmthRadius != null)
                {
                    __instance.m_radius = _warmthRadius.Value;
                }
            }
        }
    }
}
