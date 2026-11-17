$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$layout=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'Storefronts_SquareLayout.json') -Raw | ConvertFrom-Json
$size=[int]$layout.size; $pad=[int]$layout.padding
$atlas=[Drawing.Bitmap]::new($size,$size,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
$regions=@()
foreach($e in $layout.regions) {
    $source=Join-Path $PSScriptRoot ('Sources/'+$e.source)
    $im=[Drawing.Bitmap]::new($source)
    $scaled=[Drawing.Bitmap]::new([int]$e.width,[int]$e.height,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g=[Drawing.Graphics]::FromImage($scaled)
    $g.CompositingMode=[Drawing.Drawing2D.CompositingMode]::SourceCopy
    $g.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode=[Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $attributes=[Drawing.Imaging.ImageAttributes]::new()
    $attributes.SetWrapMode([Drawing.Drawing2D.WrapMode]::TileFlipXY)
    $g.DrawImage($im,[Drawing.Rectangle]::new(0,0,$e.width,$e.height),0,0,$im.Width,$im.Height,[Drawing.GraphicsUnit]::Pixel,$attributes)
    $originalWidth=$im.Width; $originalHeight=$im.Height
    $g.Dispose(); $attributes.Dispose(); $im.Dispose()
    $width=[int]$e.width; $height=[int]$e.height; $paddedWidth=$width+2*$pad; $paddedHeight=$height+2*$pad
    $sd=$scaled.LockBits([Drawing.Rectangle]::new(0,0,$width,$height),[Drawing.Imaging.ImageLockMode]::ReadOnly,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $td=$atlas.LockBits([Drawing.Rectangle]::new(($e.x-$pad),($e.y-$pad),$paddedWidth,$paddedHeight),[Drawing.Imaging.ImageLockMode]::WriteOnly,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try {
        $line=New-Object byte[] ($width*4); $paddedLine=New-Object byte[] ($paddedWidth*4)
        for($row=0;$row-lt $paddedHeight;$row++) {
            $sourceRow=[Math]::Max(0,[Math]::Min($height-1,$row-$pad))
            [Runtime.InteropServices.Marshal]::Copy([IntPtr]::Add($sd.Scan0,$sourceRow*$sd.Stride),$line,0,$line.Length)
            [Array]::Copy($line,0,$paddedLine,$pad*4,$line.Length)
            for($p=0;$p-lt $pad;$p++) { [Array]::Copy($line,0,$paddedLine,$p*4,4); [Array]::Copy($line,$line.Length-4,$paddedLine,($pad+$width+$p)*4,4) }
            [Runtime.InteropServices.Marshal]::Copy($paddedLine,0,[IntPtr]::Add($td.Scan0,$row*$td.Stride),$paddedLine.Length)
        }
    } finally { $scaled.UnlockBits($sd); $atlas.UnlockBits($td); $scaled.Dispose() }
    $regions+=[ordered]@{source=$e.source; originalSize=@($originalWidth,$originalHeight); pixelsTopLeft=@($e.x,$e.y,$width,$height); unityUVBottomLeft=@(($e.x/[double]$size),(1-($e.y+$height)/[double]$size),($width/[double]$size),($height/[double]$size))}
}
$atlas.Save((Join-Path $PSScriptRoot 'Storefronts_SquareAtlas.png'),[Drawing.Imaging.ImageFormat]::Png)
@{width=$size;height=$size;padding=$pad;rotated=$false;rescaled=$true;regions=$regions} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'Storefronts_SquareAtlas.json') -Encoding UTF8
$review=Join-Path (Split-Path (Split-Path (Split-Path $PSScriptRoot))) 'Exports/Reviews/StorefrontAtlas'
$thumb=$atlas.GetThumbnailImage(1536,1536,$null,[IntPtr]::Zero)
$thumb.Save((Join-Path $review 'SquareAtlasPreview.png'),[Drawing.Imaging.ImageFormat]::Png)
$thumb.Dispose(); $atlas.Dispose()
Write-Output 'Saved square 4096x4096 atlas, 22 regions and updated UV metadata.'
