using System;
using System.Reflection;
using BepInEx.Bootstrap;
using BepInEx.Configuration;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private sealed class CompatApiRuntimeState<TApi>
        where TApi : class
    {
        public TApi? Api;
        public bool ReflectionFailed;
    }

    private sealed class CompatRuntimeState
    {
        public readonly CompatApiRuntimeState<AzuCraftyBoxesApi> AzuCraftyBoxes = new();
        public readonly CompatApiRuntimeState<JewelcraftingTooltipApi> JewelcraftingTooltip = new();
        public readonly CompatApiRuntimeState<JewelcraftingGemApi> JewelcraftingGem = new();
        public readonly CompatApiRuntimeState<JewelcraftingEffectApi> JewelcraftingEffect = new();
        public readonly CompatApiRuntimeState<JewelcraftingSlotApi> JewelcraftingSlot = new();
        public readonly CompatApiRuntimeState<JewelcraftingCraftingSocketUiApi> JewelcraftingCraftingSocketUi = new();
        public readonly CompatApiRuntimeState<JewelcraftingGemCuttingApi> JewelcraftingGemCutting = new();
        public readonly CompatApiRuntimeState<JewelcraftingVisualApi> JewelcraftingVisual = new();
        public readonly CompatApiRuntimeState<ItemRequiresSkillLevelApi> ItemRequiresSkillLevel = new();
        public readonly CompatApiRuntimeState<RecycleNReclaimApi> RecycleNReclaim = new();
        public readonly CompatApiRuntimeState<AdventureBackpacksApi> AdventureBackpacks = new();
        public readonly CompatApiRuntimeState<SmoothbrainBackpacksApi> SmoothbrainBackpacks = new();
        public readonly CompatApiRuntimeState<RustyBagsApi> RustyBags = new();
        public readonly CompatApiRuntimeState<MagicSupremacyApi> MagicSupremacy = new();
        public readonly CompatApiRuntimeState<BetterArcheryQuiverApi> BetterArcheryQuiver = new();
        public readonly CompatApiRuntimeState<VeiledRecipesApi> VeiledRecipes = new();
    }

    private static readonly CompatRuntimeState CompatRuntime = new();

    private delegate bool CompatApiFactory<TApi>(Assembly assembly, out TApi? api, out string detail)
        where TApi : class;

    private static bool TryGetCompatApi<TApi>(
        string guid,
        string capability,
        CompatApiRuntimeState<TApi> runtime,
        CompatApiFactory<TApi> factory,
        string warningPrefix,
        out TApi? api)
        where TApi : class
    {
        api = runtime.Api;
        if (api != null)
        {
            return true;
        }

        if (!TryGetCompatAssembly(guid, capability, runtime, out Assembly? assembly))
        {
            api = null;
            return false;
        }

        try
        {
            if (!factory(assembly!, out api, out string detail) || api == null)
            {
                runtime.ReflectionFailed = true;
                Log.LogWarning($"{warningPrefix}: {detail}.");
                return false;
            }

            runtime.Api = api;
            return true;
        }
        catch (Exception ex)
        {
            api = null;
            return MarkCompatReflectionFailed(runtime, ex.Message, warningPrefix);
        }
    }

    private static bool TryGetCompatAssembly<TApi>(string guid, string capability, CompatApiRuntimeState<TApi> runtime, out Assembly? assembly)
        where TApi : class
    {
        assembly = null;
        if (!HasPlugin(guid))
        {
            return false;
        }

        if (runtime.ReflectionFailed)
        {
            return false;
        }

        try
        {
            assembly = Chainloader.PluginInfos[guid].Instance.GetType().Assembly;
            return true;
        }
        catch (Exception ex)
        {
            return MarkCompatReflectionFailed(runtime, ex.Message, $"{capability} compatibility disabled");
        }
    }

    private static bool MarkCompatReflectionFailed<TApi>(CompatApiRuntimeState<TApi> runtime, string detail, string warningPrefix)
        where TApi : class
    {
        runtime.ReflectionFailed = true;
        Log.LogWarning($"{warningPrefix}: {detail}");
        return false;
    }

    private static bool GetCompatConfigEntryToggleOn(FieldInfo field, ref ConfigEntryBase? config)
    {
        try
        {
            config ??= field.GetValue(null) as ConfigEntryBase;
            object? value = config?.BoxedValue;
            return value != null &&
                   (string.Equals(value.ToString(), Toggle.On.ToString(), StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(value.ToString(), bool.TrueString, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
    }
}
