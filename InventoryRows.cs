using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private sealed class NativeInventoryRows
    {
        public int Rows = BaseRows;
        public bool Loaded;
    }

    private static readonly ConditionalWeakTable<Player, NativeInventoryRows> NativeRowCache = new();

    private static int GetNativeInventoryRows(Player player)
    {
        NativeInventoryRows state = NativeRowCache.GetValue(player, static _ => new NativeInventoryRows());
        if (!state.Loaded && !player.m_isLoading)
        {
            RefreshNativeInventoryRows(player);
        }
        return state.Rows;
    }

    private static void RefreshNativeInventoryRows(Player player)
    {
        if (IsUnityNull(player))
        {
            return;
        }
        NativeInventoryRows state = NativeRowCache.GetValue(player, static _ => new NativeInventoryRows());
        state.Rows = player.TryGetUniqueKeyValue("invrows", out string value) && int.TryParse(value, out int rows)
            ? Mathf.Clamp(rows, BaseRows, GetFixedRegularRows())
            : BaseRows;
        state.Loaded = true;
        InvalidateInventoryPlacementCaches();
    }

    internal static int CaptureRowsBeforeNativeResize(Player player)
    {
        return player == Player.m_localPlayer && !player.m_isLoading ? GetUsableRegularRows(player) : -1;
    }

    internal static void SetNativeInventoryStorageHeight(Inventory inventory, int nativeRows, Player player)
    {
        // Keep nativeRows unchanged on the original method's stack: invrows is
        // vanilla state, while the physical inventory also stores equipment.
        inventory.SetHeight(player == Player.m_localPlayer
            ? GetInventoryPreservationHeight(inventory, GetFullHeightForWidth(inventory.GetWidth()))
            : nativeRows);
    }

    internal static void SetNativeInventoryPanelSize(InventoryGui gui, int nativeRows, Player player)
    {
        if (gui == null)
        {
            return;
        }
        if (player == Player.m_localPlayer)
        {
            RefreshNativeInventoryRows(player);
            // InventorySlots lays out every visible native/mod row inside the
            // grid and stretches only the Bkg/Darken children. Keeping the
            // vanilla player root at its serialized four-row size prevents a
            // pocket purchase from expanding and shifting the entire panel.
            gui.SetInventorySize(BaseRows);
        }
        else
        {
            gui.SetInventorySize(nativeRows);
        }
    }

    internal static void CompleteNativeInventoryResize(Player player, int previousRows)
    {
        if (player != Player.m_localPlayer)
        {
            return;
        }
        RefreshNativeInventoryRows(player);
        RevealRegularRowsAfterKnownItem(player, previousRows);
        RequestInventoryStateEnsure(player, InventoryStateEnsureReason.InventoryChanged, InventoryStateAuditLevel.HeightOnly);
    }

    private static Vector3 GetCustomSlotPanelElementPosition(int index, float elementSpace)
    {
        return new Vector3((index / CustomSlotPanelRows) * elementSpace, -(index % CustomSlotPanelRows) * elementSpace, 0f);
    }

    private static Vector3 GetQuickSlotPanelElementPosition(int index, float elementSpace)
    {
        return new Vector3((index % QuickSlotPanelColumns) * elementSpace, -(index / QuickSlotPanelColumns) * elementSpace, 0f);
    }

    private static int GetCustomPanelColumns(int slotCount)
    {
        return slotCount <= 0 ? 0 : Mathf.CeilToInt(slotCount / (float)CustomSlotPanelRows);
    }

    private static int GetQuickPanelRows(int slotCount)
    {
        return slotCount <= 0 ? 0 : Mathf.CeilToInt(slotCount / (float)QuickSlotPanelColumns);
    }

    internal static int CaptureRegularRowsBeforeKnownItem(Player player, ItemDrop.ItemData? item)
    {
        if (IsUnityNull(player) ||
            player != Player.m_localPlayer ||
            player.m_isLoading ||
            !UseExpandableInventoryRows() ||
            item?.m_shared == null)
        {
            return -1;
        }

        string sharedName = item.m_shared.m_name;
        if (string.IsNullOrWhiteSpace(sharedName) || player.m_knownMaterial.Contains(sharedName))
        {
            return -1;
        }

        return GetUsableRegularRows(player);
    }

    internal static void RevealRegularRowsAfterKnownItem(Player player, int previousRows)
    {
        if (previousRows < BaseRows ||
            IsUnityNull(player) ||
            player != Player.m_localPlayer ||
            player.m_isLoading ||
            !UseExpandableInventoryRows())
        {
            return;
        }

        int currentRows = GetUsableRegularRows(player);
        if (currentRows <= previousRows || GetInventoryViewportRows(currentRows) >= currentRows)
        {
            return;
        }

        SetExpandableInventoryRows(currentRows, currentRows);
    }

    private static int GetInventoryViewportRows(int totalRegularRows)
    {
        int unlockedRows = Mathf.Clamp(totalRegularRows, BaseRows, BaseRows + MaxSupportedExtraRows);
        if (!UseExpandableInventoryRows())
        {
            return Mathf.Max(1, unlockedRows);
        }

        int rememberedRows = GetLastExpandableInventoryRows(unlockedRows);
        return Mathf.Clamp(Mathf.Min(unlockedRows, rememberedRows), 1, Mathf.Max(1, unlockedRows));
    }

    private static int UpdatePlayerInventoryScroll(InventoryGrid playerGrid, int viewportRows, int totalRegularRows)
    {
        if (UseExpandableInventoryRows())
        {
            HandlePlayerInventoryExpandableWheel(playerGrid, totalRegularRows);
            viewportRows = GetInventoryViewportRows(totalRegularRows);
        }

        return viewportRows;
    }

    private static bool UseExpandableInventoryRows()
    {
        return _inventoryRowsDisplayMode == null || _inventoryRowsDisplayMode.Value == InventoryRowsDisplayMode.Expandable;
    }

    private static void HandlePlayerInventoryExpandableWheel(InventoryGrid playerGrid, int totalRegularRows)
    {
        if (totalRegularRows <= BaseRows || !InventoryGui.IsVisible())
        {
            return;
        }

        if (ShouldSuppressInventoryContainerRowsWheel())
        {
            return;
        }

        if (!IsMouseOverPlayerInventory(playerGrid) && !IsGamepadUiScrollActive())
        {
            return;
        }

        float wheel = GetUiScrollDelta(UiScrollInputMode.Discrete);
        if (Mathf.Abs(wheel) < 0.01f)
        {
            return;
        }

        int direction = wheel < 0f ? 1 : -1;
        SetExpandableInventoryRows(GetLastExpandableInventoryRows(totalRegularRows) + direction, totalRegularRows);
    }

    private static int GetLastExpandableInventoryRows(int totalRegularRows)
    {
        EnsureLastExpandableInventoryRowsLoaded();
        int maxRows = Mathf.Clamp(totalRegularRows, BaseRows, BaseRows + MaxSupportedExtraRows);
        return Mathf.Clamp(InventoryPanels.LastExpandableInventoryRows, BaseRows, maxRows);
    }

    private static void SetExpandableInventoryRows(int rows, int totalRegularRows)
    {
        EnsureLastExpandableInventoryRowsLoaded();
        int maxRows = Mathf.Clamp(totalRegularRows, BaseRows, BaseRows + MaxSupportedExtraRows);
        int clampedRows = Mathf.Clamp(rows, BaseRows, maxRows);
        if (InventoryPanels.LastExpandableInventoryRows == clampedRows)
        {
            return;
        }

        InventoryPanels.LastExpandableInventoryRows = clampedRows;
        SaveLastExpandableInventoryRows();
    }

    private static void EnsureLastExpandableInventoryRowsLoaded()
    {
        if (InventoryPanels.LastExpandableInventoryRowsLoaded)
        {
            return;
        }

        InventoryPanels.LastExpandableInventoryRowsLoaded = true;
        try
        {
            EnsureClientStateLoaded();
            InventoryPanels.LastExpandableInventoryRows = Mathf.Clamp(InventoryClient.ClientState.Inventory.LastExpandableRows, BaseRows, BaseRows + MaxSupportedExtraRows);
        }
        catch (Exception)
        {
        }
    }

    private static void SaveLastExpandableInventoryRows()
    {
        try
        {
            EnsureClientStateLoaded();
            InventoryClient.ClientState.Inventory.LastExpandableRows = InventoryPanels.LastExpandableInventoryRows;
            SaveClientState();
        }
        catch (Exception)
        {
        }
    }

    private static bool IsMouseOverPlayerInventory(InventoryGrid playerGrid)
    {
        if (playerGrid == null)
        {
            return false;
        }

        Vector2 mouse = GetUiMousePosition();
        if (playerGrid.m_gridRoot != null && RectContainsScreenPoint(playerGrid.m_gridRoot, mouse))
        {
            return true;
        }

        return InventoryGui.instance != null && InventoryGui.instance.m_player != null && RectContainsScreenPoint(InventoryGui.instance.m_player, mouse);
    }

}
