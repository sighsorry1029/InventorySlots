using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YamlDotNet.Serialization;
using ItemData = ItemDrop.ItemData;

namespace InventorySlots;

internal enum SlotKind
{
    BuiltIn,
    CustomEquipment,
    Quick
}

internal enum PendingSlotUnequipDestination
{
    PlayerInventory,
    Container,
    DropOutside
}

internal enum PlayerStatPanelKind
{
    Armor,
    Between,
    Synergy,
    Weight,
    QuickStackMiniButton
}

internal sealed class SlotDefinition
{
    private readonly Func<ItemData?, bool> _accepts;

    public SlotDefinition(string id, string name, SlotKind kind, Func<ItemData?, bool> accepts, int quickSlotIndex = -1)
    {
        Id = id;
        Name = name;
        Kind = kind;
        _accepts = accepts;
        QuickSlotIndex = quickSlotIndex;
    }

    public string Id { get; }
    public string Name { get; }
    public SlotKind Kind { get; }
    public int QuickSlotIndex { get; }

    public bool Accepts(ItemData? item) => _accepts(item);
}

internal sealed class PendingSlotEquip
{
    public PendingSlotEquip(SlotDefinition slot, float createdAt)
    {
        Slot = slot;
        CreatedAt = createdAt;
    }

    public SlotDefinition Slot { get; }
    public float CreatedAt { get; }
}

internal sealed class PendingSlotUnequip
{
    public PendingSlotUnequip(SlotDefinition sourceSlot, PendingSlotUnequipDestination destination, Inventory? targetInventory, Vector2i targetPos, int amount, float createdAt)
    {
        SourceSlot = sourceSlot;
        Destination = destination;
        TargetInventory = targetInventory;
        TargetPos = targetPos;
        Amount = amount;
        CreatedAt = createdAt;
    }

    public SlotDefinition SourceSlot { get; }
    public PendingSlotUnequipDestination Destination { get; }
    public Inventory? TargetInventory { get; }
    public Vector2i TargetPos { get; }
    public int Amount { get; }
    public float CreatedAt { get; }
}

internal sealed class InventoryPanelDragMarker : MonoBehaviour
{
    public string PanelName { get; set; } = "";
    public bool Initialized { get; set; }
}

internal sealed class InventoryActionButtonMarker : MonoBehaviour
{
    public bool Initialized { get; set; }
    public bool AutoSizeInitialized { get; set; }
    public string LabelSignature { get; set; } = "";
}

internal sealed class PinnedTooltipPanelUiCache : MonoBehaviour
{
    public Image? Background { get; set; }
    public TMP_Text? BodyText { get; set; }
    public RectTransform? TextScrollView { get; set; }
    public RectTransform? TextViewport { get; set; }
    public RectTransform? TextContent { get; set; }
    public ScrollRect? TextScrollRect { get; set; }
    public Scrollbar? TextScrollbar { get; set; }
    public float TextWidth { get; set; }
    public float TextContentHeight { get; set; }
    public float TextViewportHeight { get; set; }
    public float TextTopReserved { get; set; }
    public float TextBottomReserved { get; set; }
    public bool TextHasViewportCap { get; set; }
    public string BackgroundSignature { get; set; } = "";
    public string TextLayoutSignature { get; set; } = "";
    public string TextRepairSignature { get; set; } = "";
}

internal sealed class JewelcraftingTooltipLayoutCache : MonoBehaviour
{
    public string Signature { get; set; } = "";
    public string NativeComponentSignature { get; set; } = "";
    public bool Visible { get; set; }
    public bool HasResolvedSocketGems { get; set; }
    public int RowlessRefreshAttempts { get; set; }
    public Transform? SourceRowsRoot { get; set; }
    public Transform? SourceInteract { get; set; }
    public Graphic[] NativeGraphics { get; set; } = Array.Empty<Graphic>();
    public TMP_Text[] NativeTexts { get; set; } = Array.Empty<TMP_Text>();
    public LayoutGroup[] NativeLayoutGroups { get; set; } = Array.Empty<LayoutGroup>();
    public ContentSizeFitter[] NativeContentSizeFitters { get; set; } = Array.Empty<ContentSizeFitter>();
}

