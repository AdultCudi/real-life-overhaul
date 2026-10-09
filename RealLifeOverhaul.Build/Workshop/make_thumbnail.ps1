# Makes the Workshop thumbnail from one of the player's own screenshots.
#
#   powershell -ExecutionPolicy Bypass -File RealLifeOverhaul.Build\Workshop\make_thumbnail.ps1
#
# Source: char_111653.png (1920x1080), an accident scene staged by the mod: the ambulance, a casualty on the
# stretcher, two paramedics, an officer and the cordon. A copy of it is in Workshop\gallery, which is what this
# script reads, so it runs on any machine.
# The square x 280..1360 of the picture is kept at full height. The game's top bar and the address line at the
# bottom fall under two dark bands; the upper band carries the title. Nothing is drawn over the scene itself.
# Result: Assets\Mods\RealLifeOverhaul\thumbnail.jpg, 1080x1080. The game's uploader takes the first .png, .jpg
# or .jpeg in the root of the mod folder that is no larger than its limit (1 MB), whatever its name, so this
# must stay the only picture there. The csproj's InstallMod target copies it to the mod folder.
param(
    [string]$Source = (Join-Path $PSScriptRoot 'gallery\01_accident_ambulance.png'),
    [string]$Target = (Join-Path $PSScriptRoot '..\..\Assets\Mods\RealLifeOverhaul\thumbnail.jpg'),
    [int]$Left = 280,
    [int]$Quality = 88
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$size = 1080
$topBand = 150
$bottomBand = 96

$shot = [System.Drawing.Image]::FromFile((Resolve-Path $Source))
if ($shot.Width -ne 1920 -or $shot.Height -ne 1080) { throw "The source must be 1920x1080, not $($shot.Width)x$($shot.Height)." }
$picture = New-Object System.Drawing.Bitmap $size, $size
$g = [System.Drawing.Graphics]::FromImage($picture)
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit

$g.DrawImage($shot, (New-Object System.Drawing.Rectangle 0, 0, $size, $size), (New-Object System.Drawing.Rectangle $Left, 0, $size, $size), [System.Drawing.GraphicsUnit]::Pixel)
$shot.Dispose()

$dark = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 16, 18, 24))
$orange = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 255, 148, 20))   # the colour of the mod's own markers
$white = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::White)
$soft = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 200, 204, 212))
$g.FillRectangle($dark, 0, 0, $size, $topBand)
$g.FillRectangle($orange, 0, $topBand, $size, 6)
$g.FillRectangle($dark, 0, $size - $bottomBand, $size, $bottomBand)
$g.FillRectangle($orange, 0, $size - $bottomBand - 6, $size, 6)

$centre = New-Object System.Drawing.StringFormat
$centre.Alignment = [System.Drawing.StringAlignment]::Center
$centre.LineAlignment = [System.Drawing.StringAlignment]::Center
$title = New-Object System.Drawing.Font 'Arial', 82, ([System.Drawing.FontStyle]::Bold), ([System.Drawing.GraphicsUnit]::Pixel)
$line = New-Object System.Drawing.Font 'Arial', 40, ([System.Drawing.FontStyle]::Regular), ([System.Drawing.GraphicsUnit]::Pixel)
$g.DrawString('Real Life Overhaul', $title, $white, (New-Object System.Drawing.RectangleF 0, 0, $size, $topBand), $centre)
$g.DrawString('A mod for Big Ambitions', $line, $soft, (New-Object System.Drawing.RectangleF 0, ($size - $bottomBand), $size, $bottomBand), $centre)
$g.Dispose()

$jpeg = [System.Drawing.Imaging.ImageCodecInfo]::GetImageEncoders() | Where-Object { $_.MimeType -eq 'image/jpeg' }
$options = New-Object System.Drawing.Imaging.EncoderParameters 1
$options.Param[0] = New-Object System.Drawing.Imaging.EncoderParameter ([System.Drawing.Imaging.Encoder]::Quality), ([long]$Quality)
$full = [System.IO.Path]::GetFullPath($Target)
$picture.Save($full, $jpeg, $options)
$picture.Dispose()

$bytes = (Get-Item $full).Length
if ($bytes -gt 1000000) { throw "The thumbnail is $bytes bytes: over the uploader's 1 MB limit. Lower -Quality." }
"$full  ${size}x${size}  $bytes bytes"
