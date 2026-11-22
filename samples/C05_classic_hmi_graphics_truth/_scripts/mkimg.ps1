param([Parameter(Mandatory)][string]$Dir)
# Writes the C05 test graphics (G_WMF.wmf is written by mkwmf.py): 120x80, a colored frame, a diagonal, a circle and the format name; PNG and GIF with a
# transparent background area, the others opaque. EMF via System.Drawing.Imaging.Metafile.
Add-Type -AssemblyName System.Drawing
function Draw($g, $label, $transparent) {
  if (-not $transparent) { $g.Clear([Drawing.Color]::FromArgb(255, 255, 240)) }
  $g.SmoothingMode = 'AntiAlias'
  $g.FillRectangle([Drawing.Brushes]::SteelBlue, 0, 0, 120, 14)
  $g.DrawRectangle((New-Object Drawing.Pen ([Drawing.Color]::DarkRed), 3), 2, 2, 116, 76)
  $g.DrawLine((New-Object Drawing.Pen ([Drawing.Color]::Green), 2), 4, 76, 116, 16)
  $g.FillEllipse([Drawing.Brushes]::Orange, 76, 34, 36, 36)
  $f = New-Object Drawing.Font 'Arial', 16, ([Drawing.FontStyle]::Bold), ([Drawing.GraphicsUnit]::Pixel)
  $g.DrawString($label, $f, [Drawing.Brushes]::Black, 8, 40)
  $g.DrawString('C05', (New-Object Drawing.Font 'Arial', 10, ([Drawing.FontStyle]::Regular), ([Drawing.GraphicsUnit]::Pixel)), [Drawing.Brushes]::White, 4, 1)
}
$fmts = @{ 'png' = 'Png'; 'jpg' = 'Jpeg'; 'bmp' = 'Bmp'; 'gif' = 'Gif'; 'tif' = 'Tiff' }
foreach ($e in $fmts.Keys) {
  $b = New-Object Drawing.Bitmap 120, 80; $g = [Drawing.Graphics]::FromImage($b)
  $tr = ($e -eq 'png' -or $e -eq 'gif'); if ($tr) { $g.Clear([Drawing.Color]::Transparent) }
  Draw $g $e.ToUpper() $tr; $g.Dispose()
  $b.Save("$Dir\G_$($e.ToUpper()).$e", [Drawing.Imaging.ImageFormat]::($fmts[$e])); $b.Dispose()
}
# EMF
$ref = New-Object Drawing.Bitmap 1, 1; $rg = [Drawing.Graphics]::FromImage($ref); $hdc = $rg.GetHdc()
$mf = New-Object Drawing.Imaging.Metafile "$Dir\G_EMF.emf", $hdc, (New-Object Drawing.Rectangle 0, 0, 120, 80), ([Drawing.Imaging.MetafileFrameUnit]::Pixel), ([Drawing.Imaging.EmfType]::EmfOnly)
$rg.ReleaseHdc($hdc); $rg.Dispose(); $ref.Dispose()
$g = [Drawing.Graphics]::FromImage($mf); Draw $g 'EMF' $false; $g.Dispose(); $mf.Dispose()
# SVG
@'
<svg xmlns="http://www.w3.org/2000/svg" width="120" height="80" viewBox="0 0 120 80">
  <rect x="0" y="0" width="120" height="80" fill="#fffff0"/>
  <rect x="0" y="0" width="120" height="14" fill="steelblue"/>
  <rect x="3.5" y="3.5" width="113" height="73" fill="none" stroke="darkred" stroke-width="3"/>
  <line x1="4" y1="76" x2="116" y2="16" stroke="green" stroke-width="2"/>
  <circle cx="94" cy="52" r="18" fill="orange"/>
  <text x="8" y="56" font-family="Arial" font-size="16" font-weight="bold" fill="black">SVG</text>
  <text x="4" y="11" font-family="Arial" font-size="10" fill="white">C05</text>
</svg>
'@ | Set-Content -Encoding UTF8 "$Dir\G_SVG.svg"
# a tall and a wide variant for the stretch modes
foreach ($v in @(@('G_TALL', 40, 100), @('G_WIDE', 160, 40))) {
  $b = New-Object Drawing.Bitmap $v[1], $v[2]; $g = [Drawing.Graphics]::FromImage($b); $g.Clear([Drawing.Color]::LightYellow)
  $g.DrawRectangle((New-Object Drawing.Pen ([Drawing.Color]::Blue), 3), 1, 1, $v[1] - 3, $v[2] - 3); $g.DrawLine([Drawing.Pens]::Red, 0, 0, $v[1], $v[2])
  $g.Dispose(); $b.Save("$Dir\$($v[0]).png", [Drawing.Imaging.ImageFormat]::Png); $b.Dispose()
}
Get-ChildItem $Dir | Select-Object Name, Length