internal sealed class InventoryGridElementUiCache : MonoBehaviour
{
    public TMP_Text? BindingText { get; set; }
    public string BindingSignature { get; set; } = "";
    public string EquipmentTooltipSignature { get; set; } = "";
    public RectTransform? FavoriteBorder { get; set; }
    public Image[] FavoriteBorderImages { get; set; } = Array.Empty<Image>();
    public RectTransform? PinnedTooltipMarker { get; set; }
    public TMP_Text? PinnedTooltipText { get; set; }
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

internal sealed class MovedPlayerStatPanel
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

    public MovedPlayerStatPanel(PlayerStatPanelKind kind, RectTransform rect, int sortOrder)
    {
        Kind = kind;
        SortOrder = sortOrder;
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

    public PlayerStatPanelKind Kind { get; set; }
    public int SortOrder { get; set; }
    public RectTransform Rect { get; }

    public void Restore()
    {
        if (Rect == null || _parent == null)
        {
            return;
        }

        Rect.SetParent(_parent, false);
        Rect.SetSiblingIndex(Mathf.Clamp(_siblingIndex, 0, _parent.childCount - 1));
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
    }
}

internal sealed class CraftingRecipeGridCell
{
    public CraftingRecipeGridCell(GameObject go)
    {
        Go = go;
        Rect = (RectTransform)go.transform;
        Background = go.GetComponent<Image>();
        Icon = go.transform.Find("icon")?.GetComponent<Image>();
        Amount = go.transform.Find("amount")?.GetComponent<TMP_Text>();
        Quality = go.transform.Find("quality")?.GetComponent<TMP_Text>();
        Selected = go.transform.Find("selected")?.gameObject;
        Equipped = go.transform.Find("equiped")?.gameObject;
        Queued = go.transform.Find("queued")?.gameObject;
        NoTeleport = go.transform.Find("noteleport")?.gameObject;
        Food = go.transform.Find("foodicon")?.GetComponent<Image>();
        Durability = go.transform.Find("durability")?.gameObject;
        Tooltip = go.GetComponent<UITooltip>() ?? go.AddComponent<UITooltip>();
        Input = go.GetComponent<UIInputHandler>() ?? go.AddComponent<UIInputHandler>();
        Marker = go.GetComponent<CraftingRecipeGridMarker>() ?? go.AddComponent<CraftingRecipeGridMarker>();
    }

