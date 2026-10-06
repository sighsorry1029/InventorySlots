using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace InventorySlots;

// A display snapshot, never an inventory payload. No item or mod custom data is
// deserialized on the receiving client; stats and language are the sender's.
internal sealed class ItemLinkSnapshot
{
    internal string Prefab = "";
    internal int Variant;
    internal string Label = "";
    internal string Body = "";
    internal bool Preview;
}

internal static class ItemLinkWire
{
    internal const int MaxBytes = 32768;
    internal const int MaxBody = 12000;
    internal const int MaxLinks = 3;
    private static readonly Regex Marker = new(@"\[([^\[\]<>\r\n]{1,120}) #IS:([0-9a-f]{16})\]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex Hex = new(@"\A[0-9a-f]{16}\z", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex Color = new(@"\A#[0-9a-f]{6}([0-9a-f]{2})?\z", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Dictionary<string, string> NamedColors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["orange"] = "#FFA500", ["yellow"] = "#FFFF00", ["white"] = "#FFFFFF",
        ["red"] = "#FF0000", ["green"] = "#008000", ["lime"] = "#00FF00",
        ["cyan"] = "#00FFFF", ["blue"] = "#0000FF", ["purple"] = "#800080",
        ["grey"] = "#808080", ["gray"] = "#808080", ["black"] = "#000000",
        ["silver"] = "#C0C0C0", ["magenta"] = "#FF00FF", ["lightblue"] = "#ADD8E6"
    };

    internal static bool IsToken(string? token) => token != null && Hex.IsMatch(token);
    internal static string NewToken() => Guid.NewGuid().ToString("N").Substring(0, 16);
    internal static string MarkerText(string label, string token) => $"[{Plain(label, 100)} #IS:{token}]";

    internal static string Rewrite(string body, Func<string, string, string> replace)
    {
        if (body.Length > 4096) return body;
        int count = 0;
        return Marker.Replace(body, match => ++count <= MaxLinks
            ? replace(match.Groups[2].Value.ToLowerInvariant(), match.Groups[1].Value)
            : match.Value);
    }

    internal static string Plain(string? value, int max)
    {
        string safe = Rich(value ?? "", false).Replace('[', '(').Replace(']', ')');
        safe = new string(safe.Select(c => char.IsWhiteSpace(c) ? ' ' : c).ToArray());
        if (safe.Length <= max) return safe;
        int length = max > 0 && char.IsHighSurrogate(safe[max - 1]) ? max - 1 : max;
        return safe.Substring(0, length);
    }

    // Keep only balanced emphasis and opaque colors. Layout, sprite, link,
    // material, noparse, etc. tags from remote text cannot affect our UI.
    internal static string Rich(string value, bool formatting = true)
    {
        StringBuilder result = new();
        Stack<string> stack = new();
        bool noClosingBracket = false;
        for (int i = 0; i < value.Length; i++)
        {
            char ch = value[i];
            if (ch == '<')
            {
                int end = noClosingBracket ? -1 : value.IndexOf('>', i + 1);
                if (end < 0) { noClosingBracket = true; continue; }
                string tag = value.Substring(i + 1, end - i - 1).ToLowerInvariant();
                i = end;
                if (!formatting) continue;
                if (tag == "br" || tag == "br/") { result.Append('\n'); continue; }
                if (tag.StartsWith("/") && stack.Count > 0 && stack.Peek() == tag.Substring(1))
                    result.Append("</").Append(stack.Pop()).Append('>');
                else if (stack.Count < 8 && (tag == "b" || tag == "i"))
                {
                    stack.Push(tag);
                    result.Append('<').Append(tag).Append('>');
                }
                else if (stack.Count < 8 && tag.StartsWith("color="))
                {
                    string color = tag.Substring(6).Trim('"', '\'');
                    if (NamedColors.TryGetValue(color, out string? named)) color = named;
                    if (!Color.IsMatch(color)) continue;
                    stack.Push("color");
                    result.Append("<color=").Append(color.Substring(0, 7)).Append('>');
                }
                continue;
            }
            if (ch == '>' || char.IsControl(ch) && ch != '\n' && ch != '\t' ||
                char.GetUnicodeCategory(ch) == System.Globalization.UnicodeCategory.Format) continue;
            result.Append(ch == '\u2028' || ch == '\u2029' ? '\n' : ch);
        }
        while (stack.Count > 0) result.Append("</").Append(stack.Pop()).Append('>');
        return result.ToString();
    }

