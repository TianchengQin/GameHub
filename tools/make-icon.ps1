# Generates GameHub.App/Assets/GameHub.ico (multi-size, PNG-compressed). Run: powershell -File tools/make-icon.ps1
Add-Type -AssemblyName System.Drawing

function New-IconBitmap([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.PixelOffsetMode = 'HighQuality'
    $g.Clear([System.Drawing.Color]::Transparent)
    $s = $size / 256.0

    # Rounded gradient tile
    $r = 56 * $s
    $tile = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $tile.AddArc(0, 0, $d, $d, 180, 90); $tile.AddArc($size - $d - 1, 0, $d, $d, 270, 90)
    $tile.AddArc($size - $d - 1, $size - $d - 1, $d, $d, 0, 90); $tile.AddArc(0, $size - $d - 1, $d, $d, 90, 90)
    $tile.CloseFigure()
    $grad = New-Object System.Drawing.Drawing2D.LinearGradientBrush ((New-Object System.Drawing.Point 0, 0), (New-Object System.Drawing.Point $size, $size), [System.Drawing.Color]::FromArgb(255, 30, 144, 255), [System.Drawing.Color]::FromArgb(255, 98, 70, 214))
    $g.FillPath($grad, $tile)

    # Gamepad body: wide rounded bar plus two grips
    $white = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(245, 255, 255, 255))
    $body = New-Object System.Drawing.Drawing2D.GraphicsPath
    $bx = 44 * $s; $by = 92 * $s; $bw = 168 * $s; $bh = 80 * $s; $br = 40 * $s
    $body.AddArc($bx, $by, $br * 2, $br * 2, 180, 90); $body.AddArc($bx + $bw - $br * 2, $by, $br * 2, $br * 2, 270, 90)
    $body.AddArc($bx + $bw - $br * 2, $by + $bh - $br * 2, $br * 2, $br * 2, 0, 90); $body.AddArc($bx, $by + $bh - $br * 2, $br * 2, $br * 2, 90, 90)
    $body.CloseFigure()
    $g.FillPath($white, $body)
    foreach ($gx in @(52, 156)) {
        $g.FillEllipse($white, $gx * $s, 128 * $s, 48 * $s, 76 * $s)
    }

    # D-pad (left) and buttons (right) cut out in the tile colour
    $ink = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 52, 60, 150))
    $g.FillRectangle($ink, 84 * $s, 118 * $s, 16 * $s, 44 * $s)
    $g.FillRectangle($ink, 70 * $s, 132 * $s, 44 * $s, 16 * $s)
    $g.FillEllipse($ink, 152 * $s, 112 * $s, 17 * $s, 17 * $s)
    $g.FillEllipse($ink, 172 * $s, 130 * $s, 17 * $s, 17 * $s)
    $g.FillEllipse($ink, 152 * $s, 148 * $s, 17 * $s, 17 * $s)
    $g.Dispose()
    return $bmp
}

$sizes = 16, 24, 32, 48, 64, 128, 256
$pngs = foreach ($sz in $sizes) {
    $bmp = New-IconBitmap $sz
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    , $ms.ToArray()
}

$outDir = Join-Path $PSScriptRoot '..\GameHub.App\Assets'
New-Item -ItemType Directory -Force $outDir | Out-Null
$ico = New-Object System.IO.MemoryStream
$w = New-Object System.IO.BinaryWriter $ico
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $sz = $sizes[$i]; $data = $pngs[$i]
    $dim = if ($sz -ge 256) { 0 } else { $sz }
    $w.Write([byte]$dim); $w.Write([byte]$dim); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([uint16]1); $w.Write([uint16]32); $w.Write([uint32]$data.Length); $w.Write([uint32]$offset)
    $offset += $data.Length
}
foreach ($data in $pngs) { $w.Write($data) }
$w.Flush()
[System.IO.File]::WriteAllBytes((Join-Path $outDir 'GameHub.ico'), $ico.ToArray())
[System.IO.File]::WriteAllBytes((Join-Path $outDir 'GameHub-256.png'), $pngs[$pngs.Count - 1])
"Wrote $(Join-Path $outDir 'GameHub.ico')"
