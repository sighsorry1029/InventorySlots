using InventorySlots;
using UnityEngine;

CraftingEntryTests.Run();
InventorySlotsPlugin.CheckFrames();

namespace InventorySlots
{
    internal enum CraftingRecipeSortMode { Name }

    // Execute the production fast-path router with counted boundary calls.
    // These doubles do not simulate native crafting or Unity rendering.
    public sealed partial class InventorySlotsPlugin
    {
        private static bool _craftingRedesignApplied = true;
        private static RectTransform? _craftingRecipeGrid;
        private static int _craftingRecipePage, _craftingRecipeVariantVersion;
        private static bool ScrollInput, NoCost;
        private static int Availability, Selection, WheelCalls, DynamicCalls, Repairs, Checks;
        private static string View = "view", Pins = "";
        private static readonly List<string> Calls = new();
        private static class CraftingController
        {
            public static bool Dirty, IsSearchInputDirty;
            public static int HoveredRecipeIndex;
            private static CraftingFrameFastPathStamp Stamp;
            public static bool HasFrameRebuildWork() => Dirty;
            public static bool CanReuseFrameFastPath(CraftingFrameFastPathStamp stamp) => Stamp.Equals(stamp);
            public static void StoreFrameFastPathStamp(CraftingFrameFastPathStamp stamp) => Stamp = stamp;
            public static void ResetFrameFastPathStamp() => Stamp = default;
            public static void MarkBottomControlsDirty() => Dirty = true;
        }
        private static class CraftingQueue { public static object? QueueRecipe; public static bool ContinuingQueue; }
        private static class CraftingRequirements { public static int AvailabilityVersion; }
        private static class CraftingUi { public static InputField? SearchInput = new(); }
        private static class PinnedTooltips
        {
            public static class Crafting { public static RectTransform?[] Panels = new RectTransform?[3]; }
        }
        private static bool IsUnityNull(object? value) => value == null;
        private static bool HasUnconsumedUiScrollInput() => ScrollInput;
        private static int GetSelectedCraftingRecipeIndexSafe(InventoryGui gui) => Selection;
        private static string GetCraftingRecipeViewSignature(InventoryGui gui, CraftingTabAdapterState adapter) => View;
        private static int GetCraftingRecipeGridDimension() => 4;
        private static int GetCraftingRecipePageStart() => _craftingRecipePage * 16;
        private static int GetCraftingRecipeGridAvailabilityHash(InventoryGui gui, int start, CraftingTabAdapterState adapter) => Availability;
        private static bool HasNoCraftCost() => NoCost;
        private static string GetCraftingPinnedTooltipGridSignature() => Pins;
        private static bool HandleCraftingCountWheel() { WheelCalls++; return ScrollInput; }
        private static bool HandleCraftingRecipeWheelInput(InventoryGui gui, RectTransform grid) { WheelCalls++; return ScrollInput; }
        private static void HandleCraftingGroupFavoriteClearShortcut() { }
        private static void UpdateCraftingQueueLifecycle(InventoryGui gui) => Calls.Add("lifecycle");
        private static void SuppressCraftingTabAdapterFrameResidue(InventoryGui gui, CraftingTabAdapterState adapter) => Calls.Add("residue");
        private static void SuppressJewelcraftingCraftingSocketUiForRedesign(InventoryGui gui) => Calls.Add("sockets");
        private static void LayoutCraftingTabAdapterBottomControls(InventoryGui gui, RectTransform grid, CraftingTabAdapterState adapter)
        { Calls.Add("bottom"); DynamicCalls++; }
        private static void UpdateCraftingRecipeGridZoomHint(InventoryGui gui, RectTransform grid) { }
        private static void UpdateCraftingTooltipRecipeOverlay(InventoryGui gui) { }
        private static void UpdateCraftingViewControls(InventoryGui gui, RectTransform grid, CraftingTabAdapterState adapter) { }
        private static void RepairCraftingPinnedTooltipTextVisibility() => Repairs++;

        private static void Check(bool ok, string name)
        {
            if (!ok) throw new Exception(name);
            Checks++;
        }

