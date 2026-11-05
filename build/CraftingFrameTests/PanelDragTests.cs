using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal static class PanelDragTests
{
    internal static void Run()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "InventorySlotPanels.cs"))) root = root.Parent;
        if (root == null) throw new DirectoryNotFoundException("InventorySlots checkout");
        string Method(string file, string name) => CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root.FullName, file)))
            .GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().Single(m => m.Identifier.ValueText == name).ToFullString();
        string methods = string.Join("\n", new[] { "StartInventoryPanelDrag", "StopInventoryPanelDrag",
            "CommitDraggedInventoryPanelOffset", "SaveInventoryPanelPosition", "UpdateInventoryPanelDragging" }
            .Select(name => Method("InventorySlotPanels.cs", name)));
        methods += Method("InventoryQuickSlotPanels.cs", "PositionQuickSlotPanel");
        methods += Method("PathOfValheimanStoragePanel.cs", "GetQuickSlotPanelDisplayOffset");
        methods += Method("PathOfValheimanStoragePanel.cs", "IsQuickSlotPanelAtDefaultPosition");
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create("PanelDragRegression", [CSharpSyntaxTree.ParseText(Host.Replace("/* PRODUCTION */", methods))],
            references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);
        if (!result.Success) throw new Exception(string.Join("\n", result.Diagnostics));
        try { Assembly.Load(stream.ToArray()).GetType("PanelHost")!.GetMethod("Run")!.Invoke(null, null); }
        catch (TargetInvocationException ex) { throw ex.InnerException!; }
    }

    private const string Host = """
using System;
public static class PanelHost
{
    private record struct Vector2(float x, float y)
    {
        public float sqrMagnitude => x*x+y*y;
        public static Vector2 operator -(Vector2 a, Vector2 b) => new(a.x-b.x,a.y-b.y);
        public static implicit operator Vector3(Vector2 a) => new(a.x,a.y,0);
    }
    private record struct Vector3(float x, float y, float z)
    {
        public static implicit operator Vector2(Vector3 a) => new(a.x,a.y);
        public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.x+b.x,a.y+b.y,a.z+b.z);
    }
    private static class Mathf { public static float Abs(float value) => Math.Abs(value); }
    private static class Input { public static bool Held=true; public static bool GetMouseButton(int button) => Held; }
    private enum Toggle { Off, On }
    private class Setting { public Toggle Value; }
    private class GameObject { public bool activeInHierarchy=true; }
    private class RectTransform
    {
        public RectTransform? parent;
        public Vector3 localPosition;
        public Vector3 World => parent == null ? localPosition : parent.World + localPosition;
        public GameObject gameObject=new();
        public void SetParent(RectTransform p,bool worldPositionStays) => parent=p;
    }
    private class InventoryGrid { public RectTransform m_gridRoot=new(); public float m_elementSpace=70; }
    private class InventoryGui
    {
        public static InventoryGui? instance=new(); public static bool Visible=true;
        public InventoryGrid m_playerGrid=new(); public static bool IsVisible()=>Visible;
    }
    private class InventoryPanelDragMarker
    {
        public string PanelName="Quick"; public RectTransform Rect=new();
        public T GetComponent<T>() => (T)(object)Rect;
    }
    private class InventorySlotsClientPanelPosition { public float X,Y; }
    private class ClientInventory
    {
        public InventorySlotsClientPanelPosition QuickSlotsPanelPosition=new(){X=-82,Y=-235};
        public InventorySlotsClientPanelPosition EquipmentSlotsPanelPosition=new(){X=-82,Y=0};
        public bool QuickSlotsPanelManuallyPlaced;
    }
    private class ClientState { public ClientInventory Inventory=new(); }
    private static class InventoryClient { public static ClientState ClientState=new(); }
    private static class InventoryPanels
    {
        public static RectTransform? DraggedInventoryPanel;
        public static bool DraggingQuickSlotsPanelOffset,DraggingEquipmentSlotsPanelOffset;
        public static Vector2 InventoryPanelDragStartLocalMouse,InventoryPanelDragStartOffset,InventoryPanelDragOriginalOffset;
        public static Vector2 QuickSlotsPanelRuntimeOffset=new(-82,-235),EquipmentSlotsPanelRuntimeOffset=new(-82,0);
    }
    private const string QuickSlotPanelName="Quick",CustomSlotPanelName="Equipment";
    private static readonly Vector2 QuickSlotsPanelFixedOffset=new(-82,-235);
    private static readonly Setting _quickSlotHudFollowsPanel=new();
    private static bool Settled=true,Closing,MouseAvailable=true;
    private static Vector2 Mouse;
    private static int Saves,HudSaves,SnapCalls,Checks;
    private static float RingRows=.5f;
    private static bool IsInventoryPanelClosing(InventoryGui gui)=>Closing;
    private static bool IsInventoryPanelAnimationSettled(InventoryGui gui)=>Settled;
    private static bool TryGetInventoryPanelDragLocalMousePosition(out Vector2 p) { p=Mouse;return MouseAvailable; }
    private static Vector2 SnapDraggedInventoryPanelOffset(Vector2 raw) { SnapCalls++;return raw; }
    private static void EnsureClientStateLoaded() { }
    private static void SaveClientState()=>Saves++;
    private static void SaveQuickSlotHudAnchor()=>HudSaves++;
    private static float GetPathOfValheimanRingPanelRows()=>RingRows;
    private static void CaptureQuickSlotHudAnchor(RectTransform panel,Vector3 target,float spacing) { }
    /* PRODUCTION */
    private static void Check(bool value,string name) { if(!value)throw new Exception(name); Checks++; }
    public static void Run()
    {
        var marker=new InventoryPanelDragMarker();
        Settled=false; StartInventoryPanelDrag(marker);
        Check(InventoryPanels.DraggedInventoryPanel==null,"Opening animation cannot start a drag");
        Settled=true; Closing=true; StartInventoryPanelDrag(marker);
        Check(InventoryPanels.DraggedInventoryPanel==null,"Closing cannot start a drag"); Closing=false;
        StartInventoryPanelDrag(marker);
        Check(InventoryPanels.InventoryPanelDragStartOffset.y==-270,"Start uses actual PoV ring-adjusted pose");
        UpdateInventoryPanelDragging(); StopInventoryPanelDrag();
        Check(SnapCalls==0 && Saves==0,"Click without movement neither snaps nor saves");
        Check(InventoryPanels.QuickSlotsPanelRuntimeOffset.y==-235 && !InventoryClient.ClientState.Inventory.QuickSlotsPanelManuallyPlaced,
            "Click restores default coordinates and automatic spacing");
        Check(GetQuickSlotPanelDisplayOffset(70).y==-270,"Default still follows ring height after a click");
        StartInventoryPanelDrag(marker); Mouse=new(0,35); UpdateInventoryPanelDragging(); StopInventoryPanelDrag();
        Check(Saves==1 && SnapCalls==1,"Real drag snaps then saves once on release");
        Check(InventoryClient.ClientState.Inventory.QuickSlotsPanelManuallyPlaced,"Explicit placement is distinguished even at default coordinates");
        Check(GetQuickSlotPanelDisplayOffset(70).y==-235,"Release does not reapply PoV ring spacing");
        RingRows=1; Check(GetQuickSlotPanelDisplayOffset(70).y==-235,"Manually placed panel remains independent of later ring changes");
        Check(InventoryPanels.DraggedInventoryPanel==null && !InventoryPanels.DraggingQuickSlotsPanelOffset,"Release clears drag state");
        StartInventoryPanelDrag(marker); UpdateInventoryPanelDragging(); StopInventoryPanelDrag();
        Check(Saves==1,"Click on saved pose does not rewrite state");
        _quickSlotHudFollowsPanel.Value=Toggle.On;
        StartInventoryPanelDrag(marker); Mouse=new(10,35); UpdateInventoryPanelDragging(); Input.Held=false; UpdateInventoryPanelDragging();
        Check(Saves==2 && HudSaves==1,"Mouse release commits panel and optional HUD once"); Input.Held=true;
        marker.PanelName=CustomSlotPanelName; StartInventoryPanelDrag(marker); Mouse=new(15,35); UpdateInventoryPanelDragging(); StopInventoryPanelDrag();
        Check(InventoryPanels.EquipmentSlotsPanelRuntimeOffset.x==-77,"Equipment keeps independent drag coordinates");
        Check(InventoryPanels.QuickSlotsPanelRuntimeOffset.x==-72,"Moving equipment does not dock quickslots permanently");
        var grid=InventoryGui.instance!.m_playerGrid;
        var panel=new RectTransform {parent=new()};
        PositionQuickSlotPanel(grid,panel,new(50,-80,0),70);
        Check(panel.parent==grid.m_gridRoot && panel.localPosition==new Vector3(50,-80,0),"Quick panel uses the same grid parent and local coordinates as equipment");
        grid.m_gridRoot.localPosition=new(0,120,0);
        Check(panel.World==new Vector3(50,40,0),"Panel inherits native parent movement without a second slide");
        grid.m_gridRoot.localPosition=new(0,0,0);
        Check(panel.World==new Vector3(50,-80,0),"Native settled position preserves manual offset");
        Console.WriteLine($"Panel parent/drag lifecycle: {Checks} checks passed (production methods, managed doubles; no Unity rendering).");
    }
}
""";
}
