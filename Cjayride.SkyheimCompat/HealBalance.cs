using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
using skyheim;
using UnityEngine;

namespace Cjayride.SkyheimCompat
{
    internal static class HealBalance
    {
        private const float TickInterval = 1f;
        private const string RuneHealNameToken = "rune_heal";
        private static readonly int HealRuneStatusHash = "se_rune_heal".GetStableHashCode();

        private static ConfigEntry<float> _totalMultiplier;
        private static ConfigEntry<float> _healDurationSeconds;
        private static ConfigEntry<float> _minIconSeconds;
        private static ConfigEntry<bool> _showHealVfx;

        private static readonly FieldInfo BaseHealth = AccessTools.Field(typeof(SE_Heal), "_baseHealth");
        private static readonly FieldInfo HealthPerQuality = AccessTools.Field(typeof(SE_Heal), "_healthPerQuality");
        private static readonly FieldInfo DurationPerSkill = AccessTools.Field(typeof(SE_Heal), "_durationPerSkill");

        internal static void Bind(ConfigFile config)
        {
            _totalMultiplier = config.Bind(
                "Rune healing",
                "Total Heal Multiplier",
                0.9f,
                new ConfigDescription(
                    "Fraction of Skyheim's healing rune HP. 1 = 40 HP at quality 1 before Elemental Magic skill; 0.9 is about 36 HP. Each quality adds 5 HP before this multiplier.",
                    new AcceptableValueRange<float>(0.05f, 2f)));
            _healDurationSeconds = config.Bind(
                "Rune healing",
                "Heal Duration Seconds",
                8f,
                new ConfigDescription(
                    "Spread healing over this many seconds (HoT).",
                    new AcceptableValueRange<float>(1f, 30f)));
            _minIconSeconds = config.Bind(
                "Rune healing",
                "Min Status Icon Seconds",
                10f,
                new ConfigDescription(
                    "How long the healing status icon stays on the effect bar (allies included).",
                    new AcceptableValueRange<float>(3f, 60f)));
            _showHealVfx = config.Bind(
                "Rune healing",
                "Show Heal Vfx On Allies",
                true,
                "Spawn a short heal burst VFX on each character when the healing rune HoT is applied.");
        }

        internal static bool IsSkyheimHealRune(SE_Heal heal)
        {
            if (heal == null)
            {
                return false;
            }

            if (heal.NameHash() == HealRuneStatusHash)
            {
                return true;
            }

            string name = heal.name ?? string.Empty;
            if (name.IndexOf(RuneHealNameToken, System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            string loc = heal.m_name ?? string.Empty;
            return loc.IndexOf("sh_se_rune_heal", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        internal static bool TryApplyPackHeal(SE_Heal instance, int itemLevel, float skillLevel)
        {
            if (instance == null || _totalMultiplier == null)
            {
                return false;
            }

            float baseHealth = BaseHealth?.GetValue(instance) is float health ? health : 40f;
            float perQuality = HealthPerQuality?.GetValue(instance) is float qualityStep ? qualityStep : 5f;
            float perSkill = DurationPerSkill?.GetValue(instance) is float skillStep ? skillStep : 0.01f;
            int quality = itemLevel < 1 ? 1 : itemLevel;
            float skillMultiplier = 1f + perSkill * skillLevel;
            float total = (baseHealth + perQuality * (quality - 1)) * skillMultiplier * _totalMultiplier.Value;
            if (total <= 0f)
            {
                return false;
            }

            float hotDuration = Mathf.Max(TickInterval, _healDurationSeconds.Value);
            float iconTtl = Mathf.Max(hotDuration, _minIconSeconds.Value);
            int ticks = Mathf.Max(1, Mathf.CeilToInt(hotDuration / TickInterval));
            float tickHp = total / ticks;

            instance.m_ttl = iconTtl;
            instance.m_healthOverTimeDuration = hotDuration;
            instance.m_healthOverTimeInterval = TickInterval;
            instance.m_healthOverTimeTicks = ticks;
            instance.m_healthOverTimeTickHP = tickHp;
            instance.m_healthOverTime = 0f;

            if (instance.m_character != null)
            {
                HealDiagnostics.Log($"SE_Heal applied to {instance.m_character.m_name}: quality {quality}, skill {skillLevel:0.#}, total {total:0.#} HP over {hotDuration:0.#}s ({ticks} ticks of {tickHp:0.##}), hp {instance.m_character.GetHealth():0}/{instance.m_character.GetMaxHealth():0}");
            }

            return true;
        }

        internal static void TryPlayHealVfx(Character character)
        {
            if (_showHealVfx == null || !_showHealVfx.Value || character == null)
            {
                return;
            }

            GameObject prefab = ZNetScene.instance?.GetPrefab("vfx_Potion_health_medium");
            if (prefab == null)
            {
                prefab = ZNetScene.instance?.GetPrefab("vfx_Heal");
            }

            if (prefab == null)
            {
                return;
            }

            Object.Instantiate(prefab, character.GetCenterPoint(), Quaternion.identity);
        }
    }

    [HarmonyPatch(typeof(SE_Heal), nameof(SE_Heal.SetLevel))]
    internal static class SE_Heal_SetLevel_PackBalance_Patch
    {
        private static bool Prefix(SE_Heal __instance, int itemLevel, float skillLevel)
        {
            if (__instance == null || !HealBalance.IsSkyheimHealRune(__instance))
            {
                return true;
            }

            if (!HealBalance.TryApplyPackHeal(__instance, itemLevel, skillLevel))
            {
                return true;
            }

            return false;
        }
    }

    [HarmonyPatch(typeof(SE_Heal), nameof(SE_Heal.Setup))]
    internal static class SE_Heal_Setup_Vfx_Patch
    {
        private static void Postfix(SE_Heal __instance, Character character)
        {
            if (__instance == null || character == null || !HealBalance.IsSkyheimHealRune(__instance))
            {
                return;
            }

            HealBalance.TryPlayHealVfx(character);
        }
    }
}
