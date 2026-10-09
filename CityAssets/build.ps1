<#
Rebuilds the mod's animation clips from nothing:

    scripts\clips.py + rig.py   --> build\anim (one FBX per clip, the rig in its T-pose, clips.json) and previews\<clip>.png
    Unity                       --> Assets\Mods\RealLifeOverhaul\Bundles\reallife_anim (what ships), then the checks:
                                    every clip played back on the game's skeleton and measured, previews\unity_<clip>.jpg,
                                    and the play-mode proof that clips cross-fade over an Animator Controller and hand back

    .\build.ps1                 # everything
    .\build.ps1 -SkipPreviews   # no Blender contact sheets
    .\build.ps1 -SkipBundle     # stop before Unity

Needs: Blender (winget install BlenderFoundation.Blender), Unity 2022.3.62f2, and PropAssets\build (run PropAssets\build.ps1
once: the sheets show the tools). The game is not needed. See README.md.
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

Step 'Reach' { & $python -I (Join-Path $scripts 'check_clips.py') }
$extra = @()
if ($SkipPreviews) { $extra = @('--', '--no-preview') }
Step 'Clips' { & $Blender -b -P (Join-Path $scripts 'build_clips.py') @extra | Select-String -Pattern '^clip |^clips.json|OUT OF REACH|Traceback|Error' }
if (-not $SkipBundle) {
    Step 'Unity bundle, checks and proof' { & (Join-Path $root 'build_bundle.ps1') }
}
Write-Host 'Done. Look at previews\unity_<clip>.jpg before committing.'
