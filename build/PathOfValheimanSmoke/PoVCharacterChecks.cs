using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using HarmonyLib;
using ItemData = ItemDrop.ItemData;

internal static class PoVCharacterChecks
{
    private const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static int visuals;
    private static int currentLookReads;
    private static void CountCurrentLookRead() => currentLookReads++;
    private static bool VisualBoundary() { visuals++; return false; }
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void NativeDestroy(IntPtr target, float delay);
    private static int engineDestroyCalls;
    private static readonly NativeDestroy DestroyStub = (_, __) => engineDestroyCalls++;
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int NativeShaderId(IntPtr name);
    private static readonly NativeShaderId ShaderIdStub = _ => 0;
    [DllImport("mono-2.0-bdwgc.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern void mono_add_internal_call(string name, IntPtr method);
    private static bool Ready(Player player, ref bool __result) { __result = ReferenceEquals(player, Player.m_localPlayer); return false; }
    private static bool BaseReturns(out int __1, out int __2) { __1 = 12; __2 = 2; return false; }
    private static bool Share(string __0, ref float __result)
    { if (__0 != "disenchant.corrupted-share") return true; __result = 0.25f; return false; }

    internal static void Run(Type plugin, Assembly pov, Action<bool, string> check)
    {
        // Mono resolves this engine-only call while JITting native Dispose,
        // even though the fixture has no Unity root and must not destroy one.
        mono_add_internal_call("UnityEngine.Object::Destroy_Injected", Marshal.GetFunctionPointerForDelegate(DestroyStub));
        // VisEquipment's static shader IDs need the renderer, absent in this host.
        // No renderer body below is executed; only hooks/signatures are tested.
        mono_add_internal_call("UnityEngine.Shader::PropertyToID_Injected", Marshal.GetFunctionPointerForDelegate(ShaderIdStub));
        var boundary = new Harmony("smoke.pov.character.engine-boundaries");
        Player oldPlayer = Player.m_localPlayer;
        object? oldLooks = plugin.GetField("_povLooks", Static)!.GetValue(null);
        try
        {
            Type bridgeType = plugin.Assembly.GetType("InventorySlots.PathOfValheimanLooksBridge", true)!;
            object bridge = bridgeType.GetConstructor(Instance, null, new[] { typeof(Assembly) }, null)!.Invoke(new object[] { pov });
            Delegate Api(string name) => (Delegate)bridgeType.GetField(name, Instance)!.GetValue(bridge)!;
            check(!Api("CanChange").Method.IsPrivate && !(bool)Api("CanChange").DynamicInvoke(new object?[] { null })!, "Looks bridge binds public API and runs against original PoV");
            ItemData hidden = (ItemData)Api("Hidden").DynamicInvoke()!;
            check((bool)Api("IsOpen").DynamicInvoke(hidden)!, "Original HiddenLook sentinel is accepted by original Looks.IsOpen");
            check((string)Api("Apply").DynamicInvoke(null, new List<(ItemData, ItemData, bool)>())! == "", "Invalid-player empty result is not null success");
            plugin.GetField("_povLooks", Static)!.SetValue(null, bridge);

            // Unity rendering is the boundary. ApplyAll, inventory rune removal,
            // metadata writes, validation and reset below are the original bodies.
            boundary.Patch(AccessTools.Method(typeof(Humanoid), "SetupVisEquipment"), prefix: new HarmonyMethod(typeof(PoVCharacterChecks), nameof(VisualBoundary)));
            boundary.Patch(AccessTools.DeclaredMethod(typeof(Player), "SetupVisEquipment"), prefix: new HarmonyMethod(typeof(PoVCharacterChecks), nameof(VisualBoundary)));
            boundary.Patch(plugin.GetMethod("CanShowPathOfValheimanCharacter", Static), prefix: new HarmonyMethod(typeof(PoVCharacterChecks), nameof(Ready)));
            Player player = (Player)FormatterServices.GetUninitializedObject(typeof(Player));
            Player.m_localPlayer = player;
            Inventory inventory = new Inventory("Character smoke", null, 8, 4);
            AccessTools.Field(typeof(Humanoid), "m_inventory").SetValue(player, inventory);
            ItemData item = new ItemData { m_shared = new ItemData.SharedData(), m_gridPos = new Vector2i(0, 0) };
            item.m_shared.m_itemType = ItemDrop.ItemData.ItemType.Helmet;
            item.m_shared.m_name = "Smoke helmet";
            inventory.GetAllItems().Add(item);
            string runeName = (string)pov.GetType("PassiveTree.Gear.Transmog", true)!.GetProperty("RuneName", Static)!.GetValue(null)!;
            ItemData rune = new ItemData { m_shared = new ItemData.SharedData(), m_stack = 2, m_gridPos = new Vector2i(1, 0) };
            rune.m_shared.m_name = runeName; rune.m_shared.m_maxStackSize = 50;
            inventory.GetAllItems().Add(rune);
            var selection = new List<(ItemData, ItemData, bool)> { (item, hidden, true) };
            visuals = 0;
            check(Api("Apply").DynamicInvoke(player, selection) == null && inventory.CountItems(runeName) == 1 &&
                item.m_customData["valheiman.equipment.v1.look"] == "hidden" && visuals == 1,
                "Original Looks.Apply consumes exactly one rune, writes the look and refreshes visuals once");
            check((string)Api("Apply").DynamicInvoke(player, selection)! == "already" && inventory.CountItems(runeName) == 1 && visuals == 1,
                "Applying an identical look is refused without consumption");
            Api("Reset").DynamicInvoke(player, item);
            Api("Reset").DynamicInvoke(player, item);
            check(!item.m_customData.ContainsKey("valheiman.equipment.v1.look") && visuals == 2 && inventory.CountItems(runeName) == 1,
                "Original Reset is free and repeated Reset is a no-op");
            item.m_customData["valheiman.equipment.v1.look"] = "MissingPrefab:0";
            Api("Reset").DynamicInvoke(player, item);
            check(!item.m_customData.ContainsKey("valheiman.equipment.v1.look") && visuals == 3, "Reset removes stale look metadata too");
            selection[0] = (item, hidden, false);
            check((string)Api("Apply").DynamicInvoke(player, selection)! == "lockedTip" && inventory.CountItems(runeName) == 1 && visuals == 3,
                "Hotbar-style canHide=false cannot spend a rune to hide equipment");
            inventory.GetAllItems().Remove(rune);
            selection[0] = (item, hidden, true);
            check((string)Api("Apply").DynamicInvoke(player, selection)! == "noRune" && visuals == 3 && !item.m_customData.ContainsKey("valheiman.equipment.v1.look"),
                "Original insufficient-rune guard leaves appearance unchanged");

            Type viewType = plugin.GetNestedType("PovCharacterView", BindingFlags.NonPublic)!;
            object view = Activator.CreateInstance(viewType, true)!;
            viewType.GetField("Player", Instance)!.SetValue(view, player);
            viewType.GetField("Target", Instance)!.SetValue(view, item);
            viewType.GetField("TargetCell", Instance)!.SetValue(view, item.m_gridPos);
            bool Valid() => (bool)plugin.GetMethod("ValidatePathOfValheimanLookTarget", Static)!.Invoke(null, new[] { view })!;
            check(Valid() && !(bool)viewType.GetField("CanHide", Instance)!.GetValue(view)!, "Actual IS validator accepts owned hotbar reference but never permits hiding it");
            boundary.Patch(Api("Current").Method, prefix: new HarmonyMethod(typeof(PoVCharacterChecks), nameof(CountCurrentLookRead)));
            MethodInfo refreshCurrent = plugin.GetMethod("RefreshPathOfValheimanCurrentLook", Static)!;
            object? CurrentLook() => viewType.GetField("CurrentLook", Instance)!.GetValue(view);
            void RefreshCurrent() => refreshCurrent.Invoke(null, new[] { view });
            currentLookReads = 0;
            RefreshCurrent();
            check(ReferenceEquals(CurrentLook(), item) && currentLookReads == 1, "Applied-look cache resolves original appearance through public PoV API");
            for (int i = 0; i < 100; i++) RefreshCurrent();
            check(currentLookReads == 1, "Idle appearance refresh never repeats native Current lookup");
            item.m_customData["valheiman.equipment.v1.look"] = "hidden";
            RefreshCurrent();
            check(ReferenceEquals(CurrentLook(), hidden) && currentLookReads == 2, "Changed appearance metadata refreshes the applied look, including Hidden");
            item.m_customData.Remove("valheiman.equipment.v1.look");
            RefreshCurrent();
            check(ReferenceEquals(CurrentLook(), item) && currentLookReads == 3, "Appearance reset refreshes the original look");
            var otherTarget = item.Clone();
            viewType.GetField("Target", Instance)!.SetValue(view, otherTarget);
            RefreshCurrent();
            check(ReferenceEquals(CurrentLook(), otherTarget) && currentLookReads == 4, "Changing item reference invalidates applied look even with equal metadata");
            viewType.GetField("Target", Instance)!.SetValue(view, null);
            RefreshCurrent();
            check(CurrentLook() == null && currentLookReads == 4, "Clearing the target releases the cached appearance without querying PoV");
            viewType.GetField("Target", Instance)!.SetValue(view, item);
            inventory.GetAllItems().Remove(item);
            check(!Valid(), "Removed target is rejected before Apply or Reset");
            var replacement = item.Clone(); inventory.GetAllItems().Add(replacement);
            check(!Valid(), "Same-cell replacement cannot inherit the old target reference");
            inventory.GetAllItems().Clear(); inventory.GetAllItems().Add(item); item.m_gridPos = new Vector2i(2, 0);
            check(!Valid(), "Moved target invalidates the original selection");
            item.m_gridPos = new Vector2i(0, 0);
            Player.m_localPlayer = (Player)FormatterServices.GetUninitializedObject(typeof(Player));
            check(!Valid(), "Character switch invalidates the old player target");
            Player.m_localPlayer = player;
            CheckPreview(plugin, player, item, view, boundary, check);

            Type factoryType = plugin.Assembly.GetType("InventorySlots.PathOfValheimanCharacterPanelFactory", true)!;
            foreach (bool skills in new[] { true, false })
            {
                object factory = factoryType.GetConstructor(Instance, null, new[] { typeof(Assembly), typeof(bool) }, null)!.Invoke(new object[] { pov, skills });
                check(factory != null, "Original " + (skills ? "Skills" : "Stats") + " constructor/lifecycle contracts bind without publicizing PoV");
                Type nativeType = pov.GetType(skills ? "PassiveTree.Gems.GemBoardPanel" : "PassiveTree.UI.CharacterSheet", true)!;
                object native = FormatterServices.GetUninitializedObject(nativeType);
                foreach (string fieldName in new[] { "_show", "_fit", "_tick", "_dispose" })
                {
                    var method = (MethodInfo)factoryType.GetField(fieldName, Instance)!.GetValue(factory)!;
                    Type delegateType = fieldName == "_show" ? typeof(Action<bool>) : fieldName == "_tick" ? typeof(Action<Player>) :
                        fieldName == "_fit" ? typeof(Action<UnityEngine.Vector2, UnityEngine.Vector2>) : typeof(Action);
                    check(Delegate.CreateDelegate(delegateType, native, method) != null, "Original Mono binds internal view " + fieldName + " as a closed delegate");
                }
                if (skills)
                {
                    FieldInfo current = nativeType.GetField("Current", Static)!;
                    object? saved = current.GetValue(null);
                    try
                    {
                        current.SetValue(null, native);
                        nativeType.GetMethod("Dispose")!.Invoke(native, null);
                        check(current.GetValue(null) == null, "GemBoard Dispose releases its own singleton without moving gems");
                        object other = FormatterServices.GetUninitializedObject(nativeType);
                        current.SetValue(null, other);
                        nativeType.GetMethod("Dispose")!.Invoke(native, null);
                        check(ReferenceEquals(current.GetValue(null), other), "GemBoard Dispose cannot clear a different view owner");
                    }
                    finally { current.SetValue(null, saved); }
                }
            }
            check(engineDestroyCalls == 0, "Null-root native lifecycle checks never invoke Unity destruction");

            plugin.GetMethod("BindPathOfValheimanCrafting", Static)!.Invoke(null, new object[] { pov });
            var returns = (Delegate?)plugin.GetField("_povTypedDisenchantReturns", Static)!.GetValue(null);
            if (returns != null)
            {
                Type native = pov.GetType("PassiveTree.Loot.Disenchant", true)!;
                boundary.Patch(native.GetMethod("Returns", new[] { typeof(ItemData), typeof(int).MakeByRefType(), typeof(int).MakeByRefType() }),
                    prefix: new HarmonyMethod(typeof(PoVCharacterChecks), nameof(BaseReturns)));
                boundary.Patch(pov.GetType("PassiveTree.Tunables", true)!.GetMethod("Of", Static, null, new[] { typeof(string) }, null),
                    prefix: new HarmonyMethod(typeof(PoVCharacterChecks), nameof(Share)));
                object?[] args = { item, null, 0, 0 };
                returns.DynamicInvoke(args);
                check((string)args[1]! == "PT_AzuriteDust" && (int)args[2]! == 12 && (int)args[3]! == 2, "Bound typed preview uses original ordinary dust wrapper");
                item.m_customData["pt.corrupt"] = "1";
                returns.DynamicInvoke(args);
                check((string)args[1]! == "PT_CorruptedAzuriteDust" && (int)args[2]! == 3 && (int)args[3]! == 2, "Bound typed preview preserves original corrupted dust and scaled count");
            }
        }
        finally
        {
            boundary.UnpatchSelf();
            plugin.GetField("_povLooks", Static)!.SetValue(null, oldLooks);
            Player.m_localPlayer = oldPlayer;
        }
    }

    private static void CheckPreview(Type plugin, Player player, ItemData item, object view, Harmony hooks, Action<bool, string> check)
    {
        object? Call(string name, params object?[] args) => plugin.GetMethod(name, Static)!.Invoke(null, args);
        FieldInfo stateField = plugin.GetField("_povLookPreview", Static)!, refreshField = plugin.GetField("_povRefreshLookVisuals", Static)!;
        FieldInfo viewField = plugin.GetField("_povCharacterView", Static)!;
        object? oldState = stateField.GetValue(null), oldRefresh = refreshField.GetValue(null), oldView = viewField.GetValue(null);
        Type stateType = plugin.GetNestedType("PovLookPreview", BindingFlags.NonPublic)!;
        object preview = Activator.CreateInstance(stateType, true)!;
        var visual = (VisEquipment)FormatterServices.GetUninitializedObject(typeof(VisEquipment));
        void Set(string name, object value) => stateType.GetField(name, Instance)!.SetValue(preview, value);
        try
        {
            Call("InstallPathOfValheimanLookPreview", hooks);
            check(refreshField.GetValue(null) is Action<VisEquipment>, "Original private render signatures/delegate bind without publicizing the game");
            Set("Player", player); Set("Target", item); Set("Visuals", visual); Set("Cell", item.m_gridPos); Set("Hash", 123); Set("Variant", 7);
            AccessTools.Field(typeof(Humanoid), "m_visEquipment").SetValue(player, visual);
            view.GetType().GetField("Active", Instance)!.SetValue(view, true);
            viewField.SetValue(null, view);
            var fields = new Dictionary<string, string> { ["SetHelmetEquipped"]="m_helmetItem", ["SetChestEquipped"]="m_chestItem",
                ["SetLegEquipped"]="m_legItem", ["SetUtilityEquipped"]="m_utilityItem", ["SetTrinketEquipped"]="m_trinketItem",
                ["SetRightHandEquipped"]="m_rightItem", ["SetLeftHandEquipped"]="m_leftItem", ["SetShoulderEquipped"]="m_shoulderItem",
                ["LeftBack"]="m_hiddenLeftItem", ["RightBack"]="m_hiddenRightItem" };
            foreach (var entry in fields)
            {
                FieldInfo field = AccessTools.Field(typeof(Humanoid), entry.Value);
                object? saved = field.GetValue(player); field.SetValue(player, item); Set("Channel", entry.Key); stateField.SetValue(null, preview);
                object?[] args;
                if (entry.Key.EndsWith("Back"))
                {
                    args = new object?[] { visual, 10, 20, 30 }; Call("PreviewPovLookBack", args);
                    check(entry.Key == "LeftBack" ? (int)args[1]! == 123 && (int)args[2]! == 20 && (int)args[3]! == 7 :
                        (int)args[1]! == 10 && (int)args[2]! == 123 && (int)args[3]! == 30, "Back preview changes only the selected hand: " + entry.Key);
                }
                else
                {
                    MethodInfo original = AccessTools.DeclaredMethod(typeof(VisEquipment), entry.Key);
                    bool variant = entry.Key == "SetLeftHandEquipped" || entry.Key == "SetShoulderEquipped";
                    args = variant ? new object?[] { visual, original, 10, 30 } : new object?[] { visual, original, 10 };
                    Call(variant ? "PreviewPovLookVariant" : "PreviewPovLookHash", args);
                    check((int)args[2]! == 123 && (!variant || (int)args[3]! == 7), "Actual render prefix applies selected hash/variant: " + entry.Key);
                    args[0] = FormatterServices.GetUninitializedObject(typeof(VisEquipment)); args[2] = 10;
                    Call(variant ? "PreviewPovLookVariant" : "PreviewPovLookHash", args);
                    check((int)args[2]! == 10, "Other player/portrait/ragdoll visuals untouched: " + entry.Key);
                }
                check((bool)Call("IsPovPreviewStillSelected", preview)!, "Preview still belongs to selected equipped reference: " + entry.Key);
                field.SetValue(player, item.Clone());
                check(!(bool)Call("IsPovPreviewStillSelected", preview)!, "Equipment replacement cancels preview: " + entry.Key);
                field.SetValue(player, saved);
            }
            int restored = 0;
            refreshField.SetValue(null, new Action<VisEquipment>(v => { check(stateField.GetValue(null) == null, "Preview released before committed render refresh"); restored++; }));
            stateField.SetValue(null, preview); Call("ClearPathOfValheimanLookPreview"); Call("ClearPathOfValheimanLookPreview");
            check(restored == 1 && !item.m_customData.ContainsKey("valheiman.equipment.v1.look"), "Preview cancellation is idempotent and never writes appearance metadata");
        }
        finally { stateField.SetValue(null, oldState); refreshField.SetValue(null, oldRefresh); viewField.SetValue(null, oldView); }
    }
}
