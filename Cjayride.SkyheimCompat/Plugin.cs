using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using HarmonyLib;
using SimpleJSON;
using skyheim;
using UnityEngine;

namespace Cjayride.SkyheimCompat
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [BepInDependency("skyheim")]
    [BepInDependency("seneaL.valheim.ui", BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGUID = "cjayride.skyheimcompat";
        public const string PluginName = "Cjayride Skyheim Compat";
        public const string PluginVersion = "1.1.24";
        internal const string SenealGuid = "seneaL.valheim.ui";

        internal static Plugin Instance;
        private Harmony _harmony;
        private static MethodInfo _addWord;
        private static bool _applying;
        private static bool _loggedSuccess;

        private void Awake()
        {
            Instance = this;
            WarnIfUnfixedSkyheim();
            _harmony = new Harmony(PluginGUID);
            RemoveSkyheimInventoryTooltipPrefix();
            RemoveSkyheimHudPrefabPatches();
            UnpatchByDeclaringType(typeof(InventoryGui), "UpdateContainer", HarmonyPatchType.Prefix, "SkyheimAltarPanel");
            WindfuryDamageGuard.RemoveOriginal(_harmony);
            AltarPanelFix.Apply(_harmony);
            CooldownPrefabFix.Replace();
            MagicSkillBalance.Bind(Config);
            HealBalance.Bind(Config);
            RuneAuraRadius.Bind(Config);
            HealDiagnostics.Bind(Config);
            AltarImbueCompat.Bind(Config);
            AltarCostHint.ApplySeneal(_harmony);
            ImmolateAuraGuard.Bind(Config);
            _harmony.PatchAll();
            ImmolateAuraGuard.Apply(_harmony);
            HealDiagnostics.Apply(_harmony, Config.ConfigFilePath);
            SenealCooldownOverlay.Init(_harmony);
            ApplySkyheimLocalization();
            Logger.LogInfo($"{PluginName} {PluginVersion} loaded.");
        }

        internal static void Log(string message)
        {
            Instance?.Logger.LogInfo(message);
        }

        internal static void LogWarning(string message)
        {
            Instance?.Logger.LogWarning(message);
        }

        private void WarnIfUnfixedSkyheim()
        {
            const string installHint =
                "Uninstall makail-Skyheim. Install cjayride-SkyheimFix (rewritten skyheim.dll) together with this Compat package. Compat cannot load runes if the original 1.3.12 DLL is present.";

            try
            {
                Type panel = AccessTools.TypeByName("SkyheimAltarPanel");
                if (panel == null)
                {
                    Logger.LogError("SkyheimAltarPanel was not found. " + installHint);
                    return;
                }

                FieldInfo leftSlot = AccessTools.Field(panel, "_leftSlot");
                if (leftSlot == null)
                {
                    return;
                }

                string typeName = leftSlot.FieldType?.Name;
                if (typeName == "InventoryElement")
                {
                    return;
                }

                Logger.LogError($"SkyheimAltarPanel._leftSlot is {typeName}, not InventoryElement. " + installHint);
            }
            catch (TypeLoadException ex)
            {
                Logger.LogError("Skyheim failed to load (InventoryGrid.Element was removed in Valheim 1.0). " + installHint);
                Logger.LogError(ex.ToString());
            }
            catch (Exception ex)
            {
                Logger.LogError("Skyheim failed to load. " + installHint);
                Logger.LogError(ex.ToString());
            }
        }

        internal static bool SenealUiLoaded()
        {
            return Chainloader.PluginInfos != null && Chainloader.PluginInfos.ContainsKey(SenealGuid);
        }

        private static void RemoveSkyheimHudPrefabPatches()
        {
            if (!SenealUiLoaded())
            {
                return;
            }

            UnpatchByDeclaringType(typeof(Hud), "Awake", HarmonyPatchType.Postfix, "SkyheimCooldown");
            UnpatchByDeclaringType(typeof(InventoryGui), "Awake", HarmonyPatchType.Postfix, "SkyheimCooldown");
        }

        private static void UnpatchByDeclaringType(Type originalType, string methodName, HarmonyPatchType kind, string declaringTypeName)
        {
            MethodInfo original = AccessTools.DeclaredMethod(originalType, methodName);
            if (original == null)
            {
                return;
            }

            Patches info = Harmony.GetPatchInfo(original);
            if (info == null)
            {
                return;
            }

            IEnumerable<Patch> patches = kind == HarmonyPatchType.Prefix ? info.Prefixes : info.Postfixes;
            foreach (Patch patch in patches)
            {
                if (patch.PatchMethod?.DeclaringType?.Name != declaringTypeName)
                {
                    continue;
                }

                new Harmony(patch.owner).Unpatch(original, patch.PatchMethod);
                Instance?.Logger.LogInfo($"Removed Skyheim {declaringTypeName} {originalType.Name}.{methodName} so SeneaL UI can own the HUD.");
            }
        }

        private static void RemoveSkyheimInventoryTooltipPrefix()
        {
            MethodInfo original = AccessTools.DeclaredMethod(typeof(InventoryGrid), nameof(InventoryGrid.CreateItemTooltip));
            if (original == null)
            {
                return;
            }

            Patches info = Harmony.GetPatchInfo(original);
            if (info == null)
            {
                return;
            }

            foreach (Patch patch in info.Prefixes)
            {
                if (patch.PatchMethod?.DeclaringType?.Name != "SkyheimArmor")
                {
                    continue;
                }

                new Harmony(patch.owner).Unpatch(original, patch.PatchMethod);
                Instance?.Logger.LogInfo("Removed Skyheim CreateItemTooltip prefix so vanilla inventory tooltips run.");
            }
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
        }

        internal static bool IsBlockedByCooldown(Humanoid character, ItemDrop.ItemData weapon)
        {
            if (character == null || Player.m_localPlayer == null || character != Player.m_localPlayer)
            {
                return false;
            }

            if (SkyheimCooldown.Instance == null)
            {
                return false;
            }

            SkyheimItemData data = GetRuneData(weapon);
            if (data == null || data.Cooldown <= 0f)
            {
                return false;
            }

            return SkyheimCooldown.Instance.GetCooldown(data.gameObject.name) > 0f;
        }

        internal static void ApplySkyheimLocalization(string languageName = null)
        {
            if (_applying || Localization.instance == null)
            {
                return;
            }

            _applying = true;
            try
            {
                JSONNode root = JSON.Parse(ReadLocalizationJson());
                string selectedLanguage = string.IsNullOrEmpty(languageName) ? "English" : languageName;
                JSONNode language = root[selectedLanguage] ?? root["English"];
                if (language == null || language.Count == 0)
                {
                    Instance?.Logger.LogWarning("Skyheim localization JSON had no usable language block.");
                    return;
                }

                if (_addWord == null)
                {
                    _addWord = typeof(Localization).GetMethod("AddWord", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                }

                if (_addWord == null)
                {
                    Instance?.Logger.LogWarning("Localization.AddWord was not found.");
                    return;
                }

                int count = 0;
                foreach (KeyValuePair<string, JSONNode> entry in language.Linq)
                {
                    _addWord.Invoke(Localization.instance, new object[] { entry.Key, (string)entry.Value });
                    count++;
                }

                if (!_loggedSuccess)
                {
                    _loggedSuccess = true;
                    Instance?.Logger.LogInfo($"Applied {count} Skyheim localization entries ({selectedLanguage}).");
                }
            }
            catch (Exception ex)
            {
                Instance?.Logger.LogError($"Skyheim localization load failed: {ex}");
            }
            finally
            {
                _applying = false;
            }
        }

        private static string ReadLocalizationJson()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            string resourceName = Array.Find(assembly.GetManifestResourceNames(), name => name.EndsWith("skyheim.json", StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(resourceName))
            {
                using Stream stream = assembly.GetManifestResourceStream(resourceName);
                using StreamReader reader = new StreamReader(stream);
                return reader.ReadToEnd();
            }

            string skyheimDir = Path.GetDirectoryName(typeof(SkyheimPlugin).Assembly.Location);
            string nextToSkyheim = Path.Combine(skyheimDir ?? string.Empty, "skyheim.json");
            if (File.Exists(nextToSkyheim))
            {
                return File.ReadAllText(nextToSkyheim);
            }

            throw new FileNotFoundException("Embedded skyheim.json was missing and no loose skyheim.json was found next to skyheim.dll.");
        }

        private static SkyheimItemData GetRuneData(ItemDrop.ItemData item)
        {
            if (item == null)
            {
                return null;
            }

            GameObject prefab = item.m_dropPrefab;
            if (prefab == null && ObjectDB.instance != null && item.m_shared != null)
            {
                prefab = ObjectDB.instance.GetItemPrefab(item.m_shared.m_name);
            }

            return prefab != null ? prefab.GetComponent<SkyheimItemData>() : null;
        }
    }

    [HarmonyPatch(typeof(Localization), nameof(Localization.SetLanguage))]
    internal static class Localization_SetLanguage_Patch
    {
        private static void Postfix(string language)
        {
            Plugin.ApplySkyheimLocalization(language);
        }
    }

    [HarmonyPatch(typeof(SkyheimItemData), "Awake")]
    internal static class SkyheimItemData_Awake_Patch
    {
        private static void Prefix()
        {
            Plugin.ApplySkyheimLocalization();
        }
    }

    [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.Awake))]
    [HarmonyPriority(Priority.First)]
    internal static class ObjectDB_Awake_Patch
    {
        private static void Prefix()
        {
            Plugin.ApplySkyheimLocalization();
        }
    }

    [HarmonyPatch(typeof(Player), nameof(Player.GetCurrentCraftingStation))]
    internal static class Player_GetCurrentCraftingStation_Patch
    {
        private static void Postfix(ref CraftingStation __result)
        {
            if (!__result)
            {
                __result = null;
            }
        }
    }

    [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.StartAttack))]
    [HarmonyPriority(Priority.First)]
    internal static class Humanoid_StartAttack_Patch
    {
        private static bool Prefix(Humanoid __instance, ref bool __result)
        {
            if (!Plugin.IsBlockedByCooldown(__instance, __instance.GetCurrentWeapon()))
            {
                return true;
            }

            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(Attack), nameof(Attack.Start))]
    [HarmonyPriority(Priority.First)]
    internal static class Attack_Start_Patch
    {
        private static bool Prefix(Humanoid character, ItemDrop.ItemData weapon, ref bool __result)
        {
            if (!Plugin.IsBlockedByCooldown(character, weapon))
            {
                return true;
            }

            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(Attack), nameof(Attack.StartWithoutAnimation))]
    [HarmonyPriority(Priority.First)]
    internal static class Attack_StartWithoutAnimation_Patch
    {
        private static bool Prefix(Humanoid character, ItemDrop.ItemData weapon)
        {
            return !Plugin.IsBlockedByCooldown(character, weapon);
        }
    }

    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Awake))]
    [HarmonyPriority(Priority.Last)]
    internal static class InventoryGui_Awake_TooltipPrefab_Patch
    {
        private static void Postfix(InventoryGui __instance)
        {
            if (Plugin.SenealUiLoaded())
            {
                return;
            }

            RestoreTooltipPrefabText(__instance.m_playerGrid);
            RestoreTooltipPrefabText(__instance.m_containerGrid);
            TryInjectInventoryCooldown(__instance);
        }

        private static void TryInjectInventoryCooldown(InventoryGui gui)
        {
            try
            {
                if (!gui || !gui.m_playerGrid || !gui.m_playerGrid.m_elementPrefab || SkyheimCooldown.Instance == null)
                {
                    return;
                }

                GameObject cooldown = AccessTools.Field(typeof(SkyheimCooldown), "_cooldownPrefab")
                    ?.GetValue(SkyheimCooldown.Instance) as GameObject;
                if (!cooldown)
                {
                    return;
                }

                GameObject prefab = gui.m_playerGrid.m_elementPrefab;
                if (prefab.GetComponentInChildren<SkyheimCooldownItem>(true) != null)
                {
                    return;
                }

                GameObject clone = UnityEngine.Object.Instantiate(prefab);
                UnityEngine.Object.Instantiate(cooldown, clone.transform);
                gui.m_playerGrid.m_elementPrefab = clone;
            }
            catch (Exception ex)
            {
                Plugin.LogWarning($"Skipped Skyheim inventory cooldown overlay: {ex.Message}");
            }
        }

        private static void RestoreTooltipPrefabText(InventoryGrid grid)
        {
            if (!grid || !grid.m_elementPrefab)
            {
                return;
            }

            UITooltip tooltip = grid.m_elementPrefab.GetComponent<UITooltip>();
            if (tooltip != null && tooltip.m_text == "")
            {
                tooltip.m_text = null;
            }
        }
    }

    [HarmonyPatch(typeof(InventoryGrid), nameof(InventoryGrid.CreateItemTooltip))]
    internal static class InventoryGrid_CreateItemTooltip_Patch
    {
        private static bool _settingTooltip;

        private static bool Prefix(InventoryGrid __instance, ItemDrop.ItemData item, UITooltip tooltip)
        {
            if (!TryGetCustomName(item, out _))
            {
                return true;
            }

            SetTooltip(__instance, item, tooltip);
            return false;
        }

        internal static void SetTooltip(InventoryGrid grid, ItemDrop.ItemData item, UITooltip tooltip)
        {
            if (_settingTooltip || grid == null || item?.m_shared == null || tooltip == null)
            {
                return;
            }

            string topic = TryGetCustomName(item, out string customName)
                ? customName
                : item.m_shared.m_name;

            _settingTooltip = true;
            try
            {
                tooltip.Set(topic, item.GetTooltip(-1), grid.m_tooltipAnchor, Vector2.zero);
            }
            finally
            {
                _settingTooltip = false;
            }
        }

        private static bool TryGetCustomName(ItemDrop.ItemData item, out string customName)
        {
            customName = null;
            return item?.m_customData != null
                && item.m_customData.TryGetValue("sh_mod_name", out customName)
                && !string.IsNullOrEmpty(customName);
        }
    }

    [HarmonyPatch(typeof(UITooltip), "OnHoverStart")]
    [HarmonyPriority(Priority.First)]
    internal static class UITooltip_OnHoverStart_Patch
    {
        private static void Prefix(UITooltip __instance)
        {
            InventoryElement element = __instance.GetComponent<InventoryElement>();
            if (element == null)
            {
                return;
            }

            InventoryGrid grid = element.GetComponentInParent<InventoryGrid>();
            if (grid == null || grid.m_inventory == null)
            {
                return;
            }

            ItemDrop.ItemData item = grid.m_inventory.GetItemAt(element.Position.x, element.Position.y);
            if (item != null)
            {
                InventoryGrid_CreateItemTooltip_Patch.SetTooltip(grid, item, __instance);
            }
        }
    }

    /// <summary>
    /// Vanilla spends attack eitr only after the animator enters the attack state.
    /// Skyheim buff runes (Warmth and the other status casts) never enter that state,
    /// so the cost is checked and then never taken, and every click still grants skill XP.
    /// Charge the eitr when the cast is accepted, and suppress the later vanilla charge
    /// so projectile runes are not billed twice.
    /// </summary>
    [HarmonyPatch]
    internal static class Attack_SkyheimEitrSpend_Patch
    {
        private static readonly HashSet<Attack> Prepaid = new HashSet<Attack>();
        private static readonly Dictionary<Attack, float> HiddenCost = new Dictionary<Attack, float>();

        [HarmonyPatch(typeof(Attack), nameof(Attack.Start))]
        [HarmonyPostfix]
        private static void Start_Postfix(Attack __instance, ref bool __result)
        {
            if (!__result || __instance?.m_character == null || __instance.m_weapon == null)
            {
                return;
            }

            if (__instance.m_weapon.m_dropPrefab == null || __instance.m_weapon.m_dropPrefab.GetComponent<SkyheimItemData>() == null)
            {
                return;
            }

            float cost = __instance.GetAttackEitr(__instance.m_character, __instance.m_weapon);
            if (cost <= 0f)
            {
                return;
            }

            __instance.m_character.UseEitr(cost);
            Prepaid.Add(__instance);
        }

        [HarmonyPatch(typeof(Attack), nameof(Attack.Update))]
        [HarmonyPrefix]
        private static void Update_Prefix(Attack __instance)
        {
            if (!Prepaid.Contains(__instance))
            {
                return;
            }

            if (__instance.m_wasInAttack || __instance.m_attackDone)
            {
                Prepaid.Remove(__instance);
                return;
            }

            HiddenCost[__instance] = __instance.m_attackEitr;
            __instance.m_attackEitr = 0f;
        }

        [HarmonyPatch(typeof(Attack), nameof(Attack.Update))]
        [HarmonyPostfix]
        private static void Update_Postfix(Attack __instance)
        {
            if (HiddenCost.TryGetValue(__instance, out float cost))
            {
                __instance.m_attackEitr = cost;
                HiddenCost.Remove(__instance);
            }
        }
    }

    [HarmonyPatch(typeof(Attack), nameof(Attack.HaveAmmo))]
    [HarmonyPriority(Priority.Last)]
    internal static class Attack_HaveAmmo_Patch
    {
        private static void Postfix(Humanoid character, ItemDrop.ItemData weapon, ref bool __result)
        {
            if (__result && Plugin.IsBlockedByCooldown(character, weapon))
            {
                __result = false;
            }
        }
    }
}
