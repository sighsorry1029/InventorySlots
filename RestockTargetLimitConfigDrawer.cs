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
            GUILayout.Label("Max", GUILayout.Width(28f));
            string amount = FilterUnsignedIntText(GUILayout.TextField(row.Amount, GUILayout.Width(58f)));
            bool remove = GUILayout.Button("-", GUILayout.Width(24f));
            GUILayout.EndHorizontal();

            if (remove)
            {
                RestockTargetLimitEditorRows.RemoveAt(i--);
                UpdateRestockTargetStackLimitsConfigEntry(entry);
                continue;
            }

            if (!string.Equals(item, row.Item, StringComparison.Ordinal) ||
                !string.Equals(amount, row.Amount, StringComparison.Ordinal))
            {
                row.Item = item;
                row.Amount = amount;
                UpdateRestockTargetStackLimitsConfigEntry(entry);
            }
        }

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("+", GUILayout.Width(24f)))
        {
            RestockTargetLimitEditorRows.Add(new RestockTargetLimitEditorRow("", ""));
            UpdateRestockTargetStackLimitsConfigEntry(entry);
        }

        GUILayout.Label("Add restock limit");
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
    }

    private static List<RestockTargetLimitEditorRow> ParseRestockTargetLimitEditorRows(string raw)
    {
        List<RestockTargetLimitEditorRow> rows = new();
        foreach (string entry in raw.Replace("\r", "\n").Split(new[] { '\n', ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
        {
            string trimmed = entry.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            int commentIndex = trimmed.IndexOf('#');
            if (commentIndex >= 0)
            {
                trimmed = trimmed.Substring(0, commentIndex).Trim();
            }

            if (trimmed.Length == 0)
            {
                continue;
            }

            int separator = FindRestockTargetLimitEditorSeparator(trimmed);
            if (separator <= 0)
            {
                rows.Add(new RestockTargetLimitEditorRow(trimmed, ""));
                continue;
            }

            rows.Add(new RestockTargetLimitEditorRow(
                trimmed.Substring(0, separator).Trim(),
                FilterUnsignedIntText(trimmed.Substring(separator + 1).Trim())));
        }

        return rows;
    }

    private static int FindRestockTargetLimitEditorSeparator(string entry)
    {
        int colon = entry.IndexOf(':');
        int equals = entry.IndexOf('=');
        if (colon < 0)
        {
            return equals;
        }

        if (equals < 0)
        {
            return colon;
        }

        return Math.Min(colon, equals);
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
                .Select(row => $"{row.Item.Trim()}: {row.Amount.Trim()}"));
    }

    private sealed class RestockTargetLimitEditorRow
    {
        public RestockTargetLimitEditorRow(string item, string amount)
        {
            Item = item;
            Amount = amount;
        }

        public string Item { get; set; }
        public string Amount { get; set; }
    }
}
