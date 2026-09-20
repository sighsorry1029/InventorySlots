using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using UnityEngine;

namespace InventoryActions;

public sealed partial class InventoryActionsPlugin
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
            bool refillEmpty = GUILayout.Toggle(row.RefillEmpty,
                new GUIContent("Refill empty", "Restock into one empty favorite slot when this item has no favorite stack. 0 disables restock."));
            bool remove = GUILayout.Button("-", GUILayout.Width(24f));
            GUILayout.EndHorizontal();

            if (remove)
            {
                RestockTargetLimitEditorRows.RemoveAt(i--);
                UpdateRestockTargetStackLimitsConfigEntry(entry);
                continue;
            }

            if (!string.Equals(item, row.Item, StringComparison.Ordinal) ||
                !string.Equals(amount, row.Amount, StringComparison.Ordinal) || refillEmpty != row.RefillEmpty)
            {
                row.Item = item;
                row.Amount = amount;
                row.RefillEmpty = refillEmpty;
                UpdateRestockTargetStackLimitsConfigEntry(entry);
            }
        }

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("+", GUILayout.Width(24f)))
        {
            RestockTargetLimitEditorRows.Add(new RestockTargetLimitEditorRow("", ""));
            UpdateRestockTargetStackLimitsConfigEntry(entry);
        }

        GUILayout.Label("Add restock target");
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
    }

    private static List<RestockTargetLimitEditorRow> ParseRestockTargetLimitEditorRows(string raw)
    {
        List<RestockTargetLimitEditorRow> rows = new();
        foreach (string entry in RestockTargetLimitCore.SplitEntries(raw))
        {
            string trimmed = RestockTargetLimitCore.StripInlineComment(entry).Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            int separator = RestockTargetLimitCore.FindSeparator(trimmed);
            if (separator <= 0)
            {
                rows.Add(new RestockTargetLimitEditorRow(trimmed, ""));
                continue;
            }

            RestockTargetLimitCore.SplitRuleValue(trimmed.Substring(separator + 1), out string amount, out bool refillEmpty);
            rows.Add(new RestockTargetLimitEditorRow(
                trimmed.Substring(0, separator).Trim(),
                RestockTargetLimitCore.NormalizeAmountForEditor(amount), refillEmpty));
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
                .Where(row => !string.IsNullOrWhiteSpace(row.Item) || !string.IsNullOrWhiteSpace(row.Amount))
                .Select(row => $"{row.Item.Trim()}: {row.Amount.Trim()}" + (row.RefillEmpty ? " | refill" : "")));
    }

    private sealed class RestockTargetLimitEditorRow
    {
        public RestockTargetLimitEditorRow(string item, string amount, bool refillEmpty = false)
        {
            Item = item;
            Amount = amount;
            RefillEmpty = refillEmpty;
        }

        public string Item { get; set; }
        public string Amount { get; set; }
        public bool RefillEmpty { get; set; }
    }
}
