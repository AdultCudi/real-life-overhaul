<#
Builds the bundle of the mod's own cars (reallife_cars) from the interchange folders in cars\ (one a car: car.json,
meshes\*.json, textures\*.png, exported from Blender), loads it back as a check, and writes what it holds to
contents\reallife_cars.json, which is committed with it and which EconomyCheck --mod-cars holds the code against.

    .\build.ps1

Needs Unity 2022.3.62f2 (the game's version), signed in through Unity Hub. The game is not needed: the bundle holds
meshes and textures only. Exit code 0 = built and verified.
#>
param(
    [string]$Unity = 'C:\Program Files\Unity\Hub\Editor\2022.3.62f2\Editor\Unity.exe'
)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
if (-not (Test-Path $Unity)) { Write-Host "Unity 2022.3.62f2 not found at $Unity (pass -Unity)."; exit 2 }
$project = Join-Path $root 'UnityProject'
$logs = Join-Path $project 'Logs'
New-Item -ItemType Directory -Force $logs | Out-Null

function Invoke-Unity([string]$method, [string]$log) {
    $arguments = @('-batchmode', '-quit', '-nographics', '-burst-disable-compilation', '-projectPath', "`"$project`"", '-executeMethod', $method, '-logFile', "`"$log`"")
    $process = Start-Process -FilePath $Unity -ArgumentList $arguments -Wait -PassThru -NoNewWindow
    if (Test-Path $log) { Select-String -Path $log -Pattern '^\[CarBundle\]' | ForEach-Object { $_.Line } }
    if ($process.ExitCode -ne 0) {
        Write-Host "$method failed (exit code $($process.ExitCode)). Full log: $log"
        if (Test-Path $log) { Select-String -Path $log -Pattern 'error CS|Exception|Scripts have compiler errors|another Unity instance|licens' | Select-Object -First 12 | ForEach-Object { $_.Line } }
        exit 1
    }
}

Invoke-Unity 'CarBundleBuilder.Build' (Join-Path $logs 'build.log')
Invoke-Unity 'CarBundleBuilder.Verify' (Join-Path $logs 'verify.log')
# The plain files the mod read while there was no bundle are in it now: left there, nobody would see whether the bundle's are used.
$loose = Join-Path $root '..\Assets\Mods\RealLifeOverhaul\Bundles\cars_newer'
if (Test-Path $loose) { Remove-Item -Recurse -Force $loose; Write-Host "Removed $loose, the bundle has those cars now." }
Write-Host 'OK: Assets\Mods\RealLifeOverhaul\Bundles\reallife_cars'
