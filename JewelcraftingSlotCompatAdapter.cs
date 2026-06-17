using System;
using System.Reflection;
using BepInEx.Configuration;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private sealed class JewelcraftingSlotApi
    {
        private readonly FieldInfo _ringSlotField;
        private readonly FieldInfo _necklaceSlotField;
        private readonly FieldInfo? _wisplightGemField;
        private readonly FieldInfo? _wishboneGemField;
        private readonly MethodInfo? _isJewelryEquippedMethod;
        private ConfigEntryBase? _ringSlotConfig;
        private ConfigEntryBase? _necklaceSlotConfig;
        private ConfigEntryBase? _wisplightGemConfig;
        private ConfigEntryBase? _wishboneGemConfig;

        private JewelcraftingSlotApi(FieldInfo ringSlotField, FieldInfo necklaceSlotField, FieldInfo? wisplightGemField, FieldInfo? wishboneGemField, MethodInfo? isJewelryEquippedMethod)
        {
            _ringSlotField = ringSlotField;
            _necklaceSlotField = necklaceSlotField;
            _wisplightGemField = wisplightGemField;
            _wishboneGemField = wishboneGemField;
            _isJewelryEquippedMethod = isJewelryEquippedMethod;
        }

        public static bool TryCreate(Assembly assembly, out JewelcraftingSlotApi? api, out string detail)
        {
            api = null;
            Type? pluginType = assembly.GetType("Jewelcrafting.Jewelcrafting");
            Type? apiType = assembly.GetType("Jewelcrafting.API");
            FieldInfo? ringSlotField = pluginType?.GetField("ringSlot", BindingFlags.Public | BindingFlags.Static);
            FieldInfo? necklaceSlotField = pluginType?.GetField("necklaceSlot", BindingFlags.Public | BindingFlags.Static);
            FieldInfo? wisplightGemField = pluginType?.GetField("wisplightGem", BindingFlags.Public | BindingFlags.Static);
            FieldInfo? wishboneGemField = pluginType?.GetField("wishboneGem", BindingFlags.Public | BindingFlags.Static);
            if (ringSlotField == null || necklaceSlotField == null)
            {
                detail = "ringSlot or necklaceSlot config field was not found";
                return false;
            }

            MethodInfo? isJewelryEquippedMethod = apiType?.GetMethod(
                "IsJewelryEquipped",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(Player), typeof(string) },
                null);
            api = new JewelcraftingSlotApi(ringSlotField, necklaceSlotField, wisplightGemField, wishboneGemField, isJewelryEquippedMethod);
            detail = "";
            return true;
        }

        public bool IsRingEnabled() => GetCompatConfigEntryToggleOn(_ringSlotField, ref _ringSlotConfig);

        public bool IsNecklaceEnabled() => GetCompatConfigEntryToggleOn(_necklaceSlotField, ref _necklaceSlotConfig);

        public bool IsWisplightGemEnabled() => _wisplightGemField != null && GetCompatConfigEntryToggleOn(_wisplightGemField, ref _wisplightGemConfig);

        public bool IsWishboneGemEnabled() => _wishboneGemField != null && GetCompatConfigEntryToggleOn(_wishboneGemField, ref _wishboneGemConfig);

        public bool TryGetIsJewelryEquippedMethod(out MethodBase? method)
        {
            method = _isJewelryEquippedMethod;
            return method != null;
        }
    }
}
