<#
Builds the mod's animation clip bundle from the interchange folder and proves that the clips play.

    .\build_bundle.ps1                       # CityAssets\build\anim -> Assets\Mods\RealLifeOverhaul\Bundles\reallife_anim
    .\build_bundle.ps1 -VerifyOnly           # only the check and the contact sheets (previews\unity_<clip>.jpg)

Needs Unity 2022.3.62f2 (the game's version). Nothing of the game's is used. The check plays every clip through
the mod's own player: its source files (Scripts\CityLife\Animation) are copied into the Unity project first, so what
is tested is what ships. Exit code 0 = built and verified.
#>
param(
    [string]$BuildFolder = (Join-Path $PSScriptRoot 'build\anim'),
    [string]$OutFolder = (Join-Path $PSScriptRoot '..\Assets\Mods\RealLifeOverhaul\Bundles'),
    [string]$Unity = 'C:\Program Files\Unity\Hub\Editor\2022.3.62f2\Editor\Unity.exe',
    [switch]$VerifyOnly
)
$ErrorActionPreference = 'Stop'
if (-not (Test-Path $Unity)) { Write-Host "Unity 2022.3.62f2 not found at $Unity (pass -Unity)."; exit 2 }
if (-not $VerifyOnly -and -not (Test-Path (Join-Path $BuildFolder 'clips.json'))) { Write-Host "No clips.json in $BuildFolder. Run the Blender script first."; exit 2 }

$project = Join-Path $PSScriptRoot 'UnityProject'
New-Item -ItemType Directory -Force $OutFolder | Out-Null
$OutFolder = (Resolve-Path $OutFolder).Path
$logs = Join-Path $project 'Logs'
New-Item -ItemType Directory -Force $logs | Out-Null

# The mod's own player, as it ships.
$shared = Join-Path $project 'Assets\Shared'
if (Test-Path $shared) { Remove-Item -Recurse -Force $shared }
New-Item -ItemType Directory -Force $shared | Out-Null
Copy-Item (Join-Path $PSScriptRoot '..\Assets\Mods\RealLifeOverhaul\Scripts\CityLife\Animation\*.cs') $shared

function Invoke-Unity([string]$method, [string]$log, [bool]$graphics, [bool]$quit = $true) {
    $arguments = @('-batchmode', '-burst-disable-compilation', '-projectPath', "`"$project`"", '-executeMethod', $method,
                   '-buildFolder', "`"$BuildFolder`"", '-outFolder', "`"$OutFolder`"", '-logFile', "`"$log`"")
    if (-not $graphics) { $arguments += '-nographics' }
    if ($quit) { $arguments += '-quit' }
    $process = Start-Process -FilePath $Unity -ArgumentList $arguments -Wait -PassThru -NoNewWindow
    if (Test-Path $log) { Select-String -Path $log -Pattern '^\[AnimBundle\]' | ForEach-Object { $_.Line } }
    if ($process.ExitCode -ne 0) {
        Write-Host "$method failed (exit code $($process.ExitCode)). Full log: $log"
        if (Test-Path $log) { Select-String -Path $log -Pattern 'error CS|Exception|Scripts have compiler errors|another Unity instance' | Select-Object -First 12 | ForEach-Object { $_.Line } }
        exit 1
    }
}

if (-not $VerifyOnly) { Invoke-Unity 'AnimBundleBuilder.Build' (Join-Path $logs 'build.log') $false }
Invoke-Unity 'AnimBundleBuilder.Verify' (Join-Path $logs 'verify.log') $true      # with a graphics device: it draws the contact sheets
Invoke-Unity 'AnimBundleBuilder.Prove' (Join-Path $logs 'prove.log') $true $false     # play mode: the engine's own frame loop; exits by itself
Write-Host "OK: $(Join-Path $OutFolder 'reallife_anim')"
