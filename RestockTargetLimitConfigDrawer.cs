using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using UnityEngine;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private static readonly List<RestockTargetLimitEditorRow> RestockTargetLimitEditorRows = new();
    private static string _restockTargetLimitEditorLastValue = "";

    private static void DrawRestockTargetStackLimitsConfig(ConfigEntryBase entry)
    {
        string currentValue = entry.BoxedValue as string ?? "";
        if (!string.Equals(currentValue, _restockTargetLimitEditorLastValue, StringComparison.Ordinal))
        {
            RestockTargetLimitEditorRows.Clear();
            RestockTargetLimitEditorRows.AddRange(ParseRestockTargetLimitEditorRows(currentValue));
            _restockTargetLimitEditorLastValue = currentValue;
        }

        const float gap = 4f;
        float width = GetConfigDrawerLayoutWidth(entry);
        float lineHeight = ConfigDrawerLineHeight();
        bool stacked = width < 126f + Mathf.Max(72f, GUI.skin.textField.fontSize * 4.5f);
        float rowHeight = stacked ? lineHeight * 2f + gap : lineHeight;
        Rect area = ReserveConfigDrawerArea(entry, RestockTargetLimitEditorRows.Count * (rowHeight + gap) + lineHeight);
        GUI.BeginGroup(area);
        try
        {
            for (int i = 0; i < RestockTargetLimitEditorRows.Count; i++)
            {
                RestockTargetLimitEditorRow row = RestockTargetLimitEditorRows[i];
                string item = row.Item, amount = row.Amount;
                RestockRuleMode mode = row.Mode;
                bool remove = DrawRestockTargetConfigRow(
                    new Rect(0f, i * (rowHeight + gap), Mathf.Max(0f, area.width), rowHeight),
                    stacked, ref item, ref amount, ref mode);
                if (remove)
                {
                    RestockTargetLimitEditorRows.RemoveAt(i);
                    UpdateRestockTargetStackLimitsConfigEntry(entry);
                    break;
                }

                if (!string.Equals(item, row.Item, StringComparison.Ordinal) ||
                    !string.Equals(amount, row.Amount, StringComparison.Ordinal) || mode != row.Mode)
                {
                    row.Item = item;
                    row.Amount = amount;
                    row.Mode = mode;
                    UpdateRestockTargetStackLimitsConfigEntry(entry);
                }
            }

            Rect add = new(0f, RestockTargetLimitEditorRows.Count * (rowHeight + gap), Mathf.Max(0f, area.width), lineHeight);
            if (GUI.Button(add, "+ Add restock target"))
            {
                RestockTargetLimitEditorRows.Add(new RestockTargetLimitEditorRow("", "1"));
                UpdateRestockTargetStackLimitsConfigEntry(entry);
            }
        }
        finally { GUI.EndGroup(); }
    }

    private static List<RestockTargetLimitEditorRow> ParseRestockTargetLimitEditorRows(string raw)
    {
        List<RestockTargetLimitEditorRow> rows = new();
        foreach (ItemRuleConfigCore.Entry entry in ItemRuleConfigCore.Read(raw, true))
        {
            rows.Add(new RestockTargetLimitEditorRow(entry.Key, entry.Amount, entry.Mode));
        }

        return rows;
    }

    private static string FilterUnsignedIntText(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? ""
            : new string(value.Where(char.IsDigit).ToArray());
    }

    private static void UpdateRestockTargetStackLimitsConfigEntry(ConfigEntryBase entry)
    {
        // Keep an incomplete numeric edit local; never replace a disabled rule
        // with a temporarily invalid line that would use the default target.
        if (RestockTargetLimitEditorRows.Any(row => !string.IsNullOrWhiteSpace(row.Item) &&
            (!int.TryParse(row.Amount, out int amount) || amount < 1))) return;
        string nextValue = SerializeRestockTargetLimitEditorRows();
        _restockTargetLimitEditorLastValue = nextValue;
        if (!string.Equals(entry.BoxedValue as string ?? "", nextValue, StringComparison.Ordinal))
        {
            entry.BoxedValue = nextValue;
        }
    }

    private static string SerializeRestockTargetLimitEditorRows()
    {
        return string.Join(
            "\n",
            RestockTargetLimitEditorRows
                .Where(row => !string.IsNullOrWhiteSpace(row.Item))
                .Select(row => $"{row.Item.Trim()}: " + RestockTargetLimitCore.FormatRuleValue(row.Amount.Trim(), row.Mode)));
    }

    private sealed class RestockTargetLimitEditorRow
    {
        public RestockTargetLimitEditorRow(string item, string amount, RestockRuleMode mode = RestockRuleMode.Existing)
        {
            Item = item;
            Amount = amount;
            Mode = mode;
        }

        public string Item { get; set; }
        public string Amount { get; set; }
        public RestockRuleMode Mode { get; set; }
    }
}
