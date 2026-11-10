using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using InventorySlots;

internal static class EquipmentPanelLayoutTests
{
    public static void Run()
    {
        var yaml = new DeserializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).Build();
        var parsed = yaml.Deserialize<YamlRoot>("Slots:\n  - id: demister\n    smallSlot: true\n    applyArmor: true\n    items: [Demister]\n  - id: utility\n");
        Assert.Equal(true, parsed.Slots[0].SmallSlot);
        Assert.Equal(true, parsed.Slots[0].ApplyArmor);
        Assert.Equal("Demister", parsed.Slots[0].Items.Single());
        Assert.Equal(false, parsed.Slots[1].SmallSlot);
        Assert.Equal(true, yaml.Deserialize<YamlRoot>(InventorySlotsPlugin.DefaultYaml).Slots.All(s => !s.SmallSlot));

        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "EquipmentPanelLayout.cs"))) root = root.Parent;
        if (root == null) throw new DirectoryNotFoundException("InventorySlots checkout");
        string Read(string file) => File.ReadAllText(Path.Combine(root.FullName, file));
        SyntaxNode Tree(string file) => CSharpSyntaxTree.ParseText(Read(file)).GetRoot();
        string Methods(string file, params string[] names) => string.Join("\n", Tree(file).DescendantNodes()
            .OfType<MethodDeclarationSyntax>().Where(m => names.Contains(m.Identifier.ValueText)).Select(m => m.ToFullString()));
        string model = Tree("InventorySlotsModels.cs").DescendantNodes().OfType<ClassDeclarationSyntax>()
            .Single(c => c.Identifier.ValueText == "SlotDefinition").ToFullString();
        string members = Methods("PathOfValheimanStoragePanel.cs", "IsEquipmentPanelSlot", "IsPathOfValheimanRingSlot", "UpdatePathOfValheimanRingSlots") +
            Methods("InventoryUiController.cs", "SetEmptySmallEquipmentSlotTooltip") +
            Methods("InventorySlotLayout.cs", "TryGetSlotGridPos") +
            Methods("InventoryRows.cs", "GetCustomPanelColumns");
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create("SmallEquipmentContract", new[] {
            CSharpSyntaxTree.ParseText(Read("EquipmentPanelLayout.cs")),
            CSharpSyntaxTree.ParseText(Host.Replace("/* MODEL */", model).Replace("/* METHODS */", members))
        }, references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);
        if (!result.Success) throw new Exception(string.Join("\n", result.Diagnostics));
        try { Assembly.Load(stream.ToArray()).GetType("InventorySlots.InventorySlotsPlugin")!.GetMethod("Check")!.Invoke(null, null); }
        catch (TargetInvocationException error) { throw error.InnerException!; }
    }

    // Execute production partition/geometry and storage mapping. Visibility and
    // Unity transforms are boundaries; this is not an in-game input/render test.
    private const string Host = """
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ItemData = ItemDrop.ItemData;
public class ItemDrop { public class ItemData { } }
public class Player { }
public class UITooltip { public string m_topic="",m_text=""; }
public class InventoryElement { public UITooltip m_tooltip=new(); }
public class Inventory { public int GetWidth() => 8; }
public record struct Vector2i(int x, int y);
namespace UnityEngine
{
    public record struct Vector3(float x, float y, float z)
    {
        public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.x+b.x,a.y+b.y,a.z+b.z);
        public static Vector3 operator *(Vector3 a, float b) => new(a.x*b,a.y*b,a.z*b);
    }
    public record struct Rect(float xMin, float yMin, float width, float height) { public float yMax => yMin+height; }
    public static class Mathf { public static int CeilToInt(float v) => (int)Math.Ceiling(v); }
}
namespace InventorySlots
{
    internal enum SlotKind { BuiltIn, CustomEquipment, Quick }
    /* MODEL */
    public sealed partial class InventorySlotsPlugin
    {
        private const int CustomSlotPanelRows=3;
        private static int _quickHudLayoutVersion;
        private static readonly List<SlotDefinition> SlotDefinitions=new(), PovVisibleRings=new();
        private static readonly Dictionary<UITooltip,ItemData> InventoryHoverTooltipItems=new();
        private static readonly HashSet<string> Locked=new(), Occupied=new();
        private static readonly Vector3 QuickSlotsPanelFixedOffset=new(-82,-235,0);
        private static class InventoryPanels
        { public static Vector3 QuickSlotsPanelRuntimeOffset=QuickSlotsPanelFixedOffset; public static bool DraggingQuickSlotsPanelOffset; }
        private static class InventoryClient { public static class ClientState { public static class Inventory { public static bool QuickSlotsPanelManuallyPlaced; } } }
        private static int GetFixedRegularRows()=>9;
        private static List<SlotDefinition> GetCustomPanelSlots(Player p, Inventory i)=>SlotDefinitions.Where(s=>
            IsEquipmentPanelSlot(s) && !IsPathOfValheimanRingSlot(s) && (!Locked.Contains(s.Id) || s.PovCell!=null && Occupied.Contains(s.Id))).ToList();
        private static void UpdatePathOfValheimanActionSlots(Player p, Inventory i, int count) { }
        private static bool IsUnsupportedPathOfValheimanCell(string cell)=>false;
        private static bool IsQuickSlotUnlocked(Player p, SlotDefinition s)=>!Locked.Contains(s.Id);
        private static bool IsOccupiedSlot(Inventory i, SlotDefinition s)=>Occupied.Contains(s.Id);
        /* METHODS */
        private static void Expect(bool ok,string message) { if(!ok)throw new Exception(message); }
        public static void Check()
        {
            var p=new Player(); var bag=new Inventory();
            SlotDefinition Add(string id,bool small=false,SlotKind kind=SlotKind.CustomEquipment,string? pov=null)
            { var s=new SlotDefinition(id,id,kind,_=>true,povCell:pov){SmallSlot=small,ApplyArmor=true}; SlotDefinitions.Add(s);return s; }
            foreach(var id in new[]{"helmet","chest","legs","cape"}) Add(id,true,SlotKind.BuiltIn);
            var utility=Add("utility");
            var demister=Add("demister",true);
            var emptyCell=new InventoryElement();emptyCell.m_tooltip.m_topic="old item";
            InventoryHoverTooltipItems[emptyCell.m_tooltip]=new ItemData();
            SetEmptySmallEquipmentSlotTooltip(emptyCell,demister);
            Expect(emptyCell.m_tooltip.m_topic=="" && emptyCell.m_tooltip.m_text=="demister" &&
                !InventoryHoverTooltipItems.ContainsKey(emptyCell.m_tooltip),"empty hover shows slot name without stale item extensions");
            Add("wishbone",true);Add("circlet",true);Add("jewelcrafting.ring",true);Add("custom",true);
            var quick=Add("quick1",true,SlotKind.Quick);
            var original=SlotDefinitions.ToArray();
            TryGetSlotGridPos(bag,demister,out var before);
            UpdateInventoryPanelSlots(p,bag,1);
            Expect(FullEquipmentPanelSlots.Count==5 && SmallEquipmentPanelSlots.Count==5,"core armor stays full; five optional small cells");
            Expect(!SmallEquipmentPanelSlots.Contains(quick) && !IsSmallEquipmentPanelSlot(quick),"quick slots ignore equipment size flag");
            Expect(_equipmentPanelColumns==2 && GetSmallEquipmentPanelRows()==1f,"two full columns wrap five small cells to two rows");
            Expect(GetSmallEquipmentPanelPosition(4,70,new Rect(0,-70,70,70))==new Vector3(0,-245,0),"fifth small cell begins second row");
            var rects=new[]{new Rect(0,-70,70,70),new Rect(-35,-35,70,70),new Rect(-62,0,62,64)};
            foreach(var rect in rects)
            {var point=GetSmallEquipmentPanelPosition(0,70,rect);Expect(point.x+rect.xMin*.5f==rect.xMin && point.y+rect.yMax*.5f==-210+rect.yMax,"left/top edges survive nonstandard pivots");}
            Expect(GetQuickSlotPanelDisplayOffset(70).y==-305,"default quick panel makes room for two small rows");
            InventoryClient.ClientState.Inventory.QuickSlotsPanelManuallyPlaced=true;
            Expect(GetQuickSlotPanelDisplayOffset(70).y==-235,"manual position remains absolute even at default coordinates");
            InventoryClient.ClientState.Inventory.QuickSlotsPanelManuallyPlaced=false;
            InventoryPanels.DraggingQuickSlotsPanelOffset=true;
            Expect(GetQuickSlotPanelDisplayOffset(70).y==-235,"drag excludes automatic spacing");
            InventoryPanels.DraggingQuickSlotsPanelOffset=false;
            demister.SmallSlot=false;UpdateInventoryPanelSlots(p,bag,1);
            TryGetSlotGridPos(bag,demister,out var after);
            Expect(before==after && SlotDefinitions.SequenceEqual(original) && demister.Accepts(new ItemData()) && demister.ApplyArmor,"size toggle preserves storage order, acceptance and armor");
            Expect(FullEquipmentPanelSlots.Contains(demister) && !SmallEquipmentPanelSlots.Contains(demister),"toggle restores full-size membership");
            Add("necklace"); UpdateInventoryPanelSlots(p,bag,1);
            Expect(_equipmentPanelColumns==3 && GetSmallEquipmentPanelRows()==.5f,"seven full cells use three columns and six small cells per row");
            var ring=Add("pov.ring.0",false,SlotKind.Quick,"ring.0");
            var ring2=Add("pov.ring.1",false,SlotKind.Quick,"ring.1");
            Locked.Add(ring2.Id);Occupied.Add(ring2.Id);UpdateInventoryPanelSlots(p,bag,1);
            Expect(SmallEquipmentPanelSlots.Count==6 && SmallEquipmentPanelSlots.Contains(ring2),"PoV rings share the small row, including locked occupied recovery");
            Expect(IsPathOfValheimanRingSlot(ring) && !IsPathOfValheimanRingSlot(utility),"visual size never grants PoV ring identity");
            Occupied.Clear();Locked.Add("wishbone");UpdateInventoryPanelSlots(p,bag,1);
            Expect(SmallEquipmentPanelSlots.Count==4 && !SmallEquipmentPanelSlots.Contains(ring2),"locked empty cells reserve no space");
            foreach(var s in SlotDefinitions.Where(s=>!IsSmallEquipmentPanelSlot(s))) Locked.Add(s.Id);
            UpdateInventoryPanelSlots(p,bag,1);
            Expect(_equipmentPanelColumns==1 && _equipmentPanelRows==0 && GetSmallEquipmentPanelRows()==1,"small-only panel uses two cells per row without full-row gap");
            foreach(var s in SlotDefinitions) Locked.Add(s.Id);UpdateInventoryPanelSlots(p,bag,1);
            Expect(FullEquipmentPanelSlots.Count==0 && SmallEquipmentPanelSlots.Count==0,"empty layout has no panel cells");
            _quickHudLayoutVersion=42;ClearEquipmentPanelLayout();
            Expect(_quickHudLayoutVersion==-1 && GetSmallEquipmentPanelRows()==0,"GUI teardown invalidates following HUD geometry");
        }
    }
}
""";
}
