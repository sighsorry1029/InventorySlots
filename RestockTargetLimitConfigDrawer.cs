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

        GUILayout.BeginVertical();
        for (int i = 0; i < RestockTargetLimitEditorRows.Count; i++)
        {
            RestockTargetLimitEditorRow row = RestockTargetLimitEditorRows[i];
            GUILayout.BeginHorizontal();
            GUILayout.Label("Item", GUILayout.Width(44f));
            string item = GUILayout.TextField(row.Item, GUILayout.MinWidth(130f));
            GUILayout.Label("Target", GUILayout.Width(44f));
            string amount = FilterUnsignedIntText(GUILayout.TextField(row.Amount, GUILayout.Width(58f)));
            RestockRuleMode mode = DrawRestockModeConfigButton(row.Mode);
            bool remove = GUILayout.Button("-", GUILayout.Width(24f));
            GUILayout.EndHorizontal();

            if (remove)
            {
                RestockTargetLimitEditorRows.RemoveAt(i--);
                UpdateRestockTargetStackLimitsConfigEntry(entry);
                continue;
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

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("+", GUILayout.Width(24f)))
        {
            RestockTargetLimitEditorRows.Add(new RestockTargetLimitEditorRow("", "1"));
            UpdateRestockTargetStackLimitsConfigEntry(entry);
        }

        GUILayout.Label("Add restock target");
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
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
