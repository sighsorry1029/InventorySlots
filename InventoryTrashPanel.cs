using System;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using ItemData = ItemDrop.ItemData;

namespace InventorySlots;

internal sealed class InventoryTrashButtonMarker : MonoBehaviour
{
    public Image? Icon { get; set; }
    public bool TextSuppressed { get; set; }
    public string LayoutSignature { get; set; } = "";
    public bool LastCanTrash { get; set; }
    public bool HasVisualState { get; set; }
}

[HarmonyPatch(typeof(InventoryGui), "Update")]
internal static class InventoryGuiTrashConfirmInputPatch
{
    private static void Postfix()
    {
        InventorySlotsPlugin.UpdateInventoryTrashConfirmDialogInput();
    }
}

public sealed partial class InventorySlotsPlugin
{
    private const float InventoryTrashPanelGap = 8f;
    private const string InventoryTrashButtonName = "InventorySlots_TrashButton";
    private const string InventoryTrashIconName = "InventorySlots_TrashIcon";
    private const string InventoryTrashConfirmDialogName = "InventorySlots_TrashConfirmDialog";

    private static RectTransform? _inventoryTrashPanel;
    private static Sprite? _inventoryTrashIconSprite;
    private static GameObject? _inventoryTrashConfirmDialog;
    private static Inventory? _inventoryTrashPendingInventory;
    private static ItemData? _inventoryTrashPendingItem;
    private static int _inventoryTrashPendingAmount;

    private static void UpdateInventoryTrashPanel(InventoryGui gui, InventoryGrid playerGrid, Player player, Vector3 gridOrigin, int viewportRows)
    {
        if (!IsInventoryTrashButtonEnabled() ||
            gui == null ||
            playerGrid == null ||
            playerGrid.m_gridRoot == null ||
            gui.m_takeAllButton == null ||
            !InventoryGui.IsVisible())
        {
            SetActionPanelActive(_inventoryTrashPanel, false);
            CloseInventoryTrashConfirmDialog();
            return;
        }

        _inventoryTrashPanel = EnsureActionPanel(EnsureInventoryButtonSlide(gui, gridOrigin, viewportRows), InventoryTrashPanelName, _inventoryTrashPanel);
        if (_inventoryTrashPanel == null)
        {
            return;
        }

        float elementSpace = Mathf.Max(1f, playerGrid.m_elementSpace);
        float buttonSize = Mathf.Clamp(elementSpace * 0.72f, 42f, 58f);
        Vector2 panelSize = new(buttonSize, buttonSize);
        if ((_inventoryTrashPanel.sizeDelta - panelSize).sqrMagnitude > 0.0001f)
        {
            _inventoryTrashPanel.sizeDelta = panelSize;
        }

        if (_inventoryTrashPanel.localScale != Vector3.one)
        {
            _inventoryTrashPanel.localScale = Vector3.one;
        }

        if (_inventoryTrashPanel.localRotation != Quaternion.identity)
        {
            _inventoryTrashPanel.localRotation = Quaternion.identity;
        }

        Vector3 panelPosition = gridOrigin + new Vector3(
            (InventoryWidth - 1) * elementSpace + (elementSpace - buttonSize) * 0.5f,
            -Mathf.Max(1, viewportRows) * elementSpace - InventoryTrashPanelGap,
            0f);
        if ((_inventoryTrashPanel.localPosition - panelPosition).sqrMagnitude > 0.0001f)
        {
            _inventoryTrashPanel.localPosition = panelPosition;
        }

        DisableActionPanelChildren(_inventoryTrashPanel);

        Button? trashButton = EnsureActionButton(
            _inventoryTrashPanel,
            gui.m_takeAllButton,
            InventoryTrashButtonName,
            "",
            TryClickInventoryTrashPanel);
        RectTransform? trashRect = LayoutActionButton(trashButton, buttonSize, buttonSize);
        if (trashButton != null && trashRect != null)
        {
            RegisterControllerInventoryButton(gui, InventorySlideButton.Trash, trashButton);
            ConfigureInventoryTrashButton(trashButton, buttonSize);
            bool canTrash = CanStartInventoryTrash(gui, player, showMessage: false);
            SetButtonInteractable(trashButton, HasHeldTrashCandidate(gui));
            SetInventoryTrashButtonVisual(trashButton, canTrash);
        }

        SetActionPanelActive(_inventoryTrashPanel, true);
        if (_inventoryTrashPanel.parent != null && _inventoryTrashPanel.GetSiblingIndex() != _inventoryTrashPanel.parent.childCount - 1)
        {
            _inventoryTrashPanel.SetAsLastSibling();
        }
    }

