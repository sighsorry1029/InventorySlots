using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using UnityEngine;
using ItemData = ItemDrop.ItemData;

namespace InventorySlots;

public sealed partial class InventorySlotsPlugin
{
    private static Func<ItemData, IEnumerable>? _povAffixRows;
    private static Func<object, string>? _povAffixName;
    private static Func<object, string>? _povAffixEffect;
    private static Func<object, Color>? _povAffixColor;
    private static Func<ItemData, Color>? _povNameColor;
    private static bool _povItemPresentationWarning;

    private static void BindPathOfValheimanItemPresentation(Assembly assembly)
    {
        Type rowType = assembly.GetType("Valheiman.Api.AffixRow", true)!;
        Type rowsType = typeof(IReadOnlyList<>).MakeGenericType(rowType);
        var item = Expression.Parameter(typeof(ItemData), "item");
        var rows = Expression.Variable(rowsType, "rows");
        MethodInfo describe = _pov!.Require("Valheiman.Api.Tooltip", "Describe", typeof(string), typeof(ItemData), rowsType.MakeByRefType());
        var read = Expression.Lambda<Func<ItemData, IEnumerable>>(Expression.Block(new[] { rows },
            Expression.Call(describe, item, rows), Expression.Convert(rows, typeof(IEnumerable))), item).Compile();
        var name = BindRow<string>("Name");
        var effect = BindRow<string>("Effect");
        var tint = BindRow<Color>("Tint");
        Type rarity = assembly.GetType("Valheiman.Api.Rarity", true)!;
        MethodInfo rarityOf = _pov.Require("Valheiman.Api.Items", "RarityOf", rarity, typeof(ItemData));
        MethodInfo nameColor = _pov.Require("Valheiman.Api.Items", "RarityNameColor", typeof(Color), rarity);
        var color = Expression.Lambda<Func<ItemData, Color>>(Expression.Call(nameColor, Expression.Call(rarityOf, item)), item).Compile();
        _povAffixRows = read;
        _povAffixName = name;
        _povAffixEffect = effect;
        _povAffixColor = tint;
        _povNameColor = color;

        Func<object, T> BindRow<T>(string property)
        {
            var value = Expression.Parameter(typeof(object), "row");
            PropertyInfo field = rowType.GetProperty(property) ?? throw new MissingMemberException(rowType.FullName, property);
            if (field.PropertyType != typeof(T) || field.GetGetMethod() == null) throw new MissingMemberException(rowType.FullName, property);
            return Expression.Lambda<Func<object, T>>(Expression.Property(Expression.Convert(value, rowType), field), value).Compile();
        }
    }

    // Called only by the existing tooltip refresh cadence, never a GetTooltip
    // postfix: PoV Describe itself calls GetTooltip while constructing its model.
    private static string AppendPathOfValheimanAffixes(ItemData item, string body)
    {
        if (_povAffixRows == null) return body;
        try
        {
            var text = new StringBuilder(body);
            string existing = StripRichText(body).Trim();
            foreach (object row in _povAffixRows(item))
            {
                string name = _povAffixName!(row) ?? "";
                string effect = _povAffixEffect!(row) ?? "";
                // Describe can also include another mod's effects, already present
                // in the native item tooltip. Keep that mod's original wording once.
                string plain = StripRichText(string.IsNullOrWhiteSpace(effect) ? name : effect).Trim();
                if (plain.Length == 0 || existing.IndexOf(plain, StringComparison.Ordinal) >= 0) continue;
                text.Append("\n<color=#").Append(ColorUtility.ToHtmlStringRGB(_povAffixColor!(row))).Append('>');
                if (!string.IsNullOrWhiteSpace(name)) text.Append(name).Append(string.IsNullOrWhiteSpace(effect) ? "" : ": ");
                text.Append(effect).Append("</color>");
            }
            return text.ToString();
        }
        catch (Exception error) { WarnPathOfValheimanItemPresentation(error); return body; }
    }

    private static string PathOfValheimanItemNameColor(ItemData item)
    {
        try { return _povNameColor == null ? "FFD36A" : ColorUtility.ToHtmlStringRGB(_povNameColor(item)); }
        catch (Exception error) { WarnPathOfValheimanItemPresentation(error); return "FFD36A"; }
    }

    private static void WarnPathOfValheimanItemPresentation(Exception error)
    {
        if (_povItemPresentationWarning) return;
        _povItemPresentationWarning = true;
        Log.LogWarning("PoV item presentation unavailable: " + error.GetBaseException().Message);
    }

    private static void ClearPathOfValheimanItemPresentation()
    {
        _povAffixRows = null;
        _povAffixName = _povAffixEffect = null;
        _povAffixColor = null;
        _povNameColor = null;
        _povItemPresentationWarning = false;
    }
}