        public static void CheckFrames()
        {
            var gui = new InventoryGui();
            _craftingRecipeGrid = new() { parent = gui.m_crafting };
            var vanilla = new CraftingTabAdapterState(CraftingTabAdapterKind.Vanilla);
            Check(!TryRunCraftingPanelFrameFastPath(gui, vanilla), "Cold UI needs full update");
            StoreCraftingFrameFastPathSignature(gui, vanilla);
            for (int frame = 0; frame < 120; frame++)
                Check(TryRunCraftingPanelFrameFastPath(gui, vanilla), "Idle frame reuses prepared UI");
            Check(DynamicCalls == 120, "Idle still updates dynamic requirements/timer UI each frame");
            Check(Repairs == 0, "No pin repair without visible pins");
            Check(Calls.Take(4).SequenceEqual(new[] { "lifecycle", "residue", "sockets", "bottom" }),
                "Progress cleanup and optional socket suppression precede bottom controls");

            foreach (var kind in new[] { CraftingTabAdapterKind.JewelcraftingSocket, CraftingTabAdapterKind.RecycleNReclaim, CraftingTabAdapterKind.Foreign })
                Check(!TryRunCraftingPanelFrameFastPath(gui, new(kind)), "Other tabs retain full update");
            foreach (Action change in new Action[]
            {
                () => Availability++, () => Selection++, () => gui.m_selectedVariant++,
                () => CraftingUi.SearchInput!.isFocused = !CraftingUi.SearchInput.isFocused,
                () => _craftingRecipePage++, () => _craftingRecipeVariantVersion++,
                () => CraftingRequirements.AvailabilityVersion++, () => CraftingController.HoveredRecipeIndex++,
                () => View += "changed", () => Pins += "pin", () => NoCost = !NoCost, () => Screen.width++
            })
            {
                StoreCraftingFrameFastPathSignature(gui, vanilla);
                change();
                Check(!TryRunCraftingPanelFrameFastPath(gui, vanilla), "Changed state requires immediate full update");
                StoreCraftingFrameFastPathSignature(gui, vanilla);
                Check(TryRunCraftingPanelFrameFastPath(gui, vanilla), "New state can be reused after full update");
            }

            CraftingController.Dirty = true;
            Check(!TryRunCraftingPanelFrameFastPath(gui, vanilla), "Dirty model cannot be reused");
            StoreCraftingFrameFastPathSignature(gui, vanilla);
            CraftingController.Dirty = false;
            Check(!TryRunCraftingPanelFrameFastPath(gui, vanilla), "Dirty store discards prior stamp");
            CraftingController.IsSearchInputDirty = true;
            Check(!TryRunCraftingPanelFrameFastPath(gui, vanilla), "Pending search rebuild");
            CraftingController.IsSearchInputDirty = false;
            CraftingQueue.QueueRecipe = new();
            Check(!TryRunCraftingPanelFrameFastPath(gui, vanilla), "Craft queue stays in full path");
            CraftingQueue.QueueRecipe = null;
            CraftingQueue.ContinuingQueue = true;
            Check(!TryRunCraftingPanelFrameFastPath(gui, vanilla), "Queue continuation stays in full path");
            CraftingQueue.ContinuingQueue = false;

            StoreCraftingFrameFastPathSignature(gui, vanilla);
            ScrollInput = true;
            WheelCalls = 0;
            for (int i = 0; i < 2; i++)
                Check(!TryRunCraftingPanelFrameFastPath(gui, vanilla), "Scroll bypasses fast path without consumption");
            Check(WheelCalls == 0 && ScrollInput, "No wheel handler runs before full-path fallthrough");
            HandleCraftingCountWheel();
            Check(WheelCalls == 1, "Outer full path applies one count step");
            ScrollInput = false;

            _craftingRecipeGrid.gameObject.activeSelf = false;
            Check(!TryRunCraftingPanelFrameFastPath(gui, vanilla), "Hidden grid needs recovery");
            _craftingRecipeGrid.gameObject.activeSelf = true;
            _craftingRecipeGrid.parent = new();
            Check(!TryRunCraftingPanelFrameFastPath(gui, vanilla), "Reparented grid needs recovery");
            _craftingRecipeGrid.parent = gui.m_crafting;
            PinnedTooltips.Crafting.Panels[0] = new();
            Check(TryRunCraftingPanelFrameFastPath(gui, vanilla) && Repairs == 1, "Visible pins retain repair");
            _craftingRecipeGrid = null;
            Check(!TryRunCraftingPanelFrameFastPath(gui, vanilla), "Destroyed grid needs recovery");
            Console.WriteLine($"Crafting frame routing: {Checks} checks passed.");
        }
    }
}

namespace UnityEngine
{
    public class RectTransform
    {
        public RectTransform? parent;
        public readonly GameObject gameObject = new();
        public int GetInstanceID() => GetHashCode();
    }
    public sealed class GameObject
    {
        public bool activeSelf = true;
        public bool activeInHierarchy => activeSelf;
    }
    public sealed class InputField { public bool isFocused; }
    public static class Screen { public static int width = 1920, height = 1080; }
}
public sealed class InventoryGui
{
    public RectTransform m_crafting = new();
    public int m_selectedVariant;
    public int GetInstanceID() => GetHashCode();
}

namespace HarmonyLib
{
    public static class AccessTools
    {
        public delegate TValue FieldRef<T, TValue>(T instance);
        public static FieldRef<T, TValue> FieldRefAccess<T, TValue>(string name)
        {
            var field = typeof(T).GetField(name)!;
            return instance => (TValue)field.GetValue(instance)!;
        }
    }
}
