using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace Cjayride.SkyheimCompat
{
    /// <summary>
    /// The altar details already list the shard cost and craft time, but the status line
    /// only said "Needs more Eitr Shards". With too few shards the timer never starts,
    /// so that line is the one players read. Say the numbers there.
    /// </summary>
    [HarmonyPatch(typeof(SkyheimAltarPanel), "UpdateGui")]
    internal static class AltarCostHint
    {
        private static readonly FieldInfo AltarField = AccessTools.Field(typeof(SkyheimAltarPanel), "_altar");
        private static readonly FieldInfo TimeField = AccessTools.Field(typeof(SkyheimAltarPanel), "_timeText");
        private static readonly PropertyInfo TuningProperty = AccessTools.Property(typeof(SkyheimAltar), "Tuning");

        internal static void ApplySeneal(Harmony harmony)
        {
            if (!Plugin.SenealUiLoaded())
            {
                return;
            }

            System.Type view = AccessTools.TypeByName("SeneaLUI.Inv.ContainerView");
            System.Reflection.MethodInfo tick = AccessTools.Method(view, "Tick");
            System.Reflection.FieldInfo sub = AccessTools.Field(view, "_sub");
            System.Type slots = AccessTools.TypeByName("SeneaLUI.Inv.Slots");
            System.Reflection.FieldInfo current = AccessTools.Field(slots, "Current");
            if (tick == null || sub == null || current == null)
            {
                Plugin.LogWarning("SeneaL chest window was not found. Altar shard text stays on Skyheim's own panel.");
                return;
            }

            SenealSub = sub;
            SenealCurrent = current;
            harmony.Patch(tick, postfix: new HarmonyMethod(typeof(AltarCostHint), nameof(SenealTick)));
            Plugin.Log("Rune Altar requirements show on the SeneaL chest subtitle.");
        }

        private const string ImbuedKey = "sh_mod_eitr";

        private static System.Reflection.FieldInfo SenealSub;
        private static System.Reflection.FieldInfo SenealCurrent;
        private static System.Reflection.PropertyInfo SenealText;
        private static Container _container;
        private static SkyheimAltar _altar;
        private static uint _revision = uint.MaxValue;
        private static int _second = int.MinValue;
        private static bool _finishedShown;
        private static string _line;
        private static int _finishedId;
        private static float _finishedAt = -999f;
        private static string _shardPrefab;

        [HarmonyPatch(typeof(SkyheimAltar), "RPC_StopProcess")]
        private static class WatchFinish
        {
            private static void Postfix(SkyheimAltar __instance, bool completed)
            {
                if (!completed || !__instance)
                {
                    return;
                }

                _finishedId = __instance.GetInstanceID();
                _finishedAt = Time.unscaledTime;
            }
        }

        private static void SenealTick(object __instance)
        {
            if (SenealCurrent?.GetValue(null) is not Container container)
            {
                _container = null;
                return;
            }

            if (container != _container)
            {
                _container = container;
                _altar = container.GetComponent<SkyheimAltar>();
                if (!_altar)
                {
                    _altar = container.GetComponentInParent<SkyheimAltar>();
                }

                _revision = uint.MaxValue;
            }

            if (!_altar)
            {
                return;
            }

            int second = _altar.Processing ? Mathf.CeilToInt(_altar.TimeRemaining) : 0;
            bool finished = JustFinished(_altar);
            uint revision = container.m_lastRevision;
            if (revision == _revision && second == _second && finished == _finishedShown)
            {
                return;
            }

            _revision = revision;
            _second = second;
            _finishedShown = finished;
            string line = SenealStatus(_altar);
            if (line == _line)
            {
                return;
            }

            _line = line;
            if (SenealText == null)
            {
                object sub = SenealSub?.GetValue(__instance);
                SenealText = sub?.GetType().GetProperty("text");
                SenealText?.SetValue(sub, line, null);
                return;
            }

            object label = SenealSub?.GetValue(__instance);
            SenealText.SetValue(label, line, null);
        }

        private static string SenealStatus(SkyheimAltar altar)
        {
            if (altar != null && altar.Processing)
            {
                return WaitText(altar.TimeRemaining) + " left";
            }

            SkyheimArmorRecipe recipe = altar != null ? altar.GetActiveRecipe() : null;
            if (recipe == null)
            {
                return HasImbuedArmor(altar) ? (JustFinished(altar) ? "Finished adding Eitr" : "Item already imbued") : "Put armor in";
            }

            int have = CountShards(altar);
            string wait = ShortWait(recipe.CraftingTime);
            return have < recipe.EitrShardsRequired
                ? "Need " + recipe.EitrShardsRequired + ", have " + have + ", " + wait
                : "Close chest, " + wait;
        }

        internal static string Status(SkyheimAltar altar)
        {
            if (altar != null && altar.Processing)
            {
                return "Imbuing. " + WaitText(altar.TimeRemaining) + " left.";
            }

            SkyheimArmorRecipe recipe = altar != null ? altar.GetActiveRecipe() : null;
            if (recipe == null)
            {
                if (!HasImbuedArmor(altar))
                {
                    return "Put a supported armor piece in. The shard cost shows here.";
                }

                return JustFinished(altar) ? "Finished adding Eitr." : "Item already imbued.";
            }

            int have = CountShards(altar);
            string wait = WaitText(recipe.CraftingTime);
            return have < recipe.EitrShardsRequired
                ? $"Need {recipe.EitrShardsRequired} Eitr Shards. You have {have}. Takes {wait}."
                : $"Close the chest to start. Takes {wait}.";
        }

        private static void Postfix(SkyheimAltarPanel __instance)
        {
            if (AltarField?.GetValue(__instance) is not SkyheimAltar altar || altar.Processing)
            {
                return;
            }

            if (TimeField?.GetValue(__instance) is not Text timeText)
            {
                return;
            }

            timeText.horizontalOverflow = HorizontalWrapMode.Wrap;
            timeText.verticalOverflow = VerticalWrapMode.Overflow;
            timeText.text = Status(altar);
        }

        private static bool JustFinished(SkyheimAltar altar)
        {
            return altar && altar.GetInstanceID() == _finishedId && Time.unscaledTime - _finishedAt < 15f;
        }

        private static bool HasImbuedArmor(SkyheimAltar altar)
        {
            Inventory inventory = altar != null && altar.Container ? altar.Container.GetInventory() : null;
            if (inventory == null)
            {
                return false;
            }

            foreach (ItemDrop.ItemData item in inventory.GetAllItems())
            {
                if (item?.m_customData != null && item.m_customData.ContainsKey(ImbuedKey))
                {
                    return true;
                }
            }

            return false;
        }

        private static int CountShards(SkyheimAltar altar)
        {
            if (_shardPrefab == null)
            {
                SkyheimAltarTuning tuning = TuningProperty?.GetValue(altar) as SkyheimAltarTuning;
                _shardPrefab = tuning != null ? tuning.EitrShardPrefab : "";
            }

            Inventory inventory = altar.Container?.GetInventory();
            if (inventory == null || _shardPrefab.Length == 0)
            {
                return 0;
            }

            int count = 0;
            foreach (ItemDrop.ItemData item in inventory.GetAllItems())
            {
                if (item?.m_dropPrefab != null && item.m_dropPrefab.name == _shardPrefab)
                {
                    count += item.m_stack;
                }
            }

            return count;
        }

        private static string ShortWait(float seconds)
        {
            int total = Mathf.Max(0, Mathf.RoundToInt(seconds));
            if (total >= 60 && total % 60 == 0)
            {
                return (total / 60) + " min";
            }

            if (total >= 60)
            {
                return (total / 60) + "m " + (total % 60) + "s";
            }

            return total + "s";
        }

        private static string WaitText(float seconds)
        {
            int total = Mathf.Max(0, Mathf.RoundToInt(seconds));
            if (total >= 60 && total % 60 == 0)
            {
                int minutes = total / 60;
                return minutes == 1 ? "1 minute" : minutes + " minutes";
            }

            if (total >= 60)
            {
                return (total / 60) + " min " + (total % 60) + " sec";
            }

            return total == 1 ? "1 second" : total + " seconds";
        }
    }
}
