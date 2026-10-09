<#
A garment's evidence sheet: plan -> HDRP preview -> one labelled sheet.

    .\garment.ps1 legs_m_jeans                 # every shot -> evidence\garment_legs_m_jeans.jpg
    .\garment.ps1 legs_m_jeans -Only mod,walk  # only the shots whose label starts so -> previews\garment_legs_m_jeans.png

The shots are listed in scripts\preview_plan.py (GARMENTS, garment): the game's nearest piece beside the mod's from
three sides, the poses, the hem on each shoe, the three builds, every colourway and the play distance.
#>
param([Parameter(Mandatory = $true)][string]$Id, [string]$Only = '', [int]$Columns = 8)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$blender = Get-ChildItem 'C:\Program Files\Blender Foundation' -Filter blender.exe -Recurse | Sort-Object FullName -Descending | Select-Object -First 1 -ExpandProperty FullName
$python = Get-ChildItem (Split-Path $blender) -Filter python.exe -Recurse | Select-Object -First 1 -ExpandProperty FullName
$plans = Join-Path $root 'previews\plans'
New-Item -ItemType Directory -Force $plans | Out-Null
$plan = Join-Path $plans "$Id.json"
if ($Only) { & $python -I (Join-Path $root 'scripts\preview_plan.py') $Id $plan $Only } else { & $python -I (Join-Path $root 'scripts\preview_plan.py') $Id $plan }
if ($LASTEXITCODE -ne 0) { exit 1 }
$out = Join-Path $root "previews\$Id"
if (Test-Path $out) { Remove-Item (Join-Path $out '*.png') -Force }
& (Join-Path $root 'preview.ps1') -Plan $plan -Out $out | Select-Object -Last 1
if ($LASTEXITCODE -ne 0) { exit 1 }
$items = Get-ChildItem $out -Filter *.png | Sort-Object Name | ForEach-Object { ($_.BaseName.Substring(3)) + '=' + $_.FullName }
$sheet = if ($Only) { Join-Path $root "previews\garment_$Id.png" } else { Join-Path $root "evidence\garment_$Id.jpg" }
& $python -I (Join-Path $root 'scripts\preview_sheet.py') (Join-Path $root '_tools\pylib') $sheet $Columns 0.5 @items
