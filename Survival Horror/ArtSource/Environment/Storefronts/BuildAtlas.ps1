param([string]$SourceDirectory = 'C:\Users\manta\Downloads\buildings-apartments-shopfronts-shutters')
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$root = $PSScriptRoot
$sources = Join-Path $root 'Sources'
New-Item -ItemType Directory -Path $sources -Force | Out-Null
$names = @('building_5c.png','building_pub_old.png','building_showroom_vacant.png','restaurant_window.png','shopfront_neon.png','shop_front_top1.png') + @(2..17 | ForEach-Object { if ($_ -eq 17) { 'shop_front17_derelict.png' } else { "shop_front$_.png" } })
$padding = 16
$entries = @($names | ForEach-Object {
    $source = Join-Path $SourceDirectory $_
    $destination = Join-Path $sources $_
    if ([IO.Path]::GetFullPath($source) -ne [IO.Path]::GetFullPath($destination)) { Copy-Item -LiteralPath $source -Destination $destination }
    $im = [Drawing.Image]::FromFile($destination)
    $e = [pscustomobject]@{ name=$_; width=$im.Width; height=$im.Height; paddedWidth=($im.Width+2*$padding); paddedHeight=($im.Height+2*$padding); x=0; y=0 }
    $im.Dispose()
    $e
})
$entries = @($entries | Sort-Object @{Expression='paddedHeight';Descending=$true},@{Expression='paddedWidth';Descending=$true},name)
$atlasWidth = 8192
$atlasHeight = 4096
$x=0; $y=0; $shelfHeight=0
foreach ($e in $entries) {
    if ($x+$e.paddedWidth -gt $atlasWidth) { $x=0; $y+=$shelfHeight; $shelfHeight=0 }
    $e.x=$x+$padding; $e.y=$y+$padding
    $x+=$e.paddedWidth
    $shelfHeight=[Math]::Max($shelfHeight,$e.paddedHeight)
}
if ($y+$shelfHeight -gt $atlasHeight) { $atlasHeight=8192 }
$atlas = New-Object Drawing.Bitmap($atlasWidth,$atlasHeight,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [Drawing.Graphics]::FromImage($atlas)
$g.CompositingMode=[Drawing.Drawing2D.CompositingMode]::SourceCopy
$g.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
$g.PixelOffsetMode=[Drawing.Drawing2D.PixelOffsetMode]::Half
$g.Clear([Drawing.Color]::Transparent)
foreach ($e in $entries) {
    $im=[Drawing.Image]::FromFile((Join-Path $sources $e.name))
    $g.DrawImageUnscaled($im,$e.x,$e.y)
    # Extrude edge pixels into a 16-pixel gutter, including corners.
    for ($p=1; $p -le $padding; $p++) {
        $g.DrawImage($im,([Drawing.Rectangle]::new($e.x-$p,$e.y,1,$e.height)),0,0,1,$e.height,[Drawing.GraphicsUnit]::Pixel)
        $g.DrawImage($im,([Drawing.Rectangle]::new($e.x+$e.width-1+$p,$e.y,1,$e.height)),($e.width-1),0,1,$e.height,[Drawing.GraphicsUnit]::Pixel)
        $g.DrawImage($im,([Drawing.Rectangle]::new($e.x,$e.y-$p,$e.width,1)),0,0,$e.width,1,[Drawing.GraphicsUnit]::Pixel)
        $g.DrawImage($im,([Drawing.Rectangle]::new($e.x,$e.y+$e.height-1+$p,$e.width,1)),0,($e.height-1),$e.width,1,[Drawing.GraphicsUnit]::Pixel)
    }
    foreach ($corner in @(@(0,0,-$padding,-$padding),@(($e.width-1),0,$e.width,-$padding),@(0,($e.height-1),-$padding,$e.height),@(($e.width-1),($e.height-1),$e.width,$e.height))) {
        $g.DrawImage($im,([Drawing.Rectangle]::new(($e.x+$corner[2]),($e.y+$corner[3]),$padding,$padding)),$corner[0],$corner[1],1,1,[Drawing.GraphicsUnit]::Pixel)
    }
    $im.Dispose()
}
$g.Dispose()
$clear=[Drawing.Graphics]::FromImage($atlas)
$clear.Clear([Drawing.Color]::Transparent)
$clear.Dispose()
# Copy source pixels and clamped gutters directly, independent of source DPI.
foreach ($e in $entries) {
    $im=[Drawing.Bitmap]::new((Join-Path $sources $e.name))
    $sd=$im.LockBits([Drawing.Rectangle]::new(0,0,$e.width,$e.height),[Drawing.Imaging.ImageLockMode]::ReadOnly,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $td=$atlas.LockBits([Drawing.Rectangle]::new(($e.x-$padding),($e.y-$padding),$e.paddedWidth,$e.paddedHeight),[Drawing.Imaging.ImageLockMode]::WriteOnly,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try {
        $line=New-Object byte[] ($e.width*4)
        $paddedLine=New-Object byte[] ($e.paddedWidth*4)
        for ($row=0;$row -lt $e.paddedHeight;$row++) {
            $sourceRow=[Math]::Max(0,[Math]::Min($e.height-1,$row-$padding))
            [Runtime.InteropServices.Marshal]::Copy([IntPtr]::Add($sd.Scan0,$sourceRow*$sd.Stride),$line,0,$line.Length)
            [Array]::Copy($line,0,$paddedLine,$padding*4,$line.Length)
            for ($p=0;$p-lt $padding;$p++) {
                [Array]::Copy($line,0,$paddedLine,$p*4,4)
                [Array]::Copy($line,$line.Length-4,$paddedLine,($padding+$e.width+$p)*4,4)
            }
            [Runtime.InteropServices.Marshal]::Copy($paddedLine,0,[IntPtr]::Add($td.Scan0,$row*$td.Stride),$paddedLine.Length)
        }
    } finally { $im.UnlockBits($sd); $atlas.UnlockBits($td); $im.Dispose() }
}
$atlasPath=Join-Path $root 'Storefronts_Atlas.png'
$atlas.Save($atlasPath,[Drawing.Imaging.ImageFormat]::Png)
function Get-PixelHash($bitmap,$rectangle) {
    $data=$bitmap.LockBits($rectangle,[Drawing.Imaging.ImageLockMode]::ReadOnly,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try {
        $bytes=New-Object byte[] ($rectangle.Width*$rectangle.Height*4)
        for ($row=0; $row -lt $rectangle.Height; $row++) { [Runtime.InteropServices.Marshal]::Copy([IntPtr]::Add($data.Scan0,$row*$data.Stride),$bytes,$row*$rectangle.Width*4,$rectangle.Width*4) }
        $sha=[Security.Cryptography.SHA256]::Create()
        try { return [BitConverter]::ToString($sha.ComputeHash($bytes)) } finally { $sha.Dispose() }
    } finally { $bitmap.UnlockBits($data) }
}
$saved=[Drawing.Bitmap]::new($atlasPath)
$regions=@()
foreach ($e in $entries) {
    $im=[Drawing.Bitmap]::new((Join-Path $sources $e.name))
    $hash=Get-PixelHash $im ([Drawing.Rectangle]::new(0,0,$e.width,$e.height))
    $packedHash=Get-PixelHash $saved ([Drawing.Rectangle]::new($e.x,$e.y,$e.width,$e.height))
    $im.Dispose()
    if ($hash -ne $packedHash) { throw "Pixel mismatch: $($e.name)" }
    $regions+=[ordered]@{source=$e.name; pixelsTopLeft=@($e.x,$e.y,$e.width,$e.height); unityUVBottomLeft=@(($e.x/[double]$atlasWidth),(1-($e.y+$e.height)/[double]$atlasHeight),($e.width/[double]$atlasWidth),($e.height/[double]$atlasHeight)); sourceSHA256=(Get-FileHash -LiteralPath (Join-Path $sources $e.name) -Algorithm SHA256).Hash}
}
$saved.Dispose()
@{width=$atlasWidth;height=$atlasHeight;padding=$padding;rotated=$false;rescaled=$false;regions=$regions} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $root 'Storefronts_Atlas.json') -Encoding UTF8
$atlas.Dispose()
# Labeled review sheet is separate from the actual UV texture.
$review=Join-Path (Split-Path (Split-Path (Split-Path $root))) 'Exports/Reviews/StorefrontAtlas'
New-Item -ItemType Directory -Path $review -Force | Out-Null
$preview=New-Object Drawing.Bitmap(1400,([int][Math]::Ceiling($regions.Count/4.0)*210))
$pg=[Drawing.Graphics]::FromImage($preview); $pg.Clear([Drawing.Color]::FromArgb(35,35,35))
$font=New-Object Drawing.Font('Arial',11)
for ($i=0;$i -lt $regions.Count;$i++) {
    $r=$regions[$i]; $im=[Drawing.Image]::FromFile((Join-Path $sources $r.source)); $scale=[Math]::Min(338.0/$im.Width,175.0/$im.Height)
    $px=($i%4)*350; $py=[int][Math]::Floor($i/4.0)*210
    $pg.DrawImage($im,$px,$py,[int]($im.Width*$scale),[int]($im.Height*$scale))
    $pg.DrawString($r.source,$font,[Drawing.Brushes]::White,[single]$px,[single]($py+180)); $im.Dispose()
}
$preview.Save((Join-Path $review 'Storefronts_Index.jpg'),[Drawing.Imaging.ImageFormat]::Jpeg)
$pg.Dispose(); $font.Dispose(); $preview.Dispose()
Write-Output "Packed $($regions.Count) textures at native resolution into ${atlasWidth}x${atlasHeight}; every source region verified pixel-identical after PNG save."
