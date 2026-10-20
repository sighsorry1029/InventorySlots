using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using ItemData = ItemDrop.ItemData;

internal static partial class Program
{
    private static int hudFoodCount, hudPotionCount, hudArrowCount, hudUseCalls, hudEquipCalls, hudCapacityReads;
    private static bool hudPermanent, hudEquipResult;
    private static ItemData? hudUsedItem, hudAmmo;
    private static Inventory? hudUsedInventory;
    private static bool hudFromGui;
    private static bool HudFoodCount(ref int __result) { hudCapacityReads++; __result = hudFoodCount; return false; }
    private static bool HudPotionCount(ref int __result) { hudCapacityReads++; __result = hudPotionCount; return false; }
    private static bool HudArrowCount(ref int __result) { hudCapacityReads++; __result = hudArrowCount; return false; }
    private static bool HudPermanent(ref bool __result) { __result = hudPermanent; return false; }
    private static bool HudUse(Inventory inventory, ItemData item, bool fromInventoryGui)
    { hudUseCalls++; hudUsedInventory = inventory; hudUsedItem = item; hudFromGui = fromInventoryGui; return false; }
    private static void SetHudAmmo(Humanoid player, ItemData? item)
    { hudAmmo = item; AccessTools.Field(typeof(Humanoid), "m_ammoItem").SetValue(player, item); }
    private static bool HudEquip(Humanoid __instance, ItemData item, ref bool __result)
    { hudEquipCalls++; __result = hudEquipResult; if (__result) SetHudAmmo(__instance, item); return false; }
    private static object? hudLocalization;
    private static bool HudLocalization(ref object? __result) { __result = hudLocalization; return false; }
    private static bool HudLocalize(string text, ref string __result) { __result = text; return false; }

