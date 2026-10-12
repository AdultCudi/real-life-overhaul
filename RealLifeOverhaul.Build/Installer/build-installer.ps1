# Builds the mod into a clean folder, then RealLifeOverhaul-<version>-setup.exe and RealLifeOverhaul-<version>.zip from it.
# Run from the repository root:  powershell -File RealLifeOverhaul.Build\Installer\build-installer.ps1 -OutDir D:\out
# Needs the .NET SDK and Inno Setup 6 (winget install JRSoftware.InnoSetup).
param(
    [Parameter(Mandatory = $true)][string]$OutDir,
    [string]$GameManaged = ''
)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$csproj = Join-Path $root 'RealLifeOverhaul.Build\RealLifeOverhaul.csproj'
$version = ([xml](Get-Content $csproj)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
if (-not $version) { throw 'No <Version> in RealLifeOverhaul.csproj.' }

$stage = Join-Path $OutDir 'stage'
if (Test-Path $stage) { Remove-Item -Recurse -Force $stage }
New-Item -ItemType Directory -Force $stage | Out-Null
$build = @('build', $csproj, '-c', 'Release', '-nologo', '-v', 'q', "-p:ModsLocal=$stage")
if ($GameManaged) { $build += "-p:GameManaged=$GameManaged" }
& dotnet @build
if ($LASTEXITCODE -ne 0) { throw 'The build failed.' }
$mod = Join-Path $stage 'RealLifeOverhaul'
if (-not (Test-Path (Join-Path $mod 'RealLifeOverhaul.dll'))) { throw "The build did not make $mod\RealLifeOverhaul.dll." }

$iscc = @("$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe", 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe', 'C:\Program Files\Inno Setup 6\ISCC.exe') |
    Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { throw 'Inno Setup 6 is not installed (winget install JRSoftware.InnoSetup).' }
& $iscc /Q "/DAppVersion=$version" "/DSourceDir=$stage" "/DOutputDir=$OutDir" (Join-Path $PSScriptRoot 'RealLifeOverhaul.iss')
if ($LASTEXITCODE -ne 0) { throw 'Inno Setup failed.' }

$zip = Join-Path $OutDir "RealLifeOverhaul-$version.zip"
if (Test-Path $zip) { Remove-Item $zip }
Compress-Archive -Path $mod -DestinationPath $zip

foreach ($f in @((Join-Path $OutDir "RealLifeOverhaul-$version-setup.exe"), $zip)) {
    '{0}  {1}  {2:N0} bytes' -f (Get-FileHash $f -Algorithm SHA256).Hash, (Split-Path $f -Leaf), (Get-Item $f).Length
}
