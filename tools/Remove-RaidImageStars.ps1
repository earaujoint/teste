Add-Type -AssemblyName System.Drawing
$files = @('Macro\Assets\boss-slide-1.png', 'Macro\Assets\boss-slide-2.png')
foreach ($path in $files) {
    $resolvedPath = (Resolve-Path $path).Path
    $bitmap = [System.Drawing.Bitmap]::new($resolvedPath)
    try {
        $left = 12; $right = 65; $top = 64; $bottom = 120
        for ($y = $top; $y -le $bottom; $y++) {
            $a = $bitmap.GetPixel($left - 1, $y)
            $b = $bitmap.GetPixel($right + 1, $y)
            for ($x = $left; $x -le $right; $x++) {
                $t = ($x - $left + 1.0) / ($right - $left + 2.0)
                $r = [int][Math]::Round($a.R + ($b.R - $a.R) * $t)
                $g = [int][Math]::Round($a.G + ($b.G - $a.G) * $t)
                $bl = [int][Math]::Round($a.B + ($b.B - $a.B) * $t)
                $bitmap.SetPixel($x, $y, [System.Drawing.Color]::FromArgb($r, $g, $bl))
            }
        }
        $temporaryPath = "$resolvedPath.tmp.png"
        $bitmap.Save($temporaryPath, [System.Drawing.Imaging.ImageFormat]::Png)
    } finally { $bitmap.Dispose() }
    Move-Item -LiteralPath $temporaryPath -Destination $resolvedPath -Force
}
