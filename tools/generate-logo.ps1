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

$icoWriterSource = @'
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

public static class OccadIcoWriter
{
    public static void Write(string path, Bitmap[] frames)
    {
        if (frames == null || frames.Length == 0)
            throw new ArgumentException("At least one icon frame is required.", nameof(frames));

        const int headerSize = 6;
        const int entrySize = 16;
        var payloads = new List<byte[]>(frames.Length);

        foreach (var bitmap in frames)
            payloads.Add(BuildDib(bitmap));

        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);

        writer.Write((ushort)0);
        writer.Write((ushort)1);
        writer.Write((ushort)frames.Length);

        var offset = headerSize + entrySize * frames.Length;
        for (var index = 0; index < frames.Length; index++)
        {
            var bitmap = frames[index];
            if (bitmap.Width != bitmap.Height || bitmap.Width > 256)
                throw new ArgumentException("Icon frames must be square and no larger than 256 pixels.");

            writer.Write(bitmap.Width == 256 ? (byte)0 : (byte)bitmap.Width);
            writer.Write(bitmap.Height == 256 ? (byte)0 : (byte)bitmap.Height);
            writer.Write((byte)0);
            writer.Write((byte)0);
            writer.Write((ushort)1);
            writer.Write((ushort)32);
            writer.Write((uint)payloads[index].Length);
            writer.Write((uint)offset);
            offset += payloads[index].Length;
        }

        foreach (var payload in payloads)
            writer.Write(payload);
    }

    private static byte[] BuildDib(Bitmap bitmap)
    {
        var width = bitmap.Width;
        var height = bitmap.Height;
        var xorSize = checked(width * height * 4);
        var maskStride = ((width + 31) / 32) * 4;
        var maskSize = checked(maskStride * height);

        using var stream = new MemoryStream(40 + xorSize + maskSize);
        using var writer = new BinaryWriter(stream);

        writer.Write(40);
        writer.Write(width);
        writer.Write(height * 2);
        writer.Write((ushort)1);
        writer.Write((ushort)32);
        writer.Write(0);
        writer.Write(xorSize);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);

        for (var y = height - 1; y >= 0; y--)
        {
            for (var x = 0; x < width; x++)
            {
                var pixel = bitmap.GetPixel(x, y);
                writer.Write(pixel.B);
                writer.Write(pixel.G);
                writer.Write(pixel.R);
                writer.Write(pixel.A);
            }
        }

        writer.Write(new byte[maskSize]);
        return stream.ToArray();
    }
}
'@

if (-not ('OccadIcoWriter' -as [type])) {
    Add-Type -TypeDefinition $icoWriterSource -ReferencedAssemblies @([System.Drawing.Bitmap].Assembly.Location)
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

$frames = [System.Collections.Generic.List[System.Drawing.Bitmap]]::new()
try {
    foreach ($size in $iconSizes) {
        $frames.Add((New-OccadLogoBitmap -Size $size))
    }

    [OccadIcoWriter]::Write(
        $icoPath,
        $frames.ToArray())
}
finally {
    foreach ($frame in $frames) {
        $frame.Dispose()
    }
}

function Test-OccadIco {
    param([Parameter(Mandatory = $true)][string]$Path)

    $bytes = [System.IO.File]::ReadAllBytes($Path)
    if ($bytes.Length -lt 6) {
        throw "Generated ICO is truncated: $Path"
    }

    $reserved = [BitConverter]::ToUInt16($bytes, 0)
    $type = [BitConverter]::ToUInt16($bytes, 2)
    $count = [BitConverter]::ToUInt16($bytes, 4)
    if ($reserved -ne 0 -or $type -ne 1 -or $count -ne $iconSizes.Count) {
        throw "Generated ICO header is invalid: $Path"
    }

    $directoryEnd = 6 + 16 * $count
    if ($bytes.Length -lt $directoryEnd) {
        throw "Generated ICO directory is truncated: $Path"
    }

    for ($index = 0; $index -lt $count; $index++) {
        $entry = 6 + 16 * $index
        $length = [BitConverter]::ToUInt32($bytes, $entry + 8)
        $offset = [BitConverter]::ToUInt32($bytes, $entry + 12)
        if ($length -eq 0 -or
            $offset -lt $directoryEnd -or
            ([uint64]$offset + [uint64]$length) -gt [uint64]$bytes.Length) {
            throw "Generated ICO frame $index is invalid: $Path"
        }
    }
}

Test-OccadIco -Path $icoPath

Write-Host "[logo] PNG: $pngPath (1024x1024)"
Write-Host "[logo] ICO: $icoPath ($($iconSizes -join ', ') px)"
