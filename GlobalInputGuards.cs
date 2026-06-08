using UnityEngine;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private static bool ShouldBlockGlobalHotkeys(Player? player = null)
    {
        if (player != null && (player.m_isLoading || ((Character)player).InCutscene()))
        {
            return true;
        }

        if (IsCraftingSearchFocused())
        {
            return true;
        }

        if (Chat.instance != null && !IsUnityNull(Chat.instance) && Chat.instance.HasFocus())
        {
            return true;
        }

        if (global::Console.IsVisible() ||
            TextInput.IsVisible() ||
            Menu.IsVisible() ||
            Minimap.IsOpen() ||
            Minimap.InTextInput() ||
            StoreGui.IsVisible() ||
            GameCamera.InFreeFly())
        {
            return true;
        }

        if (TextViewer.instance != null && !IsUnityNull(TextViewer.instance) && TextViewer.instance.IsVisible())
        {
            return true;
        }

        return ZNet.instance != null && !IsUnityNull(ZNet.instance) && ZNet.instance.InPasswordDialog();
    }
}
