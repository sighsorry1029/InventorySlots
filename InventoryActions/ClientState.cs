using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using InventoryPersistence;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using UnityEngine;

namespace InventoryActions;

internal sealed class InventoryActionsClientState
{
    public bool FeatureGuideCollapsed { get; set; }
    public bool FeatureGuideHidden { get; set; }
    public Dictionary<string, InventoryActionsClientPlayerState> Players { get; set; } = new(StringComparer.Ordinal);
}

internal sealed class InventoryActionsClientPlayerState
{
    public List<InventoryActionsFavoriteSlot> FavoriteSlots { get; set; } = new();
}

internal sealed class InventoryActionsFavoriteSlot
{
    public int X { get; set; }
    public int Y { get; set; }
    public string Prefab { get; set; } = "";
}

public sealed partial class InventoryActionsPlugin
{
    // UI preferences belong to this client, not to the currently loaded character.
    private static InventoryActionsClientState? _clientState;
    private static bool _clientStateDirectWriteFallbackLogged;
    private static bool _clientStateLoadFailed;
    private static string ClientStateFilePath => Path.Combine(Paths.ConfigPath, "InventoryActions.ClientState.yml");

    private static InventoryActionsClientState GetClientState()
    {
        if (_clientState != null) return _clientState;
        _clientState = new InventoryActionsClientState();
        try
        {
            if (File.Exists(ClientStateFilePath))
                _clientState = new DeserializerBuilder()
                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                    .IgnoreUnmatchedProperties().Build()
                    .Deserialize<InventoryActionsClientState>(File.ReadAllText(ClientStateFilePath))
                    ?? new InventoryActionsClientState();
            _clientState.Players ??= new Dictionary<string, InventoryActionsClientPlayerState>(StringComparer.Ordinal);
        }
        catch (Exception ex)
        {
            // A malformed consolidated file must not be overwritten with empty player records.
            _clientStateLoadFailed = true;
            Log.LogWarning($"Failed to load InventoryActions client state from {ClientStateFilePath}: {ex.Message}");
        }
        return _clientState;
    }

    private static bool IsFeatureGuideVisible() => !GetClientState().FeatureGuideHidden;
    private static bool IsFeatureGuideCollapsed() => GetClientState().FeatureGuideCollapsed;

    private static void SetFeatureGuideState(bool visible, bool collapsed)
    {
        InventoryActionsClientState state = GetClientState();
        state.FeatureGuideHidden = !visible;
        state.FeatureGuideCollapsed = visible && collapsed;
        InvalidateFeatureGuideTextAndMeasurements();
        SaveFeatureGuideState();
    }

    private static bool SaveClientState()
    {
        InventoryActionsClientState state = GetClientState();
        if (_clientStateLoadFailed) return false;
        SnapshotLoadedFavorites(state);
        try
        {
            string yaml = new SerializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build().Serialize(state);
            Exception? compatibilityReason = LinkCompatibleFileWriter.WriteAllText(ClientStateFilePath, yaml);
            if (compatibilityReason != null && !_clientStateDirectWriteFallbackLogged)
            {
                _clientStateDirectWriteFallbackLogged = true;
                Log.LogWarning(
                    $"InventoryActions client state is using link-compatible direct writes because atomic replacement is unavailable for {ClientStateFilePath} " +
                    $"({compatibilityReason.GetType().Name}, 0x{compatibilityReason.HResult:X8}).");
            }
            return true;
        }
        catch (Exception ex)
        {
            Log.LogWarning($"Failed to save InventoryActions client state: {ex.Message}");
            return false;
        }
    }

    private static void SnapshotLoadedFavorites(InventoryActionsClientState state)
    {
        string playerId = Runtime.LoadedFavoritesPlayerId;
        if (string.IsNullOrEmpty(playerId)) return;
        state.Players[playerId] = new InventoryActionsClientPlayerState
        {
            FavoriteSlots = Runtime.FavoriteSlots.OrderBy(slot => slot.y).ThenBy(slot => slot.x)
                .Select(slot => new InventoryActionsFavoriteSlot
                {
                    X = slot.x, Y = slot.y,
                    Prefab = FavoriteSlotItems.TryGetValue(slot, out string? prefab) ? prefab ?? "" : ""
                }).ToList()
        };
    }

    private static void EnsureFavoritesLoaded(Player player)
    {
        if (player == null) return;
        string playerId = GetPlayerId(player);
        if (string.Equals(Runtime.LoadedFavoritesPlayerId, playerId, StringComparison.Ordinal)) return;

        InventoryActionsClientState state = GetClientState();
        // Preserve observations made during a failed-save retry delay under the old owner.
        SnapshotLoadedFavorites(state);
        FlushPendingClientState();
        Runtime.FavoriteSlots.Clear();
        FavoriteSlotItems.Clear();
        Runtime.LoadedFavoritesPlayerId = playerId;
        _favoriteMemoryPending = true;
        if (!state.Players.TryGetValue(playerId, out InventoryActionsClientPlayerState? playerState) ||
            playerState?.FavoriteSlots == null) return;
        foreach (InventoryActionsFavoriteSlot slot in playerState.FavoriteSlots)
        {
            if (slot == null || slot.X < 0 || slot.Y < 0) continue;
            Vector2i cell = new(slot.X, slot.Y);
            Runtime.FavoriteSlots.Add(cell);
            if (!string.IsNullOrEmpty(slot.Prefab)) FavoriteSlotItems[cell] = slot.Prefab;
        }
    }

    private static void SaveFavorites(Player player)
    {
        if (player == null) return;
        EnsureFavoritesLoaded(player);
        _favoriteMemorySavePending = true;
        _favoriteMemorySaveRetryAt = Time.unscaledTime + 5f;
        FlushPendingClientState();
    }

    private static void FlushPendingClientState()
    {
        if (_favoriteMemorySavePending && _clientState != null && SaveClientState())
        {
            _favoriteMemorySavePending = false;
            _favoriteMemorySaveRetryAt = 0f;
        }
    }
}
