using System;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
using skyheim;
using UnityEngine;

namespace Cjayride.SkyheimCompat
{
    /// <summary>
    /// Opt-in logging along the Rune of Healing path. Patches are only installed when enabled,
    /// so a default config leaves the game untouched.
    /// </summary>
    internal static class HealDiagnostics
    {
        private const string HealRunePrefab = "rune_heal";
        private const string HealAoePrefab = "rune_heal_aoe";
        private static readonly int HealStatusHash = "se_rune_heal".GetStableHashCode();

        private static ConfigEntry<bool> _enabled;

        internal static bool Enabled => _enabled != null && _enabled.Value;

        internal static void Bind(ConfigFile config)
        {
            _enabled = config.Bind(
                "Rune healing",
                "Debug Logging",
                false,
                "Log each step of a Rune of Healing cast to BepInEx/LogOutput.log. Requires a game restart to take effect.");
        }

        internal static void Log(string message)
        {
            if (Enabled)
            {
                Plugin.Log("[HealDebug] " + message);
            }
        }

        internal static void Apply(Harmony harmony, string configPath)
        {
            Plugin.Log($"[HealDebug] Debug Logging is {(Enabled ? "ON" : "OFF")} (read from {configPath}).");
            if (!Enabled)
            {
                return;
            }

            TryPatch(harmony, AccessTools.Method(typeof(Humanoid), nameof(Humanoid.StartAttack)), postfix: nameof(StartAttack_Postfix));
            TryPatch(harmony, AccessTools.Method(typeof(Attack), nameof(Attack.OnAttackTrigger)), prefix: nameof(OnAttackTrigger_Prefix));
            TryPatch(harmony, AccessTools.Method(typeof(Aoe), nameof(Aoe.Setup)), postfix: nameof(AoeSetup_Postfix));
            TryPatch(harmony, AccessTools.Method(typeof(Character), nameof(Character.Damage)), prefix: nameof(Damage_Prefix), priority: Priority.First);
            TryPatch(harmony, AccessTools.Method(typeof(Character), "RPC_Damage"), prefix: nameof(RpcDamage_Prefix), priority: Priority.First);
            TryPatch(
                harmony,
                AccessTools.Method(typeof(SEMan), nameof(SEMan.AddStatusEffect), new[] { typeof(int), typeof(bool), typeof(int), typeof(float), typeof(short) }),
                postfix: nameof(AddStatusEffect_Postfix));
        }

        private static void TryPatch(Harmony harmony, MethodInfo target, string prefix = null, string postfix = null, int priority = Priority.Normal)
        {
            if (target == null)
            {
                Plugin.LogWarning($"[HealDebug] Could not find a target for {prefix ?? postfix}; skipped.");
                return;
            }

            try
            {
                harmony.Patch(
                    target,
                    prefix: prefix != null ? new HarmonyMethod(typeof(HealDiagnostics), prefix) { priority = priority } : null,
                    postfix: postfix != null ? new HarmonyMethod(typeof(HealDiagnostics), postfix) { priority = priority } : null);
            }
            catch (Exception ex)
            {
                Plugin.LogWarning($"[HealDebug] Failed to patch {target.DeclaringType?.Name}.{target.Name}: {ex.Message}");
            }
        }

        private static bool IsHealRune(ItemDrop.ItemData item)
        {
            if (item == null)
            {
                return false;
            }

            if (item.m_dropPrefab != null)
            {
                return item.m_dropPrefab.name.StartsWith(HealRunePrefab, StringComparison.Ordinal)
                    && !item.m_dropPrefab.name.StartsWith(HealAoePrefab, StringComparison.Ordinal);
            }

            return item.m_shared?.m_name != null && item.m_shared.m_name.IndexOf(HealRunePrefab, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void StartAttack_Postfix(Humanoid __instance, bool __result)
        {
            if (__instance == null || __instance != Player.m_localPlayer)
            {
                return;
            }

            ItemDrop.ItemData weapon = __instance.GetCurrentWeapon();
            if (!IsHealRune(weapon))
            {
                return;
            }

            float cooldown = SkyheimCooldown.Instance != null ? SkyheimCooldown.Instance.GetCooldown(HealRunePrefab) : -1f;
            bool registered = ObjectDB.instance != null && ObjectDB.instance.GetStatusEffect(HealStatusHash) != null;
            Log($"StartAttack with Rune of Healing -> {(__result ? "started" : "REFUSED")}; se_rune_heal registered {registered}; eitr {Player.m_localPlayer.GetEitr():0.#}/{Player.m_localPlayer.GetMaxEitr():0.#}, cost {weapon.m_shared.m_attack.m_attackEitr:0.#}, cooldown left {cooldown:0.#}s, godMode {__instance.InGodMode()}, debugFly {__instance.IsDebugFlying()}");
        }

        private static void OnAttackTrigger_Prefix(Attack __instance)
        {
            if (__instance?.m_character != Player.m_localPlayer || !IsHealRune(__instance.m_weapon))
            {
                return;
            }

            Log($"Attack trigger fired (type {__instance.m_attackType}, projectile {(__instance.m_attackProjectile != null ? __instance.m_attackProjectile.name : "none")}).");
        }

        private static void AoeSetup_Postfix(Aoe __instance, Character owner, HitData hitData)
        {
            if (__instance == null || !__instance.gameObject.name.StartsWith(HealAoePrefab, StringComparison.Ordinal))
            {
                return;
            }

            Log($"Heal area spawned: owner {(owner != null ? owner.m_name : "none")}, hitOwner {__instance.m_hitOwner}, radius {__instance.m_radius}, status hash {__instance.m_statusEffectHash} (expected {HealStatusHash}), hitData {(hitData != null ? "set" : "null")}.");
        }

        private static void Damage_Prefix(Character __instance, HitData hit)
        {
            if (__instance == null || hit == null || hit.m_statusEffectHash != HealStatusHash)
            {
                return;
            }

            Log($"Heal hit sent to {__instance.m_name} (netview valid {__instance.m_nview != null && __instance.m_nview.IsValid()}).");
        }

        private static void AddStatusEffect_Postfix(SEMan __instance, int nameHash, StatusEffect __result)
        {
            if (nameHash != HealStatusHash)
            {
                return;
            }

            Character character = __instance?.m_character;
            Log($"AddStatusEffect se_rune_heal on {(character != null ? character.m_name : "?")}: {(__result != null ? "new effect added" : "no new effect (refreshed existing, not owner, or not registered)")}.");
        }

        private static void RpcDamage_Prefix(Character __instance, HitData hit)
        {
            if (__instance == null || hit == null || hit.m_statusEffectHash != HealStatusHash)
            {
                return;
            }

            bool owner = __instance.m_nview != null && __instance.m_nview.IsOwner();
            Log($"Heal hit reached {__instance.m_name}: owner {owner}, debugFly {__instance.IsDebugFlying()}, dead {__instance.IsDead()}, teleporting {__instance.IsTeleporting()}, hp {__instance.GetHealth():0}/{__instance.GetMaxHealth():0}, attacker set {hit.HaveAttacker()}, pvp {__instance.IsPVPEnabled()}.");
        }
    }
}
