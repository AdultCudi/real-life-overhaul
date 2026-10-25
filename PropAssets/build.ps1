<#
Rebuilds the mod's prop bundles from nothing, one set of sets.json after another (README, "The bundles"):

  the first set (reallife_props, closed: the procedural props of scripts\props.py)
    scripts\atlas.py        --> build\textures (the shared atlas) and build\atlas.json
    scripts\decals.py       --> build\textures\decal_*.png (road paint laid by decal projectors: a bus stop's lettering, its broken line)
    scripts\build_props.py  --> build\meshes, build\catalogue.json and previews\ (renders to look at)
    scripts\check_life.py   --> fails if a handle or a height that an animation clip relies on has moved
    scripts\scenes.py       --> previews\scene_<name>.png: whole scenes (a precinct frontage, a bus stop, a fire, ...)
  every later set (reallife_props_2, open: the Blender exports in models\)
    scripts\models.py --scan --> build_2\models.json and each model's picture
    scripts\atlas.py        --> build_2\textures (its own atlas, as small as its props allow; none while it is empty)
    scripts\build_props.py  --> build_2\meshes, build_2\catalogue.json and previews\
  for each set
    Unity                   --> Assets\Mods\RealLifeOverhaul\Bundles\<bundle> (what ships), loaded back as a check,
                                and contents\<bundle>.json, what it holds (committed with it)

    .\build.ps1                 # everything
    .\build.ps1 -Set second     # one set
    .\build.ps1 -SkipPreviews   # no renders
    .\build.ps1 -SkipBundle     # stop before Unity
    .\build.ps1 -Test           # proves the path of a model from Blender to a bundle with two made-up models (a .glb
                                # and an .fbx) built as the second set into build_test\, which ships nothing

Needs: Blender (winget install BlenderFoundation.Blender), Unity 2022.3.62f2. The game is not needed. See README.md.
#>
param(
    [string]$Blender = '',
    [string[]]$Set = @(),
    [switch]$SkipPreviews,
    [switch]$SkipBundle,
    [switch]$Test
)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$scripts = Join-Path $root 'scripts'
$sets = (Get-Content (Join-Path $root 'sets.json') -Raw | ConvertFrom-Json).sets

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

# Pillow, for painting the atlas, into a local folder (nothing is installed system-wide).
$pylib = Join-Path $root '_tools\pylib'
if (-not (Test-Path (Join-Path $pylib 'PIL'))) {
    Step 'Python library (Pillow)' { & $python -m pip install --quiet --target $pylib Pillow }
}
$extra = @()
if ($SkipPreviews -or $Test) { $extra = @('--', '--no-preview') }
$shown = '^mesh |^model |^models:|^catalogue|^atlas|OVER BUDGET|PROP IDS|Traceback|Error|models/'

function Build-Set($entry, [string]$folder, [string]$out) {
    $env:PROPS_SET = $entry.name
    $env:PROPS_BUILD = $folder
    try {
        if ($entry -eq $sets[0]) {
            Step "Atlas ($($entry.bundle))" { & $python (Join-Path $scripts 'atlas.py') }
            Step 'Decals' { & $python (Join-Path $scripts 'decals.py') }
            Step "Props ($($entry.bundle))" { & $Blender -b -P (Join-Path $scripts 'build_props.py') @extra | Select-String -Pattern $shown }
            Step 'Grips and heights the clips rely on' { & $python (Join-Path $scripts 'check_life.py') | Select-String -Pattern 'WRONG|all as contracted|Traceback|Error' }
            if (-not $SkipPreviews) {
                Step 'Scenes' { & $Blender -b -P (Join-Path $scripts 'scenes.py') | Select-String -Pattern '^scene |Traceback|Error' }
            }
        } else {
            Step "Models ($($entry.bundle))" { & $Blender -b -P (Join-Path $scripts 'models.py') -- --scan | Select-String -Pattern $shown }
            Step "Atlas ($($entry.bundle))" { & $python (Join-Path $scripts 'atlas.py') }
            Step "Props ($($entry.bundle))" { & $Blender -b -P (Join-Path $scripts 'build_props.py') @extra | Select-String -Pattern $shown }
        }
        if (-not $SkipBundle) {
            $where = @{ Set = $entry.name; BuildFolder = $folder }
            if ($out) { $where.OutFolder = $out }
            Step "Unity bundle ($($entry.bundle))" { & (Join-Path $root 'build_bundle.ps1') @where }
        }
    } finally {
        Remove-Item Env:PROPS_SET -ErrorAction SilentlyContinue
        Remove-Item Env:PROPS_BUILD -ErrorAction SilentlyContinue
    }
}

if ($Test) {
    # Two made-up models through the second set's whole path, into build_test\ (git-ignored): nothing of it ships.
    $folder = Join-Path $root 'build_test'
    if (Test-Path $folder) { Remove-Item -Recurse -Force $folder }
    $models = Join-Path $folder 'models'
    Step 'Test models (made in Blender, exported as .glb and .fbx)' { & $Blender -b -P (Join-Path $scripts 'test_models.py') -- $models | Select-String -Pattern '^test |Traceback|Error' }
    $env:PROPS_MODELS = $models
    try {
        Build-Set ($sets | Where-Object { $_.open } | Select-Object -Last 1) (Join-Path $folder 'build') (Join-Path $folder 'bundle')
    } finally {
        Remove-Item Env:PROPS_MODELS -ErrorAction SilentlyContinue
    }
    Write-Host "Test build done: $(Join-Path $folder 'bundle'). Nothing in Bundles\ changed."
    exit 0
}

foreach ($entry in $sets) {
    if ($Set.Count -gt 0 -and $Set -notcontains $entry.name) { continue }
    Build-Set $entry (Join-Path $root $entry.build) ''
}
Write-Host 'Done. Look at the renders in previews\ before committing.'
