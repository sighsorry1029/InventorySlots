using System;
using System.IO;
using System.Linq;
using InventorySlots;

int checks = 0;
void Check(bool value, string reason) { checks++; if (!value) throw new Exception(reason); }
CursorChecks.Run(Check);
ClanChecks.Run(Check);
ItemLinkUiChecks.Run(Check);
ItemLinkSnapshot snapshot = new() { Prefab = "Hammer", Label = "망치 ×1 · Q2", Body = "<color=orange>망치</color>\nDurability: 200\n<i>Epic effect</i>", Variant = 2, Preview = true };
byte[] data = ItemLinkWire.Encode(snapshot);
ItemLinkSnapshot? decoded = ItemLinkWire.Decode(data);
Check(decoded != null && decoded.Preview && decoded.Variant == 2 && decoded.Prefab == "Hammer" && decoded.Label == snapshot.Label, "snapshot identity roundtrip");
Check(decoded!.Body == "<color=#FFA500>망치</color>\nDurability: 200\n<i>Epic effect</i>", "preserves text/effect/known colors");
Check(ItemLinkWire.Rich("<size=9999><link=evil><sprite=4><b>x</link></size>") == "<b>x</b>", "remote layout/link/sprite tags removed, emphasis balanced");
Check(ItemLinkWire.Rich("<color=#ff00ff00>x</color>") == "<color=#ff00ff>x</color>", "remote text cannot hide via alpha");
Check(ItemLinkWire.Rich("<b><i>x</b></i>") == "<b><i>x</i></b>", "mismatched closing tags isolated");
Check(ItemLinkWire.Plain("[A]\tB\u2028C\u2029D\u202E<color=red>E</color>", 100) == "(A) B C DE", "single line, no markup/bidi");
Check(ItemLinkWire.Plain("ab\U0001F600", 3) == "ab", "label truncation preserves surrogate pair");
string token = "0123456789abcdef";
string marker = ItemLinkWire.MarkerText("Hammer", token);
foreach (string value in new[] { marker, marker.ToUpperInvariant(), marker.ToLowerInvariant() })
    Check(ItemLinkWire.Rewrite(value, (t, label) => t) == token, "normal/shout/whisper retain token");
int replacements = 0;
ItemLinkWire.Rewrite(string.Join(" ", Enumerable.Repeat(marker, 5)), (t, label) => { replacements++; return "link"; });
Check(replacements == 3, "links per chat message bounded");
Check(ItemLinkWire.Rewrite("[x #IS:0123456789abcdef0]", (_, _) => "BAD") != "BAD", "strict marker length");
Check(ItemLinkWire.InsertDraft("/s selling today", 11, 16, marker, 200, out string draft, out int caret) && draft == "/s selling " + marker && caret == draft.Length, "selection insertion preserves shout prefix");
Check(ItemLinkWire.InsertDraft("/w hello", 8, 8, marker, 200, out draft, out caret) && draft == "/w hello " + marker, "appending preserves whisper prefix");
Check(!ItemLinkWire.InsertDraft("keep", 4, 4, marker, 8, out _, out _), "full draft is rejected without truncating marker");
Check(ItemLinkWire.InsertDraft("abcXYZdef", 6, 3, marker, 200, out draft, out _) && draft == "abc " + marker + " def", "reverse selection and readable spacing");

