using System;
using System.IO;
using BepInEx;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
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

    private static void SaveClientState()
    {
        try
        {
            EnsureClientStateLoaded();
            NormalizeClientState();
            Directory.CreateDirectory(Path.GetDirectoryName(ClientStateFilePath)!);

            ISerializer serializer = new SerializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();

            string tempPath = ClientStateFilePath + ".tmp";
            File.WriteAllText(tempPath, serializer.Serialize(InventoryClient.ClientState));
            if (File.Exists(ClientStateFilePath))
            {
                File.Delete(ClientStateFilePath);
            }

            File.Move(tempPath, ClientStateFilePath);
        }
        catch (Exception ex)
        {
            Log.LogWarning($"Failed to save InventorySlots client state: {ex.Message}");
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
