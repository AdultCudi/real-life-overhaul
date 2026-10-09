<#
Working loop for a look: writes a plan, renders it in the HDRP preview and lays the shots out in one sheet per view.

    .\trial.ps1 hair_trial            # plan name from scripts\preview_plan.py -> previews\hair_trial_<view>.png
#>
param([Parameter(Mandatory = $true)][string]$Name, [int]$Columns = 0)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$blender = Get-ChildItem 'C:\Program Files\Blender Foundation' -Filter blender.exe -Recurse | Sort-Object FullName -Descending | Select-Object -First 1 -ExpandProperty FullName
$python = Get-ChildItem (Split-Path $blender) -Filter python.exe -Recurse | Select-Object -First 1 -ExpandProperty FullName
$plans = Join-Path $root 'previews\plans'
New-Item -ItemType Directory -Force $plans | Out-Null
$plan = Join-Path $plans "$Name.json"
& $python -I (Join-Path $root 'scripts\preview_plan.py') $Name $plan
if ($LASTEXITCODE -ne 0) { exit 1 }
$out = Join-Path $root "previews\$Name"
if (Test-Path $out) { Remove-Item (Join-Path $out '*.png') -Force }
& (Join-Path $root 'preview.ps1') -Plan $plan -Out $out | Select-Object -Last 1
if ($LASTEXITCODE -ne 0) { exit 1 }
# One sheet per view; a shot is named <what>_<stage>_<view><yaw>_<finish>.
$views = Get-ChildItem $out -Filter *.png | ForEach-Object { if ($_.BaseName -match '_((city|creation)_[a-z]+\d+)_') { $Matches[1] } } | Sort-Object -Unique
foreach ($view in $views) {
    $files = Get-ChildItem $out -Filter "*_${view}_*.png" | Sort-Object Name
    $finishes = ($files | ForEach-Object { ($_.BaseName -split '_')[-1] } | Sort-Object -Unique).Count
    $perRow = if ($Columns -gt 0) { $Columns } else { $finishes }
    $scale = if ($view -like 'city_head*') { 2 } elseif ($view -like 'city*') { 1 } else { 0.45 }
    $items = $files | ForEach-Object { ($_.BaseName -replace "_$view", '') + '=' + $_.FullName }
    & $python -I (Join-Path $root 'scripts\preview_sheet.py') (Join-Path $root '_tools\pylib') (Join-Path $root "previews\${Name}_$view.png") $perRow $scale @items
}
