# World-space proportions at approximately 85 px/m: 6 x 5 m loading bays, 6 x 3 m upper bays.
param([string]$OutputPath=(Join-Path $PSScriptRoot '../../../Assets/Game/Environment/FillerBuildings/IndustrialDepot/IndustrialDepot_Atlas.png'))
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$source=[Drawing.Bitmap]::new((Join-Path $PSScriptRoot 'IndustrialDepot_Source.png'))
$coping=[Drawing.Bitmap]::new((Join-Path $PSScriptRoot 'GlazingAndCoping_Source.png'))
$atlas=[Drawing.Bitmap]::new(1024,1024)
$g=[Drawing.Graphics]::FromImage($atlas)
try {
    $g.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    function Region($image,$x,$y,$w,$h,$sx,$sy,$sw,$sh) {
        $g.DrawImage($image,[Drawing.Rectangle]::new($x,$y,$w,$h),$sx,$sy,$sw,$sh,[Drawing.GraphicsUnit]::Pixel)
    }
    foreach($column in 0,1){
        Region $source ($column*512) 0 512 427 ($column*768+3) 3 762 692
        Region $source ($column*512) 427 512 256 ($column*768+3) 699 762 322
    }
    # Solid upper cladding and metal flashing share the remaining region.
    Region $source 0 683 1024 341 4 60 760 530
    Region $coping 0 683 1024 32 630 252 620 52
    $atlas.Save([IO.Path]::GetFullPath($OutputPath),[Drawing.Imaging.ImageFormat]::Png)
} finally { $g.Dispose(); $atlas.Dispose(); $source.Dispose(); $coping.Dispose() }
