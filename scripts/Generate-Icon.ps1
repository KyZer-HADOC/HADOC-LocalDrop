$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Drawing

$repoRoot = Split-Path -Parent $PSScriptRoot
$assetDir = Join-Path $repoRoot "src\HADOC.LocalDrop\Assets"
New-Item -ItemType Directory -Force -Path $assetDir | Out-Null

$pngPath = Join-Path $assetDir "HADOC-LocalDrop.png"
$icoPath = Join-Path $assetDir "HADOC-LocalDrop.ico"

$size = 256
$bitmap = New-Object System.Drawing.Bitmap(
    $size,
    $size,
    [System.Drawing.Imaging.PixelFormat]::Format32bppArgb
)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
$graphics.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit

function New-RoundedPath([float]$X, [float]$Y, [float]$Width, [float]$Height, [float]$Radius) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $diameter = $Radius * 2
    $path.AddArc($X, $Y, $diameter, $diameter, 180, 90)
    $path.AddArc($X + $Width - $diameter, $Y, $diameter, $diameter, 270, 90)
    $path.AddArc($X + $Width - $diameter, $Y + $Height - $diameter, $diameter, $diameter, 0, 90)
    $path.AddArc($X, $Y + $Height - $diameter, $diameter, $diameter, 90, 90)
    $path.CloseFigure()
    return $path
}

function Fill-Polygon($Brush, [System.Drawing.Point[]]$Points) {
    $graphics.FillPolygon($Brush, $Points)
}

