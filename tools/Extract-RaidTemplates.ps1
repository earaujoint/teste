param([Parameter(Mandatory=$true)][string]$Source)
Add-Type -AssemblyName System.Drawing
$outputDirectory = Join-Path $PSScriptRoot '../Macro/Assets/RaidDetection'
[System.IO.Directory]::CreateDirectory($outputDirectory) | Out-Null
$sourceImage = [System.Drawing.Bitmap]::FromFile((Resolve-Path -LiteralPath $Source))
try {
    # Exact pixel crops from the supplied 1919x1079 reference; retain text and button frame.
    foreach ($crop in @(
        @{ Name='team-reward.png'; X=562; Y=886; Width=390; Height=96 },
        @{ Name='ok.png'; X=968; Y=886; Width=390; Height=96 }
    )) {
        $rectangle = [System.Drawing.Rectangle]::new($crop.X, $crop.Y, $crop.Width, $crop.Height)
        $template = $sourceImage.Clone($rectangle, [System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
        try { $template.Save((Join-Path $outputDirectory $crop.Name), [System.Drawing.Imaging.ImageFormat]::Png) }
        finally { $template.Dispose() }
    }
} finally { $sourceImage.Dispose() }
