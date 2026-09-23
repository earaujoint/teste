param([Parameter(Mandatory=$true)][string]$Source)

Add-Type -AssemblyName System.Drawing
$outputDirectory = Join-Path $PSScriptRoot '../Macro/Assets/EnergySave'
[System.IO.Directory]::CreateDirectory($outputDirectory) | Out-Null
$sourceImage = [System.Drawing.Bitmap]::FromFile((Resolve-Path -LiteralPath $Source))
try {
    # Dragon face and blue eyes, avoiding the localized countdown/instruction text.
    $crop = [System.Drawing.Rectangle]::new(785, 250, 385, 340)
    $template = $sourceImage.Clone($crop, [System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
    try { $template.Save((Join-Path $outputDirectory 'energy-save.png'), [System.Drawing.Imaging.ImageFormat]::Png) }
    finally { $template.Dispose() }
} finally { $sourceImage.Dispose() }