try {
    $graphics.Clear([System.Drawing.Color]::Transparent)

    $tile = New-RoundedPath 9 9 238 238 38
    $graphics.SetClip($tile)

    $background = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        (New-Object System.Drawing.Rectangle(0, 0, 256, 256)),
        [System.Drawing.Color]::FromArgb(255, 7, 10, 16),
        [System.Drawing.Color]::FromArgb(255, 28, 12, 20),
        42
    )
    $graphics.FillRectangle($background, 0, 0, 256, 256)

    $glowBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(35, 238, 42, 62))
    $graphics.FillEllipse($glowBrush, 18, 20, 220, 170)

    # Device silhouettes sit behind the main transfer mark.
    $deviceFill = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 15, 22, 32))
    $deviceEdge = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(230, 194, 207, 224), 2.2)
    $screenFill = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 5, 9, 15))
    $redBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 244, 58, 70))
    $redPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(245, 244, 58, 70), 3.4)
    $silverPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(235, 222, 232, 244), 3.2)
    $silverBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 224, 232, 243))

    # Laptop.
    $graphics.FillRectangle($deviceFill, 28, 120, 76, 51)
    $graphics.DrawRectangle($deviceEdge, 28, 120, 76, 51)
    $graphics.FillRectangle($screenFill, 34, 126, 64, 39)
    $graphics.FillRectangle($redBrush, 52, 141, 11, 11)
    $graphics.FillRectangle($redBrush, 67, 141, 11, 11)
    $graphics.FillPolygon($deviceFill, [System.Drawing.Point[]]@(
        (New-Object System.Drawing.Point(22, 173)),
        (New-Object System.Drawing.Point(110, 173)),
        (New-Object System.Drawing.Point(101, 181)),
        (New-Object System.Drawing.Point(32, 181))
    ))
    $graphics.DrawLine($deviceEdge, 22, 173, 110, 173)

    # Phone.
    $phone = New-RoundedPath 177 119 49 70 10
    $graphics.FillPath($deviceFill, $phone)
    $graphics.DrawPath($deviceEdge, $phone)
    $phoneScreen = New-RoundedPath 182 127 39 53 5
    $graphics.FillPath($screenFill, $phoneScreen)
    $graphics.FillEllipse($redBrush, 198, 143, 8, 8)
    $graphics.FillRectangle($redBrush, 195, 154, 14, 13)

    # Wi-Fi signal.
    $wifiPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 245, 76, 84), 5.5)
    $wifiPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $wifiPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $graphics.DrawArc($wifiPen, 91, 19, 74, 74, 225, 90)
    $graphics.DrawArc($wifiPen, 104, 32, 48, 48, 225, 90)
    $graphics.DrawArc($wifiPen, 117, 45, 22, 22, 225, 90)
    $graphics.FillEllipse($redBrush, 124, 75, 8, 8)

    # Main metallic H mark.
    $metal = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        (New-Object System.Drawing.Rectangle(76, 77, 106, 112)),
        [System.Drawing.Color]::FromArgb(255, 255, 255, 255),
        [System.Drawing.Color]::FromArgb(255, 137, 157, 182),
        90
    )
    Fill-Polygon $metal ([System.Drawing.Point[]]@(
        (New-Object System.Drawing.Point(79, 78)),
        (New-Object System.Drawing.Point(103, 78)),
        (New-Object System.Drawing.Point(116, 177)),
        (New-Object System.Drawing.Point(92, 177))
    ))
    Fill-Polygon $metal ([System.Drawing.Point[]]@(
        (New-Object System.Drawing.Point(153, 78)),
        (New-Object System.Drawing.Point(177, 78)),
        (New-Object System.Drawing.Point(164, 177)),
        (New-Object System.Drawing.Point(140, 177))
    ))
    Fill-Polygon $metal ([System.Drawing.Point[]]@(
        (New-Object System.Drawing.Point(99, 118)),
        (New-Object System.Drawing.Point(160, 118)),
        (New-Object System.Drawing.Point(157, 141)),
        (New-Object System.Drawing.Point(102, 141))
    ))

    # Red transfer bolt / forward arrow crossing the H.
    $bolt = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        (New-Object System.Drawing.Rectangle(58, 72, 150, 120)),
        [System.Drawing.Color]::FromArgb(255, 255, 104, 111),
        [System.Drawing.Color]::FromArgb(255, 190, 24, 43),
        45
    )
    Fill-Polygon $bolt ([System.Drawing.Point[]]@(
        (New-Object System.Drawing.Point(57, 169)),
        (New-Object System.Drawing.Point(151, 93)),
        (New-Object System.Drawing.Point(140, 82)),
        (New-Object System.Drawing.Point(204, 72)),
        (New-Object System.Drawing.Point(177, 125)),
        (New-Object System.Drawing.Point(175, 108)),
        (New-Object System.Drawing.Point(78, 184))
    ))

    # A bright orbit links the PC and phone.
    $orbitPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(215, 245, 54, 68), 2.8)
    $graphics.DrawArc($orbitPen, 25, 114, 206, 91, 5, 170)
    $orbitHighlight = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(195, 225, 235, 246), 1.5)
    $graphics.DrawArc($orbitHighlight, 46, 132, 164, 62, 188, 150)

    # Brand wordmark.
    $brandFont = New-Object System.Drawing.Font("Segoe UI", 19, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
    $subFont = New-Object System.Drawing.Font("Segoe UI", 8, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
    $brandBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 244, 247, 252))
    $subBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 245, 73, 82))
    $format = New-Object System.Drawing.StringFormat
    $format.Alignment = [System.Drawing.StringAlignment]::Center
    $graphics.DrawString("HADOC", $brandFont, $brandBrush, [System.Drawing.RectangleF]::new(20, 190, 216, 27), $format)
    $graphics.DrawString("LOCALDROP  •  SHARE / CONNECT / TRANSFER", $subFont, $subBrush, [System.Drawing.RectangleF]::new(14, 219, 228, 15), $format)

    $graphics.ResetClip()

    # Neon border and subtle outer edge.
    $outerGlow = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(100, 245, 43, 61), 8)
    $outerGlow.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $graphics.DrawPath($outerGlow, $tile)
    $border = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 239, 55, 68), 2.5)
    $graphics.DrawPath($border, $tile)

    $bitmap.Save($pngPath, [System.Drawing.Imaging.ImageFormat]::Png)

    # ICO file with a PNG-compressed 256x256, 32-bit image.
    $pngBytes = [System.IO.File]::ReadAllBytes($pngPath)
    $memory = New-Object System.IO.MemoryStream
    $writer = New-Object System.IO.BinaryWriter($memory)
    $writer.Write([UInt16]0)
    $writer.Write([UInt16]1)
    $writer.Write([UInt16]1)
    $writer.Write([Byte]0)
    $writer.Write([Byte]0)
    $writer.Write([Byte]0)
    $writer.Write([Byte]0)
    $writer.Write([UInt16]1)
    $writer.Write([UInt16]32)
    $writer.Write([UInt32]$pngBytes.Length)
    $writer.Write([UInt32]22)
    $writer.Write($pngBytes)
    $writer.Flush()
    [System.IO.File]::WriteAllBytes($icoPath, $memory.ToArray())
    $writer.Dispose()
    $memory.Dispose()

    Write-Host "Generated app icon: $icoPath"
    Write-Host "Generated preview: $pngPath"
}
finally {
    if ($graphics) { $graphics.Dispose() }
    if ($bitmap) { $bitmap.Dispose() }
    foreach ($resource in @($tile, $background, $glowBrush, $deviceFill, $deviceEdge, $screenFill,
        $redBrush, $redPen, $silverPen, $silverBrush, $phone, $phoneScreen, $wifiPen, $metal,
        $bolt, $orbitPen, $orbitHighlight, $brandFont, $subFont, $brandBrush, $subBrush, $format,
        $outerGlow, $border)) {
        if ($null -ne $resource) { $resource.Dispose() }
    }
}
