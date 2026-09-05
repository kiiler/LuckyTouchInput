# Vector-equivalent icon renderer. No downloaded artwork or fonts are required.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
function Fill-RoundRect($Graphics, $Brush, [float]$X, [float]$Y, [float]$Width, [float]$Height, [float]$Radius) {
    $path = [Drawing.Drawing2D.GraphicsPath]::new()
    try {
        $diameter = 2 * $Radius
        $path.AddArc($X, $Y, $diameter, $diameter, 180, 90)
        $path.AddArc($X + $Width - $diameter, $Y, $diameter, $diameter, 270, 90)
        $path.AddArc($X + $Width - $diameter, $Y + $Height - $diameter, $diameter, $diameter, 0, 90)
        $path.AddArc($X, $Y + $Height - $diameter, $diameter, $diameter, 90, 90)
        $path.CloseFigure()
        $Graphics.FillPath($Brush, $path)
    }
    finally { $path.Dispose() }
}
$blue = [Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#0067c0'))
$white = [Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#eef4fb'))
$mint = [Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#78ead1'))
$frames = @()
try {
    foreach ($size in @(16, 32, 48, 64, 128, 256)) {
        $bitmap = [Drawing.Bitmap]::new($size, $size)
        $graphics = [Drawing.Graphics]::FromImage($bitmap)
        $stream = [IO.MemoryStream]::new()
        try {
            $graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
            $graphics.ScaleTransform($size / 256.0, $size / 256.0)
            Fill-RoundRect $graphics $blue 0 0 256 256 52
            Fill-RoundRect $graphics $white 32 60 192 144 22
            foreach ($y in @(84, 120)) {
                foreach ($x in @(52, 92, 132, 172)) { Fill-RoundRect $graphics $blue $x $y 32 24 6 }
            }
            Fill-RoundRect $graphics $blue 72 160 112 24 6
            $graphics.FillEllipse($mint, 180, 32, 40, 40)
            $bitmap.Save($stream, [Drawing.Imaging.ImageFormat]::Png)
            $frames += [pscustomobject]@{ Size = $size; Bytes = $stream.ToArray() }
        }
        finally { $stream.Dispose(); $graphics.Dispose(); $bitmap.Dispose() }
    }
    $path = Join-Path (Split-Path -Parent $PSScriptRoot) 'assets\icon.ico'
    $writer = [IO.BinaryWriter]::new([IO.File]::Create($path))
    try {
        $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$frames.Count)
        $offset = 6 + 16 * $frames.Count
        foreach ($frame in $frames) {
            $encodedSize = if ($frame.Size -eq 256) { 0 } else { $frame.Size }
            $writer.Write([byte]$encodedSize); $writer.Write([byte]$encodedSize)
            $writer.Write([byte]0); $writer.Write([byte]0)
            $writer.Write([uint16]1); $writer.Write([uint16]32)
            $writer.Write([uint32]$frame.Bytes.Length); $writer.Write([uint32]$offset)
            $offset += $frame.Bytes.Length
        }
        foreach ($frame in $frames) { $writer.Write([byte[]]$frame.Bytes) }
    }
    finally { $writer.Dispose() }
}
finally { $blue.Dispose(); $white.Dispose(); $mint.Dispose() }
