using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace Cjayride.SkyheimCompat
{
    /// <summary>
    /// Skyheim finds altar armor with Inventory.GetItem(prefabName, -1, true), which also requires the
    /// item's world level to be current and its m_dropPrefab to be the real prefab, and only inspects the
    /// first match. Enchanted (Epic Loot) gear can fail those checks, so the altar ignored it.
    /// This replaces the lookup with one that matches by prefab or item name and checks every item.
    /// </summary>
    [HarmonyPatch]
    internal static class AltarImbueCompat
    {
        private const string ModTypeKey = "sh_mod_type";

        private const string DefaultExtraRecipes =
            "ArmorHorseClothChest_T1:20:20:0.2:0.25:240, " +
            "ArmorHorseClothLegs_T1:15:15:0.1:0.25:240, " +
            "ArmorHorseClothHelmet_T1:8:10:0.2:0.1:120, " +
            "ArmorHorseClothChest_T2:25:25:0:0.1:300, " +
            "ArmorHorseClothLegs_T2:18:15:0:0.1:300, " +
            "ArmorHorseClothHelmet_T2:10:10:0:0.1:120, " +
            "rae_OdinHorse_Helmet:6:5:0.2:0.5:60, " +
            "ArmorLoxChest:25:25:0:0.1:300, " +
            "ArmorLoxLegs:18:15:0:0.1:300, " +
            "HelmetLox:10:10:0:0.1:120";

        private static ConfigEntry<string> _extraRecipes;
        private static readonly PropertyInfo TuningProperty = AccessTools.Property(typeof(SkyheimAltar), "Tuning");

        internal static void Bind(ConfigFile config)
        {
            _extraRecipes = config.Bind(
                "Altar imbue",
                "Extra Recipes",
                DefaultExtraRecipes,
                "Extra armor the Skyheim altar can eitr-imbue, comma separated. Format per entry: " +
                "PrefabName:ShardsRequired:MaxEitr:EitrRegen:ArmorPenalty:Seconds " +
                "(regen and armor penalty are fractions, e.g. 0.2 = 20%). Defaults cover the OdinHorse " +
                "Skogarmadr (T1) and Drapmadr (T2) sets, the Horse Trophy Helmet, and the Valheim 1.0 Lox Fur " +
                "set (ArmorLoxChest, ArmorLoxLegs, HelmetLox), which Skyheim's built-in recipe list predates " +
                "and does not recognize. Entries for prefabs Skyheim already supports replace Skyheim's values.");
        }

        [HarmonyPatch(typeof(SkyheimAltarTuning), nameof(SkyheimAltarTuning.RuntimeDeserialize))]
        [HarmonyPostfix]
        private static void RuntimeDeserialize_Postfix(SkyheimAltarTuning __instance)
        {
            if (__instance?.Recipes == null || _extraRecipes == null)
            {
                return;
            }

            int added = 0;
            foreach (string raw in _extraRecipes.Value.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                SkyheimArmorRecipe recipe = Parse(raw.Trim());
                if (recipe == null)
                {
                    continue;
                }

                __instance.Recipes.RemoveAll(r => string.Equals(r.SourceItemDrop, recipe.SourceItemDrop, StringComparison.Ordinal));
                __instance.Recipes.Add(recipe);
                added++;
            }

            Plugin.Log($"Altar imbue: {added} extra recipe(s) loaded, {__instance.Recipes.Count} total.");
        }

        private static SkyheimArmorRecipe Parse(string entry)
        {
            if (entry.Length == 0)
            {
                return null;
            }

            string[] parts = entry.Split(':');
            if (parts.Length != 6
                || !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int shards)
                || !TryFloat(parts[2], out float eitr)
                || !TryFloat(parts[3], out float regen)
                || !TryFloat(parts[4], out float armor)
                || !TryFloat(parts[5], out float seconds))
            {
                Plugin.LogWarning($"Altar imbue: ignoring malformed recipe '{entry}' (expected Prefab:Shards:Eitr:Regen:Armor:Seconds).");
                return null;
            }

            return new SkyheimArmorRecipe
            {
                SourceItemDrop = parts[0].Trim(),
                EitrShardsRequired = shards,
                EitrModifier = eitr,
                EitrRegenModifier = regen,
                ArmorModifier = armor,
                CraftingTime = seconds
            };
        }

        private static bool TryFloat(string text, out float value)
        {
            return float.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private static SkyheimAltarTuning GetTuning(SkyheimAltar altar)
        {
            return TuningProperty?.GetValue(altar) as SkyheimAltarTuning;
        }

        private static bool Matches(ItemDrop.ItemData item, string prefabName, string sharedName)
        {
            if (item?.m_shared == null)
            {
                return false;
            }

            if (item.m_dropPrefab != null && item.m_dropPrefab.name == prefabName)
            {
                return true;
            }

            return sharedName != null && item.m_shared.m_name == sharedName;
        }

        private static string SharedNameOf(string prefabName)
        {
            GameObject prefab = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(prefabName) : null;
            ItemDrop drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
            return drop?.m_itemData?.m_shared?.m_name;
        }

        private static ItemDrop.ItemData FindImbuable(Inventory inventory, SkyheimArmorRecipe recipe)
        {
            string sharedName = SharedNameOf(recipe.SourceItemDrop);
            foreach (ItemDrop.ItemData item in inventory.GetAllItems())
            {
                if (Matches(item, recipe.SourceItemDrop, sharedName) && !item.m_customData.ContainsKey(ModTypeKey))
                {
                    return item;
                }
            }

            return null;
        }

        private static int CountShards(Inventory inventory, string shardPrefab)
        {
            int count = 0;
            foreach (ItemDrop.ItemData item in inventory.GetAllItems())
            {
                if (item?.m_dropPrefab != null && item.m_dropPrefab.name == shardPrefab)
                {
                    count += item.m_stack;
                }
            }

            return count;
        }

        private static void RemoveShards(Inventory inventory, string shardPrefab, int amount)
        {
            foreach (ItemDrop.ItemData item in new List<ItemDrop.ItemData>(inventory.GetAllItems()))
            {
                if (amount <= 0)
                {
                    break;
                }

                if (item?.m_dropPrefab == null || item.m_dropPrefab.name != shardPrefab)
                {
                    continue;
                }

                int take = Mathf.Min(item.m_stack, amount);
                inventory.RemoveItem(item, take);
                amount -= take;
            }
        }

        [HarmonyPatch(typeof(SkyheimAltar), nameof(SkyheimAltar.GetActiveRecipe))]
        [HarmonyPrefix]
        private static bool GetActiveRecipe_Prefix(SkyheimAltar __instance, ref SkyheimArmorRecipe __result)
        {
            SkyheimAltarTuning tuning = GetTuning(__instance);
            Inventory inventory = __instance.Container?.GetInventory();
            if (tuning?.Recipes == null || inventory == null)
            {
                return true;
            }

            __result = null;
            foreach (SkyheimArmorRecipe recipe in tuning.Recipes)
            {
                if (FindImbuable(inventory, recipe) != null)
                {
                    __result = recipe;
                    break;
                }
            }

            return false;
        }

        [HarmonyPatch(typeof(SkyheimAltar), nameof(SkyheimAltar.HasEitrShards))]
        [HarmonyPrefix]
        private static bool HasEitrShards_Prefix(SkyheimAltar __instance, SkyheimArmorRecipe recipe, ref bool __result)
        {
            SkyheimAltarTuning tuning = GetTuning(__instance);
            Inventory inventory = __instance.Container?.GetInventory();
            if (tuning == null || inventory == null || recipe == null)
            {
                return true;
            }

            __result = CountShards(inventory, tuning.EitrShardPrefab) >= recipe.EitrShardsRequired;
            return false;
        }

        [HarmonyPatch(typeof(SkyheimAltar), "CraftItem")]
        [HarmonyPrefix]
        private static bool CraftItem_Prefix(SkyheimAltar __instance)
        {
            SkyheimAltarTuning tuning = GetTuning(__instance);
            SkyheimAltarContainer chest = __instance.Container;
            Inventory inventory = chest?.GetInventory();
            if (tuning?.Recipes == null || inventory == null)
            {
                return true;
            }

            bool changed = false;
            foreach (SkyheimArmorRecipe recipe in tuning.Recipes)
            {
                ItemDrop.ItemData item = FindImbuable(inventory, recipe);
                if (item == null || CountShards(inventory, tuning.EitrShardPrefab) < recipe.EitrShardsRequired)
                {
                    continue;
                }

                RemoveShards(inventory, tuning.EitrShardPrefab, recipe.EitrShardsRequired);
                item.m_customData[ModTypeKey] = SkyheimAltar.ImbueType.Eitr.ToString();
                item.m_customData["sh_mod_eitr"] = recipe.EitrModifier.ToString();
                item.m_customData["sh_mod_eitr_regen"] = recipe.EitrRegenModifier.ToString();
                item.m_customData["sh_mod_armor"] = recipe.ArmorModifier.ToString();
                item.m_customData["sh_mod_name"] = "Imbued " + item.m_shared.m_name;
                changed = true;
                Plugin.Log($"Altar imbue: imbued {recipe.SourceItemDrop} (+{recipe.EitrModifier} eitr) using {recipe.EitrShardsRequired} shards.");
            }

            if (changed)
            {
                chest.Save();
            }

            return false;
        }
    }
}
