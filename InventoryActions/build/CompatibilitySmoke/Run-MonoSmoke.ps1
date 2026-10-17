[CmdletBinding()]
param(
    [string]$GamePath,
    [string]$ModDll,
    [ValidateSet('basic', 'muc-session', 'muc-restock', 'muc-api', 'ui-layout',
        'restock-reserve', 'button-modes', 'favorite-fill', 'material-access',
        'empty-favorite', 'epicloot-overlap', 'pov')]
    [string]$Mode = 'muc-session',
    [string]$MucDll,
    [string]$PovDll
)

$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
if (!$GamePath) {
    [xml]$settings = Get-Content -Raw -LiteralPath (Join-Path $repo 'environment.props')
    $GamePath = [string]$settings.Project.PropertyGroup.ValheimGamePath
}
$GamePath = (Resolve-Path -LiteralPath $GamePath).ProviderPath
if (!$ModDll) { $ModDll = Join-Path $repo 'InventoryActions/bin/Debug/InventoryActions.dll' }
$ModDll = (Resolve-Path -LiteralPath $ModDll).ProviderPath
$managed = Join-Path $GamePath 'valheim_Data/Managed'
$core = Join-Path $GamePath 'BepInEx/core'
$runtime = Join-Path $GamePath 'MonoBleedingEdge/EmbedRuntime/mono-2.0-bdwgc.dll'
$etc = Join-Path $GamePath 'MonoBleedingEdge/etc'
foreach ($path in @($runtime, $etc, "$managed/mscorlib.dll", "$managed/assembly_valheim.dll", "$core/0Harmony.dll", "$core/BepInEx.dll")) {
    if (!(Test-Path -LiteralPath $path)) { throw "Required original runtime file/directory is missing: $path" }
}
if ($Mode -in @('muc-restock', 'muc-api')) {
    if (!$MucDll) { throw "-$Mode requires -MucDll pointing to the original MultiUserChest.dll." }
    $MucDll = (Resolve-Path -LiteralPath $MucDll).ProviderPath
} elseif ($MucDll) { throw '-MucDll applies only to muc-restock or muc-api.' }
if ($Mode -eq 'pov') {
    if (!$PovDll) { throw '-Mode pov requires -PovDll pointing to the original PathOfValheiman.dll.' }
    $PovDll = (Resolve-Path -LiteralPath $PovDll).ProviderPath
} elseif ($PovDll) { throw '-PovDll applies only to pov.' }

# Build only the test executable. Build/deploy the final mod separately.
& dotnet build (Join-Path $PSScriptRoot 'CompatibilitySmoke.csproj') -c Debug `
    '-p:DeployToGame=false' "-p:ValheimGamePath=$GamePath" "-p:CorlibPath=$managed" "-p:BepInExPath=$(Join-Path $GamePath 'BepInEx')" --nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
if (!(Test-Path -LiteralPath $vswhere)) { throw 'Install Visual Studio C++ x64 build tools to build MonoHost.' }
$vcvars = & $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -find 'VC/Auxiliary/Build/vcvars64.bat'
if ($LASTEXITCODE -ne 0 -or !$vcvars) { throw 'Visual Studio C++ x64 build tools were not found.' }
$output = Join-Path $PSScriptRoot 'obj/MonoHost-x64'
[IO.Directory]::CreateDirectory($output) | Out-Null
$batch = Join-Path $output 'Build.cmd'
# Pass paths through the child environment, so cmd does not parse their contents
# as generated source. Keep delayed expansion disabled (paths can contain !).
[IO.File]::WriteAllText($batch, @'
@echo off
setlocal DisableDelayedExpansion
call "%IA_SMOKE_VCVARS%" >nul
if errorlevel 1 exit /b %errorlevel%
cl /nologo /W4 /WX /EHsc /MD /Fe:MonoHost.exe "%IA_SMOKE_SOURCE%"
exit /b %errorlevel%
'@, [Text.Encoding]::ASCII)
$start = New-Object Diagnostics.ProcessStartInfo
$start.FileName = $env:ComSpec
$start.Arguments = '/d /c Build.cmd'
$start.WorkingDirectory = $output
$start.UseShellExecute = $false
$start.CreateNoWindow = $true
$start.RedirectStandardOutput = $true
$start.RedirectStandardError = $true
$start.EnvironmentVariables['IA_SMOKE_VCVARS'] = [string]$vcvars
$start.EnvironmentVariables['IA_SMOKE_SOURCE'] = Join-Path $PSScriptRoot 'MonoHost.cpp'
$process = [Diagnostics.Process]::Start($start)
try {
    $compilerOutput = $process.StandardOutput.ReadToEndAsync()
    $compilerError = $process.StandardError.ReadToEndAsync()
    $process.WaitForExit()
    $compileExit = $process.ExitCode
    Write-Host $compilerOutput.GetAwaiter().GetResult()
    Write-Host $compilerError.GetAwaiter().GetResult()
} finally { $process.Dispose() }
if ($compileExit -ne 0) { exit $compileExit }

$test = Join-Path $PSScriptRoot 'bin/Debug/net48/CompatibilitySmoke.exe'
$testArgs = @($ModDll, $managed, $core)
if ($Mode -ne 'basic') { $testArgs += "--$Mode" }
if ($MucDll) { $testArgs += $MucDll }
if ($PovDll) { $testArgs += $PovDll }
Write-Host "Testing $ModDll using original Valheim x64 Mono ($Mode)."
& (Join-Path $output 'MonoHost.exe') $runtime $managed $etc $test @testArgs
$testExit = $LASTEXITCODE
Write-Host "Mono smoke process exit code: $testExit"
exit $testExit
