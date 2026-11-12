# Regenerates the PadakKey app icons from the default sparrow (SparrowIdle.png).
#   Icons/AppIcon/AppIcon-1024.png  macOS master (tile with Apple's ~10% margin); build.sh turns it into AppIcon.icns
#   Windows/PadakKey/PadakKey.ico   Windows multi-size icon (near full-bleed tile)
# Run from the repo root on Windows: powershell -ExecutionPolicy Bypass -File scripts/make-app-icon.ps1
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$root = Split-Path -Parent $PSScriptRoot
$source = [Drawing.Bitmap]::FromFile((Join-Path $root 'Sources/BarOTP/Resources/SparrowIdle.png'))

# Crop to the sparrow's opaque pixels so every size centers the bird, not the source canvas.
$minX = $source.Width; $minY = $source.Height; $maxX = -1; $maxY = -1
$data = $source.LockBits((New-Object Drawing.Rectangle 0, 0, $source.Width, $source.Height), 'ReadOnly', 'Format32bppArgb')
$bytes = New-Object byte[] ($data.Stride * $source.Height)
[Runtime.InteropServices.Marshal]::Copy($data.Scan0, $bytes, 0, $bytes.Length); $source.UnlockBits($data)
for ($y = 0; $y -lt $source.Height; $y++) { for ($x = 0; $x -lt $source.Width; $x++) {
    if ($bytes[$y * $data.Stride + $x * 4 + 3] -gt 16) { if ($x -lt $minX) { $minX = $x }; if ($x -gt $maxX) { $maxX = $x }; if ($y -lt $minY) { $minY = $y }; if ($y -gt $maxY) { $maxY = $y } }
} }
$bird = New-Object Drawing.Rectangle $minX, $minY, ($maxX - $minX + 1), ($maxY - $minY + 1)