    public GameObject Go { get; }
    public RectTransform Rect { get; }
    public Image? Background { get; }
    public Image? Icon { get; }
    public TMP_Text? Amount { get; }
    public TMP_Text? Quality { get; }
    public GameObject? Selected { get; }
    public GameObject? Equipped { get; }
    public GameObject? Queued { get; }
    public GameObject? NoTeleport { get; }
    public Image? Food { get; }
    public GameObject? Durability { get; }
    public UITooltip Tooltip { get; }
    public UIInputHandler Input { get; }
    public CraftingRecipeGridMarker Marker { get; }
}

internal sealed class CraftingRecipeGridMarker : MonoBehaviour
{
    public int Index { get; set; }
    public UITooltip? Tooltip { get; set; }
    public bool Initialized { get; set; }
}

internal sealed class CraftingPinnedTooltipMarkerState : MonoBehaviour
{
    public string LayoutSignature { get; set; } = "";
}

internal sealed class CraftingRequirementUiMarker : MonoBehaviour
{
    public string ChildSignature { get; set; } = "";
    public string LayoutSignature { get; set; } = "";
    public string AmountSignature { get; set; } = "";
    public Transform? Name { get; set; }
    public RectTransform? Icon { get; set; }
    public Image? IconImage { get; set; }
    public RectTransform? Amount { get; set; }
    public TMP_Text? AmountText { get; set; }
    public RectTransform? Hitbox { get; set; }
    public Image[] BackgroundImages { get; set; } = Array.Empty<Image>();
    public string CompetingTooltipSignature { get; set; } = "";
    public UITooltip[] CompetingTooltips { get; set; } = Array.Empty<UITooltip>();
}

internal sealed class CraftingTextCacheState : MonoBehaviour
{
    public string ChildSignature { get; set; } = "";
    public CraftingTextStamp LastTextStamp { get; set; }
    public CraftingTextColorStamp LastColorStamp { get; set; }
    public string ProgressBaseLabel { get; set; } = "";
    public TMP_Text[] TmpTexts { get; set; } = Array.Empty<TMP_Text>();
    public Text[] LegacyTexts { get; set; } = Array.Empty<Text>();
}

internal sealed class CraftingTooltipState : MonoBehaviour
{
    public CraftingSimpleTooltipStamp Stamp { get; set; }
}

internal readonly struct CraftingHoverTooltipContent
{
    public CraftingHoverTooltipContent(string topic, string body)
    {
        Topic = topic;
        Body = body;
    }

    public string Topic { get; }
    public string Body { get; }
}

internal sealed class CraftingRecipeStyleButtonMarker : MonoBehaviour
{
    public InventoryGui? Gui { get; set; }
    public int Index { get; set; } = -1;
}

internal sealed class CraftingRecipeViewEntry
{
    public CraftingRecipeViewEntry(
        int originalIndex,
        InventoryGui.RecipeDataPair pair,
        bool isFavorite,
        SortKey sortKey,
        bool isVeiledRecipeMasked,
        bool isVeiledRecipePreview)
    {
        OriginalIndex = originalIndex;
        Pair = pair;
        IsFavorite = isFavorite;
        SortKey = sortKey;
        IsVeiledRecipeMasked = isVeiledRecipeMasked;
        IsVeiledRecipePreview = isVeiledRecipePreview;
    }

    public int OriginalIndex { get; }
    public InventoryGui.RecipeDataPair Pair { get; }
    public bool IsFavorite { get; }
    public SortKey SortKey { get; }
    public bool IsVeiledRecipeMasked { get; }
    public bool IsVeiledRecipePreview { get; }
}

internal readonly struct CraftingRecipePairCacheKey : IEquatable<CraftingRecipePairCacheKey>
{
    public CraftingRecipePairCacheKey(Recipe? recipe, string itemKey)
    {
        RecipeId = recipe != null ? recipe.GetInstanceID() : 0;
        ItemKey = itemKey ?? "";
    }

    public int RecipeId { get; }
    public string ItemKey { get; }
    public bool IsValid => RecipeId != 0;

    public bool Equals(CraftingRecipePairCacheKey other) =>
        RecipeId == other.RecipeId && string.Equals(ItemKey, other.ItemKey, StringComparison.Ordinal);

    public override bool Equals(object? obj) =>
        obj is CraftingRecipePairCacheKey other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            return (RecipeId * 397) ^ StringComparer.Ordinal.GetHashCode(ItemKey);
        }
    }
}

internal readonly struct CraftingTabRowBounds
{
    public CraftingTabRowBounds(float left, float right, float top, float bottom)
    {
        Left = left;
        Right = right;
        Top = top;
        Bottom = bottom;
    }

    public float Left { get; }
    public float Right { get; }
    public float Top { get; }
    public float Bottom { get; }
    public float Height => Mathf.Max(1f, Top - Bottom);
}

internal sealed class CraftingRecipeGroupFilter
{
    private readonly Func<ItemData, bool> _matches;
    private readonly string _iconPrefab;

    public CraftingRecipeGroupFilter(
        string id,
        string tooltip,
        Func<ItemData, bool> matches,
        string iconPrefab = "")
    {
        Id = id;
        Tooltip = tooltip;
        _matches = matches;
        _iconPrefab = iconPrefab;
    }

