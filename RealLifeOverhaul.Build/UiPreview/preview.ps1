<#
Renders the interface kit offline into PNGs (not-in-repo/previews/ui by default): the mod's own Scripts/Ui files, game-free
ones only, are copied with this folder's runner, stand-ins and samples into UiAssets/UnityProject/Assets/Preview, and
Unity draws each sample screen with the real fonts from the built bundle, at 1920 x 1080 and 1280 x 720, dark and light.

    .\preview.ps1
    .\preview.ps1 -Out D:\somewhere -Backdrop D:\BigAmbitionsMod\not-in-repo\previews\ui\_reference\fullmenu_backdrop.png

Needs the font bundle built (UiAssets/build.ps1) and Unity 2022.3.62f2 with a graphics card: it renders with graphics,
so no -nographics. The backdrop is the game's own full menu image, read locally and never committed. Exit code 0 = every
shot written.
#>
param(
    [string]$Out = (Join-Path $PSScriptRoot '..\..\..\not-in-repo\previews\ui'),
    [string]$Backdrop = (Join-Path $PSScriptRoot '..\..\..\not-in-repo\previews\ui\_reference\fullmenu_backdrop.png'),
    [string]$Unity = 'C:\Program Files\Unity\Hub\Editor\2022.3.62f2\Editor\Unity.exe'
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$project = Join-Path $repo 'UiAssets\UnityProject'
$bundle = Join-Path $repo 'Assets\Mods\RealLifeOverhaul\Bundles\reallife_ui'
if (-not (Test-Path $Unity)) { Write-Host "Unity 2022.3.62f2 not found at $Unity (pass -Unity)."; exit 2 }
if (-not (Test-Path $bundle)) { Write-Host 'The font bundle is not built: run UiAssets\build.ps1 first.'; exit 2 }
if (-not (Test-Path (Join-Path $project 'Assets\TextMesh Pro\Resources\TMP Settings.asset'))) { Write-Host 'Run UiAssets\build.ps1 once first (it prepares the project).'; exit 2 }

# The kit's game-free files, as the mod ships them; the files that meet the game stay out.
$preview = Join-Path $project 'Assets\Preview'
if (Test-Path $preview) { Remove-Item -Recurse -Force $preview }
New-Item -ItemType Directory -Force (Join-Path $preview 'Kit') | Out-Null
New-Item -ItemType Directory -Force (Join-Path $preview 'Editor') | Out-Null
$gameFiles = @('UiGame*.cs', 'UiDirector*.cs', 'UiText.cs', 'UiGuide.cs')
Get-ChildItem (Join-Path $repo 'Assets\Mods\RealLifeOverhaul\Scripts\Ui') -Filter *.cs | Where-Object {
    $name = $_.Name
    -not ($gameFiles | Where-Object { $name -like $_ })
} | ForEach-Object { Copy-Item $_.FullName (Join-Path $preview 'Kit') }
Copy-Item (Join-Path $PSScriptRoot 'PreviewStubs.cs') (Join-Path $preview 'Kit')
Copy-Item (Join-Path $PSScriptRoot 'UiSamples.cs') (Join-Path $preview 'Kit')
Copy-Item (Join-Path $PSScriptRoot 'UiPreviewRunner.cs') (Join-Path $preview 'Editor')

New-Item -ItemType Directory -Force $Out | Out-Null
$Out = (Resolve-Path $Out).Path
$log = Join-Path $project 'Logs\preview.log'
New-Item -ItemType Directory -Force (Split-Path $log) | Out-Null
$arguments = @('-batchmode', '-quit', '-projectPath', "`"$project`"", '-executeMethod', 'UiPreviewRunner.Render', '-bundle', "`"$bundle`"", '-out', "`"$Out`"", '-logFile', "`"$log`"")
if (Test-Path $Backdrop) { $arguments += @('-backdrop', "`"$((Resolve-Path $Backdrop).Path)`"") }
$process = Start-Process -FilePath $Unity -ArgumentList $arguments -Wait -PassThru -NoNewWindow
if (Test-Path $log) { Select-String -Path $log -Pattern '^\[UiPreview\]' | ForEach-Object { $_.Line } }
if ($process.ExitCode -ne 0) {
    Write-Host "The preview failed (exit code $($process.ExitCode)). Full log: $log"
    if (Test-Path $log) { Select-String -Path $log -Pattern 'error CS|Exception|Scripts have compiler errors' | Select-Object -First 15 | ForEach-Object { $_.Line } }
    exit 1
}
Write-Host "OK: $Out"
