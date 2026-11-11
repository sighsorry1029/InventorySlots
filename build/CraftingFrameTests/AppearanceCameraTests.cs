using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal static class AppearanceCameraTests
{
    internal static void Run()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "PathOfValheimanAppearanceCamera.cs"))) root = root.Parent;
        if (root == null) throw new DirectoryNotFoundException("InventorySlots checkout");
        var production = CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root.FullName, "PathOfValheimanAppearanceCamera.cs")))
            .GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().First();
        // Actual session, guards, input and pose code. UI raycast, screen bounds,
        // physics and game objects are boundary doubles, not a renderer simulation.
        string members = string.Concat(production.Members.Where(m => m is not MethodDeclarationSyntax method ||
            method.Identifier.ValueText is not ("InstallPathOfValheimanAppearanceCamera" or
                "GetPathOfValheimanAppearanceArea")));
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Select(p => MetadataReference.CreateFromFile(p));
        var compilation = CSharpCompilation.Create("AppearanceCameraRegression",
            [CSharpSyntaxTree.ParseText(Host.Replace("/* PRODUCTION */", members))], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);
        if (!result.Success) throw new Exception(string.Join("\n", result.Diagnostics));
        try { Assembly.Load(stream.ToArray()).GetType("Host")!.GetMethod("Run")!.Invoke(null, null); }
        catch (TargetInvocationException error) { throw error.InnerException!; }
    }

    private const string Host = """
using System;
using System.Collections.Generic;
public static class Host
{
    /* PRODUCTION */
    private static int Checks, Warnings;
    private static bool IsDedicatedServer;
    private static Plugin _instance = new();
    private class Plugin { public bool isActiveAndEnabled=true; }
    private static PovCharacterView _povCharacterView;
    private static bool UiHit;
    private static Rect Area = new(0.35f,0.08f,0.3f,0.8f);
    private static Rect GetPathOfValheimanAppearanceArea(PovAppearanceCamera orbit) => Area;
    private static bool IsInventoryPanelClosing(InventoryGui gui) => gui.Closing;
    private static object PovCharacterDragItem(InventoryGui gui) => gui.Drag;
    private static object PovCharacterDragGo(InventoryGui gui) => gui.DragGo;
    private static GameObject _inventoryTrashConfirmDialog;
    private static bool ShouldBlockGlobalHotkeys(Player player) => Menu.Visible || Console.Visible || TextInput.Visible ||
        StoreGui.Visible || Minimap.Open || Chat.instance != null && Chat.instance.Focus;
    private static class Log { public static void LogWarning(string s) => Warnings++; }
    private class PovCharacterView
    {
        public bool Active=true, Closing; public int Tab; public bool[] Failed=new bool[3];
        public InventoryGui Gui=InventoryGui.instance; public Player Player=Player.m_localPlayer;
        public RectTransform Root=new();
    }
    private class RectTransform { public GameObject gameObject=new(); }
    private class GameObject { public bool activeInHierarchy=true; public Transform transform=new(); }
    private class Button { public GameObject gameObject=new(); public Transform transform=>gameObject.transform; }
    private class Dialog { public GameObject gameObject=new(); }
    private class Player
    {
        public static Player m_localPlayer=new(); public Transform transform=new();
        public bool Dead, Teleport, Intro, Cutscene, Attached;
        public bool IsDead()=>Dead; public bool IsTeleporting()=>Teleport; public bool InIntro()=>Intro;
        public bool InCutscene()=>Cutscene; public bool IsAttached()=>Attached;
        public Vector3 GetCenterPoint()=>new(10,1,20); public float GetHeight()=>1.8f;
    }
    private class Transform
    {
        public Vector3 eulerAngles; public Transform parent;
        public bool IsChildOf(Transform other)=>this==other||parent!=null&&parent.IsChildOf(other);
    }
    private class InventoryGui
    {
        public static InventoryGui instance=new(); public static bool Visible=true;
        public bool Closing; public object Drag, DragGo; public static bool IsVisible()=>Visible;
        public Dialog m_splitDialog, m_variantDialog;
        public Button m_dropButton=new();
        public bool IsSkillsPanelOpen, IsTextPanelOpen, IsTrophisPanelOpen, IsAchievementsPanelOpen;
    }
    private class Camera { public float fieldOfView=65, aspect=16f/9, nearClipPlane=0.5f; }
    private class GameCamera
    {
        public static GameCamera instance=new(); public static bool FreeFly;
        public static bool InFreeFly()=>FreeFly; public float m_raycastWidth=0.35f; public int m_blockCameraMask=1;
    }
    private class EventSystem
    {
        public static EventSystem current=new(); public static List<RaycastResult> Results=new();
        public void RaycastAll(PointerEventData data,List<RaycastResult> results)
        { results.AddRange(Results); if(UiHit) results.Add(new(){gameObject=new()}); }
    }
    private class PointerEventData { public Vector2 position; public PointerEventData(EventSystem events){} public void Reset(){} }
    private class RaycastResult { public GameObject gameObject; }
    private static class Application { public static bool isFocused=true; }
    private static class Screen { public static int width=1920, height=1080; }
    private static class Time { public static int frameCount; }
    private enum KeyCode { Mouse0, Mouse1 }
    private enum QueryTriggerInteraction { Ignore }
    private struct RaycastHit { public float distance; }
    private static class Physics
    {
        public static bool Inside, Wall, Throw; public static float Distance=1;
        public static bool CheckSphere(Vector3 p,float r,int mask,QueryTriggerInteraction q)
        { if(Throw) throw new Exception("physics unavailable"); return Inside; }
        public static bool SphereCast(Vector3 p,float r,Vector3 d,out RaycastHit hit,float max,int mask,QueryTriggerInteraction q)
        { hit=new(){distance=Distance}; return Wall; }
    }
    private static class ZInput
    {
        public static bool Held, Down, Gamepad; public static Vector2 Delta;
        public static KeyCode Button=KeyCode.Mouse0;
        public static bool GetKey(KeyCode k)=>k==Button&&Held; public static bool GetKeyDown(KeyCode k)=>k==Button&&Down;
        public static Vector2 GetMouseDelta()=>Delta; public static bool IsExclusiveGamepadActive()=>Gamepad;
    }
    private static class PlayerController { public static float m_mouseSens=1; public static bool m_invertMouse; }
    private static class Input { public static Vector2 mousePosition=new(960,540); }
    private static class Menu { public static bool Visible; public static bool IsVisible()=>Visible; }
    private static class Console { public static bool Visible; public static bool IsVisible()=>Visible; }
    private static class TextInput { public static bool Visible; public static bool IsVisible()=>Visible; }
    private static class UnifiedPopup { public static bool Visible; public static bool IsVisible()=>Visible; }
    private static class StoreGui { public static bool Visible; public static bool IsVisible()=>Visible; }
    private static class Minimap { public static bool Open; public static bool IsOpen()=>Open; }
    private class Hud
    {
        public static Hud instance=new(); public static bool Radial; public static bool InRadial()=>Radial;
        public Text m_hoverName=new();
    }
    private class Text { public GameObject gameObject=new(); public string text=""; public bool raycastTarget=true; }
    private class Chat { public static Chat instance; public bool Focus; public bool HasFocus()=>Focus; }
    private record struct Vector2(float x,float y)
    { public static Vector2 operator *(Vector2 v,float n)=>new(v.x*n,v.y*n); }
    private record struct Vector3(float x,float y,float z)
    {
        public float sqrMagnitude=>x*x+y*y+z*z; public float magnitude=>MathF.Sqrt(sqrMagnitude);
        public Vector3 normalized=>this*(1f/Math.Max(0.000001f,magnitude));
        public static Vector3 operator +(Vector3 a,Vector3 b)=>new(a.x+b.x,a.y+b.y,a.z+b.z);
        public static Vector3 operator -(Vector3 a,Vector3 b)=>new(a.x-b.x,a.y-b.y,a.z-b.z);
        public static Vector3 operator *(Vector3 v,float n)=>new(v.x*n,v.y*n,v.z*n);
    }
    private record struct Quaternion(System.Numerics.Quaternion Value)
    {
        public static Quaternion Euler(float x,float y,float z)=>new(System.Numerics.Quaternion.CreateFromYawPitchRoll(y*Mathf.Deg2Rad,x*Mathf.Deg2Rad,z*Mathf.Deg2Rad));
        public static Vector3 operator *(Quaternion q,Vector3 p)
        { var v=System.Numerics.Vector3.Transform(new(p.x,p.y,p.z),q.Value); return new(v.X,v.Y,v.Z); }
        public Quaternion Inverse()=>new(System.Numerics.Quaternion.Inverse(Value));
    }
    private record struct Rect(float x,float y,float width,float height)
    {
        public Vector2 center=>new(x+width/2,y+height/2);
        public bool Contains(Vector2 p)=>p.x>=x&&p.x<=x+width&&p.y>=y&&p.y<=y+height;
    }
    private static class Mathf
    {
        public const float Deg2Rad=MathF.PI/180;
        public static float Max(float a,float b)=>Math.Max(a,b);
        public static float Clamp(float n,float a,float b)=>Math.Clamp(n,a,b);
        public static float Repeat(float n,float max)=>n-MathF.Floor(n/max)*max;
        public static float Tan(float n)=>MathF.Tan(n);
    }
    private static void Expect(bool value,string label) { if(!value) throw new Exception(label); Checks++; }
    private static bool Near(float a,float b)=>Math.Abs(a-b)<0.0001f;
    private static void Fresh()
    {
        ResetPathOfValheimanAppearanceCamera(); _povAppearanceCameraFailedView=null;
        Player.m_localPlayer=new(); InventoryGui.instance=new(); GameCamera.instance=new();
        _povCharacterView=new(); InventoryGui.Visible=true; GameCamera.FreeFly=false;
        Physics.Wall=Physics.Inside=Physics.Throw=false;
        UiHit=ZInput.Held=ZInput.Down=false; Input.mousePosition=new(960,540); Time.frameCount++;
        ZInput.Button=KeyCode.Mouse0;
        Hud.instance=new();
        EventSystem.current=new(); EventSystem.Results.Clear();
    }
    private static (Vector3,Quaternion) Frame(bool original=true)
    {
        Vector3 p=new(99,98,97); Quaternion r=Quaternion.Euler(0,30,0);
        ApplyPathOfValheimanAppearanceCamera(GameCamera.instance,new Camera(),original,ref p,ref r);
        Time.frameCount++; return (p,r);
    }
    public static void Run()
    {
        Fresh(); var p=Frame().Item1;
        Expect(p.z>20,"First view is in front of unchanged player");
        Expect(Player.m_localPlayer.transform.eulerAngles==new Vector3(),"No player rotation write");
        foreach(float aspect in new[]{4f/3,16f/9,21f/9})
        foreach(float x in new[]{0.22f,0.5f,0.76f})
        foreach(float yaw in new[]{0f,125f,270f})
        {
            var area=new Rect(x-0.1f,0.08f,0.2f,0.8f); var center=new Vector3(10,1,20);
            CalculatePathOfValheimanAppearancePose(center,1.8f,65,aspect,area,yaw,5,out var pos,out var rot);
            var local=rot.Inverse()*(center-pos); float tan=MathF.Tan(65*Mathf.Deg2Rad/2);
            Expect(Near(0.5f+local.x/(2*local.z*tan*aspect),x),"Center projects into free screen gap across aspect/yaw");
            Expect(Near(0.5f+local.y/(2*local.z*tan),area.center.y),"Vertical framing is stable");
        }
        foreach(Action<PovCharacterView> block in new Action<PovCharacterView>[] {
            v=>v.Active=false,v=>v.Closing=true,v=>v.Tab=1,v=>v.Tab=2,v=>v.Failed[0]=true,
            v=>v.Root.gameObject.activeInHierarchy=false,v=>v.Gui.Closing=true,
            v=>v.Player.Dead=true,v=>v.Player.Teleport=true,v=>v.Player.Intro=true,
            v=>v.Player.Cutscene=true,v=>v.Player.Attached=true,
            v=>Player.m_localPlayer=new(),v=>InventoryGui.instance=new(),v=>GameCamera.instance=new() })
        {
            Fresh(); Frame(); block(_povCharacterView); ValidatePathOfValheimanAppearanceCamera();
            Expect(_povAppearanceCamera==null,"Invalid session released even if camera branch bypasses hook");
            // A new ordinary camera may establish a fresh session; other identity/state changes may not.
        }
        Fresh(); Frame(); GameCamera.FreeFly=true; ValidatePathOfValheimanAppearanceCamera();
        Expect(_povAppearanceCamera==null&&Frame().Item1.x==99,"Freefly keeps native output");
        Fresh(); Frame(); Application.isFocused=false; ValidatePathOfValheimanAppearanceCamera();
        Expect(_povAppearanceCamera==null,"Focus loss releases drag/session"); Application.isFocused=true;
        Fresh(); Frame(); Expect(Frame(false).Item1.x==99&&_povAppearanceCamera==null,"Other camera prefix wins");
        Fresh(); IsDedicatedServer=true; Expect(Frame().Item1.x==99,"No dedicated camera work"); IsDedicatedServer=false;
        Fresh(); _instance.isActiveAndEnabled=false; Expect(Frame().Item1.x==99,"Disabled plugin yields"); _instance.isActiveAndEnabled=true;
        Fresh(); Physics.Wall=true; p=Frame().Item1;
        Expect(Near((p-Player.m_localPlayer.GetCenterPoint()).magnitude,0.95f),"Wall limits distance with clearance");
        Physics.Distance=0.2f; Expect(Frame().Item1.x==99,"Too close wall keeps native pose"); Physics.Distance=1;
        Physics.Inside=true; Expect(Frame().Item1.x==99,"Initial overlap keeps native pose");
        Fresh(); Physics.Throw=true; Expect(Frame().Item1.x==99,"Failure leaves native output");
        Physics.Throw=false; Expect(Frame().Item1.x==99&&Warnings==1,"Failure isolated and logged once per view");
        Fresh(); Frame(); ZInput.Held=true; ZInput.Delta=new(10,3); Frame();
        Expect(!_povAppearanceCamera.Dragging&&Near(_povAppearanceCamera.Yaw,180),"Held LMB on entry does not capture");
        ZInput.Down=true; Frame(); ZInput.Down=false; Frame();
        Expect(_povAppearanceCamera.Dragging&&Near(_povAppearanceCamera.Yaw,200),"Fresh central left drag rotates camera");
        var orbit=_povAppearanceCamera; int frame=Time.frameCount; UpdatePathOfValheimanAppearanceCameraInput(orbit,Area);
        float yawNow=orbit.Yaw; UpdatePathOfValheimanAppearanceCameraInput(orbit,Area);
        Expect(Near(yawNow,orbit.Yaw),"Multiple pose evaluations consume mouse delta only once per frame");
        Time.frameCount++; UiHit=true; Frame(); UiHit=false; Frame();
        Expect(!orbit.Dragging&&Near(yawNow,orbit.Yaw),"Entering UI cancels, holding cannot reacquire");
        Fresh(); Frame(); ZInput.Button=KeyCode.Mouse1; ZInput.Held=ZInput.Down=true;
        Frame(); ZInput.Down=false; Frame();
        Expect(!_povAppearanceCamera.Dragging&&Near(_povAppearanceCamera.Yaw,180),"Right drag leaves orbit unchanged");
        Fresh(); Frame(); UiHit=true; ZInput.Held=ZInput.Down=true; Frame();
        UiHit=ZInput.Down=false; Frame();
        Expect(!_povAppearanceCamera.Dragging&&Near(_povAppearanceCamera.Yaw,180),"Left drag begun on a panel cannot become orbit in empty space");
        Fresh(); Frame(); ZInput.Held=ZInput.Down=true; Frame(); ZInput.Down=false; Frame();
        yawNow=_povAppearanceCamera.Yaw; ZInput.Held=false; Frame();
        Expect(!_povAppearanceCamera.Dragging&&Near(_povAppearanceCamera.Yaw,yawNow),"Releasing left button stops rotation");
        foreach(Action block in new Action[] {()=>UiHit=true,()=>UnifiedPopup.Visible=true,()=>Menu.Visible=true,
            ()=>InventoryGui.instance.Drag=new(),()=>InventoryGui.instance.DragGo=new(),()=>ZInput.Gamepad=true,
            ()=>Input.mousePosition=new(10,10),()=>InventoryGui.instance.m_splitDialog=new(),
            ()=>InventoryGui.instance.m_variantDialog=new(),()=>InventoryGui.instance.IsSkillsPanelOpen=true,
            ()=>InventoryGui.instance.IsTextPanelOpen=true,()=>InventoryGui.instance.IsTrophisPanelOpen=true,
            ()=>InventoryGui.instance.IsAchievementsPanelOpen=true,()=>_inventoryTrashConfirmDialog=new()})
        {
            Fresh(); Frame(); ZInput.Held=ZInput.Down=true; block(); Frame();
            Expect(!_povAppearanceCamera.Dragging,"UI/modal/item drag/outside screen area does not start orbit");
            UiHit=UnifiedPopup.Visible=Menu.Visible=ZInput.Gamepad=false;
            _inventoryTrashConfirmDialog=null;
        }
        Fresh(); InventoryGui.instance.Drag=new(); Frame(); InventoryGui.instance.Drag=null;
        ZInput.Held=ZInput.Down=true; Frame(); ZInput.Down=false; Frame();
        Expect(!_povAppearanceCamera.Dragging,"LMB that dropped/transferred an item cannot start orbit");
        ZInput.Held=false; Frame(); ZInput.Held=ZInput.Down=true; Frame();
        Expect(_povAppearanceCamera.Dragging,"A new LMB after dropping an item starts orbit");
        Fresh(); Frame(); orbit=_povAppearanceCamera;
        EventSystem.Results.Add(new(){gameObject=InventoryGui.instance.m_dropButton.gameObject});
        Expect(!IsPathOfValheimanAppearancePointerOverUi(orbit,Input.mousePosition),"Native outside-drop surface does not mask empty world area");
        EventSystem.Results.Add(new(){gameObject=new(){transform=new(){parent=InventoryGui.instance.m_dropButton.transform}}});
        Expect(IsPathOfValheimanAppearancePointerOverUi(orbit,Input.mousePosition),"Interactive descendants of drop surface still block input");
        EventSystem.Results.RemoveAt(1);
        UiHit=true;
        Expect(IsPathOfValheimanAppearancePointerOverUi(orbit,Input.mousePosition),"Actual UI over drop surface still wins");
        UiHit=false; EventSystem.current=new();
        Expect(!IsPathOfValheimanAppearancePointerOverUi(orbit,Input.mousePosition)&&orbit.Events==EventSystem.current,"New EventSystem replaces pointer cache");
        EventSystem.current=null;
        Expect(IsPathOfValheimanAppearancePointerOverUi(orbit,Input.mousePosition),"Unavailable event routing refuses input");
        Fresh(); Frame(); orbit=_povAppearanceCamera;
        Text hoverName=Hud.instance.m_hoverName;
        EventSystem.Results.Add(new(){gameObject=hoverName.gameObject});
        ZInput.Held=ZInput.Down=true; ZInput.Delta=new(10,3); Frame(); ZInput.Down=false; Frame();
        Expect(orbit.Dragging&&Near(orbit.Yaw,200),"Empty native hover label cannot block a central left drag");
        hoverName.text="Workbench";
        Expect(!IsPathOfValheimanAppearancePointerOverUi(orbit,Input.mousePosition),"Noninteractive hover label remains exempt when populated");
        Expect(hoverName.text=="Workbench"&&hoverName.raycastTarget&&hoverName.gameObject.activeInHierarchy,"Orbit does not alter the HUD text, raycasting or activation");
        UiHit=true; Frame();
        Expect(!orbit.Dragging&&Near(orbit.Yaw,200),"A button or tooltip alongside the hover label still cancels orbit");
        UiHit=false;
        EventSystem.Results.Add(new(){gameObject=new(){transform=new(){parent=hoverName.gameObject.transform}}});
        Expect(IsPathOfValheimanAppearancePointerOverUi(orbit,Input.mousePosition),"Hover label descendants are not blanket-exempted");
        EventSystem.Results.RemoveAt(1);
        Hud.instance=new();
        Expect(IsPathOfValheimanAppearancePointerOverUi(orbit,Input.mousePosition),"Old HUD object is no longer exempt after HUD replacement");
        EventSystem.Results[0]=new(){gameObject=Hud.instance.m_hoverName.gameObject};
        Expect(!IsPathOfValheimanAppearancePointerOverUi(orbit,Input.mousePosition),"Replacement HUD label is recognized without a stale cache");
        Hud.instance.m_hoverName=null;
        Expect(IsPathOfValheimanAppearancePointerOverUi(orbit,Input.mousePosition),"Missing hover label does not exempt arbitrary UI");
        Hud.instance=null;
        Expect(IsPathOfValheimanAppearancePointerOverUi(orbit,Input.mousePosition),"Missing HUD is safe and keeps unrelated UI blocking");
        System.Console.WriteLine($"Appearance camera: {Checks} checks passed (engine/UI boundaries doubled).");
    }
}
""";
}
