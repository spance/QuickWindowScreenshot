param(
    [string] $OutputDir = ""
)

$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $OutputDir = Join-Path $repoRoot 'src\QuickWindowScreenshot\Assets'
}

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

$svgPath = Join-Path $OutputDir 'QuickWindowScreenshot.svg'
$png1024Path = Join-Path $OutputDir 'QuickWindowScreenshot-1024.png'
$png256Path = Join-Path $OutputDir 'QuickWindowScreenshot-256.png'
$icoPath = Join-Path $OutputDir 'QuickWindowScreenshot.ico'

$svg = @'
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 1024 1024" role="img" aria-label="Quick Window Screenshot logo">
  <defs>
    <linearGradient id="bg" x1="132" y1="132" x2="892" y2="892" gradientUnits="userSpaceOnUse">
      <stop offset="0" stop-color="#0D9AA5"/>
      <stop offset="1" stop-color="#3D57E8"/>
    </linearGradient>
    <linearGradient id="focus" x1="410" y1="410" x2="614" y2="614" gradientUnits="userSpaceOnUse">
      <stop offset="0" stop-color="#C7FCFF"/>
      <stop offset="1" stop-color="#9AEBCF"/>
    </linearGradient>
  </defs>

  <rect x="96" y="96" width="832" height="832" rx="208" fill="url(#bg)"/>
  <rect x="128" y="128" width="768" height="768" rx="176" fill="none" stroke="#FFFFFF" stroke-width="20" opacity="0.14"/>

  <path d="M330 426V330h132M694 426V330H562M330 598v96h132M694 598v96H562"
        fill="none" stroke="#F7FCFF" stroke-width="62" stroke-linecap="round" stroke-linejoin="round"/>
  <path d="M330 330h132M330 330v96"
        fill="none" stroke="#76F4F4" stroke-width="24" stroke-linecap="round" opacity="0.82"/>

  <rect x="424" y="424" width="176" height="176" rx="44" fill="url(#focus)" opacity="0.96"/>

  <path d="M674 674l84 84" fill="none" stroke="#FFB44F" stroke-width="56" stroke-linecap="round"/>
  <path d="M684 684l56 56" fill="none" stroke="#FFF0B8" stroke-width="16" stroke-linecap="round" opacity="0.78"/>
</svg>
'@

Set-Content -LiteralPath $svgPath -Value $svg -Encoding UTF8

Add-Type -AssemblyName System.Drawing

function New-Color {
    param(
        [string] $Hex,
        [int] $Alpha = 255
    )

    $normalized = $Hex.TrimStart('#')
    return [System.Drawing.Color]::FromArgb(
        $Alpha,
        [Convert]::ToInt32($normalized.Substring(0, 2), 16),
        [Convert]::ToInt32($normalized.Substring(2, 2), 16),
        [Convert]::ToInt32($normalized.Substring(4, 2), 16))
}

function New-RoundedRectanglePath {
    param(
        [float] $X,
        [float] $Y,
        [float] $Width,
        [float] $Height,
        [float] $Radius
    )

    $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $diameter = $Radius * 2
    $path.AddArc($X, $Y, $diameter, $diameter, 180, 90)
    $path.AddArc($X + $Width - $diameter, $Y, $diameter, $diameter, 270, 90)
    $path.AddArc($X + $Width - $diameter, $Y + $Height - $diameter, $diameter, $diameter, 0, 90)
    $path.AddArc($X, $Y + $Height - $diameter, $diameter, $diameter, 90, 90)
    $path.CloseFigure()
    return $path
}

function New-RoundPen {
    param(
        [System.Drawing.Color] $Color,
        [float] $Width
    )

    $pen = [System.Drawing.Pen]::new($Color, $Width)
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    return $pen
}

function Draw-Corner {
    param(
        [System.Drawing.Graphics] $Graphics,
        [System.Drawing.Pen] $Pen,
        [float] $X1,
        [float] $Y1,
        [float] $X2,
        [float] $Y2,
        [float] $X3,
        [float] $Y3
    )

    $Graphics.DrawLines($Pen, [System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new($X1, $Y1),
        [System.Drawing.PointF]::new($X2, $Y2),
        [System.Drawing.PointF]::new($X3, $Y3)))
}

