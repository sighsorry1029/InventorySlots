$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$parts = [Collections.Generic.List[string]]::new()
$parts.Add('using System; using System.Reflection; using HarmonyLib; using UnityEngine; using UnityEngine.UI; namespace InventoryActions; public sealed partial class InventoryActionsPlugin {')
$sources = @{
    'Ui.cs' = @('UpdateContainerActionButtons','AlignContainerActionButtonRows','LayoutButtonPair','LayoutContainerSortButton','LayoutButtonRect','CopyRectTransformFrame','HideContainerActionButtons','CaptureRectTransformSnapshot','RestoreContainerActionButtonLayout','ReleaseContainerActionButtonLayout','SetButtonActive','SetButtonInteractable','GetRectWidth','GetRectHeight')
    'Favorites.cs' = @('GetUnpatchedFavoriteGridWidth','UpdateFavoriteBorders')
}
foreach ($file in $sources.Keys) {
    $source = [IO.File]::ReadAllText((Join-Path $root "InventoryActions/$file"))
    if ($file -eq 'Favorites.cs') {
        foreach ($field in [regex]::Matches($source, '(?m)^    private static readonly [^\r\n]+;')) { $parts.Add($field.Value) }
    }
    foreach ($name in $sources[$file]) {
        $matches = [regex]::Matches($source, "(?m)^    (?:private|internal) static [^\r\n]*\b$name\(")
        if ($matches.Count -eq 0) { throw "Missing production method: $file $name" }
        foreach ($match in $matches) {
            $start = $match.Index
            $open = $source.IndexOf('{', $start)
            $depth = 1
            $end = $open + 1
            # Selected methods have ordinary block bodies and no braces in string literals.
            while ($depth -gt 0 -and $end -lt $source.Length) {
                if ($source[$end] -eq '{') { ++$depth }
                if ($source[$end] -eq '}') { --$depth }
                ++$end
            }
            if ($depth -ne 0) { throw "Unbalanced production method: $name" }
            $parts.Add($source.Substring($start, $end - $start))
        }
    }
}
$parts.Add('}')
$output = Join-Path $PSScriptRoot 'obj/ProductionMethods.cs'
New-Item -ItemType Directory -Path (Split-Path $output) -Force | Out-Null
[IO.File]::WriteAllText($output, ($parts -join "`n"))
