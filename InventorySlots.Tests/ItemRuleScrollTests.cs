using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal static class ItemRuleScrollTests
{
    public static void Run()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "Shared/ItemRules/ItemRuleUi.cs"))) root = root.Parent;
        if (root == null) throw new DirectoryNotFoundException("InventorySlots checkout");
        string source = File.ReadAllText(Path.Combine(root.FullName, "Shared/ItemRules/ItemRuleUi.cs"));
        foreach (string symbol in new[] { "INVENTORY_SLOTS", "INVENTORY_ACTIONS" })
        {
            var tree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(preprocessorSymbols: new[] { symbol })).GetRoot();
            var plugin = tree.DescendantNodes().OfType<ClassDeclarationSyntax>()
                .First(c => c.Identifier.ValueText is "InventorySlotsPlugin" or "InventoryActionsPlugin");
            string members = string.Join("\n", plugin.Members.Where(m => m switch {
                EnumDeclarationSyntax e => e.Identifier.ValueText == "ItemRulePanelScrollMode",
                FieldDeclarationSyntax f => f.Declaration.Variables.Any(v => v.Identifier.ValueText is "ItemRuleRowHeight" or "_itemRulePanelScrollMode"),
                MethodDeclarationSyntax method => method.Identifier.ValueText == "GetItemRuleScrollOffset",
                ClassDeclarationSyntax c => c.Identifier.ValueText == "ItemRuleScrollRect",
                _ => false
            }).Select(m => m.ToFullString()));
            var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
                .Select(path => MetadataReference.CreateFromFile(path));
            var compilation = CSharpCompilation.Create("RuleScroll_" + symbol,
                new[] { CSharpSyntaxTree.ParseText(Host.Replace("/* PRODUCTION */", members)) }, references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
            using var stream = new MemoryStream();
            var result = compilation.Emit(stream);
            if (!result.Success) throw new Exception(string.Join("\n", result.Diagnostics));
            try { Assembly.Load(stream.ToArray()).GetType("ScrollHost")!.GetMethod("Check")!.Invoke(null, null); }
            catch (TargetInvocationException error) { throw error.InnerException!; }
        }
    }

    // Production wheel code with managed UI boundaries; this does not simulate
    // Unity EventSystem dispatch, native dragging, TMP or actual device input.
    private const string Host = """
using System;
public class ConfigEntry<T> { public T Value=default!; }
public struct Vector2 { public float x,y; public Vector2(float x,float y){this.x=x;this.y=y;} }
public struct Rect { public float height; }
public class RectTransform { public Rect rect; public Vector2 anchoredPosition; }
public class BaseInputModule
{
    public float DeltaPerTick=1;
    public Vector2 ConvertPointerEventScrollDeltaToTicks(Vector2 delta)=>new(delta.x/DeltaPerTick,delta.y/DeltaPerTick);
}
public class PointerEventData { public Vector2 scrollDelta; public BaseInputModule? currentInputModule; public bool used; public void Use()=>used=true; }
public static class Mathf
{
    public static float Max(float a,float b)=>Math.Max(a,b);
    public static int Max(int a,int b)=>Math.Max(a,b);
    public static float Clamp(float x,float a,float b)=>Math.Clamp(x,a,b);
    public static float Floor(float v)=>(float)Math.Floor(v);
    public static float Ceil(float v)=>(float)Math.Ceiling(v);
    public static int FloorToInt(float v)=>(int)Math.Floor(v);
    public static int RoundToInt(float v)=>(int)Math.Round(v);
    public static float Abs(float v)=>Math.Abs(v);
    public static bool Approximately(float a,float b)=>Math.Abs(a-b)<.0001f;
}
public class ScrollRect
{
    public RectTransform content=new(),viewport=new(); public bool Active=true; public int BaseScrollCalls,Stops,Disables;
    public bool IsActive()=>Active;
    public virtual void OnScroll(PointerEventData e)=>BaseScrollCalls++;
    public void StopMovement()=>Stops++;
    protected void SetContentAnchoredPosition(Vector2 p)=>content.anchoredPosition=p;
    protected virtual void OnDisable()=>Disables++;
    public void Disable()=>OnDisable();
}
public static class ScrollHost
{
    /* PRODUCTION */
    static void Expect(bool ok,string name){if(!ok)throw new Exception(name);}
    public static void Check()
    {
        float Row(float offset,int steps)=>GetItemRuleScrollOffset(offset,760,228,steps,ItemRulePanelScrollMode.Row);
        Expect(Row(0,1)==38 && Row(38,-1)==0,"one unit moves one row in either direction");
        Expect(Row(10,1)==38 && Row(10,-1)==0,"dragged fractional position reaches the adjacent boundary");
        Expect(Row(114,2)==190 && Row(114,-2)==38,"multiple wheel units preserve count");
        Expect(Row(0,-1)==0 && Row(532,1)==532,"top/bottom clamp");
        Expect(GetItemRuleScrollOffset(0,760,171,1,ItemRulePanelScrollMode.Page)==152,"4.5 visible rows yield four-row page");
        Expect(GetItemRuleScrollOffset(10,760,228,1,ItemRulePanelScrollMode.Page)==228,"page from fractional start aligns without skipping extra row");
        Expect(GetItemRuleScrollOffset(238,760,228,-1,ItemRulePanelScrollMode.Page)==38,"upward page from partial row");
        Expect(GetItemRuleScrollOffset(570,760,171,1,ItemRulePanelScrollMode.Row)==589,"partial last page exposes entire final item");
        Expect(GetItemRuleScrollOffset(0,760,20,1,ItemRulePanelScrollMode.Page)==38,"viewport shorter than one row still moves at least one");
        Expect(GetItemRuleScrollOffset(0,76,228,1,ItemRulePanelScrollMode.Page)==0,"short list does not scroll");
        Expect(Row(10,0)==10,"zero units preserve freely dragged offset");
        var scroll=new ItemRuleScrollRect(); scroll.content.rect.height=760;scroll.viewport.rect.height=228;
        scroll.content.anchoredPosition=new Vector2(7,0);
        BaseInputModule? module=null;
        PointerEventData Wheel(float y,float x=0){var e=new PointerEventData{scrollDelta=new Vector2(x,y),currentInputModule=module};scroll.OnScroll(e);return e;}
        Expect(Wheel(-1).used && scroll.content.anchoredPosition.y==38 && scroll.content.anchoredPosition.x==7,"unset config defaults Row and preserves horizontal offset");
        Expect(scroll.BaseScrollCalls==0 && scroll.Stops==1,"custom wheel does not also run vanilla continuous movement");
        _itemRulePanelScrollMode=new ConfigEntry<ItemRulePanelScrollMode>{Value=ItemRulePanelScrollMode.Page};
        Wheel(-1);Expect(scroll.content.anchoredPosition.y==266,"live switch to Page uses current viewport");
        scroll.viewport.rect.height=171;Wheel(-1);Expect(scroll.content.anchoredPosition.y==418,"page adapts when panel height shrinks");
        _itemRulePanelScrollMode.Value=ItemRulePanelScrollMode.Row;scroll.content.anchoredPosition.y=0;
        module=new BaseInputModule{DeltaPerTick=6};
        Wheel(-6);Expect(scroll.content.anchoredPosition.y==38,"InputSystem default delta is one row, not six");
        Wheel(-12);Expect(scroll.content.anchoredPosition.y==114,"multiple normalized ticks retain count");
        module.DeltaPerTick=.05f; // Original Valheim 1.0.21 EventSystem.prefab.
        Wheel(-.05f);Expect(scroll.content.anchoredPosition.y==152,"Valheim's serialized module scale moves one row immediately");
        module.DeltaPerTick=6;
        Wheel(.05f);Expect(scroll.content.anchoredPosition.y==114,"action-scaled input below one tick responds immediately upwards");
        _itemRulePanelScrollMode.Value=ItemRulePanelScrollMode.Page;
        Wheel(-.05f);Expect(scroll.content.anchoredPosition.y==266,"small input immediately moves a full page after live mode change");
        _itemRulePanelScrollMode.Value=ItemRulePanelScrollMode.Row;
        scroll.content.anchoredPosition.y=10;Wheel(-.05f);
        Expect(scroll.content.anchoredPosition.y==38,"small wheel input snaps after a freely dragged offset");
        scroll.Disable();Wheel(-.05f);Expect(scroll.content.anchoredPosition.y==76 && scroll.Disables==1,"reopened panel responds immediately and retains base lifecycle");
        scroll.Active=false;var blocked=Wheel(-1);Expect(!blocked.used && scroll.content.anchoredPosition.y==76,"inactive panel ignores wheel");scroll.Active=true;
        var zero=Wheel(0);Expect(!zero.used && scroll.content.anchoredPosition.y==76,"zero delta does nothing");
        Expect(!Wheel(float.NaN).used && !Wheel(float.PositiveInfinity).used,"nonfinite input ignored");
        Wheel(0,6);Expect(scroll.content.anchoredPosition.y==114,"vertical ScrollRect dominant-axis convention retained");
        module=null;Wheel(-.05f);Expect(scroll.content.anchoredPosition.y==152,"small legacy events also respond without accumulation");
    }
}
""";
}