    internal static byte[] Encode(ItemLinkSnapshot value)
    {
        if (value.Body.Length > MaxBody || value.Body.Count(c => c == '\n') > 200 || value.Label.Length > 120 || value.Prefab.Length > 128 || value.Variant < 0)
            throw new InvalidDataException("Item link is too large.");
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream, Encoding.UTF8);
        writer.Write((byte)1);
        writer.Write(value.Preview);
        writer.Write(value.Variant);
        WriteString(writer, value.Prefab);
        WriteString(writer, value.Label);
        WriteString(writer, value.Body);
        if (stream.Length > MaxBytes) throw new InvalidDataException("Item link is too large.");
        return stream.ToArray();
    }

    internal static ItemLinkSnapshot? Decode(byte[] data)
    {
        if (data.Length > MaxBytes) return null;
        try
        {
            using MemoryStream stream = new(data, false);
            using BinaryReader reader = new(stream, new UTF8Encoding(false, true));
            if (reader.ReadByte() != 1) return null;
            byte preview = reader.ReadByte();
            int variant = reader.ReadInt32();
            if (preview > 1 || variant < 0 || variant > 1000000) return null;
            ItemLinkSnapshot result = new()
            {
                Preview = preview == 1, Variant = variant,
                Prefab = ReadString(reader, 128), Label = ReadString(reader, 120), Body = ReadString(reader, MaxBody)
            };
            if (stream.Position != stream.Length) return null;
            result.Label = Plain(result.Label, 120);
            result.Body = Rich(result.Body);
            // Canonical formatting can grow text (named colors, closing tags).
            // Validate the result too, before permission callbacks or layout.
            if (result.Body.Length > MaxBody || result.Body.Count(c => c == '\n') > 200 || Encode(result).Length > MaxBytes) return null;
            return result;
        }
        catch (Exception ex) when (ex is IOException || ex is InvalidDataException || ex is ArgumentException || ex is OverflowException)
        {
            return null;
        }
    }

    private static void WriteString(BinaryWriter writer, string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }

    private static string ReadString(BinaryReader reader, int maxCharacters)
    {
        int size = reader.ReadInt32();
        if (size < 0 || size > maxCharacters * 4 || size > reader.BaseStream.Length - reader.BaseStream.Position)
            throw new InvalidDataException();
        string value = new UTF8Encoding(false, true).GetString(reader.ReadBytes(size));
        if (value.Length > maxCharacters) throw new InvalidDataException();
        return value;
    }

    internal static bool InsertDraft(string draft, int anchor, int focus, string marker, int limit, out string next, out int caret)
    {
        int start = Math.Max(0, Math.Min(draft.Length, Math.Min(anchor, focus)));
        int end = Math.Max(start, Math.Min(draft.Length, Math.Max(anchor, focus)));
        string before = draft.Substring(0, start);
        string after = draft.Substring(end);
        string insert = (before.Length > 0 && !char.IsWhiteSpace(before[before.Length - 1]) ? " " : "") + marker +
                        (after.Length > 0 && !char.IsWhiteSpace(after[0]) ? " " : "");
        next = before + insert + after;
        caret = start + insert.Length;
        return limit <= 0 || next.Length <= limit;
    }
}

// Small session-only caches. A response is accepted only for a link observed in
// native or server-attested clan chat, from that sender, for our outstanding nonce.
internal sealed class ItemLinkStore
{
    internal const double Lifetime = 600;
    internal sealed class Entry
    {
        internal long Sender;
        internal string Token = "", Label = "", Nonce = "";
        internal string ClanId = "";
        internal ItemLinkSnapshot? Snapshot;
        internal double Created, Requested;
        internal int Attempts;
        internal bool CheckingResponse;
    }
    private sealed class Offer
    {
        internal byte[] Data = Array.Empty<byte>();
        internal double Created;
        internal bool Sent;
        internal string ClanId = "";
    }
    private readonly Dictionary<string, Offer> _offers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Entry> _received = new(StringComparer.Ordinal);
    private double _responseWindow;
    private int _responses;
    private readonly Dictionary<long, int> _peerResponses = new();
    internal int Count => _received.Count;
    internal static string Key(long sender, string token) => sender.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + token;

