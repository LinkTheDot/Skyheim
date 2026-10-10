# Changelog

## 1.1.18

- Rune of Healing is a configurable heal-over-time. Default is 90% of Skyheim's amount (about 36 HP at quality 1 before Elemental Magic skill), spread over 8 seconds, with the status icon held for at least 10 seconds.
- Heal Radius and Warmth Radius. Skyheim's areas are 5 m and 30 m. Warmth is an area by design.
- Optional Debug Logging for a Rune of Healing cast. It prints whether logging is on and which config file was read.
- Immolation aura hits do not build Epic Loot Eikthyr Shocking Charge. The aura explosion plays once per tick, with Aura Audio Volume Scale.
- The Rune Altar can eitr-imbue armor that Epic Loot has already enchanted. Extra Recipes adds the OdinHorse cloth sets and the Horse Trophy Helmet.

## 1.1.15

- Rune cooldown timers draw on the hotbar and in the inventory. The overlay was being created switched off, so the seconds never appeared.
- Destroyed cooldown overlays are removed before Skyheim updates them, which stops the fillAmount null-reference spam.

## 1.1.13

- Rune magic skills (Nature, Holy, Fire, Frost) have Skill Experience Gain Factor and Skill Experience Loss, with the same defaults as Blacksmithing (1 and 0).

## 1.1.12

- Skyheim buff runes spend their eitr cost when the cast is accepted. Warmth and the other status runes were checking the cost and then never taking it, so they could be clicked forever and the magic skill leveled to the cap.

## 1.1.11

- Null-safe Rune Altar container update, so a missing SkyheimAltarPanel script does not break the inventory.
- Builds the rune cooldown overlay at runtime, so a missing SkyheimCooldownItem script does not leave cooldowns blank.

## 1.1.10

- Fixed a NullReferenceException in Skyheim's windfury damage hook when poison/AoE hits have no attacker, nview, or status manager.

## 1.1.9

- Thunderstore package uses the 1.0 COMPAT icon.

## 1.1.8

- Draws Skyheim rune cooldown seconds on SeneaL hotbar, action, and inventory slots without replacing SeneaL's HUD prefabs.
- If SeneaL UI is not installed, Skyheim keeps its original vanilla hotbar and inventory cooldown overlays.

## 1.1.7

- Stops Skyheim from rewriting the vanilla hotbar/inventory slot prefabs when SeneaL UI is installed, so food, action bar, and minimap can display.

## 1.1.5

- Fixed a stack-overflow crash when hovering enchanted armor in a chest.
- Inventory descriptions are populated directly at pointer-enter, before Valheim tries to show the tooltip.

## 1.1.1

- Fixed item names and descriptions in-game (no more missing/blank localization).
- Fixed a crash that occurred when trying to craft items (including with `devcommands`).
- Added configuration files for all the runes (`BepInEx/config/cjayride.skyheimcompat.cfg` after you load a world once).
- Enforces rune cooldown blocking on current Valheim.
