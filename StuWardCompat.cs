using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Bootstrap;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    // Shared viewing and ownership handoffs deliberately use the same policy.
    private static bool HasContainerAreaRequesterAccess(long playerId, Container container)
    {
        if (!CheckContainerAreaAccess(container, playerId)) return false;
        var managed = StuWardCompat.TryCheckContainerAccess(container, playerId, out var allowed);
        if (managed && !allowed) return false;
        if (!container.m_checkGuardStone) return true;
        bool guarded = false;
        foreach (PrivateArea ward in (List<PrivateArea>)ContainerAreaWards.GetValue(null))
        {
            if (ward == null || !IsContainerAreaWardEnabled(ward) ||
                !IsInsideContainerAreaWard(ward, container.transform.position, 0f)) continue;
            guarded = true;
            // STUWard already checked all overlapping managed wards, including
            // Containers settings, group trust and admin debug. Vanilla wards
            // retain their original any-owner/explicit-permit policy below.
            if (managed && StuWardCompat.IsManagedWard(ward)) return true;
            Piece? piece = ward.GetComponent<Piece>();
            if (piece != null && piece.GetCreator() == playerId ||
                ContainerAreaWardPlayers(ward).Any(entry => entry.Key == playerId)) return true;
        }
        return !guarded;
    }
}

internal static class StuWardCompat
{
    private delegate bool CheckAccess(Container container, long playerId, out bool allowed);
    private static CheckAccess? _check;
    private static Func<PrivateArea, bool>? _isManaged;
    private static bool _resolved;
    private static bool _warned;

    internal static bool TryCheckContainerAccess(Container container, long playerId, out bool allowed)
    {
        allowed = false;
        if (!_resolved)
        {
            if (!Chainloader.PluginInfos.TryGetValue("sighsorry.STUWard", out var plugin) || plugin.Instance == null) return false;
            _resolved = true;
            try
            {
                var api = plugin.Instance.GetType().Assembly.GetType("STUWard.WardAccessApi");
                var check = api?.GetMethod("TryCheckContainerAccess", BindingFlags.Public | BindingFlags.Static, null,
                    new[] { typeof(Container), typeof(long), typeof(bool).MakeByRefType() }, null);
                var isManaged = api?.GetMethod("IsManagedWard", BindingFlags.Public | BindingFlags.Static, null,
                    new[] { typeof(PrivateArea) }, null);
                if (check == null || isManaged == null) throw new MissingMethodException("STUWard.WardAccessApi");
                _check = (CheckAccess)Delegate.CreateDelegate(typeof(CheckAccess), check);
                _isManaged = (Func<PrivateArea, bool>)Delegate.CreateDelegate(typeof(Func<PrivateArea, bool>), isManaged);
            }
            catch (Exception error)
            {
                _check = null;
                _isManaged = null;
                Warn(error);
            }
        }
        if (_check == null) return false;
        try { return _check(container, playerId, out allowed); }
        catch (Exception error)
        {
            Warn(error);
            allowed = false;
            return true;
        }
    }

    internal static bool IsManagedWard(PrivateArea ward)
    {
        try { return _isManaged?.Invoke(ward) == true; }
        catch (Exception error) { Warn(error); return false; }
    }

    private static void Warn(Exception error)
    {
        if (_warned) return;
        _warned = true;
        InventorySlotsPlugin.Log.LogWarning($"STUWard shared-chest authorization is unavailable: {error.Message}. Group access requires the updated STUWard API.");
    }
}
