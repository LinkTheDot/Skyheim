using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace Cjayride.SkyheimCompat
{
    /// <summary>
    /// Immolation is not a status effect: it is the rune_immolate_aoe object attached to the caster
    /// (radius 10, ttl 20, hit every 2s). Each tick hits every enemy in range, and every hit spawns
    /// vfx_fireball_impact + sh_sfx_explode and counts as a weapon strike for Epic Loot.
    /// Aoe.OnHit calls Character.Damage synchronously, so a flag scoped to OnHit identifies aura hits.
    /// </summary>
    [HarmonyPatch]
    internal static class ImmolateAuraGuard
    {
        private const string AoePrefabName = "rune_immolate_aoe";

        private static ConfigEntry<bool> _blockShockingCharge;
        private static ConfigEntry<float> _volumeScale;
        private static ConfigEntry<bool> _oneSoundPerTick;

        private static Aoe _currentAoe;
        private static readonly Dictionary<int, float> LastSoundTime = new Dictionary<int, float>();

        internal static bool InImmolateHit => _currentAoe != null;

        internal static void Bind(ConfigFile config)
        {
            _blockShockingCharge = config.Bind(
                "Epic Loot compat",
                "Block Shocking Charge From Immolation Aura",
                true,
                "When true, hits from the Immolation aura do not build or discharge Epic Loot Eikthyr Shocking Charge.");
            _volumeScale = config.Bind(
                "Rune immolation",
                "Aura Audio Volume Scale",
                0.35f,
                new ConfigDescription(
                    "Volume multiplier for the explosion sound each Immolation aura hit plays (0 = mute).",
                    new AcceptableValueRange<float>(0f, 1f)));
            _oneSoundPerTick = config.Bind(
                "Rune immolation",
                "One Hit Sound Per Tick",
                true,
                "When true, an aura tick that hits several enemies plays the explosion sound once instead of once per enemy.");
        }

        internal static void Apply(Harmony harmony)
        {
            if (_blockShockingCharge == null || !_blockShockingCharge.Value)
            {
                return;
            }

            Type type = AccessTools.TypeByName("EpicLoot.MagicItemEffects.Shards.EikthyrShockingCharge");
            MethodInfo target = type != null ? AccessTools.Method(type, "OnDamageDealt") : null;
            if (target == null)
            {
                return;
            }

            harmony.Patch(target, prefix: new HarmonyMethod(typeof(ImmolateAuraGuard), nameof(ShockingCharge_Prefix)));
            Plugin.Log("Epic Loot: Immolation aura hits will not trigger Eikthyr Shocking Charge.");
        }

        private static bool IsImmolateAoe(Aoe aoe)
        {
            return aoe != null && aoe.gameObject.name.StartsWith(AoePrefabName, StringComparison.Ordinal);
        }

        [HarmonyPatch(typeof(Aoe), "OnHit")]
        [HarmonyPrefix]
        private static void OnHit_Prefix(Aoe __instance)
        {
            _currentAoe = IsImmolateAoe(__instance) ? __instance : null;
        }

        [HarmonyPatch(typeof(Aoe), "OnHit")]
        [HarmonyFinalizer]
        private static void OnHit_Finalizer()
        {
            _currentAoe = null;
        }

        [HarmonyPatch(typeof(EffectList), nameof(EffectList.Create))]
        [HarmonyPostfix]
        private static void EffectList_Create_Postfix(EffectList __instance, GameObject[] __result)
        {
            if (_currentAoe == null || __result == null || _volumeScale == null || __instance != _currentAoe.m_hitEffects)
            {
                return;
            }

            float scale = _volumeScale.Value;
            if (_oneSoundPerTick.Value && !ClaimTickSound(_currentAoe))
            {
                scale = 0f;
            }

            if (scale >= 0.999f)
            {
                return;
            }

            for (int i = 0; i < __result.Length; i++)
            {
                GameObject go = __result[i];
                if (go == null)
                {
                    continue;
                }

                ZSFX[] sounds = go.GetComponentsInChildren<ZSFX>(true);
                for (int s = 0; s < sounds.Length; s++)
                {
                    if (scale <= 0f)
                    {
                        AudioSource source = sounds[s].GetComponent<AudioSource>();
                        if (source != null)
                        {
                            source.mute = true;
                        }

                        continue;
                    }

                    sounds[s].m_minVol *= scale;
                    sounds[s].m_maxVol *= scale;
                }
            }
        }

        private static bool ClaimTickSound(Aoe aoe)
        {
            int id = aoe.GetInstanceID();
            float now = Time.time;
            float window = Mathf.Max(0.25f, aoe.m_hitInterval * 0.5f);
            if (LastSoundTime.TryGetValue(id, out float last) && now - last < window)
            {
                return false;
            }

            if (LastSoundTime.Count > 64)
            {
                LastSoundTime.Clear();
            }

            LastSoundTime[id] = now;
            return true;
        }

        private static bool ShockingCharge_Prefix()
        {
            return !InImmolateHit;
        }
    }
}