    public string Id { get; }
    public string Tooltip { get; }
    public string Label => Id switch
    {
        "favorite" => "Favor",
        "melee" => "Melee",
        "ranged" => "Range",
        "magic" => "Magic",
        "armor" => "Equip",
        "food" => "Food",
        "consumable" => "Use",
        "meadbase" => "Base",
        "tool" => "Tool",
        "misc" => "Misc",
        _ => Id
    };
    public bool Matches(ItemData item) => _matches(item);

    public Sprite? GetIcon() => InventorySlotsPlugin.GetItemPrefabIcon(_iconPrefab);
}

internal sealed class CraftingRecipeGroupPanel
{
    public CraftingRecipeGroupPanel(RectTransform rect)
    {
        Rect = rect;
        Background = rect.GetComponent<Image>();
    }

    public RectTransform Rect { get; }
    public Image? Background { get; }
}

internal sealed class CraftingRecipeGroupButton
{
    public CraftingRecipeGroupButton(GameObject go)
    {
        Go = go;
        Rect = (RectTransform)go.transform;
        Background = go.GetComponent<Image>();
        Icon = go.transform.Find("icon")?.GetComponent<Image>();
        Tooltip = go.GetComponent<UITooltip>() ?? go.AddComponent<UITooltip>();
        Input = go.GetComponent<UIInputHandler>() ?? go.AddComponent<UIInputHandler>();
        Marker = go.GetComponent<CraftingGroupButtonMarker>() ?? go.AddComponent<CraftingGroupButtonMarker>();
        ActiveOverlay = EnsureActiveOverlay(Rect);
    }

    public GameObject Go { get; }
    public RectTransform Rect { get; }
    public Image? Background { get; }
    public Image? Icon { get; }
    public Image? ActiveOverlay { get; }
    public UITooltip Tooltip { get; }
    public UIInputHandler Input { get; }
    public CraftingGroupButtonMarker Marker { get; }

    private static Image EnsureActiveOverlay(RectTransform parent)
    {
        Transform? existing = parent.Find("InventorySlots_CraftingGroupActive");
        RectTransform rect = existing != null
            ? (RectTransform)existing
            : new GameObject("InventorySlots_CraftingGroupActive", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        if (rect.parent != parent)
        {
            rect.SetParent(parent, false);
        }

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        Image image = rect.GetComponent<Image>();
        image.sprite = InventorySlotsPlugin.GetSolidUiSprite();
        image.raycastTarget = false;
        rect.gameObject.SetActive(false);
        return image;
    }
}

internal sealed class CraftingGroupButtonMarker : MonoBehaviour
{
    public string FilterId { get; set; } = "";
    public bool Initialized { get; set; }
}

internal static class KeyboardShortcutExtensions
{
    public static bool IsKeyDown(this KeyboardShortcut shortcut)
    {
        return shortcut.MainKey != KeyCode.None && Input.GetKeyDown(shortcut.MainKey) && shortcut.Modifiers.All(IsShortcutKeyHeld);
    }

    public static string GetDisplayText(this KeyboardShortcut shortcut)
    {
        if (shortcut.MainKey == KeyCode.None)
        {
            return "";
        }

        IEnumerable<string> parts = shortcut.Modifiers
            .Select(GetShortcutKeyDisplayText)
            .Concat(new[] { GetShortcutKeyDisplayText(shortcut.MainKey) })
            .Where(part => !string.IsNullOrWhiteSpace(part));
        return string.Join(" + ", parts);
    }

    public static string GetCompactDisplayText(this KeyboardShortcut shortcut)
    {
        if (shortcut.MainKey == KeyCode.None)
        {
            return "";
        }

        IEnumerable<string> parts = shortcut.Modifiers
            .Select(GetCompactShortcutKeyDisplayText)
            .Concat(new[] { GetCompactShortcutKeyDisplayText(shortcut.MainKey) })
            .Where(part => !string.IsNullOrWhiteSpace(part));
        return string.Join("+", parts);
    }