    internal void Clear() { _offers.Clear(); _received.Clear(); _responses = 0; _responseWindow = 0; _peerResponses.Clear(); }
    internal void AddOffer(string token, byte[] data, double now)
    {
        Prune(now);
        if (_offers.Count >= 64) _offers.Remove(_offers.OrderBy(p => p.Value.Created).First().Key);
        _offers[token] = new Offer { Data = data, Created = now };
    }
    internal void MarkSent(string body, double now) => ItemLinkWire.Rewrite(body, (token, label) =>
    {
        if (_offers.TryGetValue(token, out Offer? offer) && now - offer.Created <= Lifetime) offer.Sent = true;
        return "";
    });
    // A private send never grants the native/public offer. Only the accepted echo
    // can publish a clan offer; merely inserting a draft or queuing a send cannot.
    internal void MarkClanSent(string body, string clanId, double now) => ItemLinkWire.Rewrite(body, (token, label) =>
    {
        if (clanId.Length > 0 && _offers.TryGetValue(token, out Offer? offer) && now - offer.Created <= Lifetime)
            offer.ClanId = clanId;
        return "";
    });
    internal void ClearClan()
    {
        foreach (Offer offer in _offers.Values) offer.ClanId = "";
        foreach (string key in _received.Where(pair => pair.Value.ClanId.Length > 0).Select(pair => pair.Key).ToArray())
            _received.Remove(key);
    }
    internal Entry Observe(long sender, string token, string label, double now, string clanId = "")
    {
        string key = Key(sender, token);
        if (_received.TryGetValue(key, out Entry? entry) && now - entry.Created <= Lifetime)
        {
            // A native delivery of the same link makes it independent of clan scope.
            if (clanId.Length == 0) entry.ClanId = "";
            return entry;
        }
        Prune(now);
        if (_received.Count >= 128) _received.Remove(_received.OrderBy(p => p.Value.Created).First().Key);
        return _received[key] = new Entry { Sender = sender, Token = token, Label = label, Created = now, ClanId = clanId };
    }
    internal Entry? Find(string key, double now) => _received.TryGetValue(key, out Entry? entry) && now - entry.Created <= Lifetime ? entry : null;
    internal bool Request(Entry entry, double now)
    {
        if (Find(Key(entry.Sender, entry.Token), now) != entry || entry.Snapshot != null || entry.Attempts >= 3 ||
            entry.Attempts > 0 && now - entry.Requested < 4) return false;
        entry.Nonce = ItemLinkWire.NewToken();
        entry.Requested = now;
        entry.CheckingResponse = false;
        entry.Attempts++;
        return true;
    }
    internal byte[]? GetOffer(string token, double now, long requester = 0, Func<string, long, bool>? canShareClan = null)
    {
        if (!_offers.TryGetValue(token, out Offer? offer) || now - offer.Created > Lifetime) return null;
        return offer.Sent || offer.ClanId.Length > 0 && canShareClan?.Invoke(offer.ClanId, requester) == true
            ? offer.Data : null;
    }
    internal bool AllowResponse(long peer, double now)
    {
        if (now - _responseWindow >= 10) { _responseWindow = now; _responses = 0; _peerResponses.Clear(); }
        if (_responses >= 30) return false;
        _peerResponses.TryGetValue(peer, out int count);
        if (count >= 6) return false;
        _peerResponses[peer] = count + 1;
        ++_responses;
        return true;
    }
    internal bool BeginResponse(long sender, string token, string nonce, double now)
    {
        Entry? entry = Find(Key(sender, token), now);
        if (!MatchesResponse(entry, nonce, now) || entry!.CheckingResponse) return false;
        entry.CheckingResponse = true;
        return true;
    }
    private static bool MatchesResponse(Entry? entry, string nonce, double now) =>
        entry != null && entry.Snapshot == null && entry.Nonce.Length > 0 && entry.Nonce == nonce && now - entry.Requested <= 12;
    internal bool Accept(long sender, string token, string nonce, byte[] data, double now)
    {
        Entry? entry = Find(Key(sender, token), now);
        if (!MatchesResponse(entry, nonce, now) || !entry!.CheckingResponse) return false;
        ItemLinkSnapshot? snapshot = ItemLinkWire.Decode(data);
        if (snapshot == null) return false;
        entry.Snapshot = snapshot;
        entry.Nonce = "";
        return true;
    }
    internal void Prune(double now)
    {
        foreach (string key in _offers.Where(p => now - p.Value.Created > Lifetime).Select(p => p.Key).ToArray()) _offers.Remove(key);
        foreach (string key in _received.Where(p => now - p.Value.Created > Lifetime).Select(p => p.Key).ToArray()) _received.Remove(key);
    }
}
