using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ItemData = ItemDrop.ItemData;

namespace InventoryActions;

internal sealed class InventoryActionRuntimeState
{
    public readonly List<Container> KnownContainers = new();
    public readonly HashSet<Vector2i> FavoriteSlots = new();
    public readonly ContainerHoldActionState AreaQuickStackHold = new();
    public readonly ContainerHoldActionState AreaRestockHold = new();
    public Dictionary<string, int> RestockTargetStackLimits = new(StringComparer.OrdinalIgnoreCase);
    public string LoadedFavoritesPlayerId = "";
    public RectTransform? PlayerActionPanel;
    public RectTransform? TrashPanel;
    public Button? ContainerStoreAllButton;
    public Button? ContainerRestockButton;
    public Button? ContainerSortButton;
    public RectTransformSnapshot? TakeAllButtonOriginal;
    public RectTransformSnapshot? StackAllButtonOriginal;
    public Sprite? TrashIconSprite;
    public GameObject? TrashConfirmDialog;
    public Inventory? TrashPendingInventory;
    public ItemData? TrashPendingItem;
    public int TrashPendingAmount;
}

internal sealed class ContainerHoldActionState
{
    public Container? Container;
    public float StartTime = -1f;
    public bool Triggered;
}

internal sealed class InventoryActionButtonMarker : MonoBehaviour
{
    public bool Initialized { get; set; }
    public bool AutoSizeInitialized { get; set; }
    public string LabelSignature { get; set; } = "";
}

internal sealed class InventoryTrashButtonMarker : MonoBehaviour
{
    public Image? Icon { get; set; }
    public bool TextSuppressed { get; set; }
    public string LayoutSignature { get; set; } = "";
    public bool LastCanTrash { get; set; }
    public bool HasVisualState { get; set; }
}

internal sealed class InventoryGridElementMarker : MonoBehaviour
{
    public RectTransform? FavoriteBorder { get; set; }
    public Image[] FavoriteBorderImages { get; set; } = Array.Empty<Image>();
}

internal sealed class RectTransformSnapshot
{
    private readonly Transform _parent;
    private readonly int _siblingIndex;
    private readonly Vector2 _anchorMin;
    private readonly Vector2 _anchorMax;
    private readonly Vector2 _pivot;
    private readonly Vector2 _sizeDelta;
    private readonly Vector2 _anchoredPosition;
    private readonly Vector2 _offsetMin;
    private readonly Vector2 _offsetMax;
    private readonly Vector3 _localPosition;
    private readonly Quaternion _localRotation;
    private readonly Vector3 _localScale;

    public RectTransformSnapshot(RectTransform rect)
    {
        Rect = rect;
        _parent = rect.parent;
        _siblingIndex = rect.GetSiblingIndex();
        _anchorMin = rect.anchorMin;
        _anchorMax = rect.anchorMax;
        _pivot = rect.pivot;
        _sizeDelta = rect.sizeDelta;
        _anchoredPosition = rect.anchoredPosition;
        _offsetMin = rect.offsetMin;
        _offsetMax = rect.offsetMax;
        _localPosition = rect.localPosition;
        _localRotation = rect.localRotation;
        _localScale = rect.localScale;
    }

    public RectTransform Rect { get; }

    public void Restore()
    {
        if (Rect == null)
        {
            return;
        }

        if (_parent != null && Rect.parent != _parent)
        {
            Rect.SetParent(_parent, false);
        }

        Rect.anchorMin = _anchorMin;
        Rect.anchorMax = _anchorMax;
        Rect.pivot = _pivot;
        Rect.sizeDelta = _sizeDelta;
        Rect.anchoredPosition = _anchoredPosition;
        Rect.offsetMin = _offsetMin;
        Rect.offsetMax = _offsetMax;
        Rect.localPosition = _localPosition;
        Rect.localRotation = _localRotation;
        Rect.localScale = _localScale;
        if (_parent != null)
        {
            Rect.SetSiblingIndex(Mathf.Clamp(_siblingIndex, 0, _parent.childCount - 1));
        }
    }
}
