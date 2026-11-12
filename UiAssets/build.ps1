<#
Builds the interface's font bundle (reallife_ui) from the pinned fonts in fonts\ (README.md, "The fonts"):

  scripts\fonts.py  --> build\fonts (each weight as a static font, and each family's characters) and
                        Scripts\Ui\UiFontMetrics.Data.cs (the metrics the kit lays text out by, committed)
  Unity             --> Assets\Mods\RealLifeOverhaul\Bundles\reallife_ui (what ships), loaded back as a check,
                        and contents\reallife_ui.json, what it holds and what its atlases cost (committed with it)

    .\build.ps1                # everything
    .\build.ps1 -SkipBundle    # the metrics only

Needs Python 3 with fontTools (pip install fonttools) and Unity 2022.3.62f2 (the game's version), signed in through
Unity Hub. The game is not needed. Exit code 0 = built and verified.
#>
param(
    [switch]$SkipBundle,
    [string]$Unity = 'C:\Program Files\Unity\Hub\Editor\2022.3.62f2\Editor\Unity.exe',
    [string]$Python = ''
)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
if (-not $Python) {
    $Python = Get-Command python -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty Source
}
if (-not $Python) { Write-Host 'Python not found.'; exit 2 }

Write-Host '== Fonts (static weights, characters, metrics)'
& $Python -I (Join-Path $root 'scripts\fonts.py')
if ($LASTEXITCODE -ne 0) { Write-Host 'FAILED: fonts.py'; exit 1 }
if ($SkipBundle) { exit 0 }

if (-not (Test-Path $Unity)) { Write-Host "Unity 2022.3.62f2 not found at $Unity (pass -Unity)."; exit 2 }
$project = Join-Path $root 'UnityProject'
$logs = Join-Path $project 'Logs'
New-Item -ItemType Directory -Force $logs | Out-Null
$fonts = Join-Path $root 'build\fonts'
$licences = Join-Path $root 'fonts'
$out = (Resolve-Path (Join-Path $root '..\Assets\Mods\RealLifeOverhaul\Bundles')).Path
$contents = Join-Path $root 'contents\reallife_ui.json'

function Invoke-Unity([string]$method, [string]$log, [string[]]$extra) {
    $arguments = @('-batchmode', '-quit', '-nographics', '-burst-disable-compilation', '-projectPath', "`"$project`"", '-executeMethod', $method, '-logFile', "`"$log`"") + $extra
    $process = Start-Process -FilePath $Unity -ArgumentList $arguments -Wait -PassThru -NoNewWindow
    if (Test-Path $log) { Select-String -Path $log -Pattern '^\[UiBundle\]' | ForEach-Object { $_.Line } }
    if ($process.ExitCode -ne 0) {
        Write-Host "$method failed (exit code $($process.ExitCode)). Full log: $log"
        if (Test-Path $log) { Select-String -Path $log -Pattern 'error CS|Exception|Scripts have compiler errors|another Unity instance|licens' | Select-Object -First 12 | ForEach-Object { $_.Line } }
        exit 1
    }
}

$settings = Join-Path $project 'Assets\TextMesh Pro\Resources\TMP Settings.asset'
if (-not (Test-Path $settings)) {
    Write-Host '== Unity: the project opened once (its packages resolved)'
    Invoke-Unity 'UiFontBuilder.Prepare' (Join-Path $logs 'prepare.log') @()
    Write-Host '== TextMeshPro essentials'
    & $Python -I (Join-Path $root 'scripts\tmp_essentials.py') $project
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path $settings)) { Write-Host 'FAILED: TextMeshPro essential resources were not unpacked'; exit 1 }
}
Write-Host '== Unity: font assets and the bundle'
Invoke-Unity 'UiFontBuilder.Build' (Join-Path $logs 'build.log') @('-fontsFolder', "`"$fonts`"", '-outFolder', "`"$out`"", '-licences', "`"$licences`"")
Write-Host '== Unity: the bundle loaded back'
Invoke-Unity 'UiFontBuilder.Verify' (Join-Path $logs 'verify.log') @('-outFolder', "`"$out`"", '-contents', "`"$contents`"")
Write-Host "OK: $(Join-Path $out 'reallife_ui')"
