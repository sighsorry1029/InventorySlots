using System.Runtime.CompilerServices;
using BepInEx.Configuration;
using UnityEngine;

#if INVENTORY_SLOTS
namespace InventorySlots;
public sealed partial class InventorySlotsPlugin
#else
namespace InventoryActions;
public sealed partial class InventoryActionsPlugin
#endif
{
    private sealed class ConfigDrawerMeasurement
    {
        internal float MeasuredWidth = 240f;
        internal float LayoutWidth = 240f;
    }

    // ConfigManager's Edit dialog uses a different ConfigEntry object. Weak
    // keys keep its measurement separate without retaining closed dialogs.
    private static readonly ConditionalWeakTable<ConfigEntryBase, ConfigDrawerMeasurement> ConfigDrawerMeasurements = new();

    private static float GetConfigDrawerLayoutWidth(ConfigEntryBase entry)
    {
        ConfigDrawerMeasurement measurement = ConfigDrawerMeasurements.GetOrCreateValue(entry);
        if (Event.current.type == EventType.Layout) measurement.LayoutWidth = measurement.MeasuredWidth;
        return Mathf.Max(1f, measurement.LayoutWidth);
    }

    private static Rect ReserveConfigDrawerArea(ConfigEntryBase entry, float height)
    {
        // A zero minimum width lets the host assign its remaining value column.
        // Only GUI controls go inside: their text cannot enlarge that column.
        Rect area = GUILayoutUtility.GetRect(0f, 10000f, height, height, GUILayout.ExpandWidth(true));
        if (Event.current.type == EventType.Repaint && area.width > 0f)
            ConfigDrawerMeasurements.GetOrCreateValue(entry).MeasuredWidth = area.width;
        return area;
    }

    private static float ConfigDrawerLineHeight() => Mathf.Max(32f,
        Mathf.Max(GUI.skin.textField.CalcHeight(new GUIContent("Ag"), 10000f),
            GUI.skin.button.CalcHeight(new GUIContent("Ag"), 10000f)));

    private static GUIStyle ConfigDrawerWrappedLabelStyle() => new(GUI.skin.label)
    {
        wordWrap = true, clipping = TextClipping.Clip, fixedWidth = 0f, fixedHeight = 0f
    };

    private static bool DrawRestockTargetConfigRow(Rect row, bool stacked, ref string item, ref string amount, ref RestockRuleMode mode)
    {
        const float gap = 4f;
        float lineHeight = stacked ? (row.height - gap) * 0.5f : row.height;
        // During a resize, the old height is used until the next Layout pass.
        // Keep all four controls inside even that transitional rectangle.
        float scale = Mathf.Min(1f, row.width / (stacked ? 64f : 150f));
        float spacing = gap * scale;
        float modeWidth = 32f * scale;
        float removeWidth = 24f * scale;
        float amountWidth = stacked ? Mathf.Max(0f, row.width - modeWidth - removeWidth - 2f * spacing) : 58f * scale;
        float itemWidth = stacked ? row.width : Mathf.Max(0f, row.width - modeWidth - amountWidth - removeWidth - 3f * spacing);
        Rect itemRect = new(row.x, row.y, itemWidth, lineHeight);
        float controlsY = row.y + (stacked ? lineHeight + gap : 0f);
        float controlsX = row.x + (stacked ? 0f : itemWidth + spacing);
        Rect modeRect = new(controlsX, controlsY, modeWidth, lineHeight);
        Rect amountRect = new(modeRect.xMax + spacing, controlsY, amountWidth, lineHeight);
        Rect removeRect = new(amountRect.xMax + spacing, controlsY, removeWidth, lineHeight);
        item = GUI.TextField(itemRect, item);
        GUI.Label(itemRect, new GUIContent("", "Item prefab or name"), GUIStyle.none);
        mode = DrawRestockModeConfigButton(mode, modeRect);
        amount = FilterUnsignedIntText(GUI.TextField(amountRect, amount));
        GUI.Label(amountRect, new GUIContent("", "Target quantity (at least 1)"), GUIStyle.none);
        return GUI.Button(removeRect, new GUIContent("×", "Remove restock target"));
    }
}
