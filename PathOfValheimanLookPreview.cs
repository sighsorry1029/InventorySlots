using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using ItemData = ItemDrop.ItemData;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    // Renderer-only override. Never write ItemData, VisEquipment's stored item
    // fields or ZDO: those are committed appearances and can be saved/networked.
    private sealed class PovLookPreview
    {
        internal Player Player = null!;
        internal VisEquipment Visuals = null!;
        internal ItemData Target = null!;
        internal Vector2i Cell;
        internal string Channel = "";
        internal int Hash, Variant;
    }

    private static PovLookPreview? _povLookPreview;
    private static Action<VisEquipment>? _povRefreshLookVisuals;
    private static readonly AccessTools.FieldRef<Humanoid, VisEquipment> PovPreviewVisuals =
        AccessTools.FieldRefAccess<Humanoid, VisEquipment>("m_visEquipment");
    private static readonly Dictionary<string, AccessTools.FieldRef<Humanoid, ItemData>> PovPreviewChannels = new()
    {
        ["SetHelmetEquipped"] = AccessTools.FieldRefAccess<Humanoid, ItemData>("m_helmetItem"),
        ["SetChestEquipped"] = AccessTools.FieldRefAccess<Humanoid, ItemData>("m_chestItem"),
        ["SetLegEquipped"] = AccessTools.FieldRefAccess<Humanoid, ItemData>("m_legItem"),
        ["SetShoulderEquipped"] = AccessTools.FieldRefAccess<Humanoid, ItemData>("m_shoulderItem"),
        ["SetUtilityEquipped"] = AccessTools.FieldRefAccess<Humanoid, ItemData>("m_utilityItem"),
        ["SetTrinketEquipped"] = AccessTools.FieldRefAccess<Humanoid, ItemData>("m_trinketItem"),
        ["SetRightHandEquipped"] = AccessTools.FieldRefAccess<Humanoid, ItemData>("m_rightItem"),
        ["SetLeftHandEquipped"] = AccessTools.FieldRefAccess<Humanoid, ItemData>("m_leftItem"),
        ["LeftBack"] = AccessTools.FieldRefAccess<Humanoid, ItemData>("m_hiddenLeftItem"),
        ["RightBack"] = AccessTools.FieldRefAccess<Humanoid, ItemData>("m_hiddenRightItem")
    };

    private static void InstallPathOfValheimanLookPreview(Harmony harmony)
    {
        MethodInfo Require(string name, params Type[] args)
        {
            MethodInfo method = AccessTools.DeclaredMethod(typeof(VisEquipment), name, args);
            Type expected = name == "UpdateEquipmentVisuals" ? typeof(void) : typeof(bool);
            if (method == null || method.IsStatic || method.ReturnType != expected)
                throw new MissingMethodException(typeof(VisEquipment).FullName, name);
            return method;
        }
        MethodInfo update = Require("UpdateEquipmentVisuals");
        // Resolve every signature before installing this optional capability.
        var methods = new List<(MethodInfo Target, string Hook)>();
        foreach (string name in new[] { "SetChestEquipped", "SetLegEquipped", "SetUtilityEquipped", "SetTrinketEquipped" })
            methods.Add((Require(name, typeof(int)), nameof(PreviewPovLookHash)));
        foreach (string name in new[] { "SetHelmetEquipped", "SetRightHandEquipped" })
            methods.Add((Require(name, typeof(int), typeof(int)), nameof(PreviewPovLookHash)));
        foreach (string name in new[] { "SetLeftHandEquipped", "SetShoulderEquipped" })
            methods.Add((Require(name, typeof(int), typeof(int), typeof(int)), nameof(PreviewPovLookVariant)));
        methods.Add((Require("SetBackEquipped", typeof(int), typeof(int), typeof(int), typeof(int), typeof(int)), nameof(PreviewPovLookBack)));
        foreach (var method in methods)
            harmony.Patch(method.Target, prefix: new HarmonyMethod(typeof(InventorySlotsPlugin), method.Hook) { priority = Priority.Last });
        harmony.Patch(update, prefix: new HarmonyMethod(typeof(InventorySlotsPlugin), nameof(ValidatePovPreviewBeforeRender)));
        _povRefreshLookVisuals = AccessTools.MethodDelegate<Action<VisEquipment>>(update);
    }

    private static void StartPathOfValheimanLookPreview(PovCharacterView view)
    {
        ClearPathOfValheimanLookPreview();
        if (_povRefreshLookVisuals == null || view.Target == null || view.Picked == null ||
            !ValidatePathOfValheimanLookTarget(view)) return;
        string? channel = null;
        foreach (var entry in PovPreviewChannels)
            if (ReferenceEquals(entry.Value(view.Player), view.Target)) { channel = entry.Key; break; }
        // Quivers/custom attachments and unequipped hotbar items have no vanilla
        // render channel. Selection/final application still work through PoV.
        if (channel == null) return;
        bool hidden = ReferenceEquals(view.Picked, _povLooks!.Hidden());
        if (hidden && !view.CanHide) return;
        string prefab = hidden ? "" : view.Picked.m_dropPrefab != null ? view.Picked.m_dropPrefab.name : "";
        if (!hidden && string.IsNullOrEmpty(prefab)) return;
        VisEquipment visual = PovPreviewVisuals(view.Player);
        if (visual == null) return;
        _povLookPreview = new PovLookPreview
        {
            Player = view.Player, Visuals = visual, Target = view.Target, Cell = view.TargetCell,
            Channel = channel, Hash = hidden ? 0 : prefab.GetStableHashCode(), Variant = hidden ? 0 : view.Picked.m_variant
        };
        try { _povRefreshLookVisuals(visual); }
        catch { ClearPathOfValheimanLookPreview(); throw; }
    }

    private static bool IsPovPreviewStillSelected(PovLookPreview preview) =>
        _povCharacterView is { Active: true, Closing: false, Tab: 0 } view &&
        ReferenceEquals(view.Player, preview.Player) && ReferenceEquals(view.Target, preview.Target) &&
        ReferenceEquals(Player.m_localPlayer, preview.Player) && preview.Target.m_gridPos == preview.Cell &&
        ReferenceEquals(PovPreviewChannels[preview.Channel](preview.Player), preview.Target) &&
        ReferenceEquals(PovPreviewVisuals(preview.Player), preview.Visuals);

    private static void ValidatePathOfValheimanLookPreview()
    {
        if (_povLookPreview is { } preview && (!IsPovPreviewStillSelected(preview) ||
            !InventoryGui.IsVisible() || !CanShowPathOfValheimanCharacter(preview.Player) ||
            !ContainsExactItemReference(preview.Player.GetInventory(), preview.Target)))
            ClearPathOfValheimanLookPreview();
    }

    private static void ValidatePovPreviewBeforeRender(VisEquipment __instance)
    {
        if (_povLookPreview is not { } preview || !ReferenceEquals(preview.Visuals, __instance)) return;
        // We are already inside the refresh; let this call restore committed
        // visuals without recursively starting another refresh from a prefix.
        if (!IsPovPreviewStillSelected(preview) || !InventoryGui.IsVisible() || !CanShowPathOfValheimanCharacter(preview.Player))
            _povLookPreview = null;
    }

    private static void ClearPathOfValheimanLookPreview()
    {
        PovLookPreview? preview = _povLookPreview;
        _povLookPreview = null;
        if (preview == null || preview.Visuals == null) return;
        try { _povRefreshLookVisuals?.Invoke(preview.Visuals); }
        catch (Exception error) { Log.LogWarning("PoV appearance preview restore: " + error.GetBaseException().Message); }
    }

    private static bool CanRenderPovPreview(VisEquipment visual, string channel) =>
        _povLookPreview is { } preview && ReferenceEquals(preview.Visuals, visual) && preview.Channel == channel &&
        ReferenceEquals(PovPreviewChannels[channel](preview.Player), preview.Target);

    private static void PreviewPovLookHash(VisEquipment __instance, MethodBase __originalMethod, ref int __0)
    {
        if (_povLookPreview != null && CanRenderPovPreview(__instance, __originalMethod.Name)) __0 = _povLookPreview.Hash;
    }

    private static void PreviewPovLookVariant(VisEquipment __instance, MethodBase __originalMethod, ref int __0, ref int __1)
    {
        if (_povLookPreview == null || !CanRenderPovPreview(__instance, __originalMethod.Name)) return;
        __0 = _povLookPreview.Hash; __1 = _povLookPreview.Variant;
    }

    private static void PreviewPovLookBack(VisEquipment __instance, ref int __0, ref int __1, ref int __2)
    {
        if (_povLookPreview == null) return;
        if (CanRenderPovPreview(__instance, "LeftBack")) { __0 = _povLookPreview.Hash; __2 = _povLookPreview.Variant; }
        else if (CanRenderPovPreview(__instance, "RightBack")) __1 = _povLookPreview.Hash;
    }
}
