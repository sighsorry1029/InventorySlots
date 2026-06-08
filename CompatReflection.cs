using System;
using System.Reflection;
using BepInEx.Bootstrap;
using BepInEx.Configuration;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
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
            return SetCompatCapabilityState(ref runtime.CapabilityState, CompatCapabilityState.Available, capability);
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
                return MarkCompatMissingApi(
                    runtime,
                    capability,
                    detail,
                    $"{warningPrefix}: {detail}.");
            }

            runtime.Api = api;
            return SetCompatCapabilityState(ref runtime.CapabilityState, CompatCapabilityState.Available, capability);
        }
        catch (Exception ex)
        {
            api = null;
            return MarkCompatReflectionFailed(runtime, capability, ex.Message, warningPrefix);
        }
    }

    private static bool TryGetCompatAssembly<TApi>(string guid, string capability, CompatApiRuntimeState<TApi> runtime, out Assembly? assembly)
        where TApi : class
    {
        assembly = null;
        if (!HasPlugin(guid))
        {
            return SetCompatCapabilityState(ref runtime.CapabilityState, CompatCapabilityState.Unavailable, capability);
        }

        if (runtime.ReflectionFailed)
        {
            return runtime.CapabilityState == CompatCapabilityState.MissingApi
                ? false
                : SetCompatCapabilityState(ref runtime.CapabilityState, CompatCapabilityState.Failed, capability);
        }

        try
        {
            assembly = Chainloader.PluginInfos[guid].Instance.GetType().Assembly;
            return true;
        }
        catch (Exception ex)
        {
            return MarkCompatReflectionFailed(runtime, capability, ex.Message, $"{capability} compatibility disabled");
        }
    }

    private static bool MarkCompatMissingApi<TApi>(CompatApiRuntimeState<TApi> runtime, string capability, string detail, string warning)
        where TApi : class
    {
        runtime.ReflectionFailed = true;
        SetCompatCapabilityState(ref runtime.CapabilityState, CompatCapabilityState.MissingApi, capability, detail);
        Log.LogWarning(warning);
        return false;
    }

    private static bool MarkCompatReflectionFailed<TApi>(CompatApiRuntimeState<TApi> runtime, string capability, string detail, string warningPrefix)
        where TApi : class
    {
        runtime.ReflectionFailed = true;
        SetCompatCapabilityState(ref runtime.CapabilityState, CompatCapabilityState.Failed, capability, detail);
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