Check(ItemLinkWire.Decode(data.Concat(new byte[] { 0 }).ToArray()) == null, "trailing data rejected");
Check(ItemLinkWire.Decode(new byte[ItemLinkWire.MaxBytes + 1]) == null, "packet budget checked first");
for (int i = 0; i < data.Length; i++) Check(ItemLinkWire.Decode(data.Take(i).ToArray()) == null, "truncated packet " + i);
byte[] malformed = (byte[])data.Clone();
malformed[0] = 2; Check(ItemLinkWire.Decode(malformed) == null, "unknown protocol version");
malformed = (byte[])data.Clone(); malformed[1] = 2; Check(ItemLinkWire.Decode(malformed) == null, "invalid bool enum");
malformed = (byte[])data.Clone(); Array.Copy(BitConverter.GetBytes(int.MaxValue), 0, malformed, 6, 4);
Check(ItemLinkWire.Decode(malformed) == null, "claimed huge string cannot allocate");
snapshot.Body = new string('x', 11997) + "<b>";
Check(ItemLinkWire.Decode(ItemLinkWire.Encode(snapshot)) == null, "sanitizer expansion must still fit limit");
snapshot.Body = new string('\n', 201);
bool threw = false; try { ItemLinkWire.Encode(snapshot); } catch (InvalidDataException) { threw = true; }
Check(threw, "too many lines rejected before UI layout");
Random rng = new(7821);
for (int i = 0; i < 1000; i++)
{
    byte[] fuzz = new byte[rng.Next(0, 500)]; rng.NextBytes(fuzz);
    Check(ItemLinkWire.Decode(fuzz) == null, "malformed packet safely rejected " + i);
}

ItemLinkStore store = new();
store.AddOffer(token, data, 0);
Check(store.GetOffer(token, 0) == null, "unsent draft not available over RPC");
store.MarkSent("normal " + marker, 1);
Check(store.GetOffer(token, 1) != null, "native send makes owned snapshot available");
ItemLinkStore.Entry entry = store.Observe(42, token, "hammer", 2);
Check(store.Request(entry, 2), "first hover requests snapshot");
string nonce = entry.Nonce;
Check(!store.Request(entry, 3), "hover does not spam requests");
Check(!store.BeginResponse(99, token, nonce, 3), "other peer cannot fill link");
Check(!store.BeginResponse(42, token, "wrong", 3), "unsolicited nonce rejected");
Check(store.BeginResponse(42, token, nonce, 3), "pending response claimed once");
Check(!store.BeginResponse(42, token, nonce, 3), "duplicate cannot queue another permission callback");
Check(store.Accept(42, token, nonce, data, 3), "permitted matching response accepted");
Check(!store.Accept(42, token, nonce, data, 3), "response cannot replace an already accepted snapshot");
Check(!store.Request(entry, 7), "received link doesn't re-fetch");
ItemLinkSnapshot pinned = entry.Snapshot!;
store.Prune(603);
Check(store.Find(ItemLinkStore.Key(42, token), 603) == null && store.GetOffer(token, 603) == null, "expired offers and link cache evicted");
Check(pinned.Label == "망치 ×1 · Q2", "pinned display has independent lifetime");
entry = store.Observe(42, token, "hammer", 604);
store.Request(entry, 604); nonce = entry.Nonce;
Check(store.Request(entry, 609) && nonce != entry.Nonce, "retry replaces nonce");
Check(!store.BeginResponse(42, token, nonce, 609), "late first response rejected after retry");
Check(!store.BeginResponse(42, token, entry.Nonce, 622), "timed out response rejected before permission callback");
Check(store.Request(entry, 623) && !store.Request(entry, 630), "at most three request attempts");
for (int i = 0; i < 250; i++) store.Observe(i + 1, ItemLinkWire.NewToken(), "item", 630);
Check(store.Count == 128, "received cache bounded");
for (int i = 0; i < 6; i++) Check(store.AllowResponse(42, 650), "peer allowance " + i);
Check(!store.AllowResponse(42, 650) && store.AllowResponse(43, 650), "one peer cannot consume the global allowance");
for (int i = 0; i < 100; i++) store.AllowResponse(100 + i, 650);
Check(!store.AllowResponse(2000, 650) && store.AllowResponse(42, 661), "global cap and window reset");
store.Clear();
Check(store.Count == 0 && !store.BeginResponse(42, token, entry.Nonce, 661), "disconnect drops all pending responses");
Console.WriteLine($"Item link checks passed: {checks}");