    private static void OnInventoryTrashPanelConfigChanged()
    {
        if (IsInventoryTrashButtonEnabled())
        {
            return;
        }

        SetActionPanelActive(_inventoryTrashPanel, false);
        CloseInventoryTrashConfirmDialog();
    }

    private static bool HasHeldTrashCandidate(InventoryGui gui) =>
        gui != null && gui.m_dragGo != null && gui.m_dragItem != null && gui.m_dragInventory != null;

    private static void ConfigureInventoryTrashButton(Button button, float buttonSize)
    {
        InventoryTrashButtonMarker marker = button.GetComponent<InventoryTrashButtonMarker>() ?? button.gameObject.AddComponent<InventoryTrashButtonMarker>();
        if (!marker.TextSuppressed)
        {
            foreach (TMP_Text text in button.GetComponentsInChildren<TMP_Text>(true))
            {
                text.text = "";
                text.enabled = false;
            }

            foreach (UnityEngine.UI.Text text in button.GetComponentsInChildren<UnityEngine.UI.Text>(true))
            {
                text.text = "";
                text.enabled = false;
            }

            marker.TextSuppressed = true;
        }

        if (marker.Icon == null || IsUnityNull(marker.Icon))
        {
            Transform existing = button.transform.Find(InventoryTrashIconName);
            marker.Icon = existing != null ? existing.GetComponent<Image>() : null;
            if (marker.Icon == null || IsUnityNull(marker.Icon))
            {
                GameObject iconGo = new(InventoryTrashIconName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                RectTransform iconRect = (RectTransform)iconGo.transform;
                iconRect.SetParent(button.transform, false);
                marker.Icon = iconGo.GetComponent<Image>();
            }
        }

        // The template carries a UITooltip, but this icon communicates its state
        // through the held-item highlight and the delete confirmation dialog.
        UITooltip? tooltip = button.GetComponent<UITooltip>();
        if (tooltip != null)
        {
            tooltip.m_topic = "";
            tooltip.m_text = "";
            tooltip.enabled = false;
            Object.Destroy(tooltip);
        }

        Sprite sprite = GetInventoryTrashIconSprite();
        float iconSize = Mathf.Max(18f, buttonSize * 0.58f);
        string signature = $"{buttonSize:0.###}|{iconSize:0.###}|{GetUnityObjectId(sprite)}";
        if (string.Equals(marker.LayoutSignature, signature, StringComparison.Ordinal))
        {
            return;
        }

        RectTransform rect = (RectTransform)marker.Icon!.transform;
        SetRectLayout(rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(iconSize, iconSize));
        if (marker.Icon.sprite != sprite)
        {
            marker.Icon.sprite = sprite;
        }

        if (!marker.Icon.preserveAspect)
        {
            marker.Icon.preserveAspect = true;
        }

        if (marker.Icon.raycastTarget)
        {
            marker.Icon.raycastTarget = false;
        }

        marker.LayoutSignature = signature;
    }

    private static void SetInventoryTrashButtonVisual(Button button, bool canTrash)
    {
        InventoryTrashButtonMarker? marker = button.GetComponent<InventoryTrashButtonMarker>();
        if (marker?.Icon != null && !IsUnityNull(marker.Icon))
        {
            if (marker.HasVisualState && marker.LastCanTrash == canTrash)
            {
                return;
            }

            marker.Icon.color = canTrash ? new Color(1f, 0.82f, 0.55f, 1f) : new Color(0.75f, 0.75f, 0.75f, 0.65f);
            marker.LastCanTrash = canTrash;
            marker.HasVisualState = true;
        }
    }

    private static Sprite GetInventoryTrashIconSprite()
    {
        if (_inventoryTrashIconSprite != null && !IsUnityNull(_inventoryTrashIconSprite))
        {
            return _inventoryTrashIconSprite;
        }

        const int size = 64;
        Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
        {
            name = "InventorySlots_TrashIconTexture",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        Color clear = new(0f, 0f, 0f, 0f);
        Color line = Color.white;
        Color[] pixels = new Color[size * size];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = clear;
        }

        DrawTrashLine(pixels, size, 19, 18, 45, 18, 3, line);
        DrawTrashLine(pixels, size, 25, 13, 39, 13, 3, line);
        DrawTrashLine(pixels, size, 23, 22, 27, 50, 3, line);
        DrawTrashLine(pixels, size, 41, 22, 37, 50, 3, line);
        DrawTrashLine(pixels, size, 27, 50, 37, 50, 3, line);
        DrawTrashLine(pixels, size, 30, 26, 30, 45, 2, line);
        DrawTrashLine(pixels, size, 34, 26, 34, 45, 2, line);

        texture.SetPixels(pixels);
        texture.Apply(updateMipmaps: false, makeNoLongerReadable: true);
        _inventoryTrashIconSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        _inventoryTrashIconSprite.name = "InventorySlots_TrashIcon";
        return _inventoryTrashIconSprite;
    }

    private static void DrawTrashLine(Color[] pixels, int textureSize, int x0, int y0, int x1, int y1, int thickness, Color color)
    {
        int dx = Math.Abs(x1 - x0);
        int sx = x0 < x1 ? 1 : -1;
        int dy = -Math.Abs(y1 - y0);
        int sy = y0 < y1 ? 1 : -1;
        int err = dx + dy;
        while (true)
        {
            DrawTrashPoint(pixels, textureSize, x0, y0, thickness, color);
            if (x0 == x1 && y0 == y1)
            {
                break;
            }

            int e2 = 2 * err;
            if (e2 >= dy)
            {
                err += dy;
                x0 += sx;
            }

            if (e2 <= dx)
            {
                err += dx;
                y0 += sy;
            }
        }
    }

    private static void DrawTrashPoint(Color[] pixels, int textureSize, int x, int y, int thickness, Color color)
    {
        int radius = Math.Max(1, thickness);
        for (int yy = y - radius; yy <= y + radius; yy++)
        {
            for (int xx = x - radius; xx <= x + radius; xx++)
            {
                if (xx < 0 || xx >= textureSize || yy < 0 || yy >= textureSize)
                {
                    continue;
                }

                int pixelY = textureSize - 1 - yy;
                pixels[pixelY * textureSize + xx] = color;
            }
        }
    }

    private static void TryClickInventoryTrashPanel()
    {
        InventoryGui gui = InventoryGui.instance;
        Player player = Player.m_localPlayer;
        if (!CanStartInventoryTrash(gui, player, showMessage: true))
        {
            return;
        }

        int amount = Mathf.Clamp(gui.m_dragAmount, 1, gui.m_dragItem.m_stack);
        ShowInventoryTrashConfirmDialog(gui, gui.m_dragInventory, gui.m_dragItem, amount);
    }

    private static bool CanStartInventoryTrash(InventoryGui? gui, Player? player, bool showMessage)
    {
        if (!IsInventoryTrashButtonEnabled())
        {
            return false;
        }

        if (gui == null || player == null || !InventoryGui.IsVisible() || IsInventoryPanelClosing(gui))
        {
            return false;
        }

        if (player.m_isLoading || player.IsTeleporting())
        {
            return false;
        }

        if (gui.m_dragGo == null || gui.m_dragItem == null || gui.m_dragInventory == null)
        {
            if (showMessage)
            {
                ShowInventoryTrashMessage(player, "$inventoryslots_trash_no_item", "Pick up an item first.");
            }

            return false;
        }

        Inventory playerInventory = ((Humanoid)player).GetInventory();
        if (playerInventory == null || gui.m_dragInventory != playerInventory)
        {
            if (showMessage)
            {
                ShowInventoryTrashMessage(player, "$inventoryslots_trash_player_inventory_only", "Only player inventory items can be trashed.");
            }

            return false;
        }

        return CanTrashInventoryItem(player, playerInventory, gui.m_dragItem, showMessage);
    }

    private static bool CanTrashInventoryItem(Player player, Inventory inventory, ItemData item, bool showMessage)
    {
        if (inventory == null || item == null || !inventory.ContainsItem(item))
        {
            if (showMessage)
            {
                ShowInventoryTrashMessage(player, "$inventoryslots_trash_item_missing", "That item is no longer available.");
            }

            return false;
        }

        if (!CanTrashInventoryCell(player, inventory, item.m_gridPos))
        {
            if (showMessage)
            {
                ShowInventoryTrashMessage(player, "$inventoryslots_trash_hotbar_item", "Only regular inventory items can be trashed.");
            }

            return false;
        }

        if (IsFavoriteProtected(player, inventory, item))
        {
            if (showMessage)
            {
                ShowInventoryTrashMessage(player, "$inventoryslots_trash_favorite_item", "Favorite items cannot be trashed.");
            }

            return false;
        }

        if (item.m_shared.m_questItem)
        {
            if (showMessage)
            {
                ShowInventoryTrashMessage(player, "$inventoryslots_trash_quest_item", "Quest items cannot be trashed.");
            }

            return false;
        }

        return true;
    }

    private static bool CanTrashInventoryCell(Player player, Inventory inventory, Vector2i pos)
    {
        InventoryCellKind kind = GetInventoryCellKind(player, inventory, pos);
        return InventoryActionCellPolicyCore.CanTrashSlot(kind);
    }

    private static readonly AccessTools.FieldRef<SplitDialog, Button> TrashSplitOkButton =
        AccessTools.FieldRefAccess<SplitDialog, Button>("m_splitOkButton");
    private static readonly AccessTools.FieldRef<SplitDialog, Button> TrashSplitCancelButton =
        AccessTools.FieldRefAccess<SplitDialog, Button>("m_splitCancelButton");

    private static void ShowInventoryTrashConfirmDialog(InventoryGui gui, Inventory inventory, ItemData item, int amount)
    {
        CloseInventoryTrashConfirmDialog();
        if (gui == null || gui.m_splitDialog == null || inventory == null || item == null || amount <= 0)
        {
            return;
        }

        _inventoryTrashPendingInventory = inventory;
        _inventoryTrashPendingItem = item;
        _inventoryTrashPendingAmount = amount;

        // The clone owns its SplitDialog listeners. Use its public events instead
        // of duplicating the old split panel's hierarchy and button lifecycle.
        SplitDialog dialog = Object.Instantiate(gui.m_splitDialog, gui.transform);
        _inventoryTrashConfirmDialog = dialog.gameObject;
        dialog.name = InventoryTrashConfirmDialogName;
        dialog.SplitAccepted += ConfirmInventoryTrashDelete;
        dialog.SplitCanceled += CloseInventoryTrashConfirmDialog;
        dialog.UpdateLimits(Mathf.Max(1, item.m_shared.m_maxStackSize), false);
        dialog.SliderValue = amount;
        dialog.m_splitSlider.gameObject.SetActive(false);
        string itemName = LocalizeUi(item.m_shared.m_name, item.m_shared.m_name);
        string format = LocalizeUi("$inventoryslots_trash_confirm_format", "Delete {item}?");
        dialog.UpdateIcon(item.GetIcon(), format.Replace("{item}", itemName));
        TMP_Text? title = dialog.transform.Find("win_bkg/Text")?.GetComponent<TMP_Text>();
        if (title != null)
        {
            ApplyDefaultFontAsset(title);
            title.text = LocalizeUi("$inventoryslots_trash_confirm_title", "Delete item");
        }

        SetInventoryTrashConfirmButtonText(TrashSplitOkButton(dialog), LocalizeUi("$inventoryslots_trash_delete", "Delete"), new Color(1f, 0.25f, 0.12f, 1f));
        SetInventoryTrashConfirmButtonText(TrashSplitCancelButton(dialog), LocalizeUi("$menu_cancel", "Cancel"), Color.white);
        dialog.SetActive(true);
    }
    private static void SetInventoryTrashConfirmButtonText(Button button, string label, Color color)
    {
        foreach (TMP_Text text in GetInventoryButtonCaptionTexts<TMP_Text>(button))
        {
            Localization.instance?.RemoveTextFromCache(text);
            ApplyDefaultFontAsset(text);
            text.text = label;
            text.color = color;
        }

        foreach (UnityEngine.UI.Text text in GetInventoryButtonCaptionTexts<UnityEngine.UI.Text>(button))
        {
            Localization.instance?.RemoveTextFromCache(text);
            text.text = label;
            text.color = color;
        }
    }

    internal static void UpdateInventoryTrashConfirmDialogInput()
    {
        if (_inventoryTrashConfirmDialog == null || IsUnityNull(_inventoryTrashConfirmDialog) || !_inventoryTrashConfirmDialog.activeSelf)
        {
            return;
        }

        if (ZInput.GetButtonDown("JoyButtonB") || ZInput.GetKeyDown(KeyCode.Escape, true))
        {
            CloseInventoryTrashConfirmDialog();
            return;
        }

        if (ZInput.GetKeyDown(KeyCode.Return, true) || ZInput.GetKeyDown(KeyCode.KeypadEnter, true))
        {
            ConfirmInventoryTrashDelete();
        }
    }

    private static void ConfirmInventoryTrashDelete()
    {
        InventoryGui gui = InventoryGui.instance;
        Player player = Player.m_localPlayer;
        Inventory? inventory = _inventoryTrashPendingInventory;
        ItemData? item = _inventoryTrashPendingItem;
        int amount = _inventoryTrashPendingAmount;
        CloseInventoryTrashConfirmDialog();

        if (gui == null || player == null || inventory == null || item == null)
        {
            return;
        }

        Inventory playerInventory = ((Humanoid)player).GetInventory();
        if (inventory != playerInventory || gui.m_dragInventory != inventory || gui.m_dragItem != item || !CanTrashInventoryItem(player, inventory, item, showMessage: true))
        {
            gui.SetupDragItem(null, null, 0);
            return;
        }

        amount = Mathf.Clamp(amount, 1, item.m_stack);
        bool fullStack = amount >= item.m_stack;
        if (fullStack)
        {
            TryPrepareSlotItemForExternalRemoval(player, inventory, item, out _);
            if (((Humanoid)player).IsItemEquiped(item))
            {
                player.RemoveEquipAction(item);
                ((Humanoid)player).UnequipItem(item, false);
            }

            ClearSlotActionState(item);
            inventory.RemoveItem(item);
        }
        else
        {
            inventory.RemoveItem(item, amount);
        }

        gui.SetupDragItem(null, null, 0);
        gui.UpdateCraftingPanel(false);
        inventory.Changed();
        ClearCraftingRequirementAvailabilityCache();
        gui.m_moveItemEffects.Create(gui.transform.position, Quaternion.identity);
    }

    private static void ShowInventoryTrashMessage(Player player, string token, string fallback)
    {
        ((Character)player).Message(MessageHud.MessageType.Center, LocalizeUi(token, fallback), 0, null);
    }

    private static void CloseInventoryTrashConfirmDialog()
    {
        _inventoryTrashPendingInventory = null;
        _inventoryTrashPendingItem = null;
        _inventoryTrashPendingAmount = 0;
        if (_inventoryTrashConfirmDialog != null && !IsUnityNull(_inventoryTrashConfirmDialog))
        {
            Object.Destroy(_inventoryTrashConfirmDialog);
        }

        _inventoryTrashConfirmDialog = null;
    }
}
