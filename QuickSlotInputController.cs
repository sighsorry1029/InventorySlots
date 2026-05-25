using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using UnityEngine;
using ItemData = ItemDrop.ItemData;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private static bool QuickSlotAcceptsItem(ItemData? item)
    {
        if (item?.m_shared == null)
        {
            return false;
        }

        foreach (string token in _yamlConfig.QuickSlots ?? new List<string>())
        {
            if (ItemMatchesYamlReferenceToken(item, token))
            {
                return true;
            }
        }

        return false;
    }

    private static void HandleHotbarSwitch(Player player)
    {
        if (ShouldBlockGlobalHotkeys(player) ||
            (_hotbarSwitchKey == null || !_hotbarSwitchKey.Value.IsKeyDown()) &&
            !IsControllerHotkeyDown(_controllerHotbarSwitchButton))
        {
            return;
        }

        Inventory inventory = ((Humanoid)player).GetInventory();
        if (inventory == null)
        {
            return;
        }

        int rowsToSwitch = Mathf.Clamp(HotbarRowsToSwitch, 1, GetUsableRegularRows(player));
        foreach (ItemData item in inventory.m_inventory)
        {
            if (item == null || item.m_gridPos.y < 0 || item.m_gridPos.y >= rowsToSwitch)
            {
                continue;
            }

            if (TryGetSlotAtGridPos(inventory, item.m_gridPos, out _))
            {
                continue;
            }

            item.m_gridPos.y--;
            if (item.m_gridPos.y < 0)
            {
                item.m_gridPos.y = rowsToSwitch - 1;
            }
        }

        inventory.Changed();
    }

    private static void HandleQuickSlotHotkeys(Player player)
    {
        if (_quickSlotHotkeys == null || ShouldBlockGlobalHotkeys(player))
        {
            return;
        }

        Inventory inventory = ((Humanoid)player).GetInventory();
        if (inventory == null)
        {
            return;
        }

        int count = Mathf.Min(GetUnlockedQuickSlotCount(player), _quickSlotHotkeys.Length);
        ConfigEntry<ControllerHotkeyAction>[]? controllerQuickSlotButtons = _controllerQuickSlotButtons;
        int controllerCount = Mathf.Min(count, controllerQuickSlotButtons?.Length ?? 0);
        for (int i = 0; i < controllerCount; i++)
        {
            if (controllerQuickSlotButtons == null ||
                !IsControllerHotkeyDown(controllerQuickSlotButtons[i]) ||
                !TryGetQuickSlotDefinition(i, out SlotDefinition? slot) ||
                !IsQuickSlotUnlocked(player, slot!))
            {
                continue;
            }

            ActivateQuickSlotHotkey(player, inventory, slot!);
            return;
        }

        for (int i = 0; i < count; i++)
        {
            KeyboardShortcut shortcut = _quickSlotHotkeys[i].Value;
            if (!shortcut.Modifiers.Any() ||
                !IsShortcutDownAllowingAltPair(shortcut) ||
                !TryGetQuickSlotDefinition(i, out SlotDefinition? slot) ||
                !IsQuickSlotUnlocked(player, slot!))
            {
                continue;
            }

            ActivateQuickSlotHotkey(player, inventory, slot!);
            return;
        }

        for (int i = 0; i < count; i++)
        {
            KeyboardShortcut shortcut = _quickSlotHotkeys[i].Value;
            if (shortcut.Modifiers.Any() ||
                !IsShortcutDownAllowingAltPair(shortcut) ||
                !TryGetQuickSlotDefinition(i, out SlotDefinition? slot) ||
                !IsQuickSlotUnlocked(player, slot!))
            {
                continue;
            }

            ActivateQuickSlotHotkey(player, inventory, slot!);
            return;
        }
    }

    private static void ActivateQuickSlotHotkey(Player player, Inventory inventory, SlotDefinition slot)
    {
        Vector2i pos = GetSlotGridPos(inventory, slot);
        ItemData? item = inventory.GetItemAt(pos.x, pos.y);
        if (item != null && slot.Accepts(item))
        {
            player.UseItem(inventory, item, fromInventoryGui: false);
            inventory.Changed();
        }
    }
}
