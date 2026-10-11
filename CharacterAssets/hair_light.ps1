<#
Hair against a strong light: renders every hairstyle of the game at night from the mod's third-person camera with one
lamp 3 m from the head (behind it as the camera sees it, beside it, at the camera's side), as the game's own hair, as the
finish was before 11 October and as it is, and writes the sheets and the measurements kept in evidence\hair-light.

    .\hair_light.ps1               # pictures (1 008 shots) and numbers (510 shots): about an hour
    .\hair_light.ps1 -Only measure # only the numbers -> evidence\hair-light\measured.txt
    .\hair_light.ps1 -Only pictures

Needs what preview.ps1 needs (Unity 2022.3.62f2 with a licence: Unity Hub signed in and running, a GPU, the local
reference) and build\ with the strand maps and the underlays (build.ps1 -SkipPreviews -SkipBundle). See README.md,
"Hair against a strong light".
#>
param([ValidateSet('both', 'pictures', 'measure')][string]$Only = 'both')
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$blender = Get-ChildItem 'C:\Program Files\Blender Foundation' -Filter blender.exe -Recurse | Sort-Object FullName -Descending | Select-Object -First 1 -ExpandProperty FullName
$python = Get-ChildItem (Split-Path $blender) -Filter python.exe -Recurse | Select-Object -First 1 -ExpandProperty FullName
$plans = Join-Path $root 'previews\plans'
New-Item -ItemType Directory -Force $plans | Out-Null
$names = @()
if ($Only -ne 'measure') { $names += 'hair_light' }
if ($Only -ne 'pictures') { $names += 'hair_light_measure' }
foreach ($name in $names) {
    $plan = Join-Path $plans "$name.json"
    & $python -I (Join-Path $root 'scripts\preview_plan.py') $name $plan
    if ($LASTEXITCODE -ne 0) { exit 1 }
    $out = Join-Path $root "previews\$name"
    if (Test-Path $out) { Remove-Item (Join-Path $out '*.png') -Force }
    & (Join-Path $root 'preview.ps1') -Plan $plan -Out $out | Select-Object -Last 1
    if ($LASTEXITCODE -ne 0) { exit 1 }
}
& $python -I (Join-Path $root 'scripts\hair_light_sheet.py') (Join-Path $root '_tools\pylib') (Join-Path $root 'previews\hair_light') `
    (Join-Path $root 'previews\hair_light_measure') (Join-Path $root 'evidence\hair-light')
if ($LASTEXITCODE -ne 0) { exit 1 }
Write-Host "OK: $(Join-Path $root 'evidence\hair-light')"
