#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$registry = Get-Content -Raw -LiteralPath (Join-Path $repositoryRoot 'CompatRegistry.cs')
$backup = Get-Content -Raw -LiteralPath (Join-Path $repositoryRoot 'SlotActionBackupController.cs')
$load = Get-Content -Raw -LiteralPath (Join-Path $repositoryRoot 'InventoryPatchHandlers.cs')

function Read-OneMatch([string] $source, [string] $pattern) {
    $matches = [regex]::Matches($source, $pattern)
    if ($matches.Count -ne 1) { throw "Expected one source match: $pattern" }
    return $matches[0].Value
}

# Compile the real production predicates and entry guards; only the code after
# those guards is replaced by a mutation counter. Unity and plugin lookup are
# controlled test doubles, so this test does not load or touch game data.
$serverCharactersId = Read-OneMatch $registry 'private const string ServerCharactersGuid = "[^"]+";'
$serverManagerId = Read-OneMatch $registry 'private const string ServerManagerGuid = "[^"]+";'
$oldPolicy = Read-OneMatch $registry 'private static bool HasServerCharactersActive\s*=>[^;]+;'
$policy = Read-OneMatch $registry 'private static bool HasServerCharacterManagementActive\s*=>[^;]+;'
$saveGuard = Read-OneMatch $backup '(?s)internal static void SaveSlotBackup\(Player player\)\s*\{.*?(?=\s*try\s*\{)'
$restoreGuard = Read-OneMatch $backup '(?s)internal static void TryRestoreSlotBackup\(Player player\)\s*\{.*?(?=\s*Inventory inventory)'
if ($saveGuard -notmatch 'if \(HasServerCharacterManagementActive\)' -or
    $restoreGuard -notmatch 'IsUnityNull\(player\) \|\| HasServerCharacterManagementActive \|\| InventorySafety.RestoringSlotBackup') {
    throw 'Both backup entry points must keep the shared early-return policy.'
}
if ($load -notmatch 'return HasServerCharactersActive &&' -or
    $load -match 'HasServerCharacterManagementActive' -or
    $backup -match '(?:Remove|Clear)\s*\(\s*BackupKey') {
    throw 'Detached ServerCharacters loading and existing backup data must remain unchanged.'
}

$source = @"
using System;
public static class BackupCompatProbe
{
    $serverCharactersId
    $serverManagerId
    $oldPolicy
    $policy
    private static bool HasPlugin(string id)
    {
        Lookups++;
        return id == ServerCharactersGuid ? ServerCharacters : id == ServerManagerGuid && ServerManager;
    }
    private static bool IsUnityNull(Player player) { return player == null; }
    public static bool ServerCharacters, ServerManager;
    public static int Lookups, Mutations, InventoryReads;
    public static bool Managed { get { return HasServerCharacterManagementActive; } }
    public static bool DetachedLoad { get { return HasServerCharactersActive; } }
    $saveGuard
        Mutations++;
    }
    $restoreGuard
        Mutations++;
    }
    public static void Save(Player player) { SaveSlotBackup(player); }
    public static void Restore(Player player) { TryRestoreSlotBackup(player); }
}
public static class ZNet { public static bool IsSinglePlayer; }
public static class InventorySafety { public static bool RestoringSlotBackup; }
public class Inventory { }
public class Humanoid
{
    public Inventory GetInventory() { BackupCompatProbe.InventoryReads++; return new Inventory(); }
}
public class Player : Humanoid
{
    public static Player m_localPlayer;
    public bool m_isLoading;
}
"@
Add-Type -TypeDefinition $source
$player = New-Object Player
[Player]::m_localPlayer = $player
$cases = 0
foreach ($single in @($false, $true)) {
    foreach ($sc in @($false, $true)) {
        foreach ($sm in @($false, $true)) {
            [ZNet]::IsSinglePlayer = $single
            [BackupCompatProbe]::ServerCharacters = $sc
            [BackupCompatProbe]::ServerManager = $sm
            [BackupCompatProbe]::Lookups = 0
            $managed = -not $single -and ($sc -or $sm)
            if ([BackupCompatProbe]::Managed -ne $managed -or
                [BackupCompatProbe]::DetachedLoad -ne (-not $single -and $sc)) {
                throw "Policy mismatch: single=$single, SC=$sc, SM=$sm"
            }
            [BackupCompatProbe]::Mutations = 0
            [BackupCompatProbe]::InventoryReads = 0
            [BackupCompatProbe]::Save($player)
            [BackupCompatProbe]::Restore($player)
            $expected = if ($managed) { 0 } else { 2 }
            if ([BackupCompatProbe]::Mutations -ne $expected -or
                [BackupCompatProbe]::InventoryReads -ne 1) {
                throw 'Save/restore must return before backup mutation; restore must not inspect the inventory.'
            }
            if ($single -and [BackupCompatProbe]::Lookups -ne 0) {
                throw 'Single-player must short-circuit without plugin lookup.'
            }
            $cases++
        }
    }
}
Write-Output "PASS: $cases plugin/mode combinations; production save/restore guards, single-player short-circuit, and detached-load boundary."
