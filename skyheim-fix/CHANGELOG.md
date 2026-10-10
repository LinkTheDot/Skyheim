# Changelog

## 1.3.30

- Requires Skyheim Compat 1.1.18. Compat has no dependencies of its own.
- No change to skyheim.dll behavior. The version moved so this package depends on the Compat build with the healing rune, immolation, and altar imbue changes.

## 1.3.29

- Requires Skyheim Compat 1.1.15. Compat has no dependencies of its own.
- No change to skyheim.dll behavior. The version moved so this package depends on the Compat build that shows rune cooldown timers.

## 1.3.28

- Requires Skyheim Compat 1.1.13. Compat has no dependencies of its own.
- Status runes spend their eitr cost when the cast is accepted, so Warmth and the other buff runes can no longer be clicked for free skill experience.
- Rune magic skills have Skill Experience Gain Factor (default 1) and Skill Experience Loss (default 0).

## 1.3.27

- Keep the merged DLL's assembly name as `skyheim`, so the asset bundle can attach SkyheimAltarPanel, recipes, status effects, and the other scripts. The log reports Skyheim 1.3.27 instead of 1.3.12.

## 1.3.26

- Bosses drop `eitr_shard_drop` in the configured stack (Eikthyr 5, and the rest of `cjayride.SkyheimEitr.cfg`). The stack is written onto the loot list the ragdoll saves, so boss loot chests receive the full amount instead of Skyheim's built-in 1.

## 1.3.25

- Boss eitr shards stack to 50. Per-player amounts are in `cjayride.SkyheimEitr.cfg` (built into `skyheim.dll`). Dedicated server sends those numbers to clients.

## 1.3.24

- Requires Skyheim Compat 1.1.10 (guards Skyheim's Character.Damage windfury hook so poison/AoE hits no longer NRE).

## 1.3.23

- Requires Skyheim Compat 1.1.9.
- Thunderstore package uses the 1.0 FIX icon.

## 1.3.22

- Requires Skyheim Compat 1.1.8 (SeneaL UI HUD plus rune cooldown overlay).

## 1.3.21

- Valheim 1.0 / InventoryElement rewrite of skyheim.dll.
- Localization, craft crash fix, and rune config live in the Compat companion.
