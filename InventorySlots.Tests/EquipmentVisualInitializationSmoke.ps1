#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$controller = Get-Content -Raw -LiteralPath (Join-Path $repositoryRoot 'CustomEquipmentVisualController.cs')

function Read-OneMatch([string] $pattern) {
    $matches = [regex]::Matches($controller, $pattern)
    if ($matches.Count -ne 1) { throw "Expected one production source match: $pattern" }
    $matches[0].Value
}

# Execute the production readiness predicate, deferred processing, and entry guards.
# Only Unity objects and the downstream visual mutation are replaced with doubles.
# The separate original-DLL contract check verifies the real private field.
$deferred = Read-OneMatch '(?s)    private static bool IsEquipmentMaterialManagerReady\(\).*?(?=    private static readonly Func<VisEquipment)'
$localGuard = Read-OneMatch '(?s)    internal static void UpdateCustomEquipmentVisuals\(Player player\).*?(?=        SyncBackpackCompatState\(player\);)'
$remoteGuard = Read-OneMatch '(?s)    internal static void UpdateCustomEquipmentVisualsFromZdo\(VisEquipment visEquipment\).*?(?=        ZDO\? zdo)'
$source = @"
using System;
using System.Collections.Generic;
using System.Linq;
public class UnityObject { public bool Destroyed; }
public class MaterialPropertyBlock { }
public class MaterialMan : UnityObject { public static MaterialMan instance; public MaterialPropertyBlock Block; }
public class ObjectDB : UnityObject { public static ObjectDB instance = new ObjectDB(); }
public class VisEquipment : UnityObject { }
public class Inventory { }
public class Humanoid : UnityObject { public Inventory Inventory = new Inventory(); public Inventory GetInventory() => Inventory; }
public class Player : Humanoid { public bool m_isLoading; public VisEquipment m_visEquipment = new VisEquipment(); }
public static class EquipmentVisualProbe
{
    public static bool IsDedicatedServer;
    public static int LocalUpdates, RemoteUpdates;
    private static readonly HashSet<Player> PendingEquipmentVisualPlayers = new HashSet<Player>();
    private static bool IsUnityNull(UnityObject obj) => obj == null || obj.Destroyed;
    private static MaterialPropertyBlock MaterialManagerPropertyBlock(MaterialMan manager) => manager.Block;
    private static void ClearCustomEquipmentVisuals(VisEquipment owner) { }
    $deferred
    $localGuard
        LocalUpdates++;
    }
    $remoteGuard
        RemoteUpdates++;
    }
    public static void Local(Player player) => UpdateCustomEquipmentVisuals(player);
    public static void Remote(VisEquipment owner) => UpdateCustomEquipmentVisualsFromZdo(owner);
    public static void Tick() => ProcessDeferredEquipmentVisuals();
    public static int Pending => PendingEquipmentVisualPlayers.Count;
}
"@
Add-Type -TypeDefinition $source
function Check([bool] $condition, [string] $description) {
    if (-not $condition) { throw $description }
    "PASS $description"
}

$player = [Player]::new()
[EquipmentVisualProbe]::Local($player)
[EquipmentVisualProbe]::Local($player)
[EquipmentVisualProbe]::Tick()
Check ([EquipmentVisualProbe]::Pending -eq 1 -and [EquipmentVisualProbe]::LocalUpdates -eq 0) 'Missing manager defers and deduplicates visual requests'

$manager = [MaterialMan]::new()
[MaterialMan]::instance = $manager
[EquipmentVisualProbe]::Tick()
[EquipmentVisualProbe]::Remote($player.m_visEquipment)
Check ([EquipmentVisualProbe]::Pending -eq 1 -and [EquipmentVisualProbe]::LocalUpdates -eq 0 -and [EquipmentVisualProbe]::RemoteUpdates -eq 0) 'Awake without Start does not register local or remote visuals'

$manager.Block = [MaterialPropertyBlock]::new()
[EquipmentVisualProbe]::Tick()
[EquipmentVisualProbe]::Tick()
[EquipmentVisualProbe]::Remote($player.m_visEquipment)
Check ([EquipmentVisualProbe]::Pending -eq 0 -and [EquipmentVisualProbe]::LocalUpdates -eq 1 -and [EquipmentVisualProbe]::RemoteUpdates -eq 1) 'Initialization replays local work once and allows the next remote update'

[MaterialMan]::instance = [MaterialMan]::new()
[EquipmentVisualProbe]::Local($player)
Check ([EquipmentVisualProbe]::Pending -eq 1 -and [EquipmentVisualProbe]::LocalUpdates -eq 1) 'A replacement scene manager must initialize independently'
$player.m_isLoading = $true
[MaterialMan]::instance.Block = [MaterialPropertyBlock]::new()
[EquipmentVisualProbe]::Tick()
Check ([EquipmentVisualProbe]::Pending -eq 1 -and [EquipmentVisualProbe]::LocalUpdates -eq 1) 'A character still loading retains its deferred update'
$player.m_isLoading = $false
[EquipmentVisualProbe]::Tick()
Check ([EquipmentVisualProbe]::Pending -eq 0 -and [EquipmentVisualProbe]::LocalUpdates -eq 2) 'The loaded character receives the deferred update'

[MaterialMan]::instance = $null
[EquipmentVisualProbe]::Local($player)
$player.Destroyed = $true
[EquipmentVisualProbe]::Tick()
Check ([EquipmentVisualProbe]::Pending -eq 0 -and [EquipmentVisualProbe]::LocalUpdates -eq 2) 'Destroyed preview characters are discarded even before initialization'

[EquipmentVisualProbe]::IsDedicatedServer = $true
[EquipmentVisualProbe]::Local([Player]::new())
[EquipmentVisualProbe]::Remote([VisEquipment]::new())
Check ([EquipmentVisualProbe]::Pending -eq 0 -and [EquipmentVisualProbe]::LocalUpdates -eq 2 -and [EquipmentVisualProbe]::RemoteUpdates -eq 1) 'Dedicated servers do not queue or create visual work'
'8 checks passed using production guards with Unity doubles; no game session was executed.'
