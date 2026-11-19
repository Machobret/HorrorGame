$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$environment = Join-Path (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path 'Assets/Game/Environment'

function Build-Mask($family, $name, $cloth, $bulbs) {
    $folder = Join-Path $environment $family
    $base = [Drawing.Bitmap]::new((Join-Path $folder "${family}_Opaque_A_BaseColor.png"))
    $mask = [Drawing.Bitmap]::new($base.Width, $base.Height, [Drawing.Imaging.PixelFormat]::Format24bppRgb)
    try {
        foreach ($region in $cloth + $bulbs) {
            $left, $top, $right, $bottom = ($region -split ',' | ForEach-Object { [int]$_ })
            $isBulb = $bulbs -contains $region
            for ($y = $top; $y -lt $bottom; $y++) {
                for ($x = $left; $x -lt $right; $x++) {
                    $texel = $base.GetPixel($x, $y)
                    $value = if ($isBulb) {
                        [Drawing.Color]::White
                    } else {
                        [Drawing.Color]::FromArgb([int](48 + .78 * $texel.R), [int](48 + .78 * $texel.G),
                            [int](48 + .78 * $texel.B))
                    }
                    $mask.SetPixel($x, $y, $value)
                }
            }
        }
        $mask.Save((Join-Path $folder "${name}_Emission.png"), [Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $mask.Dispose()
        $base.Dispose()
    }
}

# Pixel rectangles come from the shade and bulb UV islands. Edges are measured from the top left.
# Each lamp family has its own copy of the mask and keeps the original shared albedo.
Build-Mask 'HouseholdDecor' 'VillageDeskLamp' @(
    # Cream shade, exterior and interior.
    '792,20,816,41', '975,322,997,344', '818,20,841,41', '884,175,905,201',
    '983,73,996,95', '270,16,292,28', '843,20,866,41', '956,66,978,67', '956,67,982,87',
    # Tan shade, exterior and interior.
    '677,33,699,63', '748,31,770,59', '724,30,746,59', '771,31,794,59',
    '971,247,988,270', '970,20,985,43', '700,33,723,63', '819,259,842,287'
) @(
    '344,203,350,210', '44,220,47,223', '57,60,60,64',
    '104,227,111,234', '56,4,59,6', '1,17,4,22'
)

Build-Mask 'LightingElectrical' 'HouseStandingLamp' @(
    # Floor shade, then table shade.
    '69,91,123,108', '1,90,49,108', '112,62,146,76', '75,72,111,84'
) @(
    '9,4,15,10', '113,4,118,10', '239,2,244,6', '67,2,72,6'
)