    public static string GetDisplayText(this KeyCode key) => GetShortcutKeyDisplayText(key);

    public static string GetCompactDisplayText(this KeyCode key) => GetCompactShortcutKeyDisplayText(key);

    private static bool IsShortcutKeyHeld(KeyCode key)
    {
        return key switch
        {
            KeyCode.LeftAlt or KeyCode.RightAlt => Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt),
            KeyCode.LeftControl or KeyCode.RightControl => Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl),
            KeyCode.LeftShift or KeyCode.RightShift => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift),
            _ => Input.GetKey(key)
        };
    }

    private static string GetShortcutKeyDisplayText(KeyCode key)
    {
        return key switch
        {
            KeyCode.None => "",
            KeyCode.LeftAlt or KeyCode.RightAlt => "Alt",
            KeyCode.LeftControl or KeyCode.RightControl => "Ctrl",
            KeyCode.LeftShift or KeyCode.RightShift => "Shift",
            KeyCode.Mouse0 => "Mouse1",
            KeyCode.Mouse1 => "Mouse2",
            KeyCode.Mouse2 => "Mouse3",
            KeyCode.Mouse3 => "Mouse4",
            KeyCode.Mouse4 => "Mouse5",
            KeyCode.Mouse5 => "Mouse6",
            KeyCode.Mouse6 => "Mouse7",
            _ => key.ToString()
        };
    }

    private static string GetCompactShortcutKeyDisplayText(KeyCode key)
    {
        string text = key.ToString();
        if (text.StartsWith("Alpha", StringComparison.Ordinal))
        {
            return text.Substring("Alpha".Length);
        }

        if (text.StartsWith("Keypad", StringComparison.Ordinal))
        {
            return "Num" + text.Substring("Keypad".Length);
        }

        return key switch
        {
            KeyCode.None => "",
            KeyCode.LeftAlt or KeyCode.RightAlt => "Alt",
            KeyCode.LeftControl or KeyCode.RightControl => "Ctrl",
            KeyCode.LeftShift or KeyCode.RightShift => "Shift",
            KeyCode.Mouse0 => "M1",
            KeyCode.Mouse1 => "M2",
            KeyCode.Mouse2 => "M3",
            KeyCode.Mouse3 => "M4",
            KeyCode.Mouse4 => "M5",
            KeyCode.Mouse5 => "M6",
            KeyCode.Mouse6 => "M7",
            KeyCode.LeftBracket => "[",
            KeyCode.RightBracket => "]",
            KeyCode.BackQuote => "`",
            KeyCode.Backslash => "\\",
            KeyCode.Slash => "/",
            KeyCode.Semicolon => ";",
            KeyCode.Quote => "'",
            KeyCode.Comma => ",",
            KeyCode.Period => ".",
            KeyCode.Minus => "-",
            KeyCode.Equals => "=",
            KeyCode.Space => "Spc",
            KeyCode.Escape => "Esc",
            KeyCode.Return => "Enter",
            KeyCode.Backspace => "Bksp",
            KeyCode.Delete => "Del",
            KeyCode.Insert => "Ins",
            KeyCode.PageUp => "PgUp",
            KeyCode.PageDown => "PgDn",
            KeyCode.CapsLock => "Caps",
            KeyCode.LeftArrow => "Left",
            KeyCode.RightArrow => "Right",
            KeyCode.UpArrow => "Up",
            KeyCode.DownArrow => "Down",
            _ => text
        };
    }
}

internal static class ToggleExtensions
{
    public static bool IsOn(this InventorySlotsPlugin.Toggle value) => value == InventorySlotsPlugin.Toggle.On;
    public static bool IsOff(this InventorySlotsPlugin.Toggle value) => value == InventorySlotsPlugin.Toggle.Off;
}
