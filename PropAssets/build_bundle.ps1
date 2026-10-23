<#
Builds the mod's street prop asset bundle from the interchange folder and checks that it loads.

    .\build_bundle.ps1                       # PropAssets\build -> Assets\Mods\RealLifeOverhaul\Bundles\reallife_props
    .\build_bundle.ps1 -BuildFolder X -OutFolder Y

Needs Unity 2022.3.62f2 (the game's version). Nothing of the game's is used: the bundle holds meshes,
textures and the catalogue only. Exit code 0 = built and verified.
#>
param(
    [string]$BuildFolder = (Join-Path $PSScriptRoot 'build'),
    [string]$OutFolder = (Join-Path $PSScriptRoot '..\Assets\Mods\RealLifeOverhaul\Bundles'),
    [string]$Unity = 'C:\Program Files\Unity\Hub\Editor\2022.3.62f2\Editor\Unity.exe',
    [string]$BundleName = 'reallife_props'
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
    $arguments = @('-batchmode', '-quit', '-nographics', '-burst-disable-compilation', '-projectPath', "`"$project`"", '-executeMethod', $method,
                   '-buildFolder', "`"$BuildFolder`"", '-outFolder', "`"$OutFolder`"", '-bundleName', $BundleName, '-logFile', "`"$log`"")
    $process = Start-Process -FilePath $Unity -ArgumentList $arguments -Wait -PassThru -NoNewWindow
    if (Test-Path $log) { Select-String -Path $log -Pattern '^\[PropBundle\]' | ForEach-Object { $_.Line } }
    if ($process.ExitCode -ne 0) {
        Write-Host "$method failed (exit code $($process.ExitCode)). Full log: $log"
        if (Test-Path $log) { Select-String -Path $log -Pattern 'error CS|Exception|Scripts have compiler errors|another Unity instance' | Select-Object -First 10 | ForEach-Object { $_.Line } }
        exit 1
    }
}

Invoke-Unity 'PropBundleBuilder.Build' (Join-Path $logs 'build.log')
Invoke-Unity 'PropBundleBuilder.Verify' (Join-Path $logs 'verify.log')
# Props shipped beside the bundle while it could not be built (scripts/newer_props.py) are in it now: the mod would
# otherwise go on using those copies in place of the bundle's.
$newer = Join-Path $OutFolder 'props_newer'
if ($BundleName -eq 'reallife_props' -and (Test-Path $newer)) { Remove-Item -Recurse -Force $newer; Write-Host "Removed $($newer), the bundle has those props now." }
Write-Host "OK: $(Join-Path $OutFolder $BundleName)"
