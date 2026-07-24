using System;
using System.Linq;
using BepInEx.Configuration;
using UnityEngine;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    internal static bool HandleFavoriteClick(InventoryGrid grid, UIInputHandler clickHandler, bool leftClick)
    {
        if (!ShouldHandleFavoriteClick(grid))
        {
            return true;
        }

        Player player = Player.m_localPlayer;
        Inventory inventory = ((Humanoid)player).GetInventory();
        if (inventory == null || grid.m_inventory != inventory)
        {
            return true;
        }

        Vector2i pos = grid.GetButtonPos(clickHandler.gameObject);
        if (pos.x < 0 || pos.y < 0 || IsOutOfBounds(inventory, pos))
        {
            return true;
        }

        if (!leftClick || !CanFavoriteSlot(player, inventory, pos))
        {
            return true;
        }

        ToggleFavoriteSlot(player, pos);
        return false;
    }

    private static bool ShouldHandleFavoriteClick(InventoryGrid grid)
    {
        if (grid == null || InventoryGui.instance == null || Player.m_localPlayer == null)
        {
            return false;
        }

        Player player = Player.m_localPlayer;
        if (player.m_isLoading || player.IsTeleporting())
        {
            return false;
        }

        if (InventoryGui.instance.m_dragGo != null)
        {
            return false;
        }

        if (grid != InventoryGui.instance.m_playerGrid)
        {
            return false;
        }

        return IsFavoriteModifierHeld();
    }

    private static bool IsFavoriteModifierHeld()
    {
        return _favoriteModifierKey != null && IsShortcutHeldAllowingAltPair(_favoriteModifierKey.Value) ||
               IsControllerHotkeyHeld(_controllerFavoriteModifierButton);
    }

    internal static bool ShouldSuppressVanillaHotbarItemUse(Player player, int index)
    {
        if (player != Player.m_localPlayer || _quickSlotHotkeys == null || !TryGetHotbarKeyCode(index, out KeyCode hotbarKey))
        {
            return false;
        }

        foreach (ConfigEntry<KeyboardShortcut> hotkey in _quickSlotHotkeys)
        {
            KeyboardShortcut shortcut = hotkey.Value;
            if (shortcut.MainKey != hotbarKey || !shortcut.Modifiers.Any())
            {
                continue;
            }

            if (AreShortcutModifiersHeldAllowingAltPair(shortcut) && IsShortcutMainKeyDownAllowingAltPair(shortcut))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetHotbarKeyCode(int index, out KeyCode keyCode)
    {
        keyCode = index switch
        {
            1 => KeyCode.Alpha1,
            2 => KeyCode.Alpha2,
            3 => KeyCode.Alpha3,
            4 => KeyCode.Alpha4,
            5 => KeyCode.Alpha5,
            6 => KeyCode.Alpha6,
            7 => KeyCode.Alpha7,
            8 => KeyCode.Alpha8,
            _ => KeyCode.None
        };

        return keyCode != KeyCode.None;
    }

    private static bool IsShortcutDownAllowingAltPair(KeyboardShortcut shortcut)
    {
        return shortcut.MainKey != KeyCode.None &&
               AreShortcutModifiersHeldAllowingAltPair(shortcut) &&
               IsShortcutMainKeyDownAllowingAltPair(shortcut);
    }

    private static bool IsShortcutHeldAllowingAltPair(KeyboardShortcut shortcut)
    {
        if (shortcut.MainKey == KeyCode.None || !AreShortcutModifiersHeldAllowingAltPair(shortcut))
        {
            return false;
        }

        return IsShortcutMainKeyHeldAllowingAltPair(shortcut);
    }

    private static bool AreShortcutModifiersHeldAllowingAltPair(KeyboardShortcut shortcut)
    {
        foreach (KeyCode modifier in shortcut.Modifiers)
        {
            if (!IsShortcutModifierHeldAllowingAltPair(modifier))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsShortcutModifierHeldAllowingAltPair(KeyCode key)
    {
        if (key is KeyCode.LeftAlt or KeyCode.RightAlt)
        {
            return Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
        }

        return Input.GetKey(key);
    }

    private static bool IsShortcutMainKeyHeldAllowingAltPair(KeyboardShortcut shortcut)
    {
        if (shortcut.MainKey is KeyCode.LeftAlt or KeyCode.RightAlt)
        {
            return Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
        }

        return Input.GetKey(shortcut.MainKey);
    }

    private static bool IsShortcutMainKeyDownAllowingAltPair(KeyboardShortcut shortcut)
    {
        if (shortcut.MainKey is KeyCode.LeftAlt or KeyCode.RightAlt)
        {
            return Input.GetKeyDown(KeyCode.LeftAlt) || Input.GetKeyDown(KeyCode.RightAlt);
        }

        return Input.GetKeyDown(shortcut.MainKey);
    }

    private static bool CanFavoriteSlot(Player player, Inventory inventory, Vector2i pos)
    {
        InventoryCellKind kind = GetInventoryCellKind(player, inventory, pos);
        return InventoryActionCellPolicyCore.CanFavoriteSlot(kind);
    }

    private static void ToggleFavoriteSlot(Player player, Vector2i pos)
    {
        EnsureFavoritesLoaded(player);
        bool added = FavoriteSlots.Add(pos);
        if (!added)
        {
            FavoriteSlots.Remove(pos);
        }

        SaveFavorites(player);
    }

    private static void EnsureFavoritesLoaded(Player player)
    {
        if (player == null)
        {
            return;
        }

        string playerId = GetPlayerId(player);
        if (string.Equals(InventoryClient.LoadedFavoritesPlayerId, playerId, StringComparison.Ordinal))
        {
            return;
        }

        FavoriteSlots.Clear();
        InventoryClient.LoadedFavoritesPlayerId = playerId;

        try
        {
            InventorySlotsClientPlayerState? data = GetClientPlayerState(playerId, create: false);
            if (data != null)
            {
                foreach (InventorySlotsFavoriteSlot slot in data.FavoriteSlots)
                {
                    if (slot.X >= 0 && slot.Y >= 0)
                    {
                        FavoriteSlots.Add(new Vector2i(slot.X, slot.Y));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.LogWarning($"Failed to load InventorySlots favorites from {ClientStateFilePath}: {ex.Message}");
        }
    }

    private static void ApplyAutoFavoriteHotbarSwitchRowToLocalPlayer()
    {
        Player? player = Player.m_localPlayer;
        if (player != null)
        {
            ApplyAutoFavoriteHotbarSwitchRowForPlayer(player);
        }
    }

    private static void ApplyAutoFavoriteHotbarSwitchRowForPlayer(Player player)
    {
        if (player == null || player != Player.m_localPlayer || !ShouldAutoFavoriteHotbarSwitchRow())
        {
            return;
        }

        EnsureFavoritesLoaded(player);
        if (ApplyAutoFavoriteHotbarSwitchRow(player))
        {
            SaveFavorites(player);
        }
    }

    private static bool ApplyAutoFavoriteHotbarSwitchRow(Player player)
    {
        if (!ShouldAutoFavoriteHotbarSwitchRow() || player == null)
        {
            return false;
        }

        Inventory inventory = ((Humanoid)player).GetInventory();
        if (inventory == null || inventory.GetHeight() <= HotbarSwitchFavoriteRow)
        {
            return false;
        }

        int width = Math.Max(1, inventory.GetWidth());
        bool changed = false;
        for (int x = 0; x < width; x++)
        {
            changed |= FavoriteSlots.Add(new Vector2i(x, HotbarSwitchFavoriteRow));
        }

        return changed;
    }

    private static bool ShouldAutoFavoriteHotbarSwitchRow() =>
        _autoFavoriteHotbarSwitchRow != null && _autoFavoriteHotbarSwitchRow.Value.IsOn();

    private static void SaveFavorites(Player player)
    {
        if (player == null)
        {
            return;
        }

        string playerId = GetPlayerId(player);
        InventoryClient.LoadedFavoritesPlayerId = playerId;
        try
        {
            InventorySlotsClientPlayerState data = GetClientPlayerState(playerId, create: true)!;
            data.FavoriteSlots = FavoriteSlots
                .OrderBy(slot => slot.y)
                .ThenBy(slot => slot.x)
                .Select(slot => new InventorySlotsFavoriteSlot { X = slot.x, Y = slot.y })
                .ToList();
            SaveClientState();
        }
        catch (Exception ex)
        {
            Log.LogWarning($"Failed to save InventorySlots favorites: {ex.Message}");
        }
    }
}