function New-LogoBitmap {
    param([int] $Size)

    $bitmap = [System.Drawing.Bitmap]::new($Size, $Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $graphics.Clear([System.Drawing.Color]::Transparent)
    $graphics.ScaleTransform($Size / 1024.0, $Size / 1024.0)

    $background = New-RoundedRectanglePath 96 96 832 832 208
    $backgroundBrush = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
        [System.Drawing.RectangleF]::new(96, 96, 832, 832),
        (New-Color '#0D9AA5'),
        (New-Color '#3D57E8'),
        [System.Drawing.Drawing2D.LinearGradientMode]::ForwardDiagonal)
    $graphics.FillPath($backgroundBrush, $background)

    $innerRing = New-RoundedRectanglePath 128 128 768 768 176
    $ringPen = [System.Drawing.Pen]::new((New-Color '#FFFFFF' 36), 20)
    $graphics.DrawPath($ringPen, $innerRing)

    $cornerPen = New-RoundPen (New-Color '#F7FCFF') 62
    Draw-Corner $graphics $cornerPen 330 426 330 330 462 330
    Draw-Corner $graphics $cornerPen 694 426 694 330 562 330
    Draw-Corner $graphics $cornerPen 330 598 330 694 462 694
    Draw-Corner $graphics $cornerPen 694 598 694 694 562 694

    $accentCornerPen = New-RoundPen (New-Color '#76F4F4' 210) 24
    $graphics.DrawLine($accentCornerPen, 330, 330, 462, 330)
    $graphics.DrawLine($accentCornerPen, 330, 330, 330, 426)

    $focus = New-RoundedRectanglePath 424 424 176 176 44
    $focusBrush = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
        [System.Drawing.RectangleF]::new(424, 424, 176, 176),
        (New-Color '#C7FCFF'),
        (New-Color '#9AEBCF'),
        [System.Drawing.Drawing2D.LinearGradientMode]::ForwardDiagonal)
    $graphics.FillPath($focusBrush, $focus)

    $speedPen = New-RoundPen (New-Color '#FFB44F') 56
    $graphics.DrawLine($speedPen, 674, 674, 758, 758)
    $shinePen = New-RoundPen (New-Color '#FFF0B8' 200) 16
    $graphics.DrawLine($shinePen, 684, 684, 740, 740)

    $graphics.Dispose()
    return $bitmap
}

function Save-Png {
    param(
        [System.Drawing.Bitmap] $Bitmap,
        [string] $Path
    )

    $Bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    $Bitmap.Dispose()
}

Save-Png (New-LogoBitmap 1024) $png1024Path
Save-Png (New-LogoBitmap 256) $png256Path

$iconSizes = @(16, 24, 32, 48, 64, 128, 256)
$entries = @()
$offset = 6 + ($iconSizes.Count * 16)
foreach ($size in $iconSizes) {
    $bitmap = New-LogoBitmap $size
    $stream = [System.IO.MemoryStream]::new()
    $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
    $bytes = $stream.ToArray()
    $entries += [PSCustomObject]@{
        Size = $size
        Bytes = $bytes
        Offset = $offset
    }
    $offset += $bytes.Length
    $stream.Dispose()
    $bitmap.Dispose()
}

$fileStream = [System.IO.File]::Open($icoPath, [System.IO.FileMode]::Create, [System.IO.FileAccess]::Write)
$writer = [System.IO.BinaryWriter]::new($fileStream)
try {
    $writer.Write([uint16]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]$entries.Count)
    foreach ($entry in $entries) {
        $dimension = if ($entry.Size -ge 256) { 0 } else { $entry.Size }
        $writer.Write([byte]$dimension)
        $writer.Write([byte]$dimension)
        $writer.Write([byte]0)
        $writer.Write([byte]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]32)
        $writer.Write([uint32]$entry.Bytes.Length)
        $writer.Write([uint32]$entry.Offset)
    }
    foreach ($entry in $entries) {
        $writer.Write([byte[]]$entry.Bytes)
    }
}
finally {
    $writer.Dispose()
    $fileStream.Dispose()
}

Write-Output "Generated $svgPath"
Write-Output "Generated $png1024Path"
Write-Output "Generated $png256Path"
Write-Output "Generated $icoPath"
