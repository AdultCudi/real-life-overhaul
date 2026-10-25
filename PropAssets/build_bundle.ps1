<#
Builds one of the mod's prop asset bundles (sets.json) from its interchange folder, checks that it loads, and writes
what it holds to contents\<bundle>.json (scripts\contents.py), which is committed with it.

    .\build_bundle.ps1                       # the first set: PropAssets\build -> Assets\Mods\RealLifeOverhaul\Bundles\reallife_props
    .\build_bundle.ps1 -Set second           # PropAssets\build_2 -> Bundles\reallife_props_2
    .\build_bundle.ps1 -Set second -BuildFolder X -OutFolder Y    # elsewhere (the contents list goes beside the bundle)

Needs Unity 2022.3.62f2 (the game's version). Nothing of the game's is used: the bundle holds meshes,
textures and the catalogue only. Exit code 0 = built and verified.
#>
param(
    [string]$Set = '',
    [string]$BuildFolder = '',
    [string]$OutFolder = (Join-Path $PSScriptRoot '..\Assets\Mods\RealLifeOverhaul\Bundles'),
    [string]$Unity = 'C:\Program Files\Unity\Hub\Editor\2022.3.62f2\Editor\Unity.exe',
    [string]$Python = ''
)
$ErrorActionPreference = 'Stop'
$sets = (Get-Content (Join-Path $PSScriptRoot 'sets.json') -Raw | ConvertFrom-Json).sets
$entry = if ($Set) { $sets | Where-Object { $_.name -eq $Set } } else { $sets[0] }
if (-not $entry) { Write-Host "sets.json has no set '$Set'."; exit 2 }
if (-not $BuildFolder) { $BuildFolder = Join-Path $PSScriptRoot $entry.build }
$BundleName = $entry.bundle
if (-not (Test-Path $Unity)) { Write-Host "Unity 2022.3.62f2 not found at $Unity (pass -Unity)."; exit 2 }
if (-not (Test-Path (Join-Path $BuildFolder 'catalogue.json'))) { Write-Host "No catalogue.json in $BuildFolder. Run the Blender scripts first."; exit 2 }
if (-not $Python) {
    $Python = Get-Command python -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty Source
    if (-not $Python) {
        $Python = Get-ChildItem 'C:\Program Files\Blender Foundation' -Filter python.exe -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty FullName
    }
}

$project = Join-Path $PSScriptRoot 'UnityProject'
$BuildFolder = (Resolve-Path $BuildFolder).Path
New-Item -ItemType Directory -Force $OutFolder | Out-Null
$OutFolder = (Resolve-Path $OutFolder).Path
$shipping = $OutFolder -eq (Resolve-Path (Join-Path $PSScriptRoot '..\Assets\Mods\RealLifeOverhaul\Bundles')).Path
$logs = Join-Path $project 'Logs'
New-Item -ItemType Directory -Force $logs | Out-Null

function Invoke-Unity([string]$method, [string]$log) {
    $arguments = @('-batchmode', '-quit', '-nographics', '-burst-disable-compilation', '-projectPath', "`"$project`"", '-executeMethod', $method,
                   '-buildFolder', "`"$BuildFolder`"", '-outFolder', "`"$OutFolder`"", '-bundleName', $BundleName, '-logFile', "`"$log`"")
    $process = Start-Process -FilePath $Unity -ArgumentList $arguments -Wait -PassThru -NoNewWindow
    if (Test-Path $log) { Select-String -Path $log -Pattern '^\[PropBundle\]' | ForEach-Object { $_.Line } }
    if ($process.ExitCode -ne 0) {
        Write-Host "$method failed (exit code $($process.ExitCode)). Full log: $log"
        if (Test-Path $log) { Select-String -Path $log -Pattern 'error CS|Exception|Scripts have compiler errors|another Unity instance' | Select-Object -First 10 | ForEach-Object { $_.Line } }
        exit 1
    }
}

Invoke-Unity 'PropBundleBuilder.Build' (Join-Path $logs "build_$BundleName.log")
Invoke-Unity 'PropBundleBuilder.Verify' (Join-Path $logs "verify_$BundleName.log")
# Props shipped beside the first bundle while it could not be built (scripts/newer_props.py) are in it now: the mod would
# otherwise go on using those copies in place of the bundle's.
$newer = Join-Path $OutFolder 'props_newer'
if ($BundleName -eq $sets[0].bundle -and (Test-Path $newer)) { Remove-Item -Recurse -Force $newer; Write-Host "Removed $($newer), the bundle has those props now." }
# The committed list for a shipped bundle; beside the bundle for one built elsewhere (build.ps1 -Test).
$listed = @((Join-Path $PSScriptRoot 'scripts\contents.py'), $BuildFolder, (Join-Path $OutFolder $BundleName))
if (-not $shipping) { $listed += Join-Path $OutFolder "$BundleName.contents.json" }
$env:PROPS_SET = $entry.name
& $Python @listed
$status = $LASTEXITCODE
Remove-Item Env:PROPS_SET -ErrorAction SilentlyContinue
if ($status -ne 0) { Write-Host 'contents.py failed'; exit 1 }
Write-Host "OK: $(Join-Path $OutFolder $BundleName)"
