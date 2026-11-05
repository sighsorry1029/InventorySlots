using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using UnityEngine;
using Valheim.UI;
using ItemData = ItemDrop.ItemData;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private const string QuickSlotRadialGroupName = "InventorySlots_QuickSlotsRadialGroup";
    private static Sprite? _quickSlotRadialIcon;
    private static bool _quickSlotRadialIconLoadAttempted;

    private static Sprite GetQuickSlotRadialIcon()
    {
        if (_quickSlotRadialIcon != null) return _quickSlotRadialIcon;
        if (!_quickSlotRadialIconLoadAttempted)
        {
            _quickSlotRadialIconLoadAttempted = true;
            Texture2D? texture = null;
            try
            {
                using Stream stream = typeof(InventorySlotsPlugin).Assembly.GetManifestResourceStream("InventorySlots.QuickSlotsRadial.png")
                    ?? throw new InvalidDataException("Missing embedded quick-slot radial icon.");
                using var bytes = new MemoryStream();
                stream.CopyTo(bytes);
                texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                {
                    name = "InventorySlots_QuickSlotsRadialIcon",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave
                };
                // This Unity module targets netstandard 2.1 while this plugin
                // targets .NET Framework 4.8. Resolve its public API once when loading the icon.
                Type imageConversion = Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule", throwOnError: true)!;
                var method = imageConversion.GetMethod("LoadImage", new[] { typeof(Texture2D), typeof(byte[]), typeof(bool) });
                if (method == null || !method.IsStatic || method.ReturnType != typeof(bool))
                    throw new MissingMethodException(imageConversion.FullName, "LoadImage(Texture2D, byte[], bool)");
                var loadImage = (Func<Texture2D, byte[], bool, bool>)Delegate.CreateDelegate(typeof(Func<Texture2D, byte[], bool, bool>), method);
                if (!loadImage(texture, bytes.ToArray(), true))
                    throw new InvalidDataException("Invalid quick-slot radial icon PNG.");
                _quickSlotRadialIcon = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
                _quickSlotRadialIcon.name = texture.name;
                _quickSlotRadialIcon.hideFlags = HideFlags.HideAndDontSave;
                return _quickSlotRadialIcon;
            }
            catch (Exception exception)
            {
                if (texture != null) UnityEngine.Object.Destroy(texture);
                _instance?.Logger.LogWarning($"Quick-slot radial icon could not be loaded; using the native icon: {exception.Message}");
            }
        }
        return RadialData.SO.HotbarGroupConfig.Sprite;
    }

    private static void DestroyQuickSlotRadialIcon()
    {
        if (_quickSlotRadialIcon != null)
        {
            UnityEngine.Object.Destroy(_quickSlotRadialIcon.texture);
            UnityEngine.Object.Destroy(_quickSlotRadialIcon);
        }
        _quickSlotRadialIcon = null;
        _quickSlotRadialIconLoadAttempted = false;
    }

    internal static void AddQuickSlotRadialGroup(RadialBase radial, List<RadialMenuElement> elements)
    {
        Player? player = Player.m_localPlayer;
        if (_instance == null || !_instance.isActiveAndEnabled || IsDedicatedServer ||
            player == null || player.m_isLoading || player.IsDead() ||
            radial.IsHoverMenu || radial.CurrentConfig is not ValheimRadialConfig ||
            elements == null || GetUnlockedQuickSlotCount(player) <= 0 ||
            RadialData.SO == null || RadialData.SO.GroupElement == null) return;

        foreach (RadialMenuElement element in elements)
            if (element != null && element.name == QuickSlotRadialGroupName) return;

        GroupElement group = UnityEngine.Object.Instantiate(RadialData.SO.GroupElement);
        group.name = QuickSlotRadialGroupName;
        group.Init(QuickSlotsRadialConfig.Instance, radial.CurrentConfig, radial);
        elements.Add(group);
    }

    private sealed class QuickSlotsRadialConfig : IRadialConfig
    {
        public static readonly QuickSlotsRadialConfig Instance = new();
        public string LocalizedName => LocalizeUi("$inventoryslots_radial_quick_slots", "Quick slots");
        public Sprite Sprite => GetQuickSlotRadialIcon();

        public void InitRadialConfig(RadialBase radial)
        {
            Player? player = Player.m_localPlayer;
            if (_instance == null || !_instance.isActiveAndEnabled ||
                player == null || player.m_isLoading || player.IsDead() || RadialData.SO == null ||
                RadialData.SO.ItemElement == null || RadialData.SO.EmptyElement == null)
            {
                radial.QueuedClose();
                return;
            }

            Inventory inventory = player.GetInventory();
            int count = GetUnlockedQuickSlotCount(player);
            if (inventory == null || count <= 0)
            {
                radial.QueuedClose();
                return;
            }

            var elements = new List<RadialMenuElement>(count);
            for (int i = 0; i < count; i++)
            {
                ItemData? item = null;
                if (TryGetQuickSlotDefinition(i, out SlotDefinition? slot) && slot != null &&
                    IsQuickSlotUnlocked(player, slot) && TryGetSlotGridPos(inventory, slot, out Vector2i pos))
                    item = inventory.GetItemAt(pos.x, pos.y);

                if (item == null || slot == null || !slot.Accepts(item))
                {
                    EmptyElement empty = UnityEngine.Object.Instantiate(RadialData.SO.EmptyElement);
                    empty.Init();
                    elements.Add(empty);
                    continue;
                }

                elements.Add(CreateQuickSlotRadialItem(player, inventory, slot.Id, item));
            }
            // Native construction owns Back, controller/mouse selection and
            // layer sizing, including nine slots plus Back. Do not alter it.
            radial.ConstructRadial(elements);
        }
    }

    private static ItemElement CreateQuickSlotRadialItem(Player player, Inventory inventory, string slotId, ItemData item)
    {
        ItemElement element = UnityEngine.Object.Instantiate(RadialData.SO.ItemElement);
        element.Init(item);
        element.Interact = () =>
        {
            if (!TryValidateQuickSlotRadialItem(player, inventory, slotId, item, out SlotDefinition? current))
                return false;
            ActivateQuickSlotHotkey(player, inventory, current!);
            return true;
        };
        // Preserve native drop/submenu actions, but never use a stale entry
        // after a character change, slot lock, or replacement of its item.
        Func<bool>? secondary = element.SecondaryInteract;
        element.SecondaryInteract = () =>
            TryValidateQuickSlotRadialItem(player, inventory, slotId, item, out _) && (secondary?.Invoke() ?? false);
        Func<RadialBase, int, bool>? subRadial = element.TryOpenSubRadial;
        element.TryOpenSubRadial = (menu, index) =>
            TryValidateQuickSlotRadialItem(player, inventory, slotId, item, out _) && (subRadial?.Invoke(menu, index) ?? false);
        Func<GameObject, bool>? hover = element.HoverMenuInteract;
        element.HoverMenuInteract = target =>
            TryValidateQuickSlotRadialItem(player, inventory, slotId, item, out _) && (hover?.Invoke(target) ?? false);
        element.CloseOnInteract = () => true;
        element.AdvancedCloseOnInteract = null;
        return element;
    }

    private static bool TryValidateQuickSlotRadialItem(Player player, Inventory inventory, string slotId,
        ItemData item, out SlotDefinition? slot)
    {
        slot = null;
        // LastUsed retains the original ItemElement and its callbacks. Never
        // resolve just a slot index and accidentally use its replacement item.
        return _instance != null && _instance.isActiveAndEnabled &&
            player != null && player == Player.m_localPlayer && !player.m_isLoading && !player.IsDead() &&
            !ShouldBlockGlobalHotkeys(player) && ReferenceEquals(player.GetInventory(), inventory) &&
            item.m_stack > 0 && TryGetSlotById(slotId, out slot) && slot != null &&
            slot.Kind == SlotKind.Quick && slot.PovCell == null && IsQuickSlotUnlocked(player, slot) &&
            TryGetSlotGridPos(inventory, slot, out Vector2i pos) &&
            ReferenceEquals(inventory.GetItemAt(pos.x, pos.y), item) && slot.Accepts(item);
    }
}

[HarmonyPatch(typeof(RadialBase), nameof(RadialBase.ConstructRadial), new[] { typeof(List<RadialMenuElement>) })]
internal static class QuickSlotsRadialMenuPatch
{
    private static void Prefix(RadialBase __instance, List<RadialMenuElement> __0) =>
        InventorySlotsPlugin.AddQuickSlotRadialGroup(__instance, __0);
}
