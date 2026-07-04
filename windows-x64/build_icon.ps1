# Builds AppIcon.ico from the macOS TextPad icon source (icon_base.png).
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$sourcePng = Join-Path (Split-Path -Parent $root) "macos\icon_base.png"
$assetsDir = Join-Path $root "TextPad\Assets"
$icoPath = Join-Path $assetsDir "AppIcon.ico"

if (-not (Test-Path $sourcePng)) {
    throw "Missing macOS icon source: $sourcePng"
}

New-Item -ItemType Directory -Force -Path $assetsDir | Out-Null
Add-Type -AssemblyName System.Drawing

function New-ResizedPngBytes {
    param([string]$Path, [int]$Size)

    $source = [System.Drawing.Image]::FromFile($Path)
    try {
        $bitmap = New-Object System.Drawing.Bitmap $Size, $Size
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        try {
            $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
            $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
            $graphics.DrawImage($source, 0, 0, $Size, $Size)
        }
        finally {
            $graphics.Dispose()
        }

        $stream = New-Object System.IO.MemoryStream
        try {
            $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
            return ,$stream.ToArray()
        }
        finally {
            $stream.Dispose()
        }
    }
    finally {
        $bitmap.Dispose()
        $source.Dispose()
    }
}

$sizes = @(16, 32, 48, 64, 128, 256)
$pngImages = foreach ($size in $sizes) {
    New-ResizedPngBytes -Path $sourcePng -Size $size
}

$stream = [System.IO.File]::Open($icoPath, [System.IO.FileMode]::Create, [System.IO.FileAccess]::Write)
try {
    $writer = New-Object System.IO.BinaryWriter $stream
    $writer.Write([UInt16]0)
    $writer.Write([UInt16]1)
    $writer.Write([UInt16]$sizes.Count)

    $offset = 6 + (16 * $sizes.Count)
    for ($i = 0; $i -lt $sizes.Count; $i++) {
        $size = $sizes[$i]
        $png = $pngImages[$i]
        $widthByte = if ($size -ge 256) { [byte]0 } else { [byte]$size }
        $writer.Write($widthByte)
        $writer.Write($widthByte)
        $writer.Write([byte]0)
        $writer.Write([byte]0)
        $writer.Write([UInt16]1)
        $writer.Write([UInt16]32)
        $writer.Write([UInt32]$png.Length)
        $writer.Write([UInt32]$offset)
        $offset += $png.Length
    }

    foreach ($png in $pngImages) {
        $writer.Write($png)
    }
}
finally {
    $stream.Dispose()
}

Write-Host "Created $icoPath" -ForegroundColor Green