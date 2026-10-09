<#
Builds the mod's character asset bundle from the interchange folder and checks that it loads.

    .\build_bundle.ps1                       # CharacterAssets\build -> Assets\Mods\RealLifeOverhaul\Bundles\reallife_character
    .\build_bundle.ps1 -BuildFolder X -OutFolder Y

Needs Unity 2022.3.62f2 (the game's version). Nothing of the game's is used: the bundle holds meshes,
textures and the catalogue only. Exit code 0 = built and verified.
#>
param(
    [string]$BuildFolder = (Join-Path $PSScriptRoot 'build'),
    [string]$OutFolder = (Join-Path $PSScriptRoot '..\Assets\Mods\RealLifeOverhaul\Bundles'),
    [string]$Unity = 'C:\Program Files\Unity\Hub\Editor\2022.3.62f2\Editor\Unity.exe'
)
$ErrorActionPreference = 'Stop'
if (-not (Test-Path $Unity)) { Write-Host "Unity 2022.3.62f2 not found at $Unity (pass -Unity)."; exit 2 }
if (-not (Test-Path (Join-Path $BuildFolder 'catalogue.json'))) { Write-Host "No catalogue.json in $BuildFolder. Run the Blender scripts first."; exit 2 }

$project = Join-Path $PSScriptRoot 'UnityProject'
$BuildFolder = (Resolve-Path $BuildFolder).Path
New-Item -ItemType Directory -Force $OutFolder | Out-Null
$OutFolder = (Resolve-Path $OutFolder).Path
$logs = Join-Path $project 'Logs'
New-Item -ItemType Directory -Force $logs | Out-Null

function Invoke-Unity([string]$method, [string]$log) {
    $arguments = @('-batchmode', '-quit', '-nographics', '-projectPath', "`"$project`"", '-executeMethod', $method,
                   '-buildFolder', "`"$BuildFolder`"", '-outFolder', "`"$OutFolder`"", '-logFile', "`"$log`"")
    $process = Start-Process -FilePath $Unity -ArgumentList $arguments -Wait -PassThru -NoNewWindow
    if (Test-Path $log) { Select-String -Path $log -Pattern '^\[CharacterBundle\]' | ForEach-Object { $_.Line } }
    if ($process.ExitCode -ne 0) {
        Write-Host "$method failed (exit code $($process.ExitCode)). Full log: $log"
        if (Test-Path $log) { Select-String -Path $log -Pattern 'error CS|Exception|Scripts have compiler errors|another Unity instance' | Select-Object -First 10 | ForEach-Object { $_.Line } }
        exit 1
    }
}

Invoke-Unity 'CharacterBundleBuilder.Build' (Join-Path $logs 'build.log')
Invoke-Unity 'CharacterBundleBuilder.Verify' (Join-Path $logs 'verify.log')
Write-Host "OK: $(Join-Path $OutFolder 'reallife_character')"
