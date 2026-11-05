using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal static class PinnedTooltipHeaderTests
{
    internal static void Run()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "PinnedTooltipPanels.cs"))) root = root.Parent;
        if (root == null) throw new DirectoryNotFoundException("InventorySlots checkout");
        MethodDeclarationSyntax Read(string file, string name) => CSharpSyntaxTree.ParseText(
            File.ReadAllText(Path.Combine(root.FullName, file))).GetRoot().DescendantNodes()
            .OfType<MethodDeclarationSyntax>().Single(m => m.Identifier.ValueText == name);
        string methods = Read("PinnedTooltipPanels.cs", "SetPinnedTooltipContent").ToFullString() +
            Read("PinnedTooltipPanels.cs", "LayoutPinnedTooltipHeader").ToFullString() +
            Read("ItemLinkUi.cs", "TrySplitSharedItemTooltipHeader").ToFullString();
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create("PinnedHeaderRegression", new[] {
            CSharpSyntaxTree.ParseText(Host.Replace("/* PRODUCTION */", methods)),
            CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root.FullName, "ItemLinkCore.cs")))
        }, references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);
        if (!result.Success) throw new Exception(string.Join("\n", result.Diagnostics));
        try { Assembly.Load(stream.ToArray()).GetType("HeaderHost")!.GetMethod("Check")!.Invoke(null, null); }
        catch (TargetInvocationException error) { throw error.InnerException!; }
    }

    // Execute production layout/content code with text measurements as boundary
    // doubles. This checks geometry and snapshot preservation, not Unity glyphs.
    private const string Host = """
using System;
using System.Linq;
using System.Text.RegularExpressions;
using InventorySlots;
public static class HeaderHost
{
    /* PRODUCTION */
    private const float PinnedTooltipHeaderIconSize = 64f;
    private static int Checks;
    private static void Expect(bool value, string label) { if (!value) throw new Exception(label); Checks++; }
    private static void CreateTextRect(string name, RectTransform panel, out TMP_Text text) => text = new TMP_Text();
    private static void ApplyDefaultFontAsset(TMP_Text text) { }
    private static void ApplyTooltipSourceFont(TMP_Text text, string name) { }
    private static void SetTopLeftRectLayout(RectTransform rect, Vector2 position, Vector2 size) { rect.position = position; rect.size = size; }
    public static void Check()
    {
        var panel = new RectTransform();
        var cache = panel.Cache = new PinnedTooltipPanelUiCache();
        var body = new TMP_Text();
        cache.HeaderIcon = new Image();
        string title = "<color=#FFD36A>Wooden Sword</color>";
        Expect(SetPinnedTooltipContent(panel, body, title, "Stats"), "First item creates the header");
        float top = LayoutPinnedTooltipHeader(cache, 350f, 700f, 92f);
        Expect(top == 92f && cache.HeaderText!.rectTransform.position.x == 92f, "Title shares the icon row; body starts below the compact header");
        Expect(body.text == "Stats" && cache.HeaderText!.text == title, "Body retains its contents without a duplicate title");
        cache.TextLayoutSignature = "stable";
        Expect(!SetPinnedTooltipContent(panel, body, title, "Stats") && cache.TextLayoutSignature == "stable", "Unchanged item keeps cached measurements");
        Expect(SetPinnedTooltipContent(panel, body, title.Replace("FFD36A", "AA00FF"), "Stats") && cache.TextLayoutSignature == "",
            "Name color changes invalidate layout even when stats stay identical");
        cache.HeaderAction = new RectTransform();
        LayoutPinnedTooltipHeader(cache, 350f, 700f, 18f);
        Expect(cache.HeaderText!.rectTransform.position.x + cache.HeaderText.rectTransform.size.x == 300f,
            "Title reserves the upper-right share/close button");
        cache.HeaderAction.gameObject.SetActive(false);
        LayoutPinnedTooltipHeader(cache, 350f, 700f, 18f);
        Expect(cache.HeaderText!.rectTransform.position.x + cache.HeaderText.rectTransform.size.x == 334f,
            "Disabled share feature releases the title's right-side space");
        SetPinnedTooltipContent(panel, body, "<color=#FFD36A>Long crafted weapon name with several words</color>", "Stats");
        float wrappedTop = LayoutPinnedTooltipHeader(cache, 350f, 700f, 18f);
        Expect(wrappedTop > top && cache.HeaderText!.rectTransform.size.y > 64f, "Long names wrap and expand the header");
        cache.HeaderHint = new TMP_Text { text = "Open chat or inventory to close", fontSize = 13f };
        float hintedTop = LayoutPinnedTooltipHeader(cache, 350f, 700f, 18f);
        Expect(hintedTop > wrappedTop, "Shared-item hint contributes to reserved header height");
        Expect(-cache.HeaderHint.rectTransform.position.y >= -cache.HeaderText!.rectTransform.position.y + cache.HeaderText.rectTransform.size.y,
            "Hint follows the title without overlapping it");
        Expect(hintedTop >= -cache.HeaderHint.rectTransform.position.y + cache.HeaderHint.rectTransform.size.y + 12f,
            "Body begins after the entire title/hint block");
        cache.HeaderIcon.gameObject.SetActive(false);
        LayoutPinnedTooltipHeader(cache, 350f, 700f, 18f);
        Expect(cache.HeaderText!.rectTransform.position.x == 16f, "Missing remote prefab icon leaves no empty icon column");
        cache.HeaderIcon.gameObject.SetActive(true);
        cache.HeaderAction.gameObject.SetActive(true);
        float narrowTop = LayoutPinnedTooltipHeader(cache, 120f, 180f, 18f);
        Expect(!cache.HeaderIcon.enabled && cache.HeaderText!.rectTransform.position.x + cache.HeaderText.rectTransform.size.x <= 70f,
            "Narrow shared panel releases icon space and keeps its title clear of the close button");
        Expect(narrowTop + 18f + 72f <= 180f, "Long name and hint still leave a body viewport on a short canvas");
        LayoutPinnedTooltipHeader(cache, 350f, 700f, 18f);
        Expect(cache.HeaderIcon.enabled, "Resizing wider restores the same icon without recreating the panel");

        string nested = "<color=#FFD36A>검\n<b><color=#AA00FF>특수 이름</color></b></color>";
        string stats = "\nWeight: <color=#FFAA00>0.8</color>\nFreshness: 3h";
        string oldWire = ItemLinkWire.Rich("<size=28>" + nested + "</size>\n" + stats);
        Expect(TrySplitSharedItemTooltipHeader(oldWire, out string receivedTitle, out string receivedBody), "Recognizes the existing sanitized wire heading");
        Expect(receivedTitle == ItemLinkWire.Rich(nested) && receivedBody == ItemLinkWire.Rich(stats), "Nested colors, emphasis, title linebreaks and body spacing survive splitting");
        Expect(ItemLinkWire.Rich(receivedTitle + "\n" + receivedBody) == oldWire, "Separated display recomposes to the unchanged wire v1 body");
        foreach (string unknown in new[] { "Ordinary body\nStats", "<color=#FFD36A>Body</color> with text\nStats", "<color=#FFD36A>Unclosed" })
        {
            Expect(!TrySplitSharedItemTooltipHeader(unknown, out string t, out string b) && t == "" && b == unknown,
                "Unknown snapshot retains its complete body");
        }
        SetPinnedTooltipContent(panel, body, "", "Unknown snapshot");
        Expect(!cache.HeaderText!.gameObject.activeSelf && body.text == "Unknown snapshot", "Reused panel clears a previous title for unrecognized snapshots");
        Console.WriteLine($"Pinned tooltip header: {Checks} checks passed (production methods; measured-text doubles, no Unity rendering).");
    }
}
public sealed class GameObject { public bool activeSelf = true; public void SetActive(bool active) => activeSelf = active; }
public sealed class RectTransform
{
    public GameObject gameObject = new();
    public Vector2 position, size;
    public PinnedTooltipPanelUiCache? Cache;
    public T GetComponent<T>() where T:class => (Cache as T)!;
}
public sealed class Image { public bool enabled = true; public GameObject gameObject = new(); public object? sprite = new(); public RectTransform rectTransform = new(); }
public record struct Vector2(float x, float y);
public record struct Color(float r, float g, float b, float a);
public enum TextAlignmentOptions { MidlineLeft }
public enum TextWrappingModes { Normal }
public enum TextOverflowModes { Truncate }
public enum FontStyles { Bold }
public sealed class TMP_Text
{
    public GameObject gameObject = new();
    public RectTransform rectTransform = new();
    public string text = "";
    public bool enableAutoSizing;
    public float fontSize = 28f;
    public Color color;
    public FontStyles fontStyle;
    public TextAlignmentOptions alignment;
    public TextWrappingModes textWrappingMode;
    public TextOverflowModes overflowMode;
    public bool raycastTarget;
    public Vector2 GetPreferredValues(string value, float width, float height)
    {
        int columns = Math.Max(1, (int)(width / (fontSize * 0.5f)));
        string plain = Regex.Replace(value, "<[^>]+>", "");
        return new(width, plain.Split('\n').Sum(line => Math.Max(1, (int)Math.Ceiling((double)line.Length / columns))) * fontSize);
    }
}
public static class Mathf { public static float Max(float a, float b) => Math.Max(a,b); public static float Min(float a, float b) => Math.Min(a,b); }
public sealed class PinnedTooltipPanelUiCache
{
    public TMP_Text? HeaderText, HeaderHint;
    public Image? HeaderIcon;
    public RectTransform? HeaderAction;
    public string TextLayoutSignature = "", TextRepairSignature = "";
}
""";
}
