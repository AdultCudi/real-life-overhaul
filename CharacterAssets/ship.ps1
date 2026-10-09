<#
After a garment is accepted: catalogue -> bundle (built and loaded back) -> the mod's build and its checks.

    .\ship.ps1                    # exit code 0 = bundle verified, 0 warnings, 0 errors, ALL CHECKS PASSED

Builds into a temporary folder: nothing is installed into the game.
#>
$ErrorActionPreference = 'Stop'
$env:Path = [Environment]::GetEnvironmentVariable('Path', 'Machine') + ';' + [Environment]::GetEnvironmentVariable('Path', 'User')
$root = $PSScriptRoot
$blender = Get-ChildItem 'C:\Program Files\Blender Foundation' -Filter blender.exe -Recurse | Sort-Object FullName -Descending | Select-Object -First 1 -ExpandProperty FullName
$python = Get-ChildItem (Split-Path $blender) -Filter python.exe -Recurse | Select-Object -First 1 -ExpandProperty FullName
& $python (Join-Path $root 'scripts\make_catalogue.py') | Select-Object -Last 1
& (Join-Path $root 'build_bundle.ps1') | Select-Object -Last 1
if ($LASTEXITCODE -ne 0) { exit 1 }
$repo = Split-Path $root
$build = dotnet build (Join-Path $repo 'RealLifeOverhaul.Build\RealLifeOverhaul.csproj') -c Release -nologo -v q "-p:ModsLocal=$(Join-Path $env:TEMP 'rlo_modslocal_cq')"
$build | Select-String 'Warning\(s\)|Error\(s\)|error|warning CS' | Select-Object -First 8
if (-not ($build -match '\b0 Warning\(s\)') -or -not ($build -match '\b0 Error\(s\)')) { Write-Host 'FAILED: the mod does not build clean'; exit 1 }
dotnet build (Join-Path $repo 'RealLifeOverhaul.Build\CatalogueCheck') -c Release -nologo -v q | Out-Null
$check = dotnet (Join-Path $repo 'RealLifeOverhaul.Build\CatalogueCheck\bin\Release\net10.0\CatalogueCheck.dll')
$check | Select-String 'FAIL' | Select-Object -First 8
$check | Select-Object -Last 1
if (-not ($check -match 'ALL CHECKS PASSED')) { exit 1 }
