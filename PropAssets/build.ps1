<#
Rebuilds every street prop of the mod from nothing:

    scripts\atlas.py        --> build\textures (the shared atlas) and build\atlas.json
    scripts\decals.py       --> build\textures\decal_*.png (road paint laid by decal projectors: a bus stop's lettering, its broken line)
    scripts\build_props.py  --> build\meshes, build\catalogue.json and previews\ (renders to look at)
    scripts\check_life.py   --> fails if a handle or a height that an animation clip relies on has moved
    scripts\scenes.py       --> previews\scene_<name>.png: whole scenes (a precinct frontage, a bus stop, a fire, ...)
    Unity                   --> Assets\Mods\RealLifeOverhaul\Bundles\reallife_props (what ships), loaded back as a check

    .\build.ps1                 # everything
    .\build.ps1 -SkipPreviews   # no renders
    .\build.ps1 -SkipBundle     # stop before Unity

Needs: Blender (winget install BlenderFoundation.Blender), Unity 2022.3.62f2. The game is not needed. See README.md.
#>
param(
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

function Step([string]$name, [scriptblock]$run) {
    Write-Host "== $name"
    & $run
    if ($LASTEXITCODE -ne 0) { Write-Host "FAILED: $name"; exit 1 }
}

# 1. Pillow, for painting the atlas, into a local folder (nothing is installed system-wide).
$pylib = Join-Path $root '_tools\pylib'
if (-not (Test-Path (Join-Path $pylib 'PIL'))) {
    Step 'Python library (Pillow)' { & $python -m pip install --quiet --target $pylib Pillow }
}

# 2. The atlas, the road paint laid as decals, then the props mapped onto the atlas.
Step 'Atlas' { & $python (Join-Path $scripts 'atlas.py') }
Step 'Decals' { & $python (Join-Path $scripts 'decals.py') }
$extra = @()
if ($SkipPreviews) { $extra = @('--', '--no-preview') }
Step 'Props' { & $Blender -b -P (Join-Path $scripts 'build_props.py') @extra | Select-String -Pattern '^mesh |^catalogue|OVER BUDGET|Traceback|Error' }
Step 'Grips and heights the clips rely on' { & $python (Join-Path $scripts 'check_life.py') | Select-String -Pattern 'WRONG|all as contracted|Traceback|Error' }
if (-not $SkipPreviews) {
    Step 'Scenes' { & $Blender -b -P (Join-Path $scripts 'scenes.py') | Select-String -Pattern '^scene |Traceback|Error' }
}

# 3. The bundle the mod ships, and the check that it loads.
if (-not $SkipBundle) {
    Step 'Unity bundle' { & (Join-Path $root 'build_bundle.ps1') }
}
Write-Host 'Done. Look at the renders in previews\ before committing.'
