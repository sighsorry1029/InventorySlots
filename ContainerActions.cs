using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using ItemData = ItemDrop.ItemData;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private const string ContainerActionSuccessFxPrefabName = "fx_HildirChest_Unlock";
    private const int ContainerActionSuccessFxMaxMode = 12;
    private static readonly Dictionary<Type, SfxVolumeMemberCache> SfxVolumeMembersByType = new();
    private delegate bool TryGetContainerHoldContext(Player player, out Container container);

    private static List<Container> GetActionContainers(Player player, Container currentContainer, bool areaForQuickStack)
    {
        List<Container> containers = new();
        HashSet<Container> seen = new();
        if (currentContainer != null && currentContainer.m_inventory != null && seen.Add(currentContainer))
        {
            containers.Add(currentContainer);
        }

        float range = areaForQuickStack ? _areaQuickStackRange?.Value ?? 0f : _areaRestockRange?.Value ?? 0f;
        if (range <= 0f)
        {
            return containers;
        }

        if (currentContainer == null || IsUnityNull(currentContainer))
        {
            return containers;
        }

        Vector3 origin = currentContainer.transform.position;
        float rangeSq = range * range;
        List<(Container Container, float DistanceSq)> areaContainers = new();
        for (int i = InventoryContainers.KnownContainers.Count - 1; i >= 0; i--)
        {
            Container container = InventoryContainers.KnownContainers[i];
            if (container == null || IsUnityNull(container))
            {
                InventoryContainers.KnownContainers.RemoveAt(i);
                continue;
            }

            if (seen.Contains(container))
            {
                continue;
            }

            if (IsAreaContainerAllowed(player, container, currentContainer, origin, rangeSq, out float distanceSq))
            {
                areaContainers.Add((container, distanceSq));
                seen.Add(container);
            }
        }

        areaContainers.Sort((left, right) => left.DistanceSq.CompareTo(right.DistanceSq));
        foreach ((Container container, _) in areaContainers)
        {
            containers.Add(container);
        }

        return containers;
    }

    private static int RunContainerTransferAcrossContainers(
        Player localPlayer,
        Container anchorContainer,
        bool includeArea,
        bool areaForQuickStack,
        Func<Container, int> transfer,
        Action onMoved)
    {
        if (localPlayer == null || anchorContainer == null || transfer == null)
        {
            return 0;
        }

        int fxMode = includeArea ? GetContainerActionSuccessFxMode() : 0;
        int changedContainerFxCount = 0;
        List<Container> containers = includeArea
            ? GetActionContainers(localPlayer, anchorContainer, areaForQuickStack)
            : new List<Container> { anchorContainer };

        return ContainerTransferCore.Run(
            containers,
            container => !IsUnityNull(container) && container.m_inventory != null,
            transfer,
            (container, _) => changedContainerFxCount = TryPlayChangedContainerActionSuccessFx(localPlayer, container, fxMode, changedContainerFxCount),
            () =>
            {
                onMoved?.Invoke();
                if (fxMode == 1)
                {
                    PlayContainerActionSuccessFx(localPlayer, anchorContainer);
                }
            });
    }

    private static void ShowContainerActionResult(Player player, string actionToken, string actionFallback, int moved)
    {
        if (player == null)
        {
            return;
        }

        string action = LocalizeUi(actionToken, actionFallback);
        string format = LocalizeUi("$inventoryslots_action_result_format", "{action}: {count}");
        string message = format
            .Replace("{action}", action)
            .Replace("{count}", moved.ToString());
        player.Message(MessageHud.MessageType.Center, message, 0, null);
    }

    private static List<Vector2i> GetPlayerActionSlots(Player player, Inventory inventory, bool includeHotbar, bool blockFavorites = false)
    {
        List<Vector2i> slots = new();
        int fixedRows = Math.Min(GetFixedRegularRows(), inventory.GetHeight());
        HashSet<Vector2i> blockedSlots = blockFavorites ? GetFavoriteBlockedSlots(player, inventory) : new HashSet<Vector2i>();
        for (int y = 0; y < fixedRows; y++)
        {
            for (int x = 0; x < inventory.GetWidth(); x++)
            {
                Vector2i pos = new(x, y);
                if (blockedSlots.Contains(pos))
                {
                    continue;
                }

                InventoryCellKind kind = GetInventoryCellKind(player, inventory, pos);
                if (InventoryActionCellPolicyCore.CanUseContainerActionSource(kind, includeHotbar))
                {
                    slots.Add(pos);
                }
            }
        }

        return slots;
    }

    private static HashSet<Vector2i> GetFavoriteBlockedSlots(Player player, Inventory inventory)
    {
        HashSet<Vector2i> blocked = new();
        if (!AreFavoritesEnabled())
        {
            return blocked;
        }

        EnsureFavoritesLoaded(player);
        foreach (ItemData item in inventory.m_inventory)
        {
            if (item?.m_shared == null)
            {
                continue;
            }

            if (FavoriteSlots.Contains(item.m_gridPos))
            {
                blocked.Add(item.m_gridPos);
            }
        }

        return blocked;
    }

    private static bool TryGetActionContext(Player? player, out Player localPlayer, out Inventory playerInventory, out Container container, out Inventory containerInventory)
    {
        localPlayer = null!;
        playerInventory = null!;
        container = null!;
        containerInventory = null!;

        if (player == null || player.m_isLoading || InventoryGui.instance == null)
        {
            return false;
        }

        Container currentContainer = InventoryGui.instance.m_currentContainer;
        if (currentContainer == null || currentContainer.m_inventory == null)
        {
            return false;
        }

        if (!CanMutateContainerDirectly(currentContainer, allowLocalWithoutZNetView: true))
        {
            player.Message(MessageHud.MessageType.Center, LocalizeUi("$inventoryslots_container_not_ready", "Container is not ready."), 0, null);
            return false;
        }

        Inventory inventory = ((Humanoid)player).GetInventory();
        if (inventory == null)
        {
            return false;
        }

        localPlayer = player;
        playerInventory = inventory;
        container = currentContainer;
        containerInventory = currentContainer.m_inventory;
        return true;
    }

    private static bool HasNoCustomData(ItemData item)
    {
        return item.m_customData == null || item.m_customData.Count == 0;
    }

    private static bool CanUseContainerActionStacking(ItemData item)
    {
        return item?.m_shared != null && (HasNoCustomData(item) || IsTrustedCustomDataStackingItem(item));
    }

    private static bool IsTrustedCustomDataStackingItem(ItemData? item)
    {
        return IsJewelcraftingOrbItem(item) || IsEpicLootStackableMaterial(item);
    }

    private static bool IsJewelcraftingOrbItem(ItemData? item)
    {
        if (item?.m_shared == null || !HasPlugin(JewelcraftingGuid))
        {
            return false;
        }

        string prefabName = GetItemPrefabName(item);
        if (prefabName.StartsWith("JC_Orb_", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        string sharedName = item.m_shared.m_name ?? "";
        return sharedName.StartsWith("$jc_orb_", StringComparison.OrdinalIgnoreCase) ||
               sharedName.IndexOf("orb_of_", StringComparison.OrdinalIgnoreCase) >= 0 &&
               prefabName.StartsWith("JC_", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsEpicLootStackableMaterial(ItemData? item)
    {
        if (item?.m_shared == null || !HasPlugin(EpicLootGuid))
        {
            return false;
        }

        string prefabName = GetItemPrefabName(item);
        if (IsEpicLootStackableMaterialToken(prefabName))
        {
            return true;
        }

        if (!IsUnityNull(item.m_dropPrefab) && IsEpicLootStackableMaterialToken(item.m_dropPrefab.name))
        {
            return true;
        }

        return IsEpicLootStackableMaterialToken(item.m_shared.m_name ?? "");
    }

    private static bool IsEpicLootStackableMaterialToken(string token)
    {
        return token.StartsWith("Shard", StringComparison.OrdinalIgnoreCase) ||
               token.StartsWith("Essence", StringComparison.OrdinalIgnoreCase) ||
               token.StartsWith("Dust", StringComparison.OrdinalIgnoreCase) ||
               token.StartsWith("Reagent", StringComparison.OrdinalIgnoreCase);
    }

    private static int CountMovedFromContainerSource(Inventory sourceInventory, ItemData sourceItem, int before, int requestedAmount, bool moveSucceeded, out bool remotePending)
    {
        int after = sourceInventory.m_inventory.Contains(sourceItem) ? sourceItem.m_stack : 0;
        bool useFallback = IsRemoteMultiUserChestInventory(sourceInventory);
        int moved = ContainerActionCore.CountMovedAmount(before, after, requestedAmount, moveSucceeded, useFallback);
        remotePending = Math.Max(0, before - after) == 0 && moveSucceeded && useFallback && moved > 0;
        return moved;
    }

    private static void RemoveItemIfStillOwned(Inventory inventory, ItemData item)
    {
        if (inventory != null && item != null && inventory.m_inventory.Contains(item))
        {
            inventory.RemoveItem(item);
        }
    }

    private static int CompareGridOrder(Vector2i a, Vector2i b)
    {
        return ContainerActionCore.CompareGridOrder(a.x, a.y, b.x, b.y);
    }

    private static int GetContainerActionSuccessFxMode() =>
        Mathf.Clamp(_containerActionSuccessFxMode != null ? _containerActionSuccessFxMode.Value : 1, 0, ContainerActionSuccessFxMaxMode);

    private static float GetContainerHoverHoldDuration() =>
        Mathf.Clamp(_containerHoverHoldDuration != null ? _containerHoverHoldDuration.Value : ContainerHoverHoldDurationDefault, ContainerHoverHoldDurationMin, ContainerHoverHoldDurationMax);

    private static void HandleContainerHoldHotkey(
        Player player,
        ContainerHoldActionState hold,
        TryGetContainerHoldContext tryGetContext,
        Func<Player, Container, bool> executeAction)
    {
        if (!tryGetContext(player, out Container container))
        {
            ResetContainerHold(hold);
            return;
        }

        if (IsUnityNull(hold.Container) || hold.Container != container)
        {
            hold.Container = container;
            hold.StartTime = Time.time;
            hold.Triggered = false;
        }

        if (hold.Triggered || Time.time - hold.StartTime < GetContainerHoverHoldDuration())
        {
            return;
        }

        if (executeAction(player, container))
        {
            hold.Triggered = true;
        }
        else
        {
            ResetContainerHold(hold);
        }
    }

    private static void ResetContainerHold(ContainerHoldActionState hold)
    {
        hold.Container = null;
        hold.StartTime = -1f;
        hold.Triggered = false;
    }

    private static float GetContainerActionSuccessFxVolume() =>
        Mathf.Clamp01(_containerActionSuccessFxVolume != null ? _containerActionSuccessFxVolume.Value : 1f);

    private static int TryPlayChangedContainerActionSuccessFx(Player player, Container container, int mode, int played)
    {
        if (mode < 2 || played >= mode)
        {
            return played;
        }

        PlayContainerActionSuccessFx(player, container);
        return played + 1;
    }

    private static void PlayContainerActionSuccessFx(Player player, Container container)
    {
        if (player == null ||
            IsUnityNull(player) ||
            container == null ||
            IsUnityNull(container) ||
            ZNetScene.instance == null)
        {
            return;
        }

        GameObject? prefab = ZNetScene.instance.GetPrefab(ContainerActionSuccessFxPrefabName);
        if (prefab == null || IsUnityNull(prefab))
        {
            return;
        }

        Vector3 position = container.transform.position;
        Quaternion rotation = container.transform.rotation;
        GameObject instance = UnityEngine.Object.Instantiate(prefab, position, rotation);
        ApplyContainerActionSuccessFxVolume(instance);
    }

    private static void ApplyContainerActionSuccessFxVolume(GameObject instance)
    {
        float volumeScale = GetContainerActionSuccessFxVolume();
        if (instance == null || IsUnityNull(instance) || volumeScale >= 0.999f)
        {
            return;
        }

        foreach (Component component in instance.GetComponentsInChildren<Component>(includeInactive: true))
        {
            if (component != null && !IsUnityNull(component))
            {
                ScaleSfxComponentVolume(component, volumeScale);
            }
        }
    }

    private static void ScaleSfxComponentVolume(Component component, float volumeScale)
    {
        Type type = component.GetType();
        if (IsUnityAudioSource(type))
        {
            ScaleUnityAudioSourceVolume(component, type, volumeScale);
            return;
        }

        if (!IsLikelySfxComponent(type))
        {
            return;
        }

        SfxVolumeMemberCache members = GetSfxVolumeMemberCache(type);
        foreach (FieldInfo field in members.Fields)
        {
            try
            {
                field.SetValue(component, Mathf.Max(0f, (float)field.GetValue(component) * volumeScale));
            }
            catch
            {
                // Some Unity-backed members can reject reflection writes; AudioSource volume is already handled above.
            }
        }

        foreach (PropertyInfo property in members.Properties)
        {
            try
            {
                property.SetValue(component, Mathf.Max(0f, (float)property.GetValue(component) * volumeScale));
            }
            catch
            {
                // Best-effort support for SFX wrappers with writable volume properties.
            }
        }
    }

    private static SfxVolumeMemberCache GetSfxVolumeMemberCache(Type type)
    {
        if (SfxVolumeMembersByType.TryGetValue(type, out SfxVolumeMemberCache cache))
        {
            return cache;
        }

        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        cache = new SfxVolumeMemberCache(
            type.GetFields(flags)
                .Where(field =>
                    field.FieldType == typeof(float) &&
                    IsSfxVolumeMemberName(field.Name) &&
                    !IsUnsupportedSfxVolumeMemberName(field.Name))
                .ToArray(),
            type.GetProperties(flags)
                .Where(property =>
                    property.PropertyType == typeof(float) &&
                    property.CanRead &&
                    property.CanWrite &&
                    property.GetIndexParameters().Length == 0 &&
                    IsSfxVolumeMemberName(property.Name) &&
                    !IsUnsupportedSfxVolumeMemberName(property.Name))
                .ToArray());
        SfxVolumeMembersByType[type] = cache;
        return cache;
    }

    private static bool IsLikelySfxComponent(Type type)
    {
        string name = type.Name;
        return name.IndexOf("SFX", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Audio", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsSfxVolumeMemberName(string name)
    {
        return name.IndexOf("volume", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("vol", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsUnsupportedSfxVolumeMemberName(string name)
    {
        return string.Equals(name, "minVolume", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(name, "maxVolume", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsUnityAudioSource(Type type) =>
        string.Equals(type.FullName, "UnityEngine.AudioSource", StringComparison.Ordinal);

    private static void ScaleUnityAudioSourceVolume(Component component, Type type, float volumeScale)
    {
        PropertyInfo? volumeProperty = type.GetProperty("volume", BindingFlags.Instance | BindingFlags.Public);
        if (volumeProperty == null || !volumeProperty.CanRead || !volumeProperty.CanWrite)
        {
            return;
        }

        try
        {
            volumeProperty.SetValue(component, Mathf.Max(0f, (float)volumeProperty.GetValue(component) * volumeScale));
        }
        catch
        {
            // Best-effort volume scaling for Unity audio sources without touching deprecated minVolume/maxVolume.
        }
    }

    private readonly struct SfxVolumeMemberCache
    {
        public SfxVolumeMemberCache(FieldInfo[] fields, PropertyInfo[] properties)
        {
            Fields = fields;
            Properties = properties;
        }

        public FieldInfo[] Fields { get; }
        public PropertyInfo[] Properties { get; }
    }
}
