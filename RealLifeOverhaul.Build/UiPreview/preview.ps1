<#
Renders the interface kit offline into PNGs (not-in-repo/previews/ui by default): the mod's own Scripts/Ui files, game-free
ones only, are copied with this folder's runner, stand-ins and samples into UiAssets/UnityProject/Assets/Preview, and
Unity draws each sample screen with the real fonts from the built bundle, at 1920 x 1080 and 1280 x 720, dark and light,
over the game's whole full menu, its masthead and prompt band painted flat by the kit.

    .\preview.ps1
    .\preview.ps1 -Out D:\somewhere
    .\preview.ps1 -Only contacts # the screens whose name begins so

Needs the font bundle built (UiAssets/build.ps1) and Unity 2022.3.62f2 with a
graphics card: it renders with graphics, so no -nographics. Exit code 0 = every shot written.
#>
param(
    [string]$Out = (Join-Path $PSScriptRoot '..\..\..\not-in-repo\previews\ui'),
    [string]$Only = '',
    [string]$Set = '',          # cs2: the prototype's six screens over the game's world (needs -Backdrops)
    [string]$Backdrops = '',    # a folder of the game's own street pictures, local only, never committed
    [string]$Method = 'UiPreviewRunner.Render',   # another editor entry that draws into -Out (the 2.5D map's own pictures: Map25Preview.Render)
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
$gameFiles = @('UiGame*.cs', 'UiDirector*.cs', 'UiText.cs', 'UiGuide.cs', 'UiWindowPrefs.cs')
@('Stocks', 'Flare') | ForEach-Object { Get-ChildItem (Join-Path $repo (Join-Path 'Assets\Mods\RealLifeOverhaul\Scripts\Ui' $_)) -Filter *.cs -ErrorAction SilentlyContinue } | Where-Object {
    $_.Name -notlike '*Screen.cs' -and $_.Name -notlike '*Screen.*.cs' -and $_.Name -notlike '*Build.cs' -and $_.Name -notlike '*Build.*.cs'   # a screen's parts (FlareScreen.Fill.cs) meet the game too
} | ForEach-Object { Copy-Item $_.FullName (Join-Path $preview 'Kit') }
Get-ChildItem (Join-Path $repo 'Assets\Mods\RealLifeOverhaul\Scripts\Ui\Money') -Filter *.cs -ErrorAction SilentlyContinue | Where-Object { $_.Name -notlike '*Live.cs' -and $_.Name -notlike '*Live.*.cs' } | ForEach-Object { Copy-Item $_.FullName (Join-Path $preview 'Kit') }
Get-ChildItem (Join-Path $repo 'Assets\Mods\RealLifeOverhaul\Scripts\Ui\Work') -Filter *.cs -ErrorAction SilentlyContinue | Where-Object { $_.Name -notlike '*Live.cs' -and $_.Name -notlike '*Live.*.cs' } | ForEach-Object { Copy-Item $_.FullName (Join-Path $preview 'Kit') }
Get-ChildItem (Join-Path $repo 'Assets\Mods\RealLifeOverhaul\Scripts\Ui\Estate') -Filter *.cs -ErrorAction SilentlyContinue | Where-Object { $_.Name -notlike '*Live.cs' -and $_.Name -notlike '*Live.*.cs' } | ForEach-Object { Copy-Item $_.FullName (Join-Path $preview 'Kit') }
Get-ChildItem (Join-Path $repo 'Assets\Mods\RealLifeOverhaul\Scripts\Ui\Services') -Filter *.cs -ErrorAction SilentlyContinue | Where-Object { $_.Name -notlike '*Live.cs' -and $_.Name -notlike '*Live.*.cs' } | ForEach-Object { Copy-Item $_.FullName (Join-Path $preview 'Kit') }
Get-ChildItem (Join-Path $repo 'Assets\Mods\RealLifeOverhaul\Scripts\Ui\Tech') -Filter *.cs -ErrorAction SilentlyContinue | Where-Object { $_.Name -notlike '*Live.cs' -and $_.Name -notlike '*Live.*.cs' } | ForEach-Object { Copy-Item $_.FullName (Join-Path $preview 'Kit') }
Get-ChildItem (Join-Path $repo 'Assets\Mods\RealLifeOverhaul\Scripts\Ui\Construction') -Filter *.cs -ErrorAction SilentlyContinue | Where-Object { $_.Name -notlike '*Live.cs' -and $_.Name -notlike '*Live.*.cs' } | ForEach-Object { Copy-Item $_.FullName (Join-Path $preview 'Kit') }
# Build mode's panels (Ui\BuildMode): the model and the drawing; the face that meets the game and its hooks stay out.
Get-ChildItem (Join-Path $repo 'Assets\Mods\RealLifeOverhaul\Scripts\Ui\BuildMode') -Filter *.cs -ErrorAction SilentlyContinue | Where-Object { $_.Name -notlike 'BuildFace*.cs' -and $_.Name -ne 'BuildKits.cs' -and $_.Name -notlike 'UiDirector*.cs' } | ForEach-Object { Copy-Item $_.FullName (Join-Path $preview 'Kit') }
# The interior designer's panels (Ui\Designer): the model, the drawing and the rules; the face, the game's reads and the hooks stay out.
Get-ChildItem (Join-Path $repo 'Assets\Mods\RealLifeOverhaul\Scripts\Ui\Designer') -Filter *.cs -ErrorAction SilentlyContinue | Where-Object { $_.Name -notlike 'DesignerFace*.cs' -and $_.Name -notlike 'DesignerGame*.cs' -and $_.Name -ne 'DesignerKits.cs' -and $_.Name -ne 'DesignerPanes.cs' -and $_.Name -notlike 'DesignerTip*.cs' -and $_.Name -notlike 'UiDirector*.cs' } | ForEach-Object { Copy-Item $_.FullName (Join-Path $preview 'Kit') }
Get-ChildItem (Join-Path $repo 'Assets\Mods\RealLifeOverhaul\Scripts\Ui\Ventures') -Filter *.cs -ErrorAction SilentlyContinue | Where-Object { $_.Name -notlike '*Live.cs' -and $_.Name -notlike '*Live.*.cs' } | ForEach-Object { Copy-Item $_.FullName (Join-Path $preview 'Kit') }
# The showrooms' state and rules (game-free): the samples build the Car Showroom's one world from the real state and read its reports through the real functions.
@('DealerState.cs', 'DealerReport.cs', 'DealerCatalogue.cs', 'DealerRules.cs', 'DealerRules.Market.cs', 'DealerRules.Customers.cs', 'DealerRules.Business.cs', 'RivalRules.cs', 'ViewRules.cs', 'DealerDay.cs', 'BidEntry.cs') | ForEach-Object {
    Copy-Item (Join-Path $repo (Join-Path 'Assets\Mods\RealLifeOverhaul\Scripts\Economy\Dealer' $_)) (Join-Path $preview 'Kit')
}
Get-ChildItem (Join-Path $repo 'Assets\Mods\RealLifeOverhaul\Scripts\Ui\News') -Filter *.cs -ErrorAction SilentlyContinue | Where-Object { $_.Name -notlike 'NewsLive*.cs' -and $_.Name -ne 'NewsPeopleRules.cs' -and $_.Name -ne 'NewsReadingPage.cs' } | ForEach-Object { Copy-Item $_.FullName (Join-Path $preview 'Kit') }
Get-ChildItem (Join-Path $repo 'Assets\Mods\RealLifeOverhaul\Scripts\Ui\Company') -Filter *.cs -ErrorAction SilentlyContinue | Where-Object { $_.Name -ne 'CompanyLiveDoes.cs' -and $_.Name -notlike '*Live.cs' -and $_.Name -notlike '*Live.*.cs' -and $_.Name -notlike '*Screen.cs' } | ForEach-Object { Copy-Item $_.FullName (Join-Path $preview 'Kit') }
# The city map's 2.5D picture (Ui\Map) and its panels (Ui\MapDock): the game-free files of both; the live screens, the files that meet the game and the kits that bind them stay out.
@('Map', 'MapDock') | ForEach-Object { Get-ChildItem (Join-Path $repo (Join-Path 'Assets\Mods\RealLifeOverhaul\Scripts\Ui' $_)) -Filter *.cs -ErrorAction SilentlyContinue } | Where-Object { $_.Name -notlike '*Live.cs' -and $_.Name -notlike '*Live.*.cs' -and $_.Name -notlike '*Game.cs' -and $_.Name -notlike '*Screen.cs' -and $_.Name -ne 'MapKits.cs' -and $_.Name -ne 'MapRouteTag.cs' -and $_.Name -notlike 'UiDirector*.cs' } | ForEach-Object { Copy-Item $_.FullName (Join-Path $preview 'Kit') }
# The 2.5D map's made-up city and the owner's real footprints are preview and check code, kept out of the mod: they come from the shared build folder.
@('Map25Fixture.cs', 'Map25Real.cs') | ForEach-Object { Copy-Item (Join-Path $repo (Join-Path 'RealLifeOverhaul.Build\Shared' $_)) (Join-Path $preview 'Kit') }
Get-ChildItem (Join-Path $repo 'Assets\Mods\RealLifeOverhaul\Scripts\Ui') -Filter *.cs | Where-Object {
    $name = $_.Name
    -not ($gameFiles | Where-Object { $name -like $_ })
} | ForEach-Object { Copy-Item $_.FullName (Join-Path $preview 'Kit') }
Copy-Item (Join-Path $PSScriptRoot 'PreviewStubs.cs') (Join-Path $preview 'Kit')
# The phone's model and screen, and the pages' and folders' rules they stand on: game-free, outside the kit's own folder.
@('Scripts\Ui\Phone\PhoneItems.cs', 'Scripts\Ui\Phone\PhoneScreen.cs', 'Scripts\Core\GameUi\PagerRules.cs', 'Scripts\Core\GameUi\PhoneLayoutRules.cs', 'Scripts\Ui\Host\UiCost.cs', 'Scripts\Ui\Host\IUiGameWay.cs', 'Scripts\Driving\CarCatalogue.cs', 'Scripts\Navigation\NavPlaceRules.cs', 'Scripts\Photos\GalleryQuery.cs', 'Scripts\Photos\GalleryRules.cs', 'Scripts\Photos\PhotoStoreRules.cs', 'Scripts\Photos\PhotoStore.cs', 'Scripts\Photos\PhotoSidecarFile.cs', 'Scripts\Photos\PhotoSidecarRules.cs', 'Scripts\Photos\PhotoSeenRules.cs', 'Scripts\Photos\CameraRules.cs', 'Scripts\Core\PhotoThumbs.cs', 'Scripts\DaySeries\DaySeriesBook.cs', 'Scripts\DaySeries\DaySeriesCatalogue.cs') | ForEach-Object {
    Copy-Item (Join-Path $repo (Join-Path 'Assets\Mods\RealLifeOverhaul' $_)) (Join-Path $preview 'Kit')
}
# A Windows font with Chinese and Korean, for the shots in those languages: the game falls back to its own Noto fonts.
New-Item -ItemType Directory -Force (Join-Path $preview 'Fonts') | Out-Null
@('msyh.ttc', 'malgun.ttf') | ForEach-Object {
    $font = Join-Path $env:WINDIR (Join-Path 'Fonts' $_)
    if (Test-Path $font) { Copy-Item $font (Join-Path $preview 'Fonts') }
}
Get-ChildItem $PSScriptRoot -Filter 'UiSamples*.cs' | ForEach-Object { Copy-Item $_.FullName (Join-Path $preview 'Kit') }
Get-ChildItem $PSScriptRoot -Filter 'UiPreviewRunner*.cs' | ForEach-Object { Copy-Item $_.FullName (Join-Path $preview 'Editor') }

New-Item -ItemType Directory -Force $Out | Out-Null
$Out = (Resolve-Path $Out).Path
$log = Join-Path $project 'Logs\preview.log'
New-Item -ItemType Directory -Force (Split-Path $log) | Out-Null
$arguments = @('-batchmode', '-quit', '-projectPath', "`"$project`"", '-executeMethod', $Method, '-bundle', "`"$bundle`"", '-out', "`"$Out`"", '-logFile', "`"$log`"")
if ($Only) { $arguments += @('-only', $Only) }
if ($Set) { $arguments += @('-set', $Set) }
if ($Backdrops) { $arguments += @('-backdrops', "`"$Backdrops`"") }
$process = Start-Process -FilePath $Unity -ArgumentList $arguments -Wait -PassThru -NoNewWindow
if (Test-Path $log) { Select-String -Path $log -Pattern '^\[UiPreview\]' | ForEach-Object { $_.Line } }
if ($process.ExitCode -ne 0) {
    Write-Host "The preview failed (exit code $($process.ExitCode)). Full log: $log"
    if (Test-Path $log) { Select-String -Path $log -Pattern 'error CS|Exception|Scripts have compiler errors' | Select-Object -First 15 | ForEach-Object { $_.Line } }
    exit 1
}
Write-Host "OK: $Out"
