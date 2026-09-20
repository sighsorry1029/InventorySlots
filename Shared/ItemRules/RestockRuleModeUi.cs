using UnityEngine;
using Object = UnityEngine.Object;

#if INVENTORY_SLOTS
namespace InventorySlots;
public sealed partial class InventorySlotsPlugin
#else
namespace InventoryActions;
public sealed partial class InventoryActionsPlugin
#endif
{
    private static readonly Sprite?[] RestockRuleModeIcons = new Sprite?[3];

    private static string RestockModeText(string key, string fallback) =>
        LocalizeUi("$" + ModName.ToLowerInvariant() + "_rules_mode_" + key, fallback);

    private static string GetRestockModeTitle(RestockRuleMode mode) => mode switch
    {
        RestockRuleMode.Off => RestockModeText("off", "Restock: Off"),
        RestockRuleMode.Existing => RestockModeText("existing", "Restock: Existing stacks"),
        _ => RestockModeText("includeempty", "Restock: Include empty slots")
    };

    private static string GetRestockModeHelp(RestockRuleMode mode) => (mode switch
    {
        RestockRuleMode.Off => RestockModeText("off_help", "Do not restock this item. Keep the configured target quantity."),
        RestockRuleMode.Existing => RestockModeText("existing_help", "Only top up this item's existing favorite stacks to the target quantity."),
        _ => RestockModeText("includeempty_help", "Also restore remembered empty favorite slots. If no slot is remembered and no favorite stack remains, use one unassigned empty favorite slot. Occupied slots are never replaced.")
    }) + "\n" + RestockModeText("cycle", "Click to change: Off → Existing → Include empty.");

    private static Sprite GetRestockModeIcon(RestockRuleMode mode)
    {
        int index = (int)mode;
        if (RestockRuleModeIcons[index] != null) return RestockRuleModeIcons[index]!;
        Color[] pixels = new Color[64 * 64];
        Color color = new(1f, 0.78f, 0.32f, 1f);
        if (mode == RestockRuleMode.Off) DrawTrashLine(pixels, 64, 18, 32, 46, 32, 2, color);
        else DrawRestockSymbol(pixels, color, mode == RestockRuleMode.IncludeEmpty);
        Texture2D texture = new(64, 64, TextureFormat.RGBA32, false)
        { name = ModName + "_RestockMode_" + mode, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        texture.SetPixels(pixels); texture.Apply(false, true);
        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f), 100);
        RestockRuleModeIcons[index] = sprite;
        return sprite;
    }

    private static void DrawRestockSymbol(Color[] pixels, Color color, bool includeParcel)
    {
        // Same top-down geometry as the main Restock button. Only the parcel
        // differs between the two enabled rule modes.
        void Line(int x, int y, int xx, int yy) => DrawTrashLine(pixels, 64, x, y, xx, yy, 1, color);
        void Arc(float from, float to)
        {
            int x = Mathf.RoundToInt(32 + 22 * Mathf.Cos(from * Mathf.Deg2Rad));
            int y = Mathf.RoundToInt(32 + 22 * Mathf.Sin(from * Mathf.Deg2Rad));
            for (float angle = from + 5; angle <= to; angle += 5)
            {
                int xx = Mathf.RoundToInt(32 + 22 * Mathf.Cos(angle * Mathf.Deg2Rad));
                int yy = Mathf.RoundToInt(32 + 22 * Mathf.Sin(angle * Mathf.Deg2Rad));
                Line(x, y, xx, yy); x = xx; y = yy;
            }
        }
        Arc(-70, 90); Line(32, 54, 38, 49); Line(32, 54, 38, 59);
        Arc(110, 270); Line(32, 10, 26, 5); Line(32, 10, 26, 15);
        if (!includeParcel) return;
        Line(23, 27, 32, 22); Line(32, 22, 41, 27); Line(41, 27, 41, 38);
        Line(41, 38, 32, 43); Line(32, 43, 23, 38); Line(23, 38, 23, 27);
        Line(23, 27, 32, 32); Line(32, 32, 41, 27); Line(32, 32, 32, 43);
    }

    private static RestockRuleMode DrawRestockModeConfigButton(RestockRuleMode mode)
    {
        bool clicked = GUILayout.Button(new GUIContent("", GetRestockModeTitle(mode) + "\n" + GetRestockModeHelp(mode)),
            GUILayout.Width(32f), GUILayout.Height(32f));
        if (clicked) mode = RestockTargetLimitCore.NextMode(mode);
        if (Event.current.type == EventType.Repaint)
        {
            Rect rect = GUILayoutUtility.GetLastRect();
            rect.x += 3; rect.y += 3; rect.width -= 6; rect.height -= 6;
            GUI.DrawTexture(rect, GetRestockModeIcon(mode).texture, ScaleMode.ScaleToFit, true);
        }
        return mode;
    }

    private static void DestroyRestockModeIcons()
    {
        for (int i = 0; i < RestockRuleModeIcons.Length; i++)
        {
            Sprite? sprite = RestockRuleModeIcons[i];
            if (sprite != null) { Object.Destroy(sprite.texture); Object.Destroy(sprite); }
            RestockRuleModeIcons[i] = null;
        }
    }
}
