$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$root = Split-Path -Parent $PSScriptRoot
$bitmap = [System.Drawing.Bitmap]::new(64, 64)
$g = [System.Drawing.Graphics]::FromImage($bitmap)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.Clear([System.Drawing.Color]::FromArgb(27, 46, 36))
$rope = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(213, 192, 152), 2)
$g.DrawBezier($rope, 0, 15, 18, 25, 46, 25, 64, 15)
$paper = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(248, 248, 237))
$g.FillRectangle($paper, 10, 24, 19, 25)
$g.FillRectangle($paper, 36, 25, 19, 25)
$mint = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(106, 177, 137))
$g.FillRectangle($mint, 13, 29, 13, 14)
$g.FillRectangle($mint, 39, 30, 13, 14)
$peg = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(218, 181, 125))
$g.FillRectangle($peg, 18, 18, 4, 12)
$g.FillRectangle($peg, 43, 19, 4, 12)
$icon = [System.Drawing.Icon]::FromHandle($bitmap.GetHicon())
$stream = [System.IO.File]::Create((Join-Path $root 'assets\hanger.ico'))
$icon.Save($stream)
$stream.Dispose()
$bitmap.Save((Join-Path $root 'assets\hanger.png'), [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bitmap.Dispose(); $rope.Dispose(); $paper.Dispose(); $mint.Dispose(); $peg.Dispose()
