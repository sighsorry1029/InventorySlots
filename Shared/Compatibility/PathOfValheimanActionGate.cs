using System;
using System.Reflection;
using BepInEx.Bootstrap;
using UnityEngine.UI;

#if INVENTORY_SLOTS
namespace InventorySlots;
public sealed partial class InventorySlotsPlugin
#else
namespace InventoryActions;
public sealed partial class InventoryActionsPlugin
#endif
{
    private static bool _povTidyPackRequired;
    private static Func<bool>? _povTidyPackAvailable;
    private static bool _povTidyPackWarning;

    private static void InitializePathOfValheimanActionGate()
    {
        ResetPathOfValheimanActionGate();
        if (IsDedicatedServer || !Chainloader.PluginInfos.TryGetValue("treextr.pathofvalheiman", out var plugin)) return;
        _povTidyPackRequired = true;
        try
        {
            if (plugin.Instance == null) throw new InvalidOperationException("PoV plugin has not initialized");
            BindPathOfValheimanActionGate(plugin.Instance.GetType().Assembly);
        }
        catch (Exception error) { DisablePathOfValheimanActionGate(error); }
    }

    private static void BindPathOfValheimanActionGate(Assembly assembly)
    {
        _povTidyPackRequired = true;
        _povTidyPackAvailable = null;
        Type? type = assembly.GetType("Valheiman.Api.QuickStack");
        MethodInfo? getter = type?.GetMethod("get_Available", BindingFlags.Public | BindingFlags.Static,
            null, Type.EmptyTypes, null);
        if (type?.IsVisible != true || getter == null || getter.ReturnType != typeof(bool) || getter.ContainsGenericParameters)
            throw new MissingMethodException("Valheiman.Api.QuickStack", "get_Available");
        // Cache the public accessor, never its result: progression, respec and
        // character changes must be reflected before starting the next action.
        _povTidyPackAvailable = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), getter);
    }

    private static bool CanUseTidyPackActions()
    {
        if (!_povTidyPackRequired) return true;
        try { return _povTidyPackAvailable?.Invoke() == true; }
        catch (Exception error) { DisablePathOfValheimanActionGate(error); return false; }
    }

    private static void DisablePathOfValheimanActionGate(Exception error)
    {
        _povTidyPackAvailable = null;
        if (!_povTidyPackWarning)
            Log.LogWarning("PoV Tidy Pack unlock status unavailable; nearby quick stack and player inventory sorting are locked: " + error.GetBaseException().Message);
        _povTidyPackWarning = true;
    }

    private static void ResetPathOfValheimanActionGate()
    {
        _povTidyPackRequired = false;
        _povTidyPackAvailable = null;
        _povTidyPackWarning = false;
    }

    private static string GetTidyPackLockedText() => _povTidyPackAvailable == null
        ? LocalizeUi("$" + ModName.ToLowerInvariant() + "_pov_tidy_pack_unavailable", "PoV unlock status unavailable: nearby quick stack and inventory sorting are locked.")
        : LocalizeUi("$" + ModName.ToLowerInvariant() + "_pov_tidy_pack_locked", "Nearby quick stack and inventory sorting require PoV's Tidy Pack mastery.");

    private static bool RequireTidyPackActions(Player? player)
    {
        if (CanUseTidyPackActions()) return true;
        if (player != null) player.Message(MessageHud.MessageType.Center, GetTidyPackLockedText(), 0, null);
        return false;
    }

    private static string GetTidyPackSortLockedText() =>
        LocalizeUi("$" + ModName.ToLowerInvariant() + "_pov_sort_locked", "Sort: locked by PoV");

    private static string AddTidyPackGuideHint(string guide)
    {
        if (CanUseTidyPackActions()) return guide;
        string hint = "\n<color=#FFA94D>" + GetTidyPackLockedText() + "</color>";
        int titleEnd = guide.IndexOf('\n');
        return titleEnd >= 0 ? guide.Insert(titleEnd, hint) : guide + hint;
    }

    private static void UpdateTidyPackSortButton(Button? button)
    {
        if (button == null) return;
        bool available = CanUseTidyPackActions();
        SetButtonInteractable(button, available);
        UITooltip tooltip = button.GetComponent<UITooltip>() ?? button.gameObject.AddComponent<UITooltip>();
        tooltip.m_topic = LocalizeUi("$" + ModName.ToLowerInvariant() + "_action_sort", "Sort");
        tooltip.m_text = available
            ? LocalizeUi("$" + ModName.ToLowerInvariant() + "_pov_sort_description", "Fill favorite stacks from matching non-favorite stacks outside the hotbar, then sort the remaining ordinary stacks. Favorite stacks stay in place.")
            : GetTidyPackLockedText();
    }
}
