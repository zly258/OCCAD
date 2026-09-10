param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\src\OCCAD.Avalonia\Assets')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$blue = [System.Drawing.Color]::FromArgb(47, 111, 159)
$white = [System.Drawing.Color]::White
$iconSizes = @(16, 24, 32, 48, 64, 128, 256)

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

function New-OccadLogoBitmap {
    param([Parameter(Mandatory = $true)][int]$Size)

    $bitmap = [System.Drawing.Bitmap]::new($Size, $Size)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
        $graphics.Clear($blue)

        $padding = [single]($Size * 0.22)
        $stroke = [single]([Math]::Max(2.0, $Size * 0.115))
        $diameter = [single]($Size - 2.0 * $padding)
        $rect = [System.Drawing.RectangleF]::new($padding, $padding, $diameter, $diameter)
        $pen = [System.Drawing.Pen]::new($white, $stroke)
        try {
            $graphics.DrawArc($pen, $rect, 40.0, 280.0)

            $radius = $diameter * 0.5
            $center = $Size * 0.5
            $capRadius = $stroke * 0.5
            foreach ($angle in @(40.0, 320.0)) {
                $radians = $angle * [Math]::PI / 180.0
                $x = $center + $radius * [Math]::Cos($radians)
                $y = $center + $radius * [Math]::Sin($radians)
                $graphics.FillEllipse(
                    [System.Drawing.Brushes]::White,
                    [single]($x - $capRadius),
                    [single]($y - $capRadius),
                    $stroke,
                    $stroke)
            }
        }
        finally {
            $pen.Dispose()
        }
    }
    finally {
        $graphics.Dispose()
    }

    return $bitmap
}

function Convert-ToPngBytes {
    param([Parameter(Mandatory = $true)][System.Drawing.Bitmap]$Bitmap)

    $stream = [System.IO.MemoryStream]::new()
    try {
        $Bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
        return $stream.ToArray()
    }
    finally {
        $stream.Dispose()
    }
}

$pngPath = Join-Path $OutputDirectory 'OCCAD.png'
$icoPath = Join-Path $OutputDirectory 'OCCAD.ico'

$master = New-OccadLogoBitmap -Size 1024
try {
    $master.Save($pngPath, [System.Drawing.Imaging.ImageFormat]::Png)
}
finally {
    $master.Dispose()
}

$frames = foreach ($size in $iconSizes) {
    $bitmap = New-OccadLogoBitmap -Size $size
    try {
        [PSCustomObject]@{
            Size = $size
            Bytes = Convert-ToPngBytes -Bitmap $bitmap
        }
    }
    finally {
        $bitmap.Dispose()
    }
}

$headerSize = 6
$entrySize = 16
$offset = $headerSize + $entrySize * $frames.Count
$stream = [System.IO.File]::Create($icoPath)
$writer = [System.IO.BinaryWriter]::new($stream)
try {
    $writer.Write([uint16]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]$frames.Count)

    foreach ($frame in $frames) {
        $dimension = if ($frame.Size -eq 256) { [byte]0 } else { [byte]$frame.Size }
        $writer.Write($dimension)
        $writer.Write($dimension)
        $writer.Write([byte]0)
        $writer.Write([byte]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]32)
        $writer.Write([uint32]$frame.Bytes.Length)
        $writer.Write([uint32]$offset)
        $offset += $frame.Bytes.Length
    }

    foreach ($frame in $frames) {
        $writer.Write($frame.Bytes)
    }
}
finally {
    $writer.Dispose()
    $stream.Dispose()
}

Write-Host "[logo] PNG: $pngPath (1024x1024)"
Write-Host "[logo] ICO: $icoPath ($($iconSizes -join ', ') px)"