function New-Tile([int]$size, [double]$margin) {
    $bitmap = New-Object Drawing.Bitmap $size, $size, ([Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [Drawing.Graphics]::FromImage($bitmap)
    $g.SmoothingMode = 'AntiAlias'; $g.InterpolationMode = 'HighQualityBicubic'; $g.PixelOffsetMode = 'HighQuality'; $g.Clear([Drawing.Color]::Transparent)
    $inset = $size * $margin; $side = $size - 2 * $inset; $radius = $side * 0.225
    $path = New-Object Drawing.Drawing2D.GraphicsPath
    $path.AddArc($inset, $inset, 2 * $radius, 2 * $radius, 180, 90)
    $path.AddArc($inset + $side - 2 * $radius, $inset, 2 * $radius, 2 * $radius, 270, 90)
    $path.AddArc($inset + $side - 2 * $radius, $inset + $side - 2 * $radius, 2 * $radius, 2 * $radius, 0, 90)
    $path.AddArc($inset, $inset + $side - 2 * $radius, 2 * $radius, 2 * $radius, 90, 90); $path.CloseFigure()
    $brush = New-Object Drawing.Drawing2D.LinearGradientBrush ((New-Object Drawing.PointF 0, $inset), (New-Object Drawing.PointF 0, ($inset + $side)), [Drawing.Color]::FromArgb(255, 253, 251, 246), [Drawing.Color]::FromArgb(255, 236, 231, 220))
    $g.FillPath($brush, $path)
    if ($size -ge 64) { $pen = New-Object Drawing.Pen ([Drawing.Color]::FromArgb(40, 0, 0, 0)), ([Math]::Max(1, $size / 256)); $g.DrawPath($pen, $path); $pen.Dispose() }
    # The bird fills ~76% of the tile; small sizes get a little more so the outline stays readable.
    $fill = if ($size -le 32) { 0.86 } else { 0.76 }
    $scale = $side * $fill / [Math]::Max($bird.Width, $bird.Height)
    $w = $bird.Width * $scale; $h = $bird.Height * $scale
    $g.DrawImage($source, (New-Object Drawing.RectangleF ($inset + ($side - $w) / 2), ($inset + ($side - $h) / 2), $w, $h), $bird, 'Pixel')
    $brush.Dispose(); $path.Dispose(); $g.Dispose(); return $bitmap
}
function Get-PngBytes([Drawing.Bitmap]$bitmap) { $stream = New-Object IO.MemoryStream; $bitmap.Save($stream, [Drawing.Imaging.ImageFormat]::Png); return ,$stream.ToArray() }
# Classic 32-bit DIB icon entry (BGRA bottom-up + empty AND mask); readable by every Windows icon loader.
function Get-DibBytes([Drawing.Bitmap]$bitmap) {
    $n = $bitmap.Width; $stream = New-Object IO.MemoryStream; $w = New-Object IO.BinaryWriter $stream
    $w.Write([uint32]40); $w.Write([int32]$n); $w.Write([int32](2 * $n)); $w.Write([uint16]1); $w.Write([uint16]32)
    $w.Write([uint32]0); $w.Write([uint32]0); $w.Write([int32]0); $w.Write([int32]0); $w.Write([uint32]0); $w.Write([uint32]0)
    $data = $bitmap.LockBits((New-Object Drawing.Rectangle 0, 0, $n, $n), 'ReadOnly', 'Format32bppArgb')
    $pixels = New-Object byte[] ($data.Stride * $n); [Runtime.InteropServices.Marshal]::Copy($data.Scan0, $pixels, 0, $pixels.Length); $bitmap.UnlockBits($data)
    for ($y = $n - 1; $y -ge 0; $y--) { $w.Write($pixels, $y * $data.Stride, $n * 4) }
    $maskRow = [int]([Math]::Ceiling($n / 32.0) * 4); $w.Write((New-Object byte[] ($maskRow * $n)))
    $w.Flush(); return ,$stream.ToArray()
}

$macDir = Join-Path $root 'Icons/AppIcon'; New-Item -ItemType Directory -Force $macDir | Out-Null
# Antivirus scanners can briefly hold a just-written file open, so retry instead of failing the whole run.
function Save-Bytes([string]$path, [byte[]]$bytes) {
    for ($attempt = 1; ; $attempt++) {
        try { Remove-Item $path -Force -ErrorAction SilentlyContinue; [IO.File]::WriteAllBytes($path, $bytes); return }
        catch { if ($attempt -ge 20) { throw }; Start-Sleep -Milliseconds 500 }
    }
}
$mac = New-Tile 1024 0.098; Save-Bytes (Join-Path $macDir 'AppIcon-1024.png') (Get-PngBytes $mac); $mac.Dispose()

# ICO: PNG-compressed 256px entry, 32-bit DIB entries for the smaller sizes.
$sizes = 16, 20, 24, 32, 40, 48, 64, 128, 256
$images = New-Object 'System.Collections.Generic.List[byte[]]'
foreach ($size in $sizes) { $tile = New-Tile $size 0.03; if ($size -ge 256) { $images.Add((Get-PngBytes $tile)) } else { $images.Add((Get-DibBytes $tile)) }; $tile.Dispose() }
$out = New-Object IO.MemoryStream; $writer = New-Object IO.BinaryWriter $out
$writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $dimension = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }
    $writer.Write([byte]$dimension); $writer.Write([byte]$dimension); $writer.Write([byte]0); $writer.Write([byte]0)
    $writer.Write([uint16]1); $writer.Write([uint16]32); $writer.Write([uint32]$images[$i].Length); $writer.Write([uint32]$offset)
    $offset += $images[$i].Length
}
foreach ($image in $images) { $writer.Write($image) }
Save-Bytes (Join-Path $root 'Windows/PadakKey/PadakKey.ico') $out.ToArray()
$source.Dispose()
Write-Output 'Wrote Icons/AppIcon/AppIcon-1024.png and Windows/PadakKey/PadakKey.ico'
