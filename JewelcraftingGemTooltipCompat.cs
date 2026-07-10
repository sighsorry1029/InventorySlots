using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ItemData = ItemDrop.ItemData;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private readonly struct JewelcraftingGemIconData
    {
        public readonly Sprite Sprite;
        public readonly string DisplayName;

        public JewelcraftingGemIconData(Sprite sprite, string displayName)
        {
            Sprite = sprite;
            DisplayName = displayName;
        }
    }

    private readonly struct JewelcraftingSocketGemData
    {
        public readonly string PrefabName;
        public readonly Dictionary<string, uint>? Seeds;

        public JewelcraftingSocketGemData(string prefabName, Dictionary<string, uint>? seeds)
        {
            PrefabName = prefabName ?? "";
            Seeds = seeds;
        }

        public bool HasGem => !string.IsNullOrWhiteSpace(PrefabName);
    }

    private static bool UpdateCraftingGemIconRow(RectTransform panel, InventoryGui.RecipeDataPair pair, ref RectTransform? cachedRow, Vector2 bottomLeft, float iconSize, float gap)
    {
        ItemData? item = GetCraftingJewelcraftingTooltipItem(pair);
        List<JewelcraftingGemIconData> gems = item?.m_shared != null ? GetJewelcraftingGemIconData(item) : new List<JewelcraftingGemIconData>();
        return UpdateCraftingGemIconRow(panel, gems, ref cachedRow, bottomLeft, iconSize, gap);
    }

    private static bool UpdateCraftingGemIconRow(RectTransform panel, List<JewelcraftingGemIconData> gems, ref RectTransform? cachedRow, Vector2 bottomLeft, float iconSize, float gap, bool enableIconTooltips = true)
    {
        if (gems.Count == 0)
        {
            HideCraftingGemIconRow(ref cachedRow);
            return false;
        }

        RectTransform row = EnsureCraftingGemIconRow(panel, ref cachedRow);
        row.anchorMin = new Vector2(0f, 0f);
        row.anchorMax = new Vector2(0f, 0f);
        row.pivot = new Vector2(0f, 0f);
        row.anchoredPosition = bottomLeft;
        row.sizeDelta = new Vector2(gems.Count * iconSize + Mathf.Max(0, gems.Count - 1) * gap, iconSize);
        row.localScale = Vector3.one;
        row.localRotation = Quaternion.identity;
        row.gameObject.SetActive(true);

        for (int i = 0; i < gems.Count; i++)
        {
            RectTransform iconRect = EnsureCraftingGemIcon(row, i);
            iconRect.anchorMin = new Vector2(0f, 0.5f);
            iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0f, 0.5f);
            iconRect.anchoredPosition = new Vector2(i * (iconSize + gap), iconSize * 0.5f);
            iconRect.sizeDelta = new Vector2(iconSize, iconSize);
            iconRect.localScale = Vector3.one;
            iconRect.localRotation = Quaternion.identity;
            iconRect.gameObject.SetActive(true);

            Image image = iconRect.GetComponent<Image>();
            image.sprite = gems[i].Sprite;
            image.color = Color.white;
            image.preserveAspect = true;
            string displayName = enableIconTooltips ? gems[i].DisplayName : "";
            if (enableIconTooltips && IsPinnedTooltipTransform(panel))
            {
                ConfigurePinnedGemIconTooltip(iconRect, displayName);
            }
            else
            {
                image.raycastTarget = enableIconTooltips;
                ConfigureCraftingGemIconTooltip(iconRect, displayName);
            }
        }

        for (int i = gems.Count; i < row.childCount; i++)
        {
            row.GetChild(i).gameObject.SetActive(false);
        }

        return true;
    }

    private static void HideCraftingGemIconRow(ref RectTransform? row)
    {
        if (row != null && !IsUnityNull(row))
        {
            row.gameObject.SetActive(false);
        }
    }

    private static RectTransform EnsureCraftingGemIconRow(RectTransform panel, ref RectTransform? cachedRow)
    {
        if (cachedRow != null && !IsUnityNull(cachedRow) && cachedRow.parent == panel)
        {
            return cachedRow;
        }

        if (cachedRow != null && !IsUnityNull(cachedRow))
        {
            cachedRow.gameObject.SetActive(false);
        }

        Transform? existing = panel.Find(CraftingGemIconRowName);
        cachedRow = existing != null ? existing.GetComponent<RectTransform>() : null;
        if (cachedRow == null)
        {
            cachedRow = new GameObject(CraftingGemIconRowName, typeof(RectTransform)).GetComponent<RectTransform>();
            cachedRow.SetParent(panel, false);
        }

        return cachedRow;
    }

    private static RectTransform EnsureCraftingGemIcon(RectTransform row, int index)
    {
        string name = "GemIcon" + index;
        Transform? existing = row.Find(name);
        RectTransform? icon = existing != null ? existing.GetComponent<RectTransform>() : null;
        if (icon == null)
        {
            icon = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(UITooltip)).GetComponent<RectTransform>();
            icon.SetParent(row, false);
        }
        else if (icon.GetComponent<UITooltip>() == null)
        {
            icon.gameObject.AddComponent<UITooltip>();
        }

        return icon;
    }

    private static void ConfigureCraftingGemIconTooltip(RectTransform iconRect, string displayName)
    {
        ClearPinnedGemIconTooltip(iconRect);
        UITooltip tooltip = iconRect.GetComponent<UITooltip>() ?? iconRect.gameObject.AddComponent<UITooltip>();
        EnsureTooltipPrefab(tooltip);
        if (string.IsNullOrWhiteSpace(displayName))
        {
            tooltip.Set("", "", null, default);
            return;
        }

        tooltip.Set(displayName, "", InventoryGui.instance?.m_playerGrid != null ? InventoryGui.instance.m_playerGrid.m_tooltipAnchor : null, default);
    }

    private static void ConfigurePinnedGemIconTooltip(RectTransform iconRect, string displayName)
    {
        Image? image = iconRect.GetComponent<Image>();
        if (image != null && !IsUnityNull(image))
        {
            image.raycastTarget = false;
        }

        UITooltip? tooltip = iconRect.GetComponent<UITooltip>();
        if (tooltip != null && !IsUnityNull(tooltip))
        {
            tooltip.m_topic = "";
            tooltip.m_text = "";
            tooltip.enabled = false;
        }

        PinnedGemIconTooltipTarget target = iconRect.GetComponent<PinnedGemIconTooltipTarget>() ?? iconRect.gameObject.AddComponent<PinnedGemIconTooltipTarget>();
        target.SetDisplayName(displayName);
        EnsurePinnedGemIconPanelHitTester(iconRect);
    }

    private static void ClearPinnedGemIconTooltip(RectTransform iconRect)
    {
        PinnedGemIconTooltipTarget? target = iconRect.GetComponent<PinnedGemIconTooltipTarget>();
        if (target != null && !IsUnityNull(target))
        {
            target.SetDisplayName("");
        }
    }

    private static void EnsurePinnedGemIconPanelHitTester(RectTransform iconRect)
    {
        RectTransform? panel = FindPinnedTooltipRoot(iconRect);
        if (panel == null || IsUnityNull(panel))
        {
            return;
        }

        PinnedGemIconTooltipPanelHitTester hitTester =
            panel.GetComponent<PinnedGemIconTooltipPanelHitTester>() ??
            panel.gameObject.AddComponent<PinnedGemIconTooltipPanelHitTester>();
        hitTester.InvalidateTargets();
    }

    private static RectTransform? FindPinnedTooltipRoot(Transform? transform)
    {
        for (Transform? current = transform; current != null; current = current.parent)
        {
            string name = current.name;
            if (name.StartsWith(InventoryPinnedTooltipNamePrefix, StringComparison.Ordinal) ||
                name.StartsWith(CraftingPinnedTooltipNamePrefix, StringComparison.Ordinal))
            {
                return current as RectTransform;
            }
        }

        return null;
    }

    private static bool IsPinnedTooltipTransform(Transform? transform)
    {
        for (Transform? current = transform; current != null; current = current.parent)
        {
            string name = current.name;
            if (name.StartsWith(InventoryPinnedTooltipNamePrefix, StringComparison.Ordinal) ||
                name.StartsWith(CraftingPinnedTooltipNamePrefix, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static List<JewelcraftingGemIconData> GetJewelcraftingGemIconData(ItemData item)
    {
        List<JewelcraftingGemIconData> gems = new();
        if (item?.m_shared == null || !TryGetJewelcraftingGemApi(out JewelcraftingGemApi? api) || api == null)
        {
            return gems;
        }

        try
        {
            object? result = api.GetGems(item);
            if (result is not System.Collections.IEnumerable gemObjects)
            {
                return gems;
            }

            foreach (object? gem in gemObjects)
            {
                if (gem == null)
                {
                    continue;
                }

                api.TryGetGemIconData(gem, out Sprite? sprite, out string prefabName);
                sprite ??= GetJewelcraftingGemSprite(prefabName);
                if (sprite != null && !IsUnityNull(sprite))
                {
                    gems.Add(new JewelcraftingGemIconData(sprite, GetJewelcraftingGemDisplayName(prefabName)));
                }
            }
        }
        catch (Exception)
        {
        }

        if (gems.Count == 0)
        {
            List<string> rawGemPrefabs = GetJewelcraftingSocketPrefabNamesFromCustomData(item);
            if (rawGemPrefabs.Count == 0 && TryGetJewelcraftingGemApi(out JewelcraftingGemApi? directApi) && directApi != null)
            {
                foreach (JewelcraftingSocketGemData socket in directApi.GetOpenSocketGemData(item))
                {
                    if (socket.HasGem)
                    {
                        rawGemPrefabs.Add(socket.PrefabName);
                    }
                }

                foreach (JewelcraftingSocketGemData socket in directApi.GetSocketGemData(item))
                {
                    if (socket.HasGem)
                    {
                        rawGemPrefabs.Add(socket.PrefabName);
                    }
                }
            }

            foreach (string prefabName in rawGemPrefabs)
            {
                Sprite? sprite = GetJewelcraftingGemSprite(prefabName);
                if (sprite != null && !IsUnityNull(sprite))
                {
                    gems.Add(new JewelcraftingGemIconData(sprite, GetJewelcraftingGemDisplayName(prefabName)));
                }
            }

        }

        return gems;
    }

    private static bool TryGetJewelcraftingOpenSocketContainer(ItemData item, out ItemData? openItem, out Inventory? openInventory)
    {
        openItem = null;
        openInventory = null;
        if (item?.m_shared == null || !TryGetJewelcraftingGemApi(out JewelcraftingGemApi? api) || api == null)
        {
            return false;
        }

        openItem = api.GetOpenSocketContainerItem();
        openInventory = api.GetOpenSocketContainerInventory();
        if (openItem?.m_shared == null || openInventory == null)
        {
            openItem = null;
            openInventory = null;
            return false;
        }

        if (!CanUseJewelcraftingOpenSocketContainerItem(item, openItem))
        {
            openItem = null;
            openInventory = null;
            return false;
        }

        return true;
    }

    private static bool CanUseJewelcraftingOpenSocketContainerItem(ItemData item, ItemData openItem)
    {
        if (ReferenceEquals(item, openItem))
        {
            return true;
        }

        if (item.m_shared == null ||
            openItem.m_shared == null ||
            !string.Equals(GetItemPrefabName(item), GetItemPrefabName(openItem), StringComparison.Ordinal) ||
            !string.Equals(item.m_shared.m_name, openItem.m_shared.m_name, StringComparison.Ordinal) ||
            item.m_variant != openItem.m_variant ||
            item.m_quality != openItem.m_quality)
        {
            return false;
        }

        return item.m_gridPos.x == openItem.m_gridPos.x &&
               item.m_gridPos.y == openItem.m_gridPos.y;
    }

    private static string GetJewelcraftingOpenSocketInventorySignature(ItemData item)
    {
        if (!TryGetJewelcraftingOpenSocketContainer(item, out _, out Inventory? inventory) || inventory == null)
        {
            return "";
        }

        List<string> parts = new() { inventory.m_width + "x" + inventory.m_height };
        foreach (ItemData gem in inventory.m_inventory)
        {
            if (gem?.m_shared == null)
            {
                continue;
            }

            parts.Add(string.Join(
                ":",
                gem.m_gridPos.x,
                gem.m_gridPos.y,
                GetItemPrefabName(gem),
                gem.m_stack,
                GetEquipmentSlotTooltipSignature(gem)));
        }

        parts.Sort(StringComparer.Ordinal);
        return string.Join(";", parts);
    }

    private static Sprite? GetJewelcraftingGemSprite(string prefabName)
    {
        if (string.IsNullOrWhiteSpace(prefabName) || ObjectDB.instance == null)
        {
            return null;
        }

        GameObject? prefab = ObjectDB.instance.GetItemPrefab(prefabName);
        ItemDrop? itemDrop = prefab != null && !IsUnityNull(prefab) ? prefab.GetComponent<ItemDrop>() : null;
        return itemDrop?.m_itemData?.GetIcon();
    }

    private static List<string> GetJewelcraftingSocketPrefabNamesFromCustomData(ItemData item)
    {
        List<string> names = new();
        if (item.m_customData == null || item.m_customData.Count == 0)
        {
            return names;
        }

        foreach (KeyValuePair<string, string> pair in item.m_customData)
        {
            if (!IsJewelcraftingSocketDataKey(pair.Key) || string.IsNullOrWhiteSpace(pair.Value))
            {
                continue;
            }

            string socketData = pair.Value.Split('|')[0];
            foreach (string socket in socketData.Split(','))
            {
                string prefabName = socket.Split(':')[0].Trim();
                if (!string.IsNullOrWhiteSpace(prefabName))
                {
                    names.Add(prefabName);
                }
            }
        }

        return names;
    }

    private static bool HasJewelcraftingPotentialCustomData(ItemData item)
    {
        if (item.m_customData == null || item.m_customData.Count == 0)
        {
            return false;
        }

        foreach (string key in item.m_customData.Keys)
        {
            if (key.IndexOf("Jewelcrafting.SocketSeed", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                continue;
            }

            if (IsJewelcraftingTooltipDataKey(key))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsJewelcraftingSocketDataKey(string key) =>
        key.IndexOf("Jewelcrafting.Sockets", StringComparison.OrdinalIgnoreCase) >= 0 ||
        key.IndexOf("Jewelcrafting.Box", StringComparison.OrdinalIgnoreCase) >= 0 ||
        key.IndexOf("Jewelcrafting.SocketBag", StringComparison.OrdinalIgnoreCase) >= 0 ||
        key.IndexOf("Jewelcrafting.Frame", StringComparison.OrdinalIgnoreCase) >= 0;

    private static string GetJewelcraftingGemDisplayName(string prefabName)
    {
        if (string.IsNullOrWhiteSpace(prefabName))
        {
            return "";
        }

        GameObject? prefab = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(prefabName) : null;
        ItemDrop? itemDrop = prefab != null && !IsUnityNull(prefab) ? prefab.GetComponent<ItemDrop>() : null;
        string name = itemDrop?.m_itemData?.m_shared?.m_name ?? prefabName;
        return Localization.instance != null ? Localization.instance.Localize(name) : name;
    }

    private static int FindMatchingJewelcraftingGemIcon(IReadOnlyList<JewelcraftingGemIconData> gems, bool[] used, Sprite sprite)
    {
        for (int i = 0; i < gems.Count; i++)
        {
            if (used[i])
            {
                continue;
            }

            Sprite gemSprite = gems[i].Sprite;
            if (gemSprite == sprite || !IsUnityNull(gemSprite) && string.Equals(gemSprite.name, sprite.name, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }
}

internal sealed class PinnedGemIconTooltipTarget : MonoBehaviour
{
    public void SetDisplayName(string displayName)
    {
        DisplayName = displayName ?? "";
        enabled = !string.IsNullOrWhiteSpace(DisplayName);
    }

    public string DisplayName { get; private set; } = "";
}

internal sealed class PinnedGemIconTooltipPanelHitTester : MonoBehaviour
{
    private readonly List<PinnedGemIconTooltipTarget> _targets = new();
    private PinnedGemIconTooltipTarget? _hoveredTarget;
    private RectTransform? _rectTransform;
    private Canvas? _canvas;
    private Camera? _camera;
    private bool _targetsDirty = true;

    public void InvalidateTargets()
    {
        _targetsDirty = true;
        _canvas = null;
        _camera = null;
    }

    private void Update()
    {
        PinnedGemIconTooltipTarget? target = FindHoveredTarget();
        if (target != null)
        {
            _hoveredTarget = target;
            InventorySlotsPlugin.ShowPinnedGemNameTooltip(this, target.DisplayName);
            return;
        }

        if (_hoveredTarget != null)
        {
            _hoveredTarget = null;
            InventorySlotsPlugin.HidePinnedGemNameTooltip(this);
        }
    }

    private void OnEnable()
    {
        InvalidateTargets();
    }

    private void OnTransformChildrenChanged()
    {
        _targetsDirty = true;
    }

    private void OnDisable()
    {
        if (_hoveredTarget != null)
        {
            _hoveredTarget = null;
            InventorySlotsPlugin.HidePinnedGemNameTooltip(this);
        }
    }

    private PinnedGemIconTooltipTarget? FindHoveredTarget()
    {
        _rectTransform ??= transform as RectTransform;
        if (_rectTransform == null ||
            !RectTransformContainsMouse(_rectTransform))
        {
            return null;
        }

        RefreshTargetsIfNeeded();
        for (int i = 0; i < _targets.Count; i++)
        {
            PinnedGemIconTooltipTarget target = _targets[i];
            if (target == null ||
                !target.isActiveAndEnabled ||
                string.IsNullOrWhiteSpace(target.DisplayName))
            {
                continue;
            }

            RectTransform? targetRect = target.transform as RectTransform;
            if (targetRect != null && RectTransformContainsMouse(targetRect))
            {
                return target;
            }
        }

        return null;
    }

    private void RefreshTargetsIfNeeded()
    {
        if (!_targetsDirty)
        {
            return;
        }

        _targets.Clear();
        GetComponentsInChildren(includeInactive: false, _targets);
        _canvas = GetComponentInParent<Canvas>();
        _camera = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? _canvas.worldCamera
            : null;
        _targetsDirty = false;
    }

    private bool RectTransformContainsMouse(RectTransform rect)
    {
        if (_canvas == null)
        {
            _canvas = GetComponentInParent<Canvas>();
            _camera = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? _canvas.worldCamera
                : null;
        }

        return RectTransformUtility.RectangleContainsScreenPoint(rect, Input.mousePosition, _camera);
    }
}
