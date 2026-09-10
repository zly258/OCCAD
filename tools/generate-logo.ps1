param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\src\OCCAD.Avalonia\Assets')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$blue = [System.Drawing.Color]::FromArgb(47, 111, 159)
$white = [System.Drawing.Color]::White

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

function New-OccadLogoBitmap {
    param([int]$Size)

    $bitmap = [System.Drawing.Bitmap]::new($Size, $Size)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.Clear($blue)

    $scale = $Size / 256.0
    $pen = [System.Drawing.Pen]::new($white, [float](28 * $scale))
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round

    $graphics.DrawLine($pen, 76 * $scale, 72 * $scale, 176 * $scale, 72 * $scale)
    $graphics.DrawLine($pen, 76 * $scale, 72 * $scale, 76 * $scale, 184 * $scale)
    $graphics.DrawLine($pen, 76 * $scale, 184 * $scale, 176 * $scale, 184 * $scale)

    $gripPen = [System.Drawing.Pen]::new($white, [float](8 * $scale))
    $graphics.DrawRectangle(
        $gripPen,
        [float](178 * $scale),
        [float](119 * $scale),
        [float](25 * $scale),
        [float](25 * $scale))

    $gripPen.Dispose()
    $pen.Dispose()
    $graphics.Dispose()
    return $bitmap
}

$pngPath = Join-Path $OutputDirectory 'OCCAD.png'
$icoPath = Join-Path $OutputDirectory 'OCCAD.ico'

$bitmap = New-OccadLogoBitmap -Size 256
$bitmap.Save($pngPath, [System.Drawing.Imaging.ImageFormat]::Png)

# Store the generated PNG payload in a valid ICO container.
# Windows and Avalonia can scale the 256 px source for smaller icon sizes.
$pngBytes = [System.IO.File]::ReadAllBytes($pngPath)
$stream = [System.IO.File]::Create($icoPath)
$writer = [System.IO.BinaryWriter]::new($stream)
try {
    $writer.Write([uint16]0)      # reserved
    $writer.Write([uint16]1)      # icon
    $writer.Write([uint16]1)      # one image
    $writer.Write([byte]0)        # 256 px width
    $writer.Write([byte]0)        # 256 px height
    $writer.Write([byte]0)        # color count
    $writer.Write([byte]0)        # reserved
    $writer.Write([uint16]1)      # planes
    $writer.Write([uint16]32)     # bits per pixel
    $writer.Write([uint32]$pngBytes.Length)
    $writer.Write([uint32]22)     # payload offset
    $writer.Write($pngBytes)
}
finally {
    $writer.Dispose()
    $stream.Dispose()
    $bitmap.Dispose()
}

Write-Host "Generated: $pngPath"
Write-Host "Generated: $icoPath"