    private static void CheckHud(Assembly mod, Assembly pov, Type plugin, Player player, Inventory bag, ConfigFile config)
    {
        object? Call(string name, params object?[] args) => plugin.GetMethod(name, Statics)!.Invoke(null, args);
        var slots = (IDictionary)plugin.GetField("PovSlots", Statics)!.GetValue(null)!;
        var items = (List<ItemData>)AccessTools.Field(typeof(Inventory), "m_inventory").GetValue(bag);
        var definitions = (IList)plugin.GetProperty("SlotDefinitions", Statics)!.GetValue(null)!;
        object[] savedDefinitions = definitions.Cast<object>().ToArray();
        object visibility = plugin.GetField("QuickHudVisibility", Statics)!.GetValue(null)!;
        Type visibilityType = visibility.GetType();
        void Clear() => visibilityType.GetMethod("Clear")!.Invoke(visibility, null);
        void Sync(bool force = false) => Call("SyncQuickSlotHudVisibility", player, force);
        IList Ids() => (IList)plugin.GetField("QuickHudSlotIds", Statics)!.GetValue(null)!;
        float Alpha(string id) => (float)visibilityType.GetMethod("GetSlotAlpha")!.Invoke(visibility, new object[] { Ids().IndexOf(id), 1f })!;
        Type mode = mod.GetType("InventorySlots.QuickSlotHudMode", true)!;
        MethodInfo bind = typeof(ConfigFile).GetMethods().Single(m => m.Name == "Bind" && m.IsGenericMethod &&
            m.GetParameters().Length == 4 && m.GetParameters()[0].ParameterType == typeof(string) && m.GetParameters()[3].ParameterType == typeof(string));
        var entry = (ConfigEntryBase)bind.MakeGenericMethod(mode).Invoke(config, new object[] { "test", "hud-mode", Enum.Parse(mode, "UsedSlotOnUse"), "" })!;
        FieldInfo modeField = plugin.GetField("_quickSlotHudMode", Statics)!;
        object? oldMode = modeField.GetValue(null);
        Type state = pov.GetType("PassiveTree.Gear.State", true)!;
        var hooks = new Harmony("smoke.pov.hud.engine-boundaries");
        ItemData Item(string cell, int type, float food = 0)
        {
            object?[] args = { bag, slots[cell], null }; Call("TryGetSlotGridPos", args);
            var item = new ItemData { m_gridPos = (Vector2i)args[2]!, m_stack = 5,
                m_shared = new ItemData.SharedData { m_name = cell, m_itemType = (ItemData.ItemType)type, m_food = food } };
            items.Add(item); return item;
        }
        // Only replace world/engine boundaries and capacity grants. State's
        // real Eat/Drink/CycleArrows, provider callbacks and IS hooks execute.
        hooks.Patch(AccessTools.Method(state, "FoodCount"), prefix: new HarmonyMethod(typeof(Program), nameof(HudFoodCount)));
        hooks.Patch(AccessTools.Method(state, "PotionCount"), prefix: new HarmonyMethod(typeof(Program), nameof(HudPotionCount)));
        hooks.Patch(AccessTools.Method(state, "ArrowCount"), prefix: new HarmonyMethod(typeof(Program), nameof(HudArrowCount)));
        MethodInfo? permanent = null;
        foreach (var instruction in PatchProcessor.GetOriginalInstructions(AccessTools.Method(state, "Drink")))
            if (instruction.operand is MethodInfo method && method.Name == "Permanent") permanent = method;
        if (permanent != null)
            hooks.Patch(permanent, prefix: new HarmonyMethod(typeof(Program), nameof(HudPermanent)));
        else Check(pov.GetType("Valheiman.Api.Equipment")!.GetMethod("IsRing") != null,
            "5.1 Drink no longer has the old permanent-potion guard");
        hooks.Patch(AccessTools.Method(typeof(Humanoid), "UseItem", new[] { typeof(Inventory), typeof(ItemData), typeof(bool) }), prefix: new HarmonyMethod(typeof(Program), nameof(HudUse)));
        hooks.Patch(AccessTools.Method(typeof(Humanoid), "EquipItem"), prefix: new HarmonyMethod(typeof(Program), nameof(HudEquip)));
        Type localization = Assembly.Load("assembly_guiutils").GetType("Localization", true)!;
        hudLocalization = FormatterServices.GetUninitializedObject(localization);
        hooks.Patch(AccessTools.PropertyGetter(localization, "instance"), prefix: new HarmonyMethod(typeof(Program), nameof(HudLocalization)));
        hooks.Patch(AccessTools.Method(localization, "Localize", new[] { typeof(string) }), prefix: new HarmonyMethod(typeof(Program), nameof(HudLocalize)));
        hooks.Patch(AccessTools.DeclaredMethod(typeof(Character), "Message"), prefix: new HarmonyMethod(typeof(Program), nameof(NoProgress)));
        try
        {
            modeField.SetValue(null, entry);
            items.Clear(); hudFoodCount = hudPotionCount = hudArrowCount = 3;
            hudUseCalls = hudEquipCalls = 0; hudPermanent = false; hudEquipResult = true; SetHudAmmo(player, null);
            Check((bool)plugin.GetField("_povHudBound", Statics)!.GetValue(null)!, "original PoV action bindings install independently of native HUD fallback");
            Sync(true);
            Check(Ids().Count == 12 && (int)plugin.GetField("_quickHudColumns", Statics)!.GetValue(null)! == 6 &&
                (int)plugin.GetField("_quickHudRows", Statics)!.GetValue(null)! == 3, "combined HUD has three ordinary and nine real PoV cells in two banks");
            int capacityReads = hudCapacityReads;
            Sync(); Sync();
            Check(hudCapacityReads == capacityReads, "unchanged hidden HUD reuses its layout without querying native capacities");
            string[] fallbackFields = { "_povEquipmentHost", "_povCellHudTick", "_povCellHudHide", "_povUnifiedHudPlayer" };
            object?[] oldFallback = fallbackFields.Select(f => plugin.GetField(f, Statics)!.GetValue(null)).ToArray();
            object panels = plugin.GetField("InventoryPanels", Statics)!.GetValue(null)!;
            FieldInfo rootField = panels.GetType().GetField("QuickSlotsHotkeyBarRect")!;
            object? oldRoot = rootField.GetValue(panels);
            int ticks = 0, hides = 0;
            try
            {
                plugin.GetField("_povEquipmentHost", Statics)!.SetValue(null, new Func<string>(() => "sighsorry.InventorySlots"));
                plugin.GetField("_povCellHudTick", Statics)!.SetValue(null, new Action<Player>(_ => ticks++));
                plugin.GetField("_povCellHudHide", Statics)!.SetValue(null, new Action(() => hides++));
                plugin.GetField("_povUnifiedHudPlayer", Statics)!.SetValue(null, null);
                rootField.SetValue(panels, null); Call("UpdatePathOfValheimanCellHud");
                Check(ticks == 1 && hides == 0, "native HUD remains available before unified renderer is ready");
                plugin.GetField("_povUnifiedHudPlayer", Statics)!.SetValue(null, player);
                rootField.SetValue(panels, FormatterServices.GetUninitializedObject(typeof(RectTransform)));
                Call("UpdatePathOfValheimanCellHud");
                Check(ticks == 1 && hides == 1, "ready unified renderer suppresses duplicate native HUD");
                plugin.GetField("_povHudBound", Statics)!.SetValue(null, false); Call("UpdatePathOfValheimanCellHud");
                Check(ticks == 2 && hides == 1, "unavailable action integration keeps native display fallback");
                plugin.GetField("_povHudBound", Statics)!.SetValue(null, true);
                rootField.SetValue(panels, null); Call("UpdatePathOfValheimanCellHud");
                Check(ticks == 3 && hides == 1, "lost unified root restores native display fallback");
            }
            finally
            {
                for (int i = 0; i < fallbackFields.Length; i++) plugin.GetField(fallbackFields[i], Statics)!.SetValue(null, oldFallback[i]);
                plugin.GetField("_povHudBound", Statics)!.SetValue(null, true); rootField.SetValue(panels, oldRoot);
            }
            ItemData food = Item("food.0", 2, 20);
            ItemData potion = Item("potion.0", 2);
            potion.m_shared.m_consumeStatusEffect = (StatusEffect)FormatterServices.GetUninitializedObject(typeof(StatusEffect));
            ItemData arrow0 = Item("arrow.0", 9), arrow2 = Item("arrow.2", 9);
            arrow0.m_shared.m_ammoType = arrow2.m_shared.m_ammoType = "$ammo_arrows";
            Clear(); Call("UsePathOfValheimanHudSlot", player, slots["food.0"]);
            Check(hudUseCalls == 1 && ReferenceEquals(hudUsedItem, food) && ReferenceEquals(hudUsedInventory, bag) && hudFromGui,
                "HUD food dispatch calls original PoV Eat then one game UseItem with original arguments");
            Check(Alpha("pov.food.0") == 1 && Alpha("pov.potion.0") == 0, "native food use reveals only its cell in UsedSlotOnUse");
            Call("InvalidateSlotDefinitionCaches"); Sync();
            Check(Alpha("pov.food.0") == 1, "inventory notification after consumption preserves same-cell reveal");
            Clear(); hudPermanent = true;
            if (permanent != null)
            {
                Call("UsePathOfValheimanHudSlot", player, slots["potion.0"]);
                Check(hudUseCalls == 1 && Alpha("pov.potion.0") == 0, "original permanent potion rule neither consumes nor reveals a false use");
            }
            hudPermanent = false;
            state.GetMethod("Drink", Statics)!.Invoke(null, new object[] { player, 0 });
            Check(hudUseCalls == 2 && ReferenceEquals(hudUsedItem, potion) && Alpha("pov.potion.0") == 1,
                "native PoV hotkey path also reveals potion without a second consume");
            Clear(); SetHudAmmo(player, arrow0);
            Call("UsePathOfValheimanHudSlot", player, slots["arrow.0"]);
            Check(hudEquipCalls == 1 && ReferenceEquals(hudAmmo, arrow2) && Alpha("pov.arrow.2") == 1 && Alpha("pov.arrow.0") == 0,
                $"HUD arrow click runs original cycle, skips empty middle cell and reveals selected ammo (equips={hudEquipCalls}, ammo={hudAmmo?.m_shared.m_name}, alpha2={Alpha("pov.arrow.2")}, alpha0={Alpha("pov.arrow.0")})");
            state.GetMethod("CycleArrows", Statics)!.Invoke(null, new object[] { player });
            Check(hudEquipCalls == 2 && ReferenceEquals(hudAmmo, arrow0), "native arrow hotkey wraps to first stack");
            hudEquipResult = false;
            state.GetMethod("CycleArrows", Statics)!.Invoke(null, new object[] { player });
            Check(hudEquipCalls == 3 && ReferenceEquals(hudAmmo, arrow0), "failed native ammo equip preserves previous selection without retry");
            hudFoodCount = 0; Call("UsePathOfValheimanHudSlot", player, slots["food.0"]);
            Check(hudUseCalls == 2, "closed occupied food slot cannot be used from HUD");
            AccessTools.Field(typeof(Player), "m_isLoading").SetValue(player, true);
            Call("UsePathOfValheimanHudSlot", player, slots["potion.0"]);
            Check(hudUseCalls == 2, "HUD actions respect player load guard");
            AccessTools.Field(typeof(Player), "m_isLoading").SetValue(player, false);
            entry.BoxedValue = Enum.Parse(mode, "AllOnUse"); Sync();
            state.GetMethod("Drink", Statics)!.Invoke(null, new object[] { player, 0 });
            Check(Ids().Cast<string>().All(id => Alpha(id) == 1), "PoV use reveals both HUD banks in AllOnUse");
            entry.BoxedValue = Enum.Parse(mode, "Off"); Sync();
            state.GetMethod("Drink", Statics)!.Invoke(null, new object[] { player, 0 });
            Check(Ids().Cast<string>().All(id => Alpha(id) == 0), "Off hides combined HUD without blocking native use");
            entry.BoxedValue = Enum.Parse(mode, "On"); Sync();
            Check(Ids().Cast<string>().All(id => Alpha(id) == 1), "On shows both banks without a use notification");
            // No generic bank: the only open native cell must start in column 0.
            foreach (object slot in savedDefinitions.Where(s => s.GetType().GetProperty("PovCell")!.GetValue(s) == null).ToArray()) definitions.Remove(slot);
            items.Clear(); hudFoodCount = hudPotionCount = 0; hudArrowCount = 1;
            Call("InvalidateSlotDefinitionCaches"); Sync();
            Check(Ids().Count == 1 && (string)Ids()[0]! == "pov.arrow.0" &&
                (int)plugin.GetField("_quickHudColumns", Statics)!.GetValue(null)! == 1, "PoV-only HUD has one cell and no blank ordinary bank");
        }
        finally
        {
            hooks.UnpatchSelf(); modeField.SetValue(null, oldMode); Clear();
            AccessTools.Field(typeof(Player), "m_isLoading").SetValue(player, false);
            definitions.Clear(); foreach (object definition in savedDefinitions) definitions.Add(definition);
            Call("InvalidateSlotDefinitionCaches");
            SetHudAmmo(player, null); hudUsedItem = null; hudUsedInventory = null; hudLocalization = null;
        }
    }
}
