<#
Rebuilds every character asset of the mod from nothing:

    game files --extract--> _reference (local only)
    Blender scripts --> build\ (meshes, textures, catalogue) and previews\ (renders to look at)
    Unity --> Assets\Mods\RealLifeOverhaul\Bundles\reallife_character (what ships)

    .\build.ps1                 # everything
    .\build.ps1 -SkipPreviews   # no renders (much faster)
    .\build.ps1 -SkipBundle     # stop before Unity

Needs: Big Ambitions installed, Blender (winget install BlenderFoundation.Blender), Unity 2022.3.62f2.
See README.md.
#>
param(
    [string]$GameData = 'G:\Steam\steamapps\common\Big Ambitions\Big Ambitions_Data',
    [string]$Blender = '',
    [switch]$SkipPreviews,
    [switch]$SkipBundle
)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$scripts = Join-Path $root 'scripts'

if (-not $Blender) {
    $Blender = Get-ChildItem 'C:\Program Files\Blender Foundation' -Filter blender.exe -Recurse -ErrorAction SilentlyContinue |
        Sort-Object FullName -Descending | Select-Object -First 1 -ExpandProperty FullName
}
if (-not $Blender -or -not (Test-Path $Blender)) { Write-Host 'Blender not found. Install it: winget install BlenderFoundation.Blender'; exit 2 }
$python = Get-ChildItem (Split-Path $Blender) -Filter python.exe -Recurse | Select-Object -First 1 -ExpandProperty FullName
if (-not (Test-Path (Join-Path $GameData 'level1'))) { Write-Host "Game data not found at $GameData (pass -GameData)."; exit 2 }

function Step([string]$name, [scriptblock]$run) {
    Write-Host "== $name"
    & $run
    if ($LASTEXITCODE -ne 0) { Write-Host "FAILED: $name"; exit 1 }
}

# 1. Tools: the library that reads Unity files, and Pillow, into a local folder (nothing is installed system-wide).
$pylib = Join-Path $root '_tools\pylib'
if (-not (Test-Path (Join-Path $pylib 'UnityPy'))) {
    Step 'Python libraries (UnityPy, Pillow)' { & $python -m pip install --quiet --target $pylib UnityPy }
}

# 2. The game's rig and meshes, for fitting only. Never committed, never shipped.
$reference = Join-Path $root '_reference'
if (-not (Test-Path (Join-Path $reference 'index.json'))) {
    Step 'Reference from the game' { & $python -I (Join-Path $scripts 'extract_reference.py') $GameData $reference $pylib }
}

# 3. Assets.
$extra = @()
if ($SkipPreviews) { $extra = @('--', '--no-preview') }
Step 'Scalp underlays' { & $Blender -b -P (Join-Path $scripts 'build_underlays.py') @extra | Select-String -Pattern '^mesh |^underlay |Traceback|Error' }
Step 'Shoes' { & $Blender -b -P (Join-Path $scripts 'build_shoes.py') | Select-String -Pattern '^mesh |Traceback|Error' }
Step 'Garments (cut, drape, bake: a minute or two each)' { & $Blender -b -P (Join-Path $scripts 'build_garments.py') | Select-String -Pattern '^mesh |^drape|^garments|Traceback|Error' }
Step 'Hair strand maps' { & $python -I (Join-Path $scripts 'build_strands.py') (Join-Path $root 'build') $pylib }
Step 'Catalogue' { & $python (Join-Path $scripts 'make_catalogue.py') }

# 4. The bundle the mod ships, and the check that it loads.
if (-not $SkipBundle) {
    Step 'Unity bundle' { & (Join-Path $root 'build_bundle.ps1') }
}
Write-Host 'Done. Look at the renders in previews\ before committing.'
