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
        private readonly FieldInfo? _socketedGemsField;
        private readonly FieldInfo? _socketNameField;
        private readonly FieldInfo? _socketSeedField;
        private readonly FieldInfo? _effectPowersField;
        private readonly FieldInfo? _effectNamesField;
        private readonly MethodInfo? _getGemLocationMethod;
        private readonly MethodInfo? _getItemGemLocationMethod;
        private readonly MethodInfo? _displayGemEffectPowerMethod;
        private readonly FieldInfo? _openEquipmentField;
        private readonly FieldInfo? _openInventoryField;
        private readonly PropertyInfo? _itemInfoItemDataProperty;
        private readonly MethodInfo? _itemInfoGetAllSocketSeedsMethod;
        private readonly PropertyInfo? _socketSeedSeedProperty;
        private readonly Dictionary<Type, MemberInfo?> _gemSpriteMembers = new();
        private readonly Dictionary<Type, MemberInfo?> _gemPrefabMembers = new();
        private readonly Dictionary<Type, MemberInfo?> _gemEffectsMembers = new();

        private JewelcraftingGemApi(
            MethodInfo getGemsMethod,
            MethodInfo? itemDataMethod,
            MethodInfo? itemInfoGetSocketableMethod,
            FieldInfo? socketedGemsField,
            FieldInfo? socketNameField,
            FieldInfo? socketSeedField,
            FieldInfo? effectPowersField,
            FieldInfo? effectNamesField,
            MethodInfo? getGemLocationMethod,
            MethodInfo? getItemGemLocationMethod,
            MethodInfo? displayGemEffectPowerMethod,
            FieldInfo? openEquipmentField,
            FieldInfo? openInventoryField,
            PropertyInfo? itemInfoItemDataProperty,
            MethodInfo? itemInfoGetAllSocketSeedsMethod,
            PropertyInfo? socketSeedSeedProperty)
        {
            _getGemsMethod = getGemsMethod;
            _itemDataMethod = itemDataMethod;
            _itemInfoGetSocketableMethod = itemInfoGetSocketableMethod;
            _socketedGemsField = socketedGemsField;
            _socketNameField = socketNameField;
            _socketSeedField = socketSeedField;
            _effectPowersField = effectPowersField;
            _effectNamesField = effectNamesField;
            _getGemLocationMethod = getGemLocationMethod;
            _getItemGemLocationMethod = getItemGemLocationMethod;
            _displayGemEffectPowerMethod = displayGemEffectPowerMethod;
            _openEquipmentField = openEquipmentField;
            _openInventoryField = openInventoryField;
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
            Type? socketItemType = assembly.GetType("Jewelcrafting.SocketItem");
            Type? jewelcraftingType = assembly.GetType("Jewelcrafting.Jewelcrafting");
            Type? effectDefType = assembly.GetType("Jewelcrafting.GemEffects.EffectDef");
            Type? utilsType = assembly.GetType("Jewelcrafting.Utils");
            Type? effectPowerType = assembly.GetType("Jewelcrafting.GemEffects.EffectPower");
            Type? addFakeSocketsContainerType = assembly.GetType("Jewelcrafting.GemStones+AddFakeSocketsContainer");
            Type? socketSeedType = assembly.GetType("Jewelcrafting.SocketSeed");

            MethodInfo? itemDataMethod = itemExtensionsType?.GetMethod(
                "Data",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(ItemDrop.ItemData) },
                null);
            MethodInfo? itemInfoGetAllSocketSeedsMethod = null;
            MethodInfo? itemInfoGetMethod = null;
            if (itemInfoType != null)
            {
                foreach (MethodInfo method in itemInfoType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (socketableType != null && method.Name == "Get" && method.IsGenericMethodDefinition && method.GetParameters().Length == 1)
                    {
                        itemInfoGetMethod = method.MakeGenericMethod(socketableType);
                    }
                    else if (socketSeedType != null && method.Name == "GetAll" && method.IsGenericMethodDefinition && method.GetParameters().Length == 0)
                    {
                        itemInfoGetAllSocketSeedsMethod = method.MakeGenericMethod(socketSeedType);
                    }
                }
            }

            MethodInfo? displayGemEffectPowerMethod = null;
            if (utilsType != null && effectPowerType != null)
            {
                foreach (MethodInfo method in utilsType.GetMethods(BindingFlags.Public | BindingFlags.Static))
                {
                    if (method.Name == "DisplayGemEffectPower" && method.GetParameters().Length == 5)
                    {
                        displayGemEffectPowerMethod = method;
                        break;
                    }
                }
            }

            api = new JewelcraftingGemApi(
                getGemsMethod,
                itemDataMethod,
                itemInfoGetMethod,
                socketableType?.GetField("socketedGems", BindingFlags.Public | BindingFlags.Instance),
                socketItemType?.GetField("Name", BindingFlags.Public | BindingFlags.Instance),
                socketItemType?.GetField("Seed", BindingFlags.Public | BindingFlags.Instance),
                jewelcraftingType?.GetField("EffectPowers", BindingFlags.Public | BindingFlags.Static),
                effectDefType?.GetField("EffectNames", BindingFlags.Public | BindingFlags.Static),
                utilsType?.GetMethod("GetGemLocation", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(ItemDrop.ItemData.SharedData), typeof(Player) }, null),
                utilsType?.GetMethod("GetItemGemLocation", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(ItemDrop.ItemData) }, null),
                displayGemEffectPowerMethod,
                addFakeSocketsContainerType?.GetField("openEquipment", BindingFlags.Public | BindingFlags.Static),
                addFakeSocketsContainerType?.GetField("openInventory", BindingFlags.Public | BindingFlags.Static),
                itemInfoType?.GetProperty("ItemData", BindingFlags.Public | BindingFlags.Instance),
                itemInfoGetAllSocketSeedsMethod,
                socketSeedType?.GetProperty("Seed", BindingFlags.Public | BindingFlags.Instance));
            detail = "";
            return true;
        }

        public object? GetGems(ItemDrop.ItemData item) => _getGemsMethod.Invoke(null, new object[] { item });

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

        public bool TryGetGemTooltipData(object gem, out Sprite? sprite, out string prefabName, out List<KeyValuePair<string, float>> effects)
        {
            effects = new List<KeyValuePair<string, float>>();
            if (!TryGetGemIconData(gem, out sprite, out prefabName) && string.IsNullOrWhiteSpace(prefabName))
            {
                return false;
            }

            Type type = gem.GetType();
            if (!_gemEffectsMembers.TryGetValue(type, out MemberInfo? effectsMember))
            {
                effectsMember = FindGemMember(type, typeof(IDictionary), "gemEffects", "GemEffects", "effects", "Effects");
                _gemEffectsMembers[type] = effectsMember;
            }

            if (GetGemMemberValue(effectsMember, gem) is not IDictionary effectDictionary)
            {
                return true;
            }

            foreach (DictionaryEntry entry in effectDictionary)
            {
                string name = entry.Key?.ToString() ?? "";
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                if (entry.Value is float floatValue)
                {
                    effects.Add(new KeyValuePair<string, float>(name, floatValue));
                }
                else if (entry.Value is IConvertible convertible)
                {
                    try
                    {
                        effects.Add(new KeyValuePair<string, float>(name, convertible.ToSingle(CultureInfo.InvariantCulture)));
                    }
                    catch
                    {
                        // ignored
                    }
                }
                else if (entry.Value != null && float.TryParse(entry.Value.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out float parsedValue))
                {
                    effects.Add(new KeyValuePair<string, float>(name, parsedValue));
                }
            }

            return true;
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
            catch (Exception ex)
            {
                Log.LogDebug($"Jewelcrafting direct socket read failed for {GetItemPrefabName(item)}: {ex.Message}");
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

        public string GetSocketDebugSummary(ItemDrop.ItemData item)
        {
            List<JewelcraftingSocketGemData> sockets = GetSocketGemData(item);
            List<string> names = new();
            foreach (JewelcraftingSocketGemData socket in sockets)
            {
                names.Add(string.IsNullOrWhiteSpace(socket.PrefabName) ? "<empty>" : socket.PrefabName);
            }

            return "directSockets=" + sockets.Count + ", directNames=" + string.Join("/", names);
        }

        public string GetOpenSocketDebugSummary(ItemDrop.ItemData item)
        {
            List<JewelcraftingSocketGemData> sockets = GetOpenSocketGemData(item);
            List<string> names = new();
            foreach (JewelcraftingSocketGemData socket in sockets)
            {
                names.Add(string.IsNullOrWhiteSpace(socket.PrefabName) ? "<empty>" : socket.PrefabName);
            }

            return "openSockets=" + sockets.Count + ", openNames=" + string.Join("/", names);
        }

        public string BuildSocketEffectText(ItemDrop.ItemData item, JewelcraftingSocketGemData socket)
        {
            if (!socket.HasGem ||
                _effectPowersField == null ||
                _effectNamesField == null ||
                _getGemLocationMethod == null ||
                _getItemGemLocationMethod == null ||
                _displayGemEffectPowerMethod == null ||
                _effectPowersField.GetValue(null) is not IDictionary effectPowers ||
                _effectNamesField.GetValue(null) is not IDictionary effectNames)
            {
                return "";
            }

            object? location = _getGemLocationMethod.Invoke(null, new object?[] { item.m_shared, Player.m_localPlayer });
            object? itemLocation = _getItemGemLocationMethod.Invoke(null, new object[] { item });
            List<object> powers = new();
            int socketHash = StringExtensionMethods.GetStableHashCode(socket.PrefabName);
            if (effectPowers.Contains(socketHash) && effectPowers[socketHash] is IDictionary locationPowers)
            {
                AddLocationPowers(locationPowers, location, powers);
                AddLocationPowers(locationPowers, itemLocation, powers);
            }

            if (powers.Count == 0)
            {
                return "$jc_effect_no_effect";
            }

            List<string> lines = new();
            foreach (object power in powers)
            {
                object? effect = power.GetType().GetField("Effect", BindingFlags.Public | BindingFlags.Instance)?.GetValue(power);
                string effectName = effect != null && effectNames.Contains(effect)
                    ? effectNames[effect]?.ToString() ?? effect.ToString() ?? ""
                    : effect?.ToString() ?? "";
                string value = "";
                try
                {
                    value = _displayGemEffectPowerMethod.Invoke(null, new object?[] { power, null, 0, socket.Seeds, false }) as string ?? "";
                }
                catch
                {
                    // ignored
                }

                if (!string.IsNullOrWhiteSpace(effectName))
                {
                    lines.Add("$jc_effect_" + effectName.ToLowerInvariant() + (string.IsNullOrWhiteSpace(value) ? "" : " " + value));
                }
            }

            return string.Join("\n", lines);
        }

        private static void AddLocationPowers(IDictionary locationPowers, object? location, List<object> powers)
        {
            if (location == null || !locationPowers.Contains(location) || locationPowers[location] is not IEnumerable list)
            {
                return;
            }

            foreach (object? power in list)
            {
                if (power != null)
                {
                    powers.Add(power);
                }
            }
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

        public string GetGemDebugSummary(object gem)
        {
            if (gem == null)
            {
                return "null";
            }

            Type type = gem.GetType();
            List<string> parts = new() { type.FullName ?? type.Name };
            foreach (MemberInfo member in type.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (member is not FieldInfo and not PropertyInfo)
                {
                    continue;
                }

                object? value = GetGemMemberValue(member, gem);
                string text = value switch
                {
                    null => "null",
                    Sprite sprite => "Sprite:" + sprite.name,
                    GameObject gameObject => "GameObject:" + gameObject.name,
                    IDictionary dictionary => "Dictionary:" + dictionary.Count,
                    _ => value.ToString() ?? ""
                };
                if (text.Length > 80)
                {
                    text = text.Substring(0, 80) + "...";
                }

                parts.Add(member.Name + "=" + text);
                if (parts.Count >= 8)
                {
                    break;
                }
            }

            return string.Join(";", parts);
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
