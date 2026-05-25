using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Bootstrap;
using HarmonyLib;
using UnityEngine;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private const string RecycleNReclaimCompatibilityCapability = "Recycle_N_Reclaim reclaim tab";
    private const float RecycleNReclaimSignatureCacheSeconds = 0.1f;

    private readonly struct RecycleNReclaimYieldTextEntry
    {
        public RecycleNReclaimYieldTextEntry(string name, int amount, ItemDrop.ItemData? item = null)
        {
            Name = name;
            Amount = amount;
            Item = item;
        }

        public string Name { get; }
        public int Amount { get; }
        public ItemDrop.ItemData? Item { get; }
    }

    private static bool TryGetRecycleNReclaimApi(out RecycleNReclaimApi? api)
    {
        return TryGetCompatApi(
            RecycleNReclaimGuid,
            RecycleNReclaimCompatibilityCapability,
            CompatRuntime.RecycleNReclaim,
            RecycleNReclaimApi.TryCreate,
            "Recycle_N_Reclaim compatibility disabled",
            out api);
    }

    private static int _recycleNReclaimActiveFrame = -1;
    private static bool _recycleNReclaimActiveFrameValue;
    private static float _recycleNReclaimContextSignatureExpiresAt;
    private static string _recycleNReclaimContextSignature = "";

    private static bool IsRecycleNReclaimReclaimTabActive(InventoryGui? gui = null)
    {
        if (gui != null && (gui.m_crafting == null || !gui.m_crafting.gameObject.activeInHierarchy))
        {
            return false;
        }

        int frame = Time.frameCount;
        if (_recycleNReclaimActiveFrame == frame)
        {
            return _recycleNReclaimActiveFrameValue;
        }

        _recycleNReclaimActiveFrame = frame;
        _recycleNReclaimActiveFrameValue = TryGetRecycleNReclaimApi(out RecycleNReclaimApi? api) && api!.IsRecycleTabActive();
        return _recycleNReclaimActiveFrameValue;
    }

    private static bool TryGetRecycleNReclaimRecyclingImpedimentCount(int originalIndex, out int count)
    {
        count = 0;
        return TryGetRecycleNReclaimApi(out RecycleNReclaimApi? api) &&
               api!.IsRecycleTabActive() &&
               api.TryGetRecyclingImpedimentCount(originalIndex, out count);
    }

    private static string GetRecycleNReclaimContextSignature()
    {
        if (Time.unscaledTime < _recycleNReclaimContextSignatureExpiresAt)
        {
            return _recycleNReclaimContextSignature;
        }

        _recycleNReclaimContextSignatureExpiresAt = Time.unscaledTime + RecycleNReclaimSignatureCacheSeconds;
        _recycleNReclaimContextSignature = TryGetRecycleNReclaimApi(out RecycleNReclaimApi? api) && api!.IsRecycleTabActive()
            ? api.GetContextSignature()
            : "";
        return _recycleNReclaimContextSignature;
    }

    private static void ClearRecycleNReclaimSignatureCaches()
    {
        _recycleNReclaimActiveFrame = -1;
        _recycleNReclaimActiveFrameValue = false;
        _recycleNReclaimContextSignatureExpiresAt = 0f;
        _recycleNReclaimContextSignature = "";
        ClearRecycleNReclaimRecipeListSignatureCache();
    }

    private static bool TryGetRecycleNReclaimSummary(
        int originalIndex,
        List<string> impediments,
        List<RecycleNReclaimYieldTextEntry> yields)
    {
        impediments.Clear();
        yields.Clear();
        return TryGetRecycleNReclaimApi(out RecycleNReclaimApi? api) &&
               api!.IsRecycleTabActive() &&
               api.TryGetReclaimSummary(originalIndex, impediments, yields);
    }

    internal static void OnRecycleNReclaimNativePanelUpdated()
    {
        InventoryGui? gui = InventoryGui.instance;
        if (gui == null || IsUnityNull(gui) || Player.m_localPlayer == null)
        {
            return;
        }

        ClearRecycleNReclaimSignatureCaches();
        if (TryGetRecycleNReclaimApi(out RecycleNReclaimApi? api) && api != null && api.IsRecycleTabActive())
        {
            api.TryRefreshSelectedRecipeUi();
            ClearRecycleNReclaimSignatureCaches();
            UpdateCraftingPanelRedesign(gui, CraftingPanelUpdateReason.RecipeListChanged);
        }
    }
}

[HarmonyPatch]
internal static class RecycleNReclaimNativePanelUpdatedInventorySlotsPatch
{
    private const string RecycleNReclaimGuid = "Azumatt.Recycle_N_Reclaim";
    private const string StationRecyclingTabHolderTypeName = "Recycle_N_Reclaim.GamePatches.UI.StationRecyclingTabHolder";

    private static MethodBase? TargetMethod() =>
        TryGetTargetMethod(out MethodBase? method) ? method : null;

    private static bool Prepare() =>
        TryGetTargetMethod(out _);

    private static bool TryGetTargetMethod(out MethodBase? method)
    {
        method = null;
        if (!Chainloader.PluginInfos.TryGetValue(RecycleNReclaimGuid, out BepInEx.PluginInfo pluginInfo) ||
            pluginInfo.Instance == null)
        {
            return false;
        }

        Type? holderType = pluginInfo.Instance.GetType().Assembly.GetType(StationRecyclingTabHolderTypeName);
        method = holderType?.GetMethod("UpdateCraftingPanel", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
        return method != null;
    }

    private static void Postfix()
    {
        InventorySlotsPlugin.OnRecycleNReclaimNativePanelUpdated();
    }
}
