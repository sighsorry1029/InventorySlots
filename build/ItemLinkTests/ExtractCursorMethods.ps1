$ErrorActionPreference = 'Stop'
$source = [IO.File]::ReadAllText((Join-Path $PSScriptRoot '../../ItemLinkUi.cs'))
$parts = [Collections.Generic.List[string]]::new()
$parts.Add('using System; using Splatform; using UnityEngine; namespace InventorySlots; public sealed partial class InventorySlotsPlugin {')
$property = [regex]::Match($source, '(?ms)^    private static bool ItemLinkChatFocused =>.*?;')
if (!$property.Success) { throw 'Missing chat-focus property' }
$parts.Add($property.Value)
foreach ($name in @('KeepItemLinkChatCursorFree', 'UpdateItemLinkPointerLease', 'CaptureItemLinkChatInput',
                   'UpdateItemLinkPinInput', 'IsItemLinkInputBlocked', 'RestoreItemLinkPinFocus', 'ConsumeItemLinkPin',
                   'GetItemLinkHoverPosition', 'GetItemLinkSortingCanvas', 'UpdateItemLinkCanvasOrder',
                   'ReceiveItemLinkRequest', 'ReceiveItemLinkResponse', 'RequestItemLink', 'FormatItemLinkBody', 'FilterItemLinkSnapshot')) {
    if ($name -eq 'ReceiveItemLinkRequest') {
        $source = [IO.File]::ReadAllText((Join-Path $PSScriptRoot '../../ItemLinks.cs'))
        foreach ($helper in @('CanUseItemLinkEntry', 'ItemLinkSessionValid')) {
            $expression = [regex]::Match($source, "(?ms)^    private static bool $helper\(.*?;")
            if (!$expression.Success) { throw "Missing policy: $helper" }
            $parts.Add($expression.Value)
        }
    }
    $match = [regex]::Match($source, "(?m)^    (?:private|internal) static [^\r\n]*\b$name\(")
    if (!$match.Success) { throw "Missing production method: $name" }
    $open = $source.IndexOf('{', $match.Index)
    $depth = 1; $end = $open + 1
    # These selected block bodies have no string literals containing braces.
    while ($depth -gt 0 -and $end -lt $source.Length) {
        if ($source[$end] -eq '{') { ++$depth }
        if ($source[$end] -eq '}') { --$depth }
        ++$end
    }
    if ($depth -ne 0) { throw "Unbalanced method: $name" }
    $parts.Add($source.Substring($match.Index, $end - $match.Index))
}
$parts.Add('}')
$output = Join-Path $PSScriptRoot 'obj/CursorMethods.cs'
New-Item -ItemType Directory -Path (Split-Path $output) -Force | Out-Null
[IO.File]::WriteAllText($output, ($parts -join "`n"))
