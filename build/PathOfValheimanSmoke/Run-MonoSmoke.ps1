[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$PovDll,
    [Parameter(Mandatory = $true)][string]$OptionalDependencyDirectory,
    [string]$GamePath,
    [string]$ModDll,
    [ValidateSet('cold', 'warm')][string]$Mode = 'cold'
)

$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if (!$GamePath) {
    [xml]$settings = Get-Content -Raw -LiteralPath (Join-Path $repo 'environment.props')
    $GamePath = [string]$settings.Project.PropertyGroup.ValheimGamePath
}
$GamePath = (Resolve-Path -LiteralPath $GamePath).ProviderPath
if (!$ModDll) { $ModDll = Join-Path $repo 'bin/Debug/InventorySlots.dll' }
$ModDll = (Resolve-Path -LiteralPath $ModDll).ProviderPath
$PovDll = (Resolve-Path -LiteralPath $PovDll).ProviderPath
$OptionalDependencyDirectory = (Resolve-Path -LiteralPath $OptionalDependencyDirectory).ProviderPath
$managed = Join-Path $GamePath 'valheim_Data/Managed'
$core = Join-Path $GamePath 'BepInEx/core'
$runtime = Join-Path $GamePath 'MonoBleedingEdge/EmbedRuntime/mono-2.0-bdwgc.dll'
$etc = Join-Path $GamePath 'MonoBleedingEdge/etc'

# Only the isolated test executable is built here. Build/deploy the mod separately.
& dotnet build (Join-Path $PSScriptRoot 'StorageSmoke.csproj') -c Debug "-p:GameRoot=$GamePath" -p:DeployToGame=false --nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
if (!(Test-Path -LiteralPath $vswhere)) { throw 'Visual Studio C++ x64 build tools are required for MonoHost.' }
$vcvars = & $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -find 'VC/Auxiliary/Build/vcvars64.bat'
if ($LASTEXITCODE -ne 0 -or !$vcvars) { throw 'Visual Studio C++ x64 build tools were not found.' }
$output = Join-Path $PSScriptRoot 'obj/MonoHost-x64'
[IO.Directory]::CreateDirectory($output) | Out-Null
$batch = Join-Path $output 'Build.cmd'
[IO.File]::WriteAllText($batch, @'
@echo off
setlocal DisableDelayedExpansion
call "%POV_SMOKE_VCVARS%" >nul
if errorlevel 1 exit /b %errorlevel%
cl /nologo /W4 /WX /EHsc /MD /Fe:MonoHost.exe "%POV_SMOKE_SOURCE%"
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
$start.EnvironmentVariables['POV_SMOKE_VCVARS'] = [string]$vcvars
# Reuse the repository's audited native host source; no second runtime wrapper.
$start.EnvironmentVariables['POV_SMOKE_SOURCE'] = Join-Path $repo 'InventoryActions/build/CompatibilitySmoke/MonoHost.cpp'
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

$test = Join-Path $PSScriptRoot 'bin/Debug/net48/StorageSmoke.exe'
Get-FileHash -LiteralPath $ModDll,$PovDll,"$managed/assembly_valheim.dll",$runtime -Algorithm SHA256 |
    Format-List | Out-File -LiteralPath (Join-Path $output "$Mode-inputs.txt") -Encoding utf8
& (Join-Path $output 'MonoHost.exe') $runtime $managed $etc $test $ModDll $PovDll $managed $core $Mode $OptionalDependencyDirectory 2>&1 |
    Tee-Object -FilePath (Join-Path $output "$Mode.log")
$result = $LASTEXITCODE
"ProcessExitCode=$result" | Add-Content -LiteralPath (Join-Path $output "$Mode.log")
exit $result
