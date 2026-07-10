using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEngine;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private sealed class JewelcraftingGemApi
    {
        private readonly MethodInfo _getGemsMethod;
        private readonly MethodInfo? _itemDataMethod;
        private readonly MethodInfo? _itemInfoGetSocketableMethod;
        private readonly MethodInfo? _itemInfoGetItemContainerMethod;
        private readonly FieldInfo? _socketedGemsField;
        private readonly FieldInfo? _socketNameField;
        private readonly FieldInfo? _socketSeedField;
        private readonly FieldInfo? _openEquipmentField;
        private readonly FieldInfo? _openInventoryField;
        private readonly MethodInfo? _openSocketContainerMethod;
        private readonly FieldInfo? _inventoryInteractBehaviourField;
        private readonly FieldInfo? _inventorySocketingField;
        private readonly Type? _itemBagType;
        private readonly PropertyInfo? _itemInfoItemDataProperty;
        private readonly MethodInfo? _itemInfoGetAllSocketSeedsMethod;
        private readonly PropertyInfo? _socketSeedSeedProperty;
        private readonly Dictionary<Type, MemberInfo?> _gemSpriteMembers = new();
        private readonly Dictionary<Type, MemberInfo?> _gemPrefabMembers = new();

        private JewelcraftingGemApi(
            MethodInfo getGemsMethod,
            MethodInfo? itemDataMethod,
            MethodInfo? itemInfoGetSocketableMethod,
            MethodInfo? itemInfoGetItemContainerMethod,
            FieldInfo? socketedGemsField,
            FieldInfo? socketNameField,
            FieldInfo? socketSeedField,
            FieldInfo? openEquipmentField,
            FieldInfo? openInventoryField,
            MethodInfo? openSocketContainerMethod,
            FieldInfo? inventoryInteractBehaviourField,
            FieldInfo? inventorySocketingField,
            Type? itemBagType,
            PropertyInfo? itemInfoItemDataProperty,
            MethodInfo? itemInfoGetAllSocketSeedsMethod,
            PropertyInfo? socketSeedSeedProperty)
        {
            _getGemsMethod = getGemsMethod;
            _itemDataMethod = itemDataMethod;
            _itemInfoGetSocketableMethod = itemInfoGetSocketableMethod;
            _itemInfoGetItemContainerMethod = itemInfoGetItemContainerMethod;
            _socketedGemsField = socketedGemsField;
            _socketNameField = socketNameField;
            _socketSeedField = socketSeedField;
            _openEquipmentField = openEquipmentField;
            _openInventoryField = openInventoryField;
            _openSocketContainerMethod = openSocketContainerMethod;
            _inventoryInteractBehaviourField = inventoryInteractBehaviourField;
            _inventorySocketingField = inventorySocketingField;
            _itemBagType = itemBagType;
            _itemInfoItemDataProperty = itemInfoItemDataProperty;
            _itemInfoGetAllSocketSeedsMethod = itemInfoGetAllSocketSeedsMethod;
            _socketSeedSeedProperty = socketSeedSeedProperty;
        }

        public static bool TryCreate(Assembly assembly, out JewelcraftingGemApi? api, out string detail)
        {
            api = null;
            Type? apiType = assembly.GetType("Jewelcrafting.API");
            MethodInfo? getGemsMethod = apiType?.GetMethod(
                "GetGems",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(ItemDrop.ItemData) },
                null);
            if (getGemsMethod == null)
            {
                detail = "API.GetGems was not found";
                return false;
            }

            Type? itemExtensionsType = assembly.GetType("ItemDataManager.ItemExtensions");
            Type? itemInfoType = assembly.GetType("ItemDataManager.ItemInfo");
            Type? socketableType = assembly.GetType("Jewelcrafting.Socketable");
            Type? itemContainerType = assembly.GetType("Jewelcrafting.ItemContainer");
            Type? itemBagType = assembly.GetType("Jewelcrafting.ItemBag");
            Type? socketItemType = assembly.GetType("Jewelcrafting.SocketItem");
            Type? jewelcraftingType = assembly.GetType("Jewelcrafting.Jewelcrafting");
            Type? addFakeSocketsContainerType = assembly.GetType("Jewelcrafting.GemStones+AddFakeSocketsContainer");
            Type? openFakeSocketsContainerType = assembly.GetType("Jewelcrafting.GemStones+OpenFakeSocketsContainer");
            Type? socketSeedType = assembly.GetType("Jewelcrafting.SocketSeed");

            MethodInfo? itemDataMethod = itemExtensionsType?.GetMethod(
                "Data",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(ItemDrop.ItemData) },
                null);
            MethodInfo? itemInfoGetAllSocketSeedsMethod = null;
            MethodInfo? itemInfoGetMethod = null;
            MethodInfo? itemInfoGetItemContainerMethod = null;
            if (itemInfoType != null)
            {
                foreach (MethodInfo method in itemInfoType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (method.Name == "Get" && method.IsGenericMethodDefinition)
                    {
                        ParameterInfo[] parameters = method.GetParameters();
                        if (socketableType != null && parameters.Length == 1)
                        {
                            itemInfoGetMethod = method.MakeGenericMethod(socketableType);
                        }

                        if (itemContainerType != null && (parameters.Length == 0 || (parameters.Length == 1 && itemInfoGetItemContainerMethod == null)))
                        {
                            itemInfoGetItemContainerMethod = method.MakeGenericMethod(itemContainerType);
                        }
                    }
                    else if (socketSeedType != null && method.Name == "GetAll" && method.IsGenericMethodDefinition && method.GetParameters().Length == 0)
                    {
                        itemInfoGetAllSocketSeedsMethod = method.MakeGenericMethod(socketSeedType);
                    }
                }
            }

            api = new JewelcraftingGemApi(
                getGemsMethod,
                itemDataMethod,
                itemInfoGetMethod,
                itemInfoGetItemContainerMethod,
                socketableType?.GetField("socketedGems", BindingFlags.Public | BindingFlags.Instance),
                socketItemType?.GetField("Name", BindingFlags.Public | BindingFlags.Instance),
                socketItemType?.GetField("Seed", BindingFlags.Public | BindingFlags.Instance),
                addFakeSocketsContainerType?.GetField("openEquipment", BindingFlags.Public | BindingFlags.Static),
                addFakeSocketsContainerType?.GetField("openInventory", BindingFlags.Public | BindingFlags.Static),
                openFakeSocketsContainerType?.GetMethod("Open", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(InventoryGui), typeof(ItemDrop.ItemData) }, null),
                jewelcraftingType?.GetField("inventoryInteractBehaviour", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static),
                jewelcraftingType?.GetField("inventorySocketing", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static),
                itemBagType,
                itemInfoType?.GetProperty("ItemData", BindingFlags.Public | BindingFlags.Instance),
                itemInfoGetAllSocketSeedsMethod,
                socketSeedType?.GetProperty("Seed", BindingFlags.Public | BindingFlags.Instance));
            detail = "";
            return true;
        }

        public object? GetGems(ItemDrop.ItemData item) => _getGemsMethod.Invoke(null, new object[] { item });

        public bool CanOpenSocketContainerFromInventory(ItemDrop.ItemData item, bool gemcutterStationActive)
        {
            object? itemContainer = GetItemContainer(item);
            if (itemContainer == null || string.Equals(GetConfigEntryValueName(_inventoryInteractBehaviourField), "Enabled", StringComparison.Ordinal))
            {
                return false;
            }

            string inventorySocketing = GetConfigEntryValueName(_inventorySocketingField);
            return string.IsNullOrWhiteSpace(inventorySocketing) ||
                   string.Equals(inventorySocketing, "On", StringComparison.Ordinal) ||
                   IsItemBag(itemContainer) ||
                   gemcutterStationActive;
        }

        public bool HasSocketContainer(ItemDrop.ItemData item) => GetItemContainer(item) != null;

        public bool TryOpenSocketContainer(InventoryGui gui, ItemDrop.ItemData item)
        {
            if (_openSocketContainerMethod == null || gui == null || item?.m_shared == null)
            {
                return false;
            }

            try
            {
                object? result = _openSocketContainerMethod.Invoke(null, new object[] { gui, item });
                return result is bool continueVanilla && !continueVanilla;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public ItemDrop.ItemData? GetOpenSocketContainerItem()
        {
            if (_openEquipmentField == null || _itemInfoItemDataProperty == null)
            {
                return null;
            }

            try
            {
                object? itemInfo = _openEquipmentField.GetValue(null);
                return itemInfo != null ? _itemInfoItemDataProperty.GetValue(itemInfo) as ItemDrop.ItemData : null;
            }
            catch
            {
                return null;
            }
        }

        private object? GetItemContainer(ItemDrop.ItemData item)
        {
            if (_itemDataMethod == null || _itemInfoGetItemContainerMethod == null || item?.m_shared == null)
            {
                return null;
            }

            try
            {
                object? itemInfo = _itemDataMethod.Invoke(null, new object[] { item });
                return itemInfo != null ? InvokeItemInfoGet(_itemInfoGetItemContainerMethod, itemInfo) : null;
            }
            catch
            {
                return null;
            }
        }

        private static object? InvokeItemInfoGet(MethodInfo method, object itemInfo)
        {
            ParameterInfo[] parameters = method.GetParameters();
            return parameters.Length == 0
                ? method.Invoke(itemInfo, Array.Empty<object>())
                : method.Invoke(itemInfo, new object[] { "" });
        }

        private bool IsItemBag(object itemContainer) =>
            _itemBagType != null && _itemBagType.IsInstanceOfType(itemContainer);

        private static string GetConfigEntryValueName(FieldInfo? field)
        {
            if (field == null)
            {
                return "";
            }

            try
            {
                object? configEntry = field.GetValue(null);
                object? value = configEntry?.GetType().GetProperty("Value", BindingFlags.Public | BindingFlags.Instance)?.GetValue(configEntry);
                return value?.ToString() ?? "";
            }
            catch
            {
                return "";
            }
        }

        public Inventory? GetOpenSocketContainerInventory()
        {
            if (_openInventoryField == null)
            {
                return null;
            }

            try
            {
                return _openInventoryField.GetValue(null) as Inventory;
            }
            catch
            {
                return null;
            }
        }

        public bool TryGetGemIconData(object gem, out Sprite? sprite, out string prefabName)
        {
            sprite = null;
            prefabName = "";
            if (gem == null)
            {
                return false;
            }

            Type type = gem.GetType();
            if (!_gemSpriteMembers.TryGetValue(type, out MemberInfo? spriteMember))
            {
                spriteMember = FindGemMember(type, typeof(Sprite), "gemSprite", "GemSprite", "sprite", "Sprite", "icon", "Icon", "gemIcon", "GemIcon");
                _gemSpriteMembers[type] = spriteMember;
            }

            if (!_gemPrefabMembers.TryGetValue(type, out MemberInfo? prefabMember))
            {
                prefabMember = FindGemMember(type, null, "gemPrefab", "GemPrefab", "prefab", "Prefab", "prefabName", "PrefabName", "name", "Name");
                _gemPrefabMembers[type] = prefabMember;
            }

            sprite = GetGemMemberValue(spriteMember, gem) as Sprite;
            object? prefabValue = GetGemMemberValue(prefabMember, gem);
            prefabName = prefabValue switch
            {
                string value => value,
                GameObject gameObject when gameObject != null && !IsUnityNull(gameObject) => gameObject.name,
                ItemDrop itemDrop when itemDrop != null && !IsUnityNull(itemDrop) => itemDrop.gameObject.name,
                _ => prefabValue?.ToString() ?? ""
            };
            return sprite != null || !string.IsNullOrWhiteSpace(prefabName);
        }

        public List<JewelcraftingSocketGemData> GetSocketGemData(ItemDrop.ItemData item)
        {
            List<JewelcraftingSocketGemData> sockets = new();
            if (_itemDataMethod == null ||
                _itemInfoGetSocketableMethod == null ||
                _socketedGemsField == null ||
                _socketNameField == null)
            {
                return sockets;
            }

            try
            {
                object? itemInfo = _itemDataMethod.Invoke(null, new object[] { item });
                object? socketable = itemInfo != null ? _itemInfoGetSocketableMethod.Invoke(itemInfo, new object[] { "" }) : null;
                if (socketable == null || _socketedGemsField.GetValue(socketable) is not IEnumerable socketItems)
                {
                    return sockets;
                }

                foreach (object? socket in socketItems)
                {
                    if (socket == null)
                    {
                        sockets.Add(new JewelcraftingSocketGemData("", null));
                        continue;
                    }

                    string prefabName = _socketNameField.GetValue(socket) as string ?? "";
                    Dictionary<string, uint>? seeds = CopySocketSeeds(_socketSeedField?.GetValue(socket));
                    sockets.Add(new JewelcraftingSocketGemData(prefabName, seeds));
                }
            }
            catch (Exception)
            {
            }

            return sockets;
        }

        public List<JewelcraftingSocketGemData> GetOpenSocketGemData(ItemDrop.ItemData item)
        {
            List<JewelcraftingSocketGemData> sockets = new();
            ItemDrop.ItemData? openItem = GetOpenSocketContainerItem();
            Inventory? inventory = GetOpenSocketContainerInventory();
            if (openItem?.m_shared == null || inventory == null || !IsSameOpenSocketContainerItem(item, openItem))
            {
                return sockets;
            }

            int width = Math.Max(1, inventory.m_width);
            int size = Math.Max(0, width * Math.Max(0, inventory.m_height));
            for (int i = 0; i < size; i++)
            {
                sockets.Add(new JewelcraftingSocketGemData("", null));
            }

            foreach (ItemDrop.ItemData? gem in inventory.m_inventory)
            {
                if (gem?.m_shared == null)
                {
                    continue;
                }

                int index = gem.m_gridPos.x + gem.m_gridPos.y * width;
                if (index < 0 || index >= sockets.Count)
                {
                    continue;
                }

                string prefabName = gem.m_dropPrefab != null && !IsUnityNull(gem.m_dropPrefab)
                    ? gem.m_dropPrefab.name
                    : GetItemPrefabName(gem);
                sockets[index] = new JewelcraftingSocketGemData(prefabName, GetSocketSeedData(gem));
            }

            return sockets;
        }

        private static Dictionary<string, uint>? CopySocketSeeds(object? seedObject)
        {
            if (seedObject is not IDictionary dictionary || dictionary.Count == 0)
            {
                return null;
            }

            Dictionary<string, uint> seeds = new();
            foreach (DictionaryEntry entry in dictionary)
            {
                string key = entry.Key?.ToString() ?? "";
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                if (entry.Value is uint uintValue)
                {
                    seeds[key] = uintValue;
                }
                else if (entry.Value != null && uint.TryParse(entry.Value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out uint parsedValue))
                {
                    seeds[key] = parsedValue;
                }
            }

            return seeds.Count > 0 ? seeds : null;
        }

        private Dictionary<string, uint>? GetSocketSeedData(ItemDrop.ItemData gem)
        {
            if (_itemDataMethod == null ||
                _itemInfoGetAllSocketSeedsMethod == null ||
                _socketSeedSeedProperty == null)
            {
                return null;
            }

            try
            {
                object? itemInfo = _itemDataMethod.Invoke(null, new object[] { gem });
                object? result = itemInfo != null ? _itemInfoGetAllSocketSeedsMethod.Invoke(itemInfo, null) : null;
                if (result is not IDictionary seedsByKey || seedsByKey.Count == 0)
                {
                    return null;
                }

                Dictionary<string, uint> seeds = new();
                foreach (DictionaryEntry entry in seedsByKey)
                {
                    string key = entry.Key?.ToString() ?? "";
                    if (string.IsNullOrWhiteSpace(key) || entry.Value == null)
                    {
                        continue;
                    }

                    object? value = _socketSeedSeedProperty.GetValue(entry.Value);
                    if (value is uint uintValue)
                    {
                        seeds[key] = uintValue;
                    }
                    else if (value != null && uint.TryParse(value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out uint parsed))
                    {
                        seeds[key] = parsed;
                    }
                }

                return seeds.Count > 0 ? seeds : null;
            }
            catch
            {
                return null;
            }
        }

        private static bool IsSameOpenSocketContainerItem(ItemDrop.ItemData item, ItemDrop.ItemData openItem)
        {
            if (ReferenceEquals(item, openItem))
            {
                return true;
            }

            return item.m_shared != null &&
                   openItem.m_shared != null &&
                   string.Equals(GetItemPrefabName(item), GetItemPrefabName(openItem), StringComparison.Ordinal) &&
                   string.Equals(item.m_shared.m_name, openItem.m_shared.m_name, StringComparison.Ordinal) &&
                   item.m_variant == openItem.m_variant &&
                   item.m_quality == openItem.m_quality &&
                   item.m_gridPos.x == openItem.m_gridPos.x &&
                   item.m_gridPos.y == openItem.m_gridPos.y;
        }

        private static MemberInfo? FindGemMember(Type type, Type? targetType, params string[] names)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.IgnoreCase;
            foreach (string name in names)
            {
                FieldInfo? field = type.GetField(name, flags);
                if (field != null && (targetType == null || targetType.IsAssignableFrom(field.FieldType) || targetType == typeof(IDictionary) && typeof(IDictionary).IsAssignableFrom(field.FieldType)))
                {
                    return field;
                }

                PropertyInfo? property = type.GetProperty(name, flags);
                if (property?.GetIndexParameters().Length == 0 &&
                    (targetType == null || targetType.IsAssignableFrom(property.PropertyType) || targetType == typeof(IDictionary) && typeof(IDictionary).IsAssignableFrom(property.PropertyType)))
                {
                    return property;
                }
            }

            foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (targetType != null && (targetType.IsAssignableFrom(field.FieldType) || targetType == typeof(IDictionary) && typeof(IDictionary).IsAssignableFrom(field.FieldType)))
                {
                    return field;
                }
            }

            return null;
        }

        private static object? GetGemMemberValue(MemberInfo? member, object instance)
        {
            try
            {
                return member switch
                {
                    FieldInfo field => field.GetValue(instance),
                    PropertyInfo { CanRead: true } property when property.GetIndexParameters().Length == 0 => property.GetValue(instance),
                    _ => null
                };
            }
            catch
            {
                return null;
            }
        }
    }
}
