# Skyheim 1.0 Fix + Compat

Temporary Valheim 1.0 packages by **cjayride** for [Skyheim by makail](https://thunderstore.io/c/valheim/p/makail/Skyheim/).

- **Skyheim 1.0 Fix 1.3.30** — rewritten `skyheim.dll` (bundle scripts bind; bosses drop the configured eitr shard stack). Requires Skyheim Compat 1.1.18.
- **Skyheim Compat 1.1.18** — healing rune heal-over-time, configurable heal and warmth radii, immolation aura guard, and altar imbue for enchanted gear and OdinHorse armor.

Install **both**. Do not also enable original **makail-Skyheim**.

Thunderstore zips for this release:

- `dist/upload/SkyheimFix-1.3.30.zip`
- `dist/upload/SkyheimCompat-1.1.18.zip`

---

# Cjayride Skyheim 1.3.13

Binary compatibility rebuild of makail's Skyheim 1.3.12 for current Valheim.

## Crash that this fixes

```
TypeLoadException: Could not load type of field 'SkyheimAltarPanel:_leftSlot' (7)
due to: Expected reference type but got type kind 17
```

`InventoryGrid.Element` no longer exists. Slot UI is `InventoryElement` (a MonoBehaviour). The original DLL still stored nested `Element` fields, so BepInEx died while adding the plugin component.

## Other API retargets in this DLL

- `EffectList.Create` extra `ZDOID` argument (`ZDOID.None`)
- `SEMan.AddStatusEffect` extra `Int16` variant argument (`0`)
- `Humanoid.IsTeleportable(bool)` (`false`)
- `Character.Message` extra `bool` (`false`)
- `InventoryGrid.m_elements` is `List<InventoryElement>`

## Playtest drop-in

Replace `skyheim.dll` in `makail-Skyheim` with `dist/skyheim.dll`. Keep `skyheim.json` next to the DLL.

## Cooldown + names (compat plugin)

Build:

`dotnet build D:\valheim-mods\cjaycraft\Cjayride.Skyheim\Cjayride.SkyheimCompat\Cjayride.SkyheimCompat.csproj -c Release`

Install `dist/compat/Cjayride.SkyheimCompat.dll` as a second plugin. 1.0.2 embeds `skyheim.json` and injects it before Skyheim item `Awake`, so crafting names/descriptions do not depend on a loose JSON path.
