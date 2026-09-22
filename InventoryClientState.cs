using System;
using System.IO;
using BepInEx;
using InventoryPersistence;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private static bool _clientStateDirectWriteFallbackLogged;

    private static void EnsureClientStateLoaded()
    {
        if (InventoryClient.ClientStateLoaded)
        {
            return;
        }

        InventoryClient.ClientStateLoaded = true;
        InventoryClient.ClientState = new InventorySlotsClientState();
        if (!File.Exists(ClientStateFilePath))
        {
            SaveClientState();
            return;
        }

        try
        {
            IDeserializer deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();
            InventoryClient.ClientState = deserializer.Deserialize<InventorySlotsClientState>(File.ReadAllText(ClientStateFilePath)) ?? new InventorySlotsClientState();
            NormalizeClientState();
        }
        catch (Exception ex)
        {
            InventoryClient.ClientState = new InventorySlotsClientState();
            Log.LogWarning($"Failed to load InventorySlots client state from {ClientStateFilePath}: {ex.Message}");
        }
    }

    private static bool SaveClientState()
    {
        try
        {
            EnsureClientStateLoaded();
            NormalizeClientState();

            ISerializer serializer = new SerializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();

            Exception? compatibilityReason = LinkCompatibleFileWriter.WriteAllText(
                ClientStateFilePath,
                serializer.Serialize(InventoryClient.ClientState));
            if (compatibilityReason != null && !_clientStateDirectWriteFallbackLogged)
            {
                _clientStateDirectWriteFallbackLogged = true;
                Log.LogWarning(
                    $"InventorySlots client state is using link-compatible direct writes because atomic replacement is unavailable for {ClientStateFilePath} " +
                    $"({compatibilityReason.GetType().Name}, 0x{compatibilityReason.HResult:X8}).");
            }

            return true;
        }
        catch (Exception ex)
        {
            Log.LogWarning($"Failed to save InventorySlots client state: {ex.Message}");
            return false;
        }
    }

    private static InventorySlotsClientPlayerState? GetClientPlayerState(string playerId, bool create)
    {
        EnsureClientStateLoaded();
        if (InventoryClient.ClientState.Players.TryGetValue(playerId, out InventorySlotsClientPlayerState playerState))
        {
            return playerState;
        }

        if (!create)
        {
            return null;
        }

        playerState = new InventorySlotsClientPlayerState();
        InventoryClient.ClientState.Players[playerId] = playerState;
        return playerState;
    }

    private static void NormalizeClientState()
    {
        InventoryClient.ClientState = ClientStateCore.Normalize(InventoryClient.ClientState);
    }
}
