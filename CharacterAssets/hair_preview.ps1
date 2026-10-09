<#
Renders the game's detailed character under HDRP (the game's pipeline version), with or without the mod's looks.
This is where a look is judged before it goes into the bundle: see README.md, "The preview".

    .\preview.ps1 -Plan previews\plans\calibration.json -Out previews\calibration
    .\preview.ps1 -Plan ... -Out ... -Looks build      # with the candidate assets in build\

Needs the local reference (scripts\extract_preview.py, run on first use) and Unity 2022.3.62f2 with a GPU:
HDRP does not render with -nographics. Exit code 0 = every shot of the plan was written.
#>
param(
    [Parameter(Mandatory = $true)][string]$Plan,
    [Parameter(Mandatory = $true)][string]$Out,
    [string]$Looks = (Join-Path $PSScriptRoot 'build_hair'),
    [string]$GameData = 'G:\Steam\steamapps\common\Big Ambitions\Big Ambitions_Data',
    [string]$Unity = 'C:\Program Files\Unity\Hub\Editor\2022.3.62f2\Editor\Unity.exe'
)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
if (-not (Test-Path $Unity)) { Write-Host "Unity 2022.3.62f2 not found at $Unity (pass -Unity)."; exit 2 }

$reference = Join-Path $root '_reference\preview'
if (-not (Test-Path (Join-Path $reference 'character.json'))) {
    $blender = Get-ChildItem 'C:\Program Files\Blender Foundation' -Filter blender.exe -Recurse -ErrorAction SilentlyContinue |
        Sort-Object FullName -Descending | Select-Object -First 1 -ExpandProperty FullName
    if (-not $blender) { Write-Host 'Blender not found (its Python reads the game files). Run build.ps1 once first.'; exit 2 }
    $python = Get-ChildItem (Split-Path $blender) -Filter python.exe -Recurse | Select-Object -First 1 -ExpandProperty FullName
    $pylib = Join-Path $root '_tools\pylib'
    if (-not (Test-Path (Join-Path $pylib 'UnityPy'))) { & $python -m pip install --quiet --target $pylib UnityPy }
    Write-Host '== Reference from the game (meshes, materials, textures; local only)'
    & $python -I (Join-Path $root 'scripts\extract_preview.py') $GameData $reference $pylib
    if ($LASTEXITCODE -ne 0) { Write-Host 'FAILED: reference'; exit 1 }
}

# The preview runs the mod's own game-free look code, so what is judged here is what ships.
$project = Join-Path $root 'PreviewProject_hair'
$shared = Join-Path $project 'Assets\Editor\Shared'
New-Item -ItemType Directory -Force $shared | Out-Null
Get-ChildItem $shared -Filter *.cs -ErrorAction SilentlyContinue | Remove-Item -Force
$sharedList = Join-Path $root 'PreviewProject\shared_sources.txt'
if (Test-Path $sharedList) {
    foreach ($line in Get-Content $sharedList) {
        $line = $line.Trim()
        if ($line -and -not $line.StartsWith('#')) { Copy-Item (Join-Path $root "..\$line") $shared -Force }
    }
}

New-Item -ItemType Directory -Force $Out | Out-Null
$Out = (Resolve-Path $Out).Path
$Plan = (Resolve-Path $Plan).Path
$logs = Join-Path $project 'Logs'
New-Item -ItemType Directory -Force $logs | Out-Null
$log = Join-Path $logs 'preview.log'
# -batchmode: no editor window on the desktop (the GPU stays in use; never -nographics). Burst is switched off because
# Windows' Smart App Control refuses the unsigned helper DLL it compiles; HDRP then runs its managed code instead.
$arguments = @('-batchmode', '-burst-disable-compilation', '-projectPath', "`"$project`"", '-executeMethod', 'CharacterPreview.PreviewRunner.Run',
               '-reference', "`"$reference`"", '-plan', "`"$Plan`"", '-out', "`"$Out`"", '-logFile', "`"$log`"")
if (Test-Path $Looks) { $arguments += @('-looks', "`"$((Resolve-Path $Looks).Path)`"") }
$process = Start-Process -FilePath $Unity -ArgumentList $arguments -Wait -PassThru -WindowStyle Hidden
if (Test-Path $log) { Select-String -Path $log -Pattern '^\[CharacterPreview\]' | Select-Object -Last 4 | ForEach-Object { $_.Line } }
if ($process.ExitCode -ne 0) {
    Write-Host "Preview failed (exit code $($process.ExitCode)). Full log: $log"
    if (Test-Path $log) { Select-String -Path $log -Pattern 'error CS|Exception|FAILED|compiler errors|another Unity instance' | Select-Object -First 12 | ForEach-Object { $_.Line } }
    exit 1
}
Write-Host "OK: $Out"
