# Crafting Panel Hierarchy Notes

This document preserves the useful findings from the temporary runtime
`InventoryGui.m_crafting` hierarchy dumps. The debug dump options were removed
after the panel structure was understood.

Source dumps were captured from:

- `BepInEx/config/InventorySlots/CraftingPanelHierarchy_20260512_181321.txt`
- `BepInEx/config/InventorySlots/CraftingPanelHierarchy_20260512_181348.txt`
- `BepInEx/config/InventorySlots/CraftingPanelHierarchy_20260512_181354.txt`

## Root

The crafting panel root is:

```text
_GameMain/LoadingGUI/PixelFix/IngameGui/Inventory_screen/root/Crafting
```

Observed root transform:

```text
anchorMin=(1,1)
anchorMax=(1,1)
pivot=(1,1)
sizeDelta=(570,650)
```

InventorySlots extends this root downward by the fixed crafting panel bottom
extension and protects selected direct children so their visual positions do
not drift when the root height changes.

## InventoryGui Field References

Important vanilla `InventoryGui` references under `m_crafting`:

| Field | Observed transform |
| --- | --- |
| `m_recipeListRoot` | `Crafting/RecipeList/Recipes/ListRoot` |
| `m_recipeListScroll` | `Crafting/RecipeList/RecipeScroll` |
| `m_recipeIcon` | `Crafting/Decription/Icon` |
| `m_recipeName` | `Crafting/Decription/Name` |
| `m_recipeDecription` | `Crafting/Decription/Description` |
| `m_itemCraftType` | `Crafting/Decription/CraftType` |
| `m_variantButton` | `Crafting/Decription/SelectVariant` |
| `m_minStationLevelIcon` | `Crafting/MinLevel` |
| `m_minStationLevelText` | `Crafting/level_text` |
| `m_craftButton` | `Crafting/CraftButton` |
| `m_craftProgressPanel` | `Crafting/Progress` |
| `m_tabCraft` | `Crafting/TabsButtons/Craft` |
| `m_tabUpgrade` | `Crafting/TabsButtons/UPGRADE` |
| `m_repairButton` | `Crafting/RepairButton` |
| `m_repairButtonGlow` | `Crafting/RepairButton/Glow` |
| `m_recipeRequirementList[0..3]` | `Crafting/res_bkg`, `res_bkg (1)`, `res_bkg (2)`, `res_bkg (3)` |

Note: `Decription` is the vanilla object name spelling.

## Key Vanilla Children

Common direct children observed under `Crafting`:

- `Darken`
- `selected_frame`
- `RepairSimple`
- `Bkg`
- `topic`
- `TitlePanel`
- `BraidLineHorisontalMedium`
- `station_icon`
- `TabsButtons`
- `RecipeList`
- `Decription`
- `MinLevel`
- `level_text`
- `CraftButton`
- `Progress`
- `res_bkg` through `res_bkg (3)`
- `RepairButton`

The `Bkg` image is the main wood panel background. `Darken` is a full panel
overlay. The old vanilla station level display uses `MinLevel` and
`level_text`; InventorySlots hides or repurposes these when the crafting
redesign is active.

## Tabs And Foreign UI

Vanilla tabs are children of:

```text
Crafting/TabsButtons
```

Observed vanilla tab buttons:

- `Crafting/TabsButtons/Craft`
- `Crafting/TabsButtons/UPGRADE`

VNEI adds a tab in the same tab root:

```text
Crafting/TabsButtons/VNEI
```

This means foreign tab detection should continue to respect tab buttons under
the vanilla `TabsButtons` root. InventorySlots should hide its own crafting
redesign UI on active foreign tabs, but avoid repositioning or destroying
foreign tab content.

## VNEI Attach Points

The dump showed VNEI content attached below the crafting root, for example:

```text
Crafting/VNEI(Clone)/root/Search/Content/_Template(Clone)
```

VNEI item cells contain their own `Background`, `Icon`, `Quality`, `Favorite`,
and `UITooltip` objects. InventorySlots should not treat VNEI cells as
InventorySlots crafting grid cells.

## Jewelcrafting Attach Points

Jewelcrafting socket visuals were observed under the vanilla description area:

```text
Crafting/Decription/Jewelcrafting Socket 0
Crafting/Decription/Jewelcrafting Socket 1
...
```

InventorySlots suppresses these while the crafting redesign owns recipe
tooltip/gem-row presentation. This avoids socket icons leaking into the
redesigned grid or pinned tooltip overlays.

## Resize Protection

When the crafting panel root is extended downward, children that should not
move visually need their world position restored after the root size changes.

Known children worth protecting:

- `RepairSimple`
- `Darken`
- `selected_frame`
- `RepairSimple(Clone)` if present
- `Bkg`
- `topic`
- `TitlePanel`
- `BraidLineHorisontalMedium`
- `station_icon`
- `TabsButtons`
- `RecipeList`
- `Decription`
- `MinLevel`
- `level_text`
- `CraftButton`
- `Progress`
- `res_bkg` and related requirement slots
- `RepairButton`

This is intentionally broader than only vanilla fields because foreign tab
buttons may also need to keep their position when the root is resized.

## Removed Debug Options

The following temporary debug options were removed:

- `Dump Crafting Panel Hierarchy`
- `Trace RectTransform Parent Setter`

The RectTransform parent warning seen during resolution changes was traced to
vanilla Valheim settings UI:

```text
Valheim.SettingsGui.GraphicsSettings.OnTestResolution()
ResolutionSwitchDialog -> Settings(Clone)/Panel/TabContent
```

It is not caused by InventorySlots crafting panel, pinned tooltip, or inventory
tooltip layout code.
