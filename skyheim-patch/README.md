# Skyheim Compat

**Required companion for Skyheim 1.0 Fix**  
by **cjayride**

---

## Important Credit & Disclaimer

**This is NOT an original mod.**

All credit for Skyheim goes to the original developer **makail** (also known as Makzimus).

- Original mod: [Skyheim by makail on Thunderstore](https://thunderstore.io/c/valheim/p/makail/Skyheim/)
- Original Nexus page: [Skyheim on Nexus Mods](https://www.nexusmods.com/valheim/mods/916)

This is a **companion plugin** for **Skyheim 1.0 Fix**. It is not Skyheim by itself and will not work without the Fix package (`skyheim.dll`).

If makail ever releases an official update, **please switch back to the original mod** and remove both this companion and Skyheim 1.0 Fix.

---

### Latest Update (1.1.18)

- Rune of Healing is a configurable heal-over-time (default about 36 HP at quality 1, over 8 seconds).
- Heal and Warmth area radii are configurable. Skyheim's sizes are 5 m and 30 m.
- Immolation aura hits no longer build Epic Loot Eikthyr Shocking Charge, and the explosion sound can be scaled down.
- The Rune Altar can eitr-imbue Epic Loot enchanted armor, plus OdinHorse cloth armor and the Horse Trophy Helmet.

### Latest Update (1.1.15)

- Rune cooldown timers show on the hotbar and in the inventory.
- Stops the null-reference spam from Skyheim updating a cooldown image that was already destroyed.

### Latest Update (1.1.10)

- Fixed a console NullReferenceException from Skyheim when poison/AoE damage ticks (windfury damage hook).

### Latest Update (1.1.9)

- Thunderstore package uses the 1.0 COMPAT icon.

### 1.1.8

- Draws Skyheim rune cooldown seconds on SeneaL hotbar, action, and inventory slots without replacing SeneaL's HUD prefabs.
- If SeneaL UI is not installed, Skyheim keeps its original vanilla hotbar and inventory cooldown overlays.

### 1.1.7

- Stops Skyheim from rewriting the vanilla hotbar/inventory slot prefabs when SeneaL UI is installed, so food, action bar, and minimap can display.

### 1.1.5

- Fixed a stack-overflow crash when hovering enchanted armor in a chest.
- Inventory descriptions are populated directly at pointer-enter, before Valheim tries to show the tooltip.

### 1.1.1

- Fixed item names and descriptions in-game (no more missing/blank localization).
- Fixed a crash that occurred when trying to craft items (including with `devcommands`).
- Added configuration files for all the runes (`BepInEx/config/cjayride.skyheimcompat.cfg` after you load a world once).
- Enforces rune cooldown blocking on current Valheim.

---

## Installation

### Thunderstore / r2modman / Gale

Install **Skyheim 1.0 Fix**. This Compat package is pulled in as a dependency.

Do **not** also enable original **makail-Skyheim**. That DLL fails on Valheim 1.0 (`TypeLoadException` / `Element`) and all runes vanish.

### Manual / Nexus

1. Install **BepInEx**.
2. Install **Skyheim 1.0 Fix 1.3.24**.
3. Install this **Skyheim Compat 1.1.10** package into `BepInEx/plugins`.
4. Launch the game.

Do **not** use this with the original (unfixed) Skyheim.

---

## Notes

- This is a temporary community fix.
- Report issues with this fix to **cjayride**.

Thank you to **makail** for creating Skyheim.  
— cjayride
